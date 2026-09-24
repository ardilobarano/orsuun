#!/usr/bin/env bash
# The Caravan smoke test: a fresh account buys Amber packs (free on a Development server; the first pays double), buys a
# mount for 7 days and again for 3 (the time adds up), a companion, wears and takes off, and checks the hero's attack
# rises with the mount. Deletes its account at the end. Needs curl, jq.
#   tools/smoke-caravan.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
g() { curl -s "$BASE$2" -H "X-Session: $1"; }
j() { jq -nc "$@"; }
W='.wardrobe | {amber, first: .firstPurchase, skin, mount, companion, pieces: [.pieces[] | "\(.id) \(.secondsLeft / 3600 | floor)h"]}'

S=$(curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "{\"deviceToken\":\"smoke-caravan-$(rid)\"}" | jq -r .sessionToken)
ATTACK=$(g "$S" /v1/me | jq .hero.attack)
echo "start: $(g "$S" /v1/me | jq -c "$W"), attack $ATTACK"
echo "pack 2, first purchase: $(p "$S" /v1/caravan/amber "$(j --arg r "$(rid)" '{requestId:$r, packId:2}')" | jq -c "$W")"
echo "pack 1: $(p "$S" /v1/caravan/amber "$(j --arg r "$(rid)" '{requestId:$r, packId:1}')" | jq -c '.wardrobe.amber')"
echo "Ember Warhorse 7 days: $(p "$S" /v1/caravan/buy "$(j --arg r "$(rid)" '{requestId:$r, pieceId:"ember-warhorse", days:7}')" | jq -c "$W"), attack $(g "$S" /v1/me | jq .hero.attack)"
echo "and 3 more days: $(p "$S" /v1/caravan/buy "$(j --arg r "$(rid)" '{requestId:$r, pieceId:"ember-warhorse", days:3}')" | jq -c "$W")"
echo "a 2-day stay (not sold): $(p "$S" /v1/caravan/buy "$(j --arg r "$(rid)" '{requestId:$r, pieceId:"sky-falcon", days:2}')" | jq -c .code)"
echo "a Commander trophy (not sold): $(p "$S" /v1/caravan/buy "$(j --arg r "$(rid)" '{requestId:$r, pieceId:"mirage-veil", days:1}')" | jq -c .code)"
echo "Sky Falcon 1 day: $(p "$S" /v1/caravan/buy "$(j --arg r "$(rid)" '{requestId:$r, pieceId:"sky-falcon", days:1}')" | jq -c "$W")"
echo "the Hollow Steed 14 days, too dear: $(p "$S" /v1/caravan/buy "$(j --arg r "$(rid)" '{requestId:$r, pieceId:"hollow-steed", days:14}')" | jq -c .code)"
echo "wear a piece not held: $(p "$S" /v1/wardrobe/wear "$(j --arg r "$(rid)" '{requestId:$r, pieceId:"hollow-steed", kind:""}')" | jq -c .code)"
echo "take off the mount: $(p "$S" /v1/wardrobe/wear "$(j --arg r "$(rid)" '{requestId:$r, pieceId:"", kind:"Mount"}')" | jq -c "$W"), attack $(g "$S" /v1/me | jq .hero.attack)"
echo "wear it again: $(p "$S" /v1/wardrobe/wear "$(j --arg r "$(rid)" '{requestId:$r, pieceId:"ember-warhorse", kind:""}')" | jq -c '.wardrobe.mount')"
curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S" | jq -c .
