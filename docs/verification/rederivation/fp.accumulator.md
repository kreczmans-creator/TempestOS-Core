# fp.accumulator — independent re-derivation (Track P, P1)
Calculator: Accumulator sizing (gas law), version 1.1.0. Source file: src/Tempest.Calc/Tempest.Calc.FluidPower/AccumulatorCalculator.cs (kernel: src/Tempest.Calc/Tempest.Calc.Engine/Physics/GasLaw.cs). Spec: docs/calcs/fluid-power/fp.accumulator.md (ADR-0170).
Script: tools/verification/fp.accumulator.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS (reviewed: one Critical, three High; see Independent review)

The arithmetic matches my independent script on all 9 KATs and 15 extra probes. The findings concern the process model in the fast modes, the real-gas treatment and one temperature scale.

## Method re-derived
- Absolute pressure: pa = p_gauge + p_atm (gauge entry) or as entered. Units bar.
- Polytropic gas law on a fixed gas mass: p V^n = const, n = 1 (isothermal) to 1.4 (adiabatic, nitrogen). Source: any fluid-power text (for example Esposito, Fluid Power with Applications, accumulator chapter).
- Gas volume between the limits: V1 = V0 (p0/p1)^(1/n), V2 = V0 (p0/p2)^(1/n), so dV = V1 − V2 and V0 = dV / [(p0/p1)^(1/n) − (p0/p2)^(1/n)].
- Temperature: the pre-charge at temperature T is p0(T) = p0 · T/Tmax (constant mass and volume, T in K). The pre-charge at the charging temperature is p0·(Tc + 273.15)/(Tmax + 273.15).
- For n = 1 the cold-gas volume scale is exactly Tmax/Tmin. For n above 1 it is (Tmax/Tmin)^(1/n), because the bracket contains p0(Tmin).
- Rules on absolute pressures: u_r0 = pa0/(0.9 pa1); u_rp = (pa2/p0(Tmin))/4.
- Next size = smallest listed size not below V0. Bar·litre = PS (bar gauge) × vessel volume (L). 1 bar·L = 100 J.
- Real gas (my own check, not in the product): Z-corrected V0 = dV / [(Z1/Z0)(p0/p1) − (Z2/Z0)(p0/p2)]. Z comes from the Span et al. (2000) reference equation for nitrogen (CoolProp 8.0.0, 293.15 K). The first pass used Peng-Robinson, which reads about 3 % low at 200 bar.

## Known-answer cases
All 29 expected values agree to 3e-10 or better. Every KAT passes in the product (probe: 9/9).
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-parker-numbers-absolute | V0 | 29.69697 | 29.69697 | 1.0e-10 | 1e-9 | PASS |
| ka-parker-numbers-absolute | p0, u_r0, u_rp | 81.64475, 1, 0.58333 | same | <6e-11 | 1e-9 | PASS |
| ka-parker-numbers-gauge | V0 | 29.80704 | 29.80704 | 1.3e-10 | 1e-9 | PASS |
| ka-parker-numbers-gauge | u_r0, u_rp | 1.001115, 0.579622 | same | <3e-10 | 1e-9 | PASS |
| ka-adiabatic | V0 | 36.69279 | 36.69279 | 9.8e-11 | 1e-9 | PASS |
| ka-charge-temperature | p0, p0_set, p0_min | 74.70905 | 74.70905 | 3.9e-11 | 1e-9 | PASS |
| ka-size-and-real-gas | V0, V_sel, u_V, bar_litres | 31.18182, 32, 0.97443, 7360 | same | <2e-11 | 1e-9 | PASS |
| ka-below-pssr | V0, bar_litres, u_r0, u_rp | 1.25, 220, 0.88889, 0.625 | same | <2e-11 | 1e-9 | PASS |
| ka-ps-above-working-pressure | V0, bar_litres, u_rp | 1.256427, 264, 0.615735 | same | <4e-10 | 1e-9 | PASS |
| ka-temperature-range | f_T, V0, V_sel, u_V, p0_min, u_rp | 1.102337, 32.73606, 50, 0.654721, 81.64475, 0.64303 | same | <3e-10 | 1e-9 | PASS |
| ka-parker-hy10-4004 (Secondary, Unconfirmed) | V0, V_sel | 29.7, 32 | 29.69697, 32 | 1.0e-4 | 5e-3 | PASS |

