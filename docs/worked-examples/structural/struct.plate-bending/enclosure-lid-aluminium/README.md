# Aluminium enclosure lid 300 × 200 × 4 mm, simply supported, 20 kPa internal pressure rise (`struct.plate-bending`)

**Source.** Analytic (Kirchhoff small-deflection theory, Navier double series for a simply supported rectangle under uniform load), recomputed in python (numpy, 1024 odd terms per direction, 1/N² tail removed), Timoshenko & Woinowsky-Krieger, Ch. 5, D = 418.958 N·m; w_max = 0.58996 mm at the centre; M_x = 41.620, M_y = 65.600 N·m/m at the centre, corner |M_xy| = 32.874 N·m/m; σ_vm = 21.560 MPa at the centre (21.352 MPa at a corner); corner reaction 2·M_xy = 65.75 N; f₁₁ = 353.29 Hz — Confirmed.

A 300 × 200 × 4 mm aluminium lid under 20 kPa. The plate is thin (span/t = 50) and the deflection is 0.59 mm, w/t = 0.15, well inside the small-deflection range, and 74 % of the span/250 limit. The greatest stress is at the centre (21.6 MPa von Mises, 16 % of the 150 MPa yield strength after γ = 1.1); at a corner the twisting moment gives 21.4 MPa and a 66 N hold-down reaction per corner, which the gasket bolts must carry. The first natural frequency is 353 Hz. E, ν and ρ are the W3 generic aluminium seed (indicative) and the yield strength is assumed: take them from the W3 material record for the alloy on the drawing. The partial factor 1.1 is the EN 1999 value, to be checked.

| Input | Value | Source |
|---|---|---|
| `E` | 70 GPa | reference: record:5eed0001-0000-4000-8000-000000000003@1 W3 generic seed 'Aluminium alloy (generic)', E: indicative, not from a standard; replace with the material record for the alloy on the drawing |
| `a` | 300 mm | drawing: ENC-LID-01 rev A |
| `b` | 200 mm | drawing: ENC-LID-01 rev A |
| `defl_ratio` | 250 | spec: Project specification: lid deflection limited to span/250 so the gasket stays seated |
| `f_y` | 150 MPa | assumption: Assumed 0.2 % proof strength for the sheet temper (the W3 generic seed has no strength); replace with the material record from the mill certificate. |
| `gamma_M0` | 1.1 | assumption: EN 1999-1-1 partial factor γ_M1 = 1.1 for the resistance of cross-sections of aluminium, entered in the γ_M0 slot; to be checked against the standard and National Annex in use |
| `load_basis` | pressure q | assumption: Load given as a pressure. |
| `load_type` | uniform | spec: Enclosure datasheet ENC-DS-07: internal pressure rise in a sealed enclosure on thermal cycling |
| `nu` | 0.33 | reference: record:5eed0001-0000-4000-8000-000000000003@1 W3 generic seed 'Aluminium alloy (generic)', nu: indicative |
| `q` | 20 kPa | spec: Enclosure datasheet ENC-DS-07: 0.2 bar differential |
| `rho` | 2700 kg/m^3 | reference: record:5eed0001-0000-4000-8000-000000000003@1 W3 generic seed 'Aluminium alloy (generic)', rho: indicative |
| `shape` | rectangular | drawing: ENC-LID-01 rev A |
| `support` | simply supported | assumption: The lid sits on a gasket land and is bolted at close pitch: treated as simply supported, which is the safe side for deflection and the central stress. |
| `t` | 4 mm | drawing: ENC-LID-01 rev A |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `D` | 418.958 N*m | 418.958 N*m | PASS |
| `w_max` | 0.589961 mm | 0.589961 mm | PASS |
| `M_x_c` | 41.6203 N*m/m | 41.6203 N*m/m | PASS |
| `M_y_c` | 65.6005 N*m/m | 65.6005 N*m/m | PASS |
| `M_xy_k` | 32.8736 N*m/m | 32.8736 N*m/m | PASS |
| `sigma_max` | 21.5596 MPa | 21.5596 MPa | PASS |
| `f1` | 353.292 Hz | 353.292 Hz | PASS |
| `u_defl` | 0.737451  | 0.737451  | PASS |

Governing check: Deflection w_max ≤ span/ratio — PASS (utilisation 0.74). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
