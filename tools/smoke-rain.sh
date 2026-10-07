#!/usr/bin/env bash
# Korstone Rain smoke test (7 Oct 2026, Rules.KorstoneRain). Three fresh heroes hunt the Oathfields; a Giant Korstone is
# dropped there (dev); a hero elsewhere is refused; A and B strike in turn until it breaks (then a strike is refused);
# the strikers and the bystander C are showered by letter. Deletes the heroes.
# Needs curl, jq; a local Development server.
#   tools/smoke-rain.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
g() { curl -s "$BASE$2" -H "X-Session: $1"; }
new() { curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "$(jq -nc --arg d "smoke-rain-$(rid)" '{deviceToken:$d}')" | jq -r .sessionToken; }
strike() { p "$1" /v1/rain/strike "$(jq -nc --arg r "$(rid)" '{requestId:$r}')"; }
A=$(new); B=$(new); C=$(new); D=$(new)
for S in "$A" "$B" "$C"; do curl -s -X POST "$BASE/v1/heartbeat" -H 'Content-Type: application/json' -H "X-Session: $S" -d '{}' > /dev/null; done
# A and B in Epic +7 gear of level 30 (the Oathfields' stone needs them); C only hunts there.
for S in "$A" "$B"; do p "$S" "/v1/dev/gear?level=30&upgrade=7" '{}' > /dev/null; done
p "$D" "/v1/dev/stage?cleared=20" '{}' > /dev/null
p "$D" /v1/park '{"stage":15}' > /dev/null
echo "the stone falls: $(p "$A" "/v1/dev/rain?map=1" '{}' | jq -c '.rain | {map, mapName, camp, hpMax, strikesLeft, broken}')"
echo "D on Gorak Pass strikes: $(strike "$D" | jq -c .code)"
BROKE=false
for n in 1 2 3; do
  for W in A B; do
    [ "$BROKE" = "true" ] && break
    S=$([ "$W" = A ] && echo "$A" || echo "$B")
    R=$(strike "$S"); echo "$W strike $n: $(echo "$R" | jq -c '{damage, hpLeft, broke, left: .state.rain.strikesLeft, code}')"
    BROKE=$(echo "$R" | jq -r '.broke // false')
  done
done
echo "a strike after it broke: $(strike "$A" | jq -c .code)"
for S in "$A" "$B" "$C"; do echo "letters: $(g "$S" /v1/mail | jq -c '[.letters[] | select(.kind == "rain") | {title, sorn, goodId, goodCount}]')"; done
echo "the stone in the state: $(g "$A" /v1/me | jq -c '.rain | {broken, brokenBy, myDamage}')"
for S in "$A" "$B" "$C" "$D"; do curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S" > /dev/null; done
echo "deleted four"
