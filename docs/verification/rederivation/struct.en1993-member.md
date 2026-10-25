# struct.en1993-member — independent re-derivation (Track P, P1)
Calculator: EN 1993-1-1 member checks (basic), version 1.0.0. Source file: src/Tempest.Calc/Tempest.Calc.Structural/MemberChecksCalculator.cs. Spec: docs/calcs/structural/struct.en1993-member.md.
Script: tools/verification/struct.en1993-member.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS (two High after review: -02 upgraded, -R1 added)

All 14 expected values of the 3 KATs match. The core formulae agree with my re-derivation in every probe. The findings concern the shear-bending reduction, the default k factors and some guards.
The repository was on branch claude/tempestos-v0260-test-readiness-s4jwh1 (VERSION 0.26.0), not release/v0.26.0. The code under test is the same version.

## Method re-derived
- Plastic axial resistance N_pl,Rd = A f_y / γM0 (BS EN 1993-1-1 cl. 6.2.3, 6.2.4). Units: cm², MPa, kN.
- Bending resistance M_c,Rd = W f_y / γM0, with W_pl for class 1-2 and W_el for class 3 (cl. 6.2.5).
- Shear area for a rolled I section, load parallel to the web: A_v = max(A − 2 b t_f + (t_w + 2r) t_f, η h_w t_w), h_w = h − 2 t_f (cl. 6.2.6(3)).
- V_pl,Rd = A_v f_y / (√3 γM0) (cl. 6.2.6(2)).
- Bending and shear (cl. 6.2.8): ρ = (2 V_Ed/V_pl,Rd − 1)² above 0.5 V_pl,Rd. For an I section, major axis: M_y,V,Rd = (W_pl,y − ρ A_w²/(4 t_w)) f_y/γM0 (Eq. 6.30).
- Elastic critical force N_cr = π² E I / L_cr² (cl. 6.3.1.2).
- Slenderness λ̄ = √(A f_y / N_cr); Φ = 0.5[1 + α(λ̄ − 0.2) + λ̄²]; χ = 1/(Φ + √(Φ² − λ̄²)) ≤ 1 (Eq. 6.49, 6.50).
- Imperfection factors (Table 6.1): a0 0.13, a 0.21, b 0.34, c 0.49, d 0.76. N_b,Rd = χ A f_y / γM1.
- Linear cross-section summation (cl. 6.2.1(7)). Member check with user k factors (cl. 6.3.3; Annex B Table B.1 used only to test the default k).

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-uc-compression | N_pl_Rd | 2083.85 | 2083.85 | 0 | 1e-4 | PASS |
| ka-uc-compression | N_cr_z | 2005.3 | 2005.257 | 2.2e-5 | 1e-3 | PASS |
| ka-uc-compression | lambda_z | 1.0194 | 1.01941 | 8.3e-6 | 1e-3 | PASS |
| ka-uc-compression | chi_z | 0.5288 | 0.52876 | 7.8e-5 | 1e-3 | PASS |
| ka-uc-compression | N_b_z_Rd | 1101.9 | 1101.85 | 4.1e-5 | 1e-3 | PASS |
| ka-uc-compression | u_buckling | 0.8168 | 0.81680 | 5.9e-6 | 1e-3 | PASS |
| ka-ub-bending-shear | M_y_c_Rd | 171.325 | 171.325 | 0 | 1e-4 | PASS |
| ka-ub-bending-shear | A_v | 20.068 | 20.0676 | 2.0e-5 | 1e-4 | PASS |
| ka-ub-bending-shear | V_pl_Rd | 318.62 | 318.616 | 1.3e-5 | 1e-3 | PASS |
| ka-ub-bending-shear | u_M_y | 0.70043 | 0.70042 | 9.8e-6 | 1e-3 | PASS |
| ka-ub-bending-shear | u_V | 0.31385 | 0.31386 | 2.4e-5 | 1e-3 | PASS |
| ka-tension-class3 | N_pl_Rd | 710 | 710 | 0 | 1e-9 | PASS |
| ka-tension-class3 | M_z_c_Rd | 10.65 | 10.65 | 0 | 1e-9 | PASS |
| ka-tension-class3 | u_section | 1.03294 | 1.03286 | 7.4e-5 | 1e-4 | PASS |

The section properties in the KATs agree with the published UK section tables for UC 203x203x46 and UB 305x165x40. See P1-struct.en1993-member-07 for the u_section rounding.

