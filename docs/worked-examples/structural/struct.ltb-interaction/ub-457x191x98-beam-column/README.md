# Beam-column UB 457×191×98 S355, 5 m between restraints: LTB, flexural buckling and N+M interaction (`struct.ltb-interaction`)

**Source.** Analytic (EN 1993-1-1 6.3.1, 6.3.2.3 with the f modification, 6.3.3 Annex B), recomputed in python; section properties from the S1 sheet (worked example ub-457x191x98); the Annex B coefficients are to be checked against the owner's copy of the standard, EN 1993-1-1:2005+A1:2014, ψ = 0.5: C₁ = 1.31, M_cr = 808.379 kN·m, λ̄_LT = 0.976099, curve c (rolled, h/b = 2.42 > 2, Table 6.5), χ_LT = 0.653705, k_c = 0.858369, f = 0.933577, χ_LT,mod = 0.700215, M_b,Rd = 539.305 kN·m; N_b,z,Rd = 1494.53 kN (λ̄_z = 1.4903, curve b); Table B.2 plastic, C_my = C_mLT = 0.8, C_mz = 1.0: k_yy = 0.81052, k_yz = 0.82482, k_zy = 0.95134, k_zz = 1.37470; equations 6.61 = 0.64441, 6.62 = 0.93050 — Unconfirmed.

A beam-column between lateral restraints 5 m apart, carrying 400 kN compression with end moments 340 and 170 kN·m in single curvature (ψ = 0.5) and a small minor-axis moment. The section properties are the S1 sheet's, from the section dimensions alone. The elastic critical moment is 808 kN·m (C₁ = 1.31) and the 6.3.2.3 curve c with the f modification gives χ_LT = 0.70 and M_b,Rd = 539 kN·m (utilisation 0.63). Minor-axis flexural buckling governs the axial term (χ_z = 0.35, N_b,Rd = 1495 kN). The Annex B interaction equation 6.62 governs at 0.93, against 0.64 for 6.61: the member passes. The Annex B coefficients are the standard's and are to be checked by the owner.

