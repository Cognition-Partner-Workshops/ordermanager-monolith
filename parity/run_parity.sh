#!/usr/bin/env bash
# =============================================================================
# End-to-end parity run: build + boot BOTH backends against isolated, freshly
# seeded SQLite databases, replay the request suite against each, and diff.
#
#   .NET (legacy) -> http://127.0.0.1:5001   (own PascalCase-schema DB)
#   Java (target) -> http://127.0.0.1:5000   (own snake_case-schema DB)
#
# Exit code 0 == every CONTRACT endpoint matches. Intentional error-handling
# divergences are reported but do not fail the run.
#
# Usage: ./run_parity.sh
# Requires: dotnet SDK, JDK 17, Maven, python3.
# =============================================================================
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
export JAVA_HOME="${JAVA_HOME:-/usr/lib/jvm/java-17-openjdk-amd64}"

DOTNET_URL="http://127.0.0.1:5001"
JAVA_URL="http://127.0.0.1:5000"
DOTNET_DB="/tmp/parity_dotnet.db"
JAVA_DB="/tmp/parity_java.db"
DOTNET_LOG="/tmp/parity_dotnet.log"
JAVA_LOG="/tmp/parity_java.log"

DOTNET_PID=""; JAVA_PID=""
cleanup() {
  [[ -n "$DOTNET_PID" ]] && kill "$DOTNET_PID" 2>/dev/null || true
  [[ -n "$JAVA_PID"   ]] && kill "$JAVA_PID"   2>/dev/null || true
}
trap cleanup EXIT

wait_up() {  # <url> <name> <logfile>
  for _ in $(seq 1 60); do
    if curl -fsS -o /dev/null "$1/api/customers" 2>/dev/null; then return 0; fi
    sleep 1
  done
  echo "ERROR: $2 did not come up. Last log lines:" >&2
  tail -n 40 "$3" >&2 || true
  return 1
}

echo "== building .NET =="
dotnet build "$ROOT/src/OrderManager.Api/OrderManager.Api.csproj" -v quiet

echo "== building Java (skipping Angular frontend build) =="
( cd "$ROOT/server-java" && mvn -q -DskipTests \
    -Dskip.installnodenpm=true -Dskip.npm=true package )
JAR="$(ls "$ROOT"/server-java/target/*.jar | head -n1)"

echo "== starting .NET on :5001 (fresh DB) =="
rm -f "$DOTNET_DB"
ConnectionStrings__DefaultConnection="Data Source=$DOTNET_DB" \
  ASPNETCORE_URLS="$DOTNET_URL" ASPNETCORE_ENVIRONMENT=Production \
  dotnet run --project "$ROOT/src/OrderManager.Api/OrderManager.Api.csproj" --no-build \
  > "$DOTNET_LOG" 2>&1 &
DOTNET_PID=$!
wait_up "$DOTNET_URL" ".NET" "$DOTNET_LOG"

echo "== starting Java on :5000 (fresh DB) =="
rm -f "$JAVA_DB"
SPRING_DATASOURCE_URL="jdbc:sqlite:$JAVA_DB" \
  java -jar "$JAR" > "$JAVA_LOG" 2>&1 &
JAVA_PID=$!
wait_up "$JAVA_URL" "Java" "$JAVA_LOG"

echo "== comparing =="
python3 "$HERE/parity_harness.py" compare \
  --dotnet-url "$DOTNET_URL" --java-url "$JAVA_URL" \
  --out "$HERE/fixtures" --report "$HERE/last-report.json"
