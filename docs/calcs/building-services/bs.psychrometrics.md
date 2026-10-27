# Psychrometrics (`bs.psychrometrics@1.4.0`)

**In plain terms.** Moist air is fixed by any two of its properties, such as temperature and relative humidity; from them follow how much water it holds, its enthalpy, its dew point and its wet-bulb temperature. Heating, cooling with dehumidification, mixing two airstreams and adding steam or spray are each a balance of dry air, water and energy, so the state after each process, and the heat or water it takes, comes straight from those balances.

## Scope

Calculates the state of moist air from dry-bulb and one other property at a stated pressure or altitude. It then applies one air-handling process with its heat and moisture balance: sensible heating or cooling, dehumidification, humidification or mixing.

## Method

- Saturation and humidity: p_ws(T) by Hyland–Wexler over water at or above 0 °C and over ice below; W = 0.621945 p_w/(p − p_w); φ = p_w/p_ws
- Enthalpy and volume per kilogram of dry air: h = 1.006 T + W(2501 + 1.86 T) kJ/kg; v = 0.287042 (T + 273.15)(1 + 1.607858 W)/p
- Dew point and wet-bulb temperatures, by bisection: p_ws(T_dp) = p_w; the wet-bulb from the adiabatic-saturation relation
- Sensible heating or cooling at constant moisture content: Phi = ṁ (h₂ − h₁)
- Cooling with dehumidification, less the heat leaving in the condensate: Q_total = ṁ(h₁ − h₂) − ṁ_w h_w, with ṁ_w = ṁ(W₁ − W₂)
- Sensible and latent split of the coil load, along T₁ → T₂ at W₁ then W₁ → W₂ at T₂: Q_sensible = ṁ(h₁ − h(T₂, W₁)); Q_latent = ṁ(h(T₂, W₁) − h₂); SHR = Q_sensible/(Q_sensible + Q_latent); with RH_2 blank and T₂ above the entering dew point the coil is dry, W₂ = W₁
- Steam and adiabatic humidification: h₂ = h₁ + (W₂ − W₁) h_steam for steam; h₂ = h₁ for adiabatic (spray) humidification
- Mixing two airstreams, weighted by dry-air mass flow: W_mix = Σṁ W/Σṁ; h_mix = Σṁ h/Σṁ

### Notes on the method

Hyland–Wexler (1983) saturation pressure over water (≥ 0 °C) and ice; ASHRAE Fundamentals Ch. 1 ideal-gas relations in own notation (`MoistAir`): W = 0.621945 p_w/(p − p_w), h = 1.006 t + W(2501 + 1.86 t), v = 0.287042 T(1 + 1.607858 W)/p; dew point and wet-bulb by capped bisection (wet-bulb from the adiabatic-saturation relation, ice form below 0 °C). Enhancement factor taken as 1 (assumption A1; ≈ 0.4 % at 20 °C).

## Inputs

