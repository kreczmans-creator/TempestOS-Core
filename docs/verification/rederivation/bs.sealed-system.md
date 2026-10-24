# bs.sealed-system — independent re-derivation (Track P, P1)
Calculator: Sealed heating system: emitters and expansion vessel, version 1.0.0. Source file: src/Tempest.Calc/Tempest.Calc.BuildingServices/SealedSystemCalculator.cs (helper Fluids/Water.cs). Spec: docs/calcs/building-services/bs.sealed-system.md.
Script: tools/verification/bs.sealed-system.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS (after independent review: 2 High open, owner to confirm the standard values)

The product matches the independent script on all 14 KAT values and on 11 extra probes (about 130 values, relative difference below 1e-8).
The findings are missing guards and one possible missing term in the vessel formula. They are not arithmetic errors.

## Method re-derived
- Mean temperature difference ΔT = (t_f + t_r)/2 − t_i, or the log mean (t_f − t_r)/ln((t_f − t_i)/(t_r − t_i)). BS EN 442-2:2014, cl. 5.
- Characteristic equation Φ = Φ50·(ΔT/50)^n, so Φ50,req = Φ_req/(ΔT/50)^n. BS EN 442-2:2014, cl. 5.
- Utilisation of a room = Φ_req/(Φ50·f_dT); the sheet reports the worst room.
- System volume V_s = ΣV_em + V_pipe + V_plant (L).
- Water density by Kell, J. Chem. Eng. Data 20 (1975), rational polynomial in t. The script gives ρ(10) = 999.702 and ρ(85) = 968.62 kg/m³.
- Expansion coefficient e_x = ρ(t_fill)/ρ(t_max) − 1.
- Pre-charge p_i = ρ(t_fill)·g·h/10⁵ + margin (bar gauge), with g = 9.80665 m/s².
- Vessel V_e = e_x·V_s/[1 − (p_i + 1)/(p_f + 1)]. This is Boyle's law with absolute pressure taken as gauge + 1 bar. It matches the BS 7074-1 form with charge equal to the initial pressure. BS EN 12828 Annex D adds a water reserve (review, finding 03).

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-dt50 | dT | 50 | 50 | 0.00e+00 | 1e-09 | PASS |
| ka-dt50 | f_dT | 1 | 1 | 0.00e+00 | 1e-09 | PASS |
| ka-dt50 | R1 | 1500 | 1500 | 0.00e+00 | 1e-09 | PASS |
| ka-dt50 | u_emit | 0.9375 | 0.9375 | 0.00e+00 | 1e-09 | PASS |
| ka-dt50 | V_s | 38 | 38 | 0.00e+00 | 1e-09 | PASS |
| ka-dt30 | f_dT | 0.5147503 | 0.5147503 | 3.94e-08 | 1e-06 | PASS |
| ka-dt30 | R1 | 2914.034 | 2914.034 | 4.35e-08 | 1e-06 | PASS |
| ka-lmtd | dT | 29.72013 | 29.72013 | 1.39e-07 | 1e-06 | PASS |
| ka-lmtd | f_dT | 0.5085164 | 0.5085164 | 5.76e-08 | 1e-06 | PASS |
| ka-vessel | e_x | 0.03208619 | 0.03208619 | 9.03e-09 | 1e-06 | PASS |
| ka-vessel | p_i | 0.7901852 | 0.7901852 | 2.65e-08 | 1e-06 | PASS |
| ka-vessel | V_e | 6.56806 | 6.56806 | 4.84e-08 | 1e-06 | PASS |
| ka-vessel | u_vessel | 0.8210075 | 0.8210075 | 4.84e-08 | 1e-06 | PASS |
| ka-manufacturer-dt30 | f_dT | 0.51 | 0.5147503 | 9.31e-03 | 1e-02 | PASS |

All KATs pass. Kell density agrees with the values in the ka-vessel note.
The ka-manufacturer-dt30 case passes with 0.93 % against a 1 % tolerance. The correct figure is 0.5148 for n = 1.3. It stays Unconfirmed until the owner checks a catalogue.

