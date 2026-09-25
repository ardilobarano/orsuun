#!/usr/bin/env bash
# The Campaign Trail smoke test (Development server): a fresh hero claims a bounty for Trail XP, climbs tiers (dev XP),
# claims the free track, buys the Trail with Amber (free packs on the playtest), claims the season costume, steps up to
# Plus for ten more tiers, and has a season end hand over what was left. Deletes its account at the end. Needs curl, jq.
#   tools/smoke-trail.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
g() { curl -s "$BASE$2" -H "X-Session: $1"; }
j() { jq -nc "$@"; }
# Filters live in variables: macOS bash 3.2 brace-expands "{a, b}" inside "$(...)".
F2='.trail.freeClaimed'
BUY='[.wardrobe.amber, .trail.pass, .trail.tier]'
T='{trail: (.trail | {season, name, days: (.secondsLeft / 86400 | floor), xp, tier, into: .xpIntoTier, pass, free: .freeClaimed, paid: .paidClaimed, owed}), amber: .wardrobe.amber, turn: .inventory.turnstones, scrolls: .inventory.scrollsOfMercy, alloys: .inventory.khansAlloys, wards: .inventory.anvilWards, skin: .wardrobe.skin, mount: .wardrobe.mount}'

S=$(curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "{\"deviceToken\":\"smoke-trail-$(rid)\"}" | jq -r .sessionToken)
echo "start: $(g "$S" /v1/me | jq -c "$T")"
echo "nothing ready: $(p "$S" /v1/trail/claim "$(j --arg r "$(rid)" '{requestId:$r, tier:0}')" | jq -c .code)"
p "$S" /v1/dev/grant '{}' > /dev/null
p "$S" "/v1/dev/stage?cleared=60" '{}' > /dev/null
p "$S" /v1/dev/bosses-up "{}" > /dev/null
echo "a Commander fight for the daily bounty: $(p "$S" /v1/boss/fight "$(j --arg r "$(rid)" '{requestId:$r, bossId:1}')" | jq -c '.lastBossFight.victory // .code')"
echo "claim 'Fight a Commander' (5): $(p "$S" /v1/bounty/claim "$(j --arg r "$(rid)" '{requestId:$r, bountyId:5}')" | jq -c .trail.xp)"
echo "dev +1700 Trail XP: $(p "$S" "/v1/dev/trail?xp=1700" '{}' | jq -c "$T | .trail")"
echo "claim tier 2 only: $(p "$S" /v1/trail/claim "$(j --arg r "$(rid)" '{requestId:$r, tier:2}')" | jq -c "[$F2, .inventory.scrollsOfMercy]")"
echo "claim all: $(p "$S" /v1/trail/claim "$(j --arg r "$(rid)" '{requestId:$r, tier:0}')" | jq -c "$T")"
echo "buy the Trail with no Amber: $(p "$S" /v1/trail/buy "$(j --arg r "$(rid)" '{requestId:$r, plus:false}')" | jq -c .code)"
p "$S" /v1/caravan/amber "$(j --arg r "$(rid)" '{requestId:$r, packId:3}')" > /dev/null
echo "Amber pack 3, first purchase: $(g "$S" /v1/me | jq -c '.wardrobe.amber')"
echo "buy the Trail: $(p "$S" /v1/trail/buy "$(j --arg r "$(rid)" '{requestId:$r, plus:false}')" | jq -c "$BUY")"
echo "buy it again: $(p "$S" /v1/trail/buy "$(j --arg r "$(rid)" '{requestId:$r, plus:false}')" | jq -c .code)"
echo "claim the paid tiers (costume at 1): $(p "$S" /v1/trail/claim "$(j --arg r "$(rid)" '{requestId:$r, tier:0}')" | jq -c "$T")"
echo "the costume held: $(g "$S" /v1/me | jq -c '[.wardrobe.pieces[] | "\(.id) \(.secondsLeft / 86400 | floor)d"]')"
echo "Plus with too little Amber: $(p "$S" /v1/trail/buy "$(j --arg r "$(rid)" '{requestId:$r, plus:true}')" | jq -c .code)"
p "$S" /v1/caravan/amber "$(j --arg r "$(rid)" '{requestId:$r, packId:3}')" > /dev/null
echo "step up to Plus (750): $(p "$S" /v1/trail/buy "$(j --arg r "$(rid)" '{requestId:$r, plus:true}')" | jq -c "$BUY")"
echo "the season ends with rewards unclaimed: $(p "$S" "/v1/dev/trail?lastSeason=true" '{}' | jq -c "$T | .trail")"
R=$(p "$S" /v1/trail/claim "$(j --arg r "$(rid)" '{requestId:$r, tier:0}')")
echo "the next claim hands them over: $(echo "$R" | jq -c "$T" 2>/dev/null || echo "$R")"
curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S" | jq -c .
