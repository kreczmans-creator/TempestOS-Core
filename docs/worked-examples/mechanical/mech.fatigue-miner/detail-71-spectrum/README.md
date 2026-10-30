# Welded detail category 71 under a three-block spectrum (`mech.fatigue-miner`)

**Source.** TempestOS calc.fatigue-miner (v0.21.0) hand calculation, docs/engineering/calculations/calc.fatigue-miner.md, Example 1 — Confirmed.

Three stress-range blocks on a category-71 detail. Each block's share of the damage is close to the others; the total 0.47 means the spectrum can be repeated about twice before failure.

| Input | Value | Source |
|---|---|---|
| `N_ref` | 2000000 | spec: SPEC-MECH-03 design criteria |
| `S_ref` | 71 MPa | spec: SPEC-MECH-03 design criteria |
| `blocks` | 3 row(s) | spec: LOAD-SCHEDULE-01 |
| `m` | 3 | spec: SPEC-MECH-03 design criteria |

| Output | Expected | Computed | Matches expected |
|---|---|---|---|
| `D_tot` | 0.46939  | 0.46939  | yes |
| `R_f` | 2.1304  | 2.13042  | yes |

Governing check: Total damage against the damage limit — PASS (utilisation 0.47). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
