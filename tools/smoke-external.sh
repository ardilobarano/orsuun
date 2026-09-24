#!/usr/bin/env bash
# Sign in with Google / Apple, walked with the Development server's "dev" stand-in provider (local or a Development
# playtest server): begin -> start -> the provider page -> callback -> orsuun:// ticket -> redeem on the device.
#   tools/smoke-external.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
j() { jq -nc "$@"; }
guest() { curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "$(j --arg d "$1" '{deviceToken:$d}')"; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
SUB="tester-$(rid | cut -c1-8)"

# One browser round: begin (as the session), follow the redirects, pull the ticket out of the return page.
flow() {
  local url start page callback
  url=$(p "$1" /v1/auth/external/begin "$(j '{provider:"dev"}')" | jq -r .url)
  start=$(curl -s -o /dev/null -w '%{redirect_url}' "$url")
  page=$(curl -s "$start")
  callback=$(echo "$page" | sed -n 's/.*href="\([^"]*\)".*/\1/p' | sed 's/&amp;/\&/g' | sed "s/sub=tester-1/sub=$SUB/")
  curl -s "$BASE$callback" | sed -n 's/.*orsuun:\/\/auth?ticket=\([A-Za-z0-9_-]*\).*/\1/p' | head -1
}

echo "providers: $(curl -s "$BASE/v1/auth/providers" | jq -c .providers)"
D1="smoke-$(rid)"; A=$(guest "$D1"); S1=$(echo "$A" | jq -r .sessionToken); ID1=$(echo "$A" | jq -r .accountId)
T=$(flow "$S1")
echo "ticket: $( [ ${#T} -gt 20 ] && echo yes || echo no)"
D9="smoke-$(rid)"
echo "ticket on another device: $(curl -s -X POST "$BASE/v1/auth/ticket" -H 'Content-Type: application/json' -d "$(j --arg t "$T" --arg d "$D9" '{ticket:$t, deviceToken:$d}')" | jq -c .code)"
T=$(flow "$S1")
R=$(curl -s -X POST "$BASE/v1/auth/ticket" -H 'Content-Type: application/json' -d "$(j --arg t "$T" --arg d "$D1" '{ticket:$t, deviceToken:$d}')")
echo "first sign-in links this hero: $(echo "$R" | jq -c '{linked, switched, provider}') same=$( [ "$(echo "$R" | jq -r .accountId)" = "$ID1" ] && echo yes || echo no)"
echo "ticket twice: $(curl -s -X POST "$BASE/v1/auth/ticket" -H 'Content-Type: application/json' -d "$(j --arg t "$T" --arg d "$D1" '{ticket:$t, deviceToken:$d}')" | jq -c .code)"
S1=$(guest "$D1" | jq -r .sessionToken)
echo "state shows it: $(curl -s "$BASE/v1/me" -H "X-Session: $S1" | jq -c .logins)"

# A second phone plays a fresh guest, then signs in with the same identity: it switches to the first hero.
D2="smoke-$(rid)"; B=$(guest "$D2"); S2=$(echo "$B" | jq -r .sessionToken); ID2=$(echo "$B" | jq -r .accountId)
T=$(flow "$S2")
R=$(curl -s -X POST "$BASE/v1/auth/ticket" -H 'Content-Type: application/json' -d "$(j --arg t "$T" --arg d "$D2" '{ticket:$t, deviceToken:$d}')")
echo "second phone: $(echo "$R" | jq -c '{linked, switched}') now the first hero=$( [ "$(echo "$R" | jq -r .accountId)" = "$ID1" ] && echo yes || echo no)"
echo "its guest login lands there: $( [ "$(guest "$D2" | jq -r .accountId)" = "$ID1" ] && echo yes || echo no)"

# The native path (an ID token straight from a button): a third identity links to a new guest.
D3="smoke-$(rid)"; C=$(guest "$D3"); S3=$(echo "$C" | jq -r .sessionToken); ID3=$(echo "$C" | jq -r .accountId)
R=$(p "$S3" /v1/auth/external "$(j --arg s "native-$SUB" '{provider:"dev", idToken:("dev|" + $s + "|n@example.com")}')")
echo "native token: $(echo "$R" | jq -c '{linked, switched}')"
S3=$(echo "$R" | jq -r .sessionToken)
echo "google not set up here: $(p "$S3" /v1/auth/external/begin "$(j '{provider:"google"}')" | jq -c .message)"
echo "expired link page: $(curl -s "$BASE/auth/dev/start?flow=nope" | grep -c 'expired')"

S1=$(guest "$D1" | jq -r .sessionToken); curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S1" > /dev/null
curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S3" > /dev/null
S2=$(guest "$D2" | jq -r .sessionToken); curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S2" > /dev/null
echo "cleaned up (the second phone's own guest $ID2 stays behind, as it would for a player)"
