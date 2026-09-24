#!/usr/bin/env bash
# Moderation smoke test against a server started with Admin__Emails=$MOD_EMAIL (default mod-smoke@example.com):
#   Admin__Emails=mod-smoke@example.com ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Orsuun.Server
#   tools/smoke-admin.sh [http://localhost:5080]
# Makes a moderator, an offender and a reporter, walks reports, hide, mute, ban, guild rename and disband, then deletes
# all three accounts. Bodies are built with jq (macOS bash 3.2 brace-expands "{..,..}" inside "$(...)").
set -u
BASE="${1:-http://localhost:5080}"
MOD_EMAIL="${MOD_EMAIL:-mod-smoke@example.com}"
MOD_PASSWORD="moderator-$(uuidgen | cut -c1-8)"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
j() { jq -nc "$@"; }
guest() { curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "$(j --arg d "$1" '{deviceToken:$d}')"; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
a() { curl -s -X "$1" "$BASE/admin/api$2" -H 'Content-Type: application/json' -H "X-Admin: $ADMIN" ${3:+-d "$3"}; }

echo "page: $(curl -s -o /dev/null -w '%{http_code} %{content_type}' "$BASE/admin") csp=$(curl -s -D - -o /dev/null "$BASE/admin" | grep -ci content-security-policy)"
echo "script: $(curl -s -o /dev/null -w '%{http_code}' "$BASE/admin/admin.js")"
echo "api without a session: $(curl -s -o /dev/null -w '%{http_code}' "$BASE/admin/api/overview")"

DM="smoke-$(rid)"; DO="smoke-$(rid)"; DR="smoke-$(rid)"
SM=$(guest "$DM" | jq -r .sessionToken)
R=$(p "$SM" /v1/auth/register "$(j --arg e "$MOD_EMAIL" --arg p "$MOD_PASSWORD" '{email:$e, password:$p}')")
echo "moderator account: $(echo "$R" | jq -r '.email // .code')"
O=$(guest "$DO"); SO=$(echo "$O" | jq -r .sessionToken); IDO=$(echo "$O" | jq -r .accountId)
SR=$(guest "$DR" | jq -r .sessionToken)
p "$SO" /v1/dev/grant '{}' > /dev/null

echo "wrong admin password: $(curl -s -X POST "$BASE/admin/api/login" -H 'Content-Type: application/json' -d "$(j --arg e "$MOD_EMAIL" '{email:$e, password:"wrong-password"}')" | jq -c .code)"
echo "a player who is not a moderator: $(curl -s -X POST "$BASE/admin/api/login" -H 'Content-Type: application/json' -d "$(j '{email:"nobody@example.com", password:"whatever-123"}')" | jq -c .code)"
ADMIN=$(curl -s -X POST "$BASE/admin/api/login" -H 'Content-Type: application/json' -d "$(j --arg e "$MOD_EMAIL" --arg p "$MOD_PASSWORD" '{email:$e, password:$p}')" | jq -r .token)
echo "admin session: $( [ ${#ADMIN} -gt 20 ] && echo yes || echo no)"

LINE=$(p "$SO" /v1/chat "$(j '{channel:"world", text:"you are all terrible, quit the game"}')" | jq -r '.lines[-1].id')
p "$SR" /v1/chat/report "$(j --argjson m "$LINE" '{messageId:$m}')" > /dev/null
echo "overview: $(a GET /overview | jq -c '{openReports, players, chat24h}')"
echo "reports: $(a GET /reports | jq -c --argjson m "$LINE" '[.[] | select(.id==$m) | {name, text, reports}]')"
echo "hide: $(a POST "/lines/$LINE" "$(j '{action:"hide"}')" | jq -c .ok)"
echo "hidden from players: $(curl -s "$BASE/v1/chat?channel=world" -H "X-Session: $SR" | jq --argjson m "$LINE" '[.lines[] | select(.id==$m)] | length')"

echo "mute 60: $(a POST "/players/$IDO/mute" "$(j '{minutes:60, reason:"insults"}')" | jq -c .ok)"
sleep 3
echo "muted player speaks: $(p "$SO" /v1/chat "$(j '{channel:"world", text:"hello?"}')" | jq -c .code)"
echo "unmute: $(a POST "/players/$IDO/mute" "$(j '{minutes:0}')" | jq -c .ok)"
echo "speaks again: $(p "$SO" /v1/chat "$(j '{channel:"world", text:"sorry"}')" | jq -c '.lines[-1].text')"

TAG="Z$(rid | cut -c1-3 | tr 'a-z' 'A-Z')"
G=$(p "$SO" /v1/guild/create "$(j --arg r "$(rid)" --arg t "$TAG" '{requestId:$r, name:("Smoke " + $t), tag:$t, color:"#C0392B"}')" | jq -r .mine.id)
echo "guild search: $(a GET "/guilds?q=$TAG" | jq -c '[.[] | {tag, members, leader}]')"
NEWTAG="Y$(rid | cut -c1-3 | tr 'a-z' 'A-Z')"
echo "rename: $(a POST "/guilds/$G/rename" "$(j --arg t "$NEWTAG" '{name:("Renamed " + $t), tag:$t}')" | jq -c .ok)"
echo "player sees: $(curl -s "$BASE/v1/guild" -H "X-Session: $SO" | jq -c '{name: .mine.name, tag: .mine.tag, last: .mine.lastEvent}')"

echo "player search: $(a GET "/players?q=$IDO" | jq -c '[.[] | {name, guild, reportedLines}]')"
echo "ban: $(a POST "/players/$IDO/ban" "$(j '{reason:"harassment", hideLines:true}')" | jq -c .ok)"
echo "banned player's request: $(curl -s -o /dev/null -w '%{http_code}' "$BASE/v1/me" -H "X-Session: $SO")"
echo "banned player's login: $(guest "$DO" | jq -c .)"
echo "their guild after the ban: $(a GET "/guilds?q=$NEWTAG" | jq -c '[.[] | {tag, members}]')"
echo "unban: $(a POST "/players/$IDO/unban" | jq -c .ok)"
SO=$(guest "$DO" | jq -r .sessionToken)
echo "back in: $(curl -s -o /dev/null -w '%{http_code}' "$BASE/v1/me" -H "X-Session: $SO")"
TAG2="X$(rid | cut -c1-3 | tr 'a-z' 'A-Z')"
G2=$(p "$SO" /v1/guild/create "$(j --arg r "$(rid)" --arg t "$TAG2" '{requestId:$r, name:("Smoke " + $t), tag:$t, color:"#2F6FD0"}')" | jq -r .mine.id)
p "$SR" /v1/guild/join "$(j --arg r "$(rid)" --arg g "$G2" '{requestId:$r, guildId:$g}')" > /dev/null
echo "disband a guild of two: $(a POST "/guilds/$G2/disband" | jq -c .ok)"
echo "members guildless: $(curl -s "$BASE/v1/me" -H "X-Session: $SO" | jq -c .guild) / $(curl -s "$BASE/v1/me" -H "X-Session: $SR" | jq -c .guild)"
echo "log: $(a GET /log | jq -c '[.[:9][] | .action]')"

for S in "$SO" "$SR" "$SM"; do curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S" > /dev/null; done
echo "cleaned up"
