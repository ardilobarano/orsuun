#!/usr/bin/env bash
# HTTP smoke test against a running dev server (bash port of smoke.ps1). Needs curl and jq.
#   tools/smoke.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
post() { curl -s -X POST "$BASE$1" -H 'Content-Type: application/json' -H "X-Session: ${SESSION:-}" -d "$2"; }

login=$(curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "{\"deviceToken\":\"smoke-$(rid)\"}")
SESSION=$(echo "$login" | jq -r .sessionToken)
echo "login: created=$(echo "$login" | jq .created)"

me=$(curl -s "$BASE/v1/me" -H "X-Session: $SESSION")
echo "me: atk=$(echo "$me" | jq .hero.attack) items=$(echo "$me" | jq '.items|length') parked=$(echo "$me" | jq .parkedStage)"
echo "park 2 while locked: $(post /v1/park '{"stage":2}' | jq -c .)"

for i in 1 2 3 4 5; do
  pu=$(post /v1/push "{\"requestId\":\"$(rid)\"}")
  echo "push: stage=$(echo "$pu" | jq .lastPush.stage) cleared=$(echo "$pu" | jq .lastPush.cleared) highest=$(echo "$pu" | jq .highestStageCleared)"
done

r=$(rid)
f=$(post /v1/forge "{\"requestId\":\"$r\",\"method\":\"ScrollOfMercy\"}")
echo "forge: $(echo "$f" | jq -r '.lastForge | "\(.outcome) +\(.levelBefore)->+\(.levelAfter)"')"
fr=$(post /v1/forge "{\"requestId\":\"$r\",\"method\":\"ScrollOfMercy\"}")
echo "forge replay: $(echo "$fr" | jq -c .)"
echo "turn: turnstones=$(post /v1/turn "{\"requestId\":\"$(rid)\"}" | jq .inventory.turnstones)"
goal='{"requestId":"'$(rid)'","count":20,"targets":[{"entryId":1,"minTier":1}]}' 
echo "turn toward a goal: $(post /v1/turn "$goal" | jq -c .lastTurn)"
bad='{"requestId":"'$(rid)'","count":5,"targets":[{"entryId":1,"minTier":1},{"entryId":1,"minTier":2}]}' 
echo "goal out of reach: $(post /v1/turn "$bad" | jq -c .)"
echo "park hunting ground: parked=$(post /v1/park '{"stage":101}' | jq .parkedStage)"
echo "park field I: parked=$(post /v1/park '{"stage":111}' | jq .parkedStage)"
post /v1/dev/bosses-up '{}' > /dev/null
for id in 1 2 3; do
  bf=$(post /v1/boss/fight "{\"requestId\":\"$(rid)\",\"bossId\":$id}")
  echo "fight $id: $(echo "$bf" | jq -r '.lastBossFight | "damage=\(.damage) killed=\(.killed) rank=\(.rank) pool=\(.poolLeft) slew=\(.slew)\n  \(.chest)"')"
