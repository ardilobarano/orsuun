#!/usr/bin/env bash
# HTTP smoke test against a running dev server (bash port of smoke.ps1). Needs curl and jq.
#   tools/smoke.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
post() { curl -s -X POST "$BASE$1" -H 'Content-Type: application/json' -H "X-Session: ${SESSION:-}" -d "$2"; }

login=$(curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "{\"deviceToken\":\"smoke-$(rid)\"}")
SESSION=$(echo "$login" | jq -r .sessionToken)
echo "login: created=$(echo "$login" | jq .created)"

me=$(curl -s "$BASE/v1/me" -H "X-Session: $SESSION")
echo "me: atk=$(echo "$me" | jq .hero.attack) items=$(echo "$me" | jq '.items|length') parked=$(echo "$me" | jq .parkedStage)"
echo "park 2 while locked: $(post /v1/park '{"stage":2}' | jq -c .)"

for i in 1 2 3 4 5; do
  pu=$(post /v1/push "{\"requestId\":\"$(rid)\"}")
  echo "push: stage=$(echo "$pu" | jq .lastPush.stage) cleared=$(echo "$pu" | jq .lastPush.cleared) highest=$(echo "$pu" | jq .highestStageCleared)"
done

r=$(rid)
f=$(post /v1/forge "{\"requestId\":\"$r\",\"method\":\"ScrollOfMercy\"}")
echo "forge: $(echo "$f" | jq -r '.lastForge | "\(.outcome) +\(.levelBefore)->+\(.levelAfter)"')"
fr=$(post /v1/forge "{\"requestId\":\"$r\",\"method\":\"ScrollOfMercy\"}")
echo "forge replay: $(echo "$fr" | jq -c .)"
echo "turn: turnstones=$(post /v1/turn "{\"requestId\":\"$(rid)\"}" | jq .inventory.turnstones)"
echo "park hunting ground: parked=$(post /v1/park '{"stage":101}' | jq .parkedStage)"
echo "park field I: parked=$(post /v1/park '{"stage":111}' | jq .parkedStage)"
post /v1/dev/bosses-up '{}' > /dev/null
for id in 1 2 3; do
  bf=$(post /v1/boss/fight "{\"requestId\":\"$(rid)\",\"bossId\":$id}")
  echo "fight $id: $(echo "$bf" | jq -r '.lastBossFight | "damage=\(.damage) killed=\(.killed) rank=\(.rank)\n  \(.chest)"')"
done
fa=$(post /v1/boss/fight "{\"requestId\":\"$(rid)\",\"bossId\":1}")
echo "fight again: $(echo "$fa" | jq -c .)"
echo "client log: $(post /v1/client-log '{"platform":"Smoke","version":"0","message":"smoke test report","stack":"at Smoke()"}' | jq -c .)"
echo "delete account: $(curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $SESSION" | jq -c .)"
echo "me after delete: $(curl -s -o /dev/null -w '%{http_code}' "$BASE/v1/me" -H "X-Session: $SESSION")"
