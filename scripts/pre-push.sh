#!/usr/bin/env bash
# Runs, before a push, the checks CI's gate runs first, so a push does not go red on something a laptop could have caught:
#   1. a locked restore (as CI: a stale packages.lock.json fails here, NU1004),
#   2. a build of the solution with warnings as errors,
#   3. the governance health check (scripts/governance-healthcheck.ps1; needs PowerShell 7, `pwsh`),
#   4. the offline document checks and the scripts' own tests (licences, glossary, verification snapshot, tests/scripts),
#   5. the fast test suites: Core and every Calc project, one after another; with --desktop, the three Desktop shards as well, one at a time.
# It is not installed as a git hook; run it yourself:  scripts/pre-push.sh [--configuration Debug|Release] [--desktop]
# Every test project runs even after one fails; the script lists what failed and exits non-zero.
set -uo pipefail

configuration=Release
desktop=0

while [ $# -gt 0 ]; do
  case "$1" in
    -c|--configuration) configuration="${2:?--configuration needs Debug or Release}"; shift 2 ;;
    --desktop) desktop=1; shift ;;
    -h|--help) sed -n '2,9p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "pre-push: unknown argument '$1' (try --help)" >&2; exit 64 ;;
  esac
done

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root" || exit 1
solution=src/TempestOS.slnx
failed=()

step() { printf '\n=== %s\n' "$*"; }

python=python3
command -v "$python" >/dev/null 2>&1 || python=python

step "Restore (locked)"
dotnet restore "$solution" --locked-mode || { echo "pre-push: the locked restore failed (a stale packages.lock.json? see CONTRIBUTING.md, NuGet lock files)" >&2; exit 1; }

step "Build ($configuration, warnings as errors)"
dotnet build "$solution" --configuration "$configuration" --no-restore -p:TreatWarningsAsErrors=true || { echo "pre-push: the build failed" >&2; exit 1; }

step "Governance health check"
if command -v pwsh >/dev/null 2>&1; then
  pwsh -NoProfile -File scripts/governance-healthcheck.ps1 || failed+=("governance health check")
else
  echo "pre-push: pwsh (PowerShell 7) is not installed, so the governance health check cannot run here; CI runs it on every push." >&2
  failed+=("governance health check (pwsh missing)")
fi

step "Document checks and the scripts' tests"
"$python" scripts/check-licences.py || failed+=("licence check")
"$python" scripts/generate-glossary.py --check || failed+=("glossary check")
"$python" scripts/generate-verification-snapshot.py --check || failed+=("verification snapshot check")
"$python" -m unittest discover -s tests/scripts || failed+=("tests/scripts")

run_tests() {
  local name="$1"; shift
  step "Test $name ($configuration)"
  dotnet test "$@" --configuration "$configuration" --no-build --blame-hang --blame-hang-timeout 5m || failed+=("$name")
}

run_tests "Core" tests/Tempest.Core.Tests

for project in Engine Sheets Library Calculators FluidPower Import; do
  [ -d "tests/Tempest.Calc.Tests.$project" ] && run_tests "Calc.$project" "tests/Tempest.Calc.Tests.$project"
done

if [ "$desktop" -eq 1 ]; then
  # The CI shards' filters (ci.yml): the first letter of the class's own namespace segment, A-C, D-P and Q-Z.
  shard_filter() { local filter="" letter; for letter in "$@"; do filter="${filter:+$filter|}FullyQualifiedName~Tempest.Desktop.Tests.$letter"; done; echo "$filter"; }
  run_tests "Desktop shard 1 (A-C)" tests/Tempest.Desktop.Tests --filter "$(shard_filter A B C)"
  run_tests "Desktop shard 2 (D-P)" tests/Tempest.Desktop.Tests --filter "$(shard_filter D E F G H I J K L M N O P)"
  run_tests "Desktop shard 3 (Q-Z)" tests/Tempest.Desktop.Tests --filter "$(shard_filter Q R S T U V W X Y Z)"
fi

if [ ${#failed[@]} -gt 0 ]; then
  printf '\npre-push: FAILED: %s\n' "$(IFS=';'; echo "${failed[*]}" | sed 's/;/; /g')" >&2
  exit 1
fi

printf '\npre-push: everything passed (%s%s).\n' "$configuration" "$([ "$desktop" -eq 1 ] && echo ', Desktop included' || echo ', Desktop not run: add --desktop')"