done
fa=$(post /v1/boss/fight "{\"requestId\":\"$(rid)\",\"bossId\":1}")
echo "fight again: $(echo "$fa" | jq -c .)"
echo "war before oath: $(curl -s "$BASE/v1/war" -H "X-Session: $SESSION" | jq -c '{season, standings: [.standings[] | {banner, points}], forts: [.fortresses[] | {name, holder, phase, wall}]}')"
early='{"requestId":"'$(rid)'","fortressId":1}' 
echo "siege before oath: $(post /v1/siege "$early" | jq -c .code)"
echo "oath: $(post /v1/banner '{"banner":"Gold"}' | jq -c '{banner, name}')"
echo "oath again: $(post /v1/banner '{"banner":"Sky"}' | jq -c .code)"
post /v1/dev/grant '{}' > /dev/null
echo "bounties: $(curl -s "$BASE/v1/me" -H "X-Session: $SESSION" | jq -c '[.bounties.items[] | "\(.title) \(.count)/\(.target)"]')"
claim='{"requestId":"'$(rid)'","bountyId":2}' 
echo "claim unfinished: $(post /v1/bounty/claim "$claim" | jq -c .code)"
buy='{"requestId":"'$(rid)'","shopItemId":1,"count":2}'
echo "buy 2 needles: $(post /v1/shop/buy "$buy" | jq -c '{marks: .inventory.huntMarks, needles: .inventory.etchingNeedles}')"
wid=$(curl -s "$BASE/v1/me" -H "X-Session: $SESSION" | jq -r '[.items[] | select(.equipped and .slot=="Weapon")][0].id')
etch='{"requestId":"'$(rid)'","itemId":"'$wid'"}'
echo "etch a full weapon: $(post /v1/etch "$etch" | jq -c .code)"
pin='{"requestId":"'$(rid)'","itemId":"'$wid'","index":1}'
echo "pin: $(post /v1/pin "$pin" | jq -c '{wax: .inventory.pinningWax, locked: ([.items[] | select(.id=="'$wid'")][0].lockedEtchingIndex)}')"
siege='{"requestId":"'$(rid)'","fortressId":1}'
echo "siege Stagfort: $(post /v1/siege "$siege" | jq -c '.lastSiege | {defending, damage, phase, wallLeft, phaseBroken, text}')"
siege2='{"requestId":"'$(rid)'","fortressId":2}'
echo "siege again at once: $(post /v1/siege "$siege2" | jq -c .code)"
echo "war after: $(curl -s "$BASE/v1/war" -H "X-Session: $SESSION" | jq -c '{standings: [.standings[] | {banner, points}], bonus: .mySornBonusPercent, cooldown: .siegeCooldownSeconds}')"
# Guilds: create (leader), a second account joins, donate, raise Plunder, shop, ranks, kick and rejoin, then the
# leader's deletion hands the guild on and the last member's deletion disbands it.
TAG=$(rid | cut -c1-4 | tr 'a-z' 'A-Z')
bad='{"requestId":"'$(rid)'","name":"x","tag":"'$TAG'","color":"#2E9E5B"}'
echo "guild bad name: $(post /v1/guild/create "$bad" | jq -c .code)"
gc='{"requestId":"'$(rid)'","name":"Smoke '$TAG'","tag":"'$TAG'","color":"#2E9E5B"}'
g=$(post /v1/guild/create "$gc")
echo "guild create: $(echo "$g" | jq -c '{mine: .mine | {name, tag, level, members}, sorn: .state.inventory.sorn, brief: .state.guild, message}')"
GID=$(echo "$g" | jq -r .mine.id)
echo "guild create again: $(post /v1/guild/create "$gc" | jq -c .code)"
don='{"requestId":"'$(rid)'","sorn":200000}'
echo "donate 200k: $(post /v1/guild/donate "$don" | jq -c '{treasury: .mine.treasury, xp: .mine.xp, level: .mine.level, tallies: .state.inventory.tallies, today: .donatedToday, message}')"
don2='{"requestId":"'$(rid)'","sorn":1000}'
echo "donate over the cap: $(post /v1/guild/donate "$don2" | jq -c .code)"
sk='{"requestId":"'$(rid)'","skill":"Plunder"}'
echo "raise Plunder: $(post /v1/guild/skill "$sk" | jq -c '{plunder: .mine.plunder, treasury: .mine.treasury, bonus: .mine.sornBonusPercent, message}')"
gs='{"requestId":"'$(rid)'","itemId":1}'
echo "guild shop Anvil Ward: $(post /v1/guild/shop "$gs" | jq -c '{tallies: .state.inventory.tallies, wards: .state.inventory.anvilWards}')"
login2=$(curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "{\"deviceToken\":\"smoke-$(rid)\"}")
S2=$(echo "$login2" | jq -r .sessionToken)
post2() { curl -s -X POST "$BASE$1" -H 'Content-Type: application/json' -H "X-Session: $S2" -d "$2"; }
echo "browse as a stranger: $(curl -s "$BASE/v1/guild?q=$TAG" -H "X-Session: $S2" | jq -c '[.browse[] | {tag, members, open}]')"
join='{"requestId":"'$(rid)'","guildId":"'$GID'"}'
echo "join: $(post2 /v1/guild/join "$join" | jq -c '{members: .mine.members, brief: .state.guild}')"
A2=$(echo "$login2" | jq -r .accountId)
rk='{"requestId":"'$(rid)'","accountId":"'$A2'","rank":"Officer"}'
echo "promote: $(post /v1/guild/rank "$rk" | jq -c '[.members[] | {rank, me}]')"
kk='{"requestId":"'$(rid)'","accountId":"'$A2'"}'
echo "kick: $(post /v1/guild/kick "$kk" | jq -c '{members: .mine.members, message}')"
shut='{"requestId":"'$(rid)'","open":false,"color":"#2E9E5B"}'
echo "close the gates: $(post /v1/guild/settings "$shut" | jq -c '{open: .mine.open}')"
join2='{"requestId":"'$(rid)'","guildId":"'$GID'"}'
echo "join a closed guild: $(post2 /v1/guild/join "$join2" | jq -c .code)"
open='{"requestId":"'$(rid)'","open":true,"color":"#2E9E5B"}'
post /v1/guild/settings "$open" > /dev/null
join3='{"requestId":"'$(rid)'","guildId":"'$GID'"}'
echo "rejoin: $(post2 /v1/guild/join "$join3" | jq -c '{members: .mine.members}')"
echo "client log: $(post /v1/client-log '{"platform":"Smoke","version":"0","message":"smoke test report","stack":"at Smoke()"}' | jq -c .)"
echo "delete account: $(curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $SESSION" | jq -c .)"
echo "me after delete: $(curl -s -o /dev/null -w '%{http_code}' "$BASE/v1/me" -H "X-Session: $SESSION")"
echo "guild after the leader left: $(curl -s "$BASE/v1/guild" -H "X-Session: $S2" | jq -c '{members: .mine.members, ranks: [.members[] | .rank], last: .mine.lastEvent}')"
curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S2" > /dev/null
login3=$(curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "{\"deviceToken\":\"smoke-$(rid)\"}")
S3=$(echo "$login3" | jq -r .sessionToken)
echo "guild after the last member left: $(curl -s "$BASE/v1/guild?q=$TAG" -H "X-Session: $S3" | jq -c '[.browse[] | .tag]')"
curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S3" > /dev/null
