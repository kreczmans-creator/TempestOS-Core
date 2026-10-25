# elec.pcb-trace — independent re-derivation (Track P, P1)
Calculator: PCB trace width (IPC-2221 legacy), version 1.0.1. Source file: src/Tempest.Calc/Tempest.Calc.Electrical/PcbTraceCalculator.cs. Spec: docs/calcs/electrical/elec.pcb-trace.md.
Script: tools/verification/elec.pcb-trace.py (exit 0 = all KATs match). It checks 17 KAT values and 47 extra probe values.
Verdict: MATCH WITH FINDINGS

## Method re-derived
- Legacy IPC-2221 chart fit (section 6, current-carrying capacity): I = k·ΔT^0.44·A^0.725. I in A, ΔT in K, A in mil².
- k = 0.048 for an external layer and 0.024 for an internal layer. Inverse: A = [I/(k·ΔT^0.44)]^(1/0.725).
- Copper thickness t = oz × 1.378 mil (35.0 µm per oz/ft²). The other common rounding is 1.37 mil (34.8 µm).
- w_min = A/t, converted with 1 mil = 0.0254 mm. Utilisation u_w = w_min/w_act.
- Resistivity ρ(T) = ρ20·[1 + α(T − 20)] with ρ20 = 0.01724 Ω·mm²/m and α = 0.00393 per K (annealed copper).
- Trace temperature T = T_amb + ΔT. R = ρ(T)·L/(w_act·t). Voltage drop = I·R. Loss = I²·R.
- Units checked: A in mil² only inside the fit. Width and resistance use mm, m and Ω·mm²/m. No unit slip found.

## Known-answer cases
The script prints every value. Grouped here by case.
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-3a-10k-1oz-external | A_req, w_min, u_w | 74.16164 mil², 1.366985 mm, 0.683493 | same | ≤ 1.6e-10 | 1e-9 | PASS |
| ka-3a-10k-1oz-internal | A_req, w_min, u_w | 192.9270 mil², 3.556130 mm, 0.889032 | same | ≤ 1.7e-10 | 1e-9 | PASS |
| ka-5a-20k-2oz-resistance | w_min, trace_R, vdrop, trace_Ploss, u_w | 0.907900 mm, 10.81896 mΩ, 54.0948 mV, 270.474 mW, 0.36316 | same | ≤ 1.3e-10 | 1e-9 | PASS |
| ka-half-oz-internal-narrow | w_min, u_w, trace_R, trace_Ploss | 0.802317 mm, 1.604634, 471.473 mΩ, 471.473 mW | same | ≤ 1.9e-10 | 1e-9 | PASS |
| ka-published-3a-10k-1oz (Secondary) | w_min | 1.37 mm | 1.36699 mm | 2.2e-3 | 5e-3 | PASS |
| ka-published-3a-10k-1oz-internal (Secondary) | w_min | 3.56 mm | 3.55613 mm | 1.1e-3 | 5e-3 | PASS |

The product also passes 6/6 of its own KATs (probe `kat`). No KAT value disagrees with the cited relation. The two Secondary cases are quoted widths, not primary values.

