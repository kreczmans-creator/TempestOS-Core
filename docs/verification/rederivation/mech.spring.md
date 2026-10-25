# mech.spring — independent re-derivation (Track P, P1)
Calculator: Helical springs: compression, extension, torsion, version 1.1.0. Source file: src/Tempest.Calc/Tempest.Calc.Mechanical/SpringCalculator.cs (helper: FatigueRelations.cs). Spec: docs/calcs/mechanical/mech.spring.md.
Script: tools/verification/mech.spring.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS

All 8 known-answer cases (70 expected values) and 14 extra probes agree with the independent script to better than 1e-9 relative. The findings concern guards, labels and conservatism, not arithmetic.

## Method re-derived
- Rate: k = G d⁴/(8 D³ n) in N/mm with G in N/mm². Source: Shigley, 10th ed., Ch. 10; EN 13906-1/-2.
- Shear factor: Bergsträsser (C + 0.5)/(C − 0.75) and Wahl (4C − 1)/(4C − 4) + 0.615/C. Stress τ = k_c 8 F D/(π d³).
- Solid length: Shigley Table 10-1 (n_t = n + e_n, L_s = d (n_t + e_s)). EN 13906-1 form uses d_max and 1.5 d for closed unground ends.
- Pitch by end type: p = (L₀ − a d)/(n + b), derived from the end geometry. Agrees with Table 10-1 geometry.
- Minimum coil clearance S_a = (0.0015 D²/d + 0.1 d) n. Recalled from DIN 2089-1 / EN 13906-1; not confirmed against the standard.
- Buckling: Shigley 10th ed., Ch. 10: C₁′ = E/(2(E − G)), C₂′ = 2π²(E − G)/(2G + E), y_cr = L₀ C₁′ [1 − √(1 − C₂′/λ²)], λ = ν L₀/D.
- Surge: f = d/(2π n D²) √(G/2ρ). I also derived it as ½ √(k/m_wire); both agree.
- Extension: F = F₀ + k s. Hook bending factor K_A and side-bend factor K_B as in Shigley Ch. 10. Body length d (n + 1).
- Torsion: k_φ = E d⁴/(64 D n_a), n_a = n + (l₁ + l₂)/(3πD). This equals the EN 13906-3 constant 3667 per degree. Factors q = (C + 0.07)/(C − 0.75) and K_i = (4C² − C − 1)/(4C (C − 1)).
- Wound body: wire length constant, so D_w = D n/(n + φ/2π) and L_w = d (n + 1 + φ/2π).
- Fatigue: Goodman 1/n_f = τ_a/S_e + τ_m/S_su with S_su = f_su R_m (shear) or R_m (bending). Strength curve R_m = A/d^m.

## Known-answer cases
All 70 rows pass; the script prints every row. Summary per case:
| Case | Symbols checked | Worst rel. diff | KAT tol | Result |
|---|---|---|---|---|
| ka-compression | C, k, k_c, L_s, s_2, tau_2, F_c, tau_c, u_c, f_n, r_f, u_f | 2.7e-9 | 1e-9 to 1e-7 | PASS |
| ka-wahl-slender | k, F_1, F_2, k_c, L_s, y_cr, u_b, f_n, r_f, n_f | 3.3e-10 | 1e-9 to 1e-7 | PASS |
| ka-extension | k, s_1, s_2, L_s, tau_0, tau_2, sigma_A, tau_B, u_A, f_n, u_f | 1.1e-9 | 1e-9 to 1e-7 | PASS |
| ka-torsion | n_a, k_phi, K_i, phi_1, phi_2, sigma_2, D_w, L_w, u_2, u_f | 2.8e-9 | 1e-7 | PASS |
| ka-torsion-en (Unconfirmed) | n_a, q, sigma_2, u_2, u_f | 3.7e-12 | 1e-7 to 1e-9 | PASS |
| ka-squared-en (Unconfirmed) | k, n_t, L_s, S_a, L_n, L_2, s_c, s_n, u_s, u_n, F_c, u_c | 8.0e-13 | 1e-9 to 1e-7 | PASS |
| ka-rm-curve | Rm, u_2, u_c, u_f | 2.2e-12 | 1e-9 to 1e-7 | PASS |
| ka-shigley-10-3 (Unconfirmed, Secondary) | C, k, k_c, L_s, F_c, u_c | 3.6e-5 | 1e-4 to 2e-4 | PASS |