| Symbol | Description | Unit | Default | Usual range | Hard range | Group |
|---|---|---|---|---|---|---|
| `known` | Second known property |  | relative humidity | relative humidity, wet-bulb temperature, humidity ratio, dew point, enthalpy |  | Setup |
| `process` | Process |  | none (state only) | none (state only), sensible heating / cooling, cooling with dehumidification, steam humidification, adiabatic (evaporative) humidification, adiabatic mixing |  | Setup |
| `altitude` | Altitude (used when no pressure is entered) | m | 0 | -500 to 5000 | -1000 to 10000 | Conditions |
| `T_db` (was `t_db`) | Dry-bulb temperature | °C | 25 | -40 to 60 | -60 to 100 | Conditions |
| `p_atm` (was `p`) | Barometric pressure (optional; blank = standard atmosphere at the altitude) | kPa | optional | 50 to 110 | 10 to 1000 | Conditions |
| `RH` | Relative humidity |  | 50 | 5 to 100 | 0 to 100 | Conditions |
| `T_wb` (was `t_wb`) | Wet-bulb temperature | °C | optional | -40 to 60 | -60 to 100 | Conditions |
| `W` | Humidity ratio (g per kg dry air) |  | optional | 0 to 60 | 0 to 1000 | Conditions |
| `T_dp` (was `t_dp`) | Dew-point temperature | °C | optional | -40 to 60 | -60 to 100 | Conditions |
| `h_air` (was `h`) | Specific enthalpy (kJ per kg dry air) |  | optional | -50 to 400 | -200 to 2000 | Conditions |
| `m_da` | Dry-air mass flow | kg/s | 1 | 0 to 1000 | 0 to 100000 | Process |
| `h_steam` | Steam enthalpy (steam humidification) |  | 2676 | 2500 to 3000 | 0 to 5000 | Process |
| `T_2` (was `t_2`) | Leaving dry-bulb (sensible, cooling) | °C | optional | -40 to 80 | -60 to 100 | Process |
| `RH_2` | Leaving relative humidity (cooling, evaporative) |  | optional | 5 to 100 | 0 to 100 | Process |
| `W_2` | Leaving humidity ratio, g/kg (steam) |  | optional | 0 to 60 | 0 to 1000 | Process |
| `eff` | Saturation efficiency for evaporative humidification (optional; blank = use the leaving relative humidity) |  | optional | 0.1 to 1 | 0 to 1 | Process |
| `T_db_b` (was `t_db_b`) | Second stream dry-bulb (mixing) | °C | optional | -40 to 60 | -60 to 100 | Mixing |
| `RH_b` | Second stream relative humidity (mixing) |  | optional | 5 to 100 | 0 to 100 | Mixing |
| `m_da_b` | Second stream dry-air mass flow (mixing) | kg/s | optional | 0 to 1000 | 0 to 100000 | Mixing |

## Assumptions

1. Moist air is an ideal mixture of ideal gases. Basis: ASHRAE Fundamentals Ch. 1 ideal-gas relations.
2. The enhancement factor is taken as 1. Basis: ASHRAE Fundamentals Ch. 1 ideal-gas relations.
3. Condensate leaves at the coil leaving dry-bulb. Basis: Process conventions.
4. Steam adds the enthalpy h_steam at negligible mass of dry air. Basis: Process conventions.
5. Adiabatic humidification follows a line of constant enthalpy, which is close to constant wet-bulb. Basis: Evaporative process idealisation.

## Outputs

| Symbol | Description |
|---|---|
| `T_1_out` | Dry-bulb temperature, state 1 |
| `p_ws_1` | Saturation pressure, state 1 |
| `p_w_1` | Vapour pressure, state 1 |
| `W_1` | Humidity ratio, state 1, in grams per kilogram of dry air |
| `RH_1` | Relative humidity, state 1, as a number from 0 to 100 |
| `phi_1` | Relative humidity, state 1, as a quantity in % (for linking) |
| `T_dp_1` | Dew point, state 1 |
| `T_wb_1` | Wet-bulb temperature, state 1 |
| `h_1` | Enthalpy, state 1, in kilojoules per kilogram of dry air |
| `v_1` | Specific volume, state 1 |
| `rho_1` | Moist-air density, state 1 |
| `T_2_out` | Dry-bulb temperature, state 2 |
| `p_ws_2` | Saturation pressure, state 2 |
| `p_w_2` | Vapour pressure, state 2 |
| `W_2` | Humidity ratio, state 2, in grams per kilogram of dry air |
| `RH_2` | Relative humidity, state 2, as a number from 0 to 100 |
| `phi_2` | Relative humidity, state 2, as a quantity in % (for linking) |
| `T_dp_2` | Dew point, state 2 |
| `T_wb_2` | Wet-bulb temperature, state 2 |
| `h_2` | Enthalpy, state 2, in kilojoules per kilogram of dry air |
| `v_2` | Specific volume, state 2 |
| `rho_2` | Moist-air density, state 2 |
| `Phi` | Heat |
| `T_b_out` | Dry-bulb temperature, state b |
| `p_ws_b` | Saturation pressure, state b |
| `p_w_b` | Vapour pressure, state b |
| `W_b` | Humidity ratio, state b, in grams per kilogram of dry air |
| `RH_b` | Relative humidity, state b, as a number from 0 to 100 |
| `phi_b` | Relative humidity, state b, as a quantity in % (for linking) |
| `T_dp_b` | Dew point, state b |
| `T_wb_b` | Wet-bulb temperature, state b |
| `h_b` | Enthalpy, state b, in kilojoules per kilogram of dry air |
| `v_b` | Specific volume, state b |
| `rho_b` | Moist-air density, state b |
| `m_da_3` | Mixed mass flow |

