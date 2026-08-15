#!/usr/bin/env bash
# One Linux round for apps/WebFormsIdentityApplication: build the app pair and run
# smoke.sh against it, inside a container. Like eng/linux-round.sh it validates the
# committed HEAD in a persistent workspace volume.
#
# The container joins the SQL container's network namespace, so the app reaches the
# server at the same 127.0.0.1,14333 the committed Web.Rehost.config names and no
# second connection string exists. Start that server first:
#
#   docker run -d --name rehost-identity-sql-linux -e ACCEPT_EULA=Y \
#     -e MSSQL_SA_PASSWORD='Rehost!Dev2026' -e MSSQL_TCP_PORT=14333 \
#     mcr.microsoft.com/mssql/server:2022-latest
set -euo pipefail

SDK_VERSION=$(python3 -c 'import json; print(json.load(open("global.json"))["sdk"]["version"])')
ARCH=$(docker version --format '{{.Server.Arch}}')
SQL_CONTAINER=${SQL_CONTAINER:-rehost-identity-sql-linux}

TTY=""
[ -t 1 ] && TTY="-t"

# shellcheck disable=SC2086 # $TTY is empty or a single flag
exec docker run --rm $TTY \
  --network "container:$SQL_CONTAINER" \
  -v "$PWD:/src:ro" \
  -v "rehost-linux-$ARCH-work:/work" \
  -v "rehost-linux-$ARCH-nuget:/root/.nuget" \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  "mcr.microsoft.com/dotnet/sdk:$SDK_VERSION" \
  bash -ec '
    if [ ! -d /work/repo/.git ]; then
      git clone -q /src /work/repo
    fi
    cd /work/repo
    git fetch -q /src HEAD
    git checkout -q -f FETCH_HEAD
    git clean -qfd
    dotnet build apps/WebFormsIdentityApplication/WebFormsIdentityApplication.slnx
    dotnet run --project apps/WebFormsIdentityApplication/WebFormsIdentityApplication.Host --no-build &
    host=$!
    trap "kill $host" EXIT
    for _ in $(seq 1 60); do
      curl -fsS -o /dev/null http://127.0.0.1:5082/ && break
      sleep 2
    done
    apps/WebFormsIdentityApplication/smoke.sh
  '
