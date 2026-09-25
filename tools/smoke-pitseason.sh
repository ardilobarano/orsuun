#!/usr/bin/env bash
# Pit seasons smoke test (25 Sep 2026). LOCAL ONLY: /v1/dev/pit-season-end settles the running season for everyone (every
# rating drifts halfway back to 1000), so never point it at the playtest server. A fresh hero fights three times, sees
# the season record and the board, buys from the widened Pit shop, then the season is ended: its rank, Laurels and title,
# and the rating's soft reset. Deletes the hero. Needs curl, jq.
#   tools/smoke-pitseason.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
case "$BASE" in *localhost*|*127.0.0.1*) ;; *) echo "local servers only"; exit 1 ;; esac
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
g() { curl -s "$BASE$2" -H "X-Session: $1"; }
j() { jq -nc "$@"; }
P='{rating, league, seasonWins, seasonLosses, laurels, title, lastRank, lastLaurels, lastChampions, days: (.seasonSecondsLeft/86400|floor)}'
S=$(curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "$(j --arg d "smoke-pitseason-$(rid)" '{deviceToken:$d}')" | jq -r .sessionToken)
p "$S" /v1/dev/grant '{}' > /dev/null
echo "before: $(g "$S" /v1/pits | jq -c "$P")"
for i in 1 2 3; do
  FOE=$(g "$S" /v1/pits | jq -r '.challengers[1].id')
  p "$S" /v1/pits/fight "$(j --arg r "$(rid)" --arg o "$FOE" '{requestId:$r, opponentId:$o}')" | jq -c '{won: .duel.won, before: .ratingBefore, after: .ratingAfter}'
done
echo "season: $(g "$S" /v1/pits | jq -c "$P")"
echo "board: $(g "$S" /v1/pits | jq -c '[.board[] | "\(.rank). \(.name) \(.rating) \(.wins)-\(.losses) \(.title)"]')"
echo "shop Oathstone: $(p "$S" /v1/pits/shop "$(j --arg r "$(rid)" '{requestId:$r, itemId:7}')" | jq -c '{laurels, message}')"
echo "shop Turnstones: $(p "$S" /v1/pits/shop "$(j --arg r "$(rid)" '{requestId:$r, itemId:4}')" | jq -c '{laurels, message}')"
echo "inventory: $(g "$S" /v1/me | jq -c '{oathstones: .inventory.oathstones, turnstones: .inventory.turnstones}')"
echo "season ends: $(p "$S" /v1/dev/pit-season-end '{}' | jq -c "$P")"
curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S" | jq -c .
