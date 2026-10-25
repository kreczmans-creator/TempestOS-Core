# bs.psychrometrics — independent re-derivation (Track P, P1)
Calculator: Psychrometrics, version 1.0.0. Source file: src/Tempest.Calc/Tempest.Calc.BuildingServices/PsychrometricsCalculator.cs (helpers in Fluids/MoistAir.cs and Fluids/Water.cs). Spec: docs/calcs/building-services/bs.psychrometrics.md.
Script: tools/verification/bs.psychrometrics.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS (after independent review: 1 Critical, 1 High open)

The state relations match to 1e-9. The cooling-with-dehumidification process swaps the sensible and latent loads (finding 01). It also gives a wrong total for a dry coil (finding R1).
The checkout was on branch claude/tempestos-v0260-test-readiness-s4jwh1, not release/v0.26.0. I did not switch branches.

## Method re-derived
- Saturation pressure: Hyland and Wexler (1983) over water (0 °C and above) and over ice (below 0 °C), as given in ASHRAE Handbook, Fundamentals, Ch. 1, eqs. 5 and 6. Units kPa. Enhancement factor taken as 1.
- Humidity ratio W = 0.621945 p_w / (p − p_w); relative humidity φ = p_w / p_ws (over ice below 0 °C).
- Enthalpy h = 1.006 t + W (2501 + 1.86 t) in kJ/kg dry air. Specific volume v = 0.287042 (t + 273.15)(1 + 1.607858 W)/p in m³/kg dry air. Density (1 + W)/v.
- Thermodynamic wet-bulb t*: ASHRAE Ch. 1 eq. 35 (water) and eq. 37 (ice), solved for t* by Brent's method.
- Dew point: temperature where p_ws equals p_w; frost point below 0 °C.
- Standard atmosphere: p = 101.325 (1 − 2.25577e-5 Z)^5.2559, ASHRAE Ch. 1 eq. 3.
- Sensible process: Q = ṁ (h₂ − h₁) at constant W.
- Cooling with dehumidification: condensate ṁ(W₁ − W₂); Q_total = ṁ(h₁ − h₂) − ṁ_w 4.186 t₂. Sensible load = ṁ [h(t₁, W₁) − h(t₂, W₁)]. Latent load = ṁ [h(t₂, W₁) − h₂].
- Steam: h₂ = h₁ + (W₂ − W₁) h_steam. Adiabatic humidification: constant h. Mixing: mass-weighted W and h.
- Saturation pressure was also cross-checked against the Wagner–Pruss (2002) equation. Hyland–Wexler agrees within 0.023 % at 20 to 90 °C.

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-saturation-20 | p_ws_1 | 2.3392 | 2.3388 | 1.7e-04 | 1e-3 | PASS |
| ka-saturation-20 | W_1 | 14.758 | 14.695 | 4.3e-03 | 5e-3 | PASS |
| ka-saturation-20 | h_1 | 57.555 | 57.419 | 2.4e-03 | 5e-3 | PASS |
| ka-saturation-20 | v_1 | 0.8498 | 0.85008 | 3.3e-04 | 2e-3 | PASS |
| ka-saturation-20 | t_dp_1 | 20 | 20.000 | 2e-15 | 1e-4 | PASS |
| ka-saturation-20 | t_wb_1 | 20 | 20.000 | 2e-15 | 1e-4 | PASS |
| ka-ashrae-example-1 | W_1 | 6.40 | 6.4008 | 1.2e-04 | 2e-2 | PASS |
| ka-ashrae-example-1 | h_1 | 56.6 | 56.725 | 2.2e-03 | 1e-2 | PASS |
| ka-ashrae-example-1 | v_1 | 0.897 | 0.89625 | 8.4e-04 | 5e-3 | PASS |
| ka-ashrae-example-1 | RH_1 | 13.9 | 13.980 | 5.7e-03 | 3e-2 | PASS |
| ka-ashrae-example-1 | t_dp_1 | 7.4 | 7.434 | 4.5e-03 | 5e-2 | PASS |
| ka-sensible-heating | Q | 20.306 | 20.306 | 2e-16 | 1e-6 | PASS |
| ka-sensible-heating | W_2 | 5 | 5 | 0 | 1e-9 | PASS |
| ka-sensible-heating | t_2_out | 30 | 30 | 0 | 1e-9 | PASS |
| ka-mixing | W_2 | 8.333 | 8.3173 | 1.9e-03 | 2e-3 | PASS (margin 5 %) |
| ka-mixing | h_2 | 46.41 | 46.370 | 8.7e-04 | 2e-3 | PASS |
| ka-mixing | t_2_out | 25.07 | 25.031 | 1.6e-03 | 2e-3 | PASS |

