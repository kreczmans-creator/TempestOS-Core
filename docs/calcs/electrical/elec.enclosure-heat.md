# Enclosure heat dissipation and fan sizing (`elec.enclosure-heat@1.7.1`)

**In plain terms.** Equipment inside a closed enclosure gives off heat, and the enclosure can only lose it through its surfaces, so the air inside warms until the heat out matches the heat in. The worksheet estimates that temperature rise from the effective surface area and a heat-transfer coefficient. If the inside would get too hot, it sizes the airflow a fan must move, corrected for the thinner air at altitude, or the capacity a cooling unit needs.

## Scope

Calculates the heat load of a control enclosure, its natural temperature rise and the airflow for a target rise. It also gives the fan duty point and the cooling-unit capacity needed when the limit cannot be met.

## Method

- Heat dissipated inside the enclosure, plus any solar gain: P = Σ P_i n_i d_i (+ α I A_sun).
- Effective cooling surface, each face weighted by how freely it sheds heat: A_eff = Σ f_face × A_face.
- Temperature rise with natural cooling, one coefficient over the exposed faces (the IEC TR 60890 tables are not used): ΔT = P/(h_conv·A_eff).
- Air flow a fan must move to hold the target rise: V = P/(ρ c_p ΔT_target), with ρ = p(z)/(287.055 T) and c_p = 1006 J/(kg·K).
- Fan duty point, where the fan curve meets the system resistance (found by bisection): p_fan(V) = p_sys (V/V_ref)².
- Cooling unit capacity, when the surfaces alone cannot reject the heat: P_cool = P − h_conv·A_eff·(T_max − T_amb); with T_max at or below T_amb the term is a gain, P_cool = P + h_conv·A_eff·(T_amb − T_max), and no ventilation airflow or fan duty is given.
- Sealed enclosure (sealing temperature T_seal entered, no breather): Δp = p_site·|T_int/T_seal − 1|, the pressure difference across the walls when the air warms or cools from the sealing temperature at constant volume, in absolute temperatures (a rise when T_int > T_seal, a suction when below); a breather or relief element limits it, dp_seal = min(Δp, dp_relief); the force on a cover of area A_cover is F_seal = dp_seal·A_cover

### Notes on the method

A_eff = Σ f_face A_face; ΔT = P/(h A_eff); V = P/(ρ c_p ΔT_allow), ρ = p(z)/(287.055 T_amb); duty by bisection of p_fan(V) − p_sys (V/V_ref)²; P_cool = max(0, P − h A_eff ΔT_allow). **Sealed enclosure (1.3.0).** With the optional `T_seal` (the temperature the enclosure was closed at, no breather) the sheet offers `dp_seal = p_site·|T_int/T_seal − 1|` (kPa, absolute temperatures, constant volume, `p_site` the standard atmosphere at the altitude), the pressure difference across the walls when the air warms (a suction, with the notice `seal-suction`, when `T_int` is below `T_seal`); blank = vented, nothing offered. It is an upper bound (a breather, a leak or a flexing wall reduces it). Port `dp_seal` (→ plate-bending `q`, the pressure on a cover). **Breather (1.4.0).** The optional `dp_relief` (kPa, the cracking pressure of a breather or relief element) caps it: `dp_seal = min(dp_gas, dp_relief)`, `dp_gas` being the gas-law difference above (also offered), with the notice `seal-relief` when the element governs; the element is modelled as a cap with no flow resistance. The optional `A_cover` (m², the cover or door the pressure acts on) gives `F_seal = dp_seal·A_cover` (port, N; → snap-fit `F_req`, the force the latches must hold). A relief pressure or cover area without `T_seal` is ignored with the notice `seal-unused`.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group |
|---|---|---|---|---|---|
| `loads` | Heat loads |  | table | columns: name, P (kW), qty, duty | Setup |
| `W` | Enclosure width | m | 0.6 | 0.1 to 5 | Geometry |
| `H` | Enclosure height | m | 0.8 | 0.1 to 5 | Geometry |
| `D` | Enclosure depth | m | 0.3 | 0.1 to 5 | Geometry |
| `f_top` | Top exposure (1 free, 0 covered) |  | 1 | 0 to 1 | Geometry |
| `f_front` | Front exposure |  | 1 | 0 to 1 | Geometry |
| `f_rear` | Rear exposure (0 = wall-mounted) |  | 0 | 0 to 1 | Geometry |
| `f_left` | Left side exposure |  | 1 | 0 to 1 | Geometry |
| `f_right` | Right side exposure |  | 1 | 0 to 1 | Geometry |
| `f_bottom` | Bottom exposure (0 = on the floor) |  | 0 | 0 to 1 | Geometry |
| `A_sun` | Sunlit area | m² | 0 | 0 to 100 | Geometry |
| `alpha_sun` | Surface absorptivity |  | 0.5 | 0.1 to 1 | Material |
| `h_conv` | Overall heat-transfer coefficient of the enclosure surface (editable reference value) | W/(m²·K) | 5.5 | 3 to 8 | Material |
| `P_add` | Additional heat load (linked from the load-budget dissipated heat or another calculation) | kW | 0 | 0 to 100 | Loads |
| `I_sun` | Solar irradiance on the enclosure (0 = indoors) | W/m² | 0 | 0 to 1200 | Loads |
| `T_amb` | Ambient temperature | °C | 35 | -20 to 55 | Conditions |
| `altitude` | Site altitude (air density) | m | 0 | 0 to 4000 | Conditions |
| `T_seal` | Temperature at which the enclosure was closed with no breather or vent, for the pressure difference across its walls when the air warms or cools (optional; blank = vented, so no pressure difference) (optional) | °C |  | -20 to 55 | Conditions |
| `dp_relief` | Cracking pressure of the breather or relief element a sealed enclosure has (optional; blank = none, so the full gas-law difference acts; needs T_seal) (optional) | kPa |  | 0.1 to 20 | Conditions |
| `A_cover` | Area of the cover or door the pressure difference acts on, for the force on its fixings (optional; blank = not worked out; needs T_seal) (optional) | m² |  | 0.01 to 5 | Conditions |
| `T_max` | Maximum internal temperature | °C | 50 | 20 to 80 | Requirements |
| `p_sys` | System resistance (filters, grilles) at the reference airflow | Pa | 20 | 0 to 500 | Fan |
| `V_ref` | Reference airflow for the system resistance | m³/h | 100 | 1 to 100000 | Fan |
| `V_pts` | Fan curve: airflow points (optional) | m³/h |  |  | Fan |
| `p_pts` | Fan curve: static pressure points (optional) | Pa |  |  | Fan |

