# elec.enclosure-heat — independent re-derivation (Track P, P1)
Calculator: Enclosure heat dissipation and fan sizing, version 1.0.0 (re-checked at 1.1.0). Source file: src/Tempest.Calc/Tempest.Calc.Electrical/EnclosureHeatCalculator.cs. Spec: docs/calcs/electrical/elec.enclosure-heat.md.
Script: tools/verification/elec.enclosure-heat.py (exit 0 = all KATs match; 102 rows: 8 KAT rows and 94 extra rows).
Verdict: MATCH WITH FINDINGS (reviewed: no High; one Medium)

All 8 KAT values match. All 9 extra probes match. The arithmetic is sound. The findings concern the IEC TR 60890 label and weak guards, not wrong numbers.
Disclosure: I wrote the script before reading the Calculate method. While viewing the helper `Loads` I also saw the public `PressureAtAltitude` line. My script already used the ISA relation.

## Method re-derived
- Heat load P = Σ P_i n_i d_i + α I_sun A_sun. Units W.
- Effective surface A_eff = Σ f_face A_face. Faces: top and bottom W·D, front and rear W·H, sides D·H. Unit m².
- Natural rise ΔT = P/(h A_eff). Natural capacity at the limit P_nat = h A_eff (T_max − T_amb). Cooling unit P_cool = max(0, P − P_nat).
- Air density ρ = p(z)/(R T_amb), R = 287.055 J/(kg K), T_amb in kelvin. Pressure from the ISO 2533 standard atmosphere: 101325 (1 − 2.25577e-5 z)^5.25588 Pa.
- Forced airflow V = P/(ρ c_p ΔT_allow), c_p = 1006 J/(kg K), converted to m³/h. Net airflow credits P_nat. This matches the common manufacturer rule V ≈ 3.1 P/ΔT at sea level.
- Fan duty: root of p_fan(V) − p_sys (V/V_ref)² = 0 on the piecewise-linear fan curve. I scanned for the first sign change and refined it by bisection. Utilisation u_fan = V_req/V_duty.
- IEC TR 60890 (temperature-rise verification by calculation) and IEC 61439-1 clause 10.10 are the cited frameworks. Neither method is implemented. See -01.

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-wall-mounted | P_total (W) | 300 | 300 | 0 | 1e-9 | PASS |
| ka-wall-mounted | A_eff (m²) | 1.14 | 1.14 | 0 | 1e-9 | PASS |
| ka-wall-mounted | dT_nat (K) | 47.847 | 47.8469 | 2.3e-6 | 1e-4 | PASS |
| ka-wall-mounted | V_req (m³/h) | 62.48 | 62.4806 | 9.6e-6 | 1e-3 | PASS |
| ka-wall-mounted | P_cool (W) | 205.95 | 205.95 | 0 | 1e-4 | PASS |
| ka-fan-duty | V_duty (m³/h) | 113.75 | 113.746 | 3.6e-5 | 1e-3 | PASS |
| ka-fan-duty | p_duty (Pa) | 25.88 | 25.8762 | 1.5e-4 | 2e-3 | PASS |
| ka-fan-duty | u_fan | 0.5493 | 0.5493 | 2.2e-8 | 2e-3 | PASS |

No KAT value disagrees with physics. The KAT calls itself "IEC TR 60890 / manufacturer guides". It follows the plain-area manufacturer method, not the IEC table method.

## Extra probes (product vs independent)
The script compares 8 to 11 symbols per case (P_total, A_eff, dT_nat, T_int, P_nat_max, P_cool, rho_air, V_req, V_req_net, and the fan values where used). The tolerance is 1e-5 because the product pressure exponent is 5.2559 and mine is 5.25588.

| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| P1 free-standing | 0.6 × 1.8 × 0.4 m, 800 W, 2000 m, T_max 55 °C | A_eff | 2.76 m² | 2.76 | 0 | PASS |
| P1 | same | V_req | 159.276 m³/h | 159.276 | <1e-6 | PASS |
| P2 sunlit | 200 W + α 0.6 × 1000 W/m² × 1.5 m² | P_total | 1100 W | 1100 | 0 | PASS |
| P3 zero load | 0 W | dT_nat | 0 K | 0 | 0 | PASS |
| P4 large | 5 m cube, 50 kW, −20 °C, 4000 m | rho_air | 0.848243 | 0.848244 | 1.9e-6 | PASS |
| P9 qty and duty | 3 × 100 W × 0.5 + 2 × 20 W | P_total | 190 W | 190 | 0 | PASS |
| P11 six-point curve | 450 W, 45 °C, 1500 m, p_sys 30 Pa at 120 m³/h | V_duty | 143.199 m³/h | 143.199 | <1e-8 | PASS |
| P6 zero resistance | p_sys 0 | V_duty | 200 m³/h | 200 | 0 | PASS |
| P13 curve from 50 m³/h | KAT load | V_duty | 113.746 m³/h | 113.746 | <1e-8 | PASS |
| P12 below sea level | −500 m | rho_air | 1.215040 | 1.215040 | 2e-7 | PASS |

