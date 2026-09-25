#!/usr/bin/env bash
# Dungeon smoke test: a fresh account is set to have cleared stage 12 (/v1/dev/stage, Development only), enters the
# Hollow Spire, answers the Chained Smith (forging the weapon) and finishes the run; the second key is spent walking past
# the smith; a third entry is refused. Then a second hero in late gear (/v1/dev/gear) clears Silkmother's Warren (the
# Silkmother's Khan's Alloy) and answers the Carvers' Archive's rune lock (the open vault's Master's Needle). Deletes its
# accounts at the end. Needs curl, jq.
#   tools/smoke-dungeon.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
j() { jq -nc "$@"; }

S=$(curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "{\"deviceToken\":\"smoke-dungeon-$(rid)\"}" | jq -r .sessionToken)
p "$S" /v1/dev/grant '{}' > /dev/null
B1=$(j --arg r "$(rid)" '{requestId:$r, dungeonId:1}')
echo "locked before stage 10: $(p "$S" /v1/dungeon/enter "$B1" | jq -c .code)"
p "$S" "/v1/dev/stage?cleared=12" '{}' | jq -c '{highestStageCleared, dungeonRunsLeft}'

enter() {
  local body; body=$(j --arg r "$(rid)" '{requestId:$r, dungeonId:1}')
  p "$S" /v1/dungeon/enter "$body"
}
R=$(enter)
echo "enter: $(echo "$R" | jq -c '{runId, level, floors: [.floors[] | {floor, cleared}], atSmith, fellOn, keys: .state.dungeonRunsLeft, waiting: .state.dungeonRunAtSmith, text}')"
RUN=$(echo "$R" | jq -r .runId)
echo "enter again while the smith waits: $(enter | jq -c .code)"
if [ "$(echo "$R" | jq -r .atSmith)" = "true" ]; then
  WEAPON=$(echo "$R" | jq -r '[.state.items[] | select(.equipped and .slot=="Weapon")][0].id')
  B1=$(j --arg r "$(rid)" --argjson run "$RUN" --arg i "$WEAPON" '{requestId:$r, runId:$run, itemId:$i}')
  R=$(p "$S" /v1/dungeon/smith "$B1")
  echo "smith: $(echo "$R" | jq -c '{smith, smithItem, floors: [.floors[] | {floor, cleared}], cleared, fellOn, chest, waiting: .state.dungeonRunAtSmith, text}')"
fi
R=$(enter)
echo "second key: $(echo "$R" | jq -c '{atSmith, fellOn, keys: .state.dungeonRunsLeft}')"
if [ "$(echo "$R" | jq -r .atSmith)" = "true" ]; then
  B1=$(j --arg r "$(rid)" --argjson run "$(echo "$R" | jq -r .runId)" '{requestId:$r, runId:$run, itemId:""}')
  echo "walk past the smith: $(p "$S" /v1/dungeon/smith "$B1" | jq -c '{smithItem, floors: [.floors[] | .floor], cleared, fellOn}')"
fi
echo "third entry: $(enter | jq -c .code)"
curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S" | jq -c .

# Silkmother's Warren and the Carvers' Archive, with a hero geared for them.
S=$(curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "{\"deviceToken\":\"smoke-dungeon-$(rid)\"}" | jq -r .sessionToken)
p "$S" "/v1/dev/stage?cleared=45" '{}' > /dev/null
p "$S" "/v1/dev/gear?level=50&upgrade=9" '{}' | jq -c '{hero, level: .inventory.level, alloys: .inventory.khansAlloys, needles: .inventory.mastersNeedles}'
R=$(p "$S" /v1/dungeon/enter "$(j --arg r "$(rid)" '{requestId:$r, dungeonId:2}')")
echo "the Warren: $(echo "$R" | jq -c '{level, floors: [.floors[] | {floor, cleared}], cleared, fellOn, chest, alloys: .state.inventory.khansAlloys, text}')"
R=$(p "$S" /v1/dungeon/enter "$(j --arg r "$(rid)" '{requestId:$r, dungeonId:3}')")
echo "the Archive: $(echo "$R" | jq -c '{level, floors: [.floors[] | {floor, cleared}], atSmith, pause, riddle, runes, paused: .state.dungeonPausedId, text}')"
if [ "$(echo "$R" | jq -r .pause)" = "RuneLock" ]; then
  RUN=$(echo "$R" | jq -r .runId)
  echo "a wrong answer would be one of: $(echo "$R" | jq -c .runes)"
  ANSWER=$(cd "$(dirname "$0")/.." && grep -o "(\"[^\"]*\", \"[A-Za-z]*\", \"[A-Za-z]*\", \"[A-Za-z]*\")" src/Orsuun.Rules/Dungeons.cs | while read -r line; do
    t=$(echo "$line" | sed -E 's/^\("(.*)", "([A-Za-z]+)", "[A-Za-z]+", "[A-Za-z]+"\)$/\1/'); a=$(echo "$line" | sed -E 's/.*", "([A-Za-z]+)", "[A-Za-z]+", "[A-Za-z]+"\)$/\1/')
    [ "$t" = "$(echo "$R" | jq -r .riddle)" ] && echo "$a"; done)
  echo "the answer to \"$(echo "$R" | jq -r .riddle)\": $ANSWER"
  R=$(p "$S" /v1/dungeon/smith "$(j --arg r "$(rid)" --argjson run "$RUN" --arg a "$ANSWER" '{requestId:$r, runId:$run, itemId:"", rune:$a}')")
  echo "the rune lock: $(echo "$R" | jq -c '{floors: [.floors[] | {floor, cleared}], cleared, fellOn, chest, needles: .state.inventory.mastersNeedles, paused: .state.dungeonPausedId, text}')"
fi
curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S" | jq -c .
