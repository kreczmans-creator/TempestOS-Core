# Deep-groove ball bearing under radial load (`mech.bearing-life-basic`)

**Source.** TempestOS calc.bearing-rating-life (v0.21.0) hand calculation, docs/engineering/calculations/calc.bearing-rating-life.md, Example 1 — Confirmed.

A 35.1 kN-rated ball bearing at 4 kN and 1,500 rev/min. Its basic rating life is 7,500 h against 5,000 h required.

| Input | Value | Source |
|---|---|---|
| `C` | 35.1 kN | drawing: DRG-GB-210 rev A |
| `F_a` | 0 kN | spec: LOAD-SCHEDULE-01 |
| `F_r` | 4 kN | spec: LOAD-SCHEDULE-01 |
| `L_req` | 5000 h | spec: SPEC-MECH-03 design criteria |
| `X` | 1 | drawing: DRG-GB-210 rev A |
| `Y` | 0 | drawing: DRG-GB-210 rev A |
| `a_1` | 1 | spec: SPEC-MECH-03 design criteria |
| `n` | 1500 rpm | spec: LOAD-SCHEDULE-01 |
| `type` | ball | drawing: DRG-GB-210 rev A |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `P` | 4 kN | 4 kN | PASS |
| `L_10` | 675.68  | 675.68  | PASS |
| `L_10h` | 7507.6 h | 7507.56 h | PASS |

Governing check: Modified life against required life — PASS (utilisation 0.666). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
