#!/usr/bin/env bash
# Hunting parties smoke test (29 Sep 2026, Rules.Parties). Five fresh heroes: A befriends B, C and D; E is a stranger. A asks
# B, B joins (A leads); a stranger is refused; C joins; D's invite is declined. The party shows on /me, /v1/party and first
# on /v1/field. Party chat: lines and system lines, read from joining. A party dungeon: opened, joined, its chest shared. A leaves: B, who joined first, leads. B sends C away and the party of one ends. Deletes all five.
# Needs curl, jq; a local Development server.
#   tools/smoke-party.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
g() { curl -s "$BASE$2" -H "X-Session: $1"; }
j() { jq -nc "$@"; }
P='{leader: .leaderId[0:8], members: [.members[] | "\(.name)\(if .leader then "*" else "" end)\(if .together then "+" else "" end)"], bonus: .bonusPercent, invite: .inviteName, message, code}'
new() { curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "$(j --arg d "smoke-party-$(rid)" '{deviceToken:$d}')" | jq -r .sessionToken; }
id() { g "$1" /v1/me | jq -r .accountId; }

A=$(new); B=$(new); C=$(new); D=$(new); E=$(new)
IA=$(id "$A"); IB=$(id "$B"); IC=$(id "$C"); ID_=$(id "$D"); IE=$(id "$E")
for S in "$B" "$C" "$D"; do
  I=$(id "$S")
  p "$A" /v1/friends/add "$(j --arg i "$I" '{accountId:$i}')" > /dev/null
  p "$S" /v1/friends/answer "$(j --arg i "$IA" '{accountId:$i, accept:true}')" > /dev/null
