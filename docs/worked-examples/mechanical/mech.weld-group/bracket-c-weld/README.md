# Bracket welded to a column flange on three sides (`mech.weld-group`)

**Source.** Analytic (elastic line method; Shigley Table 9-1 pattern), Two 80 mm horizontal welds and one 200 mm vertical weld, 8 mm fillets, 30 kN down at 250 mm from the vertical weld — Confirmed.

A C-shaped fillet weld (80 + 200 + 80 mm) carries 30 kN at 250 mm. The centroid sits 17.8 mm from the vertical weld, so the torque is 6.97 kN·m. The worst corner carries 380 N/mm; on an 8 mm fillet the throat stress is 67 MPa (utilisation 0.31), and a 3 mm leg would do.

| Input | Value | Source |
|---|---|---|
| `F_x` | 0 kN | assumption: No horizontal load. |
| `F_y` | -30 kN | spec: Hoist rating with dynamic factor |
| `F_z` | 0 kN | assumption: No out-of-plane load. |
| `M_x` | 0 kN*m | assumption: None. |
| `M_y` | 0 kN*m | assumption: None. |
| `M_z` | 0 kN*m | assumption: None. |
| `e_x` | 250 mm | drawing: BRK-010 rev A |
| `e_y` | 100 mm | drawing: BRK-010 rev A |
| `f_w` | 220 MPa | assumption: S275, EN 1993-1-8 simplified method: 410/√3/(0.85 × 1.25) ≈ 223 MPa, rounded down. |
| `s` | 8 mm | drawing: BRK-010 rev A |
| `welds` | 3 row(s) | drawing: BRK-010 rev A |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `x_c` | 17.7778 mm | 17.7778 mm | PASS |
| `J_u` | 2.49422E+06 mm^3 | 2.49422E+06 mm^3 | PASS |
| `T` | -6.96667 kN*m | -6.96667 kN*m | PASS |
| `f_r_max` | 379.644 N/mm | 379.644 N/mm | PASS |
| `tau` | 67.1224 MPa | 67.1224 MPa | PASS |
| `s_req` | 2.44082 mm | 2.44082 mm | PASS |

Governing check: Throat stress against design strength — PASS (utilisation 0.31). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
