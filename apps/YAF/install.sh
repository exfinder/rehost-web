#!/usr/bin/env bash
# Drive YAF's own install wizard against an empty database, so a fresh SQL Server
# reaches the board state smoke.sh expects. A deployment step, not part of the
# journey: it is idempotent only in the sense that it refuses a board that exists.
#
#   apps/YAF/install.sh [base-url]

set -uo pipefail

BASE="${1:-http://127.0.0.1:5087}"
BASE="${BASE%/}"
BOARD='Rehost Test Forum'
CONNECTION='yafnet'
ADMIN_USER='hostadmin'
ADMIN_EMAIL='hostadmin@rehost.test'
FORUM_EMAIL='forum@rehost.test'
PASSWORD='Rehost!Dev2026'
UA='Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'

work="${YAF_INSTALL_WORK:-$(mktemp -d)}"
mkdir -p "$work"
[ -n "${YAF_INSTALL_WORK:-}" ] || trap 'rm -rf "$work"' EXIT
jar="$work/cookies.txt"
: > "$jar"

field() { tr '<' '\n' < "$1" | grep "name=\"$2\"" | sed 's/.*value="//; s/".*//' | head -1; }

step() { # step <in-html> <target> <out-html> <extra curl args...>
  local page="$1" target="$2" out="$3"; shift 3
  curl -sS --max-time 300 -A "$UA" -b "$jar" -c "$jar" -o "$out" -w '%{http_code}' -L \
    --data-urlencode "__VIEWSTATE=$(field "$page" __VIEWSTATE)" \
    --data-urlencode "__VIEWSTATEGENERATOR=$(field "$page" __VIEWSTATEGENERATOR)" \
    --data-urlencode "__EVENTVALIDATION=$(field "$page" __EVENTVALIDATION)" \
    --data-urlencode "__EVENTTARGET=$target" \
    --data-urlencode "__EVENTARGUMENT=" "$@" "$BASE/install/default.aspx"
}

die() { printf 'install: %s\n' "$1" >&2; exit 1; }

code=$(curl -sS --max-time 120 -A "$UA" -b "$jar" -c "$jar" -o "$work/0.html" -w '%{http_code}' -L "$BASE/install/default.aspx")
[ "$code" = 200 ] || die "installer answered $code"
grep -qF 'InstallWizard' "$work/0.html" || die 'no install wizard at /install/default.aspx'

code=$(step "$work/0.html" 'InstallWizard$StartNavigationTemplateContainerID$StartNextButton' "$work/1.html")
[ "$code" = 200 ] || die "welcome step answered $code"

# permissions, connection, test settings, then the migrations
n=1
while [ "$n" -le 4 ]; do
  # only the connection step rendered the listbox, and event validation
  # refuses a control the page it is posted to never rendered
  if grep -qF 'InstallWizard$lbConnections' "$work/$n.html"; then
    code=$(step "$work/$n.html" 'InstallWizard$StepNavigationTemplateContainerID$StepNextButton' "$work/$((n + 1)).html" \
      --data-urlencode "InstallWizard\$lbConnections=$CONNECTION")
  else
    code=$(step "$work/$n.html" 'InstallWizard$StepNavigationTemplateContainerID$StepNextButton' "$work/$((n + 1)).html")
  fi
  [ "$code" = 200 ] || die "step $n answered $code"
  n=$((n + 1))
done

grep -qF 'InstallWizard_TheForumName' "$work/5.html" || die 'the wizard did not reach the board form'

code=$(step "$work/5.html" 'InstallWizard$StepNavigationTemplateContainerID$StepNextButton' "$work/6.html" \
  --data-urlencode "InstallWizard\$TheForumName=$BOARD" \
  --data-urlencode "InstallWizard\$ForumEmailAddress=$FORUM_EMAIL" \
  --data-urlencode "InstallWizard\$ForumBaseUrlMask=$BASE/" \
  --data-urlencode "InstallWizard\$UserName=$ADMIN_USER" \
  --data-urlencode "InstallWizard\$AdminEmail=$ADMIN_EMAIL" \
  --data-urlencode "InstallWizard\$Password1=$PASSWORD" \
  --data-urlencode "InstallWizard\$Password2=$PASSWORD")
[ "$code" = 200 ] || die "board creation answered $code"
grep -qF 'Setup Finished' "$work/6.html" || die "board creation did not finish: $(grep -oE 'alert[^>]*>[^<]{0,160}' "$work/6.html" | head -1)"

code=$(step "$work/6.html" 'InstallWizard$FinishNavigationTemplateContainerID$FinishButton' "$work/7.html")
[ "$code" = 200 ] || die "finish answered $code"
grep -qF "$BOARD" "$work/7.html" || die 'the board did not render after finishing'

printf 'installed %s at %s\n' "$BOARD" "$BASE"
