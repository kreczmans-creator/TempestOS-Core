# B2 — Psychrometrics (`bs.psychrometrics@1.2.0`)

**In plain terms.** Moist air is fixed by any two of its properties, such as temperature and relative humidity; from them follow how much water it holds, its enthalpy, its dew point and its wet-bulb temperature. Heating, cooling with dehumidification, mixing two airstreams and adding steam or spray are each a balance of dry air, water and energy, so the state after each process, and the heat or water it takes, comes straight from those balances.

**Scope.** Moist-air state from dry-bulb plus wet-bulb, RH, humidity ratio, dew point or enthalpy, at a pressure or a standard-atmosphere altitude; outputs p_ws, p_w, W, RH, dew point, wet-bulb, h, v, ρ. One process on a dry-air mass flow: sensible heating/cooling, cooling with dehumidification (condensate, total/sensible/latent, SHR), steam humidification, adiabatic humidification (target RH or saturation efficiency), adiabatic mixing of two streams. Chart series: saturation curve, RH 20–80 % lines, process line.

**Method.** Hyland–Wexler (1983) saturation pressure over water (≥ 0 °C) and ice; ASHRAE Fundamentals Ch. 1 ideal-gas relations in own notation (`MoistAir`): W = 0.621945 p_w/(p − p_w), h = 1.006 t + W(2501 + 1.86 t), v = 0.287042 T(1 + 1.607858 W)/p; dew point and wet-bulb by capped bisection (wet-bulb from the adiabatic-saturation relation, ice form below 0 °C). Enhancement factor taken as 1 (assumption A1; ≈ 0.4 % at 20 °C).

**Inputs.** `altitude`, `p`, `t_db`, `known`, one of `RH`/`t_wb`/`W`/`t_dp`/`h`, `m_da`, `process`, `t_2`, `RH_2`, `W_2`, `h_steam`, `eff`, `t_db_b`, `RH_b`, `m_da_b`. **Outputs.** per state `t_<k>_out`, `p_ws_<k>`, `p_w_<k>`, `W_<k>` (g/kg), `RH_<k>` (%), `phi_<k>` (the same relative humidity as a percentage quantity, SI 0.5 for 50 %; added in 1.1.0 because a plain-number output cannot be linked to a percentage input, which would read 5000 %: it is the B7 `RH_int` link of the envelope-moisture chain), `t_dp_<k>`, `t_wb_<k>`, `h_<k>` (kJ/kg), `v_<k>`, `rho_<k>`; process `Q`, `Q_total`, `Q_sensible`, `Q_latent`, `SHR`, `m_w`, `m_s`, `m_w_add`, `eff_out`, `m_da_3`. **Checks.** none; blocks for supersaturated or unreachable states.

**Test cases.** Saturated air at 20 °C against ASHRAE Table 3 (within 0.5 %, enhancement factor explained); ASHRAE Ch. 1 Example 1 style 40 °C / 20 °C wet-bulb (Unconfirmed against the printed example; values from the same relations); sensible heating (analytic); mixing (analytic).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Summer cooling coil: 28 °C / 60 % to 12 °C / 95 % ([sheet](../../worked-examples/building-services/bs.psychrometrics/cooling-coil-summer/sheet.txt)).

```text
p = z = 0 m = 101.32 kPa
p_ws_1 = t = 28 °C = 3.7822 kPa
p_w_1 = W = 0.014249 = 2.2693 kPa
t_1_out =  = 28 degC
W_1 = 2.2693 / (101.32 − 2.2693) = 14.249
RH_1 = 2.2693 / 3.7822 = 60
phi_1 = 2.2693 / 3.7822 = 60 %
t_dp_1 = p_w = 2.2693 kPa = 19.514 degC
t_wb_1 = t = 28, W = 0.014249 = 22.074 degC
h_1 = 1.006 × 28 + 0.014249 × (2501 + 1.86 × 28) = 64.546
v_1 = p = 101.32 kPa = 0.87267 m³/kg
rho_1 =  = 1.1622 kg/m³
p_ws_2 = t = 12 °C = 1.4026 kPa
p_w_2 = W = 0.0082878 = 1.3325 kPa
… 15 more step(s) on the sheet
```
<!-- hand-arithmetic:end -->
