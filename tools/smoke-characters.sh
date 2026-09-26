#!/usr/bin/env bash
# Characters smoke test (25 Sep 2026): a new device signs in with the character screen (lobby), swears the account's
# oath there, makes two characters, plays the first (Amber pack, a push for a drop), puts a piece in the depot, plays the second (the same
# Amber and Banner, takes the piece), then deletes the second by name and the account. Needs curl, jq.
#   tools/smoke-characters.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
g() { curl -s "$BASE$2" -H "X-Session: $1"; }
j() { jq -nc "$@"; }
L='{characters: [.characters[] | "\(.slot) \(.name) \(.class) \(.figure) L\(.level)"], banner, amber, message}'
TAG=$(rid | tr -d 'a-f' | cut -c1-5)

LOGIN=$(curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "$(j --arg d "smoke-chars-$(rid)" '{deviceToken:$d, lobby:true}')")
S=$(echo "$LOGIN" | jq -r .sessionToken)
echo "sign in: $(echo "$LOGIN" | jq -c '{accountId, created, characters}')"
echo "/me before a character: $(g "$S" /v1/me | jq -c .code)"
echo "lobby: $(g "$S" /v1/lobby | jq -c "$L")"
echo "oath at the character screen: $(p "$S" /v1/lobby/banner '{"banner":"Sky"}' | jq -c '{banner, message}')"
echo "swear again: $(p "$S" /v1/lobby/banner '{"banner":"Gold"}' | jq -c .code)"
echo "bad name: $(p "$S" /v1/lobby/create "$(j '{name:"No Spaces", heroClass:"Vanguard"}')" | jq -c .code)"
A=$(p "$S" /v1/lobby/create "$(j --arg n "Arslan$TAG" '{name:$n, heroClass:"Vanguard", figure:"Woman"}')")
echo "create Arslan$TAG: $(echo "$A" | jq -c "$L")"
echo "same name again: $(p "$S" /v1/lobby/create "$(j --arg n "ARSLAN$TAG" '{name:$n, heroClass:"Kestrel"}')" | jq -c .code)"
B=$(p "$S" /v1/lobby/create "$(j --arg n "Borte$TAG" '{name:$n, heroClass:"Kestrel", slot:3}')")
echo "create Borte$TAG in slot 4: $(echo "$B" | jq -c "$L")"
ID1=$(echo "$B" | jq -r '.characters[0].id'); ID2=$(echo "$B" | jq -r '.characters[1].id')
echo "play the first: $(p "$S" /v1/lobby/select "$(j --arg c "$ID1" '{characterId:$c}')" | jq -c '{name, heroClass, banner}')"
echo "Amber pack: $(p "$S" /v1/caravan/amber "$(j --arg r "$(rid)" '{requestId:$r, packId:2}')" | jq -c .wardrobe.amber)"
echo "in-game oath after it: $(p "$S" /v1/banner '{"banner":"Ember"}' | jq -c .code)"
for i in 1 2 3 4 5 6; do p "$S" /v1/push "$(j --arg r "$(rid)" '{requestId:$r}')" > /dev/null; done
ITEM=$(g "$S" /v1/me | jq -r '[.items[] | select(.equipped | not)][0].id')
echo "a drop in the bag: $ITEM"
echo "depot put: $(p "$S" /v1/depot/put "$(j --arg r "$(rid)" --arg i "$ITEM" '{requestId:$r, itemId:$i}')" | jq -c '{depot: [.items[].name], message}')"
echo "put it again: $(p "$S" /v1/depot/put "$(j --arg r "$(rid)" --arg i "$ITEM" '{requestId:$r, itemId:$i}')" | jq -c .code)"
echo "play the second: $(p "$S" /v1/lobby/select "$(j --arg c "$ID2" '{characterId:$c}')" | jq -c '{name, heroClass, banner, amber: .wardrobe.amber}')"
echo "depot: $(g "$S" /v1/depot | jq -c '[.items[].name]')"
echo "take: $(p "$S" /v1/depot/take "$(j --arg r "$(rid)" --arg i "$ITEM" '{requestId:$r, itemId:$i}')" | jq -c '{depot: [.items[].name], bag: [.state.items[] | select(.equipped | not) | .name], message}')"
echo "take again: $(p "$S" /v1/depot/take "$(j --arg r "$(rid)" --arg i "$ITEM" '{requestId:$r, itemId:$i}')" | jq -c .code)"
echo "delete with a wrong name: $(p "$S" /v1/lobby/delete "$(j --arg c "$ID2" '{characterId:$c, name:"nope"}')" | jq -c .code)"
echo "delete the second: $(p "$S" /v1/lobby/delete "$(j --arg c "$ID2" --arg n "borte$TAG" '{characterId:$c, name:$n}')" | jq -c "$L")"
echo "/me after its delete: $(g "$S" /v1/me | jq -c .code)"
p "$S" /v1/lobby/select "$(j --arg c "$ID1" '{characterId:$c}')" > /dev/null
curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S" | jq -c .
echo "lobby after deleting the account: $(g "$S" /v1/lobby | jq -c .code)"
