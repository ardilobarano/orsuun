#!/usr/bin/env bash
# Guild war and fortress keep smoke test: two guilds of three sign up, a dev war night starts now, members fight duels on
# the lanes, the war is settled; then both guilds bid on a keep, a dev keep siege opens, contenders storm it and it is
# settled. Uses the Development endpoints (/v1/dev/war-night, war-end, keep-siege, keep-end) and changes the shared
# world (ratings, keeps), so run it against a local server only. Deletes its accounts at the end. Needs curl, jq.
#   tools/smoke-war.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
guest() { curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "{\"deviceToken\":\"$1\"}"; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
g() { curl -s "$BASE$2" -H "X-Session: $1"; }
# Bodies are built with jq and kept in variables: macOS bash 3.2 brace-expands "{..,..}" inside "$(...)".
j() { jq -nc "$@"; }

S=()
for i in 1 2 3 4 5 6; do
  A=$(guest "smoke-war-$(rid)")
  S[$i]=$(echo "$A" | jq -r .sessionToken)
  p "${S[$i]}" /v1/dev/grant '{}' > /dev/null
done

# Two guilds: 1 leads 2 and 3, 4 leads 5 and 6. Each member puts 100,000 in the treasury.
make_guild() {
  local leader=$1 tag=$2 body id
  body=$(j --arg r "$(rid)" --arg t "$tag" '{requestId:$r, name:("War " + $t), tag:$t, color:"#C0392B"}')
  id=$(p "$leader" /v1/guild/create "$body" | jq -r .mine.id)
  echo "$id"
}
TA="W$(rid | cut -c1-3 | tr 'a-z' 'A-Z')"; TB="X$(rid | cut -c1-3 | tr 'a-z' 'A-Z')"
GA=$(make_guild "${S[1]}" "$TA"); GB=$(make_guild "${S[4]}" "$TB")
for i in 2 3; do B1=$(j --arg r "$(rid)" --arg g "$GA" '{requestId:$r, guildId:$g}'); p "${S[$i]}" /v1/guild/join "$B1" > /dev/null; done
for i in 5 6; do B1=$(j --arg r "$(rid)" --arg g "$GB" '{requestId:$r, guildId:$g}'); p "${S[$i]}" /v1/guild/join "$B1" > /dev/null; done
for i in 1 2 3 4 5 6; do B1=$(j --arg r "$(rid)" '{requestId:$r, sorn:100000}'); p "${S[$i]}" /v1/guild/donate "$B1" > /dev/null; done
echo "guilds: [$TA] $(g "${S[1]}" /v1/guild | jq -c '{members: .mine.members, treasury: .mine.treasury}')  [$TB] $(g "${S[4]}" /v1/guild | jq -c '{members: .mine.members, treasury: .mine.treasury}')"

# Sign-up: leaders only.
echo "member signs up: $(p "${S[2]}" /v1/guild/war/signup '{"join":true}' | jq -c .code)"
echo "leader A signs up: $(p "${S[1]}" /v1/guild/war/signup '{"join":true}' | jq -c '{signedUp, nextNight, signedGuilds, message}')"
echo "leader B signs up: $(p "${S[4]}" /v1/guild/war/signup '{"join":true}' | jq -c '{signedUp, signedGuilds}')"

# A war night now.
R=$(p "${S[1]}" "/v1/dev/war-night?minutes=15" '{}')
echo "war night: $(echo "$R" | jq -c '{atWar, foe: .foe.tag, lanes: [.lanes[] | .front], secondsLeft, fightsLeft}')"
echo "member plants the flag: $(p "${S[2]}" /v1/guild/war/flag '{"lane":1}' | jq -c .code)"
echo "leader plants the flag: $(p "${S[1]}" /v1/guild/war/flag '{"lane":1}' | jq -c '[.lanes[] | {name, myFlag}]')"
for i in 1 2 3 4 5 6; do
  lane=$(( i % 3 ))
  B1=$(j --arg r "$(rid)" --argjson l "$lane" '{requestId:$r, lane:$l}')
  R=$(p "${S[$i]}" /v1/guild/war/fight "$B1")
  echo "duel $i on lane $lane: $(echo "$R" | jq -c '{won: .duel.won, chance: .duel.winChancePercent, champion: .duel.champion, hp: .duel.championHp, text: .duel.text, score: [.war.myScore, .war.theirScore], fronts: [.war.lanes[] | .front]}')"
done
B1=$(j --arg r "$(rid)" '{requestId:$r, lane:0}')
echo "again at once: $(p "${S[1]}" /v1/guild/war/fight "$B1" | jq -c .code)"
R=$(p "${S[1]}" /v1/dev/war-end '{}')
echo "war over: $(echo "$R" | jq -c '{atWar, rating, wins, losses, draws, lastResult, ladder: [.ladder[] | select(.tag==("'"$TA"'","'"$TB"'")) | {tag, rating}]}')"
echo "guild log: $(g "${S[1]}" /v1/guild | jq -c '.log[0:3]')"

# Fortress keeps: both guilds bid on Saltgate (fortress 2); a guild bids on one keep a week.
B1=$(j '{requestId:"x", fortressId:2, amount:60000}')
echo "member bids: $(p "${S[2]}" /v1/keep/bid "$B1" | jq -c .code)"
echo "A bids: $(p "${S[1]}" /v1/keep/bid "$B1" | jq -c '{message, keep: (.keeps[] | select(.fortressId==2) | {state, bids: [.bids[] | {tag, amount}], myBid})}')"
B1=$(j '{requestId:"x", fortressId:2, amount:70000}')
echo "B bids: $(p "${S[4]}" /v1/keep/bid "$B1" | jq -c '(.keeps[] | select(.fortressId==2) | [.bids[] | {tag, amount}])')"
B1=$(j '{requestId:"x", fortressId:1, amount:60000}')
echo "A bids on a second keep: $(p "${S[1]}" /v1/keep/bid "$B1" | jq -c .code)"
echo "treasury A after bidding: $(g "${S[1]}" /v1/guild | jq -c .mine.treasury)"

R=$(p "${S[1]}" "/v1/dev/keep-siege?minutes=15" '{}')
echo "keep siege: $(echo "$R" | jq -c '(.keeps[] | select(.fortressId==2) | {state, canFight, secondsLeft, bids: [.bids[] | {tag, contender}]})')"
for i in 2 5 3; do
  B1=$(j --arg r "$(rid)" '{requestId:$r, fortressId:2}')
  echo "keep fight $i: $(p "${S[$i]}" /v1/keep/fight "$B1" | jq -c '.lastSiege | {defending, damage, text}')"
done
B1=$(j --arg r "$(rid)" '{requestId:$r, fortressId:1}')
echo "storm a keep not bid on: $(p "${S[6]}" /v1/keep/fight "$B1" | jq -c .code)"
R=$(p "${S[1]}" /v1/dev/keep-end '{}')
echo "keep settled: $(echo "$R" | jq -c '(.keeps[] | select(.fortressId==2) | {state, holder: .holderTag, lastEvent})')"
echo "guild A keeps: $(g "${S[1]}" /v1/guild | jq -c '{fortresses: .mine.fortresses, bonus: .mine.sornBonusPercent}')"

for i in 1 2 3 4 5 6; do curl -s -X DELETE "$BASE/v1/account" -H "X-Session: ${S[$i]}" > /dev/null; done
echo "accounts deleted"
