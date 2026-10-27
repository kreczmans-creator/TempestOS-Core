# Plunger return spring: squared and ground compression spring, 60 to 180 N (`mech.spring`)

**Source.** Analytic (closed-form; recomputed in python3), d 3 mm, D 24 mm, n 9, squared and ground, L0 70 mm, G 79.3 GPa, E 206 GPa, rho 7850 kg/m3; F1 60 N, F2 180 N; R_m 1650 MPa (example value), f_tau 0.45, f_su 0.67, S_e 450 MPa, f_op 8 Hz: k = G d^4/(8 D^3 n) = 6.45345 N/mm, n_t = 11, L_s = 33 mm, s_2 = 27.892 mm; k_c = 1.17241; tau_2 = 477.684 MPa, F_c = 238.778 N, tau_c = 633.669 MPa (u_c 0.8534); f_n = 206.997 Hz (ratio 25.87); Goodman 1/n_f = 0.641906 — Confirmed.

A 3 mm wire coiled to 24 mm mean diameter (C = 8) with nine active coils has a rate of 6.45 N/mm. Between 60 and 180 N it moves 18.6 mm (from 9.30 to 27.89 mm); the 37 mm travel to solid would take 239 N. The stress at solid, 634 MPa, is 85 % of the permissible 742.5 MPa (u_c 0.853), which governs; the working stress is 478 MPa. Both ends are flat and parallel, so the spring is stable at any deflection. The surge frequency, 207 Hz, is 26 times the 8 Hz cycle rate (limit 13), and the Goodman factor is 1.56 against the 1.2 required. R_m, the stress fractions and the endurance strength are example values marked as assumptions: replace them from your own references.

| Input | Value | Source |
|---|---|---|
| `D` | 24 mm | drawing: PLG-210-04 rev B |
| `E` | 206 GPa | assumption: Generic spring steel Young's modulus; take it from the W3 material of the wire certificate. |
| `F_1` | 60 N | spec: Plunger assembly PLG-210 §4 rev B |
| `F_2` | 180 N | spec: Plunger assembly PLG-210 §4 rev B |
| `G` | 79.3 GPa | assumption: Generic spring steel shear modulus; take it from the W3 material of the wire certificate. |
| `L0` | 70 mm | drawing: PLG-210-04 rev B |
| `N_c` | 10000000 | spec: Plunger assembly PLG-210 §4: duty life |
| `Rm` | 1650 MPa | assumption: Example value for a 3 mm wire. Enter R_m(d) from your own copy of the wire standard or the wire certificate (record spring.wire-strength); the standard's table is not shipped. |
| `S_e` | 450 MPa | assumption: Example alternating shear endurance strength at 10^7 cycles; take it from the wire maker or your fatigue reference (record spring.endurance). |
| `corr` | Bergsträsser | spec: Company spring standard SPR-01 (EN 13906-1 correction factor) |
| `d` | 3 mm | drawing: PLG-210-04 rev B |
| `ends` | Squared and ground | drawing: PLG-210-04 rev B |
| `f_op` | 8 Hz | spec: Plunger assembly PLG-210 §4: maximum cycle rate |
| `f_su` | 0.67 | assumption: Ultimate shear strength fraction of R_m used by the Goodman relation (textbook steel value); confirm in your reference. |
| `f_tau` | 0.45 | assumption: Static permissible shear stress fraction of R_m for the example; take it from your design reference (record spring.permissible). |
| `guided` | no | assumption: Not guided: the free-buckling check is made, the safe-side reading. Tick it only if the assembly drawing shows a mandrel or a bore round the spring. |
| `mode` | Compression | spec: Plunger assembly PLG-210 §4 rev B |
| `n_active` | 9 | drawing: PLG-210-04 rev B |
| `n_f_req` | 1.2 | spec: Company spring standard SPR-01 |
| `nu_seat` | 0.5 | spec: Plunger assembly PLG-210 §4: both end plates flat and parallel, no rotation |
| `rho` | 7850 kg/m^3 | assumption: Generic steel density. |
| `rm_source` | Typed R_m(d) | assumption: R_m(d) typed for this example; a W3 material with the curve R_m = A/d^m can be applied instead. |
| `solid_method` | EN 13906-1 (d_max, coil clearance) | spec: Company spring standard SPR-01 (EN 13906-1 solid length and minimum coil clearance); no wire tolerance entered, so d_max is the nominal 3 mm |
| `tors_factor` | EN 13906-3 (q) | assumption: Torsion springs only; this compression spring does not use it, so the template default is kept. |
| `work` | Loads | spec: Plunger assembly PLG-210 §4 rev B |

| Output | Expected | Computed | Matches expected |
|---|---|---|---|
| `k` | 6.45345 N/mm | 6.45345 N/mm | yes |
| `L_s` | 33 mm | 33 mm | yes |
| `s_2` | 27.8921 mm | 27.8921 mm | yes |
| `tau_2` | 477.684 MPa | 477.684 MPa | yes |
| `F_c` | 238.778 N | 238.778 N | yes |
| `tau_c` | 633.669 MPa | 633.669 MPa | yes |
| `u_c` | 0.853426  | 0.853426  | yes |
| `f_n` | 206.997 Hz | 206.997 Hz | yes |
| `r_f` | 25.8746  | 25.8746  | yes |
| `n_f` | 1.55786  | 1.55786  | yes |

Governing check: Working deflection against the travel to the minimum length, keeping the coil clearance at L₂ — PASS (utilisation 0.88). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