| Input | Value | Source |
|---|---|---|
| `A` | 125.265 cm^2 | reference: S1 section sheet SEC-01, UB 457×191×98 (A = 125.265 cm²) |
| `C_2` | 0 | assumption: C₂ is 0 for end-moment loading. |
| `E` | 210 GPa | spec: BS EN 1993-1-1 cl. 3.2.6 |
| `G` | 81 GPa | spec: BS EN 1993-1-1 cl. 3.2.6: G = E/(2(1 + ν)) ≈ 81 GPa |
| `I_t` | 121.287 cm^4 | reference: S1 section sheet SEC-01 (J = 121.287 cm⁴) |
| `I_w` | 1.17554 dm^6 | reference: S1 section sheet SEC-01 (I_w = 1.17554 dm⁶) |
| `I_y` | 45728 cm^4 | reference: S1 section sheet SEC-01 (I_xx = 45727.99 cm⁴) |
| `I_z` | 2347.02 cm^4 | reference: S1 section sheet SEC-01 (I_yy = 2347.02 cm⁴) |
| `L` | 5 m | drawing: FR-BM-12 rev B |
| `L_cr_y` | 5 m | drawing: FR-BM-12 rev B |
| `L_cr_z` | 5 m | drawing: FR-BM-12 rev B |
| `M_y_A` | 340 kN*m | reference: S2 frame sheet FR-01, member 2, end A |
| `M_y_B` | 170 kN*m | reference: S2 frame sheet FR-01, member 2, end B (same side: single curvature) |
| `M_z_Ed` | 6 kN*m | assumption: Small minor-axis moment from the cladding-rail eccentricity (engineer's estimate). |
| `N_Ed` | -400 kN | reference: S2 frame sheet FR-01, member 2, ULS combination (compression negative) |
| `W_el_y` | 1957.53 cm^3 | reference: S1 section sheet SEC-01 (W_el,x = 1957.53 cm³) |
| `W_el_z` | 243.467 cm^3 | reference: S1 section sheet SEC-01 (W_el,y = 243.467 cm³) |
| `W_pl_y` | 2232.46 cm^3 | reference: S1 section sheet SEC-01 (W_pl,x = 2232.46 cm³) |
| `W_pl_z` | 378.904 cm^3 | reference: S1 section sheet SEC-01 (W_pl,y = 378.904 cm³) |
| `alpha_a` | 0.21 | spec: EN 1993-1-1 Table 6.1, recommended value |
| `alpha_a0` | 0.13 | spec: EN 1993-1-1 Table 6.1, recommended value |
| `alpha_b` | 0.34 | spec: EN 1993-1-1 Table 6.1, recommended value |
| `alpha_c` | 0.49 | spec: EN 1993-1-1 Table 6.1, recommended value |
| `alpha_d` | 0.76 | spec: EN 1993-1-1 Table 6.1, recommended value |
| `annex` | Annex B (method 2) | assumption: Annex B (method 2) chosen; Annex A can be selected for comparison. |
| `b` | 192.8 mm | spec: BS EN 10365 UB 457×191×98 |
| `beta_LT` | 0.75 | spec: EN 1993-1-1 cl. 6.3.2.3(1), recommended value |
| `curve_y` | auto (Table 6.2) | spec: EN 1993-1-1 Table 6.2: rolled I, h/b > 1.2, t_f ≤ 40 mm, buckling about y-y gives curve a |
| `curve_z` | auto (Table 6.2) | spec: EN 1993-1-1 Table 6.2: rolled I, h/b > 1.2, t_f ≤ 40 mm, buckling about z-z gives curve b |
| `edition` | 2005+A1:2014 | spec: BS EN 1993-1-1:2005+A1:2014 with the UK National Annex |
| `f_y` | 345 MPa | spec: BS EN 10025-2 S355, 16 mm < t ≤ 40 mm (flange 19.6 mm) |
| `gamma_M1` | 1 | spec: UK NA to BS EN 1993-1-1, NA.2.15 |
| `h` | 467.2 mm | spec: BS EN 10365 UB 457×191×98 |
| `k` | 1 | assumption: Fork supports at the lateral restraints (no end restraint of the minor-axis rotation). |
| `k_w` | 1 | assumption: Free to warp at the restraints. |
| `lambda_LT0` | 0.4 | spec: EN 1993-1-1 cl. 6.3.2.3(1), recommended value |
| `ltb_curve` | auto (Table 6.4 / 6.5) | assumption: Curve from Table 6.5 for a rolled I with h/b > 2. |
| `ltb_method` | 6.3.2.3 (rolled and welded sections) | assumption: 6.3.2.3 chosen for a hot-rolled UB; 6.3.2.2 is the alternative and gives a lower M_b,Rd. |
| `moment_mode` | end moments | assumption: The moments are the member end moments. |
| `psi_z` | 1 | assumption: Uniform minor-axis moment (conservative). |
| `section_class` | 1 | assumption: Class 1 assumed for S355 UB 457×191×98 under N + M; the engineer confirms the classification (EN 1993-1-1 Table 5.2). |
| `section_type` | rolled I | spec: BS EN 10365 UB 457×191×98, hot-rolled |
| `t_f` | 19.6 mm | spec: BS EN 10365 UB 457×191×98 |
| `torsion` | susceptible to torsional deformation (Table B.2) | assumption: An open I-section with no torsional restraint along its length is susceptible to torsional deformation (Table B.2). |
| `use_f` | yes | assumption: The f modification of 6.3.2.3(2) is permitted; k_c from the ψ formula. |
| `z_g` | 0 mm | assumption: End-moment loading: no transverse load, so the load height does not enter. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `C_1` | 1.31  | 1.31  | PASS |
| `Mcr` | 808.379 kN*m | 808.379 kN*m | PASS |
| `lambda_LT` | 0.976099  | 0.976099  | PASS |
| `chi_LT` | 0.700215  | 0.700215  | PASS |
| `Mb_Rd` | 539.305 kN*m | 539.305 kN*m | PASS |
| `u_LTB` | 0.630441  | 0.630441  | PASS |
| `N_b_Rd` | 1494.53 kN | 1494.53 kN | PASS |
| `k_yy` | 0.810522  | 0.810522  | PASS |
| `k_yz` | 0.82482  | 0.82482  | PASS |
| `k_zy` | 0.951338  | 0.951338  | PASS |
| `k_zz` | 1.3747  | 1.3747  | PASS |
| `u_61` | 0.64441  | 0.64441  | PASS |
| `u_62` | 0.930503  | 0.930503  | PASS |

Governing check: Interaction, equation 6.62 — PASS (utilisation 0.931). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
