#!/usr/bin/env bash

set -euo pipefail

REPO_ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
STATE_DIR=${DEV_STACK_STATE_DIR:-"$REPO_ROOT/temp/dev-stack"}
API_PORT=${DEV_STACK_API_PORT:-5236}
LANDING_PORT=${DEV_STACK_LANDING_PORT:-3000}
DASHBOARD_PORT=${DEV_STACK_DASHBOARD_PORT:-3001}
API_HEADSTART_SECONDS=${DEV_STACK_API_HEADSTART_SECONDS:-3}
READINESS_TIMEOUT_SECONDS=${DEV_STACK_READINESS_TIMEOUT_SECONDS:-30}
DOTNET_REQUESTED=${DEV_STACK_DOTNET_BIN:-dotnet}
NPM_REQUESTED=${DEV_STACK_NPM_BIN:-npm}

LAUNCHED_NAMES=()
LAUNCHED_PIDS=()

die() {
  printf 'Error: %s\n' "$*" >&2
  exit 1
}

validate_integer() {
  local label=$1 value=$2 minimum=$3 maximum=$4
  [[ "$value" =~ ^[0-9]+$ ]] || die "$label must be an integer"
  (( value >= minimum && value <= maximum )) || die "$label must be between $minimum and $maximum"
}

validate_configuration() {
  validate_integer 'API port' "$API_PORT" 1 65535
  validate_integer 'Landing port' "$LANDING_PORT" 1 65535
  validate_integer 'Dashboard port' "$DASHBOARD_PORT" 1 65535
  validate_integer 'API head-start duration' "$API_HEADSTART_SECONDS" 0 300
  validate_integer 'Readiness timeout' "$READINESS_TIMEOUT_SECONDS" 1 300
  [[ "$API_PORT" != "$LANDING_PORT" && "$API_PORT" != "$DASHBOARD_PORT" && "$LANDING_PORT" != "$DASHBOARD_PORT" ]] || die 'service ports must be distinct'
}

