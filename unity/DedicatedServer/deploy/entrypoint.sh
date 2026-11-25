#!/bin/sh
set -eu

export DUSTLINE_STOP_FILE=/tmp/dustline.stop
export DUSTLINE_READY_FILE=/tmp/dustline.ready
pid_file=/tmp/dustline.pid
rm -f "$DUSTLINE_STOP_FILE" "$DUSTLINE_READY_FILE" "$pid_file"

profile=${DUSTLINE_PROFILE:-ip}
set -- "$@" -batchmode -nographics -logFile - \
    --pr7-listen 0.0.0.0 --pr7-port "${DUSTLINE_PORT:-7777}" \
    --pr7-map "${DUSTLINE_MAP:-PredictionDemo}" --pr7-name "${DUSTLINE_NAME:-DUSTLINE Dedicated}" \
    --pr7-max-players "${DUSTLINE_MAX_PLAYERS:-4}" --pr7-log-level "${DUSTLINE_LOG_LEVEL:-info}" \
    --pr7-profile "$profile"
if [ "$profile" = steam ]; then
    export SteamAppId=${DUSTLINE_APP_ID:?Steam App ID required}
    export SteamGameId=$SteamAppId
    set -- "$@" --pr7-app-id "$SteamAppId" --pr7-query-port "${DUSTLINE_QUERY_PORT:-27015}"
    if [ "${DUSTLINE_ALLOW_APP_ID_480:-0}" = 1 ]; then
        set -- "$@" --pr7-allow-app-id-480
    fi
fi

stop() { touch "$DUSTLINE_STOP_FILE"; }
trap stop TERM INT
./DustlineServer.x86_64 "$@" &
game_pid=$!
printf '%s\n' "$game_pid" > "$pid_file"

set +e
while :; do
    wait "$game_pid"
    result=$?
    if ! kill -0 "$game_pid" 2>/dev/null; then
        break
    fi
done
rm -f "$pid_file" "$DUSTLINE_READY_FILE"
exit "$result"
