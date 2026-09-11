#!/usr/bin/env bash
# The stock template's browser journey against a running WebFormsApplication.Host:
# default document, pages, Friendly URL redirect, bundles, static assets, a postback to
# the server form, and mobile view switching. bash + curl only, so the same
# script runs on macOS, Linux, and Git bash on Windows.
#
#   apps/WebFormsApplication/smoke.sh [base-url]

set -uo pipefail

BASE="${1:-http://127.0.0.1:5081}"
MOBILE_UA='Mozilla/5.0 (iPhone; CPU iPhone OS 15_0 like Mac OS X) AppleWebKit/605.1.15 Mobile/15E148 Safari/604.1'

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
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

location() { grep -i '^location:' "$1" | tr -d '\r' | sed 's/^[Ll]ocation: *//'; }

get() { # get <path> <name> [curl args...]
  local path=$1 name=$2
  shift 2
  curl -sS -o "$work/$name.html" -D "$work/$name.head" -w '%{http_code}' --max-time 300 "$@" "$BASE$path"
}

status=$(get / home)
check 'GET / -> 200 (default document)' "$status" 200
check_contains 'GET / renders the template home page' "$work/home.html" 'Getting started'
action=$(sed -n 's/.*<form[^>]*action="\([^"]*\)".*/\1/p' "$work/home.html" | head -1)
check 'form on / posts back to ./' "$action" ./

hidden() { sed -n "s/.*id=\"$1\" value=\"\([^\"]*\)\".*/\1/p" "$work/home.html" | head -1; }
status=$(curl -sS -o "$work/postback.html" -w '%{http_code}' --max-time 300 \
  --data-urlencode "__VIEWSTATE=$(hidden __VIEWSTATE)" \
  --data-urlencode "__VIEWSTATEGENERATOR=$(hidden __VIEWSTATEGENERATOR)" \
  --data-urlencode "__EVENTVALIDATION=$(hidden __EVENTVALIDATION)" \
  "$BASE/")
check 'POST / with the rendered hidden fields -> 200 (postback)' "$status" 200
check_contains 'postback re-renders the template home page' "$work/postback.html" 'Getting started'

status=$(get /About about)
check 'GET /About -> 200 (Friendly URL)' "$status" 200
check_contains 'GET /About renders the About page' "$work/about.html" 'Your application description page'

status=$(get /Contact contact)
check 'GET /Contact -> 200 (Friendly URL)' "$status" 200
check_contains 'GET /Contact renders the Contact page' "$work/contact.html" 'Your contact page'

status=$(get /Default.aspx physical)
check 'GET /Default.aspx -> 301 to the Friendly URL' "$status" 301
check 'GET /Default.aspx -> Location /Default' "$(location "$work/physical.head")" /Default

status=$(get /Content/Site.css css)
check 'GET /Content/Site.css -> 200 (static asset)' "$status" 200

bundle=$(grep -o 'src="/bundles/WebFormsJs?v=[^"]*"' "$work/home.html" | head -1 | sed 's/src="//; s/"$//')
check_contains 'home references the WebFormsJs bundle' "$work/home.html" 'src="/bundles/WebFormsJs?v='
status=$(get "$bundle" bundle)
check 'GET the WebFormsJs bundle -> 200' "$status" 200
check_contains 'the bundle carries WebForms.js' "$work/bundle.html" '__doPostBack'

status=$(get /Content/css cssbundle)
check 'GET /Content/css style bundle -> 200' "$status" 200

status=$(get / mobile -A "$MOBILE_UA")
check 'GET / with a mobile UA -> 200' "$status" 200
check_contains 'mobile UA renders the mobile master' "$work/mobile.html" 'Mobile Master Page'
check_contains 'mobile master offers the view switcher' "$work/mobile.html" '__FriendlyUrls_SwitchView?ReturnUrl='

status=$(get '/__FriendlyUrls_SwitchView/Mobile?ReturnUrl=%2F' switch)
check 'switch view to Mobile -> 302' "$status" 302
check 'switch view -> Location /' "$(location "$work/switch.head")" /
check_contains 'switch view sets FriendlyUrlsViewSwitcher=Mobile' "$work/switch.head" 'FriendlyUrlsViewSwitcher=Mobile'

status=$(get / switched -b 'FriendlyUrlsViewSwitcher=Mobile')
check 'GET / with the switch cookie -> 200' "$status" 200
check_contains 'switch cookie renders the mobile master on a desktop UA' "$work/switched.html" 'Mobile Master Page'

if [ "$failures" -eq 0 ]; then
  echo; echo 'smoke: all checks passed'
else
  echo; echo "smoke: $failures check(s) failed"; exit 1
fi
