#!/usr/bin/env bash
# Stop the migration facade started by start.sh.
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
nginx -p "$DIR" -c nginx.conf -s stop
