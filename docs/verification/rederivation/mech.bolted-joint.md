# mech.bolted-joint — independent re-derivation (Track P, P1)
Calculator: Bolted joint (simplified VDI 2230), version 1.0.0. Source file: src/Tempest.Calc/Tempest.Calc.Mechanical/BoltedJointCalculator.cs. Spec: docs/calcs/mechanical/mech.bolted-joint.md.
Script: tools/verification/mech.bolted-joint.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS (one High added in review: -R1)

## Method re-derived
- Thread geometry: d_2 = d − 0.6495 P, d_3 = d − 1.2269 P, A_s = π/4 ((d_2 + d_3)/2)². Source: ISO 898-1 (2013) clause 9.1.6. Units mm, mm².
- Preload from torque: F_M = M_A/(K d), or F_M = M_A/(0.16 P + 0.58 d_2 μ_G + 0.5 D_Km μ_K) with D_Km = (d_w + d_h)/2. Source: Shigley 9th ed., preload and torque (§8-7 as cited in the spec); VDI 2230 Part 1 (2015) step R13 (tightening torque).
- Scatter: F_M,min = F_M/α_A (steps R5 and R6). The product treats F_M as the maximum preload.
- Bolt compliance: δ_S = 0.4d/(E A_N) + l_k/(E A_s) + 0.5d/(E A_d3) + 0.4d/(E A_N). Source: Shigley 9th ed. eq. 8-17 (number as cited in the spec); VDI 2230 step R3.
- Member stiffness: two 30° frusta of height l_k/2, k_f from Shigley 9th ed. eq. 8-20 (as cited in the spec), k_P = k_f/2. Shigley's relation uses one diameter d in both the prefactor and the log; the sheet mixes d and d_h (-03). Units N/mm, equal to kN/m.
- Load factor and embedding: Φ = n k_S/(k_S + k_P); F_Z = f_Z k_S k_P/(k_S + k_P). Source: VDI 2230 steps R3 and R4.
- Residual clamp F_KR = F_M,min − (1 − Φ)F_A − F_Z (steps R2 and R5); maximum bolt load F_S,max = F_M + Φ F_A.
- Equivalent stress: σ_red = √(σ_z² + 3(k_τ τ)²) with k_τ = 1.0 at assembly (step R7) and 0.5 in service (step R8). τ = M_G/W_p, W_p = π/16 d_S³, d_S = (d_2 + d_3)/2.
- Fatigue σ_a = Φ F_A/(2 A_s). Stripping m_min = S F_S,max/(0.5 π d τ_B).

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-m12-88-nut-factor | A_s | 84.27 | 84.2665 | 4.1e-05 | 1e-03 | PASS |
| ka-m12-88-nut-factor | F_M | 38.75 | 38.75 | 1.8e-16 | 1e-04 | PASS |
| ka-m12-88-nut-factor | sigma_M | 459.8 | 459.85 | 1.1e-04 | 2e-03 | PASS |
| ka-shigley-stiffness | k_P | 1.9078e6 | 1.90782e6 | 1.0e-05 | 2e-03 | PASS |
| ka-shigley-stiffness | k_S | 3.2119e5 | 3.21187e5 | 1.0e-05 | 2e-03 | PASS |
| ka-shigley-stiffness | Phi | 0.07205 | 0.0720471 | 4.1e-05 | 3e-03 | PASS |
| ka-shigley-stiffness | F_M | 40.69 | 40.6899 | 1.8e-06 | 2e-03 | PASS |

All 7 KAT values agree. The KAT arithmetic is correct for the formulas as written. The second KAT uses the bolt diameter in the k_f numerator and the hole diameter in the logarithm. See P1-mech.bolted-joint-03.

## Extra probes (product vs independent)
The script holds 11 probes (default, both methods, n = 0 and 1, separation, scatter 1, M64, near-zero, hole limits, thin clamp, soft members, loose hole). All 15 outputs agree for every probe, except nu_M and sigma_red_B (see -01). The table shows the main symbols.

| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| p1 | M16x2, nut factor, 150 N·m, F_A 20 kN | Phi | 0.0774608 | 0.077461 | 2.0e-06 | PASS |
| p1 | M16x2, nut factor, 150 N·m, F_A 20 kN | F_KR | 6.49844 | 6.49843 | 8.7e-07 | PASS |
| p1 | M16x2, nut factor, 150 N·m, F_A 20 kN | nu_M | 0.583473 | 0.559854 | 4.2e-02 | FAIL (see -01) |
| p1 | M16x2, nut factor, 150 N·m, F_A 20 kN | sigma_red_B | 328.659 | 324.424 | 1.3e-02 | FAIL (see -01) |
| p2 | default, n = 1 | Phi | 0.144094 | 0.144094 | 2.5e-06 | PASS |
| p2 | default, n = 1 | F_KR | 13.8482 | 13.8482 | 1.9e-06 | PASS |
| p2 | default, n = 1 | nu_M | 0.968657 | 0.917951 | 5.5e-02 | FAIL (see -01) |
| p2 | default, n = 1 | sigma_red_B | 536.435 | 527.225 | 1.7e-02 | FAIL (see -01) |
| p3 | default, n = 0 | Phi | 0 | 0 | 0.0e+00 | PASS |
| p3 | default, n = 0 | F_KR | 12.4072 | 12.4072 | 1.8e-06 | PASS |
| p4 | default, F_A 100 kN | Phi | 0.0720469 | 0.0720471 | 2.5e-06 | PASS |
| p4 | default, F_A 100 kN | F_KR | -70.3881 | -70.3881 | 5.7e-07 | PASS |
| p4 | default, F_A 100 kN | F_S_max | 47.8946 | 47.8946 | 1.4e-06 | PASS |
| p5 | default, alpha_A 1, f_Z 0 | Phi | 0.0720469 | 0.0720471 | 2.5e-06 | PASS |
| p5 | default, alpha_A 1, f_Z 0 | F_KR | 31.4103 | 31.4104 | 1.6e-06 | PASS |
| p6 | M64x6, l_k 300, 8 kN·m, F_A 500 kN | Phi | 0.0589982 | 0.0589982 | 1.4e-06 | PASS |
| p6 | M64x6, l_k 300, 8 kN·m, F_A 500 kN | F_KR | 9.28223 | 9.28264 | 4.4e-05 | PASS |
| p7 | default, F_A 0, M_A 0.1 N·m | Phi | 0.0720469 | 0.0720471 | 2.5e-06 | PASS |
| p7 | default, F_A 0, M_A 0.1 N·m | F_KR | -2.99216 | -2.99217 | 2.5e-06 | PASS |
| p8 | default, d_h 12.01 | Phi | 0.0634397 | 0.0634399 | 2.5e-06 | PASS |
| p8 | default, d_h 12.01 | F_KR | 13.3709 | 13.3709 | 1.8e-06 | PASS |
| p9 | default, l_k 1, d_w 200 | Phi | 9.84057e-05 | 9.84062e-05 | 5.1e-06 | PASS |
| p9 | default, l_k 1, d_w 200 | F_KR | -16.1167 | -16.1168 | 3.9e-06 | PASS |
| p10 | default, E_P 70 GPa, l_k 25 | Phi | 0.181581 | 0.181581 | 2.0e-06 | PASS |
| p10 | default, E_P 70 GPa, l_k 25 | F_KR | 14.1268 | 14.1268 | 1.9e-06 | PASS |
| p11 | default, d_h 15, d_w 18.5 | Phi | 0.0889719 | 0.0889721 | 2.4e-06 | PASS |
| p11 | default, d_h 15, d_w 18.5 | F_KR | 12.4821 | 12.4821 | 1.8e-06 | PASS |

