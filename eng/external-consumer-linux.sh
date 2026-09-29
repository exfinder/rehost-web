#!/usr/bin/env bash
# The external-consumer proof inside a Linux container: fetch the committed HEAD, pack
# the candidate feed there, then run eng/external-consumer.sh against it. The pack step
# keeps a warm NuGet cache in a volume; the rig itself always restores into an empty one.
#
#   eng/external-consumer-linux.sh                       # the daemon's native architecture
#   PLATFORM=linux/amd64 eng/external-consumer-linux.sh  # emulated x64 on an arm64 daemon
#
# REHOST_ROUND_TAG suffixes the volumes, as in linux-round.sh.
set -euo pipefail

SDK_VERSION=$(python3 -c 'import json; print(json.load(open("global.json"))["sdk"]["version"])')
PLATFORM=${PLATFORM:-}
ARCH=${PLATFORM##*/}
[ -n "$ARCH" ] || ARCH=$(docker version --format '{{.Server.Arch}}')
VOL="rehost-consumer-$ARCH${REHOST_ROUND_TAG:+-$REHOST_ROUND_TAG}"

TTY=""
[ -t 1 ] && TTY="-t"

# shellcheck disable=SC2086 # $TTY is empty or a single flag
exec docker run --rm $TTY ${PLATFORM:+--platform "$PLATFORM"} \
  -v "$PWD:/src:ro" \
  -v "$VOL-work:/work" \
  -v "$VOL-nuget:/root/.nuget" \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  "mcr.microsoft.com/dotnet/sdk:$SDK_VERSION" \
  bash -ec '
    git config --global --add safe.directory "*"
    if [ ! -d /work/repo/.git ]; then
      git clone -q /src /work/repo
    fi
    cd /work/repo
    git fetch -q /src HEAD
    git checkout -q -f FETCH_HEAD
    git clean -qfd
    echo "HEAD $(git rev-parse --short HEAD) on $(uname -m), $(. /etc/os-release && echo "$PRETTY_NAME")"
    rm -rf /work/candidate
    dotnet pack Rehost.Web.slnx -c Release -o /work/candidate -v q
    eng/external-consumer.sh /work/candidate
  '
