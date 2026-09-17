#!/usr/bin/env bash
# The forum journey against a running YAF.Host: read the board as an
# anonymous guest, prove the guest is refused a post, sign the host administrator
# in through the rendered login form, then register a member, verify the address
# from the mail YAF wrote to its pickup directory, and prove the member sees the
# board the administrator's own view withholds.
# bash + curl only, and no bash-4 builtins, so the same script runs on macOS,
# Linux, and Git bash on Windows.
#
#   apps/YAF/smoke.sh [base-url]

set -uo pipefail

BASE="${1:-http://127.0.0.1:5087}"
BASE="${BASE%/}"
SITE="$(cd "$(dirname "$0")" && pwd)/YAF.Host/rehost_root"
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

post_form() { # post_form <jar> <url> <out> <target> <extra curl args...>
  local jar="$1" url="$2" out="$3" target="$4"; shift 4
  curl -sS --max-time 180 -A "$UA" -b "$jar" -c "$jar" -o "$out" -w '%{http_code}' -L \
    --data-urlencode "__VIEWSTATE=$VS" \
    --data-urlencode "__VIEWSTATEGENERATOR=$VSG" \
    --data-urlencode "__EVENTVALIDATION=$EV" \
    --data-urlencode "__EVENTTARGET=$target" \
    --data-urlencode "__EVENTARGUMENT=" "$@" "$BASE$url"
}

read_state() { # read_state <html>
  VS=$(field "$1" __VIEWSTATE); VSG=$(field "$1" __VIEWSTATEGENERATOR); EV=$(field "$1" __EVENTVALIDATION)
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
check_contains 'guest sees the seeded forum' "$work/index.html" 'Test Forum'
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
check_contains 'topic shows its subject' "$work/topic.html" 'Hello from YAF.NET'

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

# --- the administrator opens a topic ---------------------------------------

TOPIC="Rehost smoke topic $(date +%s)"

code=$(get "$member" '/PostTopic?f=1' "$work/new-topic.html")
check 'new topic form status' "$code" 200
check_contains 'the form asks for a subject' "$work/new-topic.html" 'name="forum$ctl02$TopicSubjectTextBox"'

read_state "$work/new-topic.html"
code=$(post_form "$member" '/PostTopic?f=1' "$work/topic-posted.html" 'forum$ctl02$PostReply' \
  --data-urlencode "forum\$ctl02\$TopicSubjectTextBox=$TOPIC" \
  --data-urlencode "forum\$ctl02\$YafTextEditor=Opened by the smoke run.")
check 'posting the topic' "$code" 200
check_contains 'the topic shows its subject' "$work/topic-posted.html" "$TOPIC"

topic_id=$(grep -o 'PostMessage?t=[0-9]*' "$work/topic-posted.html" | head -1 | sed 's/.*t=//')
opening_id=$(grep -o 'DeleteMessage?m=[0-9]*' "$work/topic-posted.html" | sed 's/.*m=//' | sort -n | head -1)
if [ -n "$topic_id" ]; then pass 'the topic has an id'
else fail 'the topic has an id' 'no PostMessage?t= link on the posted topic'; fi

# --- the member replies ----------------------------------------------------

REPLY="Replied by the member at $(date +%s)"

code=$(get "$newbie" "/PostMessage?t=$topic_id&f=1" "$work/reply-form.html")
check 'member reply form status' "$code" 200

read_state "$work/reply-form.html"
code=$(post_form "$newbie" "/PostMessage?t=$topic_id&f=1" "$work/replied.html" 'forum$ctl02$PostReply' \
  --data-urlencode "forum\$ctl02\$YafTextEditor=$REPLY")
check 'posting the reply' "$code" 200
check_contains 'the reply is on the topic' "$work/replied.html" "$REPLY"
check_contains 'the reply is attributed to the member' "$work/replied.html" "$NEW_USER"

reply_id=$(grep -o 'DeleteMessage?m=[0-9]*' "$work/replied.html" | sed 's/.*m=//' | sort -n | tail -1)

code=$(get "$guest" "/Posts/t$topic_id-x" "$work/guest-topic.html")
check_contains 'a guest can read the reply' "$work/guest-topic.html" "$REPLY"

# --- only the moderator may moderate ---------------------------------------

check_contains 'the member may delete their own reply' "$work/replied.html" "DeleteMessage?m=$reply_id"
check_missing "the member cannot delete the administrator's post" "$work/replied.html" "DeleteMessage?m=$opening_id"

code=$(get "$member" "/Posts/t$topic_id-x" "$work/admin-topic.html")
check 'administrator topic status' "$code" 200
check_contains 'the administrator is offered a delete' "$work/admin-topic.html" "DeleteMessage?m=$reply_id"

code=$(get "$member" "/DeleteMessage?m=$reply_id&action=delete" "$work/delete-form.html")
check 'delete confirmation status' "$code" 200

read_state "$work/delete-form.html"
code=$(post_form "$member" "/DeleteMessage?m=$reply_id&action=delete" "$work/deleted.html" 'forum$ctl02$Delete')
check 'deleting the reply' "$code" 200

code=$(get "$guest" "/Posts/t$topic_id-x" "$work/guest-after.html")
check 'topic still reads after moderation' "$code" 200
check_missing 'the moderated reply is gone for a guest' "$work/guest-after.html" "$REPLY"

# --- the Web API host answers ----------------------------------------------

code=$(curl -sS --max-time 120 -A "$UA" -b "$member" -c "$member" -o "$work/forums.json" \
  -w '%{http_code}' -H 'Content-Type: application/json' -X POST \
  --data '{"ForumId":0,"Page":0,"PageSize":20,"SearchTerm":""}' "$BASE/api/Forum/GetForums")
check 'web api status' "$code" 200
check_contains 'the api names the category' "$work/forums.json" 'Test Category'
check_contains 'the api names the forum' "$work/forums.json" 'Test Forum'

# --- the search index finds the topic --------------------------------------
# Indexing follows the post, so give it a bounded wait rather than one shot.

found=''
n=1
while [ "$n" -le 12 ]; do
  curl -sS --max-time 120 -A "$UA" -b "$member" -c "$member" -o "$work/search.json" \
    -H 'Content-Type: application/json' -X POST \
    --data "{\"ForumId\":0,\"TopicId\":0,\"PageSize\":20,\"Page\":0,\"SearchTerm\":\"${TOPIC##* }\",\"AllForumsOption\":true}" \
    "$BASE/api/Search/GetSearchResults" >/dev/null
  if grep -qF -- "${TOPIC##* }" "$work/search.json" 2>/dev/null; then found=yes; break; fi
  sleep 5
  n=$((n + 1))
done
if [ -n "$found" ]; then pass 'search finds the topic this run posted'
else fail 'search finds the topic this run posted' "no hit for '${TOPIC##* }' after 12 tries"; fi

printf '\n'
if [ "$failures" -eq 0 ]; then
  printf 'all checks passed against %s\n' "$BASE"
else
  printf '%s check(s) failed against %s\n' "$failures" "$BASE"
fi
exit $((failures > 0))
