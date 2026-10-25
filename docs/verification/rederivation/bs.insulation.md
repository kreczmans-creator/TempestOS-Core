# bs.insulation — independent re-derivation (Track P, P1)
Calculator: Insulation thickness (ISO 12241 formula route), version 1.1.0. Source files: src/Tempest.Calc/Tempest.Calc.BuildingServices/InsulationCalculator.cs and InsulationModel.cs (dew point from GlaserModel.cs). Spec: docs/calcs/building-services/bs.insulation.md.
Script: tools/verification/bs.insulation.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS (reviewed: one High, -01)

The product matches the independent script on all 51 KAT values (worst relative difference 5.8e-15).
It also matches on 13 extra probes (114 values, below 1e-8) and behaves as specified on 16 guard probes.
The one material finding is a sub-zero humidity reading with no runtime warning.

## Method re-derived
- Pipe: R' = R_oth + ln(D_e/D_i)/(2πλ) + 1/(h_se·π·D_e), D_e = D_i + 2t. Flat: R = R_oth + t/λ + 1/h_se. Incropera, Fundamentals of Heat and Mass Transfer, 7th ed., Ch. 3; same form as BS EN ISO 12241 (formula route).
- Heat flow q = (θ_i − θ_a)/R. Surface temperature θ_se = θ_a + q/(h_se·A), with A = π·D_e per metre or 1.
- Inner film and wall: 1/(h_i·π·D_p) + ln(D_i/D_p)/(2πλ_w), D_p = D_i − 2t_w. Flat: 1/h_i + t_w/λ_w.
- Radiation: h_r = ε·σ·(T_s² + T_a²)(T_s + T_a), σ = 5.670374419e-8 W/(m²K⁴).
- Free convection in air, Holman, Heat Transfer, simplified air relations. Horizontal cylinder 1.32(ΔT/D)^0.25 and 1.24ΔT^(1/3). Vertical plate 1.42(ΔT/L)^0.25 and 1.31ΔT^(1/3). The larger is used.
- Forced convection: Churchill–Bernstein for a cylinder in cross-flow (Incropera 7th ed., Eq. 7.57). Flat plate laminar 0.664Re^0.5Pr^(1/3) below Re 5e5, mixed (0.037Re^0.8 − 871)Pr^(1/3) above (Eq. 7.30 and 7.41).
- Mixed convection blend h = (h_nat³ + h_forced³)^(1/3). Incropera, Sec. 9.9. Air k = 0.0241(T/273.15)^0.81, ν = 1.33e-5(T/273.15)^1.7, Pr 0.71 at the film temperature.
- Surface temperature solved from (θ_i − θ_se)/R_in = A·h(θ_se)·(θ_se − θ_a) with Brent's method.
- Dew point: Magnus form of ISO 13788 (610.5 Pa; 17.269 and 237.3 over water; 21.875 and 265.5 over ice), inverted.
- Required thickness: the least t beyond which the criterion holds for every thicker layer up to 1 m. Scan, then bisect.
- Utilisations: u_q = |q|/q_max; u_se = (θ_se − θ_a)/(θ_se,max − θ_a); u_dew = (θ_a − θ_se)/(θ_a − θ_dew − margin). D_crit = 2λ/h_se at the bare surface.
- No BS 5422 table and no ISO 12241 table is used or needed. Neither is in the product.

## Known-answer cases
The script prints all 51 rows. This table gives each case with key values.

| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-pipe-chosen | R / q / θ_se | 2.069029 / 28.99911 W/m / 25.76919 °C | same | 0 | 1e-9 | PASS |
| ka-pipe-heat-loss | t_q / θ_se | 37.30360 mm / 24.55751 °C | same | 6e-16 | 1e-9 | PASS |
| ka-pipe-surface | t_se / thickness | 5.325914 / 37.30360 mm | same | 2e-15 | 1e-9 | PASS |
| ka-flat-closed-form | thickness / θ_se | 116 mm / 22 °C | same | 0 | 1e-9 | PASS |
| ka-flat-condensation | θ_dew / thickness | 19.14535 °C / 12.47905 mm | same | 4e-16 | 1e-9 | PASS |
| ka-pipe-condensation | thickness / q | 11.29066 mm / 18.69528 W/m | same | 2e-15 | 1e-9 | PASS |
| ka-correlation-still | h_c / h_r / h_se | 3.674839 / 0.580066 / 4.254904 | same | 5e-16 | 1e-8 | PASS |
| ka-correlation-still | q / t_q / t_se | 36.77262 W/m / 23.37433 / 7.842292 mm | same | 6e-15 | 1e-8 | PASS |
| ka-correlation-wind | h_c / h_se / q | 14.95590 / 19.91764 / 40.69004 | same | 1e-15 | 1e-8 | PASS |
| ka-flat-correlation | h_c / h_r / q_area | 2.069476 / 5.247289 / 28.84603 | same | 0 | 1e-8 | PASS |
| ka-wall-and-film | R_oth / R / q | 0.0069695 / 2.075998 / 28.90176 | same | 0 | 1e-9 | PASS |
| ka-dew-point-entered | thickness / θ_se | 16 mm / 21 °C | same | 2e-15 | 1e-9 | PASS |
| ka-flat-condensation-ice | θ_dew / thickness | −11.17880 °C / 443.4365 mm | same | 5e-15 | 1e-9 | PASS |
| ka-flat-wind-turbulent | h_c / h_se / thickness | 40.20648 / 45.36736 / 39.11825 mm | same | 0 | 1e-8 | PASS |
| ka-bare-complies-window | thickness / D_crit | 74.29172 mm / 40 mm | same | 2e-16 | 1e-9 | PASS |

