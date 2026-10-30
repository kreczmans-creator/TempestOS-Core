# Insulation thickness (ISO 12241) (`bs.insulation@1.2.0`)

**In plain terms.** Insulation on a pipe or flat surface cuts heat loss, keeps a hot surface safe to touch and stops a cold one sweating, and the thickness needed depends on which of these you want. The worksheet finds the thickness that meets each requirement separately by the ISO 12241 formulae; the largest is the thickness to fit.

## Scope

Calculates the insulation thickness of a pipe or flat surface that meets a heat-loss limit, a hot-surface limit or an anti-condensation requirement. For a chosen thickness it gives the heat flow, surface temperature and PASS or FAIL.

## Method

- Pipe resistance per metre: R = ln(D_e/D_i)/(2π·λ) + 1/(h_se·π·D_e), D_e = D_i + 2t; with the inner film and pipe wall R_oth = 1/(h_i·π·D_p) + ln(D_i/D_p)/(2π·λ_w) added in series
- Flat resistance per square metre: R = t/λ + 1/h_se (+ 1/h_i + t_w/λ_w)
- Heat flow: q = (θ_i − θ_a)/R, positive from the process to the ambient
- Surface temperature: θ_se = θ_a + q/(h_se·π·D_e) (pipe), θ_a + q/h_se (flat)
- Surface coefficient: h_se as given, or h_se = h_c + h_r with h_r = ε·σ·(T_s² + T_a²)(T_s + T_a); h_c from free convection to air (laminar or turbulent, the larger), forced convection (cross-flow cylinder or flat plate) and their cube-law blend; solved with θ_se, which it depends on
- Heat-loss limit: the thickness beyond which |q| ≤ q_max; surface-temperature limit (hot): θ_se ≤ θ_se,max; anti-condensation (cold): θ_se ≥ θ_dew + allowance, θ_dew entered (for example from the psychrometrics calculation) or from the Magnus relation at θ_a and RH
- Thickness solution: each criterion's thickness by scanning t from 0 to 1 m for the last broken point and bisecting the bracket after it; governing t = max over the selected criteria
- Critical diameter: D_crit = 2λ/h_se; below it, insulation raises the heat flow until D_e passes D_crit

### Notes on the method

Pipe, per metre: `R_tot = R_oth + ln(D_e/D_i)/(2πλ) + 1/(h_se·π·D_e)`, `D_e = D_i + 2t`; flat, per square metre: `R_tot = R_oth + t/λ + 1/h_se`. `q = (θ_i − θ_a)/R_tot` (positive from the process to the ambient; the sheet reports the magnitude and says "heat loss" or "heat gain"); `θ_se = θ_a + q/(h_se·π·D_e)` (pipe) or `θ_a + q/h_se` (flat). `R_oth` is the optional inner film `1/(h_i·π·D_p)` and pipe wall `ln(D_i/D_p)/(2πλ_w)` (flat: `1/h_i + t_w/λ_w`), both off unless entered; with neither, the pipe's outer wall is at the process temperature.

