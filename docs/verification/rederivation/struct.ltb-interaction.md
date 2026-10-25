# struct.ltb-interaction — independent re-derivation (Track P, P1)
Calculator: EN 1993-1-1 LTB and N+M interaction (S7), version 1.1.0. Source files: src/Tempest.Calc/Tempest.Calc.Structural/LtbInteractionCalculator.cs and LtbInteractionMath.cs. Spec: docs/calcs/structural/struct.ltb-interaction.md.
Script: tools/verification/struct.ltb-interaction.py (exit 0 = all KATs match; runs in under 1 s).
Verdict: MATCH WITH FINDINGS

All 60 expected values of the 8 KATs match. All 567 values of 62 extra probes match to 5e-10. The code is correct against the published formulas as I know them. Two findings concern the method itself: the C1 curve fit and an extra cap on the f-modified factor. The branch was claude/tempestos-v0260-test-readiness-s4jwh1 (VERSION 0.26.0), as the brief said.

Shared-source caveat. I wrote Annex A and Annex B from memory, and the build team did the same from published expressions. Agreement shows no transcription slip. It does not prove the coefficients equal the printed tables. The owner should check them against a printed copy (see notes).

## Method re-derived
- M_cr (NCCI SN003 general form, doubly symmetric, z_j = 0): C1 π²E I_z/(kL)² [√((k/k_w)² I_w/I_z + (kL)² G I_t/(π²E I_z) + (C2 z_g)²) − C2 z_g]. Units N, mm, MPa.
- C1 for end moments: 1.88 − 1.40ψ + 0.52ψ² ≤ 2.70 (ENV 1993-1-1 Annex F fit). C2 = 0 for end moments.
- λ̄_LT = √(W_y f_y/M_cr), W_pl,y (class 1-2) or W_el,y (class 3). Curves: Table 6.4 (rolled h/b ≤ 2 a, > 2 b; welded c, d) and Table 6.5 (rolled b, c; welded c, d). Table 6.1 α: 0.13, 0.21, 0.34, 0.49, 0.76.
- cl. 6.3.2.2: Φ = 0.5[1 + α(λ̄ − 0.2) + λ̄²]; χ = 1/(Φ + √(Φ² − λ̄²)) ≤ 1.
- cl. 6.3.2.3 with UK NA values λ̄_LT,0 = 0.4, β = 0.75, γ_M1 = 1.0: Φ = 0.5[1 + α(λ̄ − 0.4) + βλ̄²]; χ ≤ 1 and ≤ 1/λ̄². f = 1 − 0.5(1 − k_c)[1 − 2(λ̄ − 0.8)²] ≤ 1; k_c = 1/(1.33 − 0.33ψ) (Table 6.6); χ_mod = χ/f.
- cl. 6.3.1: N_cr = π²EI/L_cr²; λ̄ = √(A f_y/N_cr); χ as above. Table 6.2 for S 235-S 420: rolled h/b > 1.2 a, b (t_f ≤ 40), b, c (≤ 100); h/b ≤ 1.2 b, c (≤ 100), d, d; welded b, c (≤ 40), c, d.
- cl. 6.3.3 Eq. 6.61, 6.62 with ΔM = 0. Annex B Tables B.1, B.2 (plastic and elastic columns), Table B.3 C_m = 0.6 + 0.4ψ ≥ 0.4.
- Annex A Tables A.1, A.2: μ, w ≤ 1.5, n_pl, a_LT = 1 − I_t/I_y, C_my,0, C_mz,0, ε_y, C_my, C_mLT, N_cr,T, λ̄_0, b_LT to e_LT, C_yy to C_zy with lower bounds.
- Cross-check without tables: a Rayleigh-Ritz eigenproblem (sine series) gives M_cr for any moment diagram and any load height. It matches the closed form for uniform moment to 1e-6 and the UDL values to 0.5 %.

## Known-answer cases
The script prints every value. Here is one row per case (n = values compared).

| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-mcr-uniform-6322 (n=7) | M_cr, chi_LT, M_b,Rd | 398.103 kN·m, 0.315851, 235.059 kN·m | same | 1.2e-12 max | 1e-6 | PASS |
| ka-6323-f-modified (n=9) | f, chi_LT, M_b,Rd | 0.885586, 0.634078, 471.886 kN·m | same | 8.8e-13 max | 1e-6 | PASS |
| ka-mcr-k-zg-c2 (n=4) | M_cr, chi_LT | 440.518 kN·m, 0.411642 | same | 4.9e-13 max | 1e-6 | PASS |
| ka-flexural-buckling (n=9) | chi_y, chi_z, N_b,Rd | 0.931891, 0.603545, 2373.98 kN | same | 9.6e-11 max | 1e-6 | PASS |
| ka-annex-b-class2 (n=9) | k_yy, k_zz, u_62 | 0.848513, 1.600473, 1.24809 | same | 2.8e-12 max | 1e-6 | PASS |
| ka-annex-b-class3-b1 (n=8) | k_yy, k_zy, u_62 | 0.860536, 0.688429, 1.18268 | same | 4.4e-12 max | 1e-6 | PASS |
| ka-annex-a-class2 (n=6) | k_yz, k_zy, u_61 | 2.166104, 0.749357, 1.39512 | same | 3.4e-12 max | 1e-6 | PASS |
| ka-annex-a-class3 (n=8) | k_yz, k_zy, u_62 | 1.395680, 1.181153, 1.48164 | same | 4.4e-12 max | 1e-6 | PASS |