KATs matched: 4 of 4 cases (17 of 17 expected values). The product's own run is the same. Two KAT expected values are loose; see finding 05.

## Extra probes (product vs independent)
All rows are in the script (about 100 rows). Product values were read from the Probe tool. State probes agree to 1e-9 relative.
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| a | 25 °C, 50 % | W, h, v, t_dp, t_wb | 9.8810 g/kg, 50.322, 0.85804, 13.864, 17.889 | same | below 3e-9 | PASS |
| b | 30 °C, wb 22 °C, 1500 m | p, W, h | 84.556 kPa, 16.695, 72.865 | same | below 1e-6 | PASS |
| c, d | −10 °C, 80 % and wb −12 °C (ice) | W, t_dp, t_wb | 1.2789, −12.490, −10.648 | same | below 2e-9 | PASS |
| e, f | dew point 15 °C; h = 60 kJ/kg | W, t_wb | 10.647, 20.098; 12.468, 20.823 | same | below 3e-9 | PASS |
| g, q, r, x | 60 °C 100 %; 0 °C; −0.5 °C; −30 °C | W, h | 152.42, 3.7745, 3.6207, 0.2334 | same | below 3e-10 | PASS |
| u, v, w | dry air; 5 °C wb −3 °C; 5 °C dp −10 °C | W, t_wb | 0, 5.836; 0.0923; 1.5994 | same | below 1e-8 | PASS |
| o | 25 °C 50 %, 1000 kPa | W | 0.98710 | 0.98710 | 5e-11 | PASS |
| i | 28 °C 50 % to 12 °C sat. | Q_total, m_w | 24.111 kW, 3.0985 g/s | same | 2e-10 | PASS |
| i | same | Q_sensible | 7.818 kW | 16.448 kW | 52 % | FINDING 01 |
| i | same | SHR | 0.3222 | 0.6778 | 52 % | FINDING 01 |
| s, t | 40 °C 50 % to 14 °C 95 %; 35 °C 60 % to 10 °C | Q_total | 61.982; 60.367 kW | same | 1e-10 | PASS |
| s, t | same | SHR | 0.5654; 0.5710 | 0.4346; 0.4290 | 30 %; 33 % | FINDING 01 |
| j, aa | steam 20 °C 30 % to 8 g/kg; 10 °C 40 % to 6 g/kg | t_2, Q | 20.494 °C, 9.803 kW; 10.457 °C, 7.948 kW | same | 2e-9 | PASS |
| k, k2 | evaporative, eff 0.8; RH_2 90 % | t_2, W_2 | 20.377 °C, 11.787; 18.968 °C | same | 4e-10 | PASS |
| ab | evaporative, eff 1.0 | RH_2 | 99.41 % | 100 % (true adiabatic saturation) | 0.6 % | FINDING 03 |
| ac | sensible heating 20 to 45 °C, 2.5 kg/s | Q | 63.719 kW | 63.719 kW | 6e-12 | PASS |
| l | mixing 30 °C 90 % with 5 °C 100 % | W_2, h_2, t_2 | 14.889, 55.547, 17.713 | same | 2e-10 | PASS (warns fog) |

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| p_ws coefficients | Hyland–Wexler water and ice | same constants | Yes |
| W, h, v, ρ | ASHRAE ideal-gas forms | same | Yes |
| Wet-bulb | eq. 35 / 37, thermodynamic | same, by bisection; returns t if no root | Yes (see 06) |
| Dew point | frost point below 0 °C | same, bisection over −60 to 100 °C | Yes; fails below −60 °C (02) |
| Altitude pressure | eq. 3 | same | Yes |
| Sensible | ṁ Δh at constant W | same | Yes |
| Cooling: Q_total, m_w | as above | same | Yes |
| Cooling: sensible and latent | sensible = temperature change at W₁ | the two formulas are exchanged | No (01) |
| Steam | h₂ = h₁ + ΔW h_steam | same | Yes |
| Evaporative | constant wet-bulb (adiabatic saturation) | constant h, liquid enthalpy dropped | Close; eff = 1 gives 99.4 % (03) |
| Mixing | mass-weighted W and h | same | Yes |
| Guards | supersaturation, negative W, t_wb above t | present; text clear except engine blocks | Partly (02, 03) |
| Units | g/kg, kPa, kJ/kg, kW | correct | Yes |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) | Status @ 0772fcf8 |
|---|---|---|---|---|---|---|
| P1-bs.psychrometrics-01 | High | Critical. Reproduced (probe i). The swap occurs on every cooling-coil run. Sensible duty is understated by half at a typical SHR. | In cooling with dehumidification, Q_sensible and Q_latent are exchanged. Q_sensible holds ṁ(h(t₂,W₁) − h₂), the moisture-removal term. Q_latent holds ṁ(h₁ − h(t₂,W₁)), the temperature-change term. SHR is therefore 1 − SHR. Q_total is right. A coil selected from the split gets the wrong sensible capacity. | 28 °C 50 %, leaving 12 °C saturated: product sensible 7.82 kW, latent 16.45 kW, SHR 0.322. Correct: sensible 16.45 kW, latent 7.82 kW, SHR 0.678. Same swap at 40 °C 50 % to 14 °C 95 %. | Swap the two formulas and labels. Add a KAT with a known split. Consider reporting SHR on Q_sensible + Q_latent only. | Open (no change). Probe: 28 °C 50 % to 12 °C saturated still gives sensible 7.82 kW, latent 16.45 kW, SHR 0.322 (swapped). |
| P1-bs.psychrometrics-02 | Medium | Low. Reproduced. It is a block, not a wrong number. W = 0 and W below 0.0066 g/kg are rare in practice. | A valid very dry state gives the block "The bracket does not contain a sign change". The dew-point search stops at −60 °C. Dry air with W = 0 reports a dew point of −60 °C with no note. | 20 °C with W = 0.005 g/kg blocks; W = 0.01 works. −40 °C with RH 5 % blocks; 10 % works. W = 0 shows t_dp −60 °C. | Catch the failed search. Report "dew point below −60 °C" as a note, or extend the search range. Show dew point as undefined for W = 0. | Open (no change). |
| P1-bs.psychrometrics-03 | Medium | Low. Reproduced (eff 1.0 gives RH 99.41 %). Constant h is the usual textbook approximation; W error is 0.6 %. | Adiabatic humidification holds h constant and drops the liquid-water enthalpy. A saturation efficiency of 1.0 gives RH 99.4 %, not 100 %. A target RH_2 near saturation fails with the same engine block. With eff and RH_2 both given, eff wins without a note. | 20 °C 50 %, eff 1.0: RH_2 99.41 %, W_2 9.770 g/kg (saturated value 9.829). 30 °C 30 %, RH_2 99 % or 100 %: Block "bracket does not contain a sign change"; 98.8 % works (limit 98.88 %). | Conserve h − W·4.186·t* (constant wet-bulb) so the limit is exactly 100 %. Report a clear message when the target cannot be reached. Note when eff overrides RH_2. | Open (no change). Probe: eff 1.0 from 20 °C 50 % still gives RH_2 99.41 %. |
| P1-bs.psychrometrics-04 | Low | Low (confirmed) | Cooling with t₂ above t₁ is accepted with a warning only. The outputs then show negative loads and an absurd SHR. | 20 °C 60 %, "cooling" to 25 °C: SHR −2.9e13, Q_total −34.0 kW. | Block when t₂ ≥ t₁ for this process, or set SHR to "n/a" when the denominator is not positive. | Open (no change). Probe: cooling 20 °C 60 % to 25 °C still gives SHR −2.9e13, Q_total −34.0 kW. |
| P1-bs.psychrometrics-05 | Low | Low (confirmed) | Two KAT inputs and values are loose. The mixing KAT assumes stream b has W = 5.000 g/kg, but RH_b = 46.93 % gives 4.952 g/kg. RH_b = 47.38 % gives 5.000. The case passes with 5 % margin on its tolerance. The example-1 KAT expects h = 56.6, but its own relations give 56.72. | Script rows "ka-mixing input check" and ka-ashrae-example-1. The KAT status is already Unconfirmed for example 1. | Correct RH_b to 47.38 %. Recompute expected values from the relations. Compare example 1 with the printed edition. | Open (no change). |
| P1-bs.psychrometrics-06 | Low | Low (confirmed) | The fog warning for a supersaturated mixture uses the rule id "no-dehumidification". Wet-bulb for that nominal state is set equal to the dry-bulb. | Probe l: mixing 30 °C 90 % with 5 °C 100 %, RH_2 116.8 %. | Add a rule id such as "supersaturated-mix". Show the wet-bulb as n/a. | Open (no change). |
| P1-bs.psychrometrics-07 | Info | Info (confirmed) | The enhancement factor of 1 lowers W by about 0.4 % when saturated at 20 °C. At the example-1 state it is larger: with f = 1.0043 at the wet-bulb, W is 6.46 g/kg, not 6.40 (1 % higher). The model also accepts 100 °C and 1000 kPa. It warns only for the usual range. | Script, ka-ashrae-example-1; probe n2: 95 °C 90 % gives W = 1881 g/kg with a range warning only. | State the size of the effect near wet-bulb conditions in the assumption text. Optionally block above 90 °C. | Open (no change). |
| P1-bs.psychrometrics-08 | Info | Info (confirmed; owner to confirm table numbers against licensed copy) | The KAT source cites "Table 3" for moist-air properties at standard pressure. I recall the moist-air table as Table 2 and the saturated-water table as Table 3 in the 2017 edition. I could not check the printed edition. | KAT ka-saturation-20 source text. | Confirm the table numbers in the printed edition. | Open (no change). |
| P1-bs.psychrometrics-R1 | High | High (new in review) | Cooling with dehumidification accepts a leaving state wetter than the entering state. RH_2 left blank defaults to 100 %. A dry coil (t₂ above the entering dew point) then gets W₂ above W₁. Q_total = ṁ(h₁ − h₂) is far too small. Only a warning appears. | 28 °C 50 % to 20 °C, RH_2 blank, 1 kg/s: product W₂ 14.70 g/kg, m_w 0, Q_total 0.948 kW. A dry coil removes 8.224 kW (sensible only). The product is 88 % low. | When W_s(t₂) ≥ W₁, treat the coil as dry: hold W₂ = W₁ and report sensible only. Or block and point to the sensible process. | Open (no change). Probe: 28 °C 50 % to 20 °C with RH_2 blank still gives W₂ 14.70 g/kg, m_w 0, Q_total 0.948 kW. |
| P1-bs.psychrometrics-R2 | Low | Low (new in review) | Q_sensible + Q_latent equals ṁ(h₁ − h₂). Q_total also subtracts the condensate enthalpy. The parts do not add up to the total, and SHR uses the larger sum. | Probe i: Q_sensible + Q_latent 24.266 kW; Q_total 24.111 kW (0.6 % apart). | State the basis of the split, or apportion the condensate term. | Open (no change). |

