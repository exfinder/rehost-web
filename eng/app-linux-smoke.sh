#!/usr/bin/env bash
# One Linux round for an app under apps/: build its App/Host pair and run its smoke.sh
# against the running host, inside a container. Like eng/linux-round.sh it validates
# the committed HEAD in a persistent workspace volume.
#
#   eng/app-linux-smoke.sh WebFormsApplication 5081
#   eng/app-linux-smoke.sh WebFormsIdentityApplication 5082
#
# WebFormsIdentityApplication needs SQL Server; the container then joins the SQL
# container's network namespace (SQL_CONTAINER, default rehost-identity-sql-linux), so
# the app reaches the server at the same 127.0.0.1,14333 the committed
# Web.Rehost.config names and no second connection string exists. Start it first:
#
#   docker run -d --name rehost-identity-sql-linux -e ACCEPT_EULA=Y \
#     -e MSSQL_SA_PASSWORD='Rehost!Dev2026' -e MSSQL_TCP_PORT=14333 \
#     mcr.microsoft.com/mssql/server:2022-latest
set -euo pipefail

APP=${1:?app folder under apps/}
PORT=${2:?host port}
SDK_VERSION=$(python3 -c 'import json; print(json.load(open("global.json"))["sdk"]["version"])')
ARCH=$(docker version --format '{{.Server.Arch}}')

NETWORK=()
if [ "$APP" = WebFormsIdentityApplication ]; then
  NETWORK=(--network "container:${SQL_CONTAINER:-rehost-identity-sql-linux}")
fi

TTY=""
[ -t 1 ] && TTY="-t"

# shellcheck disable=SC2086 # $TTY is empty or a single flag
exec docker run --rm $TTY \
  "${NETWORK[@]}" \
  -v "$PWD:/src:ro" \
  -v "rehost-linux-$ARCH-work:/work" \
  -v "rehost-linux-$ARCH-nuget:/root/.nuget" \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -e APP="$APP" -e PORT="$PORT" \
  "mcr.microsoft.com/dotnet/sdk:$SDK_VERSION" \
  bash -ec '
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
