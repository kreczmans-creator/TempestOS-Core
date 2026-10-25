# bs.glaser — independent re-derivation (Track P, P1)
Calculator: Glaser condensation (interstitial and surface), version 1.1.0. Source files: src/Tempest.Calc/Tempest.Calc.BuildingServices/GlaserCalculator.cs and GlaserModel.cs. Spec: docs/calcs/building-services/bs.glaser.md.
Script: tools/verification/bs.glaser.py (exit 0 = all KATs match). It checks 68 KAT values and 492 extra probe values in 0.05 s.
Verdict: MATCH WITH FINDINGS

## Method re-derived
- Layer values: R_j = d_j/λ_j in m²K/W and s_d,j = μ_j·d_j in m. R_T = R_si + ΣR_j + R_se.
- Interface temperature by series resistance: θ_k = θ_i − (θ_i − θ_e)·(R_si + Σ_(j≤k)R_j)/R_T.
- Saturation pressure, ISO 13788 Annex E form: 610.5·exp(aθ/(b + θ)). Water (θ ≥ 0): a = 17.269, b = 237.3. Ice (θ < 0): a = 21.875, b = 265.5. The inverse gives θ_si,min.
- Outside pressure p_e = RH_e·p_sat(θ_e). Inside pressure p_i = RH_i·p_sat(θ_i), or p_e + f_Δp·Δv·R_v·(T_i + T_e)/2 with R_v = 461.5 J/(kg·K), capped at saturation.
- Profile: vapour pressure against cumulative s_d. With no condensate it is the lower convex hull ("taut string") of p_i, p_e and the interface saturation points. This is the tangent construction.
- A wet interface is an obligatory point on the string. Between obligatory points I take the lower hull again.
- Net moisture at an interface: g_c = δ0·(slope_after − slope_before), δ0 = 2×10⁻¹⁰ kg/(m·s·Pa). Mc ← max(0, Mc + g_c·t·1000) in g/m².
- Year cycle: start in the first condensing month after a dry one (row 1 if all condense). Run twelve months. Balance passes when nothing remains.
- Surface: f_Rsi = 1 − R_si,f/R_T,f. f_Rsi,min = max over months with θ_e ≤ θ_i − 1 K of (θ_si,min − θ_e)/(θ_i − θ_e), where p_sat(θ_si,min) = p_i/φ_crit.
- Sources: ISO 13788 (surface temperature and interstitial condensation, Annex E for p_sat); Glaser (1959) for the method. Clause numbers are for the owner to check.
- Physical cross-check of the saturation relation against commonly tabulated values: 2337 Pa at 20 °C (tables 2339 Pa), 259.3 Pa over ice at −10 °C (tables 259.9 Pa), dew point 9.27 °C at 20 °C and 50 % (tables 9.3 °C).

## Known-answer cases
The script prints every value (68 rows). Grouped by case here. The product also passes its own KATs.
| Case | Symbols (count) | KAT expected (key values) | Independent | Max rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-seasonal-drying | n_start, Mc_1…Mc_6, Mc_11, Mc_12, Mc_max, Mc_end, u_Mc, pass (13) | start 11; peak 3083.081 g/m² (Mar); Mc_6 = 0; u_Mc 1.02769 | same | 1.6e-16 | 1e-9 | PASS |
| ka-psat-20 | psat_i, fRsi, fRsi_min, R_T, S_d, Mc_max, Mc_end, n_start, pass (9) | 2336.951 Pa; 0.575372; 0.403835; 0.46875; 1.87 | same | 1.2e-16 | 1e-9 | PASS |
| ka-single-interface-all-year | R_T, S_d, n_start, Mc_1, Mc_2, Mc_12, Mc_if_1, Mc_max, Mc_end, u_Mc, pass (11) | 1200.895; 2285.575; 14139.573 g/m² | same | 2.5e-14 | 1e-9 | PASS |
| ka-two-planes-tangent | R_T, S_d, Mc_if_1…4, Mc_1, Mc_max, Mc_end, pass (10) | planes 5143.147 and 772.008 g/m² | same | 7.4e-16 | 1e-9 | PASS |
| ka-surface-factor | R_Tf, fRsi, fRsi_min, n_fcrit, u_f, Mc_max, pass (7) | 0.575372; 0.590256; 1.025869 | same | 0 | 1e-9 | PASS |
| ka-humidity-class | n_start, Mc_10, Mc_4, Mc_8, Mc_max, Mc_end, fRsi_min, n_fcrit, pass (9) | start 10; peak 5111.052; end 91.744; 0.670474 | same | 1.2e-14 | 1e-9 | PASS |
| ka-humidity-class-no-margin | same nine symbols | peak 4249.892; end 0; 0.630284 | same | 4.3e-16 | 1e-9 | PASS |

