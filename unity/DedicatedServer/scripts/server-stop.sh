#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
. ./scripts/server-compose.sh
compose "${1:-ip}" stop
