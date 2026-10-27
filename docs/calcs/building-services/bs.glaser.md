# Glaser condensation (interstitial and surface) (`bs.glaser@1.4.0`)

**In plain terms.** Warm moist air passes through a wall and may reach a cold layer where it condenses and wets the insulation. The worksheet takes the layers of the wall and the indoor and outdoor conditions month by month, finds the temperature and vapour pressure at each layer boundary, and shows where condensation forms, how much builds up in winter and whether it dries out in summer. It also checks the inside surface for mould risk.

## Scope

Calculates interstitial condensation in a layered wall by the Glaser method over twelve monthly climate steps. It gives the condensate on each interface, the annual balance, the peak against the limit and the surface temperature factor.

## Method

- Layer resistance and diffusion thickness: R_j = d_j/λ_j; s_d,j = μ_j·d_j
- Total resistance, U-value and interface temperature: R_T = R_si + Σ R_j + R_se; U = 1/R_T; T_k = T_int − (T_int − T_e)·(R_si + Σ_(j≤k) R_j)/R_T
- Saturation pressure (Magnus): p_sat = 610.5·exp(17.269·T/(237.3 + T)) over water (T ≥ 0 °C); p_sat = 610.5·exp(21.875·T/(265.5 + T)) over ice (T < 0 °C)
- Vapour pressures: p_e = RH_e·p_sat(T_e); p_i = RH_i·p_sat(T_int), or p_e + f_Δp·Δv(T_e)·R_v·(T_i + T_e)/2 in the moisture-excess mode with R_v = 461.5 J/(kg·K)
- Vapour flux and condensate: g = δ0·Δp/Δs_d with δ0 = 2×10⁻¹⁰ kg/(m·s·Pa); g_c = g_in − g_out on the tangent-construction profile; Mc accumulates g_c·t over 12 months, a wet interface drying at g_c < 0
- Refinement check: the same year with every layer split into 20 equal sub-layers of the same λ and μ; the annual balance passes only when it passes both ways
- Annual balance and peak: PASS when no condensate remains after 12 months; Mc_max = the most held on any interface, checked against Mc_limit
- Surface factor: f_Rsi = 1 − R_si,f/R_T,f; f_Rsi,min = the largest monthly (T_si,min − T_e)/(T_int − T_e) with p_sat(T_si,min) = p_i/φ_crit; PASS when f_Rsi ≥ f_Rsi,min

### Notes on the method