KAT physics notes. The Shigley 10-3 chain reproduces from first principles: k = 1.3142 N/mm, K_B = 1.1174, F_s = 167.855 N, L₀ = 162.719 mm. Music-wire R_m = 2211/2.5^0.145 = 1935.9 MPa agrees with the KAT, using the Table 10-4 constants as I recall them. I could not check the published manual values, so that case stays Unconfirmed. The two EN-based KATs (q, S_a/d_max) agree with the spec and with my recollection of EN 13906-1/-3, but the clauses need the owner's copy.

## Extra probes (product vs independent)
Full list (all symbols) is in the script. Product values were read with the probe tool.
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| P1 | comp, plain ends, Wahl, s 5/20 mm, d 3, D 24, n 6.5 | k / tau_2 / u_c | 9.0144 / 483.19 / 1.5001 | same | <1e-10 | PASS |
| P2 | squared, EN, d_max 3.05 | L_s / L_n / u_n | 30.5 / 34.322 / 0.62186 | same | <1e-10 | PASS |
| P3 | squared, Shigley form | L_s / u_c | 28.5 / 1.2978 | same | <1e-9 | PASS |
| P4 | slender, ν = 1, L₀ 200 | y_cr / u_b | 13.017 / 4.6094 | same | <1e-10 | PASS (WARN raised) |
| P5 | C = 3, F = 0 and 0.01 N, no correction | k_c / tau_c | 1 / 1697.65 | same | <1e-9 | PASS |
| P6 | d = 20, D = 160, surge ratio 2.0 | f_n / u_n | 39.845 / 1.0804 | same | <1e-9 | PASS |
| P7 | d = 1, D = 3 (C = 3), plain ground | k_c / y_cr | 1.58 / 2.6034 | same | <1e-9 | PASS |
| P8 | R_m from curve A 2000, m 0.15, ν = 2 | Rm / y_cr / u_f | 1377.69 / 61.93 / 1.129 | same | <1e-9 | PASS |
| P9 | extension by deflection, F₀ 30 N | tau_0 / sigma_A / u_f | 80.40 / 770.93 / 0.5222 | same | <1e-9 | PASS |
| P10 | extension, r_h2 = 0.8 mm (C₂ = 1.07) | K_A / tau_B / u_B | 2.6875 / 2218.3 / 2.7386 | same | <1e-9 | PASS |
| P11 | torsion EN q, angles 20° and 90°, legs 40 + 60 | k_phi / sigma_2 / phi_2 | 1.4123 / 931.55 / 90° | same | <1e-9 | PASS |
| P12 | torsion C = 4, K_i, M₁ = 0 | K_i / D_w / u_f | 1.2292 / 7.9606 / 0.39995 | same | <1e-9 | PASS |
| P13 | torsion d = 1, M 0.001 to 0.01 N·m | k_phi / sigma_2 / L_w | 0.018687 / 108.16 / 13.085 | same | <1e-9 | PASS |
| P14 | ka-compression with correction "None" | k_c / u_f | 1 / 0.29656 | same | <1e-9 | PASS (see finding 01) |

Guard probes (product behaviour is as the spec states): D ≤ d, E ≤ G, d_max < d, L₀ ≤ L_n, F_2 < F_1 and a curve R_m outside 50 to 5000 MPa all block. Index outside 4 to 20, d above 20 mm, N_c below 10⁴ and r_f below 13 warn.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| Rate, Bergsträsser, Wahl | Shigley Ch. 10 | same, SI units internally | Yes |
| Solid length and pitch, four end types | Table 10-1; EN form with d_max | same; plain ends keep (0, 1) in both forms | Yes |
| Buckling and stable limit | Shigley Ch. 10 | same; y_cr shown as "stable" when λ² < C₂′ | Yes |
| Surge, both ends fixed | Shigley Ch. 10 | same; warning at ratio < 13 | Yes (see 03) |
| Hook factors K_A, K_B; initial tension | Shigley Ch. 10 | same; force and deflection modes both handled | Yes |
| Torsion rate, legs, q, K_i, D_w, L_w | EN 13906-3; Shigley | same | Yes (see 05) |
| Goodman and S_su | Shigley Ch. 6 | same; u_fat = n_f,req·u_f is the check | Yes |
| Correction factor in fatigue | EN uses k in fatigue always | follows the "corr" choice, also "None" | No (01) |
| Buckling as a check | Safety limit | warning only, no check row | Partly (02) |
| Units | N, mm, MPa | SI inside; outputs in SI, shown in mm, MPa | Yes |

