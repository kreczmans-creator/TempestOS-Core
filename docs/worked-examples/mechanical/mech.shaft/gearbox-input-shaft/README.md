# Gearbox input shaft: pinion between bearings, coupling overhung (`mech.shaft`)

**Source.** Analytic (S2 beam solver; Shigley Ch. 7 method), Stepped steel shaft 35/40/45/40/35 mm, bearings at 20 and 280 mm, pinion at 150 mm (tangential 6 kN, radial 2.2 kN, 4 kg), torque 0.3 kN·m from the coupling at 330 mm — Confirmed.

The pinion sits mid-way between the bearings, so each bearing carries √(2.2² + 6²)/2 = 3.20 kN (linked on to M5). The keyseat under the pinion governs fatigue: n_f = 1.69 against the 1.5 target. Slope at the bearings is 0.90 mrad against 1 mrad; deflection at the pinion 0.072 mm against 0.13 mm. The first critical speed is 27 times the running speed. A 40 mm key is three times longer than needed.

| Input | Value | Source |
|---|---|---|
| `E` | 207 GPa | assumption: Steel. |
| `L_key` | 40 mm | drawing: GBX-SH-01 rev C |
| `R` | 99 % | assumption: Design reliability. |
| `S_ut` | 700 MPa | spec: EN 10083-3 42CrMo4 +QT, certificate |
| `S_y` | 500 MPa | spec: EN 10083-3 42CrMo4 +QT, certificate |
| `S_y_key` | 400 MPa | spec: C45 key steel |
| `T` | 0.3 kN*m | spec: Motor rated torque × service factor (E1) |
| `finish` | machined or cold-drawn | drawing: GBX-SH-01 rev C |
| `h` | 9 mm | drawing: GBX-SH-01 rev C |
| `k_d` | 1 | assumption: Below 250 °C. |
| `k_f` | 1 | assumption: No other effects. |
| `loads` | 1 row(s) | spec: Gear mesh forces from the gear calculation |
| `n` | 1450 rpm | spec: Motor nameplate |
| `n_target` | 1.5 | spec: Company design standard |
| `rho` | 7850 kg/m^3 | assumption: Steel. |
| `sections` | 4 row(s) | assumption: Shoulders r/d ≈ 0.05; keyseat at the pinion (K_t 2.14, K_ts 3.0); q from the Neuber fit. |
| `segments` | 5 row(s) | drawing: GBX-SH-01 rev C |
| `theta_max` | 0.001 rad | assumption: Deep-groove ball bearing slope limit. |
| `w` | 14 mm | drawing: GBX-SH-01 rev C |
| `x_A` | 20 mm | drawing: GBX-SH-01 rev C |
| `x_B` | 280 mm | drawing: GBX-SH-01 rev C |
| `x_T1` | 150 mm | drawing: GBX-SH-01 rev C |
| `x_T2` | 330 mm | drawing: GBX-SH-01 rev C |
| `x_key` | 150 mm | drawing: GBX-SH-01 rev C |
| `y_max` | 0.13 mm | assumption: Spur gear deflection limit. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `R_A` | 3.19531 kN | 3.19531 kN | PASS |
| `M_max` | 0.41539 kN*m | 0.41539 kN*m | PASS |
| `n_f_min` | 1.6909  | 1.69087  | PASS |
| `n_y_min` | 4.1532  | 4.15317  | PASS |
| `tau_key` | 23.8095 MPa | 23.8095 MPa | PASS |

Governing check: Slope at the bearings against limit — PASS (utilisation 0.896). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
