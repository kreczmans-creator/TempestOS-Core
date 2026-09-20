#!/bin/bash
# SessionStart hook: make the graphify knowledge graph the basis of every
# Claude Code session on this repository (local machine and Claude Code on the web).
#
#  1. Expand graphify-out/graph.json from the committed graph.json.zip when missing.
#  2. On Claude Code on the web (fresh container), install the graphify CLI so
#     `graphify query|path|explain|update` work, and put it on the session PATH.
#
# Idempotent and non-interactive. Never fails the session: every step degrades
# to a printed note.
set -uo pipefail

ROOT="${CLAUDE_PROJECT_DIR:-$(pwd)}"
OUT="$ROOT/graphify-out"

# --- 1. graph.json from graph.json.zip -------------------------------------
if [ ! -f "$OUT/graph.json" ] && [ -f "$OUT/graph.json.zip" ]; then
  if command -v unzip >/dev/null 2>&1; then
    unzip -oq "$OUT/graph.json.zip" -d "$OUT"
  elif command -v python3 >/dev/null 2>&1; then
    python3 -m zipfile -e "$OUT/graph.json.zip" "$OUT"
  elif command -v python >/dev/null 2>&1; then
    python -m zipfile -e "$OUT/graph.json.zip" "$OUT"
  elif command -v powershell.exe >/dev/null 2>&1; then
    powershell.exe -NoProfile -Command "Expand-Archive -Force '$OUT/graph.json.zip' '$OUT'"
  fi
  if [ -f "$OUT/graph.json" ]; then
    echo "graphify: expanded graphify-out/graph.json from graph.json.zip"
  else
    echo "graphify: could not expand graphify-out/graph.json.zip (no unzip/python/powershell found)"
  fi
fi

# --- 2. graphify CLI on Claude Code on the web ------------------------------
if [ "${CLAUDE_CODE_REMOTE:-}" = "true" ]; then
  if ! command -v graphify >/dev/null 2>&1 && [ ! -x "$HOME/.local/bin/graphify" ]; then
    if command -v uv >/dev/null 2>&1; then
      uv tool install graphifyy -q >/dev/null 2>&1 || echo "graphify: uv tool install graphifyy failed"
    elif command -v python3 >/dev/null 2>&1; then
      python3 -m pip install -q graphifyy >/dev/null 2>&1 \
        || python3 -m pip install -q --break-system-packages graphifyy >/dev/null 2>&1 \
        || echo "graphify: pip install graphifyy failed"
    else
      echo "graphify: neither uv nor python3 available; graphify CLI not installed"
    fi
  fi
  if [ -x "$HOME/.local/bin/graphify" ] && [ -n "${CLAUDE_ENV_FILE:-}" ]; then
    echo 'export PATH="$HOME/.local/bin:$PATH"' >> "$CLAUDE_ENV_FILE"
  fi
  if command -v graphify >/dev/null 2>&1 || [ -x "$HOME/.local/bin/graphify" ]; then
    echo "graphify: CLI available ($("$HOME/.local/bin/graphify" --version 2>/dev/null || graphify --version 2>/dev/null || echo installed))"
  fi
fi

# The graphify skill itself is a project skill at .claude/skills/graphify, and the
# usage rules live in CLAUDE.md, so nothing else is needed for the session.
exit 0
