# fp.cylinder — independent re-derivation (Track P, P1)
Calculator: Cylinder and actuator circuit (F1), version 1.2.0. Source file: src/Tempest.Calc/Tempest.Calc.FluidPower/CylinderCalculator.cs. Spec: docs/calcs/fluid-power/fp.cylinder.md.
Script: tools/verification/fp.cylinder.py (exit 0 = all KATs match). Release 2f53791c. Probe inputs: /tmp/claude-0/probe-inputs/cyl_*.json.
Verdict: MATCH WITH FINDINGS (no formula or unit error; the findings are Low or Info, about model limits and missing guards)

I wrote the script before I read the `Calculate` code. It recomputes all 95 expected values of the six KATs and 71 extra values. All agree to better than 1e-9. The product KAT run passes 6 of 6 cases.

## Method re-derived
- Areas: A_b = πD²/4, A_r = πd_r²/4, A_a = A_b − A_r. Source: any fluid-power text, for example Esposito, Fluid Power with Applications, 7th ed., cylinder chapter. Units m², shown in cm².
- Force: F_ext = (p·A_b − p_back·A_a)·η_m and F_ret = (p·A_a − p_back·A_b)·η_m. The efficiency multiplies the net force. This is the usual textbook simplification.
- Regenerative extend: the annulus joins the cap side, so the net pressure area is A_r. F = p·A_r·η_m, v = Q·η_vc/A_r. Cap-side line flow = Q·A_b/A_r (continuity).
- Speed: v = Q·η_vc/A. Stroke time = stroke/v. Cycle time adds the dwell once.
- Pump: Q_p = V_g·n·η_vp. Shaft power P = p·Q_p/η_tp, where η_tp is the total efficiency. T = P/ω with ω = 2πn/60. Needed displacement = Q/(n·η_vp).
- Motor: T = Δp·V_g/(2π)·η_hm, with V_g per revolution.
- Lines: v = Q/(πd²/4). Smallest bore d = √(4Q/(πv_max)). Recommended size = smallest listed size not below d.
- Orifice: Q = C_d·A·√(2Δp/ρ). Bore for the circuit flow follows by inversion.
- Rod column: r = d_r/4 for a solid round rod. λ = K(stroke + L_add)/r. λ_c = π√(2E/f_y) (Euler stress equals f_y/2). Euler F_cr = π²EI/L_eff². Johnson F_cr = A·f_y·(1 − f_y·λ²/(4π²E)). Both branches meet at λ_c with equal slope. Source: Johnson–Euler column theory, as in any machine-design text.
- Rod load for buckling = max(push load, p·A_b). Stall thrust has no η_m and no back pressure (no flow).
- Not in the sheet: line pressure drop, cushioning, ISO/TS 13725 stepped-column method. The sheet states the last two itself.

## Known-answer cases
All six KATs are analytic and Confirmed. None is Secondary. Rel. diff is at most 2.2e-16 for every value. The table gives one row per case and the worst value; the script prints every value.

| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-circuit-standard (49 values) | F_ext | 119.38052 kN | 119.38052 kN | 0 | 1e-9 | PASS |
| ka-circuit-standard | P_pump | 11.687879 kW | 11.687879 kW | 0 | 1e-9 | PASS |
| ka-circuit-standard | T_shaft | 76.973118 N·m | 76.973118 N·m | 0 | 1e-9 | PASS |
| ka-circuit-standard | v_prs | 6.2690014 m/s | 6.2690014 m/s | 0 | 1e-9 | PASS |
| ka-circuit-standard | F_cr (Johnson) | 1787.8070 kN | 1787.8070 kN | 0 | 1e-9 | PASS |
| ka-circuit-standard | u_buck | 0.24601256 | 0.24601256 | 0 | 1e-9 | PASS |
| ka-regenerative (15 values) | F_ext | 58.496455 kN | 58.496455 kN | 0 | 1e-9 | PASS |
| ka-regenerative | Q_prs | 78.714286 L/min | 78.714286 L/min | 0 | 1e-9 | PASS |
| ka-euler-slender (10 values) | F_cr (Euler) | 59.059575 kN | 59.059575 kN | 0 | 1e-9 | PASS |
| ka-euler-slender | u_buck | 4.7661485 | 4.7661485 | 0 | 1e-9 | PASS |
| ka-back-pressure-fixed-free (11 values) | F_ext | 115.57527 kN | 115.57527 kN | 0 | 1e-9 | PASS |
| ka-back-pressure-fixed-free | F_cr | 1681.7037 kN | 1681.7037 kN | 1.4e-16 | 1e-9 | PASS |
| ka-orifice (2 values) | Q_o | 22.619467 L/min | 22.619467 L/min | 1.6e-16 | 1e-9 | PASS |
| ka-lossless (8 values) | v_ext | 100 mm/s | 100 mm/s | 0 | 1e-9 | PASS |

