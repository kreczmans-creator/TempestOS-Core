# 25 mm shaft at a shoulder fillet (`mech.shaft-stress`)

**Source.** TempestOS calc.shaft-combined-stress (v0.21.0) hand calculation, docs/engineering/calculations/calc.shaft-combined-stress.md, Example 3 — Confirmed.

The shoulder fillet's concentration factors raise the bending stress by 60 %. The factor of safety, 1.85, still clears the 1.5 required.

| Input | Value | Source |
|---|---|---|
| `K_t` | 1.6 | spec: SPEC-MECH-03 design criteria |
| `K_ts` | 1.3 | spec: SPEC-MECH-03 design criteria |
| `M` | 120 N*m | spec: LOAD-SCHEDULE-01 |
| `S_y` | 250 MPa | drawing: DRG-SH-402 rev A |
| `T` | 60 N*m | spec: LOAD-SCHEDULE-01 |
| `d` | 25 mm | drawing: DRG-SH-402 rev A |
| `n_req` | 1.5 | spec: SPEC-MECH-03 design criteria |

| Output | Expected | Computed | Matches expected |
|---|---|---|---|
| `sigma` | 125.16 MPa | 125.165 MPa | yes |
| `n_T` | 1.8505  | 1.8505  | yes |
| `n_VM` | 1.8842  | 1.88416  | yes |

Governing check: Factor of safety against the required factor — PASS (utilisation 0.81). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
