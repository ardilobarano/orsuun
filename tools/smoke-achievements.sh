#!/usr/bin/env bash
# Titles and achievements smoke test (27 Sep 2026; a Development server: dev level and stage). A fresh hero has nothing
# done; at level 30 with three maps cleared the level and map achievements are ready (the MENU badge counts them),
# one claims for Honor and sorn, a second claim is refused, a claim not yet done is refused, the first title is worn
# at once and can be taken off and put back. Deletes the hero.
# Needs curl, jq.
#   tools/smoke-achievements.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
g() { curl -s "$BASE$2" -H "X-Session: $1"; }
j() { jq -nc "$@"; }
A=$(curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "$(j --arg d "smoke-feats-$(rid)" '{deviceToken:$d}')" | jq -r .sessionToken)
READY='[.list[] | select(.done and (.claimed | not)) | .id]'
FRESH='{ready: '"$READY"', total: (.list | length), title}'

echo "fresh: $(g "$A" /v1/achievements | jq -c "$FRESH")"
p "$A" "/v1/dev/level?level=30" '{}' > /dev/null
p "$A" "/v1/dev/stage?cleared=30" '{}' > /dev/null
echo "level 30, 3 maps: $(g "$A" /v1/achievements | jq -c "$READY")  badge on /me: $(g "$A" /v1/me | jq .achievementsReady)"
BEFORE=$(g "$A" /v1/me | jq '{honor: .honor, sorn: .inventory.sorn}')
echo "claim level 10: $(p "$A" /v1/achievements/claim "$(j --arg r "$(rid)" '{requestId:$r, id:20}')" | jq -c .message)"
echo "  honor and sorn before $(echo "$BEFORE" | jq -c .) after $(g "$A" /v1/me | jq -c '{honor: .honor, sorn: .inventory.sorn}')"
echo "claim it again: $(p "$A" /v1/achievements/claim "$(j --arg r "$(rid)" '{requestId:$r, id:20}')" | jq -c .code)"
echo "claim level 60 (not done): $(p "$A" /v1/achievements/claim "$(j --arg r "$(rid)" '{requestId:$r, id:22}')" | jq -c .code)"
echo "claim map 3: $(p "$A" /v1/achievements/claim "$(j --arg r "$(rid)" '{requestId:$r, id:103}')" | jq -c .message)"
p "$A" "/v1/dev/stage?cleared=60" '{}' > /dev/null
echo "claim map 6 (a title): $(p "$A" /v1/achievements/claim "$(j --arg r "$(rid)" '{requestId:$r, id:106}')" | jq -c '{message, title, titleId}')"
echo "title on /me: $(g "$A" /v1/me | jq -c .title)"
echo "take it off: $(p "$A" /v1/achievements/title '{"id":0}' | jq -c '{message, title}')"
echo "a title not claimed: $(p "$A" /v1/achievements/title '{"id":112}' | jq -c .code)"
echo "put it back: $(p "$A" /v1/achievements/title '{"id":106}' | jq -c '{message, title}')"
echo "chat with the title: $(p "$A" /v1/chat '{"channel":"world","text":"smoke test title"}' > /dev/null; g "$A" "/v1/chat?channel=world" | jq -c '[.lines[] | select(.mine)][-1] | {name, title}')"
curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $A" | jq -c .
