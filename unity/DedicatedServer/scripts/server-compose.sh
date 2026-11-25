#!/bin/sh

compose() {
    profile=$1
    shift
    case "$profile" in
        ip) ;;
        steam) set -- -f deploy/compose.steam.yaml "$@" ;;
        *) printf '%s\n' 'Network profile must be ip or steam.' >&2; return 2 ;;
    esac
    if [ -f deploy/.env ]; then
        set -- --env-file deploy/.env -f deploy/compose.yaml "$@"
    else
        set -- -f deploy/compose.yaml "$@"
    fi
    docker compose --project-name dustline-pr7 "$@"
}
