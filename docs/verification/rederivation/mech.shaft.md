# mech.shaft — independent re-derivation (Track P, P1)
Calculator: Shaft: static, fatigue, keys, critical speed, version 1.0.0. Source file: src/Tempest.Calc/Tempest.Calc.Mechanical/ShaftCalculator.cs. Spec: docs/calcs/mechanical/mech.shaft.md.
Script: tools/verification/mech.shaft.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS

The three known-answer cases agree with my own derivation. The weak points are the critical-speed quotient, the shoulder side, and two silent input traps.

## Method re-derived
- Beam: Euler-Bernoulli finite elements with piecewise-constant EI. Pin at A, roller at B, one solve per plane. M, slope and deflection add as vectors. Units mm, N, MPa.
- Stress: σ_a = K_f 32 M d/(π(d⁴ − d_i⁴)) and τ_m = K_fs 16 T d/(π(d⁴ − d_i⁴)), with K_f = 1 + q(K_t − 1). Source: Shigley 11th ed., §6-10 and §7-4.
- Endurance: S_e′ = 0.5 S_ut up to 1400 MPa, else 700 MPa. Source: Shigley 11th ed., §6-9.
- Marin factors: k_a = a S_ut^b with (a, b) = (1.58, −0.085) ground, (4.51, −0.265) machined, (57.7, −0.718) hot-rolled, (272, −0.995) forged. Source: Shigley 11th ed., Table 6-2, MPa form.
- Size and reliability: k_b = 1.24 d^−0.107 up to 51 mm, 1.51 d^−0.157 above. k_c = 1. k_e = 1 − 0.08 z_R, with z_R from SciPy's normal quantile.
- Criteria: DE-Goodman 1/n_f = σ_a/S_e + √3 τ_m/S_ut. First-cycle yield n_y = S_y/√(σ_a² + 3τ_m²). Shigley 11th ed., §7-4.
- Other criteria for information: DE-Gerber, DE-ASME and DE-Soderberg with σ_m′ = √3 τ_m. Shigley 11th ed., §6-12.
- Critical speed: exact first eigenvalue from consistent-mass finite elements. Rayleigh quotient with signed deflection. Dunkerley as shaft term plus Σ m δ_ii. Shigley 11th ed., §7-6.
- Keys: τ = 2T/(d w L), σ = 4T/(d h L). Required length from 0.577 S_y in shear and S_y in bearing. Shigley 11th ed., §7-7.

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-simply-supported | R_A (kN) | 1 | 1 | 0 | 1e-6 | PASS |
| ka-simply-supported | M_max (kN·m) | 0.2 | 0.2 | 0 | 1e-6 | PASS |
| ka-simply-supported | theta_A (rad) | 2.429991e-3 | 2.429991e-3 | 1.6e-7 | 1e-5 | PASS |
| ka-simply-supported | y_load (mm) | 0.3239989 | 0.3239989 | 1.5e-7 | 1e-5 | PASS |
| ka-simply-supported | n_c (rpm) | 5305.174 | 5305.174 | 3.4e-8 | 1e-5 | PASS |
| ka-simply-supported | n_c_D (rpm) | 5305.174 | 5305.174 | 3.4e-8 | 1e-5 | PASS |
| ka-key | tau_key (MPa) | 36.99405 | 36.99405 | 6.4e-8 | 1e-6 | PASS |
| ka-key | sigma_key (MPa) | 84.55782 | 84.55782 | 3.7e-8 | 1e-6 | PASS |
| ka-shigley-7-1 | S_e (MPa) | 203.7091 | 203.7091 | 1.0e-7 | 1e-5 | PASS |
| ka-shigley-7-1 | n_f_min | 1.650015 | 1.650015 | 9.3e-8 | 1e-5 | PASS |
| ka-shigley-7-1 | n_y_min | 4.585856 | 4.585856 | 1.1e-7 | 1e-5 | PASS |

All 11 KAT values (3 of 3 cases) agree with physics and with the Shigley relations. The script also checks the n_c case against a closed form and an exact eigenvalue. The product passes its own 3 KATs through the probe.

For the same stresses, the other criteria give n_f = 1.892 (Gerber), 1.902 (ASME) and 1.581 (Soderberg). Goodman (1.650) lies between them.

