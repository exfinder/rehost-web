#!/usr/bin/env bash
# Register, log in, and log off against a running WebFormsIdentityApplication.Host,
# asserting the .NET Framework baseline recorded in
# docs/research/webforms-identity-application-gaps.md. bash + curl only, so the same
# script runs on macOS, Linux, and Git bash on Windows.
#
#   apps/WebFormsIdentityApplication/smoke.sh [base-url]

set -uo pipefail

BASE="${1:-http://127.0.0.1:5082}"
EMAIL="alice+$(date +%s)@example.com"
PASSWORD='Passw0rd!'

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
jar="$work/cookies.txt"
: > "$jar"

failures=0

check() { # check <label> <actual> <expected>
  if [ "$2" = "$3" ]; then
    printf 'PASS  %s\n' "$1"
  else
    printf 'FAIL  %s\n        expected: %s\n        actual:   %s\n' "$1" "$3" "$2"
    failures=$((failures + 1))
  fi
}

check_contains() { # check_contains <label> <file> <needle>
  if grep -qF -- "$3" "$2"; then
    printf 'PASS  %s\n' "$1"
  else
    printf 'FAIL  %s\n        missing: %s\n' "$1" "$3"
    failures=$((failures + 1))
  fi
}

check_absent() { # check_absent <label> <file> <needle>
  if grep -qF -- "$3" "$2"; then
    printf 'FAIL  %s\n        unexpected: %s\n' "$1" "$3"
    failures=$((failures + 1))
  else
    printf 'PASS  %s\n' "$1"
  fi
}

location() { grep -i '^location:' "$1" | tr -d '\r' | sed 's/^[Ll]ocation: *//'; }

set_cookie() { grep -i '^set-cookie: *'"$2"'=' "$1" | tr -d '\r'; }

field() { # field <html> <name>
  tr '<' '\n' < "$1" | grep "name=\"$2\"" | sed 's/.*value="//; s/".*//' | head -1
}

submit_name() { # the auto-generated submit button name ASP.NET assigns the form
  tr '<' '\n' < "$1" | grep 'type="submit"' | grep -o 'name="[^"]*"' | head -1 |
    sed 's/name="//; s/"$//'
}

postback_target() { # the LoginStatus __doPostBack target rendered by Site.Master
  grep -o "__doPostBack(&#39;[^&]*&#39;" "$1" | head -1 |
    sed 's/__doPostBack(&#39;//; s/&#39;$//'
}

get() { # get <path> <name>
  : > "$work/$2.html"
  : > "$work/$2.head"
  curl -sS -b "$jar" -c "$jar" -o "$work/$2.html" -D "$work/$2.head" \
    -w '%{http_code}' --max-time 300 "$BASE$1"
}

echo "base $BASE"
echo "user $EMAIL"
echo

status=$(get / home-anon)
check 'GET / anonymous -> 200' "$status" 200
check_contains 'GET / sets __AntiXsrfToken' "$work/home-anon.head" '__AntiXsrfToken='

status=$(get /Account/Manage manage-anon)
check 'GET /Account/Manage anonymous -> 302' "$status" 302
check 'GET /Account/Manage anonymous -> absolute login Location' \
  "$(location "$work/manage-anon.head")" \
  "$BASE/Account/Login?ReturnUrl=%2FAccount%2FManage"

status=$(get /Account/Register register)
check 'GET /Account/Register -> 200' "$status" 200
check_contains 'register form carries the e-mail field' "$work/register.html" \
  'name="ctl00$MainContent$Email"'

submit=$(submit_name "$work/register.html")
status=$(curl -sS -b "$jar" -c "$jar" -o "$work/register-post.html" -D "$work/register-post.head" \
  -w '%{http_code}' --max-time 300 -X POST "$BASE/Account/Register" \
  --data-urlencode "__VIEWSTATE=$(field "$work/register.html" __VIEWSTATE)" \
  --data-urlencode "__VIEWSTATEGENERATOR=$(field "$work/register.html" __VIEWSTATEGENERATOR)" \
  --data-urlencode "__EVENTVALIDATION=$(field "$work/register.html" __EVENTVALIDATION)" \
  --data-urlencode "ctl00\$MainContent\$Email=$EMAIL" \
  --data-urlencode "ctl00\$MainContent\$Password=$PASSWORD" \
  --data-urlencode "ctl00\$MainContent\$ConfirmPassword=$PASSWORD" \
  --data-urlencode "$submit=Register")
check 'POST /Account/Register -> 302' "$status" 302
check 'POST /Account/Register -> Location /' "$(location "$work/register-post.head")" /
check_contains 'register sets .AspNet.ApplicationCookie' "$work/register-post.head" \
  'Set-Cookie: .AspNet.ApplicationCookie='
check_contains 'register cookie is HttpOnly on path /' \
  "$work/register-post.head" 'path=/; HttpOnly'
check_contains 'register clears .AspNet.TwoFactorCookie' "$work/register-post.head" \
  'Set-Cookie: .AspNet.TwoFactorCookie=;'
