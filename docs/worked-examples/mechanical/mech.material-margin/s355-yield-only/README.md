# S355 at 250 MPa — margin below the requirement (`mech.material-margin`)

**Source.** Hand calculation, n_y = 355/250 = 1.42; u = 1.5/1.42 = 1.0563 — Confirmed.

Only the yield strength is known; the 1.42 factor falls short of the 1.5 required.

| Input | Value | Source |
|---|---|---|
| `f_y` | 355 MPa | drawing: DRG-BR-702 rev A |
| `k_u` | 1.5 | spec: SPEC-MECH-03 design criteria |
| `n_req` | 1.5 | spec: SPEC-MECH-03 design criteria |
| `sigma` | 250 MPa | spec: LOAD-SCHEDULE-01 |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `n_y` | 1.42  | 1.42  | PASS |
| `u` | 1.0563  | 1.05634  | PASS |

Governing check: Required factor of safety against the governing factor — FAIL (utilisation 1.06). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
