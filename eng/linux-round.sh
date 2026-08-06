#!/usr/bin/env bash
# One Linux validation round: build and test the solution inside a Linux container,
# the deployment-target OS. Mirrors the Windows-round ritual: the committed HEAD is
# fetched into a persistent workspace volume (obj/, bin/, and the NuGet cache stay
# warm between rounds; git clean -fd, never -x). Run from the repository root;
# requires a running Docker daemon. Uncommitted changes are not validated.
set -euo pipefail

exec docker run --rm \
  -v "$PWD:/src:ro" \
  -v rehost-linux-work:/work \
  -v rehost-linux-nuget:/root/.nuget \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  bash -ec '
    if [ ! -d /work/repo/.git ]; then
      git clone -q /src /work/repo
    fi
    cd /work/repo
    git fetch -q /src HEAD
    git checkout -q -f FETCH_HEAD
    git clean -qfd
    dotnet build Rehost.WebForms.slnx -v q 2>&1 | tail -3
    for p in tests/Rehost.WebForms.Runtime.Tests/Rehost.WebForms.Runtime.Tests.csproj \
             tests/Rehost.WebForms.Hosting.Tests/Rehost.WebForms.Hosting.Tests.csproj \
             tests/Rehost.WebForms.WebServices.Tests/Rehost.WebForms.WebServices.Tests.csproj; do
      dotnet test "$p" --no-build
    done
  '
