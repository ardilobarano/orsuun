#!/usr/bin/env bash
# Friends and guild invites smoke test (25 Sep 2026). Three fresh heroes: A asks B by name, B sees the request on /me and
# takes it; C asks A and A asking back makes them friends at once; A takes C off; B blocks A (the friendship ends, A
# cannot ask again). A founds a shut guild and invites C by id (as from chat); C sees the invite on /me and the guild
# screen and joins through the shut gates. A asks C to trade by id. Deletes all three. Needs curl, jq; a Development
# server (dev grant, trade rules lifted).
#   tools/smoke-friends.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
g() { curl -s "$BASE$2" -H "X-Session: $1"; }
j() { jq -nc "$@"; }
F='{friends: [.friends[] | "\(.name) L\(.level) \(.minutesAway)m"], asking: [.asking[].name], asked: [.asked[].name], canInvite, message}'
new() { curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "$(j --arg d "smoke-friends-$(rid)" '{deviceToken:$d}')" | jq -r .sessionToken; }

A=$(new); B=$(new); C=$(new)
NA=$(g "$A" /v1/me | jq -r .name); NB=$(g "$B" /v1/me | jq -r .name); IC=$(g "$C" /v1/me | jq -r .accountId); NC=$(g "$C" /v1/me | jq -r .name)
IA=$(g "$A" /v1/me | jq -r .accountId); IB=$(g "$B" /v1/me | jq -r .accountId)
echo "A=$NA B=$NB C=$NC"
echo "A asks B by name: $(p "$A" /v1/friends/add "$(j --arg n "$NB" '{accountId:"00000000-0000-0000-0000-000000000000", name:$n}')" | jq -c "$F")"
echo "A asks B again: $(p "$A" /v1/friends/add "$(j --arg i "$IB" '{accountId:$i}')" | jq -c .code)"
echo "B's /me friendAsks: $(g "$B" /v1/me | jq -c .friendAsks)"
echo "B's list: $(g "$B" /v1/friends | jq -c "$F")"
echo "B takes it: $(p "$B" /v1/friends/answer "$(j --arg i "$IA" '{accountId:$i, accept:true}')" | jq -c "$F")"
echo "A's list: $(g "$A" /v1/friends | jq -c "$F")"
echo "A asks self: $(p "$A" /v1/friends/add "$(j --arg i "$IA" '{accountId:$i}')" | jq -c .code)"
echo "C asks A: $(p "$C" /v1/friends/add "$(j --arg i "$IA" '{accountId:$i}')" | jq -c .message)"
echo "A asks C back: $(p "$A" /v1/friends/add "$(j --arg i "$IC" '{accountId:$i}')" | jq -c "$F")"
echo "A takes C off: $(p "$A" /v1/friends/remove "$(j --arg i "$IC" '{accountId:$i}')" | jq -c "$F")"
echo "B blocks A: $(p "$B" /v1/chat/block "$(j --arg i "$IA" '{accountId:$i, block:true, channel:"world"}')" | jq -c .blocked)"
echo "A's list after the block: $(g "$A" /v1/friends | jq -c "$F")"
echo "A asks B again: $(p "$A" /v1/friends/add "$(j --arg i "$IB" '{accountId:$i}')" | jq -c .code)"

p "$A" /v1/dev/grant '{}' > /dev/null
TAG="F$(rid | tr -d 'a-f' | cut -c1-3)"
p "$A" /v1/guild/create "$(j --arg r "$(rid)" --arg t "$TAG" '{requestId:$r, name:("Smoke Friends " + $t), tag:$t, color:"#8E44AD"}')" > /dev/null
p "$A" /v1/guild/settings "$(j --arg r "$(rid)" '{requestId:$r, open:false, color:"#8E44AD"}')" > /dev/null
echo "A's guild is shut: $(g "$A" /v1/guild | jq -c '{tag: .mine.tag, open: .mine.open}')"
echo "A invites C by id: $(p "$A" /v1/guild/invite "$(j --arg r "$(rid)" --arg i "$IC" '{requestId:$r, accountId:$i}')" | jq -c '{invited: [.invited[].name], message}')"
echo "invite C again: $(p "$A" /v1/guild/invite "$(j --arg r "$(rid)" --arg i "$IC" '{requestId:$r, accountId:$i}')" | jq -c .code)"
echo "A friends view canInvite: $(g "$A" /v1/friends | jq -c .canInvite)"
echo "C's /me guildInvites: $(g "$C" /v1/me | jq -c .guildInvites)"
GID_=$(g "$C" /v1/guild | jq -r '.invites[0].id')
echo "C's guild screen: $(g "$C" /v1/guild | jq -c '{invites: [.invites[] | "\(.tag) by \(.invitedBy)"]}')"
echo "C takes it: $(p "$C" /v1/guild/invite/answer "$(j --arg r "$(rid)" --arg gid "$GID_" '{requestId:$r, guildId:$gid, accept:true}')" | jq -c '{mine: .mine.tag, members: .mine.members, message}')"
echo "B invited to a guild by a non-manager: $(p "$C" /v1/guild/invite "$(j --arg r "$(rid)" --arg i "$IB" '{requestId:$r, accountId:$i}')" | jq -c .code)"
echo "A asks C to trade by id: $(p "$A" /v1/trade/invite "$(j --arg r "$(rid)" --arg i "$IC" '{requestId:$r, name:"", accountId:$i}')" | jq -c '{state, other: .otherName, message}')"
for S in "$C" "$B" "$A"; do curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S" | jq -c .; done