## Assumptions

- Heat leaves the enclosure through its exposed faces at one overall coefficient h_conv. Basis: Plain exposed-area method of manufacturer practice, not an IEC TR 60890 verification; h_conv is a reference value the user may edit.
- The coefficient covers natural convection and radiation to surroundings at the ambient temperature. Basis: Plain exposed-area method of manufacturer practice, not an IEC TR 60890 verification; h_conv is a reference value the user may edit.
- Covered faces dissipate nothing. Basis: Plain exposed-area method of manufacturer practice, not an IEC TR 60890 verification; h_conv is a reference value the user may edit.
- Forced ventilation removes the full heat load with air entering at ambient and leaving at the target rise. Basis: Manufacturer fan-sizing practice.
- Natural dissipation is neglected for the airflow, which is conservative. Basis: Manufacturer fan-sizing practice.
- A sealed enclosure (T_seal entered) is a closed, rigid, leak-free volume of dry air at the site pressure when closed. Basis: Ideal gas at constant volume (derived).
- The pressure difference follows the ideal-gas relation at constant volume, from the sealing temperature to T_int. Basis: Ideal gas at constant volume (derived).
- T_int is the internal temperature with natural dissipation, without a cooling unit. Basis: Ideal gas at constant volume (derived).
- A breather, a seal that leaks or a flexing wall reduces the difference, so the result is an upper bound. Basis: Ideal gas at constant volume (derived).
- A pressure-compensation element is modelled only as a cap at its cracking pressure, with no flow resistance. Basis: Ideal gas at constant volume (derived).
- The site pressure is the standard atmosphere at the altitude. Basis: Ideal gas at constant volume (derived).
- The fan curve is linear between the entered points. Basis: Duty-point idealisation.
- The system resistance scales with airflow squared. Basis: Duty-point idealisation.

## Outputs

| Symbol | Description | Unit |
|---|---|---|
| `P_loads` | Internal heat load | kW |
| `P_solar` | Solar gain | kW |
| `P_total` | Total heat | kW |
| `A_eff` | Effective surface | m² |
| `dT_nat` | Natural temperature rise | K |
| `T_int` | Internal temperature (natural) | °C |
| `P_nat_max` | Natural dissipation at the limit | kW |
| `P_cool` | Cooling unit needed | kW |
| `T_max` | Internal temperature limit | °C |
| `rho_air` | Air density | kg/m³ |
| `V_req` | Required airflow | m³/h |
| `V_req_net` | Required airflow (net) | m³/h |
| `V_duty` | Fan duty airflow | m³/h |
| `p_duty` | Fan duty pressure | Pa |
| `u_fan` | Airflow utilisation |  |
| `dp_gas` | Gas-law pressure difference (no breather) | kPa |
| `dp_seal` | Pressure difference of the sealed enclosure | kPa |
| `F_seal` | Force on the cover | N |

