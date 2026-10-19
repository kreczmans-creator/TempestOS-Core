## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- When graphify-out/graph.json exists, run `graphify query "<question>"` before your first grep, search or source-file read for a codebase question, and put this rule in every subagent prompt that explores code. Read raw files only for lines the graph or an earlier read already located. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
- The raw graph is committed compressed as `graphify-out/graph.json.zip` (the ~95 MB `graph.json` is git-ignored). If `graphify-out/graph.json` is missing, expand it first: PowerShell `Expand-Archive graphify-out/graph.json.zip graphify-out/` or `unzip graphify-out/graph.json.zip -d graphify-out/` (the SessionStart hook in `.claude/hooks/session-start.sh` does this automatically). After a `graphify update .`, re-zip it before committing.
- `graphify-out/graph.json.zip` is rewritten whole on every build, so a large diff to it in a pull request is expected. Do not flag it as churn or suggest skipping the graph rebuild or its hooks.
- Corpus scope is set by `.graphifyignore` at the repository root: `archive/` (superseded material), evidence PNGs, the logo SVG variants, the design-system HTML renders and site export, and graphify's own Claude Code wiring (`.claude/`, `CLAUDE.md`) are deliberately outside the graph. `graphify update .` honours that file, so the scope holds across incremental updates.
