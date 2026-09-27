#!/usr/bin/env bash
# Guild raid smoke test (27 Sep 2026; a Development server: dev grant, level and stage). A leader founds a guild and a
# second hero joins; the raid shows its boss before the first fight, three fights a day each wear down the pool and a
# fourth is refused. With LOCAL_DB set (a psql connection string) the pool is dropped to 1 HP: the next fight fells the
# boss, both fighters are paid (Tallies at once, sorn by letter), world chat says so, and a fight after is refused.
# Deletes both heroes (the guild goes with the last).
# Needs curl, jq.
#   tools/smoke-raid.sh [http://localhost:5080]
#   LOCAL_DB=postgresql://orsuun:orsuun-dev@localhost/orsuun tools/smoke-raid.sh
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
g() { curl -s "$BASE$2" -H "X-Session: $1"; }
j() { jq -nc "$@"; }
new() { curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "$(j --arg d "smoke-raid-$(rid)" '{deviceToken:$d}')" | jq -r .sessionToken; }
A=$(new); B=$(new)
for S in "$A" "$B"; do
  p "$S" /v1/dev/grant '{}' > /dev/null
  p "$S" "/v1/dev/level?level=30" '{}' > /dev/null
  p "$S" "/v1/dev/stage?cleared=25" '{}' > /dev/null
done
TAG=$(rid | tr -dc 'A-Z0-9' | cut -c1-4); [ ${#TAG} -lt 2 ] && TAG=RD$(date +%S)
GUILD=$(p "$A" /v1/guild/create "$(j --arg r "$(rid)" --arg t "$TAG" '{requestId:$r, name:("Raid " + $t), tag:$t, color:"#2E9E5B"}')" | jq -r .mine.id)
p "$B" /v1/guild/join "$(j --arg r "$(rid)" --arg g "$GUILD" '{requestId:$r, guildId:$g}')" > /dev/null
VIEW='{boss, map: .mapName, mechanic, hpMax, hpLeft, fightsLeft, slain, days: (.secondsLeft / 86400 | floor)}'
echo "before the first fight: $(g "$A" /v1/guild/raid | jq -c "$VIEW")"
for S in "$A" "$B"; do
  for n in 1 2 3; do
    printf "fight: %s\n" "$(p "$S" /v1/guild/raid/fight "$(j --arg r "$(rid)" '{requestId:$r}')" | jq -c '{damage, killed, left: .raid.hpLeft, fightsLeft: .raid.fightsLeft}')"
  done
done
echo "a fourth fight today: $(p "$A" /v1/guild/raid/fight "$(j --arg r "$(rid)" '{requestId:$r}')" | jq -c .code)"
echo "the board: $(g "$B" /v1/guild/raid | jq -c '{hpLeft, top: [.top[] | {name, damage}], mine: .myDamage}')"

if [ -n "${LOCAL_DB:-}" ]; then
  psql "$LOCAL_DB" -qtAc "UPDATE \"GuildRaids\" SET \"HpLeft\" = 1 WHERE \"GuildId\" = '$GUILD'"
  psql "$LOCAL_DB" -qtAc "DELETE FROM \"GuildRaidHits\" WHERE \"AccountId\" IN (SELECT \"Id\" FROM \"Accounts\" WHERE \"GuildId\" = '$GUILD') AND \"Damage\" < 0"
  # B's fights for today are used: A is also out. Let a fight through by moving one of A's hits to yesterday.
  psql "$LOCAL_DB" -qtAc "UPDATE \"GuildRaidHits\" SET \"Day\" = 'old' WHERE \"Id\" = (SELECT MIN(h.\"Id\") FROM \"GuildRaidHits\" h JOIN \"GuildRaids\" r ON r.\"Id\" = h.\"RaidId\" WHERE r.\"GuildId\" = '$GUILD')"
  TA=$(g "$A" /v1/me | jq .inventory.tallies)
  echo "the last blow: $(p "$A" /v1/guild/raid/fight "$(j --arg r "$(rid)" '{requestId:$r}')" | jq -c '{damage, message: .raid.message, slain: .raid.slain, slainBy: .raid.slainBy}')"
  echo "A's tallies $TA -> $(g "$A" /v1/me | jq .inventory.tallies); letters A: $(g "$A" /v1/mail | jq -c '[.letters[] | select(.kind == "raid") | {title, sorn}]')"
  echo "letters B: $(g "$B" /v1/mail | jq -c '[.letters[] | select(.kind == "raid") | {title, sorn}]')"
  echo "world chat: $(g "$A" "/v1/chat?channel=world" | jq -r '[.lines[] | select(.system and (.text | contains("guild raid")))][-1].text')"
  echo "a fight after: $(p "$B" /v1/guild/raid/fight "$(j --arg r "$(rid)" '{requestId:$r}')" | jq -c .code)"
fi
for S in "$B" "$A"; do curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S" | jq -c .; done
