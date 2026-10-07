#!/usr/bin/env bash
#
# Runs the tests, or with an argument only the test methods whose names contain it.
#
# A filter runs one project at a time: from the root, a project with no match exits 8 (zero tests
# ran), which fails the whole run even when every matching test passed.
#
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

[ "$#" -eq 0 ] && exec dotnet test

matched=false
for project in tests/*/*.csproj; do
  status=0
  dotnet test --project "$project" -- --filter-method "*$1*" || status=$?
  case "$status" in
    0) matched=true ;;
    8) ;; # nothing in this project matches
    *) exit "$status" ;;
  esac
done

$matched || { echo "error: no test method name contains '$1'." >&2; exit 8; }
