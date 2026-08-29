#!/usr/bin/env bash
# A demo-browsing journey against a running AjaxControlToolkitSampleSite.Host:
# the landing page and sitemap, representative extender demos, the .asmx
# ScriptService the AutoComplete extender calls, an UpdatePanel async postback,
# and the embedded toolkit script and stylesheet behind ScriptResource.axd and
# WebResource.axd. bash + curl only, so the same script runs on macOS, Linux,
# and Git bash on Windows.
#
#   apps/AjaxControlToolkitSampleSite/smoke.sh [base-url]

set -uo pipefail

BASE="${1:-http://127.0.0.1:5084}"

# ScriptManager gates partial rendering on browser capabilities; the default
# curl agent resolves to SupportsPartialRendering=false.
UA='Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36'

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
  curl -sS -o "$work/$name.html" -D "$work/$name.head" -w '%{http_code}' \
    --max-time 300 -A "$UA" "$@" "$BASE$path"
}

field() { # field <file> <name>
  grep -o "name=\"$2\"[^>]*value=\"[^\"]*\"" "$1" |
    sed 's/.*value="//; s/"$//' | head -1
}

status=$(get / home)
check 'GET / -> 200' "$status" 200
check_contains 'landing page renders its heading' "$work/home.html" 'ASP.NET AJAX Control Toolkit'
check_contains 'the master renders the sitemap menu' "$work/home.html" '/Accordion/Accordion.aspx'

status=$(get /Accordion/Accordion.aspx accordion)
check 'GET /Accordion/Accordion.aspx -> 200' "$status" 200
check_contains 'Accordion emits its extender behavior' "$work/accordion.html" 'Sys.Extended.UI.AccordionBehavior'
check_contains 'Accordion loads toolkit script over ScriptResource.axd' "$work/accordion.html" 'src="/ScriptResource.axd?d='
check_contains 'the master injects the App_Data control reference' "$work/accordion.html" 'data-control-type'

status=$(get /Calendar/Calendar.aspx calendar)
check 'GET /Calendar/Calendar.aspx -> 200' "$status" 200
check_contains 'Calendar emits its extender behavior' "$work/calendar.html" 'Sys.Extended.UI.CalendarBehavior'

status=$(get /ModalPopup/ModalPopup.aspx modalpopup)
check 'GET /ModalPopup/ModalPopup.aspx -> 200' "$status" 200
check_contains 'ModalPopup emits its extender behavior' "$work/modalpopup.html" 'Sys.Extended.UI.ModalPopupBehavior'

status=$(get /AutoComplete/AutoComplete.aspx autocomplete)
check 'GET /AutoComplete/AutoComplete.aspx -> 200' "$status" 200
check_contains 'AutoComplete points at its .asmx service' "$work/autocomplete.html" 'AutoComplete.asmx'

status=$(curl -sS -o "$work/asmx.json" -w '%{http_code}' --max-time 300 -A "$UA" \
  -H 'Content-Type: application/json; charset=utf-8' \
  -d '{"prefixText":"a","count":5,"contextKey":null}' \
  "$BASE/AutoComplete/AutoComplete.asmx/GetCompletionList")
check 'POST AutoComplete.asmx/GetCompletionList -> 200' "$status" 200
check_contains 'the ScriptService returns a JSON completion list' "$work/asmx.json" '{"d":["a'

status=$(get /ColorPicker/ColorPicker.aspx colorpicker)
check 'GET /ColorPicker/ColorPicker.aspx -> 200' "$status" 200
css=$(grep -o 'href="/WebResource.axd?[^"]*"' "$work/colorpicker.html" | sed 's/href="//; s/"$//; s/&amp;/\&/g' | head -1)
check_contains 'ColorPicker links its stylesheet over WebResource.axd' "$work/colorpicker.html" 'href="/WebResource.axd?d='
status=$(get "$css" colorcss)
check 'GET the WebResource.axd stylesheet -> 200' "$status" 200
check_contains 'the stylesheet carries the ColorPicker rules' "$work/colorcss.html" '.ajax__colorPicker_container'

script=$(grep -o 'src="/ScriptResource.axd?[^"]*"' "$work/accordion.html" | sed 's/src="//; s/"$//; s/&amp;/\&/g' | tail -1)
status=$(get "$script" toolkitjs)
check 'GET a toolkit ScriptResource.axd script -> 200' "$status" 200
check_contains 'the script is the embedded toolkit source' "$work/toolkitjs.html" 'Assembly:    AjaxControlToolkit'

status=$(get /ConfirmButton/ConfirmButton.aspx confirm)
check 'GET /ConfirmButton/ConfirmButton.aspx -> 200' "$status" 200
check_contains 'ConfirmButton initializes partial rendering' "$work/confirm.html" 'PageRequestManager._initialize'
sm=$(grep -o "PageRequestManager\._initialize('[^']*'" "$work/confirm.html" | sed "s/.*_initialize('//; s/'$//")
panel=$(grep -o "_initialize('[^']*', '[^']*', \['t[^']*'" "$work/confirm.html" | sed "s/.*\['t//; s/'$//")
button=$(grep -o 'name="[^"]*\$Button"' "$work/confirm.html" | sed 's/name="//; s/"$//' | head -1)
status=$(curl -sS -o "$work/delta.txt" -w '%{http_code}' --max-time 300 -A "$UA" \
  -H 'X-MicrosoftAjax: Delta=true' \
  --data-urlencode "$sm=$panel|$button" \
  --data-urlencode "__VIEWSTATE=$(field "$work/confirm.html" __VIEWSTATE)" \
  --data-urlencode "__VIEWSTATEGENERATOR=$(field "$work/confirm.html" __VIEWSTATEGENERATOR)" \
  --data-urlencode "__EVENTVALIDATION=$(field "$work/confirm.html" __EVENTVALIDATION)" \
  --data-urlencode "__EVENTTARGET=" --data-urlencode "__EVENTARGUMENT=" \
  --data-urlencode "__ASYNCPOST=true" --data-urlencode "$button=Click Me" \
  "$BASE/ConfirmButton/ConfirmButton.aspx")
check 'POST ConfirmButton async postback -> 200' "$status" 200
check_contains 'the response is an updatePanel delta' "$work/delta.txt" '|updatePanel|'
check_contains 'the delta re-renders the panel content' "$work/delta.txt" 'Content_DemoContent_Button'

if [ "$failures" -eq 0 ]; then
  echo; echo 'smoke: all checks passed'
else
  echo; echo "smoke: $failures check(s) failed"; exit 1
fi
