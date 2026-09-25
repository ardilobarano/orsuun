#!/usr/bin/env bash
# Skill books smoke test (26 Sep 2026; a Development server: dev grant, dev skill). Two fresh Vanguards: A reads Iron Whirl
# (one read, then the 8 hour rest), burns an Oathstone at M10 with Honor, lists Rending Arc books on the Exchange where B
# buys them, and hands B a book in a direct trade. Deletes both. Needs curl, jq.
#   tools/smoke-books.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
g() { curl -s "$BASE$2" -H "X-Session: $1"; }
j() { jq -nc "$@"; }
new() { curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "$(j --arg d "smoke-books-$(rid)" '{deviceToken:$d}')" | jq -r .sessionToken; }
K='{books: .inventory.books[0:3], grades: .skillGrades[0:3], progress: .skillProgress[0:3], honor, stones: .inventory.oathstones}'
A=$(new); B=$(new)
p "$A" /v1/dev/grant '{}' > /dev/null; p "$B" /v1/dev/grant '{}' > /dev/null
echo "A after the grant: $(g "$A" /v1/me | jq -c "$K")"
echo "read Iron Whirl: $(p "$A" /v1/skills/train "$(j --arg r "$(rid)" '{requestId:$r, slot:1}')" | jq -c '{success, grade, message, books: .state.inventory.books[1], ready: .state.skillReadySeconds[1]}')"
echo "read again at once: $(p "$A" /v1/skills/train "$(j --arg r "$(rid)" '{requestId:$r, slot:1}')" | jq -c '{code, message}')"
p "$A" "/v1/dev/skill?book=2&grade=10" '{}' > /dev/null
echo "Blood Fury at M10, an Oathstone: $(p "$A" /v1/skills/train "$(j --arg r "$(rid)" '{requestId:$r, slot:2}')" | jq -c '{success, grade, message, honor: .state.honor, stones: .state.inventory.oathstones}')"
echo "A lists 2 Rending Arc books: $(p "$A" /v1/market/list "$(j --arg r "$(rid)" '{requestId:$r, itemId:"00000000-0000-0000-0000-000000000000", price:1000, bookId:0, bookCount:2}')" | jq -c '{message, mine: [.mine[] | {bookId, bookCount, price}], books: .state.inventory.books[0]}')"
L=$(g "$B" "/v1/market?books=true" | jq -r '[.listings[] | select(.bookId == 0)][0].id')
SB=$(g "$B" /v1/me | jq .inventory.sorn)
echo "B buys them: $(p "$B" /v1/market/buy "$(j --arg r "$(rid)" --argjson l "$L" '{requestId:$r, listingId:$l}')" | jq -c '{message, books: .state.inventory.books[0]}') (sorn $SB -> $(g "$B" /v1/me | jq .inventory.sorn))"
NB=$(g "$B" /v1/me | jq -r .name)
T=$(p "$A" /v1/trade/invite "$(j --arg r "$(rid)" --arg n "$NB" '{requestId:$r, name:$n}')" | jq -r .id)
p "$B" /v1/trade/accept "$(j --arg r "$(rid)" --argjson t $T '{requestId:$r, tradeId:$t}')" > /dev/null
echo "A offers a book: $(p "$A" /v1/trade/offer "$(j --arg r "$(rid)" --argjson t $T '{requestId:$r, tradeId:$t, itemIds:[], sorn:0, books:[{bookId:1, count:1}]}')" | jq -c '{myBooks, theirBooks}')"
sleep 6
for who in "$A" "$B"; do p "$who" /v1/trade/press "$(j --arg r "$(rid)" --argjson t $T '{requestId:$r, tradeId:$t}')" > /dev/null; done
for who in "$A" "$B"; do p "$who" /v1/trade/press "$(j --arg r "$(rid)" --argjson t $T '{requestId:$r, tradeId:$t}')" | jq -c '{state, message}'; done
echo "B's books now: $(g "$B" /v1/me | jq -c '.inventory.books[0:3]')  A's: $(g "$A" /v1/me | jq -c '.inventory.books[0:3]')"
for S in "$A" "$B"; do curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S" | jq -c .; done
