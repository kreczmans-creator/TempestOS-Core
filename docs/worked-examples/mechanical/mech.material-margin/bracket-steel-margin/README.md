# Bracket steel margin on yield and ultimate (`mech.material-margin`)

**Source.** Hand calculation, n_y = 2.5, n_u = 3.75, governing 2.5 — Confirmed.

A 120 MPa working stress on a 300/450 MPa steel. Yield and ultimate margins agree once the ultimate is scaled by k_u = 1.5; the factor 2.5 clears 1.5.

| Input | Value | Source |
|---|---|---|
| `f_u` | 450 MPa | drawing: DRG-BR-701 rev A |
| `f_y` | 300 MPa | drawing: DRG-BR-701 rev A |
| `k_u` | 1.5 | spec: SPEC-MECH-03 design criteria |
| `n_req` | 1.5 | spec: SPEC-MECH-03 design criteria |
| `sigma` | 120 MPa | spec: LOAD-SCHEDULE-01 |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `n_y` | 2.5  | 2.5  | PASS |
| `n_gov` | 2.5  | 2.5  | PASS |
| `u` | 0.6  | 0.6  | PASS |

Governing check: Factor of safety against the required factor — PASS (utilisation 0.60). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