## Extra probes (product vs independent)
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| S1 | 70/50/21, AMTD, 3 rooms, 12 L vessel | V_e (L) / u_emit | 5.359537 / 1.519392 | same | <1e-9 | PASS |
| S2 | 80/40/20, log mean | ΔT (K) / R1 (W) | 36.40957 / 1510.361 | same | <1e-9 | PASS |
| S3 | 60/60/20, log mean (t_f = t_r) | ΔT (K) | 40.0 | 40.0 | 0 | PASS |
| S4 | 1100 L, fill 5, max 95 °C, 30 m, p_f 5 bar | e_x / p_i (bar) / V_e (L) | 0.039571 / 3.441889 / 167.6189 | same | <1e-9 | PASS |
| S5 | glycol record e = 0.05 | V_e (L) | 10.23503 | 10.23503 | <1e-9 | PASS |
| S6 | fill 4, max 30 °C | V_e (L) | 0.889215 | 0.889215 | <1e-9 | PASS |
| S7 | p_f 2.95 bar, PRV 3 bar | V_e (L) | 5.868117 | 5.868117 | <1e-9 | PASS |
| S8 | 10 000 L system | V_e (L) | 656.806 | 656.806 | <1e-9 | PASS |
| S9 | n 1.5, 90/90/16 °C (ΔT 74 K) | f_dT / u_emit | 1.800498 / 0.370268 | same | <1e-9 | PASS |
| S10 | fill 1, max 99 °C | e_x | 0.042560 | 0.042560 | <1e-9 | PASS |
| S11 | fill 30 = max 30 °C | V_e (L) | 0 | 0 | 0 | PASS |
| S12 | units: 0.09 m³, 250 kPa, 348.15 K, 68 °F, 16.4042 ft | V_e (L) | 6.568060 | 6.568060 | <1e-8 | PASS |
| S13 | V_s = 9 L, 10 010 L | block "volume" | blocked | blocked | – | PASS |
| S14 | h = 50 m (p_i 5.20 bar above p_f) | block "pressures" | blocked | blocked | – | PASS |

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| ΔT, AMTD and log mean | EN 442-2 cl. 5 | Same; log mean falls back to AMTD when t_f = t_r | Yes |
| Correction factor | (ΔT/50)^n | Same, 50 K fixed for both ΔT methods | Yes (see finding 07) |
| Water density | Kell 1975 | Same coefficients as the script (Fluids/Water.cs) | Yes |
| Expansion e_x | ρ_fill/ρ_max − 1 | Same; glycol record replaces it | Yes |
| Pre-charge | ρ·g·h + margin | Uses water density at fill, also with a glycol record | Partly (finding 05) |
| Vessel volume | Boyle's law, gauge + 1 bar | Same | Yes |
| Water reserve | BS EN 12828 Annex D (as recalled: 0.5 % of V_s, at least 3 L) | Not included | No (finding 03, owner to confirm) |
| Pressure order | p_i < p_f ≤ p_prv − 0.5 bar (as recalled) | Block only if not strictly below p_prv | No (finding 02, owner to confirm) |
| Temperature order | t_fill below t_max | No check; warning only for t_max below t_f | No (finding 01) |
| Volume limit | 10–10 000 L | Block outside the range | Yes |
| Units | SI inside | Mixed units convert correctly (S12) | Yes |
| ΔT range | Catalogue n valid in a range | No check | No (finding 04) |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) |
|---|---|---|---|---|---|
| P1-bs.sealed-system-01 | Medium | Low. Reproduced. The report was wrong: the vessel check FAILS (u = −0.088), so nothing unsafe passes. Fill above maximum is an edge input. | No guard when the fill temperature is at or above the maximum temperature. The vessel volume becomes zero or negative. The vessel check then shows FAIL with a negative utilisation (corrected in review). | t_fill 40, t_max 30 °C, V_pipe 90, V_plant 10 L: e_x = −0.003446, V_e = −0.704 L. Only an "outside usual range" notice on t_fill appears. | Block when t_max is not above t_fill (water case). Warn or block when V_e is not positive. |
| P1-bs.sealed-system-02 | Medium | High. Reproduced. As recalled, BS EN 12828 Annex D limits p_e to p_SV − 0.5 bar (p_SV ≤ 5 bar) or 0.9 p_SV (above 5 bar). The BS 7074-1 domestic vessel factors, as recalled, imply p_f = p_SV − 0.5 bar. Owner to confirm both against licensed copies. | The final pressure may sit just under the safety valve setting. A higher p_f gives a smaller vessel. I recall that BS EN 12828 asks for a margin below the valve setting; the owner should confirm the value in the standard. | p_f 2.95 bar with p_prv 3 bar is accepted. V_e = 5.868 L against 6.568 L at the default p_f 2.5 bar (−10.7 %). | Add a margin check between p_f and p_prv, set from the standard. Warn or block below it. |
| P1-bs.sealed-system-03 | Medium | High. Reproduced. As recalled, BS EN 12828 Annex D sizes V_n ≥ (V_e + V_WR)(p_e + 1)/(p_e − p_0). V_WR is at least 0.5 % of V_s and at least 3 L. The fill pressure sits at least 0.3 bar above p_0. BS 7074-1, as recalled, takes charge = initial pressure with no explicit reserve. The sheet cites BS EN 12828 for its check. Owner to confirm against licensed copies. | The vessel formula has no water reserve (seal) volume. If BS EN 12828 or the vessel maker requires one, the minimum volume is too small. I could not confirm the clause. | ka-vessel: V_e = 6.568 L. With an assumed 3 L reserve the same relation gives 12.71 L, and the 8 L vessel would fail. | Check the owner's copy of BS EN 12828 and BS 7074-1. Add the reserve term, or state in the spec that V_e excludes it. |
| P1-bs.sealed-system-04 | Low | Low (confirmed) | No range check on ΔT. The power law is applied far from the catalogue range with no notice. | t_f 30, t_r 30, t_i 24 °C gives ΔT 6 K and f_dT 0.0635. A 1000 W room needs 15 742 W at ΔT 50. No warning. | Warn when ΔT is outside about 10–70 K. Say which range the exponent n covers. |
| P1-bs.sealed-system-05 | Low | Low (confirmed) | With a glycol record the static head still uses the water density. The step is also labelled as water density. The effect is unconservative. | e_fluid 0.05, fluid 4 % denser than water (assumed): V_e 10.354 L against 10.235 L (+1.2 %). | Add an optional fluid density input. Relabel the step for glycol. |
| P1-bs.sealed-system-06 | Info | Info (confirmed) | Absolute pressure is gauge + 1 bar, not 1.01325 bar. The spec states this. | ka-vessel: 6.568 L against 6.593 L (+0.38 %). | None, or use 1.01325. |
| P1-bs.sealed-system-07 | Info | Info (confirmed) | The log-mean option divides by 50 K. At 75/65/20 the log mean is 49.83 K. A catalogue rated on AMTD 50 K then gives f_dT 0.9956 instead of 1. The error is conservative. | 75/65/20 °C with log mean: ΔT 49.83 K. | Note this in the spec, or state the basis of the catalogue rating. |
| P1-bs.sealed-system-R1 | Low | Low (new in review) | The pre-charge margin may be set to 0. As recalled, BS EN 12828 Annex D asks for p_0 at least 0.2 bar above the static head. With no margin the high point sits at 0 bar gauge when cold. Air can then enter at vents. The vessel volume itself grows, which is conservative. | p_margin input: HardMin 0, Min 0. Default 0.3 bar complies. | Warn when p_margin is below 0.2 bar (owner to confirm the value). |