The nu_M and sigma_red_B rows fail only through the polar-modulus basis. The script reproduces the product to 1e-6 when it uses d_3 in W_p.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| Stress area, d_2, d_3 | ISO 898-1 §9.1.6 | Same, constants 0.6495 and 1.2269 | Yes |
| Preload, both methods | Spec method; VDI 2230 R13 | Same; D_Km = (d_w + d_h)/2 | Yes |
| Unit handling | mm, N·m, GPa, µm | Engine converts; outputs match script in kN, MPa | Yes |
| Bolt stiffness | Shigley 8-17 style sum | Same four terms | Yes |
| Frustum stiffness | Shigley 8-20 | Bolt d in numerator, d_h in the log | Partly (-03) |
| Φ, F_Z, F_KR, F_S,max | VDI R3, R4, R2 and R5 | Same | Yes |
| Torsion modulus W_p | VDI uses d_S | Uses d_3 | No (-01) |
| Assembly check | VDI R7, full torsion, 90 % | σ_red,M/R_p02 warns above 0.9 | Yes |
| Working-state yield | 50 % torsion | Same | Yes |
| Separated joint | Bolt carries all of F_A | Linear formula continues | No (-02) |
| Stripping area | 0.5 π d per unit length | Same; text says "half the pitch" | Wording (-04) |
| Geometry guard | d < d_h < d_w | Block when not true | Yes |
| Utilisation when F_KR ≤ 0 | Not defined | u_clamp negative or −0 | Misleading (-05) |
| Input limits | Spec | Hard limits are wide; d_w has no cap against l_k or member width | Info |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) | Status @ 0772fcf8 |
|---|---|---|---|---|---|---|
| P1-mech.bolted-joint-01 | Low | Medium (upgraded: conservative, but τ_M is 16 % high and nu_M 5.5 % high at default; it matters once -R1 makes nu_M a check) | Thread torsion uses W_p = π/16 d_3³. VDI 2230 uses the mean diameter d_S. This is conservative. | Default inputs: nu_M 0.9687, independent 0.9180 (+5.5 %). sigma_red_B 536.4 vs 527.2 MPa (+1.7 %) at n = 1. The yield check moves by under 2 %. | Use d_S = (d_2 + d_3)/2 in W_p, or state the d_3 choice in the spec. The spec does not define τ_M. | Open. Default nu_M still 0.9687 (independent d_S basis 0.9180). All 11 probe cases give the same values as before. |
| P1-mech.bolted-joint-02 | Medium | Medium (confirmed: reproduced; the separation check fails, so the overall verdict is still FAIL) | After the joint separates, F_S,max, yield, fatigue and stripping use the pre-separation linear law. They understate the bolt load. The "No separation" check fails and a warning shows. | Default inputs, F_A = 100 kN: F_KR −70.4 kN; F_S,max 47.9 kN, but the bolt carries at least 100 kN. Yield u 0.94 and fatigue u 0.85 both pass. | When F_M − F_Z − (1 − Φ)F_A < 0, set F_S,max = max(F_M + Φ F_A, F_A), or mark the three checks "not valid after separation". | Open. Probe F_A 100 kN: F_KR −70.388 kN, F_S,max still 47.895 kN, yield u 0.939 and fatigue u 0.855 pass. Only the separation check fails. |
| P1-mech.bolted-joint-03 | Low | Low (confirmed: Shigley Eq. 8-20 uses one diameter in both places; see kat-sources C2) | The k_f numerator uses the bolt diameter d. The log uses the hole diameter d_h. Integrating the frustum gives d_h in both places. | Default: k_P 1.908e6 vs 2.067e6 kN/m. F_KR 13.128 vs 13.046 kN (+0.6 %, unconservative). Hole 15 mm: F_KR 12.482 vs 12.223 kN (+2.1 %). | Use d_h in the numerator, or keep Shigley's form and restrict the hole to a small clearance over d. Update the KAT if changed. | Open. Default k_P is still 1.908e6 kN/m (hole-diameter form 2.067e6). F_KR 13.128 kN. |
| P1-mech.bolted-joint-04 | Low | Low (confirmed) | The assumption text says the shear area is "half the pitch per unit length". The formula uses 0.5 π d per unit length and has no pitch. | Source lines for m_min; spec limitation text repeats "half-pitch". | Reword both texts to "50 % of the circumference on the nominal diameter". | Open. Source and spec text are unchanged ('half the pitch'). |
| P1-mech.bolted-joint-05 | Low | Low (confirmed) | Residual-clamp utilisation is negative when F_KR < 0. With F_KR_req = 0 and F_KR < 0 that check passes with u = −0. | F_A = 100 kN: u_clamp −0.071 (FAIL). Same case with F_KR_req = 0: u −0, pass. F_A = 24.3 kN: u −35.2. | Report u_clamp as "not met" when F_KR ≤ 0. Do not show negative utilisation. | Open. Probe F_A 100 kN with F_KR_req 0: the residual-clamp check still passes with u −0. |
| P1-mech.bolted-joint-06 | Info | Info (confirmed) | Default inputs raise the preload-yield warning (nu_M 0.969). In nut-factor mode the torsion stress still uses μ_G, which the user may not expect. | Default run output. | Optional: note both points in the worksheet. | Open (Info). The default run still raises 'preload-yield'. |
| P1-mech.bolted-joint-07 | Low | Low (confirmed; R-numbers from memory, owner to confirm against licensed copy) | Step labels in the worksheet differ from the VDI 2230:2015 numbering as I know it. F_M,min cites R2 (R5 and R6 hold the preloads). F_KR cites R7 (R2 and R5). Assembly equivalent stress cites R8 (R7). | Source strings in BoltedJointCalculator.cs, for example "VDI 2230 R7" on F_KR. | Check each R-number against the standard and correct the labels. | Open. The R-number strings in the source are unchanged. |
| P1-mech.bolted-joint-R1 | — | High | The assembly stress (VDI 2230 R7) is only a warning. With nu_M above 1 every check passes. The warning text still says "above 90 %". | Default inputs with M_A 95 N·m: nu_M 1.150 (σ_red,M 736 MPa against R_p02 640 MPa). All five checks pass; u_yield 0.978. | Add a utilisation check on the assembly state, nu_M against the permitted ν (0.9 by default). Keep the warning. | Open. Probe M_A 95 N·m: nu_M 1.150, all five checks pass (u_yield 0.978). Only the 'preload-yield' warning shows. |
| P1-mech.bolted-joint-R2 | — | Medium | F_M is labelled the maximum preload, but μ_G, μ_K and K are not labelled as minimum values. A user who enters mean friction understates the maximum preload. | VDI 2230 R13 computes the torque with the minimum friction values. With α_A 1.6 and a mean μ, the true maximum preload is about 23 % above F_M. The yield and stripping checks then use too low a load. | Label the friction inputs and K as the minimum (lower-bound) values, or add a separate F_M,max from μ_min. | Open. The friction inputs and K are still not labelled as minimum values. |

