#!/usr/bin/env bash
# Sworn bonds smoke test (7 Oct 2026, Rules.Bonds). A, B and C are made friends; below level 15 an ask is refused; at level 20
# A asks a stranger (refused), then B; B sees the ask and accepts; A asking C is refused (bonded); the pair hunt in one party
# and the heartbeat counts their time; their BOND line in chat; A breaks it and must wait. Deletes the heroes.
# Needs curl, jq; a local Development server.
#   tools/smoke-bonds.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
g() { curl -s "$BASE$2" -H "X-Session: $1"; }
j() { jq -nc "$@"; }
new() { curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "$(j --arg d "smoke-bond-$(rid)" '{deviceToken:$d}')" | jq -r .sessionToken; }
id() { g "$1" /v1/me | jq -r .accountId; }
hb() { curl -s -X POST "$BASE/v1/heartbeat" -H 'Content-Type: application/json' -H "X-Session: $1" -d '{}'; }
B_='{partner: .partnerName, together, ring, ringName, bonus: .xpBonusBp, ask: .askName, wait: (.waitSeconds > 0), message, code}'
A=$(new); B=$(new); C=$(new); S=$(new)
IA=$(id "$A"); IB=$(id "$B"); IC=$(id "$C"); IS=$(id "$S")
for X in "$B" "$C"; do
  I=$(id "$X")
  p "$A" /v1/friends/add "$(j --arg i "$I" '{accountId:$i}')" > /dev/null
  p "$X" /v1/friends/answer "$(j --arg i "$IA" '{accountId:$i, accept:true}')" > /dev/null
done
echo "level 1 asks: $(p "$A" /v1/bond/ask "$(j --arg i "$IB" '{accountId:$i}')" | jq -c .code)"
for X in "$A" "$B" "$C" "$S"; do p "$X" "/v1/dev/level?level=20" '{}' > /dev/null; done
echo "A asks a stranger: $(p "$A" /v1/bond/ask "$(j --arg i "$IS" '{accountId:$i}')" | jq -c .code)"
echo "A asks B: $(p "$A" /v1/bond/ask "$(j --arg i "$IB" '{accountId:$i}')" | jq -c "$B_")"
echo "B's state: $(g "$B" /v1/me | jq -c .bond)"
echo "B accepts: $(p "$B" /v1/bond/answer '{"accept":true}' | jq -c "$B_")"
echo "A asks C: $(p "$A" /v1/bond/ask "$(j --arg i "$IC" '{accountId:$i}')" | jq -c .code)"
# One party, one map: the heartbeat counts their time together.
p "$A" /v1/party/invite "$(j --arg i "$IB" '{accountId:$i}')" > /dev/null
p "$B" /v1/party/answer '{"accept":true}' > /dev/null
hb "$A" > /dev/null; hb "$B" > /dev/null; sleep 3; hb "$A" > /dev/null
echo "A's bond: $(g "$A" /v1/bond | jq -c "$B_ + {seconds: .secondsTogether}")"
echo "B on the bond line: $(p "$B" /v1/chat '{"channel":"bond","text":"for the steppe","after":0}' | jq -c '[.lines[] | .text]')"
echo "C reads the bond line: $(g "$C" '/v1/chat?channel=bond&after=0' | jq -c .code)"
echo "A breaks it: $(p "$A" /v1/bond/break '{}' | jq -c "$B_")"
echo "B's bond after: $(g "$B" /v1/bond | jq -c '{partner: .partnerName, wait: (.waitSeconds > 0)}')"
echo "A asks C at once: $(p "$A" /v1/bond/ask "$(j --arg i "$IC" '{accountId:$i}')" | jq -c .code)"
for X in "$A" "$B" "$C" "$S"; do curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $X" > /dev/null; done
echo "deleted four"
