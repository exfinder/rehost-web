#!/usr/bin/env bash
# One Linux validation round: build and test the solution inside a Linux container.
# Mirrors the Windows-round ritual: the committed HEAD is fetched into a persistent
# workspace volume (obj/, bin/, and the NuGet cache stay warm between rounds;
# git clean -fd, never -x). Run from the repository root; requires a running Docker
# daemon. Uncommitted changes are not validated.
#
# The container runs the daemon's native architecture: an emulated round costs
# minutes where a native one costs seconds, and Linux defects to date have been
# OS-level, not architectural. The image tag matches the global.json SDK pin,
# which rolls forward to nothing.
set -euo pipefail

SDK_VERSION=$(python3 -c 'import json; print(json.load(open("global.json"))["sdk"]["version"])')
ARCH=$(docker version --format '{{.Server.Arch}}')

exec docker run --rm \
  -v "$PWD:/src:ro" \
  -v "rehost-linux-$ARCH-work:/work" \
  -v "rehost-linux-$ARCH-nuget:/root/.nuget" \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  "mcr.microsoft.com/dotnet/sdk:$SDK_VERSION" \
  bash -ec '
    set -o pipefail
    if [ ! -d /work/repo/.git ]; then
      git clone -q /src /work/repo
    fi
    cd /work/repo
    git fetch -q /src HEAD
    git checkout -q -f FETCH_HEAD
    git clean -qfd
    dotnet build Rehost.WebForms.slnx -v q 2>&1 | tail -3
    dotnet test Rehost.WebForms.slnx --no-build
  '
