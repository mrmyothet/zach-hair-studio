#!/usr/bin/env bash

set -euo pipefail

REPO_ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
STATE_DIR=${DEV_STACK_STATE_DIR:-"$REPO_ROOT/temp/dev-stack"}
API_PORT=${DEV_STACK_API_PORT:-5236}
LANDING_PORT=${DEV_STACK_LANDING_PORT:-3000}
DASHBOARD_PORT=${DEV_STACK_DASHBOARD_PORT:-3001}
STOP_GRACE_SECONDS=${DEV_STACK_STOP_GRACE_SECONDS:-5}

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
  validate_integer 'Stop grace duration' "$STOP_GRACE_SECONDS" 1 300
  [[ "$API_PORT" != "$LANDING_PORT" && "$API_PORT" != "$DASHBOARD_PORT" && "$LANDING_PORT" != "$DASHBOARD_PORT" ]] || die 'service ports must be distinct'
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

signal_tree() {
  local signal=$1 pid=$2 child
  pid_is_valid "$pid" || return 0
  if command -v pgrep >/dev/null 2>&1; then
    while IFS= read -r child; do
      signal_tree "$signal" "$child"
    done < <(pgrep -P "$pid" 2>/dev/null || true)
  fi
  kill "-$signal" -- "$pid" 2>/dev/null || true
}

terminate_pid() {
  local pid=$1 deadline
  pid_is_live "$pid" || return 0
  signal_tree TERM "$pid"
  deadline=$((SECONDS + STOP_GRACE_SECONDS))
  while pid_is_live "$pid" && (( SECONDS < deadline )); do
    sleep 0.1
  done
  if pid_is_live "$pid"; then
    signal_tree KILL "$pid"
  fi
  return 0
}

stop_service() {
  local name port pid_file pid stopped=0
  name=$1
  port=$2
  pid_file="$STATE_DIR/$name.pid"

  if [[ -f "$pid_file" ]]; then
    pid=$(<"$pid_file")
    if pid_is_live "$pid"; then
      terminate_pid "$pid"
      stopped=1
    fi
    rm -f -- "$pid_file"
  fi

  while IFS= read -r pid; do
    pid_is_live "$pid" || continue
    terminate_pid "$pid"
    stopped=1
  done < <(listener_pids "$port")

  if (( stopped )); then
    printf 'Stopped %s.\n' "$name"
  else
    printf '%s already stopped.\n' "$name"
  fi
}

validate_configuration
printf 'Stopping Zach Hair Studio services...\n'
stop_service api "$API_PORT"
stop_service landing "$LANDING_PORT"
stop_service dashboard "$DASHBOARD_PORT"
printf 'All services stopped.\n'