Unit input in kW for a load row (0.1 kW × 3) gave 300 W. The no-crossing case gave the warning and a failed check, as designed.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| A_eff | IEC TR 60890 uses surface factors b per face (about 1.4 top, 0.9 vertical, from memory) | Plain area × user factor 0 to 1; worksheet step cites "IEC TR 60890 approach" | Label only, see -01 |
| Temperature rise | IEC TR 60890 uses its own correlation at mid-height and at the top | ΔT = P/(h A_eff), mean rise, h editable (5.5) | Documented simplification |
| Density | p(z)/(287.055 T_amb) | Same; exponent 5.2559 | Yes (2e-6 at 4000 m) |
| Airflow | P/(ρ c_p ΔT) | Same; also a net form | Yes |
| Fan duty | Intersection of the curve and the V² resistance | Bisection inside the curve range | Yes for one crossing |
| Limit guard | T_max > T_amb | Block "limit" | Yes |
| All faces covered | Block with a clear message | Block "engine: Division by zero" | Weak, see -02 |
| Large rise | Method validity limit | No guard | See -03 |
| Two crossings (stall dip) | Choose the stable point | Warning "no crossing" | See -04 |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) | Status @ 614ddf89 | Status @ 298ade36 |
|---|---|---|---|---|---|---|---|
| P1-elec.enclosure-heat-01 | Medium | Medium (confirmed). The cited manufacturer practice uses factored areas, for example 1.8·H·(W + D) + 1.4·W·D for a free-standing case. That equals the b-factor sum here. The direction of the error depends on the mounting. Owner to confirm against licensed copy. | The A_eff step is labelled "IEC TR 60890 approach", but it uses plain face areas. It uses neither the surface factors b nor the IEC temperature-rise correlation. A user can read the result as an IEC TR 60890 verification. | Floor-standing 0.6 × 1.8 × 0.4 m, all vertical faces free: plain A_eff 3.840 m², with b factors 3.576 m². The plain-area rise is 6.9 % lower (unconservative) for the same h. The wall-mounted KAT goes the other way, because the product gives the rear zero area. The b values are from memory; check them against your copy. | Rename the label to "plain exposed-area method (manufacturer practice)". State that it is not an IEC TR 60890 or IEC 61439-1 10.10 verification. Optionally offer the b factors. | Open. Probe: floor-standing 0.6 × 1.8 × 0.4 m still gives plain A_eff 3.84 m². The label is unchanged. | Open. Probe: floor-standing 0.6 × 1.8 × 0.4 m still gives plain A_eff 3.84 m². The step label is unchanged. |
| P1-elec.enclosure-heat-02 | Low | Low (confirmed). It still blocks, so no wrong result is shown. | With all four exposed faces set to 0 (rear and bottom default 0), A_eff is 0. The result is the Block "engine: Division by zero". | f_top = f_front = f_left = f_right = 0. | Add a block rule "A_eff must be above 0", with a clear message. | Open. Probe: all four exposed factors 0 still gives Block 'engine: Division by zero'. | Open. Probe: all four exposed factors 0 still gives Block 'engine: Division by zero'. |
| P1-elec.enclosure-heat-03 | Low | Low (confirmed). | T_int is the mean air rise from a constant h. For large rises there is no validity warning, and no allowance for the hotter top of tall enclosures. | P2: ΔT 123.5 K, T_int 158.5 °C. Only the cooling-unit warning appears. | Warn when the rise exceeds a stated limit (for example 50 K). Label T_int as a mean value. | Open. Probe: sunlit case still prints T_int 210 °C at the default size with only the cooling-unit warning. | Open. Probe: the sunlit case still prints T_int 210.4 °C with only the cooling-unit warning. The cooling pack prints 189.0 °C. |
| P1-elec.enclosure-heat-04 | Info | Info (confirmed). The product reports no crossing, which fails the check. That is conservative. | A fan curve with a stall dip can cross the system curve twice. The product tests the end points only and reports "no crossing". | V_pts 0, 100, 200 m³/h with p 50, 0, 50 Pa and p_sys 10 Pa at 100 m³/h. | Scan for all sign changes. Report the stable (right-hand) point. | Open (Info). Probe: stall-dip curve still gives 'no crossing' and a failed fan check. | Open (Info). Probe: the stall-dip curve still gives 'no crossing' and a failed fan check. |
| P1-elec.enclosure-heat-05 | Info | Info (confirmed). | The pressure exponent is 5.2559. The ISA value is 5.25588. The effect is below 2e-6. | P4 at 4000 m. | None needed. | Open (Info). Exponent unchanged. | Open (Info). The exponent in the source is still 5.2559. |
| P1-elec.enclosure-heat-R1 | Low | Low (new) | Air density is taken at T_amb. An exhaust fan moves air at about T_max, so it must deliver a larger volume. u_fan is then about 5 % low for a 15 K rise. | 35 °C in, 50 °C out: 323.15/308.15 = 1.049. | Add a fan-position choice (inlet or exhaust), or note that V_req is at inlet conditions. | Open. Probe: rho_air still taken at T_amb (1.1455 kg/m³). | Open. Probe: rho_air is still taken at T_amb (1.1455 kg/m³). |
| P1-elec.enclosure-heat-R2 | Low | Low (new) | When T_amb ≥ T_max the run blocks. That is the usual case for a cooling unit. The unit must then remove P plus the gain h·A_eff·(T_amb − T_max). The block is safe, but the tool gives no answer. | Probe: T_amb 45 °C, T_max 40 °C, 300 W: Block 'limit'. | Compute P_cool = P_total + h·A_eff·(T_amb − T_max) in that case, and skip the ventilation outputs. | Open. Probe: T_amb 45 °C, T_max 40 °C still Blocks 'limit'. | Open. Probe: T_amb 46 °C against T_max 45 °C still blocks 'limit'. It now stops the cooling pack. |

