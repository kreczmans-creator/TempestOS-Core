# struct.connection-ec3 — independent re-derivation (Track P, P1)
Calculator: Bolts and fillet welds (EN 1993-1-8), version 1.0.0. Source file: src/Tempest.Calc/Tempest.Calc.Structural/ConnectionCalculator.cs. Spec: docs/calcs/structural/struct.connection-ec3.md.
Script: tools/verification/struct.connection-ec3.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS

Every printed value I probed agrees with my own derivation. The findings are checks that EN 1993-1-8 asks for and the sheet does not make. One finding concerns the legacy picker in ADR-0169.

## Method re-derived
- Bolt strengths: f_ub = 400, 400, 500, 500, 600, 800, 1000 MPa for classes 4.6 to 10.9. Source: EN 1993-1-8:2005, Table 3.1.
- Stress area: A_s = π/4((d₂ + d₃)/2)² with d₂ = d − 0.649519 P and d₃ = d − 1.226869 P. Source: ISO 898-1:2013, cl. 9.1. Pitches from ISO 261.
- Shear: F_v,Rd = α_v f_ub A n_s/γ_M2. α_v = 0.6 for 4.6, 5.6 and 8.8, or for the shank in the plane. α_v = 0.5 for 4.8, 5.8, 6.8 and 10.9 with threads in the plane. Table 3.4.
- Bearing: F_b,Rd = k₁ α_b f_u d t/γ_M2. α_b = min(α_d, f_ub/f_u, 1). α_d = e₁/3d₀ at the end and p₁/3d₀ − 1/4 inside. k₁ = min of the e₂ and p₂ terms and 2.5. Table 3.4.
- Tension and punching: F_t,Rd = 0.9 f_ub A_s/γ_M2. B_p,Rd = 0.6π d_m t f_u/γ_M2, with d_m the mean of across-flats and across-corners. Combined: F_v/F_v,Rd + F_t/(1.4 F_t,Rd) ≤ 1. Table 3.4.
- Block tearing: V_eff,1 = f_u A_nt/γ_M2 + f_y A_nv/(√3 γ_M0), and V_eff,2 with 0.5 on the first term. Cl. 3.10.2.
- Welds: f_vw,d = f_u/(√3 β_w γ_M2) and F_w,Rd = f_vw,d a. Directional: √(σ⊥² + 3(τ⊥² + τ∥²)) ≤ f_u/(β_w γ_M2) and σ⊥ ≤ 0.9 f_u/γ_M2. Cl. 4.5.3.2 and 4.5.3.3. β_w from Table 4.1.
- Units: mm, N, MPa, kN for bolts, N/mm for weld force.

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-m20-88-shear-bearing | A_s (mm²) | 244.794 | 244.7944 | 1.6e-6 | 1e-5 | PASS |
| ka-m20-88-shear-bearing | F_v_Rd (kN) | 94.00105 | 94.00104 | 9.1e-8 | 1e-6 | PASS |
| ka-m20-88-shear-bearing | F_t_Rd (kN) | 141.0016 | 141.0016 | 2.7e-7 | 1e-6 | PASS |
| ka-m20-88-shear-bearing | F_b_Rd (kN) | 99.39394 | 99.39394 | 6.1e-9 | 1e-6 | PASS |
| ka-m20-88-shear-bearing | B_p_Rd (kN) | 199.8265 | 199.8265 | 1.5e-7 | 1e-6 | PASS |
| ka-m20-88-shear-bearing | u_vt | 0.6838832 | 0.683883 | 2.9e-7 | 1e-6 | PASS |
| ka-m20-109-threads | alpha_v | 0.5 | 0.5 | 0 | 1e-12 | PASS |
| ka-m20-109-threads | F_v_Rd (kN) | 97.91776 | 97.91775 | 8.7e-8 | 1e-6 | PASS |
| ka-weld-s275 | f_vw_d (MPa) | 222.7893 | 222.7893 | 8.8e-8 | 1e-6 | PASS |
| ka-weld-s275 | F_w_Rd (N/mm) | 935.715 | 935.715 | 2.4e-8 | 1e-6 | PASS |
| ka-p363-m20-88 | F_v_Rd (kN) | 94.1 | 94.001 | 1.1e-3 | 1e-2 | PASS |
| ka-p363-m20-88 | F_t_Rd (kN) | 141 | 141.002 | 1.1e-5 | 1e-2 | PASS |
| ka-p358-fin-plate | F_b_Rd (kN) | 99.4 | 99.394 | 6.1e-5 | 1e-2 | PASS |

All 13 KAT values (5 of 5 cases) agree. The two SCI cases stay Unconfirmed: I recall similar published values but did not check the books. A_s needs the exact ISO constants (0.649519, 1.226869). The rounded constants give a 1.7e-6 difference in F_v,Rd.

