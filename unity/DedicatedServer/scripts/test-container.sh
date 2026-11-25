#!/bin/sh
# Run against this project's already deployed lab container.
set -eu
cd "$(dirname "$0")/.."
. ./scripts/server-compose.sh
profile=${1:-ip}
container=$(compose "$profile" ps -q server)
[ -n "$container" ] || { echo "Start the lab server first." >&2; exit 2; }

inspect() { docker inspect --format "$1" "$container"; }
wait_ready() {
    attempt=0
    while [ "$(inspect '{{.State.Health.Status}}')" != healthy ]; do
        [ "$attempt" -lt 40 ] || { compose "$profile" logs --tail 60 server; return 1; }
        attempt=$((attempt + 1))
        sleep 2
    done
}

wait_ready
# Docker activates the restart policy after a successful startup period.
sleep 11
before=$(inspect '{{.RestartCount}}')
compose "$profile" exec -T server sh -c 'kill -KILL "$(cat /tmp/dustline.pid)"'
attempt=0
while [ "$(inspect '{{.RestartCount}}')" -le "$before" ]; do
    [ "$attempt" -lt 30 ] || { compose "$profile" logs --tail 60 server; exit 1; }
    attempt=$((attempt + 1))
    sleep 2
done
wait_ready
printf 'Crash recovery RestartCount %s -> %s\n' "$before" "$(inspect '{{.RestartCount}}')"
compose "$profile" ps
docker stats --no-stream "$container"
docker image inspect dustline-pr7:local --format 'Image bytes {{.Size}}'
df -h /
compose "$profile" restart server
wait_ready
compose "$profile" stop
[ "$(inspect '{{.State.ExitCode}}')" -eq 0 ]
compose "$profile" logs --no-color --tail 100 server
compose "$profile" logs --no-color --tail 100 server | grep -q '\[PR7\] SERVER_STOPPED'
compose "$profile" up -d --wait --wait-timeout 90