No KAT disagrees with physics. I also checked the hand notes in the KAT sources: the 1.2538 pressure-line utilisation and the 4.76615 slender-rod utilisation reproduce.

## Extra probes (product vs independent)
Each probe ran through the Probe tool. The script holds the product values and re-checks them. Every symbol listed agrees within 1e-6 (largest 5e-10).

| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| x1 regenerative, typical | 63/36, 120 bar, 60 L/min, 800 mm, pump 25 cm³ at 1500 rpm, K 0.7 | F_ext / F_std | 11.604 / 35.537 kN | same | <1e-9 | PASS |
| x1 | same | Q_prs / d_rec_prs | 183.75 L/min / 32 mm | same | <1e-9 | PASS |
| x2 back pressure + motor | 125/90, 250 bar, p_back 40, K 0.7, motor 32 cm³ at 200 bar | F_ret / T_motor | 93.732 kN / 91.673 N·m | same | <1e-9 | PASS |
| x3 near zero | D 10, d_r 4, 1 bar, 0.1 L/min, stroke 1, L_add 0 | F_ext / F_cr | 7.461 N / 6.283 kN | same | <1e-9 | PASS |
| x4 very large | D 500, d_r 450, 700 bar, 2000 L/min, 5000 mm, K 2.5, L_add 5000, pump 1000 cm³ at 2000 rpm | P_pump / u_buck | 2518.9 kW / 7.865 | same | <1e-9 | PASS |
| x5, x6 transition | d_r 40, stroke 910 then 911 mm, L_add 0 | u_buck | 0.89498, then 0.89694 | same | <1e-9 | PASS (branches meet) |
| x7 regenerative with p_back 30 | KAT geometry | F_ext | 58.4965 kN (same as with p_back 0) | same | 0 | PASS; see -01 |
| x8 oversize pump | 100 cm³ at 1500 rpm, 210 bar | P_pump / v_suc | 56.68 kW (excess over relief) / 1.989 m/s | same | <1e-9 | PASS |
| x9 fixed-fixed | 63/28, K 0.5, 1500 mm, 100 bar | F_cr / u_buck | 97.71 kN (Euler) / 1.1166 | same | <1e-9 | PASS |
| x10 low E, f_y | E 50 GPa, f_y 100 MPa | λ_c | 99.346 | 99.346 | <1e-9 | PASS |
| x11 thin annulus | d_r 99.5 in D 100 | v_ret / u_prs | no guard; u_pull 33.6, u_prs 59.8 FAIL | same | <1e-9 | PASS; checks catch it |

Blocks seen as designed: d_r ≥ D; p_back 51 bar at p 100 bar (retract force ≤ 0; 50.9 bar passes); V_g without n_p; η_tp > η_vp; dp_o > p; d_o without dp_o. The "size" warning fires for a short hose list.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| Areas, force, speed, swept volume | Closed form as above | Same; bar×1e5, L/min÷6e4 | Yes |
| Regenerative extend | F = p·A_r·η_m; Q_cap = Q·A_b/A_r | Same; F_std and v_std kept for comparison | Yes |
| Back pressure in regenerative mode | Discharge path has a pressure drop that lowers F | Input p_back is ignored for F_ext (stated in the sheet) | Yes, see -01 |
| Pump power | P = p·Q_actual/η_total | Uses Q_p (delivery), so excess is over relief at p | Yes (conservative) |
| Pump line pressure | Pump outlet = cylinder port + line drop | Equal; no line drop allowed | Partly, see -02 |
| Shaft torque / speed port | T = P/ω; n in rpm | SI value is rad/s inside; ports show rpm | Yes |
| Cap-port line flow | Cap discharge on retract = Q·A_b/A_a | Q_prs and Q_rtn both use it | Yes, see -04 |
| Speed vs pump | Delivered flow limits speed | Speed uses circuit Q; u_pump check flags a short pump | Partly, see -03 |
| Column slenderness | r = d/4; λ = K·L/r | Same | Yes |
| Euler / Johnson switch | λ_c = π√(2E/f_y) | Same, continuous at λ_c (x5, x6) | Yes |
| Rod load | Push load or stall thrust, whichever is larger | max(F_push, p·A_b) | Yes; relief setting not seen, see -06 |
| Applicability limits | Hard limits = valid range | Same as spec; block messages clear | Yes |
| Cushioning, piston speed | Not in scope | Not modelled; no warning | See -05 |

