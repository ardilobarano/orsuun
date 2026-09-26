#!/usr/bin/env bash
# Daily login calendar smoke test (27 Sep 2026): a new account makes two heroes; the first takes today's gift (day 1),
# a second claim is refused, and the second hero sees the account's gift already taken. Deletes the account. Needs curl, jq.
#   tools/smoke-daily.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
g() { curl -s "$BASE$2" -H "X-Session: $1"; }
j() { jq -nc "$@"; }
TAG=$(rid | tr -d 'a-f' | cut -c1-5)
S=$(curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "$(j --arg d "smoke-daily-$(rid)" '{deviceToken:$d, lobby:true}')" | jq -r .sessionToken)
p "$S" /v1/lobby/banner '{"banner":"Sky"}' > /dev/null
A=$(p "$S" /v1/lobby/create "$(j --arg n "Daya$TAG" '{name:$n, heroClass:"Vanguard"}')" | jq -r '.characters[0].id')
B=$(p "$S" /v1/lobby/create "$(j --arg n "Dayb$TAG" '{name:$n, heroClass:"Kestrel"}')" | jq -r '[.characters[] | select(.name | startswith("Dayb"))][0].id')
p "$S" /v1/lobby/select "$(j --arg c "$A" '{characterId:$c}')" > /dev/null
echo "calendar: $(g "$S" /v1/me | jq -c '.daily | {day, claimable, gifts}')"
echo "take day 1: $(p "$S" /v1/daily/claim "$(j --arg r "$(rid)" '{requestId:$r}')" | jq -c '{daily: .daily | {day, claimable}, sorn: .inventory.sorn}')"
echo "take again: $(p "$S" /v1/daily/claim "$(j --arg r "$(rid)" '{requestId:$r}')" | jq -c '{code, message}')"
p "$S" /v1/lobby/select "$(j --arg c "$B" '{characterId:$c}')" > /dev/null
echo "the second hero: $(g "$S" /v1/me | jq -c '.daily | {day, claimable, secondsToNext}')"
curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S" | jq -c .
