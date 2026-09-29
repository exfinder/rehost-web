#!/usr/bin/env bash
# One Linux validation round: build and test the solution inside a Linux container.
# Mirrors the Windows-round ritual: the committed HEAD is fetched into a persistent
# workspace volume (obj/, bin/, and the NuGet cache stay warm between rounds;
# git clean -fd, never -x). Run from the repository root; requires a running Docker
# daemon. Uncommitted changes are not validated.
#
# The round runs as the image's non-root `app` user: a privileged process ignores
# Unix mode bits, so the permission-denied coverage would skip itself as root. The
# volumes are created root-owned, hence the one-time chown before the handoff.
#
# The container runs the daemon's native architecture: an emulated round costs
# minutes where a native one costs seconds, and Linux defects to date have been
# OS-level, not architectural. The image tag matches the global.json SDK pin,
# which rolls forward to nothing.
set -euo pipefail

SDK_VERSION=$(python3 -c 'import json; print(json.load(open("global.json"))["sdk"]["version"])')
ARCH=$(docker version --format '{{.Server.Arch}}')
# REHOST_ROUND_TAG: suffix for the persistent volumes, so two checkouts can run rounds
# at the same time without sharing a workspace.
VOL="rehost-linux-$ARCH${REHOST_ROUND_TAG:+-$REHOST_ROUND_TAG}"

TTY=""
[ -t 1 ] && TTY="-t"

# shellcheck disable=SC2086 # $TTY is empty or a single flag
exec docker run --rm $TTY \
  -v "$PWD:/src:ro" \
  -v "$VOL-work:/work" \
  -v "$VOL-nuget:/home/app/.nuget" \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  "mcr.microsoft.com/dotnet/sdk:$SDK_VERSION" \
  bash -ec '
    for owned in /work /home/app/.nuget; do
      [ "$(stat -c %u "$owned")" = "$(id -u app)" ] || chown -R app:app "$owned"
    done

    # The SDK probes this directory for write access when it verifies workloads.
    chown app:app /usr/share/dotnet/metadata

    cat >/usr/local/bin/round <<"INNER"
#!/usr/bin/env bash
set -euo pipefail
if [ ! -d /work/repo/.git ]; then
  git clone -q /src /work/repo
fi
cd /work/repo
git fetch -q /src HEAD
git checkout -q -f FETCH_HEAD
git clean -qfd
dotnet build Rehost.Web.slnx -v q
dotnet test Rehost.Web.slnx --no-build
INNER
    chmod 755 /usr/local/bin/round

    exec su app -c /usr/local/bin/round
  '