## Ports

A port is listed under its public name, `<area>.<quantity>`. The output symbol stays as its first alias, so a saved link, a pack binding or an inputs file that uses the symbol still resolves.

| Public name | Output symbol | Unit | Description |
|---|---|---|---|
| `enclosure.heat_total` | `P_total` | kW | Total heat to remove (loads, linked load and solar gain) |
| `enclosure.heat_cooling_required` | `P_cool` | kW | Cooling-unit duty needed when natural dissipation cannot hold T_max (→ heat-exchanger Q_req) |
| `enclosure.heat_natural_max` | `P_nat_max` | kW | Heat the enclosure dissipates naturally at the limit |
| `enclosure.temperature_limit` | `T_max` | degC | Internal temperature limit the cooling must hold, as entered (→ heat-exchanger T_h_in: the air enters the cooler at the limit) |
| `enclosure.temperature_rise_natural` | `dT_nat` | K | Temperature rise with natural dissipation |
| `enclosure.temperature_internal` | `T_int` | degC | Internal temperature with natural dissipation |
| `enclosure.airflow_required` | `V_req` | m^3/h | Airflow for forced ventilation at the allowed rise |
| `enclosure.airflow_duty` | `V_duty` | m^3/h | Fan duty airflow (when a fan curve is given) |
| `enclosure.pressure_seal` | `dp_seal` | kPa | Pressure difference across the walls of a sealed enclosure (T_seal entered) between the sealing temperature and T_int, as a magnitude, limited to the breather or relief cracking pressure dp_relief when one is entered (→ plate-bending cover pressure q) |
| `enclosure.force_seal` | `F_seal` | N | Force of dp_seal on the cover of area A_cover (T_seal and A_cover entered; → snap-fit F_req, the force the latches must hold) |

## Checks

Checks the sheet makes, governing first on the printed sheet:

- Natural temperature rise against the allowed rise
- Required airflow against the fan duty airflow

Applicability rules:

| Rule | Severity | Effect on the result | Condition |
|---|---|---|---|
| `loads` | Block | Conservative | At least one heat load is needed. |
| `limit` | Warning | Conservative | The maximum internal temperature is at or below the ambient: only a cooling unit holds it, P_cool includes the heat gained through the walls, and no ventilation airflow or fan duty is given. |
| `fan-curve` | Block | Conservative | The fan curve needs at least two points with matching counts, flows increasing. |
| `no-fan-duty` | Warning | Conservative | The fan curve and the system resistance do not cross: no duty point. |
| `seal-relief` | Warning | Conservative | The breather or relief element opens before the gas-law pressure difference is reached: dp_seal is the cracking pressure. |
| `seal-unused` | Warning | Conservative | A relief pressure or a cover area is given without a sealing temperature: they are not used. |
| `seal-suction` | Warning | Conservative | The internal temperature is below the sealing temperature: the pressure difference is a suction, shown as its magnitude. |
| `cooling-unit` | Warning | Conservative | Natural dissipation cannot hold the limit: forced ventilation or a cooling unit is needed. |

## Limitations

The scope states what the sheet does not cover. No standard text and no licensed table is embedded; each tabulated value is entered by the user.

## References

- IEC TR 60890:2022: A method of temperature-rise verification of low-voltage switchgear and controlgear assemblies by calculation
- BS EN 61439-1:2021, cl. 10.10: Verification of temperature rise (edition to be checked)

## Test cases

### Notes on the cases

Wall-mounted 600 × 800 × 300 at 300 W (analytic); fan duty against a linear curve (analytic); `ka-sealed-enclosure` (the first case sealed at 20 °C: T_int 82.84689 °C, Δp 21.72253 kPa); `ka-sealed-relief` (the same with a 10 kPa breather and a 0.3 m² cover: dp_seal 10 kPa, F_seal 3000 N).

- `ka-wall-mounted`: Wall-mounted 600 × 800 × 300 enclosure, 300 W (analytic) (Confirmed; source: Analytic (plain exposed-area method of manufacturer guides))
- `ka-fan-duty`: Fan duty point against a linear fan curve (analytic) (Confirmed; source: Analytic)
- `ka-sealed-enclosure`: The wall-mounted enclosure sealed at 20 °C: the pressure difference when it warms (analytic) (Confirmed; source: Analytic (ideal gas at constant volume: Δp = p0·(T_int/T_seal − 1); recomputed in python3))
- `ka-sealed-relief`: The same sealed enclosure with a 10 kPa breather and a 0.3 m² cover: the relief governs (Confirmed; source: Analytic (dp_seal = min(p0·(T_int/T_seal − 1), dp_relief); F = dp_seal·A_cover; recomputed in python3))