No KAT expected value disagrees with physics or the cited method. The six Unconfirmed-status KATs agree with my own derivation.

## Extra probes (product vs independent)
UB 457×191×98 S355 is the sheet default; UC 254×254×73 and a welded 400×300×20/10 plate girder are the other sections. All rows PASS.

| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| Curve b, 6.3.2.2 | UB, L 5 m, ψ 0, N −800 | chi_LT | 0.7079434 | 0.7079434 | 4e-11 | PASS |
| Curve a/b, h/b ≤ 2 | UC, 6.3.2.2 / 6.3.2.3 | chi_LT | 0.882376 / 0.908141 | same | 1e-10 | PASS |
| Curve c, welded h/b ≤ 2 | girder, both methods | chi_LT | 0.747937 / 0.848732 | same | 1e-10 | PASS |
| Chosen curves a-d, both methods | UB, L 7 m, ψ 1 | chi_LT | 8 values | same | 1e-10 | PASS |
| Near λ̄ = 0.2 (0.199, 0.201) | 6.3.2.2, UB | chi_LT | 1 then 0.999646 | same | 0 | PASS |
| Near λ̄ = 0.4 (0.399, 0.401, 0.45) | 6.3.2.3, UB | chi_LT (0.399, 0.401, 0.45) | 1, 0.999443, 0.972060 | same | 0 | PASS |
| f factor, ψ −1, λ̄ 1.2, curve a | UB, use_f | chi_LT | 0.694444 | 0.694444 | 4e-11 | PASS |
| Destabilising load | C1 1.127, C2 0.454, z_g +233.6 | M_cr | 396.031 kN·m | 396.031 | 5e-11 | PASS |
| Stabilising load | same, z_g −233.6 | M_cr | 719.038 kN·m | 719.038 | 5e-11 | PASS |
| z_g ±1000, C2 1.5, L 0.5 m | UB | M_cr | 2 values | same | 1e-10 | PASS |
| k = k_w = 0.5; k = 0.7 | UB, L 8 m | M_cr | 2 values | same | 1e-10 | PASS |
| End moments (6 pairs) | A 300 / B −150; −250 / 100; 0 / 100 … | C_1, chi_LT, u_62 | 6 sets | same | 1e-10 | PASS |
| Annex B B.1, B.2, class 3 B.2, λ̄_z < 0.4 | UB | k_yy … k_zz | 5 sets | same | 5e-10 | PASS |
| Annex A class 1, 2, 3; LTB terms off; k 0.8; N tiny, N 0 | UB, UC | k_yy … k_zz, C_my | 8 sets | same | 5e-10 | PASS |
| Flexural curve edges | t_f 40, 60, 110; welded 45; S460 manual | chi_y, chi_z | 5 sets | same | 2e-10 | PASS |
| Near zero and large | M_y 0; I_w 1e-5; L 100 m; M 1e4 kN·m | various | 5 sets | same | 5e-10 | PASS |

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| M_cr formula, units, cancellation for z_g > 0 | SN003 general form | Same; uses a stable equal form for C2 z_g > 0 | Yes |
| z_g sign | Positive = load on compression-flange side destabilises | Eigen check confirms the sign and size | Yes |
| C1 from ψ | ENV Annex F fit, k = 1 | Same formula, cap 2.70, warns for k ≠ 1 | Yes (see -01) |
| Curve choice, Tables 6.4, 6.5, 6.2 | As listed above | Same, boundaries h/b = 2, 1.2; t_f 40, 100 | Yes |
| λ̄ thresholds | 0.2 (6.3.2.2), 0.4 (6.3.2.3) | χ = 1 at or below; continuous | Yes |
| χ_LT ≤ 1/λ̄² | Applies to 6.3.2.3 | Applied; also applied to f-modified χ | Partly (see -02) |
| f and k_c | Table 6.6, linear diagram | Same; blocks when C1 entered and k_c missing | Yes |
| Annex B Tables B.1, B.2, B.3 | As listed above | Same, including elastic kzy with 0.05 | Yes |
| Annex A Tables A.1, A.2 | As listed above | Same; threshold turns b_LT to e_LT, C_mLT off | Yes |
| Annex A at N = 0 | Undefined (ε_y → ∞) | Uses the limit C_my = 1 | Yes |
| Guards | Class 4, W_pl < W_el, N ≥ N_cr (Annex A), curve missing, tension | All block as intended; k and k_w ≤ 1 keeps out cantilevers | Yes |
| C_mLT > 0.25 block | Table B.2 divides by C_mLT − 0.25 | Input minimum 0.4, so the block cannot fire | See -03 |
| Cantilever, mono-symmetric, tapered | Out of scope | Not supported; no input allows it | Yes |
| UK NA | γ_M1 1.0; λ̄_LT,0 0.4; β 0.75; Table 6.5 | Defaults equal these | Yes |

