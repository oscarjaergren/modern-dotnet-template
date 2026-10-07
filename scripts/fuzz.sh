#!/usr/bin/env bash
#
# Fuzzes a running API against the committed openapi.json: generated requests, every response
# checked against the contract. 500s, undocumented statuses, wrong bodies and accepted invalid
# input (validation not running) all fail. Default URL: the container `mise run image-check` starts.
#
# positive_data_acceptance is off: System.Text.Json rejects 14.0 for an int, which JSON Schema
# counts as an integer (dotnet/runtime#40596).
#
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"
: "${SCHEMATHESIS_VERSION:?run this through 'mise run fuzz', which pins the version}"

docker run --rm --network host -v "$PWD/src/Api/openapi.json:/openapi.json:ro" \
  "schemathesis/schemathesis:$SCHEMATHESIS_VERSION" \
  run /openapi.json --url "${1:-http://127.0.0.1:8080}" --exclude-checks positive_data_acceptance