## Extra probes (product vs independent)
All rows use the Probe tool. "Product" values are hard-coded in the script. Rows E1 to E10 agree to 1e-6 or better unless a finding is named.
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| E1 | stepped Ø25/30/40/30/25, two planes, ground, R 95 %, 3 sections | n_f_min / n_y_min | 1.23245 / 1.77198 | same | 3e-10 | PASS |
| E1 | same | R_A, R_B, M_max, θ_B, y_load | 1.9092, 2.9165 kN, 0.23332 kN·m, 2.1292e-3, 0.12545 mm | same | 2e-10 | PASS |
| E2 | hollow 60/40, S_ut 1500, hot-rolled, R 99.9 %, k_d 0.95, k_f 0.9 | S_e / n_f_min | 108.214 MPa / 1.60662 | same | 5e-10 | PASS |
| E3 | Ø300 as-forged, R 50 %, k_b held at 254 mm | S_e / n_f_min | 88.9575 MPa / 6.33855 | same | 4e-11 | PASS |
| E4 | near zero: 1 N load, Ø20 | n_f_min / y_load | 1428.75 / 8.2012e-4 mm | same | 2e-10 | PASS |
| E5 | key 14×9×40 on Ø50, S_y,key 350, n 2 | tau_key / sigma_key / L_key_req | 85.714 / 266.67 MPa / 60.952 mm | same | 1e-10 | PASS |
| E9 | lower bounds: Ø5, S_ut 300, R 99.9 % | n_f_min / n_y_min | 1.86683 / 2.15447 | same | 1e-10 | PASS |
| E10 | upper bounds: Ø300, 3 m, T 50 kN·m, S_ut 2000, two planes | n_f_min / n_y_min | 5.68893 / 38.1385 | same | 2e-10 | PASS |
| C1 | overhang, tip mass 40 kg | n_c vs exact | 2740.4 rpm | 2698.1 rpm | +1.6 % | PASS |
| C3 | overhang, tip mass 5 kg | n_c vs exact | 7976.6 rpm | 7189.7 rpm | +10.9 % | FAIL (-01) |
| C4 | overhang, tip mass 1 kg | n_c vs exact | 17493 rpm | 12991 rpm | +34.7 % | FAIL (-01) |
| C3 | same, n = 3800 rpm | n_c/n | 2.099, no warning | 1.892 | +10.9 % | FAIL (-01) |
| C5 | masses 25 and 40 kg between bearings | n_c / n_c_D vs exact | 10259 / 9750 rpm | 10195 rpm | +0.6 % / −4.4 % | PASS |
| E8 | shoulder Ø50 bore 46 to Ø40 solid, 2 kN | n_f_min | 5.3065 | 2.8700 | +85 % | FAIL (-02) |
| E6 | L 800, T 1 kN·m, x_T2 left at 400, section at 600 | n_f at 600 mm | 7.075 | 2.695 with x_T2 = 800 | +163 % | Trap (-03) |
| E5b | shaft S_y 250, key S_y 400, n 2 | L_key_req | 53.33 mm | 85.33 mm | −37 % | FAIL (-05) |

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| Beam model, two planes, vector sum | Euler-Bernoulli, M/θ/y combined | S2 frame solver, √(M_y² + M_z²) | Yes |
| Stress formulas, hollow shaft | 32 M d/(π(d⁴ − d_i⁴)) | modulus = π(d⁴ − d_i⁴)/d | Yes |
| K_f, K_fs on alternating and steady parts | Shigley §7-4 | applied to both; q defaults to 1 if blank | Yes |
| k_a constants, four finishes | Table 6-2 | 1.58/−0.085, 4.51/−0.265, 57.7/−0.718, 272/−0.995 | Yes |
| k_b, k_c, k_e, S_e′ | §6-9 | as above; Acklam quantile, error below 1e-9 | Yes |
| k_b above 254 mm | 0.6 to 0.75 band | held at 0.6330, warning issued | Yes (see -09) |
| Fatigue and yield criteria | Goodman and Langer | Goodman and Langer only | Yes (see -08) |
| Shoulder section | worse side governs | smaller outer diameter governs | No (-02) |
| Torque on a section | from x_T1 to x_T2 | inclusive span; default x_T2 = 400 mm | Trap (-03) |
| Loads in bending | forces only | mass column and self-weight excluded | Undocumented (-04) |
| Rayleigh quotient | g Σ m y/Σ m y², signed y | uses \|y\| in the numerator | No (-01) |
| Dunkerley | Σ m δ_ii, lower bound | all lumped nodes; 1 % to 7 % below exact | Yes |
| Critical-speed warning | n_c/n < 2 | uses Rayleigh, the upper bound | Unconservative (-01) |
| Keys | 2T/(d w L), 4T/(d h L), 0.577 S_y | same; full T at any x_key | Yes (see -05) |
| Guards | geometry, S_y > S_ut, d > 254 mm | all fire; tested with five bad inputs | Yes |
| Units | mm, kN, MPa, rpm | SI inside, rpm out | Yes |

