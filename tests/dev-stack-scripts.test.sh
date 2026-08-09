#!/usr/bin/env bash

set -euo pipefail

REPO_ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
TEST_ROOT=$(mktemp -d "${TMPDIR:-/tmp}/zach-dev-stack-test.XXXXXX")
FAKE_BIN="$TEST_ROOT/bin"
STATE_DIR="$TEST_ROOT/state"
mkdir -p "$FAKE_BIN"

PIDS=()

cleanup() {
  if [[ -x "$REPO_ROOT/stop-dev.sh" ]]; then
    DEV_STACK_STATE_DIR="$STATE_DIR" \
      DEV_STACK_API_PORT="${API_PORT:-1}" \
      DEV_STACK_LANDING_PORT="${LANDING_PORT:-2}" \
      DEV_STACK_DASHBOARD_PORT="${DASHBOARD_PORT:-3}" \
      DEV_STACK_STOP_GRACE_SECONDS=1 \
      "$REPO_ROOT/stop-dev.sh" >/dev/null 2>&1 || true
  fi

  for pid in "${PIDS[@]:-}"; do
    kill -- "$pid" >/dev/null 2>&1 || true
  done
  rm -rf "$TEST_ROOT"
}
trap cleanup EXIT

fail() {
  printf 'FAIL: %s\n' "$*" >&2
  exit 1
}

assert_file_nonempty() {
  [[ -s "$1" ]] || fail "expected non-empty file: $1"
}

assert_port_closed() {
  local port=$1
  if (exec 3<>"/dev/tcp/127.0.0.1/$port") 2>/dev/null; then
    exec 3>&-
    exec 3<&-
    fail "expected port $port to be closed"
  fi
}

wait_for_port() {
  local port=$1
  local attempts=0
  until (exec 3<>"/dev/tcp/127.0.0.1/$port") 2>/dev/null; do
    ((attempts += 1))
    (( attempts < 50 )) || fail "timed out waiting for port $port"
    sleep 0.1
  done
  exec 3>&-
  exec 3<&-
}

allocate_port() {
  python3 - <<'PY'
import socket
s = socket.socket()
s.bind(("127.0.0.1", 0))
print(s.getsockname()[1])
s.close()
PY
}

cat >"$FAKE_BIN/listener.py" <<'PY'
#!/usr/bin/env python3
import signal
import socketserver
import sys

class Handler(socketserver.BaseRequestHandler):
    def handle(self):
        self.request.recv(64)
        self.request.sendall(b"HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nOK")

server = socketserver.TCPServer(("127.0.0.1", int(sys.argv[1])), Handler)
signal.signal(signal.SIGTERM, lambda *_: sys.exit(0))
signal.signal(signal.SIGINT, lambda *_: sys.exit(0))
server.serve_forever(poll_interval=0.05)
PY
chmod +x "$FAKE_BIN/listener.py"

cat >"$FAKE_BIN/dotnet" <<'SH'
#!/usr/bin/env bash
if [[ "${DEV_STACK_FAKE_FAIL_API:-}" == "1" ]]; then
  echo "intentional API failure" >&2
  exit 7
fi
exec "$(dirname "$0")/listener.py" "$DEV_STACK_API_PORT"
SH
chmod +x "$FAKE_BIN/dotnet"

cat >"$FAKE_BIN/npm" <<'SH'
#!/usr/bin/env bash
port=$DEV_STACK_LANDING_PORT
for arg in "$@"; do
  if [[ "$arg" == "$DEV_STACK_DASHBOARD_PORT" ]]; then
    port=$DEV_STACK_DASHBOARD_PORT
  fi
done
exec "$(dirname "$0")/listener.py" "$port"
SH
chmod +x "$FAKE_BIN/npm"

API_PORT=$(allocate_port)
LANDING_PORT=$(allocate_port)
DASHBOARD_PORT=$(allocate_port)

while [[ "$LANDING_PORT" == "$API_PORT" ]]; do LANDING_PORT=$(allocate_port); done
while [[ "$DASHBOARD_PORT" == "$API_PORT" || "$DASHBOARD_PORT" == "$LANDING_PORT" ]]; do DASHBOARD_PORT=$(allocate_port); done