No KAT value disagrees with the cited relations. All seven KATs use an invented climate. The spec says no published worked example is pinned. The Magnus checks above are my only external anchor.

## Extra probes (product vs independent)
Each probe compares every output the script models. These are n_start, Mc_1…Mc_12, Mc_if_k, Mc_max, Mc_tot, Mc_end, the resistances, f_Rsi, f_Rsi,min, u_f, u_Mc and pass. All agree within 1e-9. A random fuzz of 60 walls (2 to 9 layers, both humidity modes) also agrees. The fuzz ran outside the script.
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| X1 | 6 layers, 4.3–17 °C climate, 50 % | n_start; Mc_max | 12; 183.603 g/m² | same | ≤ 1e-9 | PASS |
| X3 | record starts in July, explicit days | n_start; Mc_max | 5; 2435.64 g/m² | same | ≤ 1e-9 | PASS |
| X4 | spell wraps Nov to Feb | n_start; Mc_max | 11; 1132.65 g/m² | same | ≤ 1e-9 | PASS |
| X5 | two spells, dry between | n_start; Mc_max | 11; 5161.56 g/m² | same | ≤ 1e-9 | PASS |
| X6 | 0.2 mm vapour control layer, μ 10⁵ | Mc_max | 0 | 0 | 0 | PASS |
| X7 | 8 layers, −15 °C, ice, foil | Mc_max | 14.154 g/m² | same | ≤ 1e-9 | PASS |
| X8 | single layer | warning single-layer | fires | n/a | n/a | PASS |
| X9 | summer, inside colder | n_start; fRsi_min | 0; 0 | same | 0 | PASS |
| X10 | 40 °C, 100 % inside | Mc_end | 372166 g/m² | same | ≤ 1e-9 | PASS |
| X11 | μ 10⁸, 2000 mm; 0.01 mm layer | Mc_max | 0 | 0 | 0 | PASS |
| X12 | 5 °C, 100 % all year | Mc_end | 12421.3 g/m² | same | ≤ 1e-9 | PASS |
| X13 | R_si,f 0.04, φ_crit 30 % | fRsi_min | 5.263 | same | ≤ 1e-9 | PASS |
| X18 | humidity class above saturation, f_Δp 1.5 | p_int_m; warning p-int-saturated | 2337 Pa; fires | same | 0 | PASS |

Guards tested and working: 31 layers, 11 climate rows, θ_b below θ_a and a missing dv_b (all blocked). The breathable flag and the timber column raise en15026. The element filter and R_si,f 0.13 raise their warnings. Diagram vectors match hand values.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| R, s_d, θ_k | Series resistance | Same, GlaserModel.NodeTemperatures | Yes |
| p_sat and inverse | Annex E, water above 0 °C and ice below | Same, branch at 0 °C and at 610.5 Pa | Yes |
| p_i, humidity class | Linear Δv, constant beyond, mean T, margin f_Δp | Same; capped at saturation with warning | Yes (see 04) |
| Tangent construction | Lower hull | Smallest-slope walk, ties go to the farthest point | Yes |
| g_c at contact | δ0·(slope_after − slope_before) | Same; wet nodes take either sign | Yes |
| Wet interface | Held at saturation, dries until empty | Same; dry floor 10⁻⁹ g/m² | Yes |
| Start month | First condensing month after a dry one | Same; first spell by row order | Yes |
| Month length | Days of the month | Calendar days if blank, rows read as Jan–Dec | Yes (stated) |
| Balance | Nothing left after 12 months | Mc_end ≤ 10⁻⁶ g/m² | Yes |
| f_Rsi, f_Rsi,min | 1 − R_si,f/R_T,f; θ_e ≤ θ_i − 1 K | Same; floor at 0 | Yes |
| Condensation sites | Interfaces only | Same; no guard for planes inside a layer | See 01, 02 |
| Limits | μ ≥ 1, d 0.01–2000 mm, 12 rows, ≤ 30 layers | All enforced | Yes |

