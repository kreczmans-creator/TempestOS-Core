# 50 mm solid steel shaft in a 100 mm steel hub (`mech.press-fit`)

**Source.** Analytic (Shigley 9th ed. eq. 3-56 to 3-58), p = 62.1 MPa, hub hoop 103.5 MPa, F = 87.8 kN, T = 2195 N·m — Confirmed.

Equal-stiffness steel parts: the interface pressure is 62 MPa, the hub bore hoop stress 104 MPa (von Mises 145 MPa, 41 % of yield), the press force 88 kN and the friction torque 2.2 kN·m. Shrinking the hub needs a 100 K rise for 20 µm of assembly clearance.

| Input | Value | Source |
|---|---|---|
| `E_h` | 207 GPa | spec: EN 10025 S355, E per textbook |
| `E_s` | 207 GPa | spec: steel |
| `L` | 60 mm | drawing: DRG-HUB-04 rev C |
| `S_y_h` | 355 MPa | spec: EN 10025 S355 |
| `S_y_s` | 355 MPa | spec: EN 10025 S355 |
| `alpha_h` | 0.000012 1/K | spec: steel |
| `c_a` | 20 µm | assumption: Assembly clearance for a shrink fit. |
| `d` | 50 mm | drawing: DRG-HUB-04 rev C |
| `d_i` | 0 mm | drawing: DRG-SHAFT-12 rev A |
| `d_o` | 100 mm | drawing: DRG-HUB-04 rev C |
| `delta` | 40 µm | drawing: DRG-HUB-04 (H7/p6 mean) rev C |
| `mu` | 0.15 | assumption: Dry steel on steel, pressed; Shigley Table 7-? range 0.1–0.3. |
| `nu_h` | 0.292 | spec: steel |
| `nu_s` | 0.292 | spec: steel |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `p` | 62.1 MPa | 62.1 MPa | PASS |
| `sigma_t_hub` | 103.5 MPa | 103.5 MPa | PASS |
| `F_a` | 87.8 kN | 87.7918 kN | PASS |
| `T` | 2195 N*m | 2194.8 N*m | PASS |
| `dT` | 100 K | 100 K | PASS |

Governing check: Hub yield — PASS (utilisation 0.41). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