Physics review of the KATs: all agree with the gas law and the stated source (analytic). The Secondary KAT cannot tell the absolute reading (29.697 L) from the gauge reading (29.807 L), because both lie within its 0.5 % tolerance. It does not check the Parker catalogue itself; I cannot check that catalogue here.

## Extra probes (product vs independent)
Product values come from the probe run on 2026-10-25. The script re-runs the comparison. All 15 agree to better than 3e-8 on V0, p0, p0_set, p0_min, f_T, u_r0, u_rp, V_sel, u_V and bar_litres.
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| X01 gauge | 20/30/100 bar g, dV 5 L | V0 | 10.64889 L | 10.64889 | 2e-10 | PASS |
| X02 absolute | same numbers, absolute | V0 | 10.71429 L | 10.71429 | 4e-10 | PASS |
| X03 p0 = p1 | 100/100/200 abs | V0 | 20.0 L | 20.0 | 0 | PASS |
| X04 extreme T | n 1.4, 150 to −40 °C | V0 / f_T | 50.1106 L / 1.81493 | same | 1e-10 | PASS |
| X05 charge hot | Tmax −40, Tc 100 | p0 | 144.0425 bar | 144.0425 | 2e-10 | PASS |
| X06 largest | 600/650/700 bar g, PS 700, V 5000 | V0 / bar_litres | 1518.665 L / 3.5e6 | same | 2e-10 | PASS |
| X07 polytropic | n 1.2, 20 to 50 °C, gauge | V0 | 17.63865 L | 17.63865 | 2e-10 | PASS |
| X08 near zero | dV 0.001 L, p0 0 gauge | V0 | 0.0130719 L | 0.0130719 | 3e-8 (print) | PASS |
| X09 size list | PS 160 g, sizes 20..50 | V_sel / bar_litres | 50 L / 8000 | same | 0 | PASS |
| X10 shock | n 1.4, 0 to 40 °C, gauge | V0 | 15.21822 L | 15.21822 | 3e-10 | PASS |
| X11 real gas | f_rg 1.3, 300/330/350 g | V0 | 250.8977 L | 250.8977 | 1e-10 | PASS |
| X12/X13 p_atm | 0.5 and 1.1 bar | V0 | 22.2655 / 22.3175 L | same | <3e-10 | PASS |
| X14 gauge zero | 0/0/10 g | V0 | 11.01325 L | 11.01325 | 0 | PASS |
| X15 units | psi, degF, US gal inputs | V0 | 22.22220 L | 22.22220 | 2e-10 | PASS |
Blocks confirmed in the product (exit 3): p0 > p1; p0 = 0 absolute; p2 ≤ p1; Tmin > Tmax; PS below p2 gauge (699 vs 700); V_vessel below V0. Hard limits also block f_rg 0.9, n 1.5, dV 0.0009 and 1001 L, p2 701 bar, p_atm 1.2 bar.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| V0 formula | dV/[(p0/p1)^(1/n) − (p0/p2)^(1/n)] | GasLaw.ChargeVolume, same form; isothermal path exact | Yes |
| Gauge/absolute | Absolute inside the law | pa = p + p_atm for Gauge; basis choice defaults to Gauge | Yes |
| Temperature scale | (Tmax/Tmin)^(1/n) exact; Tmax/Tmin for n = 1 | Tmax/Tmin for every n | Conservative for n > 1 (P1-03) |
| Kelvin conversion | +273.15 | Unit system converts degC, K, degF correctly (X15, X04) | Yes |
| p0 at charge temperature | p0·Tc/Tmax | same; p0_set subtracts p_atm for Gauge | Yes |
| p0 ≥ p1 guard | V0 too small if p0 above p1 | Block when pa0 > pa1; p0 = p1 allowed (u_r0 = 1.11 FAIL) | Yes |
| p0 rule, p2/p0 rule | 0.9 and 4 records | Absolute pressures, PASS/FAIL checks, not blocks | Yes (documented; see P1-04) |
| Real gas | Z-dependent | One user factor f_rg, 1 to 1.5; warning above 200 bar abs on p2 only | Weak (P1-01, P1-02, R2) |
| Process path, emergency stroke | Isothermal charge (long hold at p2), adiabatic discharge | Adiabatic from the pre-charge state for every mode | No (R1) |
| PS and vessel guards | PS ≥ p2 gauge; V ≥ V0 | Blocks with 1e-9 relative slack | Yes |
| bar·L | PS × V, 1 bar·L = 100 J | PS × V_vessel or V_sel; text divides by 100 | Yes |
| PED category, stored energy (J) | Not in scope (ADR-0170) | Not given | By design |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) |
|---|---|---|---|---|---|
| P1-fp.accumulator-01 | High | High (confirmed; numbers corrected) | The real-gas factor is capped at 1.5, the default is 1, and p2 may reach 700 bar. At high pressure the correct factor is above the cap, so no valid input gives a safe V0. Only a warning appears. | Reference-EOS factors (isothermal, 20 °C): 450/500/700 bar abs needs 1.745; 600/650/700 needs 2.007. Product X06 returns 1518.7 L (ideal); the real-gas volume is about 3000 L. The warning says "enter a real-gas factor", but 1.5 is the limit. | Raise the f_rg limit, or compute the factor from nitrogen Z(p,T) at p0, p1 and p2. Block p2 above 200 bar abs while f_rg is 1. |
| P1-fp.accumulator-02 | Medium | High (upgraded) | f_rg is one number, but the true factor depends on Z at p0, p1 and p2. It grows for narrow pressure windows. The label invites the user to enter Z, which is unconservative. No warning appears at or below 200 bar abs. | Reference EOS, 20 °C: 270/300/350 bar abs needs 1.347, Z(p2) is 1.193 (12 % short). 170/190/200 bar abs needs 1.134 with no warning. Probe: 10 L at 170/190/200 abs gives 223.5 L and no realgas notice; the real gas needs 253.5 L. | Define f_rg in the label as the bracket ratio, or take Z0, Z1, Z2 as inputs. Warn from about 100 bar abs, or when the window p2 − p1 is narrow. |
| P1-fp.accumulator-03 | Medium | Medium (confirmed) | The cold-gas scale uses Tmax/Tmin for every n. For the adiabatic-from-pre-charge form the exact scale is (Tmax/Tmin)^(1/n). The result is conservative but over 5 % in plausible use. For the hold-then-stroke path (R1) the first power is exact. | dV 10 L, 90/100/200 bar g, n 1.4, 60 to −20 °C: product 36.51 L; exact 33.75 L (+8.2 %). 150 to −40 °C: +18.6 %. | Fix R1 first. Then keep Tmax/Tmin for the hold path and use the exact scale only where the adiabatic charge applies. |
| P1-fp.accumulator-04 | Low | Low (confirmed) | Both pre-charge rules use absolute pressures. If the maker states p2/p0 on gauge pressure, the check is less strict at low pressure. The 0.9 rule gives a 0.11 % fail for 90/100 bar gauge (conservative). | 2/3/10 bar g: product u_rp = 0.914 (pass); on gauge, p2/p0 = 5 gives u = 1.25. | Keep the record note. Add a basis choice for each rule. |
| P1-fp.accumulator-05 | Info | Info (confirmed) | The Secondary KAT cannot separate gauge from absolute readings. The 250 bar·L clause text was not checked against SI 2000/128. | 29.697 L and 29.807 L both lie within 0.5 % of 29.7 L. | Pin the catalogue reading when the owner confirms it. Owner to confirm the Schedule 1 clause against the licensed copy. |
| P1-fp.accumulator-R1 | — | Critical (new) | Every mode uses V0 = dV/[(p0/p1)^(1/n) − (p0/p2)^(1/n)]. That form assumes the gas is compressed adiabatically from the pre-charge and discharges at once. In emergency-stroke duty the gas is held at p2 and cools to ambient. The charge is then isothermal and only the stroke is adiabatic: dV = V0 (p0/p2)[(p2/p1)^(1/n) − 1]. The product undersizes by 14–29 % in normal use. The sheet prompts n near 1.4, which is exactly this case. | Probe: Emergency stroke, 14 L, 90/100/200 bar abs, n 1.4, 20 °C: product 38.65 L; hold-then-stroke 48.56 L (product 0.796 of required). 90/100/300: 0.709. With n = 1 the product gives 29.70 L, still short. With the real gas the hold-then-stroke volume rises another 31 % (reviewer CoolProp run: 4.55 against 3.47 L per litre of dV). Script "Model note 4". | Use the isothermal-charge, adiabatic-discharge form in Emergency stroke mode. Offer it in Energy storage when the hold is long. In Shock mode use the isothermal-at-p1, adiabatic-compression form, dV = V0 (p0/p1)[1 − (p1/p2)^(1/n)] (product about 3 % low at X10). |
| P1-fp.accumulator-R2 | — | High (new) | The ideal-gas exponent 1.4 is far below nitrogen's real isentropic exponent at working pressure. The sheet caps n at 1.4 and gives no warning at or below 200 bar abs. Adiabatic results there are 13–24 % small. | Reference EOS, isentrope from 20 °C: n_eff 1.53 at 60–100 bar, 1.68 at 100–200 bar, 2.13 at 250–350 bar. Adiabatic factor needed: 1.144 at 90/100/200 abs, 1.238 at 135/150/200, both with no warning. | Warn for every n above 1 from about 50 bar abs. Better, apply a built-in nitrogen real-gas correction for the isothermal and adiabatic paths. |
| P1-fp.accumulator-R3 | — | Low (new) | The 250 bar·L message couples PED / UK PER duties to the PSSR threshold. PED classifies a group 2 gas vessel from 50 bar·L (Annex II, Table 2; owner to confirm against licensed copy). A user below 250 bar·L may read "the exception may apply" as no duties at all. | Probe: 170/190/200 abs, PS 250 bar, 250 L: text "PSSR and PED / UK PER duties follow". Below 250 the text names no PED duty. | Say that PED / UK PER duties apply on their own thresholds whatever the PSSR indication. |

