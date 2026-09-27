#!/usr/bin/env bash
# Name reports smoke test (27 Sep 2026) against a server started with Admin__Emails=$MOD_EMAIL (see smoke-admin.sh):
# a player reports another hero's name and guild (a second report of the same name counts once, one's own name is
# refused); the moderation page lists both; a moderator renames the hero (it gets a letter) and keeps the guild's name,
# and both leave the queue. Deletes every account (the guild goes with its last member).
#   Admin__Emails=mod-smoke@example.com ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Orsuun.Server
#   tools/smoke-names.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
MOD_EMAIL="${MOD_EMAIL:-mod-smoke@example.com}"
MOD_PASSWORD="moderator-$(uuidgen | cut -c1-8)"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
j() { jq -nc "$@"; }
guest() { curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "$(j --arg d "smoke-$(rid)" '{deviceToken:$d}')"; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
a() { curl -s -X "$1" "$BASE/admin/api$2" -H 'Content-Type: application/json' -H "X-Admin: $ADMIN" ${3:+-d "$3"}; }
SM=$(guest | jq -r .sessionToken)
p "$SM" /v1/auth/register "$(j --arg e "$MOD_EMAIL" --arg p "$MOD_PASSWORD" '{email:$e, password:$p}')" > /dev/null
ADMIN=$(curl -s -X POST "$BASE/admin/api/login" -H 'Content-Type: application/json' -d "$(j --arg e "$MOD_EMAIL" --arg p "$MOD_PASSWORD" '{email:$e, password:$p}')" | jq -r .token)
O=$(guest); SO=$(echo "$O" | jq -r .sessionToken); IDO=$(echo "$O" | jq -r .accountId)
SR=$(guest | jq -r .sessionToken)
p "$SO" /v1/dev/grant '{}' > /dev/null
TAG=N$(rid | tr -dc 'A-Z0-9' | cut -c1-2); [ ${#TAG} -lt 3 ] && TAG=NM$(date +%S | cut -c2)
p "$SO" /v1/guild/create "$(j --arg r "$(rid)" --arg t "$TAG" '{requestId:$r, name:("Rude Guild " + $t), tag:$t, color:"#2E9E5B"}')" > /dev/null
echo "report name:   $(p "$SR" /v1/report-name "$(j --arg a "$IDO" '{kind:"hero", accountId:$a}')" | jq -c .message)"
echo "again:         $(p "$SR" /v1/report-name "$(j --arg a "$IDO" '{kind:"hero", accountId:$a}')" | jq -c .message)"
echo "report guild:  $(p "$SR" /v1/report-name "$(j --arg a "$IDO" '{kind:"guild", accountId:$a}')" | jq -c .message)"
echo "own name:      $(p "$SO" /v1/report-name "$(j --arg a "$IDO" '{kind:"hero", accountId:$a}')" | jq -c .code)"
echo "queue:         $(a GET /names | jq -c --arg a "$IDO" '[.[] | select(.targetId == $a or (.kind == "guild" and (.name | startswith("Rude Guild")))) | {kind, name, tag, reports}]')"
NEW="Renamed$(rid | tr -dc '0-9' | cut -c1-5)"
echo "rename hero:   $(a POST "/players/$IDO/rename" "$(j --arg n "$NEW" '{name:$n}')" | jq -c .)"
echo "the letter:    $(curl -s "$BASE/v1/mail" -H "X-Session: $SO" | jq -c '[.letters[] | select(.kind == "system") | {from, title}]')"
echo "hero's name:   $(curl -s "$BASE/v1/me" -H "X-Session: $SO" | jq -c .name)"
GID=$(a GET /names | jq -r '[.[] | select(.kind == "guild" and (.name | startswith("Rude Guild")))][0].targetId')
echo "keep guild:    $(a POST /names/keep "$(j --arg g "$GID" '{kind:"guild", targetId:$g}')" | jq -c .)"
echo "queue after:   $(a GET /names | jq -c --arg a "$IDO" --arg g "$GID" '[.[] | select(.targetId == $a or .targetId == $g)] | length')"
for T in "$SO" "$SR" "$SM"; do curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $T" | jq -c .; done