## Extra probes (product vs independent)
Each probe compares A_req, w_min, u_w, trace_R, vdrop and trace_Ploss. All 42 values, and the 5 values of the unit-entry probe, agree within 5e-10.
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| P1 typical | 10 A, 20 K, 25 °C, ext, 1 oz, 5 mm, 80 mm | w_min / R | 4.72369 mm / 8.65517 mΩ | same | ≤ 3e-10 | PASS |
| P2 upper edge | 35 A, 100 K, 85 °C, ext, 3 oz, 10 mm, 300 mm | w_min / P | 3.33731 mm / 9946.41 mW | same | ≤ 2e-10 | PASS |
| P3 near zero | 10 mA, 1 K, int, 1 oz, 0.05 mm, 1 mm | A_req / R | 0.298931 mil² / 10.0834 mΩ | same | ≤ 2e-10 | PASS |
| P4 very large | 100 A, 40 K, ext, 2 oz, 30 mm, 500 mm | w_min / u_w | 37.1422 mm / 1.23807 | same | ≤ 3e-10 | PASS |
| P5 cold, heavy copper | 2 A, 10 K, −40 °C, int, 6 oz, 3 mm, 1 m | R | 21.9871 mΩ | same | ≤ 1.5e-10 | PASS |
| P6 half ounce, hot | 8 A, 15 K, 125 °C, ext, 0.5 oz, 20 mm, 20 mm | w_min / R | 8.26928 mm / 1.44969 mΩ | same | ≤ 3e-10 | PASS |
| P7 extreme | 1 A, 300 K, 500 °C, int, 1 oz, 1 mm, 100 mm | R | 200.243 mΩ | same | ≤ 4e-11 | PASS |
| Units | 3000 mA, 77 °F, 78.74 mil wide, 0.05 m | w_min; u_w | 1.366985 mm; 0.683494 | same | ≤ 2e-10 | PASS |

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| Cross-section relation | I = k·ΔT^0.44·A^0.725 | Same, with exponents as inputs n_dT, n_A | Yes |
| k values | 0.048 ext, 0.024 int | Defaults 0.048 and 0.024, chosen by the layer choice | Yes |
| Copper thickness | 1.378 mil/oz (35 µm) | t_oz default 1.378 mil, shown as 35.001 µm | Yes (see 01) |
| Width | A/t | A_req / t_Cu in mm | Yes |
| Resistance temperature | T_amb + ΔT | T_trace = T_amb + dT, ρ linear in T | Yes (see 02) |
| Unit handling | A in mil² only | Dimensionless ratios I/1 A and ΔT/1 K; area converted by the unit engine | Yes |
| Hard limits | I 1 mA–1 kA, dT 0.1–300 K | Same. I = 0, I < 0 and dT = 0 are blocked | Yes |
| Warnings | I > 35 A, w_min > 10 mm, oz outside 1–3, dT < 10 K | All four fire (width and current: P4; copper: P5, P6; rise: P3) | Yes |
| Check | u_w ≤ 1 | CheckUtilisation on u_w; FAIL at 1.238 and 1.605 | Yes |
| Temperature unit input | degC or K | 77 °F accepted and converted to 25 °C | Yes |

## Findings
| ID | Severity | Finding | Evidence | Suggested fix (for Track S) |
|---|---|---|---|---|
| P1-elec.pcb-trace-01 | Low | Copper thickness is 35.0 µm per oz. The other common value is 34.8 µm (1.37 mil). The product width is 0.58 % narrower, so slightly unconservative. | 3 A, 10 K, 1 oz external: 1.3670 mm at 1.378 mil; 1.3750 mm at 1.37 mil. | Keep 1.378 mil. State in the sheet that 34.8 µm gives 0.6 % wider traces. Confirm the value against the owner's copy of IPC-2221. |
| P1-elec.pcb-trace-02 | Medium | When u_w > 1, R, ΔV and P are evaluated at the allowed rise. The drawn trace would run hotter, so these values are too low. The label says "at its operating temperature". | ka-half-oz-internal-narrow: drawn width 0.5 mm gives a rise of 65.4 K, not 30 K. R at that temperature is 526 mΩ against 471.5 mΩ reported (11.6 % low). | Add a warning when u_w > 1. Or solve the curve-fit for the actual rise and use it for ρ. Or relabel the outputs "at the allowed rise". |
| P1-elec.pcb-trace-03 | Low | No notice on the trace temperature. T_amb + ΔT up to 800 °C is accepted without comment, and 185 °C passes quietly. | Probe P7: 500 °C + 300 K gives only usual-range notices. Probe P2: 85 °C + 100 K gives no notice. | Warn when T_amb + ΔT exceeds a recorded laminate limit (for example 105 or 130 °C). |
| P1-elec.pcb-trace-04 | Low | Seven "was not entered; template default used" warnings appear on every default run. They hide real warnings. | Probe P1 prints seven such lines for k_ext, k_int, n_dT, n_A, t_oz, rho_20 and alpha. | Show one grouped notice about the method record. Do not repeat it per constant. |
| P1-elec.pcb-trace-05 | Info | The sheet label "legacy IPC-2221 curve-fit, conservative for multilayer boards" lives only in the template description. The probe output does not print it. | `SheetLabel` is used once, in the description string. | Confirm the desktop sheet header shows the label. |

## Notes for the owner's manual test
- 3 A, 10 K rise, 1 oz, external: expect A = 74.16 mil² and w_min = 1.367 mm. Internal gives 192.93 mil² and 3.556 mm.
- 5 A, 20 K, 25 °C, 2 oz, external, 2.5 mm × 100 mm: expect w_min 0.9079 mm, R 10.819 mΩ, ΔV 54.09 mV, P 270.5 mW.
- 10 A, 20 K, 1 oz, external, 5 mm × 80 mm: expect w_min 4.724 mm, u_w 0.945, R 8.655 mΩ, ΔV 86.55 mV.
- Set the copper weight to 0.5 or 6 and the rise to 5 K. Expect the "copper" and "rise" warnings. Set I to 100 A to see the usual-range notice.
- Make the trace narrower than w_min (finding 02). The check turns to FAIL, but R and P stay at the allowed rise.
