#!/usr/bin/env bash
# Flip a single /api route group between the legacy .NET backend and the new
# Java backend in facade/nginx.conf, then (optionally) reload nginx.
#
# Usage:
#   ./flip-route.sh <group> <dotnet|java> [--reload]
#
#   <group> : customers | products | inventory | orders | spa
#
# Examples:
#   ./flip-route.sh customers java --reload   # cut Customers over to Java
#   ./flip-route.sh customers dotnet          # roll Customers back to .NET
set -euo pipefail

GROUP="${1:-}"
TARGET="${2:-}"
RELOAD="${3:-}"

case "$GROUP" in
  customers|products|inventory|orders|spa) ;;
  *) echo "error: group must be one of: customers products inventory orders spa" >&2; exit 2 ;;
esac

case "$TARGET" in
  dotnet) UPSTREAM="dotnet_backend" ;;
  java)   UPSTREAM="java_backend" ;;
  *) echo "error: target must be 'dotnet' or 'java'" >&2; exit 2 ;;
esac

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONF="$DIR/nginx.conf"

# Rewrite the proxy_pass on every line tagged '# ROUTE:<group>'.
sed -i -E "s|(proxy_pass http://)[a-z_]+(;[[:space:]]*# ROUTE:${GROUP}\b)|\1${UPSTREAM}\2|g" "$CONF"

echo "route '${GROUP}' -> ${UPSTREAM}"
grep -n "# ROUTE:${GROUP}\b" "$CONF" || true

if [[ "$RELOAD" == "--reload" ]]; then
  nginx -p "$DIR" -c nginx.conf -t && nginx -p "$DIR" -c nginx.conf -s reload
  echo "nginx reloaded"
fi