## Changes

**1.7.1.** Declares the unit changes of 1.7.0 (ADR-0177 review): `P_add` and the loads column `P` were in W before 1.7.0, so a bare stored number from an earlier version is read in W, never kW. No calculated value changes.

**Changes in 1.7.0 (C-2 consistency, phase C).** Layout and wording; no result changes. `h` is `h_conv` (the old symbol stays as an alias). The powers are in kW (the values are the same in SI): `P_add`, the loads column `P`, `P_loads`, `P_solar`, `P_total`, `P_nat_max`, `P_cool` and their ports; a stored value keeps its own unit, and a loads cell with no unit is read in kW. The checks compare physical values: `dT_nat` with `dT_allow`, and `V_req` with `V_duty`. The inputs are grouped Setup, Geometry, Material, Loads, Conditions, Requirements and Fan. The ports are named `enclosure.*`, and the port descriptions name templates, not codes. The six exposure fractions keep a usual range equal to their hard range 0 to 1, because the whole range is usual. The manufacturer guides are no longer a reference.

**Changes in 1.6.0 (elec.enclosure-heat-R2, chain.cooling-04, from the C-1 verification).** A `T_max` at or below the ambient is the usual case for a cooling unit and no longer blocks the sheet (rule `limit`, now a warning). The walls then gain heat from the hotter ambient, so the unit removes `P_cool = P_total + h·A_eff·(T_amb − T_max)` (the same relation as before, with the allowed rise negative: `P_nat_max` is negative, the heat gained at the limit). 300 W, `A_eff` 1.14 m², `h` 5.5 W/(m²·K), ambient 45 °C against a limit of 40 °C: `P_cool` 331.35 W; ambient equal to the limit: 300 W. The airflow `V_req`, `V_req_net`, `rho_air` and the fan duty are not given in that case (a ventilation airflow for a limit the ambient already exceeds has no meaning), and the natural-dissipation check fails. In the cooling chain E2 now passes `P_cool` and `T_max` to the coil (46 °C ambient against 45 °C: 3683.76 W).

**Changes in 1.5.1 (elec.enclosure-heat-01).** The effective-surface step is labelled "plain exposed-area method (manufacturer practice), not an IEC TR 60890 verification", and every run carries the assumption that the method uses plain face areas with one coefficient `h`, neither the surface factors nor the standard's temperature-rise correlation, so the result is not a verification of temperature rise to IEC TR 60890 or BS EN 61439-1 cl. 10.10. The numbers are unchanged; the standards stay as references to consult, not as the method used. Optionally offering the surface factors is left to a later version.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Wall-mounted PLC panel, natural dissipation and fan ([sheet](../../worked-examples/electrical/elec.enclosure-heat/wall-mounted-plc-panel/sheet.txt)).

```text
P_loads = 2 item(s) = 0.3 kW
P_solar = 0.5 × 0 W/m² × 0 m² = 0 kW
P_total = 0.3 kW + 0 kW + 0 kW = 0.3 kW
A_eff = 1 × 0.6 m × 0.3 m + 0 × 0.6 m × 0.3 m + 1 × 0.6 m × 0.8 m + 0 × 0.6 m × 0.8 m + 1 × 0.8 m × 0.3 m + 1 × 0.8 m × 0.3 m = 1.14 m²
dT_nat = 0.3 kW / (5.5 W/(m²·K) × 1.14 m²) = 47.847 K
T_int = 35 degC + 47.847 K = 82.847 degC
dT_allow = 50 degC − 35 degC = 15 K
u_nat = 47.847 K / 15 K = 3.1898
P_nat_max = 5.5 W/(m²·K) × 1.14 m² × 15 K = 0.09405 kW
P_cool = max(0 W, 0.3 kW − 0.09405 kW) = 0.20595 kW
p_site = z = 0 m = 101.32 kPa
rho_air = 101.32 kPa / (287.055 J/(kg*K) × 35 degC) = 1.1455 kg/m³
V_req = 0.3 kW / (1.1455 kg/m³ × 1006 J/(kg*K) × 15 K) = 62.481 m³/h
V_req_net = max(0 m^3/h, (0.3 kW − 0.09405 kW) / (1.1455 kg/m³ × 1006 J/(kg*K) × 15 K)) = 42.893 m³/h
… 3 more step(s) on the sheet
Governing: FAIL  Natural temperature rise against the allowed rise: 47.85 K vs 15 K  utilisation 3.19
```
<!-- hand-arithmetic:end -->
