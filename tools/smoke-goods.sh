#!/usr/bin/env bash
# Exchange goods smoke test (26 Sep 2026; a Development server: dev grant). Two fresh Vanguards: A lists 5 Turnstones and
# 2 Rider Korshards, B buys the Turnstones, the price history then shows the sale for one, A takes the Korshards back,
# and (with LOCAL_DB set, a psql connection string) a listing forced past its time returns to A on B's read. Deletes both.
# Needs curl, jq.
#   tools/smoke-goods.sh [http://localhost:5080]
#   LOCAL_DB=postgresql://orsuun:orsuun-dev@localhost/orsuun tools/smoke-goods.sh
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
g() { curl -s "$BASE$2" -H "X-Session: $1"; }
j() { jq -nc "$@"; }
new() { curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "$(j --arg d "smoke-goods-$(rid)" '{deviceToken:$d}')" | jq -r .sessionToken; }
EMPTY="00000000-0000-0000-0000-000000000000"
K='{turnstones: .inventory.turnstones, korshards: .inventory.korshards, sorn: .inventory.sorn}'
A=$(new); B=$(new)
p "$A" /v1/dev/grant '{}' > /dev/null; p "$B" /v1/dev/grant '{}' > /dev/null
echo "A after the grant: $(g "$A" /v1/me | jq -c "$K")"
echo "history before: $(g "$A" "/v1/market/history?kind=good&id=5" | jq -c '{what, sales}')"
echo "A lists 5 Turnstones: $(p "$A" /v1/market/list "$(j --arg r "$(rid)" --arg e "$EMPTY" '{requestId:$r, itemId:$e, price:5000, goodId:5, goodCount:5}')" | jq -c '{message, turnstones: .state.inventory.turnstones}')"
echo "A lists 2 Rider Korshards: $(p "$A" /v1/market/list "$(j --arg r "$(rid)" --arg e "$EMPTY" '{requestId:$r, itemId:$e, price:3000, goodId:12, goodCount:2}')" | jq -c '{message, korshards: .state.inventory.korshards}')"
echo "too many: $(p "$A" /v1/market/list "$(j --arg r "$(rid)" --arg e "$EMPTY" '{requestId:$r, itemId:$e, price:3000, goodId:5, goodCount:100000}')" | jq -c '{code, message}')"
echo "not tradable: $(p "$A" /v1/market/list "$(j --arg r "$(rid)" --arg e "$EMPTY" '{requestId:$r, itemId:$e, price:3000, goodId:99, goodCount:1}')" | jq -c '{code, message}')"
echo "B sees GOODS: $(g "$B" "/v1/market?goods=true" | jq -c '[.listings[] | {goodId, goodCount, price}]')"
echo "WPN does not list them: $(g "$B" "/v1/market?slot=Weapon" | jq -c '[.listings[] | select(.goodId >= 0)] | length')"
L=$(g "$B" "/v1/market?goods=true" | jq -r '[.listings[] | select(.goodId == 5 and .mine == false)][0].id')
echo "B buys the Turnstones: $(p "$B" /v1/market/buy "$(j --arg r "$(rid)" --argjson l "$L" '{requestId:$r, listingId:$l}')" | jq -c '{message, turnstones: .state.inventory.turnstones}')"
echo "history after: $(g "$A" "/v1/market/history?kind=good&id=5" | jq -c '{what, sales, average, last, recent}')"
echo "A was paid: $(g "$A" /v1/me | jq -c "$K")"
K2=$(g "$A" /v1/market | jq -r '[.mine[] | select(.goodId == 12 and .status == "Active")][0].id')
echo "A takes the Korshards back: $(p "$A" /v1/market/cancel "$(j --arg r "$(rid)" --argjson l "$K2" '{requestId:$r, listingId:$l}')" | jq -c '{message, korshards: .state.inventory.korshards}')"
if [ -n "${LOCAL_DB:-}" ]; then
  echo "A lists 3 Captain Korshards: $(p "$A" /v1/market/list "$(j --arg r "$(rid)" --arg e "$EMPTY" '{requestId:$r, itemId:$e, price:3000, goodId:13, goodCount:3}')" | jq -c '.state.inventory.korshards')"
  psql "$LOCAL_DB" -qc "UPDATE \"MarketListings\" SET \"ExpiresUtc\" = now() - interval '1 minute' WHERE \"GoodId\" = 13 AND \"Status\" = 0" > /dev/null
  g "$B" /v1/market > /dev/null
  echo "after it ran out on B's read, A holds: $(g "$A" /v1/me | jq -c .inventory.korshards)"
fi
for S in "$A" "$B"; do curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S" | jq -c .; done
