#!/usr/bin/env bash
# Start the migration facade (nginx) on :8080 as a non-root foreground process.
# Backends must be reachable at 127.0.0.1:5001 (.NET) and 127.0.0.1:5000 (Java).
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
mkdir -p "$DIR/temp" "$DIR/logs"
exec nginx -p "$DIR" -c nginx.conf -g 'daemon off;'
