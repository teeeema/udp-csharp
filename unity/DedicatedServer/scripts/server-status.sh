#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
. ./scripts/server-compose.sh
compose "${1:-ip}" ps -a
compose "${1:-ip}" logs --tail 30 server
