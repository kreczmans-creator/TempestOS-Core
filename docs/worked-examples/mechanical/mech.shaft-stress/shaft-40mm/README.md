# 40 mm shaft in bending and torsion (`mech.shaft-stress`)

**Source.** TempestOS calc.shaft-combined-stress (v0.21.0) hand calculation, docs/engineering/calculations/calc.shaft-combined-stress.md, Example 1 — Confirmed.

A plain 40 mm shaft. Tresca gives the lower factor, 2.33, against 2 required.

| Input | Value | Source |
|---|---|---|
| `K_t` | 1 | drawing: DRG-SH-401 rev A |
| `K_ts` | 1 | drawing: DRG-SH-401 rev A |
| `M` | 500 N*m | spec: LOAD-SCHEDULE-01 |
| `S_y` | 350 MPa | drawing: DRG-SH-401 rev A |
| `T` | 800 N*m | spec: LOAD-SCHEDULE-01 |
| `d` | 40 mm | drawing: DRG-SH-401 rev A |
| `n_req` | 2 | spec: SPEC-MECH-03 design criteria |

| Output | Expected | Computed | Matches expected |
|---|---|---|---|
| `sigma_vm` | 135.98 MPa | 135.982 MPa | yes |
| `n_T` | 2.3311  | 2.33106  | yes |
| `n_VM` | 2.5739  | 2.57387  | yes |

Governing check: Factor of safety against the required factor — PASS (utilisation 0.86). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
