#!/usr/bin/env bash
# Password reset and email verification smoke test (27 Sep 2026; a Development server without SMTP keeps each email for
# /v1/dev/mail). Signs up a guest, verifies the email with its code, asks for a reset code (and for an unknown email:
# the same answer), refuses a wrong code, sets a new password with the right one (a second device signs in with it),
# then the old password fails and the new one works. Deletes the account.
# Needs curl, jq.
#   tools/smoke-recovery.sh [http://localhost:5080]
set -u
BASE="${1:-http://localhost:5080}"
rid() { uuidgen | tr -d '-' | tr 'A-Z' 'a-z'; }
post() { curl -s -X POST "$BASE$1" -H 'Content-Type: application/json' -d "$2"; }
p() { curl -s -X POST "$BASE$2" -H 'Content-Type: application/json' -H "X-Session: $1" -d "$3"; }
j() { jq -nc "$@"; }
code() { curl -s "$BASE/v1/dev/mail?email=$1" | jq -r .text | grep -oE '[0-9]{6}' | head -1; }
EMAIL="smoke-$(rid | cut -c1-10)@example.com"
A=$(post /v1/auth/guest "$(j --arg d "smoke-rec-$(rid)" '{deviceToken:$d}')" | jq -r .sessionToken)
echo "sign up: $(p "$A" /v1/auth/register "$(j --arg e "$EMAIL" '{email:$e, password:"first-pass-1"}')" | jq -c '{email, emailVerified}')"
V=$(code "$EMAIL"); echo "verification code emailed: ${V:+yes}"
echo "wrong verify code: $(p "$A" /v1/auth/verify '{"code":"000000"}' | jq -c .code)"
echo "verify: $(p "$A" /v1/auth/verify "$(j --arg c "$V" '{code:$c}')" | jq -c .)"
echo "forgot: $(post /v1/auth/forgot "$(j --arg e "$EMAIL" '{email:$e}')" | jq -c .message)"
echo "forgot, unknown email: $(post /v1/auth/forgot '{"email":"nobody-here@example.com"}' | jq -c .message)"
R=$(code "$EMAIL")
DEV2="smoke-rec2-$(rid)"
echo "wrong reset code: $(post /v1/auth/reset "$(j --arg e "$EMAIL" --arg d "$DEV2" '{email:$e, code:"123123", password:"second-pass-2", deviceToken:$d}')" | jq -c .code)"
echo "too short a password: $(post /v1/auth/reset "$(j --arg e "$EMAIL" --arg c "$R" --arg d "$DEV2" '{email:$e, code:$c, password:"x", deviceToken:$d}')" | jq -c .code)"
echo "reset: $(post /v1/auth/reset "$(j --arg e "$EMAIL" --arg c "$R" --arg d "$DEV2" '{email:$e, code:$c, password:"second-pass-2", deviceToken:$d}')" | jq -c '{session: (.sessionToken | length > 0), characters}')"
echo "the same code again: $(post /v1/auth/reset "$(j --arg e "$EMAIL" --arg c "$R" --arg d "$DEV2" '{email:$e, code:$c, password:"third-pass-3", deviceToken:$d}')" | jq -c .code)"
echo "old password: $(post /v1/auth/login "$(j --arg e "$EMAIL" --arg d "$DEV2" '{email:$e, password:"first-pass-1", deviceToken:$d}')" | jq -c .code)"
S2=$(post /v1/auth/login "$(j --arg e "$EMAIL" --arg d "$DEV2" '{email:$e, password:"second-pass-2", deviceToken:$d}')" | jq -r .sessionToken)
echo "new password signs in: $([ ${#S2} -gt 10 ] && echo yes || echo NO)"
echo "verified on /me: $(curl -s "$BASE/v1/me" -H "X-Session: $S2" | jq -c '{email, emailVerified}')"
curl -s -X DELETE "$BASE/v1/account" -H "X-Session: $S2" | jq -c .
