#!/usr/bin/env bash
# Prove the candidate packages work for a stranger: copy the stock WAP App/Host pair out
# of the checkout into a fresh folder, restore it with an empty package cache from the
# candidate feed plus nuget.org only, build, run the smoke journey, publish, and list
# what publish produced. bash + curl + dotnet; runs on macOS, Linux, and Git bash.
#
#   eng/external-consumer.sh <candidate-feed-dir> [version] [port]
#
# Set KEEP=1 to keep the work folder for inspection.
set -euo pipefail

FEED=$(cd "${1:?candidate feed directory}" && pwd)
VERSION=${2:-0.1.0-alpha.1}
PORT=${3:-5181}
REPO=$(cd "$(dirname "$0")/.." && pwd)
WORK=$(mktemp -d "${TMPDIR:-/tmp}/rehost-consumer.XXXXXX")
APP="$WORK/app"
BASE="http://127.0.0.1:$PORT"

host=""
cleanup() {
  local status=$?
  if [ -n "$host" ]; then
    kill "$host" 2>/dev/null || true
    wait "$host" 2>/dev/null || true
  fi
  if [ "${KEEP:-0}" = 1 ]; then echo "work folder kept: $WORK"; else rm -rf "$WORK"; fi
  exit "$status"
}
trap cleanup EXIT

# Git bash hands POSIX paths to a Windows dotnet; NuGet.config needs the native spelling.
native() { if command -v cygpath >/dev/null 2>&1; then cygpath -w "$1"; else printf '%s' "$1"; fi; }

mkdir -p "$APP"
for item in WebFormsApplication WebFormsApplication.App WebFormsApplication.Host WebFormsApplication.slnx smoke.sh; do
  cp -R "$REPO/apps/WebFormsApplication/$item" "$APP/"
done
rm -rf "$APP"/*/bin "$APP"/*/obj
template=$(<"$REPO/eng/external-consumer/NuGet.config")
feed_native=$(native "$FEED")
printf '%s\n' "${template//@FEED@/$feed_native}" > "$APP/NuGet.config"
cp "$REPO/eng/external-consumer/global.json" "$APP/"

export NUGET_PACKAGES="$WORK/nuget/packages"
export NUGET_HTTP_CACHE_PATH="$WORK/nuget/http-cache"
export NUGET_PLUGINS_CACHE_PATH="$WORK/nuget/plugins-cache"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
mkdir -p "$NUGET_PACKAGES" "$NUGET_HTTP_CACHE_PATH" "$NUGET_PLUGINS_CACHE_PATH"

cd "$APP"
echo "== toolchain"
dotnet --version
dotnet --info | grep -E 'RID:|OS Name:|OS Version:|OS Platform:' | sed 's/^ *//'
dotnet --list-runtimes | grep -E '^Microsoft\.(NETCore|AspNetCore)\.App 10\.' | sed 's/ \[.*//'
echo "== candidate feed: $FEED"
ls "$FEED" | grep -E '\.nupkg$' | grep -v snupkg

echo "== restore + build (Release)"
dotnet build WebFormsApplication.slnx -c Release -p:RehostWebFormsVersion="$VERSION" -v q

echo "== restored Rehost packages (expect the seven public IDs, no components)"
ls "$NUGET_PACKAGES" | grep -i '^rehost' | sort
if ls "$NUGET_PACKAGES" | grep -qi -E '^rehost\.webforms\.(runtime|applicationservices|extensions|webservices)$'; then
  echo "FAIL  an unpublished component package was restored"; exit 1
fi

site="WebFormsApplication.Host/bin/site"
echo "== staged site"
ls "$site/bin" | grep -E '^Rehost\.WebForms.*\.dll$'
ls "$site/bin/configs"
grep -q '<runtime' "$site/web.config" && { echo "FAIL  XDT did not remove <runtime>"; exit 1; }
grep -q 'assembly="Rehost.WebForms.Optimization.WebForms"' "$site/web.config" || { echo "FAIL  XDT did not rewrite the Optimization controls assembly"; exit 1; }
echo "PASS  staged web.config carries the XDT result"

echo "== run"
dotnet "$site/bin/WebFormsApplication.Host.dll" "$BASE" > "$WORK/host.log" 2>&1 &
host=$!
for _ in $(seq 1 60); do
  curl -fsS -o /dev/null "$BASE/" 2>/dev/null && break
  kill -0 "$host" 2>/dev/null || { echo "FAIL  host exited"; cat "$WORK/host.log"; exit 1; }
  sleep 2
done
./smoke.sh "$BASE"

echo "== publish (Release)"
dotnet publish WebFormsApplication.Host -c Release -p:RehostWebFormsVersion="$VERSION" -v q
publish="WebFormsApplication.Host/bin/Release/net10.0/site-publish"
echo "publish root: $(ls "$publish" | tr '\n' ' ')"
echo "publish bin files: $(find "$publish/bin" -type f | wc -l | tr -d ' ')"
echo "publish content files: $(find "$publish" -path "$publish/bin" -prune -o -type f -print | wc -l | tr -d ' ')"
grep -q 'debug="true"' "$publish/web.config" && { echo "FAIL  publish did not apply Web.Release.config"; exit 1; }
echo "PASS  published web.config carries the Release transform"
echo "== done"