## Notes for the owner's manual test
- Defaults with a 1500 W room and a 1600 W emitter at 75/65/20 °C: expect ΔT 50 K, f_dT 1, R1 1500 W, u_emit 0.9375.
- Set 55/45/20 °C and n = 1.3: expect f_dT 0.5148 (AMTD) and 0.5085 (log mean); compare 0.5148 with your radiator catalogue.
- For 100 L, fill 10 °C, maximum 85 °C, 5 m static, p_f 2.5 bar: expect e_x 0.03209, p_i 0.790 bar, V_e 6.57 L.
- Try t_fill 40 and t_max 30 (finding 01) and p_f 2.95 with p_prv 3 (finding 02).
- Compare V_e with your vessel maker's worked example, including any reserve volume (finding 03).

## Independent review
- Reviewer: senior reviewer (Claude), 2026-10-24.
- I re-ran the script (exit 0) and read it. It is independent; Kell density is a published formula, so sharing it is not a translation.
- I reproduced findings 01, 02 and 03 with the probe. I read the Run method line by line.
- Finding 01 is lowered to Low. The report said the vessel check passes; the probe shows it fails.
- Findings 02 and 03 are raised to High. Both make the vessel smaller than the cited standard asks, in ordinary use.
- They are not Critical: the safety valve still limits the pressure. The likely result is valve discharge and loss of water.
- Standard values above are from memory, not from licensed copies. Owner to confirm the 0.5 bar margin, the 0.9 p_SV rule, and the 0.5 % / 3 L reserve.
- Supporting check: the recalled BS 7074-1 factors (0.063, 0.084, 0.126 at a 3 bar valve) all give e = 0.0360 only with p_f = 2.5 bar.
- New finding R1 (Low): no lower limit on the pre-charge margin.
- I fixed the script's observation labels (04, 05, 06 were out of step with the report) and added the review figures.