## Findings
| ID | Severity | Finding | Evidence | Suggested fix (for Track S) |
|---|---|---|---|---|
| P1-mech.shaft-01 | High | The Rayleigh sum uses \|y\| for the weights. With an overhung mass the span deflects upward, so the numerator is too large. n_c, r_c, the warning and port n_c read high. | Bearings at 100 and 500 mm on a Ø40 shaft of 700 mm, tip mass 5 kg: product 7977 rpm, exact 7190 rpm. At 3800 rpm the ratio shows 2.10 and no warning, but the true ratio is 1.89. A 1 kg mass gives +35 %. | Use signed y in the numerator. Use the lower of Rayleigh and Dunkerley for r_c, the warning and port n_c. Add an overhung-mass test. |
| P1-mech.shaft-02 | Medium | A shoulder is checked on the side with the smaller outer diameter. With different bores that side can be the stronger one. | Ø50 bore 46 to Ø40 solid, 2 kN at the shoulder: product uses Ø40, σ_a 31.8 MPa, n_f 5.31. The hollow side has σ_a 57.5 MPa and n_f 2.87. | Evaluate both sides of a step and keep the lower n_f. |
| P1-mech.shaft-03 | Medium | Defaults x_T1 = 0 and x_T2 = 400 mm suit the 400 mm example only. Sections beyond 400 mm silently carry no torque. | L 800 mm, T 1 kN·m, section at 600 mm: n_f 7.08 against 2.70 with x_T2 = 800. Only a generic "default used" notice appears. | Default x_T2 to the shaft length. Warn when T > 0 and a section or key lies outside the span. |
| P1-mech.shaft-04 | Medium | The "mass carried" column adds no weight to reactions, moment or deflection. Shaft self-weight is also absent. Neither the label nor the assumptions say so. | ka-simply-supported: 20 kg at mid-span gives M_max = 0.2 kN·m = PL/4, not 0.2196. A Ø300 × 3 m shaft weighs 16.3 kN. | State the assumption in the label and assumption list. Offer an option to add weights as vertical loads. |
| P1-mech.shaft-05 | Low | Key bearing uses the key yield strength only. The shaft S_y is on the sheet but unused. | Shaft S_y 250, key S_y 400, T 1.2 kN·m, Ø50, 14×9: L_key_req 53.3 mm. Using 250 MPa gives 85.3 mm. | Use min(S_y,key, S_y,shaft) for bearing, and add a hub strength input. |
| P1-mech.shaft-06 | Low | The spec says K_t comes "from records or fit equations". No fit equation exists. K_t, K_ts, q and q_s are typed in. | Template and spec text against the input list. | Reword the spec and assumption, or add the fit equations. |
| P1-mech.shaft-07 | Low | The ka-shigley-7-1 source cites Example 7-1. I recall these data (28 mm, 142.4 N·m, 124.3 N·m, 735/574 MPa) from the shoulder example numbered 7-2. I cannot check the book. | KAT source text. | Owner to confirm the example number against the book page and fix the text. |
| P1-mech.shaft-08 | Info | Only DE-Goodman and first-cycle yield are offered. Soderberg is 4 % lower, Gerber and ASME are 15 % higher. | See the other-criteria line above. | None needed. Consider a criterion choice. |
| P1-mech.shaft-09 | Info | Lumped-mass Rayleigh for self-mass only reads 1.2 % above exact. Holding k_b at 254 mm is 2.8 % high at Ø300. Both are minor and the k_b case warns. | E1 self-mass row, E3 row. | None needed. |

## Notes for the owner's manual test
- Enter the ka-simply-supported data (Ø30, 400 mm, 2 kN, 20 kg, ρ = 0). Expect θ_A 2.430 mrad, y 0.3240 mm and n_c 5305 rpm.
- Enter the Shigley shoulder data (Ø28, 142.4 N·m, 124.3 N·m, 735/574, R 99 %, K_t 1.68, K_ts 1.42, q 0.85, q_s 0.92). Expect S_e 203.7 MPa, n_f 1.650, n_y 4.586. My Gerber, ASME and Soderberg values are 1.89, 1.90 and 1.58. Compare them with the book.
- Try the overhang: Ø40 × 700 mm, bearings at 100 and 500 mm, 5 kg at 700 mm, ρ 7850. Expect the product at 7977 rpm and Dunkerley at 7004 rpm. An exact solution gives 7190 rpm.
- Try L = 800 mm with T = 1 kN·m and a section at 600 mm. Check the torque shown in the section line.
- Run a key test: Ø50, 14×9×40, T 1.2 kN·m. Expect 85.71 MPa shear and 266.67 MPa bearing.