## Extra probes (product vs independent)
The script holds 8 full probes, a 7-class by 2-state α_v table, 8 sizes and 5 steel grades. Every PASS row agrees to 2e-6 or better. The steel table is the exception at 1.5e-5, because the product prints five digits.
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| P1 | M16 4.6, shank, n_s 2, S235, 8 mm | F_v_Rd / F_b_Rd | 77.208 / 59.733 kN | same | <1e-7 | PASS |
| P2 | M24 10.9, S355, inner bolt, p₁ 65, F_t 60 | α_b / u_vt | 0.58333 / 1.23268 | same | <1e-7 | PASS |
| P3 | M12 8.8, 3 mm plate, F_t 30 | B_p_Rd / u_t | 35.969 kN / 0.83406 | same | <1e-7 | PASS |
| P4 | M36 5.6, hole 39, S460, 40 mm | α_b / F_b_Rd | 0.86111 / 1364 kN | same | <1e-7 | PASS |
| P5 | M20 4.6 in S355 (f_ub/f_u governs) | α_b / F_b_Rd | 0.78431 / 320 kN | same | <1e-7 | PASS |
| P6 | M27 8.8, d₀ 33, p₂ 80 | k₁ / u_b | 1.69394 / 1.09989 | same | <1e-7 | PASS |
| P8 | weld S355, a 5, σ⊥ −150, τ⊥ 90, τ∥ 60 | f_vw_d / u_dir / u_perp | 251.468 MPa / 0.55102 / 0.42517 | same | <1e-7 | PASS |
| P9 | block tearing, A_nt 300, A_nv 900 | V_eff_Rd | 241.294 kN | 241.294 kN | <1e-7 | PASS |
| α_v table | M20, 7 classes, threads and shank | F_v_Rd | 47.0 to 150.8 kN | same | <2e-6 | PASS |
| Sizes | M12 to M36, 8.8 | A_s, F_v, F_t, B_p | see script | same | <2e-6 | PASS |
| Steels | S235 to S460, f_u 410 | f_vw_d | 236.71, 222.79, 210.41, 189.37, 189.37 MPa | same | <1e-4 | PASS |
| F1 | M20 8.8, d₀ 24 (oversize) | F_b_Rd | 91.11 kN | 79.52 kN | +14.6 % | FAIL (-01) |
| F2 | P9 data, eccentric group | V_eff | 241.29 kN | 192.09 kN | +25.6 % | FAIL (-02) |
| F3 | M20 8.8, e₁ = e₂ = 60, single lap, one row | F_b_Rd | 149.09 kN | 98.40 kN | +51.5 % | FAIL (-03) |
| F4 | legacy picker, M20 10.9, SF 1 | capacity | 157.08 kN | 122.40 kN | +28.3 % | FAIL (-04) |

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| f_ub, f_yb | Table 3.1 | first figure × 100; f_yb = f_ub × second/10 | Yes |
| A_s | ISO 898-1 formula | exact constants 0.649519, 1.226869 | Yes (worksheet text rounds, -08) |
| α_v per class | 0.6 / 0.5 by class | `threads` and class test; checked for 14 combinations | Yes |
| Shear area | A_s or gross | A_s with threads, π d²/4 without | Yes |
| α_d, α_b | end, inner, f_ub/f_u, 1 | min of end and inner; capped by f_ub/f_u and 1 | Yes (conservative) |
| k₁ | edge: e₂ term; inner: p₂ term | min of both terms and 2.5 | Yes (conservative, stated) |
| k₂ | 0.9, or 0.63 countersunk | 0.9 only; no countersunk input | Scope gap (-09) |
| Punching d_m | mean of flats and corners, smaller of head and nut | ISO 4014/4032 across-flats table | Yes |
| Combined | F_v/F_v,Rd + F_t/(1.4F_t,Rd) | same; tension utilisation uses min(F_t,Rd, B_p,Rd) | Yes |
| Hole size | normal, oversize, slotted | any d₀ 13 to 40 mm; no factor | No (-01) |
| Block tearing | V_eff,1 and V_eff,2 | V_eff,1 only, labelled concentric | Partial (-02) |
| Single-lap cap | 1.5 f_u d t/γ_M2 | not applied | No (-03) |
| Spacing | Table 3.3 minima and maxima | minima only, as a pass/fail check | Partial (-06) |
| Weld β_w | 0.8, 0.85, 0.9, 1.0, 1.0 | same for S235 to S460 | Yes |
| Directional method | two conditions | \|σ⊥\| used in the second; conservative | Yes |
| Guards | d₀ > d; block and weld inputs | block "hole"; warnings for partial inputs | Yes; e₂ case in -05 |
| Governing mode | bolt modes | shear, bearing, tension, combined; no block, weld or spacing | Yes (label says bolt) |