## Extra probes (product vs independent)
Product values are from the probe tool. "Independent" is my EN-correct value. Units: kN, kN·m, cm².
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| P1 high shear | UB 305x165x40 S275, V 250, M_y 120 | M_y,c,Rd | 115.80 | 160.62 | 0.28 | FINDING-01 |
| P1 high shear | same | u_M_y | 1.0363 FAIL | 0.7471 PASS | 0.39 | FINDING-01 |
| P1 high shear | same | ρ, V_pl, χ, N_b | all agree | | <1e-9 | PASS |
| P2 V = V_pl | same, M_y 0 | M_y,c,Rd | -4e-6 | 138.29 | 1.0 | FINDING-01 |
| P3 V = 1.2 V_pl | same, M_y 50 | M_y,c,Rd | -164.47 | 106.57 | 2.5 | FINDING-01, -03 |
| P4 slender | UC, N -50, L 20 m | λ̄_z, χ_z, u_buckling | 5.097, 0.0351, 0.6830 | same | <1e-9 | PASS |
| P5 stocky | UC, N -900, L 0.5 m | λ̄_z, χ_z, u_buckling | 0.1274, 1.0, 0.4319 | same | <1e-9 | PASS |
| P6 α override | UC, α_y = α_z = 0.2 | χ_z, u_buckling | 0.6584, 0.6560 | same | <1e-9 | PASS |
| P7 partial factors | γM0 1.05, γM1 1.1, N -900, M_y 40 | u_buckling | 0.8985 | same | <1e-9 | PASS |
| P7 partial factors | same | u_member | 1.1365 | 1.1479 | 9.9e-3 | FINDING-R2 (review) |
| P8 class 2 / 3 | UB, M_y 100 | u_M_y | 0.5837 / 0.6494 | same | <1e-9 | PASS |
| P9 tension | UC, N +300, M_y 50, M_z 8 | u_section | 0.5249 | same | <1e-9 | PASS |
| P10 compression | UC, N -500, M_y 100, M_z 10, k 1.1/1.2 | u_member | 1.2236 | same | <1e-9 | PASS |
| P11 zero | N, M, V all 0 | all u | 0 | 0 | 0 | PASS |
| P12 very large | A 5e4 cm², N -8e4 kN, f_y 700 | u_section, u_member | 0.0357, 0.0517 | same | <1e-9 | PASS |
| P13 η helper | h 1000, t_w 10, η 1.2, V 1500 | A_v, u_V | 117.6, 0.6223 | same | <1e-9 | PASS |
| P14 blank r | UB dims, r blank | A_v | 18.252 | 18.252 | 0 | PASS |
| SHS k test | N -700, M_y 20, k_yy 1.0 / 1.3034 | u_member | 0.9627 / 1.0766 | 0.9627 / 1.0766 | <5e-6 | PASS (see -02) |

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| N_pl, M_c, V_pl, A_v helper | cl. 6.2.3-6.2.6 | Same formulae; r blank taken as 0; η h_w t_w floor present | Yes |
| N_cr, λ̄, Φ, χ, N_b | Eq. 6.49-6.50, Table 6.1 | Same; χ capped at 1; α override works | Yes |
| Class 3 modulus | W_el | W_el used for class 3 | Yes |
| Class 4 | Out of scope | Blocked with a clear message | Yes |
| Units | cm², cm³, cm⁴, m, GPa | Engine converts; all probes agree | Yes |
| High-shear reduction | M_y,V,Rd from Eq. 6.30 | (1 − ρ) times the whole M_c,Rd, for y and z | No (-01) |
| ρ at or above 1 | Shear check fails | Negative resistance and negative utilisation shown | Partly (-03) |
| Member check, default k | Annex A or B factors | k_yy = k_zz = 1.0, one equation, χ_min | No (-02) |
| Tension member output | No buckling check | u_member = u_M_y + u_M_z, N omitted | Partly (-04) |
| Guard: shear area | Needed only if V_Ed > 0 | Blocks the whole sheet when A_v and helper blank | Partly (-05) |
| Guard: slenderness | No EN limit | Warning above λ̄ = 2.0 | Yes (extra) |
| LTB warning | Out of scope | Shown on every run, even when M_y = 0 | Yes |
| Negative utilisation | Never a pass | Engine fails it (checked in P3) | Yes |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) | Status @ 0772fcf8 |
|---|---|---|---|---|---|---|
| P1-struct.en1993-member-01 | Medium | Medium (confirmed: conservative 28 %; I checked Eq. 6.30 by hand, 160.6 kN·m) | High-shear reduction multiplies the full bending resistance by (1 − ρ), for both axes. EN 1993-1-1 6.2.8 reduces only the web part for an I section. The result is over-conservative and can fail a section that passes. | UB 305x165x40 S275, M_y 120 kN·m, V 250 kN (0.785 V_pl, ρ = 0.324). Product M_y,c,Rd = 115.8 kN·m, u = 1.036 FAIL. Eq. 6.30 gives 160.6 kN·m, u = 0.747. M_z,c,Rd is also cut by 32 % although the web carries almost no minor-axis moment. | Use (W_pl,y − ρ A_w²/(4 t_w)) f_y/γM0 for I sections about y. Leave M_z unreduced for load in the web plane. Otherwise state the (1 − ρ) simplification on the sheet and in the warning. | Open. Probe: UB 305x165x40, M_y 120, V 250 still gives M_y,c,Rd 115.80 kN·m and u 1.036 FAIL. |
| P1-struct.en1993-member-02 | Medium | High (upgraded: unconservative pass in plausible use; hollow sections have no LTB caveat to hide behind, and nothing warns at run time) | Default k_yy = k_zz = 1.0 is unconservative where Annex B gives k > 1, and no warning says so. | Hypothetical SHS 40 cm², W_pl 150 cm³, I 1200 cm⁴, S355, curve a, L 3 m, N -700 kN, uniform M_y 20 kN·m. Product u_member = 0.963 PASS. Annex B k_yy = 1.30 gives 1.077 FAIL (about 11 % higher). Product with k_yy = 1.3034 returns 1.0766. | Warn when k is left at 1.0 and N_Ed/N_b,Rd > 0.3. Or derive k from Annex A or B, or default to the upper bound C_m(1 + 0.8 n). | Open. Probe: the SHS case still gives u_member 1.0766 with k = 1 as default (no Annex B warning). |
| P1-struct.en1993-member-03 | Low | Low (confirmed: the shear check fails, so the verdict is right) | When V_Ed ≥ V_pl,Rd, ρ ≥ 1 and the bending resistance prints as zero or negative. The overall result still fails through u_V. | V = 1.2 V_pl, M_y 50: M_y,c,Rd = -164.5 kN·m, u_M_y = -0.304, u_section = -0.304. | Clamp ρ at 1, and show a block or a warning when V_Ed > V_pl,Rd. | Open. Probe: V = 1.2 V_pl still gives M_y,c,Rd −164.47 kN·m and u_M_y −0.304. |
| P1-struct.en1993-member-04 | Low | Low (confirmed) | In tension, output u_member (labelled "Simplified member interaction", cl. 6.3.3) equals u_M_y + u_M_z and leaves out N_Ed. It is not a listed check, but a reader may take it as one. | N_Ed +300 kN, M_y 50, M_z 8: u_member = 0.381 while u_section = 0.525. | Set u_member to blank or equal to u_section in tension. | Open. Probe: N +300 gives u_member 0.381 against u_section 0.525. |
| P1-struct.en1993-member-05 | Low | Low (confirmed) | The sheet blocks completely when neither A_v nor the helper dimensions are given, even if V_Ed = 0. A pure compression check then needs shear data. | N_Ed -900, V_Ed 0, A_v and h, b, t_w, t_f blank: BLOCK shear-area, no outputs. | Skip the shear check and warn when V_Ed = 0 and no shear area is given. | Open. Probe: V_Ed 0 with no shear data still blocks (shear-area). |
| P1-struct.en1993-member-06 | Low | Medium (upgraded: the sheet cites 6.2.3, and N_u,Rd governs many bolted ties; about 19 % for S355 with 15 % holes) | Tension checks gross-section yielding only. The net-section resistance N_u,Rd (cl. 6.2.3(2)(b)) is not computed and nothing warns about bolt holes. | Template text and Run method. | Add a warning that net-section fracture is not checked. | Open. Probe: a tension case gives only the no-LTB warning; no net-section note. |
| P1-struct.en1993-member-07 | Info | Info (confirmed) | KAT ka-tension-class3 states u_section = 1.03294. The exact value is 400/710 + 5/10.65 = 1.03286. The 1e-4 tolerance hides the 7.4e-5 difference. | Independent script, row 14. | Correct the KAT value to 1.03286 (the spec text says 1.0329). | Open (no change). The KAT value is as before. |
| P1-struct.en1993-member-R1 | — | High | The shear-area helper inputs h, b, t_w, t_f and r carry UC 203x203x46 defaults. A new sheet seeds them. Nothing checks them against A. A user who changes A, W and I but not the helper gets a wrong A_v. | UC 305x305x198 S355 (A 252 cm²), V_Ed 1500 kN, helper left at the UC 203 values: A_v 210.2 cm², V_pl,Rd 4309 kN, u_V 0.35 PASS. With the true dimensions: A_v 70.0 cm², u_V 1.045 FAIL. The shear resistance is 3 times too high. | Remove the helper defaults. Also warn when 2 b t_f + (h − 2 t_f) t_w differs from A by more than about 10 %. | Open. Probe: A 252 cm² with the helper at UC 203 values still gives A_v 210.24 cm², V_pl,Rd 4309 kN, u_V 0.348. |
| P1-struct.en1993-member-R2 | — | Medium | The member interaction divides the moments by M_c,Rd, which uses γ_M0. Eq. 6.61 and 6.62 use M_Rk/γ_M1. It is unconservative when γ_M1 > γ_M0. | UC 203x203x46, N −900, M_y 40, γ_M0 1.05, γ_M1 1.1: product u_member 1.1365, EN 1.1479. With γ_M0 1.0 and γ_M1 1.1 the bending term is 10 % low. UK NA defaults (1.0, 1.0) are not affected. | Use W f_y/γ_M1 in the member check, without the shear reduction. | Open. Probe: γ_M0 1.05, γ_M1 1.1 still gives u_member 1.1365. |
| P1-struct.en1993-member-R3 | — | Low | Shear buckling of the web (6.2.6(6), h_w/t_w > 72 ε/η) is not checked or flagged. V_pl,Rd is then too high for slender webs. Rolled sections rarely reach the limit. | Template text and Run method. | Warn when h_w/t_w exceeds 72 ε/η, using the helper dimensions when given. | Open (no change). No shear-buckling check or warning. |