## Notes for the owner's manual test
- KAT 1 by hand: A_eff = 0.18 + 0.48 + 2 × 0.24 = 1.14 m². Then ΔT = 300/(5.5 × 1.14) = 47.85 K and V_req = 62.48 m³/h.
- Cooling unit: 300 − 5.5 × 1.14 × 15 = 205.95 W. The net airflow is 42.89 m³/h.
- Raise the altitude to 2000 m with the same load. Expect ρ = 0.8987 kg/m³ and V_req about 79.6 m³/h. Check by hand with p = 101325 (1 − 2.25577e-5 z)^5.25588.
- Fan duty: curve (0, 60), (100, 30), (200, 0) with p_sys 20 Pa at 100 m³/h. Expect 113.75 m³/h, 25.88 Pa, u_fan 0.549.
- Compare the area with your own IEC TR 60890 surface-factor sum. Expect a different A_eff and a different rise (-01).

## Independent review
- Reviewer: senior reviewer (Claude), 2026-10-24.
- I re-ran the script: all rows pass. It is an independent derivation. The product values in it are recorded probe outputs, which is fine.
- I read Run line by line. The density, airflow, cooling-unit and bisection steps are right.
- I reproduced the block for T_amb above T_max with the probe (R2).
- Changes: all original severities stand. R1 and R2 added, both Low.
- Residual doubt: the b factors and the manufacturer area formula in 01 are from memory. Owner to confirm against licensed copy.

## Re-check after DA-17/DA-18 and A-4/A-5 (2026-10-24)
- Source changed: version 1.1.0 adds the input P_add, a linked heat load (for E6's dissipated heat). P_total is now P_loads + P_add + P_solar.
- An empty loads table is allowed when P_add is above 0. Nothing else changed.
- Probe at 614ddf89: all seven findings are still Open. None of them touches P_add.
- Probe check of P_add: 300 W with an empty table gives P_total 300 W. ΔT is 47.85 K and P_cool 205.95 W.
- Script updated: new case P14 for P_add. It runs 102 rows and exits 0.
- New defects: none found.

## Re-check after A7b/A8 (2026-10-24)
- Source changed: version 1.2.0 declares output ports. P_cool goes to B5 Q_req. The new T_max port echoes the limit and goes to B5 T_h_in. The maths is unchanged.
- Probe at 298ade36: all seven findings are still Open. The A_eff label still says "IEC TR 60890 approach".
- Chain use: R2 (a hot ambient blocks the run) and -03 (T_int printed as 189 °C) show in pack.cooling. See chain.cooling.
- Script updated: cases P15 (the cooling-pack panel, P_cool 3422.4 W) and P16 (the T_max port). It runs 114 rows and exits 0.
- New defects: none found.
