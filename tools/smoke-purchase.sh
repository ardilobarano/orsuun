#!/usr/bin/env bash
# Amber store purchase smoke test (27 Sep 2026) against a Development server: a test-store receipt credits a pack once
# (the first purchase with its bonus), the same receipt again adds nothing, an unknown product and a malformed receipt are
# refused, and the real stores say they are not set up (no keys on a test server). Deletes the hero.
# Needs curl, jq.
#   tools/smoke-purchase.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
j() { jq -nc "$@"; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
S=$(curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "$(j --arg d "smoke-buy-$(rid)" '{deviceToken:$d}')" | jq -r .sessionToken)
T="test-$(rid)"
echo "amber before: $(curl -s "$BASE/v1/me" -H "X-Session: $S" | jq .wardrobe.amber)"
echo "buy 300 pack: $(p "$S" /v1/caravan/purchase "$(j --arg r "$T" '{store:"test", productId:"orsuun.amber.300", receipt:$r}')" | jq -c '{message, added, amber, total: .state.wardrobe.amber}')"
echo "same receipt: $(p "$S" /v1/caravan/purchase "$(j --arg r "$T" '{store:"test", productId:"orsuun.amber.300", receipt:$r}')" | jq -c '{message, added, total: .state.wardrobe.amber}')"
echo "another 60:   $(p "$S" /v1/caravan/purchase "$(j --arg r "test-$(rid)" '{store:"test", productId:"orsuun.amber.60", receipt:$r}')" | jq -c '{added, amber, total: .state.wardrobe.amber}')"
echo "no product:   $(p "$S" /v1/caravan/purchase "$(j '{store:"test", productId:"orsuun.amber.1", receipt:"test-x"}')" | jq -c .code)"
echo "bad receipt:  $(p "$S" /v1/caravan/purchase "$(j '{store:"test", productId:"orsuun.amber.60", receipt:"forged"}')" | jq -c .code)"
echo "app store:    $(p "$S" /v1/caravan/purchase "$(j '{store:"apple", productId:"orsuun.amber.60", receipt:"2000000123456789"}')" | jq -c .code)"
echo "google play:  $(p "$S" /v1/caravan/purchase "$(j '{store:"google", productId:"orsuun.amber.60", receipt:"token"}')" | jq -c .code)"
curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S" | jq -c .
