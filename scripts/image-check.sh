#!/usr/bin/env bash
#
# Runs api:latest and checks what only the published image shows: a clean AOT publish proves
# little, because AOT failures appear at runtime. Build the image first with `mise run image`.
#
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

URL=http://127.0.0.1:8080
CEILING_MB=20

fail() { echo "error: $*" >&2; exit 1; }

cid=$(docker run -d -p 8080:8080 api:latest)
cleanup() {
  local status=$?
  [ "$status" -eq 0 ] || docker logs "$cid" # the logs only matter when something failed
  docker rm -f "$cid" >/dev/null
  exit "$status"
}
trap cleanup EXIT
curl -fsS --retry 30 --retry-all-errors --retry-delay 1 --retry-connrefused -o /dev/null "$URL/alive"

curl -fsS "$URL/ping" | grep -q '"status":"pong"' || fail "/ping did not answer pong."

curl -fsS -X POST "$URL/greetings" -H 'Content-Type: application/json' \
  -d '{"name":"Ada","age":36}' | grep -q 'Hello, Ada' || fail "/greetings did not greet."

# A 409 with a traceId also proves ProblemDetails serialises under AOT.
body=$(mktemp)
code=$(curl -s -o "$body" -w '%{http_code}' -X POST "$URL/greetings" \
  -H 'Content-Type: application/json' -d '{"name":"admin"}')
[ "$code" = 409 ] || fail "expected 409 for a reserved name, got $code."
grep -q '"traceId"' "$body" || fail "the ProblemDetails has no traceId, so logs can't be correlated."

# `mise run call` generates its commands from the same contract, so they must work on the image.
API_URL=$URL scripts/call.sh ping >/dev/null || fail "'mise run call ping' failed against the image."

# Probes must answer in Production, where the Aspire default leaves them unmapped. More than the
# aggregate status would leak the list of checks.
for probe in /alive /health; do
  answer=$(curl -fsS "$URL$probe")
  [ "$answer" = Healthy ] || fail "$probe returned '$answer', expected exactly 'Healthy'."
done

# Gzipped, so it measures compressed size whichever Docker image store is in use. A large jump
# usually means AOT symbols got into a layer.
bytes=$(docker save api:latest | gzip -c | wc -c)
mb=$(( (bytes + 524288) / 1048576 ))
echo "Image size (compressed): $mb MB"
[ "$mb" -le "$CEILING_MB" ] || fail "the image is $mb MB, over the $CEILING_MB MB ceiling."

# The README quotes both numbers; keep them true.
claimed=$(grep -oE '\*\*~[0-9]+ MB\*\*' README.md | tr -dc '0-9')
[ "$(( mb > claimed ? mb - claimed : claimed - mb ))" -le 2 ] ||
  fail "README.md says ~$claimed MB but the image is $mb MB; update it."
grep -q "CI fails above $CEILING_MB" README.md ||
  fail "README.md's size ceiling doesn't match CEILING_MB=$CEILING_MB."

scripts/fuzz.sh "$URL"