## Ports

A port is listed under its public name, `<area>.<quantity>`. The output symbol stays as the port's key, so a saved link, a pack binding or an inputs file that uses the symbol or an alias still resolves.

| Public name | Output symbol | Unit | Description |
|---|---|---|---|
| `air.temperature_entering` | `T_1_out` (was `t_1_out`) | °C | Dry-bulb temperature of the entering air |
| `air.humidity_relative_entering` | `phi_1` | % | Relative humidity of the entering air, as a quantity in % |
| `air.temperature_dew_point_entering` | `T_dp_1` (was `t_dp_1`) | °C | Dew point of the entering air |
| `air.temperature_wet_bulb_entering` | `T_wb_1` (was `t_wb_1`) | °C | Wet-bulb temperature of the entering air |
| `air.humidity_ratio_entering` | `W_1` |  | Humidity ratio of the entering air, in g per kg dry air |
| `air.enthalpy_entering` | `h_1` |  | Enthalpy of the entering air, in kJ per kg dry air |
| `air.temperature_leaving` | `T_2_out` (was `t_2_out`) | °C | Dry-bulb temperature of the leaving air |
| `air.humidity_ratio_leaving` | `W_2` |  | Humidity ratio of the leaving air, in g per kg dry air |
| `air.enthalpy_leaving` | `h_2` |  | Enthalpy of the leaving air, in kJ per kg dry air |
| `air.heat_flow` | `Phi` (was `Q`) | kW | Heat added (+) or removed (−) by the sensible or steam process |

## Checks

Applicability rules:

| Rule | Severity | Effect on the result | Condition |
|---|---|---|---|
| `second-property` | Block | Conservative | The chosen second property has no value. |
| `supersaturated` | Block | Conservative | The state lies above saturation (RH > 100 %). |
| `process-input` | Block | Conservative | The process requires a leaving-state value that was not given, or it is not reachable. |
| `no-dehumidification` | Warning | Conservative | The mixture is supersaturated: fog forms and the state shown is nominal. |
| `dry-coil` | Warning | Conservative | Cooling with dehumidification and RH_2 blank, but T_2 is above the entering dew point: the coil is dry, the leaving moisture is held at the entering value and the load is sensible only. |

## Limitations



## References

- ASHRAE Handbook — Fundamentals, Ch. 1: Psychrometrics (ideal-gas relations, processes).
- Hyland & Wexler, ASHRAE Trans. 89(2A), 1983: Saturation pressure of water and ice.
- CIBSE Guide C, §1: Properties of humid air (principles).

## Test cases

### Notes on the cases

Saturated air at 20 °C against ASHRAE Table 3 (within 0.5 %, enhancement factor explained); ASHRAE Ch. 1 Example 1 style 40 °C / 20 °C wet-bulb (Unconfirmed against the printed example; values from the same relations); sensible heating (analytic); mixing (analytic); cooling with dehumidification 28 °C / 50 % to 12 °C saturated at 1 kg/s (sensible 16.448 kW, latent 7.818 kW, SHR 0.6778, total 24.111 kW) and the dry coil 28 °C / 50 % to 20 °C (8.224 kW), both from the Hyland–Wexler and ASHRAE Ch. 1 relations in python3.