## Findings
| ID | Severity | Finding | Evidence | Suggested fix (for Track S) | Reviewed |
|---|---|---|---|---|---|
| P1-fp.cylinder-01 | Low | In regenerative mode the entered back pressure does not enter F_ext. A real regenerative path has a pressure drop from the annulus to the cap. That drop lowers the force by Δp·A_a. The sheet says so in its assumption text, but gives no notice. | x7: p_back 0 and 30 bar both give F_ext 58.4965 kN. A 30 bar drop across 40.06 cm² would remove 11.4 kN (19 %). | Add a regeneration pressure-drop input, or warn when p_back > 0 in regenerative mode. State that F_ext is the lossless upper bound. | — |
| P1-fp.cylinder-02 | Low | The sheet has no pressure drop allowance. p is both the pump outlet and the cylinder port. Pump power and torque are low by the ratio of line drop to p when the sheet feeds a pump selection. This matters in the chain (see P1-chain.hydraulic-power-unit). | Spec: p is "at the cylinder and the pump". A 10 bar line drop at 160 bar is 6 % of P_pump. | Offer an optional line-drop input that raises the pump pressure only. Or carry the B1 drop to F1. | — |
| P1-fp.cylinder-03 | Low | Speeds and cycle time use the circuit flow Q, not the pump delivery. When the pump is too small, u_pump exceeds 1 and fails, but v_ext and t_cycle stay optimistic. | x4: Q 2000 L/min against Q_p 1900 L/min. v_ext 166.4 mm/s uses 2000 L/min; the pump gives 5 % less. | Show a second speed at min(Q, Q_p)·η_vc, or add a notice with u_pump > 1. | — |
| P1-fp.cylinder-04 | Low | The cap-port line is judged at the larger stroke flow φ·Q. On a typical circuit the pump line carries only Q. u_prs overstates the pressure-line velocity, and the Q_prs port gives B1 a flow φ times too large for a supply line. The error is conservative for velocity, and it overstates B1's friction loss. | KAT: Q_prs 75.63 L/min against Q_p 38.57 L/min (φ 1.96). | Rename Q_prs as the cap-side flow. Add a separate pump-line flow port Q_line for B1. | — |
| P1-fp.cylinder-05 | Low | No piston-speed or cushioning check. The sheet accepts a piston speed of 416 m/s without a speed warning. Other checks fail for those inputs, so the user is not misled. | y1: D 10 mm, d_r 4 mm, 2000 L/min, 100 bar. v_ext 415 925 mm/s; u_buck 430. | Add a piston-speed advisory (user record). Say that cushioning energy is not modelled. | — |
| P1-fp.cylinder-06 | Info | Stall thrust uses p. A relief valve is usually set above the working pressure. If the user enters the working pressure, the buckling load is low. The spec says no pressure-limiting device is credited. | Spec text, Limitations. Not a calculation error. | State in the input label that p must be the relief setting for the buckling check. | — |
| P1-fp.cylinder-07 | Info | L_add (default 100 mm) and the rod column model are not records. K_m, S_f, E and f_y raise a "not from a record" warning. L_add does not, though it sets the buckling length. | y2: L_add 0 raises no notice. A 100 mm error on 600 mm raises λ by 17 %. | Add L_add to the mounting record. | — |
| P1-fp.cylinder-08 | Info | The ISO/TS 13725 stepped-column method is not implemented. The sheet says so on the result. Solid-rod Euler on stroke + L_add is a screening, not a maker's value. | buckling_note: "screening only; ISO/TS 13725 method not implemented". | None. Owner to compare with the cylinder maker's sizing sheet. | — |

## Notes for the owner's manual test
- Standard 100/70, 160 bar, 36 L/min, 500 mm: expect F_ext 119.38 kN, F_ret 60.88 kN, v_ext 74.87 mm/s, t_cycle 10.085 s.
- Same with pump 28 cm³ at 1450 rpm: expect P_pump 11.688 kW, T_shaft 76.97 N·m, u_prs 1.254 on 16 mm and recommended 20 mm.
- Switch to regenerative: expect F_ext 58.50 kN, v_ext 152.79 mm/s, Q_prs 78.71 L/min. Add 30 bar back pressure: F_ext does not change (-01).
- Slender rod 80/40, 2000 mm stroke, K 1, 160 bar: expect F_cr 59.06 kN (Euler) and u_buck 4.77 even with a 15 kN push load.
- Compare a rod sizing against your cylinder maker's guide with the same K and safety factor. The sheet will not match ISO/TS 13725.
