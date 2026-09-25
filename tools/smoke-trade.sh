#!/usr/bin/env bash
# Direct trade smoke test (Development server: the level 30 and 72 hour rules are lifted there). Two fresh heroes: A asks
# B by name, B accepts, both put a piece and sorn on the table; a change after locking drops both back and holds the
# buttons 5 seconds; both lock, both confirm; the pieces change hands and the sorn arrives less 2%. Deletes both. Needs
# curl, jq.
#   tools/smoke-trade.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
g() { curl -s "$BASE$2" -H "X-Session: $1"; }
j() { jq -nc "$@"; }
W='{state, incoming, other: .otherName, mine: [.myItems[].name], mySorn, myStep, theirs: [.theirItems[].name], theirSorn, theirStep, lock: .lockLeft, message}'
new() { curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "{\"deviceToken\":\"smoke-trade-$(rid)\"}" | jq -r .sessionToken; }
bagpiece() {
  local item; item=$(g "$1" /v1/me | jq -r '[.items[] | select(.equipped|not)][0].id')
  if [ "$item" = "null" ]; then
    for i in 1 2 3 4; do p "$1" /v1/push "$(j --arg r "$(rid)" '{requestId:$r}')" > /dev/null; done
    item=$(g "$1" /v1/me | jq -r '[.items[] | select(.equipped|not)][0].id')
  fi
  echo "$item"
}

A=$(new); B=$(new)
p "$A" /v1/dev/grant '{}' > /dev/null; p "$B" /v1/dev/grant '{}' > /dev/null
NA=$(g "$A" /v1/me | jq -r .name); NB=$(g "$B" /v1/me | jq -r .name)
IA=$(bagpiece "$A"); IB=$(bagpiece "$B")
SA=$(g "$A" /v1/me | jq .inventory.sorn); SB=$(g "$B" /v1/me | jq .inventory.sorn)
echo "A=$NA ($SA sorn, piece $IA)  B=$NB ($SB sorn, piece $IB)"