## Notes for the owner's manual test
- Enter 90/100/210 bar, dV 14 L, n 1, Absolute. Expect V0 = 29.697 L. Switch to Gauge. Expect 29.807 L and u_r0 = 1.0011 (FAIL).
- Set n = 1.4 on the same absolute numbers. Expect 36.693 L.
- Set Tmax 50 °C, Tmin 20 °C. Expect f_T = 1.1023, V0 = 32.736 L. With n = 1.4 compare against the exact (Tmax/Tmin)^(1/n) scale.
- Enter p0 above p1. Expect a block. Enter PS below p2 gauge. Expect a block. PS 230 bar and a 32 L size gives 7360 bar·L.
- Try p2 = 350 bar with p1 near p2 and f_rg = 1.13. Compare the volume with a nitrogen property table that gives Z at p0, p1, p2.
- Emergency stroke, 14 L, 90/100/200 bar abs, n 1.4. The product gives 38.65 L. A hold-then-stroke hand calculation gives 48.56 L.

## Independent review
Reviewer: Claude (senior review of the P1 pass), 2026-10-24.
I re-ran the script (all pass) and the probe KATs (9/9). I re-read Run() and GasLaw.ChargeVolume line by line.
I reproduced X06, the 200 bar warning edge (no warning at p2 = 200 bar abs) and an Emergency-stroke case with the probe.
Z values now come from the Span et al. reference equation (CoolProp 8.0.0), not Peng-Robinson. At 293 K: Z(200) 1.052, Z(300) 1.140, Z(500) 1.368, Z(700) 1.615.
The 500 bar value is above the 1.2–1.3 often quoted; owner to confirm against a NIST or REFPROP table.
P1-01 stays High with larger factors (1.745 and 2.007 needed against the 1.5 cap).
P1-02 is upgraded to High: 13 % undersizing at 200 bar abs passes with no warning.
New R1 (Critical): the fast modes use the wrong process path; Emergency stroke undersizes by 14–29 %.
New R2 (High): real nitrogen is much stiffer than n = 1.4 at working pressure.
New R3 (Low): the PSSR message implies PED duties start at 250 bar·L.
The PSSR arithmetic is right: bar_litres = PS (gauge) × V, and the text divides joules by 100.
Residual doubts: the exact PSSR Schedule 1 wording and the PED Table 2 limits need the licensed texts. Real duty cycles lie between the ideal paths; R1 and R2 bound the error, they do not pin it.
The script now carries reference-EOS Z values and the R1 check. It stays independent of the C#.
