# Internal pressure, closed ends (`mech.thick-cylinder`)

**Source.** Analytic (Lamé), v0.16 calc.thick-walled-cylinder vector 1, a = 50, b = 100 mm, p_i = 50 MPa — Confirmed.

A 2:1 wall ratio under 50 MPa: bore hoop stress 83 MPa against a thin-wall estimate of 75 MPa; von Mises 115 MPa, 58 % of the allowable.

| Input | Value | Source |
|---|---|---|
| `a` | 50 mm | drawing: DRG-CYL-01 rev A |
| `b` | 100 mm | drawing: DRG-CYL-01 rev A |
| `closed` | yes | drawing: DRG-CYL-01 end caps rev A |
| `p_i` | 50 MPa | spec: SPEC-PV-2 §3 design pressure |
| `p_o` | 0 MPa | assumption: Atmospheric outside. |
| `sigma_allow` | 200 MPa | spec: SPEC-PV-2 §5 |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `sigma_t_a` | 83.333 MPa | 83.3333 MPa | PASS |
| `sigma_vm_a` | 115.47 MPa | 115.47 MPa | PASS |
| `u` | 0.57735  | 0.57735  | PASS |

Governing check: von Mises against allowable — PASS (utilisation 0.577). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