No KAT is Secondary or Unconfirmed. All agree with the textbook relations as I know them. Against Churchill and Chu the still-air h_c of ka-correlation-still differs by 0.4 %.

## Extra probes (product vs independent)
All values agree to better than 1e-8. The sub-zero pair (Y13 and Y14) supports finding 01.

| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| Y1 | pipe 168.3 mm, λ 0.035, 150/10 °C, h 8, q 40, θ_se 50, 60 mm | thickness / u_q | 93.1086 mm / 1.35365 | same | <1e-9 | PASS |
| Y2 | chilled pipe 22 mm, 6/28 °C, RH 80 %, margin 0 | t_dew / θ_dew | 14.3156 mm / 24.2220 °C | same | <4e-10 | PASS |
| Y3 | flat, correlation, ε 0.3, 1 m/s, L 1.5 m | h_se / thickness | 5.20527 / 57.3155 mm | same | <1e-10 | PASS |
| Y4 | cold 273 mm pipe, correlation, −10/25 °C, RH 75 %, margin 2 K | thickness / u_dew | 46.1285 mm / 0.92512 | same | <1e-9 | PASS |
| Y5 | near zero: Δθ 0.05 K | thickness / q | 0 / 0.15708 W/m | same | <1e-10 | PASS |
| Y7 | flat with film 300 and wall 5 mm, 40 mm chosen | R_oth / u_q | 0.0034444 / 2.11459 | same | <2e-10 | PASS |
| Y8 | pipe 60.3 mm, 8 m/s wind, 200/0 °C | h_c / thickness | 30.1502 / 61.9562 mm | same | <3e-11 | PASS |
| Y9 | imperial: 6.625 in, 300/70 °F, 2 in | q / u_q | 57.9167 W/m / 1.93056 | same | <2e-10 | PASS |
| Y10 | bare complies: 300 mm pipe, q 1000 W/m | thickness | 0 | 0 | 0 | PASS |
| Y11 | flat, 40 m/s, L 3 m, 150/−20 °C | h_se / thickness | 95.7938 / 271.583 mm | same | <1e-10 | PASS |
| Y12 | cold flat, dew 12 °C entered, margin 3 K, 15 W/m² | t_dew / thickness | 5.5714 / 37 mm | same | <1e-10 | PASS |
| Y13 | −30/−10 °C, RH 90 %, margin 0 | thickness / θ_dew | 63.866 mm / −11.1788 °C | same | <7e-11 | PASS |
| Y14 | as Y13, dew point −10.1005 °C entered | thickness | 792.356 mm | same | <1e-10 | PASS |

