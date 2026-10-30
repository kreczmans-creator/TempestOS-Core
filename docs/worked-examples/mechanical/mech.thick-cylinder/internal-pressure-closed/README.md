# Internal pressure, closed ends (`mech.thick-cylinder`)

**Source.** Analytic (Lamé), v0.16 calc.thick-walled-cylinder vector 1, a = 50, b = 100 mm, p_i = 50 MPa — Confirmed.

A 2:1 wall ratio under 50 MPa: bore hoop stress 83 MPa against a thin-wall estimate of 75 MPa; von Mises 115 MPa, 58 % of the allowable.

| Input | Value | Source |
|---|---|---|
| `E` | 207 GPa | assumption: Generic steel; used only for the elastic collapse pressure under external pressure, so it does not enter this internal-pressure case. |
| `a` | 50 mm | drawing: DRG-CYL-01 rev A |
| `b` | 100 mm | drawing: DRG-CYL-01 rev A |
| `closed` | yes | drawing: DRG-CYL-01 end caps rev A |
| `nu` | 0.3 | assumption: Generic steel; used only for the elastic collapse pressure under external pressure, so it does not enter this internal-pressure case. |
| `p_i` | 50 MPa | spec: SPEC-PV-2 §3 design pressure |
| `p_o` | 0 MPa | assumption: Atmospheric outside. |
| `sigma_allow` | 200 MPa | spec: SPEC-PV-2 §5 |

| Output | Expected | Computed | Matches expected |
|---|---|---|---|
| `sigma_t_a` | 83.333 MPa | 83.3333 MPa | yes |
| `sigma_vm_a` | 115.47 MPa | 115.47 MPa | yes |
| `u` | 0.57735  | 0.57735  | yes |

Governing check: von Mises against allowable — PASS (utilisation 0.58). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
