#!/usr/bin/env bash
#
# WP 21.5C (Linux variant) - the real-shell acceptance journey.
#
# Starts a virtual X display, builds the solution, launches the *real*
# Tempest.Desktop application on it and drives it through a scripted
# acceptance journey with genuine X11 input (xdotool), taking a screenshot
# at every step. It then closes the application through the operating
# system's own close message and relaunches it on the same persistence root
# to prove everything created is still there - on screen and in tempest.db.
#
# Usage:
#   scripts/run-realshell-linux.sh [--display :102] [--out <dir>] [--configuration Debug] [--no-build]
#
# Requires: Xvfb, xdotool, ImageMagick (`import`), and the .NET SDK in
# global.json. Every one of them is checked for below rather than assumed.
#
# Exit code 0 only if every required step of both passes passed.

set -uo pipefail

DISPLAY_NUMBER=":102"
OUT_DIR=""
CONFIGURATION="Debug"
BUILD=1

while [ $# -gt 0 ]; do
  case "$1" in
    --display) DISPLAY_NUMBER="$2"; shift 2 ;;
    --out) OUT_DIR="$2"; shift 2 ;;
    --configuration) CONFIGURATION="$2"; shift 2 ;;
    --no-build) BUILD=0; shift ;;
    -h|--help) sed -n '2,20p' "$0"; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

OUT_DIR="${OUT_DIR:-$REPO_ROOT/artifacts/realshell}"
RUN_ROOT="$OUT_DIR/run"
JOURNEY_OUT="$OUT_DIR/journey"
VERIFY_OUT="$OUT_DIR/verify"
RUNNER="tests/Tempest.Desktop.RealShell/bin/$CONFIGURATION/net10.0/Tempest.Desktop.RealShell.dll"

missing=""
for tool in Xvfb xdotool import dotnet; do
  command -v "$tool" >/dev/null 2>&1 || missing="$missing $tool"
done
if [ -n "$missing" ]; then
  echo "Missing required tool(s):$missing" >&2
  echo "Install them (e.g. apt-get install xvfb xdotool imagemagick) and try again." >&2
  exit 2
fi

echo "== Real-shell journey =="
echo "   repository    $REPO_ROOT"
echo "   display       $DISPLAY_NUMBER"
echo "   configuration $CONFIGURATION"
echo "   output        $OUT_DIR"

if [ "$BUILD" -eq 1 ]; then
  echo "-- Building the solution ($CONFIGURATION, warnings as errors)"
  dotnet build src/TempestOS.slnx --configuration "$CONFIGURATION" -p:TreatWarningsAsErrors=true || exit 1
fi

[ -f "$RUNNER" ] || { echo "The runner was not built: $RUNNER" >&2; exit 1; }

STARTED_XVFB=0
if ! DISPLAY="$DISPLAY_NUMBER" xdotool getdisplaygeometry >/dev/null 2>&1; then
  echo "-- Starting Xvfb on $DISPLAY_NUMBER"
  Xvfb "$DISPLAY_NUMBER" -screen 0 1600x1000x24 -nolisten tcp >"$OUT_DIR/xvfb.log" 2>&1 &
  XVFB_PID=$!
  STARTED_XVFB=1
  for _ in $(seq 1 40); do
    DISPLAY="$DISPLAY_NUMBER" xdotool getdisplaygeometry >/dev/null 2>&1 && break
    sleep 0.25
  done
fi

cleanup() {
  if [ "$STARTED_XVFB" -eq 1 ] && [ -n "${XVFB_PID:-}" ]; then
    kill "$XVFB_PID" >/dev/null 2>&1
  fi
}
trap cleanup EXIT

rm -rf "$RUN_ROOT" "$JOURNEY_OUT" "$VERIFY_OUT"
mkdir -p "$RUN_ROOT" "$JOURNEY_OUT" "$VERIFY_OUT"

echo "-- Pass 1: the journey, on a clean persistence root"
DISPLAY="$DISPLAY_NUMBER" dotnet "$RUNNER" \
  --display "$DISPLAY_NUMBER" --root "$RUN_ROOT" --out "$JOURNEY_OUT" --mode journey
JOURNEY_CODE=$?

echo "-- Pass 2: relaunch on the same root and verify what was persisted"
DISPLAY="$DISPLAY_NUMBER" dotnet "$RUNNER" \
  --display "$DISPLAY_NUMBER" --root "$RUN_ROOT" --out "$VERIFY_OUT" --mode verify
VERIFY_CODE=$?

echo
echo "== Summary =="
for file in "$JOURNEY_OUT/journey.md" "$VERIFY_OUT/journey.md"; do
  [ -f "$file" ] && sed -n '5p' "$file"
done
echo "   journey screenshots: $JOURNEY_OUT"
echo "   verify  screenshots: $VERIFY_OUT"
echo "   persistence root:    $RUN_ROOT"

if [ -f "$RUN_ROOT/logs/tempestos-crash.log" ]; then
  echo "A crash log was written:" >&2
  cat "$RUN_ROOT/logs/tempestos-crash.log" >&2
  exit 1
fi

if [ "$JOURNEY_CODE" -ne 0 ] || [ "$VERIFY_CODE" -ne 0 ]; then
  echo "FAILED (journey $JOURNEY_CODE, verify $VERIFY_CODE) - see the step tables above." >&2
  exit 1
fi

echo "PASSED - every required step of both passes was verified."
