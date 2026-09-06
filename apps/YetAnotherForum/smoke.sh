#!/usr/bin/env bash
# The forum journey against a running YetAnotherForum.Host: read the board as an
# anonymous guest, prove the guest is refused a post, sign the host administrator
# in through the rendered login form, then register a member, verify the address
# from the mail YAF wrote to its pickup directory, and prove the member sees the
# board the administrator's own view withholds.
# bash + curl only, and no bash-4 builtins, so the same script runs on macOS,
# Linux, and Git bash on Windows.
#
#   apps/YetAnotherForum/smoke.sh [base-url]

set -uo pipefail

BASE="${1:-http://127.0.0.1:5087}"
SITE="$(cd "$(dirname "$0")" && pwd)/YetAnotherForum.Host/bin/site"
MAIL_DIR="$SITE/App_Data/mail"
ADMIN_USER='hostadmin'
ADMIN_PASSWORD='Rehost!Dev2026'
UA='Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
NEW_USER="member$(date +%s)"
guest="$work/guest.txt"
member="$work/member.txt"
newbie="$work/newbie.txt"
: > "$guest"
: > "$member"
: > "$newbie"

failures=0

pass() { printf 'PASS  %s\n' "$1"; }
fail() { printf 'FAIL  %s\n        %s\n' "$1" "$2"; failures=$((failures + 1)); }

check() { # check <label> <actual> <expected>
  if [ "$2" = "$3" ]; then pass "$1"; else fail "$1" "expected: $3
        actual:   $2"; fi
}

check_contains() { # check_contains <label> <file> <needle>
  if grep -qF -- "$3" "$2"; then pass "$1"; else fail "$1" "missing: $3"; fi
}

check_missing() { # check_missing <label> <file> <needle>
  if grep -qF -- "$3" "$2"; then fail "$1" "unexpectedly present: $3"; else pass "$1"; fi
}

field() { # field <html> <name>
  tr '<' '\n' < "$1" | grep "name=\"$2\"" | sed 's/.*value="//; s/".*//' | head -1
}

approval_link() { # approval_link <eml> -- the link out of the first base64 part
  awk '
    /^Content-Transfer-Encoding: base64/ { want = 1; next }
    want && /^[[:space:]]*$/ { want = 0; body = 1; next }
    body && /^----boundary/ { exit }
    body { printf "%s", $0 }
  ' "$1" | tr -d '\r' | openssl base64 -d -A 2>/dev/null \
    | tr -c 'A-Za-z0-9:/?=&._%~-' '\n' | grep -F 'Account/Approve?code=' | head -1
}

get() { # get <jar> <path> <out>
  curl -sS --max-time 120 -A "$UA" -b "$1" -c "$1" -o "$3" -w '%{http_code}' "$BASE$2"
}

status_of() { # status_of <jar> <path>
  curl -sS --max-time 120 -A "$UA" -b "$1" -c "$1" -o /dev/null -w '%{http_code}' "$BASE$2"
}

# --- anonymous board -------------------------------------------------------

code=$(get "$guest" / "$work/index.html")
check 'guest board index status' "$code" 200
check_contains 'guest sees the board name' "$work/index.html" 'Rehost Test Forum'
check_contains 'guest sees the seeded category' "$work/index.html" 'Test Category'
check_contains 'guest sees the seeded topic' "$work/index.html" 'Hello from YAF.NET'
check_contains 'guest is greeted as a guest' "$work/index.html" 'Welcome Guest!'
check_contains 'guest is offered a login' "$work/index.html" 'LoginLink'
check_missing 'guest has no sign-out' "$work/index.html" '/Account/Logout'
check_missing 'guest has no administration' "$work/index.html" '/Admin/Admin'

code=$(get "$guest" '/category/1-Test-Category' "$work/category.html")
check 'guest category status' "$code" 200
check_contains 'category lists the forum' "$work/category.html" 'Test Forum'

code=$(get "$guest" '/Posts/t1--Hello-from-YAF-NET' "$work/topic.html")
check 'guest topic status' "$code" 200
check_contains 'topic shows its author' "$work/topic.html" 'hostadmin'

check 'theme stylesheet is served' \
  "$(status_of "$guest" '/Content/Themes/yaf/bootstrap-forum.min.css?v=1')" 200

# --- the guest may not post ------------------------------------------------

code=$(get "$guest" '/PostMessage/1' "$work/guest-post.html")
check_missing 'guest is refused the post editor' "$work/guest-post.html" 'name="forum$ctl02$PostReply"'

# --- sign the administrator in --------------------------------------------

code=$(get "$member" '/Account/Login' "$work/login.html")
check 'login page status' "$code" 200
check_contains 'login form asks for a user name' "$work/login.html" 'name="forum$ctl02$UserName"'

code=$(curl -sS --max-time 120 -A "$UA" -b "$member" -c "$member" -o "$work/after-login.html" \
  -w '%{http_code}' -L \
  --data-urlencode "__VIEWSTATE=$(field "$work/login.html" __VIEWSTATE)" \
  --data-urlencode "__VIEWSTATEGENERATOR=$(field "$work/login.html" __VIEWSTATEGENERATOR)" \
  --data-urlencode "__EVENTVALIDATION=$(field "$work/login.html" __EVENTVALIDATION)" \
  --data-urlencode "__EVENTTARGET=forum\$ctl02\$LoginButton" \
  --data-urlencode "__EVENTARGUMENT=" \
  --data-urlencode "forum\$ctl02\$UserName=$ADMIN_USER" \
  --data-urlencode "forum\$ctl02\$Password=$ADMIN_PASSWORD" \
  "$BASE/Account/Login")