run_start() {
  DEV_STACK_STATE_DIR="$STATE_DIR" \
    DEV_STACK_API_PORT="$API_PORT" \
    DEV_STACK_LANDING_PORT="$LANDING_PORT" \
    DEV_STACK_DASHBOARD_PORT="$DASHBOARD_PORT" \
    DEV_STACK_API_HEADSTART_SECONDS=0 \
    DEV_STACK_READINESS_TIMEOUT_SECONDS=5 \
    DEV_STACK_DOTNET_BIN="$FAKE_BIN/dotnet" \
    DEV_STACK_NPM_BIN="$FAKE_BIN/npm" \
    "$REPO_ROOT/start-dev.sh"
}

run_stop() {
  DEV_STACK_STATE_DIR="$STATE_DIR" \
    DEV_STACK_API_PORT="$API_PORT" \
    DEV_STACK_LANDING_PORT="$LANDING_PORT" \
    DEV_STACK_DASHBOARD_PORT="$DASHBOARD_PORT" \
    DEV_STACK_STOP_GRACE_SECONDS=1 \
    "$REPO_ROOT/stop-dev.sh"
}

[[ -x "$REPO_ROOT/start-dev.sh" ]] || fail "start-dev.sh must be executable"
[[ -x "$REPO_ROOT/stop-dev.sh" ]] || fail "stop-dev.sh must be executable"

start_output=$(run_start) || fail "initial start failed"
[[ "$start_output" == *"http://localhost:$API_PORT"* ]] || fail "missing API URL"
[[ "$start_output" == *"http://localhost:$LANDING_PORT"* ]] || fail "missing Landing URL"
[[ "$start_output" == *"http://localhost:$DASHBOARD_PORT"* ]] || fail "missing Dashboard URL"

for service in api landing dashboard; do
  assert_file_nonempty "$STATE_DIR/$service.log"
  [[ $(<"$STATE_DIR/$service.pid") =~ ^[1-9][0-9]*$ ]] || fail "invalid PID file for $service"
done

if run_start >/dev/null 2>&1; then
  fail "duplicate start unexpectedly succeeded"
fi

stop_output=$(run_stop) || fail "tracked stop failed"
[[ "$stop_output" == *"Stopped"* ]] || fail "stop output did not report work"
for port in "$API_PORT" "$LANDING_PORT" "$DASHBOARD_PORT"; do assert_port_closed "$port"; done
[[ ! -e "$STATE_DIR/api.pid" && ! -e "$STATE_DIR/landing.pid" && ! -e "$STATE_DIR/dashboard.pid" ]] || fail "PID metadata remains after stop"

second_stop_output=$(run_stop) || fail "idempotent stop failed"
[[ "$second_stop_output" == *"already stopped"* ]] || fail "second stop did not report idempotence"

if DEV_STACK_FAKE_FAIL_API=1 run_start >"$TEST_ROOT/failure.out" 2>&1; then
  fail "failing service start unexpectedly succeeded"
fi
grep -q "API failed" "$TEST_ROOT/failure.out" || fail "startup failure did not identify API"
grep -q "api.log" "$TEST_ROOT/failure.out" || fail "startup failure did not identify log"
for port in "$API_PORT" "$LANDING_PORT" "$DASHBOARD_PORT"; do assert_port_closed "$port"; done
[[ ! -e "$STATE_DIR/api.pid" && ! -e "$STATE_DIR/landing.pid" && ! -e "$STATE_DIR/dashboard.pid" ]] || fail "rollback left PID metadata"

DEV_STACK_API_PORT="$API_PORT" "$FAKE_BIN/dotnet" run & PIDS+=("$!")
DEV_STACK_LANDING_PORT="$LANDING_PORT" DEV_STACK_DASHBOARD_PORT="$DASHBOARD_PORT" "$FAKE_BIN/npm" run dev & PIDS+=("$!")
DEV_STACK_LANDING_PORT="$LANDING_PORT" DEV_STACK_DASHBOARD_PORT="$DASHBOARD_PORT" "$FAKE_BIN/npm" run dev -- -p "$DASHBOARD_PORT" & PIDS+=("$!")
for port in "$API_PORT" "$LANDING_PORT" "$DASHBOARD_PORT"; do wait_for_port "$port"; done
mkdir -p "$STATE_DIR"
printf '999999\n' >"$STATE_DIR/api.pid"

fallback_output=$(run_stop) || fail "port fallback stop failed"
[[ "$fallback_output" == *"Stopped"* ]] || fail "fallback stop did not report work"
for port in "$API_PORT" "$LANDING_PORT" "$DASHBOARD_PORT"; do assert_port_closed "$port"; done

printf 'PASS: dev-stack shell lifecycle\n'