## Findings
| ID | Severity | Finding | Evidence | Suggested fix (for Track S) |
|---|---|---|---|---|
| P1-struct.ltb-interaction-01 | Medium | The ψ formula for C1 is unconservative against the exact elastic solution for long members and double curvature. No warning says so. | UB 457×191×98, ψ −0.5 (A 100, B −50 kN·m), L 12 m: formula C1 2.70, eigen C1 2.459 (+9.8 %). M_b,Rd 407.75 kN·m against 384.46 with C1 2.4589 entered (+6.1 %). ψ 0, L 20 m: 1.88 against 1.789; M_b,Rd +4.2 %. At L 6 m the error is 2.4 % (ψ 0) and 5.9 % (ψ −0.5). The formula matches the SN003 table values to about 0.5 %, so a hand check against the table agrees. An LTBeam run will not. | Add a note or warning on the ψ-derived C1: "curve fit, can exceed the exact value by up to about 10 % for L/h large and ψ < 0.25; use LTBeam or enter C1". Optionally lower the cap or the ψ < 0.25 branch. |
| P1-struct.ltb-interaction-02 | Medium | The 1/λ̄² cap is also applied to the f-modified χ_LT,mod. As I know cl. 6.3.2.3(2), only χ_LT,mod ≤ 1 applies. The cap is conservative. | UB, 6.3.2.3, curve a, use_f, ψ −1, L chosen for λ̄ 1.2: χ_LT 0.6442, f 0.8648, χ/f 0.745. Product gives 0.6944 (= 1/λ̄²), M_b,Rd 550.4 against 590.4 kN·m (−6.8 %). | Check the owner's copy of cl. 6.3.2.3(2) and Eq. 6.58. If it caps at 1 only, remove the second cap. If kept, say so in the method text, which now cites 6.58 for it. |
| P1-struct.ltb-interaction-03 | Low | Rule `C_m_LT` (block when C_mLT ≤ 0.25) cannot fire. C_m_LT input has a hard minimum of 0.4 and the derived values are at least 0.4. | Template: C_m_LT Min 0.4; Table B.3 value ≥ 0.4. | Keep as defence, or drop the rule from the declared list. |
| P1-struct.ltb-interaction-04 | Info | Annex B class 3 with Table B.2 and the λ̄_z < 0.4 branch of Table B.2 have no KAT. | Probes "AnnexB class3 B.2" and "AnnexB class2 B.2 lam_z<0.4" match the script. | Add two KATs from the script values (k_zy 0.971761 for the class 3 probe). |
| P1-struct.ltb-interaction-05 | Info | LTB may also be ignored when M_Ed/M_cr ≤ λ̄_LT,0² (cl. 6.3.2.2(4)). The sheet checks only λ̄. This errs on the safe side. | Code path in ChiLtRolled / ChiLtGeneral. | None needed. |
| P1-struct.ltb-interaction-06 | Info | No consistency check between section inputs (for example I_y < I_z, or I_w far from the plate value). Wrong typed properties pass silently. | Not run as a case; only the hard ranges apply. | Optional warning for I_y < I_z. |

## Notes for the owner's manual test
- Uniform moment, welded 500×200×16/10, S355, 6 m, 6.3.2.2: M_cr 398.10 kN·m, λ̄_LT 1.3673, χ_LT 0.3159, M_b,Rd 235.06 kN·m. Hand-check with M_cr = (π/L)√(E I_z G I_t)·√(1 + π²E I_w/(L² G I_t)).
- Sheet default (UB 457×191×98), ψ 0.5, M_y 200 kN·m, N −1200 kN, Annex B Table B.2: M_cr 620.28, χ_LT 0.5628, M_b,Rd 446.04 kN·m, N_b,Rd 1105.8 kN, k_yy 0.848, k_zy 0.803, u_62 1.445.
- Compare k_yy, k_yz, k_zy, k_zz for Annex A and B with SCI P364 examples 3, 4, 11 and 12, and check the Table A.1, A.2, B.1, B.2 coefficients against a printed copy. Both teams worked from the same memory of those tables.
- Run an LTBeam case with ψ −0.5, L 12 m on the default UB. Expect C1 about 2.46 against the sheet's 2.70 (finding -01).
- Try z_g +233.6 mm with C1 1.127, C2 0.454 (UDL): M_cr 396.03 kN·m. With z_g −233.6 mm: 719.04 kN·m. A positive z_g must lower M_cr.