check 'login post status' "$code" 200
check_contains 'the OWIN auth cookie is set' "$member" '.AspNet.ApplicationCookie'

code=$(get "$member" / "$work/member-index.html")
check 'member board index status' "$code" 200
check_missing 'member is no longer a guest' "$work/member-index.html" 'Welcome Guest!'
check_contains 'member is named on the board' "$work/member-index.html" "$ADMIN_USER"
check_contains 'member can sign out' "$work/member-index.html" '/Account/Logout'
check_contains 'administrator sees administration' "$work/member-index.html" '/Admin/Admin'

# --- register a member and verify the address ------------------------------

code=$(get "$newbie" '/RulesAndPrivacy' "$work/rules.html")
check 'registration rules status' "$code" 200

code=$(curl -sS --max-time 120 -A "$UA" -b "$newbie" -c "$newbie" -o "$work/register.html" \
  -w '%{http_code}' -L \
  --data-urlencode "__VIEWSTATE=$(field "$work/rules.html" __VIEWSTATE)" \
  --data-urlencode "__VIEWSTATEGENERATOR=$(field "$work/rules.html" __VIEWSTATEGENERATOR)" \
  --data-urlencode "__EVENTVALIDATION=$(field "$work/rules.html" __EVENTVALIDATION)" \
  --data-urlencode "__EVENTTARGET=forum\$ctl03\$Accept" --data-urlencode "__EVENTARGUMENT=" \
  "$BASE/RulesAndPrivacy")
check 'accepting the rules reaches registration' "$code" 200
check_contains 'registration asks for an address' "$work/register.html" 'name="forum$ctl02$Email"'

before=$(ls "$MAIL_DIR" 2>/dev/null | wc -l | tr -d ' ')

code=$(curl -sS --max-time 180 -A "$UA" -b "$newbie" -c "$newbie" -o "$work/registered.html" \
  -w '%{http_code}' -L \
  --data-urlencode "__VIEWSTATE=$(field "$work/register.html" __VIEWSTATE)" \
  --data-urlencode "__VIEWSTATEGENERATOR=$(field "$work/register.html" __VIEWSTATEGENERATOR)" \
  --data-urlencode "__EVENTVALIDATION=$(field "$work/register.html" __EVENTVALIDATION)" \
  --data-urlencode "__EVENTTARGET=forum\$ctl02\$CreateUser" --data-urlencode "__EVENTARGUMENT=" \
  --data-urlencode "forum\$ctl02\$UserName=$NEW_USER" \
  --data-urlencode "forum\$ctl02\$Email=$NEW_USER@rehost.test" \
  --data-urlencode "forum\$ctl02\$Password=$ADMIN_PASSWORD" \
  --data-urlencode "forum\$ctl02\$ConfirmPassword=$ADMIN_PASSWORD" \
  "$BASE/Account/Register")
check 'registration status' "$code" 200

after=$(ls "$MAIL_DIR" 2>/dev/null | wc -l | tr -d ' ')
if [ "$after" -gt "$before" ]; then pass 'the verification mail was written'
else fail 'the verification mail was written' "no new file under $MAIL_DIR"; fi

newest=$(ls -t "$MAIL_DIR"/*.eml 2>/dev/null | head -1)
link=''
if [ -n "$newest" ]; then link=$(approval_link "$newest"); fi
if [ -n "$link" ]; then pass 'the mail carries an approval link'
else fail 'the mail carries an approval link' "none found in $newest"; fi

code=$(curl -sS --max-time 120 -A "$UA" -b "$newbie" -c "$newbie" -o "$work/approved.html" \
  -w '%{http_code}' -L "${link:-$BASE/}")
check 'following the approval link' "$code" 200

# --- the member is not an administrator ------------------------------------

code=$(get "$newbie" '/Account/Login' "$work/member-login.html")
check 'member login page status' "$code" 200

code=$(curl -sS --max-time 120 -A "$UA" -b "$newbie" -c "$newbie" -o "$work/member-in.html" \
  -w '%{http_code}' -L \
  --data-urlencode "__VIEWSTATE=$(field "$work/member-login.html" __VIEWSTATE)" \
  --data-urlencode "__VIEWSTATEGENERATOR=$(field "$work/member-login.html" __VIEWSTATEGENERATOR)" \
  --data-urlencode "__EVENTVALIDATION=$(field "$work/member-login.html" __EVENTVALIDATION)" \
  --data-urlencode "__EVENTTARGET=forum\$ctl02\$LoginButton" --data-urlencode "__EVENTARGUMENT=" \
  --data-urlencode "forum\$ctl02\$UserName=$NEW_USER" \
  --data-urlencode "forum\$ctl02\$Password=$ADMIN_PASSWORD" \
  "$BASE/Account/Login")
check 'member sign-in status' "$code" 200

code=$(get "$newbie" / "$work/newbie-index.html")
check 'member board index status' "$code" 200
check_contains 'the member is named on the board' "$work/newbie-index.html" "$NEW_USER"
check_contains 'the member can sign out' "$work/newbie-index.html" '/Account/Logout'
check_missing 'the member gets no administration' "$work/newbie-index.html" '/Admin/Admin'

printf '\n'
if [ "$failures" -eq 0 ]; then
  printf 'all checks passed against %s\n' "$BASE"
else
  printf '%s check(s) failed against %s\n' "$failures" "$BASE"
fi
exit $((failures > 0))
