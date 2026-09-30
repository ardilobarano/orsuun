#!/usr/bin/env bash
# Map quests smoke test (30 Sep 2026, Rules.MapQuests). A fresh hero: the twelve chains (the Oathfields' open, the rest
# shut); claiming early is refused; the first step counted by the dev tool and claimed (sorn and XP rise); the Korstones and
# Commander steps; the last claim pays the chain's piece; a finished chain refuses more. Deletes the hero.
# Needs curl, jq; a local Development server.
#   tools/smoke-quests.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
g() { curl -s "$BASE$2" -H "X-Session: $1"; }
claim() { p "$1" /v1/quests/claim "$(jq -nc --arg r "$(rid)" --argjson m "$2" '{requestId:$r, map:$m}')"; }
S=$(curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "$(jq -nc --arg d "smoke-quests-$(rid)" '{deviceToken:$d}')" | jq -r .sessionToken)
echo "chains: $(g "$S" /v1/quests | jq -c '{hunting, maps: (.maps | length), open: [.maps[] | select(.open) | .map], first: .maps[0] | {giver, title, step, task, camp, target}}')"
echo "state's quest: $(g "$S" /v1/me | jq -c '.quest | {map, step, task, progress, ready}')"
echo "claim early: $(claim "$S" 1 | jq -c '{code, message}')"
p "$S" "/v1/dev/quest?map=1&amount=600" '{}' > /dev/null
BEFORE=$(g "$S" /v1/me | jq -c '{sorn: .inventory.sorn, xp: .inventory.xp}')
R=$(claim "$S" 1)
echo "claim step 1: $(echo "$R" | jq -c '{step: .quest.step, task: .quest.task, camp: .quest.camp, code}')  before $BEFORE after $(echo "$R" | jq -c '{sorn: .inventory.sorn, xp: .inventory.xp}')"
echo "the Korstones step counted (dev): $(p "$S" "/v1/dev/quest?map=1&amount=8" '{}' | jq -c '.maps[0] | {step, progress, ready}')"
echo "claim step 2: $(claim "$S" 1 | jq -c '{step: .quest.step, task: .quest.task, piece: .quest.piece}')"
p "$S" "/v1/dev/quest?map=1&amount=1" '{}' > /dev/null
BAG=$(g "$S" /v1/me | jq '.items | length')
R=$(claim "$S" 1)
echo "claim step 3: $(echo "$R" | jq -c '{step: .quest.step, steps: .quest.steps, ask: .quest.ask[0:40]}')  pieces $BAG -> $(echo "$R" | jq '.items | length') (epic: $(echo "$R" | jq '[.items[] | select(.rarity == "Epic")] | length'))"
echo "claim a finished chain: $(claim "$S" 1 | jq -c .code)"
curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S" > /dev/null
echo "deleted"
