#!/usr/bin/env bash
# Social smoke test: chat, guild join requests, the Salt Exchange, sign up / sign in. Makes its own accounts and deletes
# them at the end, so it is safe against the live server (it leaves chat lines' system events only). Needs curl, jq.
#   tools/smoke-social.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
guest() { curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "{\"deviceToken\":\"$1\"}"; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
g() { curl -s "$BASE$2" -H "X-Session: $1"; }
# Bodies are built with jq and kept in variables: macOS bash 3.2 brace-expands "{..,..}" inside "$(...)".
j() { jq -nc "$@"; }

D1="smoke-$(rid)"; D2="smoke-$(rid)"
A=$(guest "$D1"); S1=$(echo "$A" | jq -r .sessionToken); ID1=$(echo "$A" | jq -r .accountId)
B=$(guest "$D2"); S2=$(echo "$B" | jq -r .sessionToken); ID2=$(echo "$B" | jq -r .accountId)
p "$S1" /v1/dev/grant '{}' > /dev/null; p "$S2" /v1/dev/grant '{}' > /dev/null

# Chat
B1=$(j '{channel:"world", text:"hello  steppe, well fuck that boar"}')
R=$(p "$S1" /v1/chat "$B1"); echo "say: $(echo "$R" | jq -c '[.lines[-1] | {text, mine}]')"
B1=$(j '{channel:"world", text:"again"}')
R=$(p "$S1" /v1/chat "$B1"); echo "say again at once: $(echo "$R" | jq -c .code)"
LINE=$(g "$S2" "/v1/chat?channel=world" | jq -r --arg id "$ID1" '[.lines[] | select(.accountId==$id)][-1].id')
echo "other player sees it: line $LINE"
B1=$(j --argjson m "$LINE" '{messageId:$m}')
R=$(p "$S2" /v1/chat/report "$B1"); echo "report: $(echo "$R" | jq -c '.lines | length')"
B1=$(j --arg a "$ID1" '{accountId:$a, block:true}')
R=$(p "$S2" /v1/chat/block "$B1"); echo "block: $(echo "$R" | jq -c --arg id "$ID1" '{blocked, fromThem: ([.lines[] | select(.accountId==$id)] | length)}')"
B1=$(j '{accountId:"00000000-0000-0000-0000-000000000000", block:false}')
R=$(p "$S2" /v1/chat/block "$B1"); echo "unblock all: $(echo "$R" | jq -c '{blocked}')"
echo "guild chat without a guild: $(g "$S1" "/v1/chat?channel=guild" | jq -c .code)"

# Guild requests and the log
TAG="Q$(rid | cut -c1-3 | tr 'a-z' 'A-Z')"
B1=$(j --arg r "$(rid)" --arg t "$TAG" '{requestId:$r, name:("Smoke " + $t), tag:$t, color:"#8E44AD"}')
G=$(p "$S1" /v1/guild/create "$B1" | jq -r .mine.id)
B1=$(j --arg r "$(rid)" '{requestId:$r, open:false, color:"#8E44AD"}')
p "$S1" /v1/guild/settings "$B1" > /dev/null
B1=$(j --arg r "$(rid)" --arg g "$G" '{requestId:$r, guildId:$g}')
R=$(p "$S2" /v1/guild/join "$B1"); echo "ask a shut guild: $(echo "$R" | jq -c --arg t "$TAG" '{message, requested: [.browse[] | select(.tag==$t) | .requested]}')"
echo "leader sees: $(g "$S1" /v1/guild | jq -c '{requests: [.requests[] | .name], log: .log}')"
B1=$(j --arg r "$(rid)" --arg a "$ID2" '{requestId:$r, accountId:$a, accept:true}')
R=$(p "$S1" /v1/guild/answer "$B1"); echo "accept: $(echo "$R" | jq -c '{members: .mine.members, message}')"
B1=$(j '{channel:"guild", text:"glad to be here"}')
R=$(p "$S2" /v1/chat "$B1"); echo "guild chat: $(echo "$R" | jq -c '[.lines[] | {system, text}]')"

# The Salt Exchange
ITEM=$(g "$S1" /v1/me | jq -r '[.items[] | select(.equipped|not)][0].id')
if [ "$ITEM" = "null" ]; then
  # A fresh account has only its worn weapon: push a few stages for a drop.
  for i in 1 2 3; do B1=$(j --arg r "$(rid)" '{requestId:$r}'); p "$S1" /v1/push "$B1" > /dev/null; done
  ITEM=$(g "$S1" /v1/me | jq -r '[.items[] | select(.equipped|not)][0].id')
fi
WORN=$(g "$S1" /v1/me | jq -r '[.items[] | select(.equipped)][0].id')
B1=$(j --arg r "$(rid)" --arg i "$WORN" '{requestId:$r, itemId:$i, price:5000}')
R=$(p "$S1" /v1/market/list "$B1"); echo "list a worn piece: $(echo "$R" | jq -c .code)"
echo "bag piece to sell: $ITEM"
if [ "$ITEM" != "null" ]; then
  B1=$(j --arg r "$(rid)" --arg i "$ITEM" '{requestId:$r, itemId:$i, price:50000}')
  L=$(p "$S1" /v1/market/list "$B1")
  echo "list: $(echo "$L" | jq -c --arg i "$ITEM" '{message, mine: [.mine[] | {price, status}], inBag: ([.state.items[] | select(.id==$i)] | length)}')"
  LID=$(echo "$L" | jq -r '.mine[0].id')
  B1=$(j --arg r "$(rid)" --argjson l "$LID" '{requestId:$r, listingId:$l}')
  R=$(p "$S1" /v1/market/buy "$B1"); echo "buy own: $(echo "$R" | jq -c .code)"
  SORN1=$(g "$S1" /v1/me | jq .inventory.sorn)
  echo "browse: $(g "$S2" "/v1/market?sort=newest" | jq -c '{total, first: (.listings[0] | {price, seller: .sellerName})}')"
  B1=$(j --arg r "$(rid)" --argjson l "$LID" '{requestId:$r, listingId:$l}')
  R=$(p "$S2" /v1/market/buy "$B1"); echo "buy: $(echo "$R" | jq -c --arg i "$ITEM" '{message, has: ([.state.items[] | select(.id==$i)] | length)}')"
  echo "seller paid: $(( $(g "$S1" /v1/me | jq .inventory.sorn) - SORN1 )) (50000 less 5%)"
  B1=$(j --arg r "$(rid)" --argjson l "$LID" '{requestId:$r, listingId:$l}')
  R=$(p "$S2" /v1/market/buy "$B1"); echo "buy again: $(echo "$R" | jq -c .code)"
  B1=$(j --arg r "$(rid)" --arg i "$ITEM" '{requestId:$r, itemId:$i, price:70000}')
  L2=$(p "$S2" /v1/market/list "$B1")
  LID2=$(echo "$L2" | jq -r '[.mine[] | select(.status=="Active")][0].id')
  B1=$(j --arg r "$(rid)" --argjson l "$LID2" '{requestId:$r, listingId:$l}')
  R=$(p "$S2" /v1/market/cancel "$B1"); echo "cancel: $(echo "$R" | jq -c --arg i "$ITEM" '{message, back: ([.state.items[] | select(.id==$i)] | length)}')"
fi

# Sign up, sign in on a new device, sign out
EMAIL="smoke-$(rid | cut -c1-8)@example.com"
B1=$(j --arg e "$EMAIL" '{email:$e, password:"12345678"}')
R=$(p "$S1" /v1/auth/register "$B1"); echo "register weak: $(echo "$R" | jq -c .code)"
B1=$(j --arg e "$EMAIL" '{email:$e, password:"korstone-breaker"}')
R=$(p "$S1" /v1/auth/register "$B1"); echo "register: $(echo "$R" | jq -c .email)"
R=$(p "$S2" /v1/auth/register "$B1"); echo "register taken: $(echo "$R" | jq -c .code)"
B1=$(j --arg e "$EMAIL" '{email:$e, password:"nope-nope-nope", deviceToken:"x"}')
R=$(curl -s -X POST "$BASE/v1/auth/login" -H 'Content-Type: application/json' -d "$B1"); echo "wrong password: $(echo "$R" | jq -c .code)"
D3="smoke-$(rid)"
B1=$(j --arg e "$EMAIL" --arg d "$D3" '{email:$e, password:"korstone-breaker", deviceToken:$d}')
LG=$(curl -s -X POST "$BASE/v1/auth/login" -H 'Content-Type: application/json' -d "$B1")
[ "$(echo "$LG" | jq -r .accountId)" = "$ID1" ] && SAME=yes || SAME=no
echo "login on a new device: same account=$SAME"
S3=$(echo "$LG" | jq -r .sessionToken)
echo "both devices work: $(g "$S1" /v1/me | jq -r .email) / $(g "$S3" /v1/me | jq -r .email)"
[ "$(guest "$D3" | jq -r .accountId)" = "$ID1" ] && SAME=yes || SAME=no
echo "that device's guest login lands there: $SAME"
S3=$(guest "$D3" | jq -r .sessionToken)
echo "sign out: $(p "$S3" /v1/auth/signout '{}' | jq -c .)"
echo "session after sign out: $(curl -s -o /dev/null -w '%{http_code}' "$BASE/v1/me" -H "X-Session: $S3")"

# Clean up: both accounts (the guild disbands with its last member).
curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S1" | jq -c .
S2=$(guest "$D2" | jq -r .sessionToken)
curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S2" | jq -c .
B1=$(j --arg e "$EMAIL" '{email:$e, password:"korstone-breaker", deviceToken:"y"}')
R=$(curl -s -X POST "$BASE/v1/auth/login" -H 'Content-Type: application/json' -d "$B1"); echo "email free again: $(echo "$R" | jq -c .code)"
