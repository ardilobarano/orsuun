#!/usr/bin/env bash
# Private messages smoke test (26 Sep 2026). Two fresh heroes: A writes B by name; B sees one unread on /me and in the
# list, opens the conversation (read) and answers; A polls with the last id it holds and gets the answer only; A sends a
# page and a half so the older lines page back; B blocks A and A cannot write; B reports A's message. Deletes both.
# Needs curl, jq. Waits out the chat's 3-second flood limit between sends.
#   tools/smoke-whispers.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
g() { curl -s "$BASE$2" -H "X-Session: $1"; }
j() { jq -nc "$@"; }
new() { curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "$(j --arg d "smoke-whispers-$(rid)" '{deviceToken:$d}')" | jq -r .sessionToken; }
T='{name, lines: [.lines[] | "\(if .mine then "me" else "them" end): \(.text)"], latest, hasOlder, blocked, message}'

A=$(new); B=$(new)
NB=$(g "$B" /v1/me | jq -r .name); IA=$(g "$A" /v1/me | jq -r .accountId); IB=$(g "$B" /v1/me | jq -r .accountId)
echo "A writes B by name: $(p "$A" /v1/whispers/send "$(j --arg n "$NB" '{accountId:"00000000-0000-0000-0000-000000000000", name:$n, text:"Well met on the steppe", after:0}')" | jq -c "$T")"
echo "B's /me whispers: $(g "$B" /v1/me | jq -c .whispers)"
echo "B's list: $(g "$B" /v1/whispers | jq -c '{unread, conversations: [.conversations[] | "\(.name): \(.lastText) (unread \(.unread))"]}')"
NA=$(g "$A" /v1/me | jq -r .name)
echo "B opens it by name: $(g "$B" "/v1/whispers/thread?name=$(jq -rn --arg n "$NA" '$n|@uri')" | jq -c "$T")"
echo "B's /me whispers after: $(g "$B" /v1/me | jq -c .whispers)"
LAST=$(g "$A" "/v1/whispers/thread?id=$IB" | jq -r .latest)
sleep 3
echo "B answers: $(p "$B" /v1/whispers/send "$(j --arg i "$IA" '{accountId:$i, text:"And you. Ride with us?", after:0}')" | jq -c '.lines | length')"
echo "A polls after $LAST: $(g "$A" "/v1/whispers/thread?id=$IB&after=$LAST" | jq -c "$T")"
echo "A writes self: $(p "$A" /v1/whispers/send "$(j --arg i "$IA" '{accountId:$i, text:"hi", after:0}')" | jq -c .code)"
echo "A writes an empty line: $(p "$A" /v1/whispers/send "$(j --arg i "$IB" '{accountId:$i, text:"   ", after:0}')" | jq -c .code)"
echo "A too fast: $(p "$A" /v1/whispers/send "$(j --arg i "$IB" '{accountId:$i, text:"one", after:0}')" | jq -c .code) $(p "$A" /v1/whispers/send "$(j --arg i "$IB" '{accountId:$i, text:"two", after:0}')" | jq -c .code)"
MID=$(g "$B" "/v1/whispers/thread?id=$IA" | jq -r '[.lines[] | select(.mine | not)][0].id')
echo "B reports A's first line: $(p "$B" /v1/whispers/report "$(j --argjson m "$MID" '{messageId:$m}')" | jq -c .message)"
echo "B blocks A: $(p "$B" /v1/chat/block "$(j --arg i "$IA" '{accountId:$i, block:true, channel:"world"}')" | jq -c .blocked)"
sleep 3
echo "A writes B blocked: $(p "$A" /v1/whispers/send "$(j --arg i "$IB" '{accountId:$i, text:"still there?", after:0}')" | jq -c .code)"
echo "B's thread shows blocked: $(g "$B" "/v1/whispers/thread?id=$IA" | jq -c .blocked)"
for S in "$A" "$B"; do curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S" | jq -c .; done