**Reference diagram.** The worksheet form and the issued sheet (Figure 1) show a reference diagram (ADR-0158, DI-08). It is not to scale and shows inputs only, never a result. It draws the pipe in section, or the flat surface, with its wall and the insulation on it. The heat leaves the surface; the conditions and limits stand in one column. One variant per `geometry` and per `h_mode`. It binds `t_w`, `lambda`, `t_chosen`, `eps`, `L_surf`, `T_proc`, `T_amb`, `RH_a`, `v_wind`, `T_dew`, `h_i`, `q_max_A`, `theta_se_max`, `dew_margin`, `geometry`, `h_mode`, `h_se`, `D_i` and `q_max_m`.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Hard range | Group |
|---|---|---|---|---|---|---|
| `geometry` | Surface |  | pipe (cylinder) | pipe (cylinder), flat surface |  | Setup |
| `h_mode` | Where the outer surface coefficient comes from |  | h_se as given | h_se as given, convection + radiation (correlation) |  | Setup |
| `D_i` | Pipe outer diameter, the inside of the insulation (pipe) | mm | 100 | 10 to 1000 | 5 to 3000 | Geometry |
| `L_surf` | Height or flow length of the flat surface (correlation, flat) | m | 1 | 0.1 to 10 | 0.01 to 100 | Geometry |
| `t_w` | Pipe wall thickness, with its conductivity (optional; blank = not included) | mm | optional | 0.5 to 30 | 0.1 to 100 | Geometry |
| `t_chosen` | Chosen insulation thickness (optional; blank = report at the required thickness, with no checks; with it, nothing blocks) | mm | 40 | 0 to 300 | 0 to 1000 | Geometry |
| `lambda` | Thermal conductivity of the insulation | W/(m·K) | 0.04 | 0.015 to 0.2 | 0.005 to 2 | Material |
| `lambda_w` | Pipe wall conductivity (optional; blank = not included) | W/(m·K) | optional | 0.2 to 400 | 0.1 to 500 | Material |
| `T_lambda` (was `theta_lambda`) | Temperature the conductivity applies at, compared with the mean insulation temperature (optional; blank = no comparison) | °C | optional | -50 to 400 | -200 to 650 | Material |
| `T_proc` (was `theta_i`) | Process (fluid) temperature | °C | 80 | -50 to 400 | -200 to 650 | Conditions |
| `T_amb` (was `theta_a`) | Ambient air temperature (also the radiation sink) | °C | 20 | -20 to 50 | -60 to 70 | Conditions |
| `RH_a` | Relative humidity of the ambient air (anti-condensation) | % | 60 | 20 to 95 | 1 to 100 | Conditions |
| `v_wind` | Air speed over the surface (correlation; 0 = still air) | m/s | 0 | 0 to 20 | 0 to 60 | Conditions |
| `T_dew` (was `theta_dew`) | Dew point of the ambient air, if known, for example from the psychrometrics calculation (optional; blank = from T_amb and RH_a by the Magnus relation) | °C | optional | -50 to 50 | -100 to 70 | Conditions |
| `h_se` | Outer surface coefficient (as given; generic default of 10 W/(m²K), replace with the value for your surface; a high value is not conservative against condensation on a cold surface, use the correlation there) | W/(m²·K) | 10 | 3 to 40 | 0.5 to 200 | Factors |
| `eps` | Emissivity of the outer surface (for the correlation; with h_se as given it only judges that value against condensation on a cold surface) |  | 0.9 | 0.05 to 1 | 0.01 to 1 | Factors |
| `h_i` | Inner film coefficient (optional; blank = the pipe wall is at the process temperature) | W/(m²·K) | optional | 50 to 20000 | 0.5 to 100000 | Factors |
| `q_max_m` | Heat-loss limit per metre of pipe (optional; blank = not a criterion; the generic default of 25 W/m is to be replaced with your project limit) | W/m | 25 | 1 to 1000 | 1e-06 to 100000 | Requirements |
| `q_max_A` | Heat-loss limit per square metre of flat surface (optional; blank = not a criterion) | W/m² | optional | 1 to 500 | 1e-06 to 100000 | Requirements |
| `theta_se_max` | Highest surface temperature, for a hot surface (optional; blank = not a criterion) | °C | optional | 30 to 70 | -50 to 300 | Requirements |
| `dew_margin` | Allowance of the surface above the dew point, for a cold surface (optional; blank = not a criterion) | K | optional | 0 to 5 | 0 to 20 | Requirements |

## Assumptions