echo "ask nobody: $(p "$A" /v1/trade/invite "$(j --arg r "$(rid)" '{requestId:$r, name:"No Such Hero Here"}')" | jq -c .code)"
echo "ask yourself: $(p "$A" /v1/trade/invite "$(j --arg r "$(rid)" --arg n "$NA" '{requestId:$r, name:$n}')" | jq -c .code)"
T=$(p "$A" /v1/trade/invite "$(j --arg r "$(rid)" --arg n "$NB" '{requestId:$r, name:$n}')"); ID=$(echo "$T" | jq .id)
echo "A asks B: $(echo "$T" | jq -c "$W")"
echo "B's heartbeat brief: $(g "$B" /v1/me | jq -c .trade)"
echo "ask again while asked: $(p "$A" /v1/trade/invite "$(j --arg r "$(rid)" --arg n "$NB" '{requestId:$r, name:$n}')" | jq -c .code)"
echo "A cannot accept its own: $(p "$A" /v1/trade/accept "$(j --arg r "$(rid)" --argjson t $ID '{requestId:$r, tradeId:$t}')" | jq -c .code)"
echo "B accepts: $(p "$B" /v1/trade/accept "$(j --arg r "$(rid)" --argjson t $ID '{requestId:$r, tradeId:$t}')" | jq -c "$W")"
echo "A offers a worn piece: $(p "$A" /v1/trade/offer "$(j --arg r "$(rid)" --argjson t $ID --arg i "$(g "$A" /v1/me | jq -r '[.items[] | select(.equipped)][0].id')" '{requestId:$r, tradeId:$t, itemIds:[$i], sorn:0}')" | jq -c .code)"
echo "A offers too much sorn: $(p "$A" /v1/trade/offer "$(j --arg r "$(rid)" --argjson t $ID '{requestId:$r, tradeId:$t, itemIds:[], sorn:999999999999}')" | jq -c .code)"
echo "A offers its piece and 100,000: $(p "$A" /v1/trade/offer "$(j --arg r "$(rid)" --argjson t $ID --arg i "$IA" '{requestId:$r, tradeId:$t, itemIds:[$i], sorn:100000}')" | jq -c "$W")"
echo "A's piece is on the table, out of its bag: $(g "$A" /v1/me | jq --arg i "$IA" '[.items[] | select(.id==$i)] | length')"
echo "B offers its piece: $(p "$B" /v1/trade/offer "$(j --arg r "$(rid)" --argjson t $ID --arg i "$IB" '{requestId:$r, tradeId:$t, itemIds:[$i], sorn:0}')" | jq -c "$W")"
echo "A locks at once (held): $(p "$A" /v1/trade/press "$(j --arg r "$(rid)" --argjson t $ID '{requestId:$r, tradeId:$t}')" | jq -c .code)"
sleep 6
echo "A locks: $(p "$A" /v1/trade/press "$(j --arg r "$(rid)" --argjson t $ID '{requestId:$r, tradeId:$t}')" | jq -c "$W")"
echo "A confirms before B locks: $(p "$A" /v1/trade/press "$(j --arg r "$(rid)" --argjson t $ID '{requestId:$r, tradeId:$t}')" | jq -c .code)"
echo "B swaps its offer at the last second: $(p "$B" /v1/trade/offer "$(j --arg r "$(rid)" --argjson t $ID '{requestId:$r, tradeId:$t, itemIds:[], sorn:0}')" | jq -c "$W")"
echo "B puts the piece back: $(p "$B" /v1/trade/offer "$(j --arg r "$(rid)" --argjson t $ID --arg i "$IB" '{requestId:$r, tradeId:$t, itemIds:[$i], sorn:0}')" | jq -c "[.myStep, .theirStep, .lockLeft]")"
sleep 6
for who in A B; do S=$A; [ $who = B ] && S=$B; echo "$who locks: $(p "$S" /v1/trade/press "$(j --arg r "$(rid)" --argjson t $ID '{requestId:$r, tradeId:$t}')" | jq -c '[.myStep, .theirStep, .message]')"; done
echo "A confirms: $(p "$A" /v1/trade/press "$(j --arg r "$(rid)" --argjson t $ID '{requestId:$r, tradeId:$t}')" | jq -c '[.myStep, .theirStep, .message]')"
R=$(p "$B" /v1/trade/press "$(j --arg r "$(rid)" --argjson t $ID '{requestId:$r, tradeId:$t}')")
echo "B confirms: $(echo "$R" | jq -c '{state, message, heroSorn: .hero.inventory.sorn}')"
echo "A now holds B's piece: $(g "$A" /v1/me | jq --arg i "$IB" '[.items[] | select(.id==$i)] | length'), not its own: $(g "$A" /v1/me | jq --arg i "$IA" '[.items[] | select(.id==$i)] | length'); sorn $SA -> $(g "$A" /v1/me | jq .inventory.sorn)"
echo "B now holds A's piece: $(g "$B" /v1/me | jq --arg i "$IA" '[.items[] | select(.id==$i)] | length'); sorn $SB -> $(g "$B" /v1/me | jq .inventory.sorn) (100,000 less 2%)"
echo "no live trade left: $(g "$A" /v1/trade | jq -c '{state, id}') brief: $(g "$A" /v1/me | jq -c .trade)"
T=$(p "$B" /v1/trade/invite "$(j --arg r "$(rid)" --arg n "$NA" '{requestId:$r, name:$n}')"); ID=$(echo "$T" | jq .id)
p "$A" /v1/trade/accept "$(j --arg r "$(rid)" --argjson t $ID '{requestId:$r, tradeId:$t}')" > /dev/null
echo "A offers B's old piece back: $(p "$A" /v1/trade/offer "$(j --arg r "$(rid)" --argjson t $ID --arg i "$IB" '{requestId:$r, tradeId:$t, itemIds:[$i], sorn:0}')" | jq -c '[.myItems[].name]'), in A's bag: $(g "$A" /v1/me | jq --arg i "$IB" '[.items[] | select(.id==$i)] | length')"
echo "A cancels: $(p "$A" /v1/trade/cancel "$(j --arg r "$(rid)" --argjson t $ID '{requestId:$r, tradeId:$t}')" | jq -c '{state, message}'), the piece is back in A's bag: $(g "$A" /v1/me | jq --arg i "$IB" '[.items[] | select(.id==$i)] | length')"
curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $A" | jq -c .; curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $B" | jq -c .
