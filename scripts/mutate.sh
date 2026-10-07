#!/usr/bin/env bash
#
# Mutation tests the code changed since a git branch (default origin/main) and fails if the unit
# tests catch under 80% of the mutants. docs/build-gates.md explains the 80 and the exclusions.
#
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"
: "${STRYKER_VERSION:?run this through 'mise run mutate', which pins the version}"

dotnet tool exec "dotnet-stryker@$STRYKER_VERSION" --yes -- \
  --test-project tests/Api.UnitTests/Api.UnitTests.csproj --project Api.csproj \
  --test-runner mtp --since:"${1:-origin/main}" --break-at 80 \
  --mutate '!**/*Endpoint.cs' --mutate '!**/Program.cs' \
  --output artifacts/stryker --reporter cleartext --reporter markdown
