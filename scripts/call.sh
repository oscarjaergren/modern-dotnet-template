#!/usr/bin/env bash
#
# Calls the API by operation, through commands Restish generates from the committed openapi.json:
#   mise run call ping
#   mise run call create-greeting 'name: Ada, age: 36'
#   mise run call ping -p staging   # an environment listed in .config/restish/restish.json
#
# Without -p it targets the app `mise run start` started, or API_URL if set. An error response
# exits non-zero and carries a traceId, which `mise run traces --search <id>` finds.
#
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

export RSH_CONFIG=.config/restish/restish.json
# Restish refuses a config other users can read, and git checks files out readable. The file
# holds URLs only; credentials belong in environment variables.
chmod 600 "$RSH_CONFIG"

[ "$#" -gt 0 ] || exec restish service --help

server=()
case " $* " in
  *" -p "* | *" --rsh-profile "* | *" -s "* | *" --rsh-server "*) ;;
  *)
    url=${API_URL:-$(mise run --quiet url)}
    [ -n "$url" ] || { echo "error: nothing to call. Run 'mise run start', or set API_URL." >&2; exit 1; }
    server=(-s "$url")
    ;;
esac

# No response cache: debugging needs what the API says now. Stdin is closed because Restish reads a
# request body from any stdin that isn't a terminal, and the open pipes CI and agents leave would
# hang it; pass a body as an argument instead (JSON works too).
exec restish service --rsh-no-cache "$@" ${server[@]+"${server[@]}"} </dev/null
