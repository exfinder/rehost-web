#!/usr/bin/env bash
# The catalog manager's browsing journey against a running eShopLegacyWebForms.Host:
# the routed home page with mock catalog data, the Catalog CRUD pages over classic
# MapPageRoute URLs, static pictures, and the script bundles. bash + curl only, so
# the same script runs on macOS, Linux, and Git bash on Windows.
#
#   apps/eShopLegacyWebForms/smoke.sh [base-url]

set -uo pipefail

BASE="${1:-http://127.0.0.1:5083}"

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

get() { # get <path> <name> [curl args...]
  local path=$1 name=$2
  shift 2
  curl -sS -o "$work/$name.html" -D "$work/$name.head" -w '%{http_code}' --max-time 300 "$@" "$BASE$path"
}

status=$(get / home)
check 'GET / -> 200 (routed default)' "$status" 200
check_contains 'GET / renders the catalog manager' "$work/home.html" 'Catalog manager (Web Forms)'
check_contains 'GET / lists mock catalog data' "$work/home.html" '.NET Bot Black Hoodie'
check_contains 'GET / renders the pager' "$work/home.html" 'esh-pager'

status=$(get /Catalog/Create create)
check 'GET /Catalog/Create -> 200 (MapPageRoute)' "$status" 200
check_contains 'Create renders its form' "$work/create.html" 'esh-button'

status=$(get /Catalog/Details/1 details)
check 'GET /Catalog/Details/1 -> 200' "$status" 200
check_contains 'Details renders the product' "$work/details.html" '.NET Bot Black Hoodie'
check_contains 'Details links Edit via GetRouteUrl' "$work/details.html" '/Catalog/Edit/1'

status=$(get /Catalog/Edit/1 edit)
check 'GET /Catalog/Edit/1 -> 200' "$status" 200
check_contains 'Edit binds the product name' "$work/edit.html" '.NET Bot Black Hoodie'

status=$(get /Catalog/Delete/1 delete)
check 'GET /Catalog/Delete/1 -> 200' "$status" 200
check_contains 'Delete renders the product' "$work/delete.html" '.NET Bot Black Hoodie'

status=$(get /About.aspx about)
check 'GET /About.aspx -> 200' "$status" 200

status=$(get /Contact.aspx contact)
check 'GET /Contact.aspx -> 200' "$status" 200

status=$(get /Pics/1.png pic)
check 'GET /Pics/1.png -> 200 (static asset)' "$status" 200

bundle=$(grep -o 'src="/bundles/MsAjaxJs?v=[^"]*"' "$work/home.html" | head -1 | sed 's/src="//; s/"$//')
check_contains 'home references the MsAjaxJs bundle' "$work/home.html" 'src="/bundles/MsAjaxJs?v='
status=$(get "$bundle" msajax)
check 'GET the MsAjaxJs bundle -> 200' "$status" 200
check_contains 'the bundle carries MicrosoftAjax' "$work/msajax.html" 'Sys.Application'

bundle=$(grep -o 'src="/bundles/WebFormsJs?v=[^"]*"' "$work/home.html" | head -1 | sed 's/src="//; s/"$//')
status=$(get "$bundle" webforms)
check 'GET the WebFormsJs bundle -> 200' "$status" 200
check_contains 'the bundle carries WebForms.js' "$work/webforms.html" '__doPostBack'

if [ "$failures" -eq 0 ]; then
  echo; echo 'smoke: all checks passed'
else
  echo; echo "smoke: $failures check(s) failed"; exit 1
fi
