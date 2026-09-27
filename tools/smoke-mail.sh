#!/usr/bin/env bash
# Mailbox smoke test (27 Sep 2026; a Development server: dev grant). Two fresh Vanguards: A lists 5 Turnstones and a
# bag piece, B buys both; A's pay comes by letter (MENU's count, the mailbox, TAKE and TAKE ALL, a second take refused,
# THROW AWAY). With LOCAL_DB set (a psql connection string) a Korshard listing and a piece forced past their time come
# home by letter on B's read: the piece is out of A's bag until taken. Deletes both.
# Needs curl, jq.
#   tools/smoke-mail.sh [http://localhost:5080]
#   LOCAL_DB=postgresql://orsuun:orsuun-dev@localhost/orsuun tools/smoke-mail.sh
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
g() { curl -s "$BASE$2" -H "X-Session: $1"; }
j() { jq -nc "$@"; }
new() { curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "$(j --arg d "smoke-mail-$(rid)" '{deviceToken:$d}')" | jq -r .sessionToken; }
EMPTY="00000000-0000-0000-0000-000000000000"
LETTERS='[.letters[] | {id, kind, title, taken, sorn, goodId, goodCount, item: (.item.name // null)}]'
A=$(new); B=$(new)
p "$A" /v1/dev/grant '{}' > /dev/null; p "$B" /v1/dev/grant '{}' > /dev/null
p "$A" "/v1/dev/gear?level=5&upgrade=0" '{}' > /dev/null   # the worn set goes to the bag: pieces to list
PIECE=$(g "$A" /v1/me | jq -r '[.items[] | select(.equipped == false)][0].id')
echo "A's sorn and bag: $(g "$A" /v1/me | jq -c '{sorn: .inventory.sorn, bag: ([.items[] | select(.equipped == false)] | length)}')"
echo "A lists 5 Turnstones: $(p "$A" /v1/market/list "$(j --arg r "$(rid)" --arg e "$EMPTY" '{requestId:$r, itemId:$e, price:5000, goodId:5, goodCount:5}')" | jq -c .message)"
echo "A lists a piece: $(p "$A" /v1/market/list "$(j --arg r "$(rid)" --arg i "$PIECE" '{requestId:$r, itemId:$i, price:8000, goodId:-1, goodCount:0}')" | jq -c .message)"
L1=$(g "$B" "/v1/market?goods=true" | jq -r '[.listings[] | select(.goodId == 5 and .mine == false)][0].id')
L2=$(g "$B" /v1/market | jq -r --arg i "$PIECE" '[.listings[] | select(.item.id == $i)][0].id')
for L in "$L1" "$L2"; do
  echo "B buys listing $L: $(p "$B" /v1/market/buy "$(j --arg r "$(rid)" --argjson l "$L" '{requestId:$r, listingId:$l}')" | jq -c .message)"
done
echo "A's unread letters on /me: $(g "$A" /v1/me | jq -c '{mail, sorn: .inventory.sorn}')"
MAIL=$(g "$A" /v1/mail)
echo "A's mailbox: $(echo "$MAIL" | jq -c "$LETTERS")"
echo "unread after opening: $(g "$A" /v1/me | jq -c .mail)"
FIRST=$(echo "$MAIL" | jq -r '[.letters[] | select(.sorn > 0)][0].id')
echo "throw away before taking: $(p "$A" /v1/mail/delete "$(j --argjson l "$FIRST" '{letterId:$l}')" | jq -c '{code, message}')"
TAKE=$(rid)
echo "A takes one: $(p "$A" /v1/mail/take "$(j --arg r "$TAKE" --argjson l "$FIRST" '{requestId:$r, letterId:$l}')" | jq -c '{message, sorn: .state.inventory.sorn}')"
echo "the same request again: $(p "$A" /v1/mail/take "$(j --arg r "$TAKE" --argjson l "$FIRST" '{requestId:$r, letterId:$l}')" | jq -c '{code}')"
echo "A takes all: $(p "$A" /v1/mail/take "$(j --arg r "$(rid)" '{requestId:$r, letterId:0}')" | jq -c '{message, sorn: .state.inventory.sorn}')"
echo "nothing left: $(p "$A" /v1/mail/take "$(j --arg r "$(rid)" '{requestId:$r, letterId:0}')" | jq -c .message)"
echo "A throws the taken away: $(p "$A" /v1/mail/delete '{"letterId":0}' | jq -c '{message, left: (.letters | length)}')"
if [ -n "${LOCAL_DB:-}" ]; then
  p "$A" "/v1/dev/gear?level=5&upgrade=0" '{}' > /dev/null
  PIECE2=$(g "$A" /v1/me | jq -r '[.items[] | select(.equipped == false)][0].id')
  p "$A" /v1/market/list "$(j --arg r "$(rid)" --arg e "$EMPTY" '{requestId:$r, itemId:$e, price:3000, goodId:13, goodCount:3}')" > /dev/null
  p "$A" /v1/market/list "$(j --arg r "$(rid)" --arg i "$PIECE2" '{requestId:$r, itemId:$i, price:9000, goodId:-1, goodCount:0}')" > /dev/null
  echo "listed: korshards $(g "$A" /v1/me | jq -c .inventory.korshards), bag $(g "$A" /v1/me | jq -c '[.items[] | select(.equipped == false)] | length')"
  psql "$LOCAL_DB" -qc "UPDATE \"MarketListings\" SET \"ExpiresUtc\" = now() - interval '1 minute' WHERE \"Status\" = 0 AND (\"GoodId\" = 13 OR \"ItemId\" = '$PIECE2')" > /dev/null
  g "$B" /v1/market > /dev/null
  echo "ran out on B's read; A's bag $(g "$A" /v1/me | jq -c '[.items[] | select(.equipped == false)] | length'), mail $(g "$A" /v1/me | jq -c .mail)"
  echo "A's letters: $(g "$A" /v1/mail | jq -c "$LETTERS")"
  echo "A takes all: $(p "$A" /v1/mail/take "$(j --arg r "$(rid)" '{requestId:$r, letterId:0}')" | jq -c '{message, korshards: .state.inventory.korshards, bag: ([.state.items[] | select(.equipped == false)] | length)}')"
fi
for S in "$A" "$B"; do curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S" | jq -c .; done
