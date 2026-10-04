#!/usr/bin/env bash
# The catalog manager's journey against a running eShopLegacyMVC.Host: the MVC
# catalog with its layout and mock data, the Details/Create/Delete views, an Edit
# post through the antiforgery token and model validation, the attribute-routed
# picture action, the Web API brands controller, and the Views folder blocked.
# bash + curl only, so the same script runs on macOS, Linux, and Git bash on Windows.
#
#   apps/eShopLegacyMVC/smoke.sh [base-url]

set -uo pipefail

BASE="${1:-http://127.0.0.1:5088}"

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

get() { # get <path> <name> [curl args...]
  local path=$1 name=$2
  shift 2
  curl -sS -b "$jar" -c "$jar" -o "$work/$name.html" -D "$work/$name.head" \
    -w '%{http_code}' --max-time 300 "$@" "$BASE$path"
}

header() { grep -i "^$1:" "$2" | head -1 | tr -d '\r' | sed 's/^[^:]*: *//'; }

form_action() { sed -n 's/.*<form action="\([^"]*\)".*/\1/p' "$1" | head -1; }

token() {
  sed -n 's/.*name="__RequestVerificationToken" type="hidden" value="\([^"]*\)".*/\1/p' "$1" | head -1
}

post_edit() { # post_edit <action> <name> <item name> <price>
  local action=$1 name=$2
  curl -sS -b "$jar" -c "$jar" -o "$work/$name.html" -D "$work/$name.head" \
    -w '%{http_code}' --max-time 300 \
    --data-urlencode "__RequestVerificationToken=$TOKEN" \
    --data-urlencode 'Id=2' \
    --data-urlencode "Name=$3" \
    --data-urlencode 'Description=.NET Black & White Mug' \
    --data-urlencode 'CatalogBrandId=2' \
    --data-urlencode 'CatalogTypeId=1' \
    --data-urlencode "Price=$4" \
    --data-urlencode 'PictureFileName=2.png' \
    --data-urlencode 'AvailableStock=100' \
    --data-urlencode 'RestockThreshold=0' \
    --data-urlencode 'MaxStockThreshold=0' \
    "$BASE$action"
}

status=$(get / home)
check 'GET / -> 200 (default route to Catalog/Index)' "$status" 200
check 'GET / reports MVC 5.3' "$(header X-AspNetMvc-Version "$work/home.head")" 5.3
check_contains 'GET / renders through _Layout' "$work/home.html" '<title>Index - Catalog manager (MVC)</title>'
check_contains 'GET / lists mock catalog data' "$work/home.html" '.NET Bot Black Hoodie'
check_contains 'GET / links pictures through the attribute route' "$work/home.html" '/items/1/pic'
check_contains 'GET / renders the style bundle' "$work/home.html" 'href="/Content/custom.css"'

status=$(get /Catalog/Details/1 details)
check 'GET /Catalog/Details/1 -> 200' "$status" 200
check_contains 'Details renders the product' "$work/details.html" '.NET Bot Black Hoodie'
check_contains 'Details links Edit' "$work/details.html" '/Catalog/Edit/1'

status=$(get /Catalog/Create create)
check 'GET /Catalog/Create -> 200' "$status" 200
check_contains 'Create renders the brand list' "$work/create.html" '<option value="4">SQL Server</option>'

status=$(get /Catalog/Delete/1 delete)
check 'GET /Catalog/Delete/1 -> 200' "$status" 200
check_contains 'Delete renders the product' "$work/delete.html" '.NET Bot Black Hoodie'

status=$(get /Catalog/Edit/2 edit)
check 'GET /Catalog/Edit/2 -> 200' "$status" 200
check_contains 'Edit binds the product name' "$work/edit.html" 'value=".NET Black &amp; White Mug"'
ACTION=$(form_action "$work/edit.html")
TOKEN=$(token "$work/edit.html")
check 'Edit posts to its own route' "$ACTION" /Catalog/Edit/2
check 'Edit renders an antiforgery token' "$([ -n "$TOKEN" ] && echo present)" present

status=$(curl -sS -o "$work/edit-forged.html" -w '%{http_code}' --max-time 300 \
  --data-urlencode 'Id=2' --data-urlencode 'Name=Forged' "$BASE$ACTION")
check 'POST Edit without the antiforgery pair -> 500' "$status" 500
check_contains 'the rejection names the antiforgery check' "$work/edit-forged.html" \
  'anti-forgery cookie &quot;__RequestVerificationToken&quot; is not present'

status=$(post_edit "$ACTION" edit-invalid 'Rehost Mug' '-1')
check 'POST Edit with a negative price -> 200 (redisplayed)' "$status" 200
check_contains 'the redisplayed form carries the range message' "$work/edit-invalid.html" \
  'The field Price must be between 0 and 1000000.'

status=$(post_edit "$ACTION" edit-post 'Rehost Mug' '9.25')
check 'POST Edit -> 302' "$status" 302
check 'POST Edit redirects to the catalog' "$(header Location "$work/edit-post.head")" /

status=$(get /Catalog/Details/2 details-2)
check 'GET /Catalog/Details/2 -> 200' "$status" 200
check_contains 'Details shows the posted name' "$work/details-2.html" 'Rehost Mug'
check_contains 'Details shows the posted price' "$work/details-2.html" '9.25'

status=$(get /items/1/pic pic)
check 'GET /items/1/pic -> 200 (attribute route)' "$status" 200
check 'the picture is a PNG' "$(header Content-Type "$work/pic.head")" image/png

status=$(get /api/brands brands -H 'Accept: application/json')
check 'GET /api/brands -> 200 (Web API)' "$status" 200
check 'the brands are JSON' "$(header Content-Type "$work/brands.head")" 'application/json; charset=utf-8'
check_contains 'the brands list the mock data' "$work/brands.html" '{"Id":2,"Brand":".NET"}'

status=$(get /api/brands/4 brand)
check 'GET /api/brands/4 -> 200' "$status" 200
check_contains 'the brand is JSON' "$work/brand.html" '{"Id":4,"Brand":"SQL Server"}'

status=$(get /api/brands/99 brand-missing)
check 'GET /api/brands/99 -> 404' "$status" 404

status=$(get /Views/Catalog/Index.cshtml view-file)
check 'GET a view file -> 404 (Views/Web.config BlockViewHandler)' "$status" 404

if [ "$failures" -eq 0 ]; then
  echo; echo 'smoke: all checks passed'
else
  echo; echo "smoke: $failures check(s) failed"; exit 1
fi