resolve_executable() {
  local requested=$1 resolved
  if [[ "$requested" == */* ]]; then
    [[ -x "$requested" ]] || die "executable is not runnable: $requested"
    printf '%s\n' "$requested"
    return
  fi

  resolved=$(command -v -- "$requested" 2>/dev/null || true)
  [[ -n "$resolved" && -x "$resolved" ]] || die "executable not found: $requested"
  printf '%s\n' "$resolved"
}

pid_is_valid() {
  [[ "$1" =~ ^[1-9][0-9]*$ ]]
}

pid_is_live() {
  pid_is_valid "$1" && kill -0 -- "$1" 2>/dev/null
}

listener_pids() {
  local port=$1 raw pid
  if command -v lsof >/dev/null 2>&1; then
    raw=$(lsof -nP -t -iTCP:"$port" -sTCP:LISTEN 2>/dev/null || true)
  elif command -v fuser >/dev/null 2>&1; then
    raw=$(fuser -n tcp "$port" 2>/dev/null || true)
  else
    raw=''
  fi

  while IFS= read -r pid; do
    pid_is_valid "$pid" && printf '%s\n' "$pid"
  done < <(printf '%s\n' "$raw" | tr '[:space:]' '\n')
}

port_is_listening() {
  local port=$1
  if [[ -n "$(listener_pids "$port")" ]]; then
    return 0
  fi
  (exec 3<>"/dev/tcp/127.0.0.1/$port") 2>/dev/null
}

terminate_tree() {
  local pid=$1 child
  pid_is_valid "$pid" || return 0
  if command -v pgrep >/dev/null 2>&1; then
    while IFS= read -r child; do
      terminate_tree "$child"
    done < <(pgrep -P "$pid" 2>/dev/null || true)
  fi
  kill -TERM -- "$pid" 2>/dev/null || true
}

remove_own_pid_file() {
  local name pid pid_file recorded=''
  name=$1
  pid=$2
  pid_file="$STATE_DIR/$name.pid"
  [[ -f "$pid_file" ]] || return 0
  recorded=$(<"$pid_file")
  [[ "$recorded" == "$pid" ]] && rm -f -- "$pid_file"
}

rollback() {
  local index pid name
  for ((index = ${#LAUNCHED_PIDS[@]} - 1; index >= 0; index--)); do
    pid=${LAUNCHED_PIDS[index]}
    name=${LAUNCHED_NAMES[index]}
    terminate_tree "$pid"
    remove_own_pid_file "$name" "$pid"
  done
}

fail_service() {
  local name=$1
  printf '%s failed to start or become ready. See log: %s/%s.log\n' "$name" "$STATE_DIR" "$(printf '%s' "$name" | tr '[:upper:]' '[:lower:]')" >&2
  rollback
  exit 1
}

check_pid_file() {
  local name pid_file pid
  name=$1
  pid_file="$STATE_DIR/$name.pid"
  [[ -f "$pid_file" ]] || return 0
  pid=$(<"$pid_file")
  if pid_is_live "$pid"; then
    die "$name is already running (tracked PID $pid). Run ./stop-dev.sh first."
  fi
  rm -f -- "$pid_file"
}

launch_service() {
  local name workdir executable log_file pid_file pid temporary
  name=$1
  workdir=$2
  executable=$3
  log_file=$4
  pid_file=$5
  shift 5
  printf 'Launching %s at %s\n' "$name" "$(date -u +'%Y-%m-%dT%H:%M:%SZ')" >>"$log_file"
  (
    cd "$workdir"
    exec nohup "$executable" "$@" >>"$log_file" 2>&1 < /dev/null
  ) &
  pid=$!
  temporary="$pid_file.tmp.$$"
  printf '%s\n' "$pid" >"$temporary"
  mv -f -- "$temporary" "$pid_file"
  LAUNCHED_NAMES+=("$name")
  LAUNCHED_PIDS+=("$pid")
}

wait_for_service() {
  local name=$1 port=$2 pid=$3 deadline=$((SECONDS + READINESS_TIMEOUT_SECONDS))
  while (( SECONDS < deadline )); do
    port_is_listening "$port" && return 0
    pid_is_live "$pid" || fail_service "$name"
    sleep 0.1
  done
  fail_service "$name"
}

validate_configuration
umask 077
mkdir -p -- "$STATE_DIR"
chmod 700 -- "$STATE_DIR"

API_DIR="$REPO_ROOT/API/ZachHairStudio.Api"
LANDING_DIR="$REPO_ROOT/landing-page"
DASHBOARD_DIR="$REPO_ROOT/dashboard"
for directory in "$API_DIR" "$LANDING_DIR" "$DASHBOARD_DIR"; do
  [[ -d "$directory" ]] || die "required service directory is missing: $directory"
done

DOTNET_BIN=$(resolve_executable "$DOTNET_REQUESTED")
NPM_BIN=$(resolve_executable "$NPM_REQUESTED")

for service in api landing dashboard; do
  check_pid_file "$service"
done
for port in "$API_PORT" "$LANDING_PORT" "$DASHBOARD_PORT"; do
  port_is_listening "$port" && die "port $port is already listening; refusing duplicate startup"
done

trap 'rollback; exit 130' INT TERM

launch_service 'api' "$API_DIR" "$DOTNET_BIN" "$STATE_DIR/api.log" "$STATE_DIR/api.pid" run
sleep "$API_HEADSTART_SECONDS"
pid_is_live "${LAUNCHED_PIDS[0]}" || fail_service 'API'

launch_service 'landing' "$LANDING_DIR" "$NPM_BIN" "$STATE_DIR/landing.log" "$STATE_DIR/landing.pid" run dev
launch_service 'dashboard' "$DASHBOARD_DIR" "$NPM_BIN" "$STATE_DIR/dashboard.log" "$STATE_DIR/dashboard.pid" run dev -- -p "$DASHBOARD_PORT"

wait_for_service 'API' "$API_PORT" "${LAUNCHED_PIDS[0]}"
wait_for_service 'Landing' "$LANDING_PORT" "${LAUNCHED_PIDS[1]}"
wait_for_service 'Dashboard' "$DASHBOARD_PORT" "${LAUNCHED_PIDS[2]}"

printf '\n  API:        http://localhost:%s\n' "$API_PORT"
printf '  Landing:    http://localhost:%s\n' "$LANDING_PORT"
printf '  Dashboard:  http://localhost:%s\n' "$DASHBOARD_PORT"
printf '\nLogs: %s\n' "$STATE_DIR"
