#!/usr/bin/env bash
# One Linux round for an app under apps/: build its App/Host pair and run its smoke.sh
# against the running host, inside a container. Like eng/linux-round.sh it validates
# the committed HEAD in a persistent workspace volume.
#
#   eng/app-linux-smoke.sh WebFormsApplication 5081
#   eng/app-linux-smoke.sh WebFormsIdentityApplication 5082
set -euo pipefail

APP=${1:?app folder under apps/}
PORT=${2:?host port}
SDK_VERSION=$(python3 -c 'import json; print(json.load(open("global.json"))["sdk"]["version"])')
ARCH=$(docker version --format '{{.Server.Arch}}')

TTY=""
[ -t 1 ] && TTY="-t"

# SMOKE_DOCKER_ARGS: extra docker-run arguments, e.g. --network container:<sql>
# for an app whose smoke needs a database container reachable at 127.0.0.1.
# shellcheck disable=SC2086 # $TTY and $SMOKE_DOCKER_ARGS are word-split flags
exec docker run --rm $TTY ${SMOKE_DOCKER_ARGS:-} \
  -v "$PWD:/src:ro" \
  -v "rehost-linux-$ARCH-work:/work" \
  -v "rehost-linux-$ARCH-nuget:/root/.nuget" \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -e APP="$APP" -e PORT="$PORT" \
  "mcr.microsoft.com/dotnet/sdk:$SDK_VERSION" \
  bash -ec '
    # linux-round.sh chowns /work to its non-root user; this script stays root
    # (it runs no tests, so the run-as-app rationale does not apply) and must
    # trust the app-owned clone and /src, whose mount ownership varies by
    # Docker file-sharing mode and resolves as /src/.git for fetch remotes.
    git config --global --add safe.directory "*"
    if [ ! -d /work/repo/.git ]; then
      git clone -q /src /work/repo
    fi
    cd /work/repo
    git fetch -q /src HEAD
    git checkout -q -f FETCH_HEAD
    git clean -qfd
    dotnet build "apps/$APP/$APP.slnx"
    dotnet run --project "apps/$APP/$APP.Host" --no-build &
    host=$!
    trap "kill $host" EXIT
    for _ in $(seq 1 60); do
      curl -fsS -o /dev/null "http://127.0.0.1:$PORT/" && break
      sleep 2
    done
    "apps/$APP/smoke.sh"
  '
