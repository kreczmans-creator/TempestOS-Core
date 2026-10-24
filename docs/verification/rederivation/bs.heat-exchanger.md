# bs.heat-exchanger — independent re-derivation (Track P, P1)
Calculator: Heat exchanger (LMTD and ε-NTU), version 1.0.0. Source file: src/Tempest.Calc/Tempest.Calc.BuildingServices/HeatExchangerCalculator.cs. Spec: docs/calcs/building-services/bs.heat-exchanger.md.
Script: tools/verification/bs.heat-exchanger.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS

The product matches the independent script on all 18 KAT values and on all 15 extra probes (relative difference below 2e-9 apart from rounding of the printed probe values).
The findings concern guards and labels, not the core formulas.

## Method re-derived
- Capacity rates C = ṁ·c_p (W/K), C_min, C_max, C_r = C_min/C_max. Incropera, 7th ed., §11.3–11.4.
- Fouled coefficient U_f = 1/(1/U + R_f) (W/m²K). Incropera, 7th ed., §11.1.
- NTU = U_f·A/C_min. Incropera, 7th ed., §11.4 (definition of NTU).
- ε(NTU, C_r) for parallel, counterflow (C_r = 1 limit NTU/(1+NTU)), one shell pass with 2n tube passes, crossflow both unmixed, and C_r = 0. Incropera, 7th ed., Table 11.3.
- Inverse NTU(ε, C_r): the script finds the root numerically by bracketed search. The product uses the closed forms of Table 11.4, with bisection for crossflow.
- Duty Q = ε·C_min·(T_h,in − T_c,in) in W; outlets by energy balance on each stream.
- ε limit for sizing: ε_max = 1 (counterflow, C_r = 0), 1/(1+C_r) (parallel), 2/(1+C_r+√(1+C_r²)) (shell and tube).
- LMTD from counterflow end differences (parallel-flow ends for parallel). Incropera, 7th ed., Eq. 11.14.
- F for one shell pass from R and P, closed form of Incropera Fig. 11.10; for crossflow F = Q/(U_f·A·ΔT_lm).
- Approach ΔT_app = smallest terminal temperature difference (my definition).

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-counter-balanced | NTU | 1 | 1 | 0.00e+00 | 1e-09 | PASS |
| ka-counter-balanced | eps | 0.5 | 0.5 | 0.00e+00 | 1e-09 | PASS |
| ka-counter-balanced | Q | 125.4 | 125.4 | 0.00e+00 | 1e-09 | PASS |
| ka-counter-balanced | T_h_out | 50 | 50 | 0.00e+00 | 1e-09 | PASS |
| ka-counter-balanced | dT_lm | 30 | 30 | 0.00e+00 | 1e-09 | PASS |
| ka-counter-balanced | F | 1 | 1 | 0.00e+00 | 1e-09 | PASS |
| ka-parallel-balanced | eps | 0.4323324 | 0.4323324 | 9.63e-08 | 1e-06 | PASS |
| ka-parallel-balanced | Q | 108.4282 | 108.429 | 6.97e-06 | 1e-05 | PASS |
| ka-shell-tube | eps | 0.5399396 | 0.5399396 | 8.13e-08 | 1e-06 | PASS |
| ka-shell-tube | Q | 135.4168 | 135.4168 | 3.00e-07 | 1e-05 | PASS |
| ka-shell-tube | T_c_out | 36.19819 | 36.19819 | 9.16e-08 | 1e-06 | PASS |
| ka-shell-tube | F | 0.9234561 | 0.9234561 | 5.61e-09 | 1e-06 | PASS |
| ka-counter-sizing | NTU | 2 | 1.999998 | 8.93e-07 | 1e-05 | PASS |
| ka-counter-sizing | A_req | 8.36 | 8.359993 | 8.93e-07 | 1e-05 | PASS |
| ka-incropera-11-1 | T_h_out | 60 | 60 | 1.18e-16 | 1e-06 | PASS |
| ka-incropera-11-1 | T_c_out | 40.2 | 40.20105 | 2.62e-05 | 1e-03 | PASS |
| ka-incropera-11-1 | dT_lm | 43.2 | 43.19999 | 3.36e-07 | 1e-03 | PASS |
| ka-incropera-11-1 | UA | 197.31 | 197.3149 | 2.47e-05 | 1e-03 | PASS |