## Findings
| ID | Severity | Finding | Evidence | Suggested fix (for Track S) |
|---|---|---|---|---|
| P1-bs.glaser-01 | Medium | The verdict changes with layer subdivision, with no warning. The spec says splitting "made no difference in any test case". That is not true in general. | 25 mm (λ 0.035, μ 8) + 50 mm (0.5, μ 10) + 215 mm (0.5, μ 40), 40 % inside, cold climate of script X16, Mc_limit 1000: as entered Mc_end 0, pass 1. Each layer in 9 slices (X17, run in the product): Mc_end 528.6 g/m², pass 0. Five slices still pass. | Split each layer internally into sub-layers (about 5 to 10) for the saturation test. Or warn when the chord exceeds p_sat inside a layer. Correct the spec wording "slightly understates". |
| P1-bs.glaser-02 | Low | When the inside surface is already below the dew point, all interstitial condensate lands on interface 1. Its amount scales with 1/s_d of the first layer. The numbers are meaningless, with no notice. | 102 mm + 15 mm uninsulated wall, 50 % inside (X14, X15): f_Rsi 0.386 against 0.737. Mc_max is 0 as entered, 1918 g/m² at 12 slices per layer, 34438 g/m² at 200. | Warn when p_sat(θ_si) < p_i in any month: "surface condensation; interstitial amounts not valid". The overall verdict is already FAIL. |
| P1-bs.glaser-03 | Low | The annual-balance check prints u = Infinity when condensate remains. | X10, X12, X18 show `u=Infinity FAIL` in the CHECK line. | Print the residual (g/m²) as the check value, not a ratio. |
| P1-bs.glaser-04 | Info | The margin f_Δp is applied to both the interstitial and the surface check. I cannot confirm from memory that both uses are in ISO 13788. The mean-temperature form of Δp needs the same check. | The spec states both uses. If the margin belongs only to the surface check, the interstitial result is conservative. | Owner to check the humidity-class clause of ISO 13788 in the licensed copy. |
| P1-bs.glaser-05 | Info | In constant-humidity mode no safety margin is applied to the internal humidity. The spec says so. A user who enters 50 % gets the bare criterion. | ka-surface-factor: f_Rsi,min 0.5903 at 50 % with no margin. Design practice adds a margin, so the user must raise RH_int. | Keep. Consider a note beside RH_int. |

## Notes for the owner's manual test
- Dew point check: 20 °C, 50 % inside, φ_crit 80 % gives θ_si,min = 12.91 °C. For 2 °C outside, f_Rsi,min = 0.5903.
- Enter 100 mm insulation (λ 0.04, μ 2) on 200 mm brick (0.8, 10), constant 0 °C and 90 %, 50 % inside. Expect 38.74 g/m² per day, 1200.9 g/m² in January and 14139.6 g/m² after 365 days. Balance FAIL.
- Five-layer wall of the KAT ka-two-planes-tangent: expect planes only at interfaces 2 and 4 (5143.1 and 772.0 g/m²).
- Take any wall and split a thick layer in two with the same λ and μ. Compare Mc_max and the balance (finding 01).
- Use 40 °C and 100 % inside to see the Infinity display (finding 03). Use a humidity class with large Δv to see the saturation cap warning.