## Findings
| ID | Severity | Finding | Evidence | Suggested fix (for Track S) |
|---|---|---|---|---|
| P1-struct.connection-ec3-01 | Medium | Oversize or slotted holes are accepted with no reduction and no warning. EN 1993-1-8 cl. 3.6.1 and the Table 3.4 notes ask 0.8 (oversize) or 0.6 (slot across the load) on F_b,Rd. | M20 8.8, 10 mm S275, e₁ = e₂ = 40, d₀ 24: product 91.1 kN. Required at most 0.8 × 99.4 = 79.5 kN. | Warn when d₀ exceeds the normal clearance. Add a hole-type choice with the 0.8 and 0.6 factors. |
| P1-struct.connection-ec3-02 | Medium | Block tearing offers V_eff,1 only. An eccentric group needs V_eff,2, which is lower. The output label says concentric but the spec limitations do not. | A_nt 300, A_nv 900, f_u 410, f_y 275: V_eff,1 241.3 kN, V_eff,2 192.1 kN. | Add a loading choice (concentric or eccentric). List the limit in the spec. |
| P1-struct.connection-ec3-03 | Medium | The cap F_b,Rd ≤ 1.5 f_u d t/γ_M2 for single-lap joints with one bolt row (cl. 3.6.1) is absent. It binds when k₁α_b exceeds 1.5. | M20 8.8, 10 mm S275, e₁ = e₂ = 60: product 149.1 kN, cap 98.4 kN. | Add a joint-type input, or warn when n_s = 1 and k₁α_b > 1.5. |
| P1-struct.connection-ec3-04 | High | ADR-0169 offers α_v f_ub for threads in the plane but multiplies by the gross shank area. The legacy module is 28 % above the S4 result for M20. | Pinned test: M20 class 10.9 gives π/4·400·500 N = 157.1 kN at SF 1. S4 gives 0.5·1000·244.8 = 122.4 kN. Class 8.8: 150.8 against 117.5 kN. | Use A_s with the thread-plane strengths, or label the picker as shank-in-plane and use 0.6. Better, retire the module as planned. |
| P1-struct.connection-ec3-05 | Low | When k₁ is zero or negative (M20, e₂ below 13.4 mm) the run stops with "Division by zero". The spacing check never shows. | e₁ 40, e₂ 12: block "engine: Division by zero." | Return u_b = ∞ with the spacing failure shown. |
| P1-struct.connection-ec3-06 | Low | Table 3.3 maxima (4t + 40 mm, 14t or 200 mm) are not checked. | e₁ = e₂ = 500 mm, M36: spacing "pass". | Add the maxima as a warning. |
| P1-struct.connection-ec3-07 | Low | Plate f_y above f_u, or an f_u that does not suit the steel grade, is accepted. | f_y 460 with f_u 340 runs with no notice. | Block f_y > f_u. Warn when f_u is outside the grade range. |
| P1-struct.connection-ec3-08 | Low | The worksheet shows d₂ = d − 0.6495P and d₃ = d − 1.2269P. The code uses 0.649519 and 1.226869. | A_s differs by 1.6e-6. The KAT value 244.794 is also rounded. | Show the exact constants. |
| P1-struct.connection-ec3-09 | Info | Countersunk bolts (k₂ = 0.63) are out of scope and nothing blocks their use. Default M12 hole is 14 mm; EN 1090-2 gives 13 mm as I recall its table, so the default is conservative. | Limitations text; hole rule in the code. | Optional: add a countersunk choice. |

## Notes for the owner's manual test
- M20 8.8, 10 mm S275 (f_u 410), e₁ = e₂ = 40, p₁ = p₂ = 70, d₀ 22. Expect A_s 244.8 mm², F_v,Rd 94.00, F_t,Rd 141.00, F_b,Rd 99.39, B_p,Rd 199.83 kN.
- Switch to class 10.9 with threads. Expect α_v 0.5 and F_v,Rd 97.92 kN. With the shank in the plane expect α_v 0.6 and 150.80 kN.
- Weld in S275 with a = 4.2 mm and f_u 410. Expect f_vw,d 222.79 MPa and F_w,Rd 935.7 N/mm. The β_w values are 0.80, 0.85, 0.90, 1.00 and 1.00 for S235 to S460.
- Enter d₀ 24 and compare F_b,Rd with 0.8 × the d₀ 22 value. Enter e₁ = e₂ = 60 and compare F_b,Rd with 1.5 f_u d t/γ_M2.
- Test the legacy picker with M20 class 10.9 and compare it with the 97.9 kN of this sheet.