1. Heat flows in steady state, one-dimensionally, through a single homogeneous layer of constant conductivity. Basis: Formula route of the standard.
2. The insulation has no thermal bridges at supports, no joints and no moisture. Basis: Formula route of the standard.
3. The conductivity λ is the value entered, which should be the value at the mean insulation temperature on the sheet. Basis: Stated.
4. With the correlation, the outer surface coefficient is h_se = h_c + h_r. Basis: Incropera, §7.2, §7.4, §9.6 and §9.9 (section numbers to be checked against the owner's copy).
5. Radiation is h_r = ε·σ·(T_s² + T_a²)(T_s + T_a) to surroundings at the ambient air temperature. Basis: Incropera, §1.2.
6. Free convection to air uses Holman's simplified relations at atmospheric pressure, the larger of the laminar and turbulent forms. Basis: Holman's constants: horizontal cylinder 1.32(ΔT/D)^0.25 and 1.24ΔT^(1/3), vertical surface 1.42(ΔT/L)^0.25 and 1.31ΔT^(1/3).
7. Forced convection uses Churchill–Bernstein for a cylinder in cross-flow and the laminar or mixed flat-plate relations along a plate. Basis: Incropera, §7.2 and §7.4.
8. Air properties are fits at the film temperature, with Pr = 0.71. Basis: Stated.
9. Free and forced convection blend as (h_nat³ + h_forced³)^(1/3). Basis: Stated.
10. The correlation is indicative, not the standard's own. Basis: The standard's own coefficients are not reproduced.
11. The dew point is entered, or is the Magnus relation at the ambient temperature and humidity. Basis: Derived; the Glaser condensation calculation shares the function.
12. Below 0 °C the Magnus relation is over ice, so the humidity is then taken relative to ice. Basis: Stated; enter the dew point for sub-zero air, because weather humidity is usually relative to water.
13. An allowance above the dew point is the user's choice. Basis: Stated.
14. Pipes are treated as horizontal and flat surfaces as vertical for free convection. Basis: Stated.
15. Air conductivity is a fit within about 3 % over 250 to 400 K. Basis: Stated.
16. Air viscosity is a fit within about 4 % over 250 to 400 K. Basis: Stated.
17. The required thickness is the least thickness beyond which the criterion holds for every thicker layer up to 1 m. Basis: Stated; this matters when thin insulation on a small pipe raises the heat flow (critical diameter).
18. No thickness table of any standard, including BS 5422, is used or shipped. Basis: No standard's table is reproduced.
19. The conductivity is the user's input, from a library material or the maker's data. Basis: No standard's table is reproduced.

## Outputs

| Symbol | Description |
|---|---|
| `t_req_q` | Required thickness, heat-loss limit |
| `u_q` | Utilisation, heat-loss limit |
| `t_req_se` | Required thickness, surface-temperature limit |
| `u_se` | Utilisation, surface-temperature limit |
| `thickness` | Governing required thickness |
| `q_per_m` | Heat flow per metre (heat loss) |
| `T_se` | Outer surface temperature |
| `T_m` | Mean insulation temperature |
| `R_tot` | Total resistance |
| `D_crit` | Critical diameter (at the bare-surface h_se) |
| `governing` | Governing criterion |
| `q_area` | Heat flow per square metre (heat loss) |
| `T_dew` | Dew point of the ambient air |
| `t_req_dew` | Required thickness, anti-condensation allowance |
| `u_dew` | Utilisation, anti-condensation allowance |
| `h_c` | Convective surface coefficient |
| `h_r` | Radiative surface coefficient |
| `h_se` | Outer surface coefficient |
| `R_oth` | Resistance of the inner film and pipe wall |

`T_dew`; per selected criterion `t_req_q`, `t_req_se`, `t_req_dew` (mm) and, with a chosen thickness, its utilisation `u_q`, `u_se`, `u_dew`; `thickness` (governing required, mm; the text "> 1000 mm" when a criterion is unattainable, which breaks a downstream link to `thickness` (a link needs a number: choose a thickness only after the criteria are attainable); absent when no criterion applies); `governing` (text); `q_per_m` (W/m, pipe) or `q_area` (W/m², flat) and `T_se` at the evaluated thickness (chosen, else required); `T_m`; `R_tot`, `R_oth`; `D_crit` (pipe); `h_c`, `h_r`, `h_se` (correlation); series of heat flow and surface temperature against thickness. **Ports.** `thickness` (mm), `q_per_m` (W/m, pipe only), `q_area` (W/m², flat only), `T_se` (°C). **Checks (with `t_chosen`).** one utilisation check per selected criterion. **Warnings.** `criterion-na` (a hot-surface limit on a cold pipe, or a dew margin or an entered `T_dew` on a hot one, is ignored), `no-condensation-check`, `no-criteria` (a thickness is chosen but no criterion applies: the heat flow and surface temperature are reported), `unattainable`, `bare-complies`, `critical-radius`, `lambda-temperature`. **Blocks.** `temperatures`, `criteria` (none applies and no thickness is chosen), `unattainable` (no thickness chosen), `surface-limit`, `dew` (dew point + margin not below ambient), `wall`.

## Ports

A port is listed under its public name, `<area>.<quantity>`. The output symbol stays as the port's key, so a saved link, a pack binding or an inputs file that uses the symbol or an alias still resolves.

| Public name | Output symbol | Unit | Description |
|---|---|---|---|
| `insulation.thickness_required` | `thickness` | mm | Governing required insulation thickness (the largest of the selected criteria); the text "> 1000 mm" when a criterion is unattainable, which a downstream link cannot read |
| `insulation.heat_flow_per_length` | `q_per_m` | W/m | Heat flow per metre of pipe at the evaluated thickness (magnitude; pipe only) |
| `insulation.heat_flow_per_area` | `q_area` | W/m² | Heat flow per square metre of a flat surface at the evaluated thickness (magnitude; flat only) |
| `insulation.temperature_surface` | `T_se` (was `theta_se`) | °C | Outer surface temperature at the evaluated thickness |

## Checks

Checks the sheet makes:

- Chosen thickness against the heat-loss limit (ISO 12241)
- Chosen thickness against the surface-temperature limit (ISO 12241)
- Chosen thickness against the anti-condensation allowance (ISO 12241)
- Chosen thickness against the heat-gain limit (ISO 12241)

Applicability rules:

| Rule | Severity | Effect on the result | Condition |
|---|---|---|---|
| `temperatures` | Block | Conservative | The process temperature must differ from the ambient temperature. |
| `criteria` | Block | Conservative | At least one criterion that applies to the sign of θ_i − θ_a must be selected (a heat-flow limit, a hot-surface limit (hot) or a dew-point allowance (cold)) or a thickness chosen. |
| `unattainable` | Block | Conservative | A criterion that no thickness up to 1 m meets blocks the sheet when no thickness is chosen. |
| `unattainable-chosen` | Warning | Conservative | With a chosen thickness, a criterion that no thickness up to 1 m meets shows its required thickness as > 1000 mm, and the chosen thickness is checked against it. |
| `surface-limit` | Block | Conservative | The hot-surface limit must be above the ambient temperature. |
| `dew` | Block | Conservative | The dew point plus the allowance must be below the ambient temperature. |
| `wall` | Block | Conservative | The pipe wall needs both thickness and conductivity, and must be thinner than the pipe radius. |
| `criterion-na` | Warning | Conservative | A hot-surface limit on a cold pipe, or a dew-point allowance on a hot one, is ignored. |
| `no-criteria` | Warning | Conservative | No criterion applies but a thickness is chosen: only the heat flow and surface temperature at it are reported. |
| `rh-over-ice` | Warning | Conservative | The ambient air is below 0 °C and the dew point is computed from RH_a: RH_a is read as relative to ice (the dew point is a frost point), whereas weather and hygrometer humidity is relative to water. |
| `h-se-condensation` | Warning | Unconservative | A fixed h_se on a cold surface with a dew-point allowance gives a thinner layer than the correlation does: the generic default is not conservative against condensation. |
| `no-condensation-check` | Warning | Unconservative | The surface is colder than the air and no dew-point allowance is entered, so condensation is not checked. |
| `bare-complies` | Warning | Unconservative | The bare surface meets a heat-flow limit that thin insulation on a pipe below the critical diameter breaks. |
| `critical-radius` | Warning | Unconservative | The pipe is below the critical diameter 2λ/h_se, or the chosen insulation carries more heat than the bare pipe. |
| `lambda-temperature` | Warning | Conservative | The mean insulation temperature is more than 20 K from the temperature the conductivity applies at. |

## Limitations

Steady state, one layer of constant λ (no temperature or moisture dependence), no supports, joints or thermal bridges, no air gaps, no sheet-metal jacket resistance, no heat gain by radiation from sun or neighbours, no wind-dependent λ. Condensation is judged on the outer surface only (no vapour-barrier design: use B7 for the wall). Pipes are treated as horizontal and flat surfaces as vertical for free convection. The correlation is indicative (Holman's air relations and Churchill–Bernstein with fitted air properties); the ISO 12241 surface coefficients are not implemented. ISO 12241 and ISO 13788 clause numbers are cited by title only and are to be checked against the owner's copy. The unused-mode inputs (`eps` in given-h mode, `q_max_m` on a flat sheet) still raise the generic "tabulated value not from a record" warning, as the Glaser sheet does.

## References

- ISO 12241:2022: Calculation rules for thermal insulation of equipment and installations (clause numbers to be checked).
- Incropera, Fundamentals of Heat and Mass Transfer, §7.2, §7.4, §9.6, §9.9, 7th ed. (section numbers to be checked): External forced, free and mixed convection correlations.
- BS EN ISO 13788:2012, cl. 6 (clause number to be checked against the owner's copy): Magnus saturation vapour pressure (shared with the Glaser condensation calculation).

## Test cases

- `ka-pipe-chosen`: Hot pipe, 30 mm chosen: heat flow and surface temperature (Confirmed; source: Analytic (R = ln(D_e/D_i)/(2πλ) + 1/(h_se·π·D_e); q = Δθ/R; θ_se = θ_a + q/(h_se·π·D_e)))
- `ka-pipe-heat-loss`: Hot pipe: the thickness for a heat-loss limit of 25 W/m (Confirmed; source: Analytic (root of q(t) = 25 W/m))
- `ka-pipe-surface`: Hot pipe: the thickness for a 45 °C surface limit, and it is not the governing one (Confirmed; source: Analytic (root of θ_se(t) = 45 °C))
- `ka-flat-closed-form`: Flat wall: t = λ(Δθ/q − 1/h_se) in closed form (Confirmed; source: Analytic (plane layer))
- `ka-flat-condensation`: Cold flat surface: the thickness to keep the surface 1 K above the dew point (Confirmed; source: Analytic (plane layer, Magnus dew point))
- `ka-pipe-condensation`: Cold pipe: the thickness for the same dew-point allowance (Confirmed; source: Analytic (root of θ_se(t) = θ_dew + 1 K on a cylinder))
- `ka-correlation-still`: Hot pipe, h_se from convection and radiation in still air, low-emissivity cladding (Confirmed; source: Analytic (the stated correlation, solved with θ_se; recomputed in python3 with an independent implementation))
- `ka-correlation-wind`: The same pipe, black surface in a 3 m/s wind: forced convection blended with free (Confirmed; source: Analytic (Churchill–Bernstein cross-flow blended with free convection; recomputed in python3))
- `ka-flat-correlation`: Flat wall 2 m high, correlation in still air (Confirmed; source: Analytic (vertical-surface free convection and radiation; recomputed in python3))
- `ka-wall-and-film`: Pipe wall and inner film in series with the insulation (Confirmed; source: Analytic (series resistances; recomputed in python3))
- `ka-dew-point-entered`: Cold flat surface with the dew point entered (linked from the psychrometrics calculation) instead of computed (Confirmed; source: Analytic (plane layer))
- `ka-flat-condensation-ice`: Cold flat surface in sub-zero air: the dew point over ice (Confirmed; source: Analytic (plane layer, Magnus form over ice below 0 °C))
- `ka-flat-wind-turbulent`: Flat wall 10 m long in a 20 m/s wind: the turbulent flat-plate branch (Confirmed; source: Analytic (mixed laminar/turbulent flat plate, Re_L above 5×10⁵, blended with free convection; recomputed in python3))
- `ka-bare-complies-window`: Small pipe below the critical diameter: the bare pipe meets the limit, a window of thin layers does not (Confirmed; source: Analytic (root of q(t) = 25 W/m beyond the hump; recomputed in python3))

## Changes

**Changes in 1.2.0 (C-2 consistency, phase C).** Layout and wording; no calculated value changes. The temperatures are now `T_proc` (was `theta_i`), `T_amb` (was `theta_a`), `T_lambda` (was `theta_lambda`) and `T_dew` (was `theta_dew`); the old symbols stay as input aliases. The outputs are now `T_se` (was `theta_se`, an alias of the port `insulation.temperature_surface`), `T_m` (was `theta_m`), `R_tot` (was `R`) and the required thicknesses `t_req_q`, `t_req_se` and `t_req_dew` (were `t_q`, `t_se` and `t_dew`). `theta_se_max` and `dew_margin` keep their names, because the criteria record fills them by symbol. The rule `unattainable` is split: with no thickness chosen it blocks (`unattainable`), and with a thickness chosen it warns and shows the required thickness as above 1000 mm (`unattainable-chosen`). The inputs sit in the groups Setup, Geometry, Material, Conditions, Factors and Requirements. The surface and dew-point criteria are checks of a temperature difference against its limit, and the ports are named `insulation.*`. The references carry their year (ISO 12241:2022, BS EN ISO 13788:2012) and book locators carry the edition.

**Test cases (analytic, Confirmed; every number recomputed in python3, the correlation cases with an independent implementation).** `ka-pipe-chosen` (D_i 100 mm, 30 mm, λ 0.04, h_se 10, 80 → 20 °C: R 2.0690287 K·m/W, q 28.999114 W/m, θ_se 25.769191 °C); `ka-pipe-heat-loss` (25 W/m needs 37.303597 mm, θ_se 24.557514 °C); `ka-pipe-surface` (45 °C needs 5.325914 mm; the 25 W/m limit governs); `ka-flat-closed-form` (`t = λ(Δθ/q − 1/h_se)` = 116 mm, θ_se 22 °C); `ka-flat-condensation` (5 °C surface in 25 °C air at 70 % RH, 1 K margin: θ_dew 19.145352 °C, t = 12.479052 mm); `ka-pipe-condensation` (the same on a 100 mm pipe: 11.290660 mm, gain 18.695278 W/m); `ka-correlation-still` (114 mm pipe at 120 °C, ε 0.1: h_c 3.674839, h_r 0.580066, h_se 4.254904, q 36.772620 W/m, θ_se 27.854972 °C, 60 W/m needs 23.374326 mm); `ka-correlation-wind` (ε 0.9, 3 m/s: h_se 19.917641, q 40.690036 W/m, 29.585912 mm); `ka-flat-correlation` (2 m high wall, ε 0.9: θ_se 23.942458 °C, q 28.846034 W/m²); `ka-wall-and-film` (3 mm wall λ 50, film 500: R_oth 0.0069695, q 28.901759 W/m); from 1.1.0 `ka-dew-point-entered` (dew point 20 °C entered, 1 K margin: 16 mm exactly, θ_se 21 °C), `ka-flat-condensation-ice` (−10 °C air at 90 %: θ_dew −11.178796 °C, 443.436476 mm), `ka-flat-wind-turbulent` (10 m wall in 20 m/s: h_c 40.206477, h_se 45.367359, q 31.445496 W/m², 39.118252 mm for 40 W/m²) and `ka-bare-complies-window` (D_i 10 mm, λ 0.2: bare 18.85 W/m passes 25 W/m, layers from 2.764604 to 74.291720 mm break it, required 74.291720 mm). **No published example is pinned**: Incropera 7th ed. Ch. 3 (insulated cylinder, critical radius examples) and Çengel & Ghajar Ch. 3 (insulated pipe examples) could not be stated with inputs and answer from memory with confidence, so none is entered; they need the owner's copies. Worked examples: `chilled-water-pipe-condensation` (heat-gain limit 12 W/m governs at 17.8 mm over the dew-point margin's 13.2 mm; 25 mm passes), `clad-hot-water-pipe-correlation` (bright cladding, h_se 4.25 W/m²K; 23.4 mm for 60 W/m, 50 mm chosen passes).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Chilled-water pipe in a plant room: heat-gain limit and anti-condensation, 25 mm chosen ([sheet](../../worked-examples/building-services/bs.insulation/chilled-water-pipe-condensation/sheet.txt)).

```text
T_dew = T_amb = 28 °C, RH = 70 % = 22.012 degC
t_req_q = scan 0 … 1000 mm, then bisection = 17.814 mm
t_req_dew = scan 0 … 1000 mm, then bisection = 13.162 mm
thickness = t_req_q = 17.814 mm, t_req_dew = 13.162 mm = 17.814 mm
D_e = 90 mm + 2 × 25 mm = 140 mm
R_ins = ln(140/90)/(2π × 0.035) = 2.0091 m·K/W
R_se = 1/(8 × π × 0.14 m) = 0.28421 m·K/W
R_tot = 0 + 2.0091 + 0.28421 = 2.2933 m·K/W
q_per_m = 22 K / 2.2933 = 9.593 W/m
T_se = 28 °C − 9.593 / 3.5186 = 25.274 degC
T_m = (6 + 25.274)/2 = 15.637 degC
D_crit = 2 × 0.035 / 8 = 8.75 mm
u_q = |q| ≤ 12 W/m = 0.79941
u_dew = θ_se ≥ 23.012 °C = 0.54661
… 2 more step(s) on the sheet
Governing: PASS  Chosen thickness against the heat-gain limit: 9.593 W/m vs 12 W/m  utilisation 0.80  « ISO 12241
```
<!-- hand-arithmetic:end -->