- `ka-saturation-20`: Saturated air at 20 °C, 101.325 kPa (Confirmed; source: ASHRAE Handbook — Fundamentals, 2017)
- `ka-ashrae-example-1`: 40 °C dry-bulb, 20 °C wet-bulb, sea level (ASHRAE Ch. 1 Example 1 style) (Unconfirmed; source: ASHRAE Handbook — Fundamentals, 2017)
- `ka-sensible-heating`: Sensible heating 10 → 30 °C at W = 5 g/kg, 1 kg/s (analytic) (Confirmed; source: Analytic from h = 1.006 t + W(2501 + 1.86 t))
- `ka-mixing`: Adiabatic mixing of 2 kg/s at 30 °C, 10 g/kg with 1 kg/s at 15 °C, 5 g/kg (analytic) (Confirmed; source: Analytic mass and energy balance)

## Changes

**Changes in 1.4.0 (C-2 consistency, phase C).** Layout and wording; no calculated value changes. The temperatures are now `T_db`, `T_wb`, `T_dp`, `T_2` and `T_db_b` (were `t_db`, `t_wb`, `t_dp`, `t_2` and `t_db_b`), the pressure `p` is now `p_atm` and the enthalpy input `h` is now `h_air`; the old symbols stay as aliases. The state outputs are `T_1_out`, `T_dp_1`, `T_wb_1`, `T_2_out` and `Phi`; the ports `air.*` keep the old lower-case symbols as aliases. The inputs sit in the groups Setup, Conditions, Process and Mixing, and the relative humidity has a usual range of 5 to 100. The messages are one or two short sentences. The humidity is described as a number from 0 to 100.

**Cooling with dehumidification (1.3.0).** The coil load splits along the path t₁ → t₂ at W₁ (sensible) then W₁ → W₂ at t₂ (latent): `Q_sensible = ṁ (h₁ − h_air(t₂, W₁))`, `Q_latent = ṁ (h_air(t₂, W₁) − h₂)`, `SHR = Q_sensible/(Q_sensible + Q_latent)`; `Q_total` also subtracts the condensate enthalpy (so the two parts add to `ṁ(h₁ − h₂)`, about 0.6 % above `Q_total` at 28 °C / 50 % to 12 °C saturated). Before 1.3.0 the two parts were exchanged (sensible 7.82 kW, latent 16.45 kW, SHR 0.322 for 28 °C / 50 % to 12 °C saturated at 1 kg/s; correct 16.45 kW, 7.82 kW, 0.678). A leaving state wetter than the entering state is not a cooling coil: with `RH_2` blank and `T_2` above the entering dew point (the saturated state at `T_2` holds more water than entered) the coil is **dry**, the air leaves at W₁, no condensate forms, the load is sensible only (SHR 1) and the sheet warns (`dry-coil`); an explicit `RH_2` that puts more moisture in the air than entered blocks (`process-input`). Before 1.3.0 a blank `RH_2` defaulted to 100 % and gave a leaving state wetter than the entering one with a warning only (28 °C / 50 % to 20 °C: Q_total 0.948 kW, SHR −7.67; dry coil 8.224 kW).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Summer cooling coil: 28 °C / 60 % to 12 °C / 95 % ([sheet](../../worked-examples/building-services/bs.psychrometrics/cooling-coil-summer/sheet.txt)).

```text
p_atm = z = 0 m = 101.32 kPa
p_ws_1 = t = 28 °C = 3.7822 kPa
p_w_1 = W = 0.014249 = 2.2693 kPa
T_1_out = t = 28 °C = 28 degC
W_1 = 2.2693 / (101.32 − 2.2693) = 14.249
RH_1 = 2.2693 / 3.7822 = 60
phi_1 = 2.2693 / 3.7822 = 60 %
T_dp_1 = p_w = 2.2693 kPa = 19.514 degC
T_wb_1 = t = 28, W = 0.014249 = 22.074 degC
h_1 = 1.006 × 28 + 0.014249 × (2501 + 1.86 × 28) = 64.546
v_1 = p = 101.32 kPa = 0.87267 m³/kg
rho_1 = (1 + 0.014249) / 0.87267 = 1.1622 kg/m³
p_ws_2 = t = 12 °C = 1.4026 kPa
p_w_2 = W = 0.0082878 = 1.3325 kPa
… 15 more step(s) on the sheet
```
<!-- hand-arithmetic:end -->