All KATs pass. The ka-incropera-11-1 case is marked Unconfirmed in the spec.
My arithmetic from its stated inputs gives q = 8524 W, T_c,o = 40.20 °C, ΔT_lm = 43.20 K, UA = 197.3 W/K and A = 5.18 m² at U = 38.1.
These agree with Example 11.1 as I remember it. The owner should still compare them with the printed example.

## Extra probes (product vs independent)
All rows use the probe tool "run" command. The script holds the full list of symbols (NTU, ε, Q, outlets, ΔT_app, UA, A_req, ΔT_lm, F).
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| E1 | counter, C_r 0.5, A 5, R_f 2e-4 | Q (kW) | 141.3849 | 141.3849 | <2e-10 | PASS |
| E2 | parallel, C_r 0.25, sizing 100 kW | A_req (m²) | 2.307234 | 2.307234 | <2e-10 | PASS |
| E3 | crossflow, C_r 0.6, A 3 | ε / F | 0.4369856 / 0.9417862 | same | <3e-10 | PASS |
| E4 | crossflow, C_r 0.6, sizing 120 kW | A_req (m²) | 3.491802 | 3.491802 | <2e-10 | PASS |
| E5 | shell 1-2, C_r 0.5, sizing 150 kW | A_req (m²) / F | 5.2571 / 0.8845 | same | <4e-10 | PASS |
| E6 | shell 1-2, C_r 1, A 10 | F | 0.5523365 | 0.5523365 | <8e-10 | PASS |
| E7 | isothermal, C_h 1e6 W/K, A 2 | Q (kW) | 158.9522 | 158.9522 | <2e-10 | PASS |
| E8 | counter, A 0.01 (near zero) | Q (kW) | 0.598568 | 0.598568 | <2e-10 | PASS |
| E9 | counter, NTU 2392 | ε | 0.9995822 | 0.9995822 | <4e-10 | PASS |
| E10 | shell 1-2, NTU 2392 | ε | 0.5857864 | 0.5857864 | <7e-10 | PASS |
| E11 | counter sizing 60 kW, R_f 1e-3 | A_req (m²) | 2.441648 | 2.441648 | <5e-11 | PASS |
| E12 | counter, streams −30 and −40 °C | T_c,out (°C) | −36.76375 | −36.76375 | <2e-9 | PASS |
| E13 | counter, C_r 0.9999 | ε | 0.500012 | 0.500012 | <7e-10 | PASS |
| E14 | counter, C_r 1, A 1, Q_req 200 kW | Q (kW) | 48.41699 | 48.41699 | <7e-11 | PASS |
| E15 | parallel, NTU 2392 (pinch) | ε | 0.5 | 0.5 | 0 | PASS |
| B1 | parallel sizing C_r 1, 150 kW | block | blocked (ε 0.5981 > 0.5) | blocked | – | PASS |
| B2 | shell 1-2 sizing C_r 1, 147 kW | block | blocked (ε 0.5861 > 0.5858) | blocked | – | PASS |
| B3 | counter sizing C_r 1, 250.7 kW | block | blocked (ε < 0.999 rule) | attainable | – | FINDING 02 |
| Q4 | mixed units: 3600 kg/h, 4.18 kJ/kgK, 353.15 K, 68 °F | Q (kW) | 125.4 | 125.4 | 0 | PASS |

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| ε relations | Incropera Table 11.3 | Same five forms; C_r = 1 limit handled | Yes |
| Inverse NTU | Table 11.4 | Closed forms; crossflow by 200-step bisection | Yes |
| Fouling | U_f = 1/(1/U + R_f) | Same; A_req uses U_f | Yes |
| Units | SI inside; kW, °C outside | Mixed input units convert correctly (probe Q4) | Yes |
| F (shell) | R, P closed form; R = 1 limit | Same; non-finite F becomes 0 | Yes |
| F (crossflow) | Back-calculated | Q/(U_f·A·ΔT_lm) | Yes |
| Pinch guard | End difference zero | Warns when an end difference is below 1e-6 K, skips LMTD | Yes |
| Duty limit (sizing) | ε below ε_max | Uses ε at NTU = 1000 as the limit | Partly (finding 02) |
| Isothermal choice | C_r = 0, one stream infinite | C_r forced to 0; no check of the stream ratio | Partly (finding 01) |
| Temperature check | T_h,in above T_c,in | Block "temperatures" | Yes |
| Crossflow correlation | Approximate, flagged | Warning shown | Yes |

