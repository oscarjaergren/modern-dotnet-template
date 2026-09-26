#!/usr/bin/env bash
#
# The committed openapi.json is the API contract: fail if a build changes it.
#
set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"

DOC="src/Api/openapi.json"

before="$(sha256sum "$DOC" | cut -d' ' -f1)"
dotnet build src/Api/Api.csproj --nologo --verbosity quiet >/dev/null
after="$(sha256sum "$DOC" | cut -d' ' -f1)"

if [ "$before" != "$after" ]; then
  echo "error: $DOC is out of date — a build regenerates it differently." >&2
  echo "       The API contract changed. Review the diff and commit it:" >&2
  echo "         git diff $DOC" >&2
  exit 1
fi

if ! git diff --quiet -- "$DOC" 2>/dev/null; then
  echo "error: $DOC has uncommitted changes. Commit the contract change." >&2
  exit 1
fi
