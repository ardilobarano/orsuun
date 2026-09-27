#!/usr/bin/env bash
# Tester analytics smoke test (27 Sep 2026) against a server started with Admin__Emails=$MOD_EMAIL (see smoke-admin.sh).
# A new hero hunts (a heartbeat), forges, pushes twice and sends guide milestones (a made-up one is refused) and an error
# report; the moderation page's funnel counts its firsts and the errors tab groups the report. Deletes both accounts.
#   Admin__Emails=mod-smoke@example.com ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Orsuun.Server
#   tools/smoke-funnel.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
MOD_EMAIL="${MOD_EMAIL:-mod-smoke@example.com}"
MOD_PASSWORD="moderator-$(uuidgen | cut -c1-8)"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
j() { jq -nc "$@"; }
guest() { curl -s -X POST "$BASE/v1/auth/guest" -H 'Content-Type: application/json' -d "$(j --arg d "$1" '{deviceToken:$d}')"; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
a() { curl -s -X "$1" "$BASE/admin/api$2" -H 'Content-Type: application/json' -H "X-Admin: $ADMIN"; }

SM=$(guest "smoke-$(rid)" | jq -r .sessionToken)
p "$SM" /v1/auth/register "$(j --arg e "$MOD_EMAIL" --arg p "$MOD_PASSWORD" '{email:$e, password:$p}')" > /dev/null
ADMIN=$(curl -s -X POST "$BASE/admin/api/login" -H 'Content-Type: application/json' -d "$(j --arg e "$MOD_EMAIL" --arg p "$MOD_PASSWORD" '{email:$e, password:$p}')" | jq -r .token)
STEPS='[.steps[] | select(.name == "hunted" or .name == "first-ForgeAttempts" or .name == "first-Pushes" or .name == "stage-1" or .name == "tutorial-1" or .name == "tutorial-done") | {name, count}]'
BEFORE=$(a GET "/funnel?days=1" | jq -c "$STEPS")

S=$(guest "smoke-$(rid)" | jq -r .sessionToken)
sleep 2
p "$S" /v1/heartbeat '{}' > /dev/null
ITEM=$(curl -s "$BASE/v1/me" -H "X-Session: $S" | jq -r '.items[0].id')
p "$S" /v1/forge "$(j --arg r "$(rid)" --arg i "$ITEM" '{requestId:$r, itemId:$i, method:"ForgeAlone"}')" > /dev/null
for n in 1 2; do p "$S" /v1/push "$(j --arg r "$(rid)" '{requestId:$r}')" > /dev/null; done
echo "guide step: $(p "$S" /v1/milestone '{"name":"tutorial-1"}' | jq -c .)"
echo "guide done: $(p "$S" /v1/milestone '{"name":"tutorial-done"}' | jq -c .)"
echo "made up: $(p "$S" /v1/milestone '{"name":"level-100"}' | jq -c .code)"
MSG="SmokeException: funnel test $(rid | cut -c1-6)"
p "$S" /v1/client-log "$(j --arg m "$MSG" '{platform:"OSXPlayer", version:"0.1.test", message:$m, stack:"at Smoke.Funnel()"}')" > /dev/null
echo "funnel before: $BEFORE"
echo "funnel after:  $(a GET "/funnel?days=1" | jq -c "$STEPS")"
echo "way in: $(a GET "/funnel?days=1" | jq -c '[.wayIn[] | {label, count}]')"
echo "errors: $(a GET /errors | jq -c --arg m "$MSG" '[.[] | select(.message == $m) | {count, heroes, platforms, versions}]')"
for T in "$S" "$SM"; do curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $T" | jq -c .; done
