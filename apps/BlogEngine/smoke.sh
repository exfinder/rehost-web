#!/usr/bin/env bash
# The blog journey against a running BlogEngine.Host on SQLite: read the public pages
# and the seven .axd feeds as a guest, sign the seeded administrator in through the
# asp:Login form, read and write through Web API, post a comment through the client
# callback and find it in the database, then sign out through login.aspx?logoff and
# through Logout.cshtml.
# bash + curl only, and no bash-4 builtins, so the same script runs on macOS,
# Linux, and Git bash on Windows.
#
#   apps/BlogEngine/smoke.sh [base-url]

set -uo pipefail

BASE="${1:-http://127.0.0.1:5086}"
BASE="${BASE%/}"
SITE="$(cd "$(dirname "$0")" && pwd)/BlogEngine.Host/rehost_root"
DB="$SITE/App_Data/BlogEngine.s3db"
ADMIN_USER='Admin'
ADMIN_PASSWORD='admin'
POST_TITLE='Welcome to BlogEngine.NET using SQLite'
POST_PATH='/post/2015/10/09/Welcome-to-BlogEngineNET-using-SQLite'
AUTH_COOKIE='.AUXBLOGENGINE'
UA='Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
STAMP=$(date +%s)
guest="$work/guest.txt"
admin="$work/admin.txt"
: > "$guest"
: > "$admin"

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

check_stored() { # check_stored <label> <needle>
  if LC_ALL=C grep -aqF -- "$2" "$DB"*; then pass "$1"
  else fail "$1" "not in $DB: $2"; fi
}

field() { # field <html> <name>
  tr '<' '\n' < "$1" | grep -F "name=\"$2\"" | sed 's/.*value="//; s/".*//' | head -1
}

