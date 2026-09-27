#!/usr/bin/env bash
# Weekend events smoke test (27 Sep 2026; a Development server: /v1/dev/event). A fresh hero sees the week's calendar on
# /me, then a lucky forge hour started now raises the Forge's chance by 5%, and within a world-clock tick (30 s) world
# chat announces it. With LOCAL_DB set (a psql connection string) the dev events are removed again. Deletes the hero.
# Needs curl, jq.
#   tools/smoke-events.sh [http://localhost:5080]
#   LOCAL_DB=postgresql://orsuun:orsuun-dev@localhost/orsuun tools/smoke-events.sh
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
g() { curl -s "$BASE$2" -H "X-Session: $1"; }
j() { jq -nc "$@"; }
A=$(curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "$(j --arg d "smoke-events-$(rid)" '{deviceToken:$d}')" | jq -r .sessionToken)

echo "the calendar on /me: $(g "$A" /v1/me | jq -c '[.events[] | {kind, running, startsInH: (.startsInSeconds / 3600 | floor), hours: ((.endsInSeconds - .startsInSeconds) / 3600 | floor)}]')"
BEFORE=$(g "$A" /v1/me | jq .forge.chanceAloneBp)
LUCKY=$(p "$A" /v1/dev/event '{"kind":"LuckyForge","minutes":2}')
echo "a lucky forge hour now: $(echo "$LUCKY" | jq -c '[.events[] | select(.running) | .name]')"
AFTER=$(echo "$LUCKY" | jq .forge.chanceAloneBp)
echo "Forge chance: $BEFORE -> $AFTER bp ($([ $((AFTER - BEFORE)) -eq 500 ] && echo '+5%, right' || echo 'WRONG'))"
echo "a bad kind: $(p "$A" /v1/dev/event '{"kind":"Nope","minutes":2}' | jq -c .code)"

printf "waiting for the world clock's announcement"
for _ in $(seq 1 14); do
  LINE=$(g "$A" "/v1/chat?channel=world" | jq -r '[.lines[] | select(.system and (.text | contains("Lucky Forge Hour has begun")))][-1].text // empty')
  [ -n "$LINE" ] && break
  printf "."; sleep 3
done
echo; echo "world chat: ${LINE:-NOT ANNOUNCED}"

if [ -n "${LOCAL_DB:-}" ]; then
  echo "dev events removed: $(psql "$LOCAL_DB" -qtAc "DELETE FROM \"WorldEvents\" WHERE \"By\" = 'dev' RETURNING \"Id\"" | wc -l | tr -d ' ')"
fi
curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $A" | jq -c .
