#!/usr/bin/env bash
# Banner change and Oath Renewal smoke test (25 Sep 2026; needs a Development server for the dev grant and level). A fresh
# hero swears to the Ember Banner, cannot change without Oathstones, changes to Sky with the grant's five, cannot change
# twice a season; cannot renew below level 105, renews at 105 (level 1, +3% attack and HP), and is deleted. Needs curl, jq.
#   tools/smoke-oaths.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
j() { jq -nc "$@"; }
S=$(curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "$(j --arg d "smoke-oaths-$(rid)" '{deviceToken:$d}')" | jq -r .sessionToken)
echo "swear Ember: $(p "$S" /v1/banner '{"banner":"Ember"}' | jq -c '{banner, stones: .inventory.oathstones}')"
echo "change with no stones: $(p "$S" /v1/banner/change "$(j --arg r "$(rid)" '{requestId:$r, banner:"Sky"}')" | jq -c '{code, message}')"
p "$S" /v1/dev/grant '{}' > /dev/null
echo "change to Sky: $(p "$S" /v1/banner/change "$(j --arg r "$(rid)" '{requestId:$r, banner:"Sky"}')" | jq -c '{banner, stones: .inventory.oathstones}')"
p "$S" /v1/dev/grant '{}' > /dev/null
echo "change again this season: $(p "$S" /v1/banner/change "$(j --arg r "$(rid)" '{requestId:$r, banner:"Gold"}')" | jq -c '{code, message}')"
echo "renew at level 1: $(p "$S" /v1/renew "$(j --arg r "$(rid)" '{requestId:$r}')" | jq -c '{code, message}')"
L=$(p "$S" "/v1/dev/level?level=105" '{}' | jq -c '{level: .inventory.level, attack: .hero.attack, hp: .hero.maxHp, renewals}')
echo "at level 105: $L"
echo "renew: $(p "$S" /v1/renew "$(j --arg r "$(rid)" '{requestId:$r}')" | jq -c '{level: .inventory.level, attack: .hero.attack, hp: .hero.maxHp, renewals}')"
curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S" | jq -c .