## Notes for the owner's manual test
- Hand-check the default case: F_M = 80 N·m / 1.966 mm = 40.69 kN, A_s = 84.27 mm², k_S about 3.21e5 kN/m.
- Expect Φ = 0.0720 at n = 0.5 and 0.1441 at n = 1. Expect F_KR = 13.13 kN with the default 10 kN load.
- Raise F_A to 100 kN. The joint separates, but yield and fatigue still pass. That is finding -02.
- Check the torsion term by hand with both W_p bases. The d_S basis gives nu_M 0.918 at default.
- Try d_h = d. The sheet must block it.

## Independent review
- Reviewer: senior reviewer (Claude), 2026-10-24.
- The script derives the joint independently. It holds switches for the W_p basis and the k_f numerator. It is not a port.
- I re-ran the script: 7/7 KAT rows pass, exit 0.
- I reproduced -02 with the probe (F_A 100 kN: F_S,max 47.9 kN, yield and fatigue pass, separation fails).
- I checked the three k_f variants by hand: mixed 1.908e6, d in both 2.213e6, d_h in both 2.067e6 kN/m.
- -01 is upgraded to Medium. The error is conservative but over 5 % on nu_M.
- New -R1 (High): an over-torqued bolt passes. VDI 2230 R7 is a required check, not a note.
- New -R2 (Medium): the friction inputs must be lower bounds for F_M to be a maximum.
- Residual doubts: the bolt resilience uses 0.4d under the head (VDI gives 0.5d for hex heads) and A_s over the whole clamp length. Both effects on Φ are small.
- Residual doubt: R-step numbers and the VDI cone form come from memory. The owner should confirm them against a licensed copy.

## Re-check after the Addendum A Blue round (0772fcf8)
- Source changed: none. BoltedJointCalculator.cs and its spec are untouched, and the version stays 1.0.0. Shared sheet code changed, so I re-ran the probes.
- Both KATs pass in the probe (2 of 2). The script is unchanged and exits 0. It holds 7 KAT rows.
- I re-ran all 11 probe cases (p1 to p11). All 15 outputs match the hard-coded product values to 4e-9 in the right units.
- All nine findings are still open, including R1 (High) and R2 (Medium). Probes F_A 100 kN and M_A 95 N·m reproduce -02 and R1.
- The 23 FAIL rows in the script output stay: they show the d_S basis (-01) and the missing bound after separation (-02).
- I found no new defect.
