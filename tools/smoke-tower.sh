#!/usr/bin/env bash
# The Endless Tower smoke test (7 Oct 2026, Rules.Tower). A fresh hero below the level is refused; at level 30 in +7 gear
# it climbs three times (floors to replay, the floor reached, the chests), a fourth climb is refused; the ladder shows it;
# the week is settled (dev) and its title comes by letter. Deletes the hero.
# Needs curl, jq; a local Development server.
#   tools/smoke-tower.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
g() { curl -s "$BASE$2" -H "X-Session: $1"; }
climb() { p "$1" /v1/tower/climb "$(jq -nc --arg r "$(rid)" '{requestId:$r}')"; }
S=$(curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "$(jq -nc --arg d "smoke-tower-$(rid)" '{deviceToken:$d}')" | jq -r .sessionToken)
echo "level 1 climbs: $(climb "$S" | jq -c '{code}')"
p "$S" "/v1/dev/level?level=30" '{}' > /dev/null
p "$S" "/v1/dev/gear?level=30&upgrade=7" '{}' > /dev/null
echo "the tower: $(g "$S" /v1/tower | jq -c '{best, climbsLeft, nextChest, nextChestHolds, rank}')"
for n in 1 2 3; do
  echo "climb $n: $(climb "$S" | jq -c '{reached, fellOn, newBest, floors: [.floors[].floor], chests: (.chests | length > 0), best: .tower.best, rank: .tower.rank, left: .tower.climbsLeft}')"
done
echo "climb 4: $(climb "$S" | jq -c .code)"
echo "the ladder: $(g "$S" /v1/tower | jq -c '{ladder: [.ladder[] | {rank, best}] | .[0:3], rank, bestEver}')"
echo "week settled: $(p "$S" /v1/dev/tower-week-end '{}' | jq -c '{title, message}')"
echo "letter: $(g "$S" /v1/mail | jq -c '[.letters[] | select(.kind == "tower") | {title, sorn}]')"
echo "achievement Tower Climber: $(g "$S" /v1/achievements | jq -c '[.list[] | select(.id == 93) | {progress, target}]')"
curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S" > /dev/null
echo "deleted"