## Notes for the owner's manual test
- 25 °C, 50 % RH, sea level: expect W 9.88 g/kg, h 50.32 kJ/kg, v 0.8580 m³/kg, dew point 13.86 °C, wet-bulb 17.89 °C.
- 28 °C, 50 % RH cooled to 12 °C saturated, 1 kg/s: expect total 24.1 kW. Sensible should be 16.4 kW and latent 7.8 kW. The tool shows them reversed.
- 28 °C, 50 % RH cooled to 20 °C with RH_2 blank: expect about 8.2 kW sensible. The tool shows 0.95 kW total (finding R1).
- 40 °C dry-bulb, 20 °C wet-bulb: expect W 6.40 g/kg, RH 13.98 %, dew point 7.43 °C (ASHRAE with enhancement factor reads W 6.46).
- Evaporative cooler with saturation efficiency 1.0 from 20 °C 50 %: expect leaving 13.78 °C and RH 99.4 % (true saturation is 100 %).
- Try W = 0.005 g/kg at 20 °C and the cryptic block appears.

## Independent review
- Reviewer: senior reviewer (Claude), 2026-10-24.
- I re-ran the script (exit 0) and read it. It is an independent derivation from ASHRAE Ch. 1 relations, not a translation of the C#.
- I reproduced findings 01, 02 and 03 with the probe. I read the Calculate method and MoistAir.cs line by line.
- Finding 01 is confirmed and raised to Critical. The code assigns ṁ(h(t₂,W₁) − h₂) to Q_sensible; that term is the moisture removal.
- Findings 02 and 03 are lowered to Low. Neither gives an unconservative number in normal use.
- New finding R1 (High): a dry-coil run with RH_2 blank returns about one ninth of the true load, with a warning only.
- New finding R2 (Low): the sensible and latent parts do not sum to Q_total.
- The script now carries rows for R1 and R2 (122/122 rows pass, exit 0).
- Residual doubt: the ASHRAE table numbers (finding 08) and Example 1 values need the printed edition.

## Re-check after the Addendum A Blue round (0772fcf8)
- Source change: none. The psychrometrics calculator and MoistAir.cs have no diff between 298ade36 and 0772fcf8.
- KATs: `Probe kat` passes all 5 cases and no expected value changed. The script exits 0.
- Spot-check with the live probe: -01 (sensible and latent still swapped), R1 (dry coil with RH_2 blank still gives 0.95 kW), -03 and -04 give the same numbers as the review.
- Findings: 10 earlier rows, 0 fixed, 10 open. The Critical/High findings -01 and R1 are still open.
- New defects: none; the code did not change.
- Probe inputs: /tmp/claude-0/probe-inputs/bs.psychrometrics.*.json (i, ab) and /tmp/claude-0/recheck4/struct-bs/.