done
echo "A asks B: $(p "$A" /v1/party/invite "$(j --arg i "$IB" '{accountId:$i}')" | jq -c "$P")"
echo "B's /me partyInvite: $(g "$B" /v1/me | jq -c .partyInvite)"
echo "B joins: $(p "$B" /v1/party/answer '{"accept":true}' | jq -c "$P")"
echo "A asks stranger E: $(p "$A" /v1/party/invite "$(j --arg i "$IE" '{accountId:$i}')" | jq -c .code)"
echo "A asks B again: $(p "$A" /v1/party/invite "$(j --arg i "$IB" '{accountId:$i}')" | jq -c .code)"
p "$A" /v1/party/invite "$(j --arg i "$IC" '{accountId:$i}')" > /dev/null
echo "C joins: $(p "$C" /v1/party/answer '{"accept":true}' | jq -c "$P")"
p "$A" /v1/party/invite "$(j --arg i "$ID_" '{accountId:$i}')" > /dev/null
echo "B says in party chat: $(p "$B" /v1/chat '{"channel":"party","text":"hello party","after":0}' | jq -c '[.lines[] | .text]')"
echo "A reads party chat: $(g "$A" '/v1/chat?channel=party&after=0' | jq -c '{channel, lines: [.lines[] | "\(if .system then "*" else .name end): \(.text)"]}')"
echo "E (no party) reads party chat: $(g "$E" '/v1/chat?channel=party&after=0' | jq -c .code)"
echo "D declines: $(p "$D" /v1/party/answer '{"accept":false}' | jq -c "$P")"
echo "D joins a lapsed invite: $(p "$D" /v1/party/answer '{"accept":true}' | jq -c .code)"
echo "A's /me partyLeader is A: $(g "$A" /v1/me | jq -r --arg a "$IA" '.partyLeader == $a')"
echo "A's field (partymates first): $(g "$A" /v1/field | jq -c '[.heroes[] | "\(.name)\(if .party then "(party)" else "" end)"]')"
echo "C (joined later) reads party chat: $(g "$C" '/v1/chat?channel=party&after=0' | jq -c '[.lines[] | .text]')"
echo "A's party: $(g "$A" /v1/party | jq -c "$P")"
echo "C kicks B (not leader): $(p "$C" /v1/party/kick "$(j --arg i "$IB" '{accountId:$i}')" | jq -c .code)"
echo "A leaves: $(p "$A" /v1/party/leave '{}' | jq -c "$P")"
echo "B reads the new leader's channel: $(g "$B" '/v1/chat?channel=party&after=0' | jq -c '[.lines[] | .text]')"
echo "B's party (B leads): $(g "$B" /v1/party | jq -c "$P")  B is $(echo "$IB" | cut -c1-8)"
echo "B sends C away: $(p "$B" /v1/party/kick "$(j --arg i "$IC" '{accountId:$i}')" | jq -c "$P")"
echo "C's party: $(g "$C" /v1/party | jq -c "$P")"
# The party board (Party finder): strangers on the same map. G lists itself; F sees G and asks it; H, not listed, cannot be asked.
F=$(new); G=$(new); H=$(new)
IG=$(id "$G"); IH=$(id "$H")
for S in "$F" "$G" "$H"; do curl -s -X POST "$BASE/v1/heartbeat" -H 'Content-Type: application/json' -H "X-Session: $S" -d '{}' > /dev/null; done
echo "G lists itself: $(p "$G" /v1/party/look '{"look":true}' | jq -c '{place, lookingLeft, message}')"
echo "F's board: $(g "$F" /v1/party/board | jq -c --arg g "$IG" '{place, g: ([.heroes[] | select(.id == $g)] | length)}')"
echo "F asks H (not listed): $(p "$F" /v1/party/invite "$(j --arg i "$IH" '{accountId:$i}')" | jq -c .code)"
echo "F asks G from the board: $(p "$F" /v1/party/invite "$(j --arg i "$IG" '{accountId:$i}')" | jq -c .message)"
echo "G joins: $(p "$G" /v1/party/answer '{"accept":true}' | jq -c "$P")"
echo "G off the board: $(g "$F" /v1/party/board | jq -c --arg g "$IG" '[.heroes[] | select(.id == $g)] | length')  G lists again: $(p "$G" /v1/party/look '{"look":true}' | jq -c .code)"
# Party dungeons (30 Sep 2026): F (leading F and G) opens the Hollow Spire for the party; G joins; both walk past the smith;
# the run is closed now (dev) and the party's chest is shared by letter among those who cleared.
for S in "$F" "$G"; do p "$S" "/v1/dev/stage?cleared=40" '{}' > /dev/null; p "$S" "/v1/dev/gear?level=40&upgrade=9" '{}' > /dev/null; done
NOID=00000000-0000-0000-0000-000000000000
R=$(p "$F" /v1/party/dungeon "$(j --arg r "$(rid)" --arg n "$NOID" '{requestId:$r, dungeonId:1, partyDungeonId:$n}')")
echo "F opens the Spire for the party: $(echo "$R" | jq -c '{atSmith, fellOn, floors: (.floors | length), mates: [(.mates // [])[] | .name], code}')"
RUN=$(echo "$R" | jq -r .runId)
[ "$(echo "$R" | jq -r .atSmith)" = "true" ] && echo "F past the smith: $(p "$F" /v1/dungeon/smith "$(j --arg r "$(rid)" --argjson run "$RUN" '{requestId:$r, runId:$run, itemId:""}')" | jq -c '{cleared, fellOn}')"
PD=$(g "$G" /v1/party | jq -r .openDungeonId)
echo "G's party card: $(g "$G" /v1/party | jq -c '{openDungeon, openedBy, left: (.openDungeonLeft > 0), openJoined, joined: .openJoinedNames}')"
R2=$(p "$G" /v1/dungeon/enter "$(j --arg r "$(rid)" --arg pd "$PD" '{requestId:$r, dungeonId:1, partyDungeonId:$pd}')")
echo "G joins: $(echo "$R2" | jq -c '{atSmith, fellOn, mates: [(.mates // [])[] | .name], code}')"
RUN2=$(echo "$R2" | jq -r .runId)
[ "$(echo "$R2" | jq -r .atSmith)" = "true" ] && echo "G past the smith: $(p "$G" /v1/dungeon/smith "$(j --arg r "$(rid)" --argjson run "$RUN2" '{requestId:$r, runId:$run, itemId:""}')" | jq -c '{cleared, fellOn}')"
echo "G joins again: $(p "$G" /v1/dungeon/enter "$(j --arg r "$(rid)" --arg pd "$PD" '{requestId:$r, dungeonId:1, partyDungeonId:$pd}')" | jq -c .code)"
echo "the run closes: $(p "$F" /v1/dev/party-dungeon-close '{}' | jq -c '{openDungeon}')"
for S in "$F" "$G"; do echo "party letters: $(g "$S" /v1/mail | jq -c '[.letters[] | select(.kind == "party") | .title] | length')"; done
echo "party chat: $(g "$F" '/v1/chat?channel=party&after=0' | jq -c '[.lines[] | select(.system) | .text] | .[-3:]')"
for S in "$H" "$G" "$F" "$E" "$D" "$C" "$B" "$A"; do curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S" > /dev/null; done
echo "deleted eight"
