#!/usr/bin/env bash
# The Pits smoke test: a fresh account looks at its challengers, fights until its five tickets are spent, refreshes,
# buys a Korshard with Laurels and checks the board. Changes other accounts' ratings a little (real defenders), so run it
# locally. Deletes its account at the end. Needs curl, jq.
#   tools/smoke-pits.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
g() { curl -s "$BASE$2" -H "X-Session: $1"; }
j() { jq -nc "$@"; }

S=$(curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "{\"deviceToken\":\"smoke-pits-$(rid)\"}" | jq -r .sessionToken)
echo "the pit: $(g "$S" /v1/pits | jq -c '{rating, league, ticketsLeft, challengers: [.challengers[] | {name, rating, winChancePercent, shade}]}')"
for i in 1 2 3 4 5 6; do
  FOE=$(g "$S" /v1/pits | jq -r '[.challengers[] | select(.shade)][0].id // .challengers[0].id')
  B1=$(j --arg r "$(rid)" --arg o "$FOE" '{requestId:$r, opponentId:$o}')
  R=$(p "$S" /v1/pits/fight "$B1")
  echo "fight $i vs $FOE: $(echo "$R" | jq -c 'if .code then .code else {won: .duel.won, chance: .duel.winChancePercent, rating: [.ratingBefore, .ratingAfter], laurels: .laurelsGained, tickets: .pits.ticketsLeft, champion: .duel.champion, hp: .duel.championHp} end')"
done
echo "a challenger not on offer: $(p "$S" /v1/pits/fight "$(j --arg r "$(rid)" '{requestId:$r, opponentId:"shade:5"}')" | jq -c .code)"
echo "refresh: $(p "$S" /v1/pits/refresh '{}' | jq -c '[.challengers[] | .name]')"
echo "shop, Trooper Korshard: $(p "$S" /v1/pits/shop "$(j --arg r "$(rid)" '{requestId:$r, itemId:1}')" | jq -c '{laurels, message}')"
echo "shop, Captain Korshard: $(p "$S" /v1/pits/shop "$(j --arg r "$(rid)" '{requestId:$r, itemId:3}')" | jq -c .code)"
echo "board: $(g "$S" /v1/pits | jq -c '[.board[] | {rank, name, rating, league, weapon, me}][0:3]')"
curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S" | jq -c .