## Findings
| ID | Severity | Finding | Evidence | Suggested fix (for Track S) |
|---|---|---|---|---|
| P1-mech.spring-01 | Medium | Choosing "None (uncorrected)" also removes the curvature factor from the fatigue stresses. Fatigue then reads unconservative. | ka-compression inputs with corr = None: u_f 0.2966 against 0.3477 with Bergsträsser (14.7 % lower, n_f 3.37 against 2.88). No warning is issued. | Always use Bergsträsser or Wahl in the Goodman stresses. Apply "None" to the static stress only, or warn when None is chosen. |
| P1-mech.spring-02 | Low | A buckling failure is a warning, not a check row. | Probe P4: u_b = 4.61 (working deflection 60 mm, y_cr 13.0 mm). Rule "buckling" warns and the check list omits it. | Add `CheckUtilisation` on u_b, or state in the report that WARN is not part of the pass/fail summary. |
| P1-mech.spring-03 | Low | The surge limit f_n/f_op ≥ 13 is less strict than the 15 to 20 I recall from Shigley 10th ed., Sec. 10-9. | P6: ratio 1.99 warns, but a ratio of 14 passes silently. | Confirm the figure in the owner's copy. Cite its source in the rule text. Consider a higher limit. |
| P1-mech.spring-04 | Low | Solid-length stress uses L_c from d_max. A wire at nominal d reaches a shorter length, so the solid force is higher. | ka-squared-en: F_c 105.18 N, but 107.42 N with L_c from nominal d (+2.1 %). | Document that u_c is taken at the maximum solid length, or also report the value at nominal d. |
| P1-mech.spring-05 | Low | D_w and L_w use the full angle φ, which includes the leg deflection. Body winding is overstated by n_a/n. | ka-torsion: n_a/n = 1.066. The wound-diameter change is 6.6 % too large. The error is conservative. | Use the body rate E d⁴/(64 D n) for the winding angle, or note the conservatism. |
| P1-mech.spring-06 | Info | Torsion rate uses the theoretical constant 64 (EN 13906-3). Shigley adds a friction-based constant of 10.8 per turn, about 6 % stiffer. | Spec and KAT use 64. | None needed. Mention the Shigley value in the assumptions if wanted. |
| P1-mech.spring-07 | Info | Extension rate uses n as entered. Hook flexibility (Shigley adds G/E coils, about 0.39) is not in the rate. The spec says so. | ka-extension: n = 20 gives k = 1.953 N/mm; with n + 0.39 the rate is 1.9 % lower. | None needed. Consider a note beside the input. |
| P1-mech.spring-08 | Info | S_a and the d_max rows are one formula for all loadings. I recall that EN 13906-1 distinguishes static and dynamic loading. This is unconfirmed. | Spec marks both Unconfirmed. | The owner should check clause and constants in the standard. |
| P1-mech.spring-09 | Info | Material curve coefficient A has the unit MPa·mm^m but is entered as "MPa". A user who converts A to another length unit gets a wrong R_m. | Record kind has unit null with the note "the unit follows m". | Label Rm_A as MPa·mm^m, or accept only that form. |

## Notes for the owner's manual test
- Compression, d 2, D 16, n 8, G 80 GPa, L₀ 45 mm: expect k = 4.8828 N/mm, k_c = 1.1724, L_s = 20 mm for squared and ground ends.
- Switch "corr" to None on that case: u_f falls from 0.3477 to 0.2966. Compare with a hand Goodman using k_c (finding 01).
- Slender case d 3, D 30, n 12, plain ground, L₀ 200, ν 1, s₂ 60 mm: expect y_cr = 13.017 mm and a buckling WARN.
- Torsion d 2, D 16, n 6, legs 30 + 30 mm, M₂ 0.15 N·m, K_i: expect k_φ = 0.50310 N·m/rad, σ₂ = 210.6 MPa. With q the stress is 212.6 MPa.
- Squared unground ends with d_max 2.04: L_s = 23.46 mm, S_a = 3.136 mm. Check the 1.5 d and S_a constants in EN 13906-1.