`R_j = d_j/λ_j`, `s_d,j = μ_j·d_j`, `R_T = R_si + ΣR_j + R_se`; node temperature `θ_k = θ_i − (θ_i − θ_e)(R_si + Σ_(j≤k)R_j)/R_T` (the same series-resistance arithmetic as B4, and the same `layers` table shape plus a `mu` column). Saturation pressure by the Magnus forms `610.5·exp(17.269θ/(237.3 + θ))` over water (θ ≥ 0 °C) and `610.5·exp(21.875θ/(265.5 + θ))` over ice; `p_sat(20 °C) = 2336.95 Pa`. Outside pressure `p_e = RH_e·p_sat(θ_e)`; inside pressure `p_i = RH_i·p_sat(θ_i)`, or `p_e + f_Δp·Δv(θ_e)·R_v·(T_i + T_e)/2` with Δv linear between two user points and constant beyond them, the mean of the inside and the month's outside temperature (in K) and a safety factor `f_Δp` (`dp_f`, a record, default 1.10, applied to the pressure difference in both the interstitial and the surface check; R_v = 461.5 J/(kg·K), derived R/M_w; limited to saturation with a warning). The sheet states that the clause wording (mean temperature and margin) must be checked against the owner's copy of ISO 13788. Vapour flux through a layer `g = δ0·Δp/Δs_d`, `δ0 = 2×10⁻¹⁰ kg/(m·s·Pa)`.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Hard range | Group |
|---|---|---|---|---|---|---|
| `layers` | Wall layers from the inside outwards (thickness d, conductivity λ and vapour resistance factor μ) |  | table |  |  | Setup |
| `layers.element` | Element (blank = every row belongs to the wall; matches the elements of the room heat loss layers table) (column) |  | optional | | | Setup |
| `layers.name` | Layer (inside first) (column) |  | optional | | | Setup |
| `layers.d` | Thickness (column) | mm |  | | | Setup |
| `layers.lambda` | Thermal conductivity λ (column) | W/(m·K) |  | | | Setup |
| `layers.mu` | Vapour resistance factor μ (column) |  |  | | | Setup |
| `layers.timber` | Timber or breathable layer (hygroscopic, capillary transport: raises the EN 15026 warning) (column) |  | optional | | | Setup |
| `climate` | Twelve monthly outside conditions (the user's climate record, in the order the year is run) |  | table |  |  | Setup |
| `climate.month` | Month (column) |  | optional | | | Setup |
| `climate.theta_e` | Mean outside temperature θe (column) | °C |  | | | Setup |
| `climate.RH_e` | Mean outside relative humidity RHe (column) | % |  | | | Setup |
| `climate.days` | Days in the month (blank = calendar days, rows in January to December order) (column) | d | optional | | | Setup |
| `humidity_mode` | Internal humidity from |  | constant internal relative humidity | constant internal relative humidity, moisture excess from a humidity class (linear in the outside temperature) |  | Setup |
| `breathable` | The build-up is timber or breathable (hygroscopic layers, capillary transport) |  |  |  |  | Setup |
| `element` | Element whose rows of the layers table to use (optional; blank = all rows) |  | optional |  |  | Setup |
| `T_int` (was `theta_int`) | Internal air temperature | °C | 20 | 10 to 30 | 0 to 40 | Conditions |
| `RH_int` | Internal relative humidity (constant-humidity mode) | % | 50 | 20 to 90 | 1 to 100 | Conditions |
| `dv_a` | Moisture excess Δv at the cold point (humidity-class record) | g/m³ | optional | 0 to 20 | 0 to 50 | Conditions |
| `theta_a` | Outside temperature of the cold point (Δv constant below it) | °C | optional | -20 to 30 | -60 to 60 | Conditions |
| `dv_b` | Moisture excess Δv at the warm point (humidity-class record) | g/m³ | optional | 0 to 20 | 0 to 50 | Conditions |
| `theta_b` | Outside temperature of the warm point (Δv constant above it) | °C | optional | -20 to 40 | -60 to 60 | Conditions |
| `Rsi` | Internal surface resistance R_si | m²·K/W | 0.13 | 0.04 to 0.25 | 0.01 to 1 | Factors |
| `Rse` | External surface resistance R_se | m²·K/W | 0.04 | 0.02 to 0.25 | 0.01 to 1 | Factors |
| `Rsi_f` | Internal surface resistance R_si,f for the f_Rsi check (0.25 m²K/W is the conventional value for an opaque element and 0.13 for glazing; a record to check against the owner's copy of ISO 13788) | m²·K/W | 0.25 | 0.04 to 0.5 | 0.01 to 1 | Factors |
| `dp_f` | Safety factor on the vapour-pressure difference Δp = Δv·R_v·(T_i + T_e)/2 in the moisture-excess mode (1.10 by convention; a record, wording to be checked against the owner's copy of ISO 13788) |  | 1.1 | 1 to 1.5 | 1 to 2 | Factors |
| `RH_crit` | Critical surface relative humidity | % | 80 | 50 to 100 | 30 to 100 | Requirements |
| `Mc_limit` | Limit on the condensate on any interface (optional; blank = report only) | g/m² | optional | 1 to 5000 | 0.001 to 1000000 | Requirements |
| `U_ref` | U-value of this wall from the room heat loss calculation, for the consistency check against 1/R_T (optional; blank = not checked) | W/(m²·K) | optional | 0.05 to 8 | 0.01 to 10 | Requirements |
| `diagram_month` | Month to draw the Glaser diagram for (optional; blank = the month of the peak, or the coldest if nothing condenses) |  | optional | 1 to 12 | 0.5 to 12.5 | Requirements |

## Assumptions

1. Vapour diffuses one-dimensionally through homogeneous layers in series, in steady state each month. Basis: The Glaser method.
2. Each month uses its mean climate, with no air movement, liquid transport, moisture storage, or solar or rain gain. Basis: The Glaser method.
3. Material properties λ and μ are constants of each layer. Basis: The Glaser method.
4. Condensate does not change the properties of a layer. Basis: The Glaser method.
5. Condensation is checked at the interfaces between layers. Basis: The Glaser method.
6. The saturation line is straight between interface values, which can miss condensate inside a layer. Basis: Stated limit; whether ISO 13788 requires sub-division is to be checked against the owner's copy.
7. The annual balance is also run with every layer split into 20 sub-layers and passes only when both runs pass. Basis: Refinement check.
8. The limit on the condensate per interface is checked on the interfaces as entered. Basis: Stated limit.
9. The Magnus saturation pressure is used over water at and above 0 °C and over ice below. Basis: Derived.
10. The water-vapour permeability of air is δ0 = 2×10⁻¹⁰ kg/(m·s·Pa). Basis: Derived; closed-form physics, not data of a standard.
11. A blank month length is the calendar days of the month, with the climate rows taken as January to December. Basis: Calendar months, February 28 days; enter the days where the record starts in another month.
12. A wet interface that dries within a month is dry from the next month. Basis: Monthly steps.
13. The rates of a month are those of the wet state at its start. Basis: Monthly steps.
14. The climate, humidity-class points, surface resistances, critical humidity and condensate limit are the user's own records. Basis: No table from a standard is shipped.

## Outputs

| Symbol | Description |
|---|---|
| `U_wall` | U-value of the wall |
| `R_T` | Total thermal resistance |
| `S_d` | Total diffusion-equivalent air layer thickness |
| `psat_i` | Saturation vapour pressure at the internal temperature |
| `p_ext_m` | Outside vapour pressure by month |
| `p_int_m` | Inside vapour pressure by month |
| `n_start` | First month of condensation |
| `Mc_1` | M1: condensate on the wall |
| `Mc_2` | M2: condensate on the wall |
| `Mc_3` | M3: condensate on the wall |
| `Mc_4` | M4: condensate on the wall |
| `Mc_5` | M5: condensate on the wall |
| `Mc_6` | M6: condensate on the wall |
| `Mc_7` | M7: condensate on the wall |
| `Mc_8` | M8: condensate on the wall |
| `Mc_9` | M9: condensate on the wall |
| `Mc_10` | M10: condensate on the wall |
| `Mc_11` | M11: condensate on the wall |
| `Mc_12` | M12: condensate on the wall |
| `Mc_if_1` | Peak condensate on interface 1 |
| `Mc_max` | Most condensate on any interface |
| `Mc_tot` | Most condensate on the wall |
| `Mc_end` | Condensate left after 12 months |
| `Mc_end_fine` | Condensate left after 12 months, each layer in 20 sub-layers |
| `Mc_tot_fine` | Most condensate on the wall, each layer in 20 sub-layers |
| `n_diag` | Month of the diagram |
| `sd_if` | Diffusion thickness at the nodes |
| `x_if` | Depth at the nodes |
| `T_if` | Temperature at the nodes |
| `psat_if` | Saturation pressure at the nodes |
| `p_if` | Vapour pressure at the nodes |
| `fRsi` | Temperature factor of the internal surface |
| `fRsi_min` | Minimum temperature factor |
| `n_fcrit` | Month governing the surface check |
| `R_Tf` | Total resistance for the surface check |
| `u_Mc` | Peak condensate against its limit |
| `u_f` | Surface factor against its minimum |
| `pass` | All checks pass |
| `Mc_if_2` | Peak condensate on interface 2 |
| `Mc_if_3` | Peak condensate on interface 3 |
| `Mc_if_4` | Peak condensate on interface 4 |
| `dU_ref` | Difference from the heat-loss calculation's U-value |

Per layer `R_j`, `sd_j`; `R_T`, `U_wall`, `dU_ref` (with `U_ref`), `S_d`, `psat_i`; vectors `p_ext_m`, `p_int_m` (Pa by month); `n_start` (row the accumulation starts in; 0 = no condensation); `Mc_1…Mc_12` (condensate on the wall at the end of each month, g/m², in the climate's row order, with the month's change in the working); `Mc_if_k` (peak on each interface); `Mc_max`, `Mc_tot` (peak of the whole wall), `Mc_end`, `Mc_end_fine`, `Mc_tot_fine` (the same two with every layer in 20 sub-layers); for the diagram month `n_diag` and the vectors `sd_if`, `x_if`, `T_if`, `psat_if`, `p_if` with series "Saturation pressure", "Vapour pressure" (against s_d) and "Temperature" (against depth); `R_Tf`, `fRsi`, `fRsi_min`, `n_fcrit`, `u_f`, `u_Mc`, `pass`. **Ports.** `Mc_max` (g/m²), `fRsi`, `pass` (1 when every check passes, else 0). **Checks.** Annual moisture balance; peak condensate against `Mc_limit` (when given); f_Rsi against f_Rsi,min. **Warnings.** `layer-subdivision` (the 20-sub-layer run differs from the as-entered run); `en15026` when the build-up is flagged timber or breathable, or when any layer is flagged `timber` (the warning names the layers; use an EN 15026 hygrothermal simulation); `rsi-f-low` when `Rsi_f` is below 0.25; a single layer has no interface; the humidity class exceeds saturation; no month cold enough for a surface requirement; rows of another element ignored.

## Ports

A port is listed under its public name, `<area>.<quantity>`. The output symbol stays as the port's key, so a saved link, a pack binding or an inputs file that uses the symbol or an alias still resolves.

| Public name | Output symbol | Unit | Description |
|---|---|---|---|
| `condensation.mass_interface_max` | `Mc_max` | g/m² | Most condensate held on any interface in the year |
| `condensation.factor_temperature_surface` | `fRsi` |  | Temperature factor of the internal surface |
| `condensation.checks_passed` | `pass` |  | 1 when every check passes (annual balance, peak against the limit, surface factor), else 0 |

## Checks

Checks the sheet makes:

- Condensate left after 12 months against zero (BS EN ISO 13788 cl. 6)
- Peak condensate on an interface against its limit (BS EN ISO 13788 cl. 6)
- Minimum surface temperature factor against the factor of the wall at 80 % critical humidity (BS EN ISO 13788 cl. 5)

Applicability rules:

| Rule | Severity | Effect on the result | Condition |
|---|---|---|---|
| `layers` | Block | Conservative | Enter the layers (inside first), each with thickness, conductivity and μ; at most 30. |
| `element` | Block | Conservative | The element named must have rows in the layers table. |
| `climate` | Block | Conservative | The climate record has exactly twelve monthly rows, each with a temperature and a humidity. |
| `humidity-class` | Block | Conservative | The moisture-excess mode needs both humidity-class points, the cold point below the warm one. |
| `element-filter` | Warning | Conservative | Rows of the layers table that belong to another element are ignored. |
| `layer-subdivision` | Warning | Unconservative | The condensate left after 12 months, or the wall peak, changes (a verdict flips, or by more than 10 %) when every layer is split into 20 sub-layers: condensate forms inside a layer. The annual balance must pass both ways. |
| `single-layer` | Warning | Conservative | A single layer has no interface: no interstitial condensation can be found. |
| `p-int-saturated` | Warning | Conservative | The internal vapour pressure from the humidity class exceeds saturation at the internal temperature in some month and is limited to it. |
| `fRsi-not-applicable` | Warning | Conservative | No month has the outside air at least 1 K colder than the inside: f_Rsi,min is taken as 0. |
| `rsi-f-low` | Warning | Unconservative | The internal surface resistance for the surface check is below 0.25 m²K/W, the conventional value for an opaque element (0.13 is for glazing). |
| `u-ref-mismatch` | Warning | Conservative | The U-value 1/R_T of the layers differs by more than 2 % from the U-value given by the heat-loss calculation: the two describe different constructions or surface resistances. |
| `en15026` | Warning | Unconservative | A timber or breathable build-up (the flag, or any layer flagged timber or breathable) is outside the Glaser method: use EN 15026 hygrothermal simulation. |

## Limitations

Steady state in each month with monthly mean climate: no air movement, liquid transport, moisture storage, rain or solar gain; λ and μ constant (no moisture dependence); condensation is found at the interfaces between layers, and the saturation line is joined straight between interface values; because p_sat is convex inside a layer, the chord can cross it between two interfaces, so the interface-only result can understate the risk and its verdict can change with how the wall was cut (bs.glaser-01: 25 mm λ 0.035 μ 8 + 50 mm λ 0.5 μ 10 + 215 mm λ 0.5 μ 40 at 40 % passes as entered and fails, 561.7 g/m² left, with every layer in 20 slices; the converged value is about 580). Since 1.3.0 the year is also run with every layer split internally into 20 equal sub-layers (not counted against the 30-layer limit), the annual balance passes only when both runs pass, `Mc_end_fine` and `Mc_tot_fine` are reported, and the warning `layer-subdivision` is raised when a verdict flips or Mc_end or the wall peak differs by more than 10 %. The limit on the condensate per interface is checked on the interfaces as entered. Whether ISO 13788 requires sub-division is to be checked against the owner's copy. A single constant internal temperature; the humidity class is two user points (linear, constant beyond), not a table. The safety margin on the internal humidity is the Δp factor of the moisture-excess mode only; in the constant-humidity mode none is applied (add it to `RH_int`). Not for timber, breathable or hygroscopic build-ups (EN 15026); not an assessment of the risk of mould growth or of the surface moisture content.

## References

- BS EN ISO 13788:2012, cl. 5 (clause number to be checked against the owner's copy): Hygrothermal performance of building components and elements — internal surface temperature and surface condensation.
- BS EN ISO 13788:2012, cl. 6 (clause number to be checked against the owner's copy): Hygrothermal performance of building components and elements — interstitial condensation (Glaser method).
- BS EN ISO 13788:2012: Internal humidity from the humidity class, moisture excess and its allowance (clause to be checked).
- BS EN 15026:2007: Assessment of moisture transfer by numerical simulation (the whole standard, for timber and breathable builds).

## Test cases

- `ka-seasonal-drying`: The same wall in an invented maritime year: condensation from November, dry again in June (Confirmed; source: Analytic (the single-interface closed form, month by month, calendar days))
- `ka-psat-20`: Magnus saturation pressure at 20 °C, and a wall with one interface that never condenses (Confirmed; source: Analytic (Magnus form, the brief's coefficients))
- `ka-single-interface-all-year`: One interface condensing every month of a constant 0 °C / 90 % climate (Confirmed; source: Analytic (series resistance, Magnus, g = δ0·Δp/Δs_d))
- `ka-two-planes-tangent`: Five layers with two condensation planes: the tangent construction touches the saturation line twice (Confirmed; source: Analytic (tangent construction, checked against an independent obstacle-problem solve in python3))
- `ka-surface-factor`: f_Rsi against f_Rsi,min: R_si 0.25 for the surface check, critical humidity 80 % (Confirmed; source: Analytic (f_Rsi = 1 − R_si/R_T; inverse Magnus for θ_si,min))
- `ka-u-ref-cross-check`: Wall U-value 1/R_T and the cross-check against the heat-loss calculation's U-value (Confirmed; source: Analytic (U = 1/R_T; |U − U_ref|/U_ref))
- `ka-humidity-class`: Moisture excess from two user points: Δv = 5 g/m³ at or below 0 °C, falling linearly to 0 at 20 °C, with the 1.10 margin on Δp (Confirmed; source: Analytic (p_i = p_e + f_Δp·Δv·R_v·(T_i + T_e)/2, f_Δp = 1.10, R_v = 461.5 J/(kg·K); single-interface closed form; clause wording to be checked against the owner's copy of ISO 13788))
- `ka-humidity-class-no-margin`: The same moisture excess with no margin on Δp (f_Δp = 1): the mean-temperature formula alone (Confirmed; source: Analytic (p_i = p_e + Δv·R_v·(T_i + T_e)/2; single-interface closed form))

## Changes

**Changes in 1.4.0 (C-2 consistency, phase C).** Layout and wording; no calculated value changes. The internal air temperature `theta_int` is now `T_int` (alias kept) and the output `theta_if` is now `T_if`. The temperatures of the climate record (`theta_a`, `theta_b`, and the climate column `theta_e`) keep their symbols, because record import and a saved climate table match the exact key. The inputs sit in the groups Setup, Conditions, Factors and Requirements; `diagram_month` has a usual range of 1 to 12 and a hard range of 0.5 to 12.5. The three checks read "Condensate left after 12 months against zero", "Peak condensate on an interface against its limit" and "Minimum surface temperature factor against the factor of the wall at the critical humidity", with their values. The method has eight lines, the 14 assumptions are one short sentence each, the record clauses read "BS EN ISO 13788 cl. n", and the ports are `condensation.mass_interface_max`, `condensation.factor_temperature_surface` and `condensation.checks_passed`.

**Test cases (analytic, Confirmed; recomputed in python3, the profile also against an independent obstacle-problem solve).** `ka-seasonal-drying` (100 mm insulation λ 0.04 μ 2 on 200 mm brick λ 0.8 μ 10, R_T 2.92 m²K/W, S_d 2.2 m, in an invented maritime year with calendar-day months: accumulation from November, peak 3083.081 g/m² in March, dry by June, Mc_end 0, balance PASS, peak against a 3000 g/m² limit u = 1.0277); `ka-psat-20` (p_sat(20 °C) = 2336.951 Pa, i.e. the brief's 2337 Pa to its rounding, and a plaster and brick wall at 40 % internal humidity that never condenses and passes the surface check); `ka-single-interface-all-year` (constant 0 °C / 90 %: 38.7385 g/m² a day, 1200.895 g/m² in January, 14139.573 g/m² after the 365 days, balance FAIL); `ka-two-planes-tangent` (five layers, contacts at interfaces 2 and 4: 5143.147 and 772.008 g/m² after twelve months); `ka-surface-factor` (R_si,f 0.25: f_Rsi 0.575372 < f_Rsi,min 0.590256, FAIL); `ka-u-ref-cross-check` (the plaster and brick wall: U_wall = 1/0.46875 = 2.133333 W/m²K, 3.0303 % from a U_ref of 2.2: warns); `ka-humidity-class` (Δv 5 g/m³ at ≤ 0 °C to 0 at 20 °C with the 1.10 margin on Δp: start in October, peak 5111.052 g/m², 91.744 g/m² left, balance FAIL, f_Rsi,min 0.670474) and `ka-humidity-class-no-margin` (f_Δp = 1: peak 4249.892 g/m², PASS balance, f_Rsi,min 0.630284). The climate is invented and every λ, μ and thickness is an illustrative input, not design data. **A published Glaser worked example (an ISO 13788 annex example, a textbook example, or a Condensa/WUFI run) is not pinned**: no example's inputs and answer could be stated from memory with confidence, so none is entered; it needs the owner's copy (see "not pinned" in the build report). Worked examples: `internal-insulation-no-vcl` (peak 3022.6 g/m² against a 500 g/m² limit, FAIL), `internal-insulation-with-vcl` (no condensation, PASS).

**Citations to check.** ISO 13788 clause numbers (cl. 5 surface temperature factor, cl. 6 interstitial condensation) are cited from memory for the structure of the standard and are to be checked against the owner's copy; no text of the standard is reproduced. The δ0, Magnus coefficients and R_v are closed-form physics; the calendar-day month with rows taken as January to December, the 1 K threshold for the surface requirement and the "first month of condensation after a dry month" start rule are this product's reading and are stated on the sheet.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Solid brick wall with internal mineral wool and no vapour control layer: interstitial condensation over a year ([sheet](../../worked-examples/building-services/bs.glaser/internal-insulation-no-vcl/sheet.txt)).

```text
R_1 = Plasterboard: 12.5 mm / 0.25 W/(m·K) = 0.05 m²·K/W
sd_1 = Plasterboard: 8 × 12.5 mm = 0.1 m
R_2 = Mineral wool: 50 mm / 0.035 W/(m·K) = 1.4286 m²·K/W
sd_2 = Mineral wool: 1 × 50 mm = 0.05 m
R_3 = Existing brick: 215 mm / 0.8 W/(m·K) = 0.26875 m²·K/W
sd_3 = Existing brick: 10 × 215 mm = 2.15 m
R_T = 0.13 + 1.7473 + 0.04 = 1.9173 m²·K/W
U_wall = 1 / 1.9173 = 0.52156 W/(m²·K)
S_d = 2.3 m
psat_i = θ = 20 °C = 2337 Pa
p_ext_m = months 1 … 12 of the climate = [634.76, 666.5, 785.09, 917.96, 1137.7, 1344.8, 1526.5, 1567.8, 1397.6, 1141.4, 841.16, 689.22] Pa
p_int_m = 50 % × 2337 Pa = [1168.5, 1168.5, 1168.5, 1168.5, 1168.5, 1168.5, 1168.5, 1168.5, 1168.5, 1168.5, 1168.5, 1168.5] Pa
n_start = M11 (row 11) = 11
Mc_1 = M1: θ_e = 2 °C; 1023.7 g/m² gained (−: dried) this month = 2069.8 g/m²
… 31 more step(s) on the sheet
Governing: FAIL  Peak condensate on an interface against its limit: 3022.7 g/m² vs 500 g/m²  utilisation 6.05  « BS EN ISO 13788 cl. 6
```
<!-- hand-arithmetic:end -->
