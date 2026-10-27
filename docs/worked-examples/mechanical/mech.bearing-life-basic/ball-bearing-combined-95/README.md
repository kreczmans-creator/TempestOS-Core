# Ball bearing, combined load, 95 % reliability (`mech.bearing-life-basic`)

**Source.** TempestOS calc.bearing-rating-life (v0.21.0) hand calculation, docs/engineering/calculations/calc.bearing-rating-life.md, Example 3 — Confirmed.

Radial and axial load combine through the catalogue X and Y factors into a 3.93 kN equivalent load. At 95 % reliability (a₁ = 0.64) the life is 2,746 h against 2,000 h required.

| Input | Value | Source |
|---|---|---|
| `C` | 25 kN | drawing: DRG-GB-211 rev A |
| `F_a` | 1.5 kN | spec: LOAD-SCHEDULE-01 |
| `F_r` | 3 kN | spec: LOAD-SCHEDULE-01 |
| `L_req` | 2000 h | spec: SPEC-MECH-03 design criteria |
| `X` | 0.56 | drawing: DRG-GB-211 rev A |
| `Y` | 1.5 | drawing: DRG-GB-211 rev A |
| `a_1` | 0.64 | spec: SPEC-MECH-03 design criteria |
| `n` | 1000 rpm | spec: LOAD-SCHEDULE-01 |
| `type` | ball | drawing: DRG-GB-211 rev A |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `F_dyn` | 3.93 kN | 3.93 kN | PASS |
| `L_10h` | 4290.3 h | 4290.33 h | PASS |
| `L_nmh` | 2745.8 h | 2745.81 h | PASS |

Governing check: Required life against modified life — PASS (utilisation 0.73). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