form_action() { # form_action <html> <page-path> -- the rendered action, resolved against the page
  local action
  action=$(grep -oE '<form[^>]*action="[^"]*"' "$1" | head -1 | sed 's/.*action="//; s/"$//; s/&amp;/\&/g')
  case "$action" in
    /*) printf '%s' "$action" ;;
    *) printf '%s/%s' "${2%/*}" "${action#./}" ;;
  esac
}

get() { # get <jar> <path> <out>
  curl -sS --max-time 120 -A "$UA" -b "$1" -c "$1" -o "$3" -w '%{http_code}' "$BASE$2"
}

get_typed() { # get_typed <jar> <path> <out> -- "<status> <content type>"
  curl -sS --max-time 120 -A "$UA" -b "$1" -c "$1" -o "$3" -w '%{http_code} %{content_type}' "$BASE$2"
}

redirect_of() { # redirect_of <jar> <path> -- "<status> <location>"
  curl -sS --max-time 120 -A "$UA" -b "$1" -c "$1" -o /dev/null -w '%{http_code} %{redirect_url}' "$BASE$2"
}

has_auth_cookie() { # has_auth_cookie <jar>
  if grep -qF "$AUTH_COOKIE" "$1"; then printf yes; else printf no; fi
}

sign_in() { # sign_in <jar> <label>
  local code action
  code=$(get "$1" '/Account/login.aspx' "$work/login.html")
  check "$2: login page status" "$code" 200
  check_contains "$2: login form asks for a user name" "$work/login.html" 'name="ctl00$MainContent$LoginUser$UserName"'
  action=$(form_action "$work/login.html" '/Account/login.aspx')
  code=$(curl -sS --max-time 120 -A "$UA" -b "$1" -c "$1" -o /dev/null -w '%{http_code} %{redirect_url}' \
    --data-urlencode "__VIEWSTATE=$(field "$work/login.html" __VIEWSTATE)" \
    --data-urlencode "__VIEWSTATEGENERATOR=$(field "$work/login.html" __VIEWSTATEGENERATOR)" \
    --data-urlencode "__EVENTVALIDATION=$(field "$work/login.html" __EVENTVALIDATION)" \
    --data-urlencode '__EVENTTARGET=' --data-urlencode '__EVENTARGUMENT=' \
    --data-urlencode "ctl00\$MainContent\$LoginUser\$UserName=$ADMIN_USER" \
    --data-urlencode "ctl00\$MainContent\$LoginUser\$Password=$ADMIN_PASSWORD" \
    --data-urlencode 'ctl00$MainContent$LoginUser$LoginButton=Log in' \
    "$BASE$action")
  check "$2: sign-in redirects home" "$code" "302 $BASE/"
  check "$2: the forms-auth cookie is set" "$(has_auth_cookie "$1")" yes
}

# --- public pages ------------------------------------------------------------

code=$(get "$guest" / "$work/home.html")
check 'home status' "$code" 200
check_contains 'home lists the seeded post' "$work/home.html" "$POST_TITLE"
check_contains 'home offers a login' "$work/home.html" '/Account/login.aspx?ReturnURL=/admin/'

code=$(get "$guest" "$POST_PATH" "$work/post.html")
check 'post status' "$code" 200
check_contains 'post shows its title' "$work/post.html" "$POST_TITLE"
check_contains 'post renders the comment form' "$work/post.html" 'name="ctl00$cphBody$ucCommentList$hfCaptcha"'
check_contains 'post initializes client callbacks' "$work/post.html" 'WebForm_InitCallback();'

webforms=$(grep -oE 'src="/WebResource\.axd\?[^"]*"' "$work/post.html" | head -1 | sed 's/^src="//; s/"$//; s/&amp;/\&/g')
code=$(get "$guest" "${webforms:-/WebResource.axd}" "$work/webforms.js")
check 'WebForms.js status' "$code" 200
check_contains 'WebForms.js defines the callback' "$work/webforms.js" 'function WebForm_DoCallback('

code=$(get "$guest" '/category/General' "$work/category.html")
check 'category status' "$code" 200
check_contains 'category lists the post' "$work/category.html" "$POST_TITLE"

code=$(get "$guest" "/author/$ADMIN_USER" "$work/author.html")
check 'author status' "$code" 200
check_contains 'author lists the post' "$work/author.html" "$POST_TITLE"

code=$(get "$guest" '/archive' "$work/archive.html")
check 'archive status' "$code" 200
check_contains 'archive lists the post' "$work/archive.html" "$POST_TITLE"

code=$(get "$guest" '/contact' "$work/contact.html")
check 'contact status' "$code" 200
check_contains 'contact renders its form' "$work/contact.html" 'name="ctl00$cphBody$txtName"'

code=$(get "$guest" '/search?q=welcome' "$work/search.html")
check 'search status' "$code" 200
check_contains 'search names the query' "$work/search.html" "Search results for 'welcome'"
check_contains 'search finds the post' "$work/search.html" "$POST_TITLE"

check 'unknown path is 404' "$(get "$guest" '/no-such-page' "$work/missing.html")" 404

# --- feeds -------------------------------------------------------------------

check 'RSS status and type' "$(get_typed "$guest" '/syndication.axd' "$work/rss.xml")" '200 application/rss+xml'
check_contains 'RSS carries the post' "$work/rss.xml" "<title>$POST_TITLE</title>"
check 'Atom status and type' "$(get_typed "$guest" '/syndication.axd?format=atom' "$work/atom.xml")" '200 application/atom+xml'
check_contains 'Atom carries the post' "$work/atom.xml" "$POST_TITLE"
check 'OpenSearch status' "$(get "$guest" '/opensearch.axd' "$work/opensearch.xml")" 200
check_contains 'OpenSearch describes the blog' "$work/opensearch.xml" '<OpenSearchDescription'
check 'RSD status' "$(get "$guest" '/rsd.axd' "$work/rsd.xml")" 200
check_contains 'RSD names the engine' "$work/rsd.xml" '<engineName>BlogEngine.NET'
check 'SIOC status and type' "$(get_typed "$guest" '/sioc.axd' "$work/sioc.xml")" '200 application/rdf+xml'
check_contains 'SIOC is RDF' "$work/sioc.xml" '<rdf:RDF'
check 'FOAF status and type' "$(get_typed "$guest" '/foaf.axd' "$work/foaf.xml")" '200 application/rdf+xml'
check_contains 'FOAF names a person' "$work/foaf.xml" 'foaf:Person'
check 'APML status' "$(get "$guest" '/apml.axd' "$work/apml.xml")" 200
check_contains 'APML is APML' "$work/apml.xml" '<APML>'

check 'a guest is refused the users API' "$(get "$guest" '/api/users' "$work/guest-users.html")" 401

# --- sign in and the Web API ------------------------------------------------

sign_in "$admin" 'administrator'

code=$(get "$admin" / "$work/home-admin.html")
check 'signed-in home status' "$code" 200
check_contains 'signed-in home links the admin panel' "$work/home-admin.html" 'href="/admin/" id="ctl00_aLogin"'

code=$(get "$admin" '/admin/' "$work/admin.html")
check 'admin panel status' "$code" 200
check_contains 'admin panel renders' "$work/admin.html" '(Admin)'

check 'posts API status and type' "$(get_typed "$admin" '/api/posts' "$work/posts.json")" '200 application/json; charset=utf-8'
check_contains 'posts API lists the post' "$work/posts.json" "\"Title\":\"$POST_TITLE\""
check 'tags API status and type' "$(get_typed "$admin" '/api/tags' "$work/tags.json")" '200 application/json; charset=utf-8'
check_contains 'tags API lists a seeded tag' "$work/tags.json" '"TagName":"welcome"'
check 'categories API status and type' "$(get_typed "$admin" '/api/categories' "$work/categories.json")" '200 application/json; charset=utf-8'
check_contains 'categories API lists the category' "$work/categories.json" '"Title":"General"'
check 'users API status and type' "$(get_typed "$admin" '/api/users' "$work/users.json")" '200 application/json; charset=utf-8'
check_contains 'users API lists the administrator' "$work/users.json" "\"UserName\":\"$ADMIN_USER\""

PAGE_SLUG="smoke-page-$STAMP"
PAGE_TEXT="Published by the smoke run $STAMP."
code=$(curl -sS --max-time 120 -A "$UA" -b "$admin" -c "$admin" -o "$work/page.json" -w '%{http_code}' \
  -H 'Content-Type: application/json' \
  --data "{\"Title\":\"Smoke page $STAMP\",\"Slug\":\"$PAGE_SLUG\",\"Content\":\"<p>$PAGE_TEXT</p>\",\"IsPublished\":true,\"ShowInList\":true,\"DateCreated\":\"2026-01-01 12:00\",\"Parent\":{\"OptionName\":\"none\",\"OptionValue\":\"none\"}}" \
  "$BASE/api/pages")
check 'pages API creates a page' "$code" 201
check_stored 'the page is in the database' "$PAGE_SLUG"

code=$(get "$guest" "/page/$PAGE_SLUG" "$work/page.html")
check 'page status' "$code" 200
check_contains 'page shows its content' "$work/page.html" "$PAGE_TEXT"

# --- a comment through the client callback ----------------------------------

COMMENT="Comment from the smoke run $STAMP"
code=$(get "$guest" "$POST_PATH" "$work/comment-form.html")
captcha=$(field "$work/comment-form.html" 'ctl00$cphBody$ucCommentList$hfCaptcha')
action=$(form_action "$work/comment-form.html" "$POST_PATH")
code=$(curl -sS --max-time 120 -A "$UA" -b "$guest" -c "$guest" -o "$work/callback.txt" -w '%{http_code}' \
  --data-urlencode '__EVENTTARGET=' --data-urlencode '__EVENTARGUMENT=' \
  --data-urlencode "__VIEWSTATE=$(field "$work/comment-form.html" __VIEWSTATE)" \
  --data-urlencode "__VIEWSTATEGENERATOR=$(field "$work/comment-form.html" __VIEWSTATEGENERATOR)" \
  --data-urlencode "ctl00\$cphBody\$ucCommentList\$hfCaptcha=$captcha" \
  --data-urlencode 'txtName=' --data-urlencode 'txtContent=' \
  --data-urlencode '__CALLBACKID=ctl00$cphBody$ucCommentList' \
  --data-urlencode "__CALLBACKPARAM=Smoke Reader-|-reader@rehost.test-|--|--|-$COMMENT-|-false-|-false-|-$captcha-|--|--|--|--|-" \
  --data-urlencode "__EVENTVALIDATION=$(field "$work/comment-form.html" __EVENTVALIDATION)" \
  "$BASE$action")
check 'comment callback status' "$code" 200
check_contains 'the callback renders the comment' "$work/callback.txt" "$COMMENT"
check_missing 'the callback reports no error' "$work/callback.txt" 'unknown error'
check_stored 'the comment is in the database' "$COMMENT"

code=$(get "$guest" "$POST_PATH" "$work/post-after.html")
check_contains 'the post shows the approved comment' "$work/post-after.html" "$COMMENT"

# --- sign out ----------------------------------------------------------------

check 'login.aspx?logoff redirects home' "$(redirect_of "$admin" '/Account/login.aspx?logoff')" "302 $BASE/"
check 'logoff clears the forms-auth cookie' "$(has_auth_cookie "$admin")" no
check 'the users API refuses after logoff' "$(get "$admin" '/api/users' "$work/after-logoff.html")" 401
code=$(get "$admin" / "$work/home-signed-out.html")
check_contains 'home offers a login again' "$work/home-signed-out.html" '/Account/login.aspx?ReturnURL=/admin/'

sign_in "$admin" 'second sign-in'
check 'Logout.cshtml redirects home' "$(redirect_of "$admin" '/Account/Logout.cshtml')" "302 $BASE/"
check 'Logout.cshtml clears the forms-auth cookie' "$(has_auth_cookie "$admin")" no
check 'the users API refuses after Logout.cshtml' "$(get "$admin" '/api/users' "$work/after-logout.html")" 401

printf '\n'
if [ "$failures" -eq 0 ]; then
  printf 'all checks passed against %s\n' "$BASE"
else
  printf '%s check(s) failed against %s\n' "$failures" "$BASE"
fi
exit $((failures > 0))