## Findings
| ID | Severity | Finding | Evidence | Suggested fix (for Track S) |
|---|---|---|---|---|
| P1-bs.heat-exchanger-01 | Medium | The isothermal arrangement forces C_r = 0 but cannot name the constant-temperature stream. The other stream's C_min then drives ε = 1 − e^(−NTU). With comparable streams the duty is overstated. The "constant" stream's outlet also changes. | Inputs: isothermal, ṁ_h 1, ṁ_c 2 kg/s, c_p 4180, 80 and 20 °C, U 1000, A 4.18. Product Q = 158.54 kW and T_h,out = 42.07 °C. Counterflow with the same inputs gives 141.64 kW (+11.9 %). The sheet shows C_r = 0. | Add a choice for the isothermal stream and treat its C as infinite. Hold its outlet at its inlet. Or warn when C_min/C_max is above about 0.05. Say in the template that the constant-temperature stream needs a very large ṁ·c_p. |
| P1-bs.heat-exchanger-02 | Low | Sizing blocks any ε above the value at NTU = 1000. For counterflow with C_r = 1 that is 0.999. The message says the exchanger "cannot exceed" it. Rating accepts NTU 2392. | Counterflow, 1 kg/s each, 4180 J/kgK, 80 and 20 °C, U 1000, sizing Q_req 250.7 kW: blocked at ε 0.9996. Rating with U 10000 and A 1000 m² gives ε 0.99958. | Use the analytic ε_max per arrangement, or reword the message to "above the supported NTU of 1000". |
| P1-bs.heat-exchanger-03 | Low | The area from sizing is not checked against the rating limit of 0.01–1000 m². | Counterflow, equal 1 kg/s streams, 80 and 20 °C, U 1000, Q_req 250 kW: A_req = 1306 m² with no notice. Only the approach check fails. | Warn when A_req is outside 0.01–1000 m². |
| P1-bs.heat-exchanger-04 | Info | KAT value slip. The ka-parallel-balanced Q is 108.4282 kW, but ε·C_min·ΔT = 108.4290 kW. The 1e-5 tolerance hides the 7e-6 difference. ka-counter-sizing shows the same effect: NTU 1.999998. | Script rows for both cases. | Correct the expected value to 108.4290 and use more digits in Q_req. Add an A_req = 5.18 m² check to ka-incropera-11-1. |
| P1-bs.heat-exchanger-05 | Info | The shell-and-tube F at the ε limit is rounding noise. | Shell 1-2, C_r 1, NTU 2392: product F = 0.0558. The true limit is 0. The low-F warning still appears. | No action. |
| P1-bs.heat-exchanger-06 | Info | The crossflow correlation differs from a numerical solution by −3.3 % to +1.1 %. | Script section "Crossflow correlation". At NTU 3, C_r 0.5 the correlation is high by 1.1 %. | No action. The warning already says "within a few percent". |

## Notes for the owner's manual test
- Counterflow, 1 kg/s and 4180 J/kgK on both sides, 80 and 20 °C, U 1000, A 4.18: expect ε 0.5, Q 125.4 kW, both outlets 50 °C.
- Repeat with "shell and tube" and ṁ_c = 2: expect ε 0.53994, F 0.92346, T_c,out 36.198 °C.
- Check Incropera Example 11.1 in sizing mode with Q_req 8.524 kW: expect T_c,o 40.2 °C, ΔT_lm 43.2 K, A_req about 5.18 m².
- Try the isothermal choice with comparable streams (finding 01) and compare with counterflow.
- Enter a parallel-flow sizing duty above 50 % of C_min·ΔT_max with C_r = 1: expect the "duty-unattainable" block.
