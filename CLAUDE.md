## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
- The raw graph is committed compressed as `graphify-out/graph.json.zip` (the 90 MB `graph.json` is git-ignored). If `graphify-out/graph.json` is missing, expand it first: PowerShell `Expand-Archive graphify-out/graph.json.zip graphify-out/` or `unzip graphify-out/graph.json.zip -d graphify-out/`. After a `graphify update .`, re-zip it before committing.
- Corpus scope of the committed graph: `src/`, `tests/`, `docs/`, `scripts/`, `.github/` and root docs. `archive/` (superseded material), evidence PNGs and the `docs/design/system/site-export/` HTML dumps were deliberately excluded.
