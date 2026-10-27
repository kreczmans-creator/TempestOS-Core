# External pressure, closed ends (`mech.thick-cylinder`)

**Source.** Analytic (Lamé), v0.16 vector 2, a = 40, b = 60 mm, p_o = 20 MPa — Confirmed.

External pressure puts the whole wall in compression: bore hoop −72 MPa, outer −52 MPa; von Mises 62 MPa at the bore, 62 % of the allowable.

| Input | Value | Source |
|---|---|---|
| `E` | 207 GPa | assumption: Generic steel Young modulus for the elastic collapse pressure; take it from the material record of the tube. |
| `a` | 40 mm | drawing: DRG-CYL-02 rev B |
| `b` | 60 mm | drawing: DRG-CYL-02 rev B |
| `closed` | yes | drawing: DRG-CYL-02 rev B |
| `nu` | 0.3 | assumption: Generic steel Poisson ratio for the elastic collapse pressure; take it from the material record of the tube. |
| `p_i` | 0 MPa | assumption: Vented bore. |
| `p_o` | 20 MPa | spec: SPEC-SUB-1 §2 external pressure |
| `sigma_allow` | 100 MPa | spec: SPEC-SUB-1 §4 |

| Output | Expected | Computed | Matches expected |
|---|---|---|---|
| `sigma_t_a` | -72 MPa | -72 MPa | yes |
| `sigma_vm_a` | 62.354 MPa | 62.3538 MPa | yes |
| `u` | 0.62354  | 0.623538  | yes |

Governing check: von Mises against allowable — PASS (utilisation 0.62). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
