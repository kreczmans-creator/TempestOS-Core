# Slope-5 curve with an endurance limit — damage above one (`mech.fatigue-miner`)

**Source.** TempestOS calc.fatigue-miner (v0.21.0) hand calculation, docs/engineering/calculations/calc.fatigue-miner.md, Example 2 — Confirmed.

The 50 MPa block lies below the 60 MPa endurance limit and does no damage, but the other two sum to 1.43: the detail fails before one spectrum is complete.

| Input | Value | Source |
|---|---|---|
| `N_ref` | 1000000 | spec: SPEC-MECH-03 design criteria |
| `S_e` | 60 MPa | spec: SPEC-MECH-03 design criteria |
| `S_ref` | 150 MPa | spec: SPEC-MECH-03 design criteria |
| `blocks` | 3 row(s) | spec: LOAD-SCHEDULE-01 |
| `m` | 5 | spec: SPEC-MECH-03 design criteria |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `D_tot` | 1.433  | 1.43296  | PASS |
| `R_f` | 0.69786  | 0.697856  | PASS |

Governing check: Total damage against the damage limit — FAIL (utilisation 1.44). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