## Notes for the owner's manual test
- UC 203x203x46 S355, N -900 kN, L 4 m, curves b and c: expect N_b,z,Rd 1101.9 kN, χ_z 0.5288, u_buckling 0.817.
- UB 305x165x40 S275, M_y 120, V 250: the sheet fails at u_M_y 1.036. By Eq. 6.30 by hand, M_y,V,Rd is 160.6 kN·m and the section passes (finding -01).
- Set curve to d and L to 20 m: expect the slenderness warning (λ̄ above 2.0) and χ well below 0.1.
- Try a square hollow section with equal moments at both ends and N/N_b near 0.6. Compare the sheet with Annex B by hand (finding -02).
- Leave A_v and the helper dimensions blank with V = 0 and note the block (finding -05).

## Independent review
- Reviewer: senior reviewer (Claude), 2026-10-24.
- I re-ran the script after one fix: all KAT rows pass, TOTAL FAILS 0, exit 0.
- Script fix: the member check now uses M_Rk/γ_M1, as Eq. 6.61 does. The first pass had copied the product's γ_M0 term.
- I reproduced -01 by hand (Eq. 6.30 gives 160.6 kN·m) and -02 by hand (Annex B k_yy 1.3035).
- -02 is upgraded to High. The default k = 1.0 lets an 8 % overstress pass with no runtime notice.
- -06 is upgraded to Medium. The sheet cites 6.2.3 but leaves out N_u,Rd.
- New -R1 (High): stale helper dimensions gave a shear resistance 3 times too high. I reproduced it with the probe.
- The engine skips optional inputs with defaults, but the desktop sheet seeds them. So R1 bites in the app, not the probe.
- New -R2 (Medium) and -R3 (Low) as listed.
- Residual doubt: Annex B k-factor formulae are from memory. Owner to confirm against licensed copy.

## Re-check after the Addendum A Blue round (0772fcf8)
- Source change (3c637a3d; version 1.0.1): only the description of N_Ed. It now states tension positive and compression negative, the opposite sign to S6. No calculation changed.
- KATs: `Probe kat` passes all 4 cases and no expected value changed. The script exits 0 with 0 fails.
- I re-ran the probes for -01 to -05, R1 and R2 with the original inputs. Every value matches the review.
- Findings: 10 earlier rows, 0 fixed, 10 open. The High rows -02 and R1 are still open.
- New defects: none found. The sign-convention text is a documentation gain for the S2 to S3 and S3 to S6 links.
- Probe inputs: /tmp/claude-0/recheck4/struct-bs/em1.py.