check_contains 'register clears .AspNet.ExternalCookie' "$work/register-post.head" \
  'Set-Cookie: .AspNet.ExternalCookie=;'

status=$(get /Account/Manage manage)
check 'GET /Account/Manage signed in -> 200' "$status" 200
check_contains 'GET /Account/Manage greets the user' "$work/manage.html" "Hello, $EMAIL"

status=$(get / home)
check 'GET / signed in -> 200' "$status" 200
target=$(postback_target "$work/home.html")
check_contains 'LoginStatus renders a log-off postback' "$work/home.html" "__doPostBack(&#39;$target"

status=$(curl -sS -b "$jar" -c "$jar" -o "$work/logoff.html" -D "$work/logoff.head" \
  -w '%{http_code}' --max-time 300 -X POST "$BASE/" \
  --data-urlencode "__EVENTTARGET=$target" \
  --data-urlencode '__EVENTARGUMENT=' \
  --data-urlencode "__VIEWSTATE=$(field "$work/home.html" __VIEWSTATE)" \
  --data-urlencode "__VIEWSTATEGENERATOR=$(field "$work/home.html" __VIEWSTATEGENERATOR)" \
  --data-urlencode "__EVENTVALIDATION=$(field "$work/home.html" __EVENTVALIDATION)")
check 'POST / log off -> 302' "$status" 302
check 'POST / log off -> Location /' "$(location "$work/logoff.head")" /
check_contains 'log off expires .AspNet.ApplicationCookie' "$work/logoff.head" \
  'Set-Cookie: .AspNet.ApplicationCookie=; expires='
check_contains 'log off expires .ASPXAUTH (FormsAuthentication.SignOut)' "$work/logoff.head" \
  'Set-Cookie: .ASPXAUTH=; expires='

status=$(get /Account/Manage manage-after)
check 'GET /Account/Manage after log off -> 302' "$status" 302
check 'GET /Account/Manage after log off -> absolute login Location' \
  "$(location "$work/manage-after.head")" \
  "$BASE/Account/Login?ReturnUrl=%2FAccount%2FManage"

status=$(get /Account/Login login-bad)
check 'GET /Account/Login -> 200' "$status" 200
submit=$(submit_name "$work/login-bad.html")
status=$(curl -sS -b "$jar" -c "$jar" -o "$work/login-bad-post.html" -D "$work/login-bad-post.head" \
  -w '%{http_code}' --max-time 300 -X POST "$BASE/Account/Login" \
  --data-urlencode "__VIEWSTATE=$(field "$work/login-bad.html" __VIEWSTATE)" \
  --data-urlencode "__VIEWSTATEGENERATOR=$(field "$work/login-bad.html" __VIEWSTATEGENERATOR)" \
  --data-urlencode "__EVENTVALIDATION=$(field "$work/login-bad.html" __EVENTVALIDATION)" \
  --data-urlencode "ctl00\$MainContent\$Email=$EMAIL" \
  --data-urlencode "ctl00\$MainContent\$Password=wrong-$PASSWORD" \
  --data-urlencode "$submit=Log in")
check 'POST /Account/Login wrong password -> 200' "$status" 200
check_contains 'POST /Account/Login wrong password reports the failure' \
  "$work/login-bad-post.html" 'Invalid login attempt'
check_absent 'POST /Account/Login wrong password issues no cookie' \
  "$work/login-bad-post.head" 'Set-Cookie: .AspNet.ApplicationCookie='

status=$(get /Account/Login login)
check 'GET /Account/Login -> 200' "$status" 200
submit=$(submit_name "$work/login.html")
status=$(curl -sS -b "$jar" -c "$jar" -o "$work/login-post.html" -D "$work/login-post.head" \
  -w '%{http_code}' --max-time 300 -X POST "$BASE/Account/Login" \
  --data-urlencode "__VIEWSTATE=$(field "$work/login.html" __VIEWSTATE)" \
  --data-urlencode "__VIEWSTATEGENERATOR=$(field "$work/login.html" __VIEWSTATEGENERATOR)" \
  --data-urlencode "__EVENTVALIDATION=$(field "$work/login.html" __EVENTVALIDATION)" \
  --data-urlencode "ctl00\$MainContent\$Email=$EMAIL" \
  --data-urlencode "ctl00\$MainContent\$Password=$PASSWORD" \
  --data-urlencode "$submit=Log in")
check 'POST /Account/Login right password -> 302' "$status" 302
check 'POST /Account/Login right password -> Location /' "$(location "$work/login-post.head")" /
check_contains 'login sets .AspNet.ApplicationCookie' "$work/login-post.head" \
  'Set-Cookie: .AspNet.ApplicationCookie='

status=$(get /Account/Manage manage-again)
check 'GET /Account/Manage after login -> 200' "$status" 200
check_contains 'GET /Account/Manage after login greets the user' "$work/manage-again.html" \
  "Hello, $EMAIL"

echo
if [ "$failures" -eq 0 ]; then
  echo "smoke: all checks passed"
else
  echo "smoke: $failures check(s) failed"
fi
exit $((failures > 0))