Y6 (3000 mm, 600 °C, q 500 W/m) is blocked as "unattainable" by the product, and by the script (no t up to 1 m). With t_chosen = 100 mm the product shows "> 1000 mm" and FAIL.
Guard probes (product only): Δθ 0.005 K blocks. Dew point plus margin at or above ambient blocks. Hot-surface limit below ambient blocks. Wall without conductivity, or wall over the radius, blocks. No criterion and no thickness blocks. A dew margin on a hot pipe, or a θ_se limit on a cold pipe, warns `criterion-na`. A cold pipe without a margin warns `no-condensation-check`. The 10 mm pipe warns `bare-complies` and `critical-radius`. `theta_lambda` 42 K from the mean warns `lambda-temperature`.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| Resistances and q | cylinder and plane series | `Geometry`, `Evaluate`; wall and film at D_i − 2t_w | Yes |
| Surface temperature | θ_a + q/(h·A) | same; bisection to 1e-13 for the correlation | Yes |
| Radiation | ε σ (Ts²+Ta²)(Ts+Ta) | same, σ CODATA 2018 | Yes |
| Free convection | Holman, larger form | `Math.Max` of the two | Yes (see 02) |
| Forced convection | Churchill–Bernstein; flat plate 0.664 / mixed | same; Re 5e5 switch (`<=` in the product) | Yes |
| Blend | cube law | `Cbrt(n³ + f³)` | Yes |
| Air properties | fits in spec | same constants | Yes |
| Dew point | Magnus, ice below 0 °C | `GlaserModel.DewPoint`, branch on p ≥ 610.5 Pa | Yes (see 01) |
| Required thickness | last violation, then bisect | 400-point quadratic grid, bisect | Yes |
| Unattainable | block, or ">1000 mm" with t_chosen | as specified | Yes |
| Utilisations | q/q_max etc. | same formulas | Yes |
| D_crit | 2λ/h_se bare | `bare.H`, correlation uses bare h | Yes |
| Units | SI; °F, in verified | engine conversion | Yes |
| Hard limits | spec ranges | enforced; soft ranges warn | Yes |
| Sub-zero RH | stated in assumptions | no runtime warning | Partly (01) |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) |
| --- | --- | --- | --- | --- | --- |
| P1-bs.insulation-01 | Medium | High (upgraded). Reproduced Y13: θ_dew −11.179 °C, 63.9 mm, no notice. Cold stores and freezer rooms are plausible use, and their hygrometers read RH over water. Probe: −35 °C surface in −20 °C air at RH 85 % (water) gives 47.1 mm and passes. That air is 103 % RH over ice, so no thickness prevents frost. | For ambient air below 0 °C the sheet reads `RH_a` as relative to ice. Weather and instrument RH is relative to water. A user who enters it gets a dew point about 1 K too low and a thickness far too small. No runtime warning appears. The assumption text mentions it. | Flat, −30/−10 °C, margin 0, RH 90 %: product 63.9 mm (θ_dew −11.179 °C). With the frost point for 90 % relative to water (−10.100 °C) entered: 792.4 mm. Probes Y13 and Y14, no notice raised. | Add a warning, for example `rh-over-ice`, when θ_a < 0 and the dew point comes from RH_a. Or convert water-based RH to a vapour pressure first. |
| P1-bs.insulation-02 | Low | Low (confirmed). The Churchill and Chu values are the author's; I did not recompute them. | Taking the larger of the laminar and turbulent free-convection forms gives h_c above a Rayleigh-based correlation for small pipes. That lowers the predicted surface temperature, which is unconservative for the surface-temperature and dew-point criteria. | Churchill and Chu: D 0.1 m, ΔT 25 K gives 4.80 W/(m²K), the sheet's laminar form gives 5.25 (+9 %). D 0.03 m, ΔT 4 K: 3.82 against 4.49 (+17 %). With h_c lowered by 15 % in ka-correlation-still, t_se rises from 7.84 to 9.01 mm. | Keep the "indicative" label. Optionally switch the regime on Rayleigh number, or use Churchill and Chu for the cylinder. Prefer a recorded h_se. |
| P1-bs.insulation-03 | Info | Info (confirmed). | λ is a fixed input. The sheet shows the mean insulation temperature but does not iterate on it. A `lambda-temperature` warning needs `theta_lambda` to be entered. | h12 probe: warns only because `theta_lambda` was given. | None. The owner repeats the run with λ at the reported mean temperature. |
| P1-bs.insulation-04 | Info | Info (confirmed). | Neither BS 5422 tables nor the ISO 12241 surface-coefficient formulas are used. The default `h_se` of 10 W/(m²K) is a plain default, not a standard value. | Template and spec §B5. | None. Say so on the sheet if the owner compares with a BS 5422 table. |

## Notes for the owner's manual test
- Hot pipe D_i 100 mm, λ 0.04, h_se 10, 80/20 °C, 30 mm chosen: expect R = 2.0690 K·m/W, q = 28.999 W/m, θ_se = 25.769 °C. Check R by hand: 1.8701 + 0.1989.
- Same pipe with q_max 25 W/m and no thickness: expect 37.304 mm and θ_se 24.558 °C.
- Flat wall, λ 0.04, h_se 10, Δθ 60 K, limit 20 W/m²: expect exactly 116 mm and θ_se 22 °C.
- Cold flat 5 °C in 25 °C air, RH 70 %, margin 1 K: expect θ_dew 19.145 °C and 12.479 mm.
- Try ambient −10 °C, RH 90 %, margin 0, surface −30 °C. The sheet gives 63.9 mm. Compare with the frost-point reading in finding 01.
- Try a 10 mm pipe with λ 0.2, q 25 W/m: expect the bare-complies and critical-radius warnings, and 74.29 mm.

## Independent review
- Reviewer: senior reviewer (Claude), 2026-10-24, release 2f53791c.
- Script re-run: exit 0, 165 values. It is independent.
- Reproduced -01 (Y13) and a cold-store probe. Raised it to High: the sheet passes a thickness where frost cannot be prevented.
- Skimmed the other rows. No new finding.
- Verdict changed: one High finding.
