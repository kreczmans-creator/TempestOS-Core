# Heat exchanger (LMTD and ε-NTU) (`bs.heat-exchanger@1.5.0`)

**In plain terms.** A heat exchanger passes heat from a hot stream to a cold one through a wall, and how much it passes depends on the area, how well the wall conducts, and the flow arrangement. The worksheet gives either the duty and outlet temperatures of a given exchanger, or the area needed for a given duty, by the effectiveness-NTU method, and cross-checks the answer with the log-mean temperature difference.

## Scope

Calculates a single-phase heat exchanger with constant properties: the duty and outlet temperatures for a given area, or the area for a given duty. It covers counterflow, parallel flow, shell and tube, crossflow and one fluid at constant temperature.

## Method

- Capacity rates: C_h = ṁ_h·c_p,h, C_c = ṁ_c·c_p,c, C_r = C_min/C_max; with one fluid at constant temperature (C_r = 0) that fluid's capacity rate is infinite, C_min is the other stream's and the constant stream leaves at its inlet temperature
- Overall coefficient with fouling: U_f = 1/(1/U_clean + R_f)
- Transfer units: NTU = U_f·A/C_min; effectiveness ε(NTU, C_r) for the arrangement
- Heat exchanger duty: Phi = ε·C_min·(T_h,in − T_c,in); outlets from the energy balance
- Area for a required duty: ε = Q_req/(C_min·ΔT_max), NTU from the inverse relation, A = NTU·C_min/U_f
- Cross-check with LMTD: Phi = U_f·A·F_corr·ΔT_lm, F_corr from R and P (one shell pass) or back-calculated

### Notes on the method

`C = ṁ c_p`, `C_r = C_min/C_max`, `U_f = 1/(1/U_clean + R_f)`, `NTU = U_f A/C_min`; ε(NTU, C_r) and its inverse per Incropera 7th ed. Tables 11.3–11.4 (crossflow inverse by bisection). `Phi = ε C_min (T_h,in − T_c,in)`; outlets by energy balance. Cross-check `Phi = U_f A F_corr ΔT_lm`, F from R and P for one shell pass, back-calculated for crossflow. Pinched ends warn and skip the LMTD line. **One fluid at constant temperature (C_r = 0)** takes `const_side` (the hot side, e.g. condensing vapour, or the cold side, e.g. boiling liquid): that fluid's capacity rate is infinite, `C_min` is the *other* stream's, `ε = 1 − e^−NTU`, and the constant stream leaves at its inlet temperature (its flow and specific heat are not used). The ε-NTU and LMTD duties describe one exchanger, so a gap of more than 2 % between them warns (`lmtd-mismatch`).

**Reference diagram.** The worksheet form and the issued sheet (Figure 1) show a reference diagram: not to scale, inputs only, no result (ADR-0158, DI-08). It draws the shell with the hot stream above the dividing wall and the cold stream below, the flow arrows as the arrangement runs them. One variant per arrangement (and per constant side for C_r = 0) and per mode; the rating variants label `A`, the sizing variants `Q_req`. It binds `A`, `Q_req`, `dT_min`, `m_h`, `cp_h`, `T_h_in`, `m_c`, `cp_c`, `T_c_in`, `U_clean` and `R_f`.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Hard range | Group |
|---|---|---|---|---|---|---|
| `arrangement` | Flow arrangement |  | counterflow | counterflow, parallel flow, shell and tube, 1 shell pass, 2n tube passes, crossflow, both fluids unmixed (approximate), one fluid at constant temperature (C_r = 0) |  | Setup |
| `mode` | Mode |  | rating (given area) | rating (given area), sizing (given duty) |  | Setup |
| `const_side` | Which side is at constant temperature (used only by the C_r = 0 arrangement) |  | hot side at constant temperature (e.g. condensing vapour) | hot side at constant temperature (e.g. condensing vapour), cold side at constant temperature (e.g. boiling liquid) |  | Setup |
| `m_h` | Hot-side mass flow | kg/s | 1 | 0.01 to 50 | 0.001 to 100 | Hot side |
| `cp_h` | Hot-side specific heat | J/(kg·K) | 4180 | 500 to 10000 | 1 to 100000 | Hot side |
| `T_h_in` | Hot-side inlet temperature | °C | 80 | -20 to 180 | -40 to 200 | Hot side |
| `T_dew` | Dew point of the hot-side air, when the hot side is humid air (optional; blank = not checked, and the duty here is sensible only) | °C | optional | -40 to 60 | -80 to 100 | Hot side |
| `m_c` | Cold-side mass flow | kg/s | 1 | 0.01 to 50 | 0.001 to 100 | Cold side |
| `cp_c` | Cold-side specific heat | J/(kg·K) | 4180 | 500 to 10000 | 1 to 100000 | Cold side |
| `T_c_in` | Cold-side inlet temperature | °C | 20 | -20 to 150 | -40 to 200 | Cold side |
| `U_clean` (was `U`) | Overall heat transfer coefficient (clean) | W/(m²·K) | 1000 | 20 to 8000 | 10 to 10000 | Exchanger |
| `R_f` | Total fouling resistance | m²·K/W | 0 | 0 to 0.01 | 0 to 0.1 | Exchanger |
| `A` | Heat transfer area (rating) | m² | 4.18 | 0.05 to 500 | 0.01 to 1000 | Exchanger |
| `dT_min` | Minimum approach temperature difference | K | 2 | 0 to 50 | 0 to 200 | Requirements |
| `Q_req` | Required duty | kW | optional | 0.001 to 100000 | 1e-06 to 1000000 | Requirements |

## Assumptions

1. Single phase on both sides (except the C_r = 0 arrangement), constant properties, no heat loss to surroundings. Basis: Incropera 7th ed. §11.3–11.4.
2. The crossflow both-unmixed effectiveness is the published approximate correlation, flagged on the sheet. Basis: Incropera 7th ed. Table 11.3.
3. Specific heats are user records at the mean temperature of each stream. Basis: Entered as reference records, which carry their source, edition and licence.

## Outputs

| Symbol | Description |
|---|---|
| `C_r` | Capacity ratio |
| `NTU` | Transfer units |
| `eps` | Effectiveness |
| `Phi` | Heat duty |
| `T_h_out` | Hot outlet |
| `T_c_out` | Cold outlet |
| `dT_c` | Cold-side rise |
| `T_c_m` | Cold-side mean |
| `cp_c` | Cold-side specific heat |
| `dT_app` | Approach |
| `UA` | Overall conductance U·A |
| `dT_lm` | Log-mean temperature difference |
| `Q_lmtd` | Duty by the LMTD method |
| `F_corr` | LMTD correction factor |
| `u_duty` | Duty utilisation |
| `A_req` | Required area |

`C_r`, `NTU`, `eps`, `Phi`, `T_h_out`, `T_c_out`, `dT_lm`, `F_corr`, `Q_lmtd`, `dT_app`, `UA`, `A_req` (sizing), and from 1.1.0 `dT_c` (cold-side rise, K) and `T_c_m` (mean cold-side temperature, °C). **Ports.** `Phi`, `T_h_out`, `T_c_out`, `A_req`, `dT_c`, `T_c_m`, `cp_c` → B1: `Phi` is its `Phi_load`, `dT_c` its `dT_load`, `T_c_m` its fluid temperature `T` and `cp_c` its `cp_load` (the cooling chain, `pack.cooling`); `Q_req` takes E2's `P_cool` and `T_h_in` E2's `T_max` (the enclosure air enters the coil at its limit; hot-side c_p 1006 J/(kg·K) is the conventional dry-air value E2 uses). **Checks.** approach against the minimum; duty against required (rating).

## Ports

A port is listed under its public name, `<area>.<quantity>`. The output symbol stays as the port's key, so a saved link, a pack binding or an inputs file that uses the symbol or an alias still resolves.

| Public name | Output symbol | Unit | Description |
|---|---|---|---|
| `exchanger.duty` | `Phi` (was `Q`) | kW | Heat duty |
| `exchanger.temperature_hot_out` | `T_h_out` | °C | Hot-side outlet temperature |
| `exchanger.temperature_cold_out` | `T_c_out` | °C | Cold-side outlet temperature |
| `exchanger.temperature_rise_cold` | `dT_c` | K | Cold-side temperature rise, T_c,out − T_c,in (→ pipe-pump dT_load) |
| `exchanger.temperature_cold_mean` | `T_c_m` | °C | Mean cold-side temperature (→ pipe-pump T_fluid) |
| `exchanger.specific_heat_cold` | `cp_c` | J/(kg·K) | Cold-side specific heat as entered (→ pipe-pump cp_load, so a glycol mixture reaches the pump flow) |
| `exchanger.area_required` | `A_req` | m² | Required area (sizing) |

## Checks

Checks the sheet makes:

- Approach against minimum
- Duty against required

Applicability rules:

| Rule | Severity | Effect on the result | Condition |
|---|---|---|---|
| `temperatures` | Block | Conservative | The hot inlet must be hotter than the cold inlet. |
| `area-missing` | Block | Conservative | Rating needs an area; sizing needs a required duty. |
| `duty-unattainable` | Block | Conservative | The required duty exceeds what any area of this arrangement can transfer. |
| `lmtd-mismatch` | Warning | Conservative | The ε-NTU duty and the LMTD duty differ by more than 2 %: the arrangement or the temperatures are inconsistent. |
| `crossflow-approx` | Warning | Conservative | Crossflow both-unmixed effectiveness is an approximate correlation. |
| `condensation` | Warning | Unconservative | The cold-side inlet is below the dew point of the hot-side air: the coil surface can condense water, which this sensible-only duty does not include and which needs a drain. |
| `pinch` | Warning | Conservative | An end temperature difference is zero: the area is effectively infinite. |
| `low-F` | Warning | Conservative | F below 0.75: the 1-shell-pass arrangement is used inefficiently; consider more shell passes. |

## Limitations

Single phase; constant properties; no pressure drop.

## References

- Incropera, Fundamentals of Heat and Mass Transfer, §11.3–11.5, Tables 11.3–11.4, 7th ed.: LMTD and effectiveness-NTU methods.
- Çengel & Ghajar, Heat and Mass Transfer, Ch. 11, 5th ed.: Heat exchangers.

## Test cases

### Notes on the cases

`ka-counter-balanced` (ε 0.5, ΔT_lm 30 K), `ka-parallel-balanced` (ε 0.432332, Q 108.42896 kW — tightened to 1e-6), `ka-isothermal-hot` (hot side constant, cold 2 kg/s: NTU 0.5, Q 197.364 kW, cold side to 43.608 °C), `ka-isothermal-cold` (cold side constant: Q 158.536 kW, hot side to 42.073 °C), `ka-shell-tube` (ε 0.539940, F 0.923456 — ε-NTU and LMTD agree), `ka-counter-sizing` (NTU 2, A 8.36 m²) — analytic, Confirmed; `ka-incropera-11-1` (q 8524 W, T_c,o 40.2 °C, ΔT_lm 43.2 °C, UA 197.3 W/K) — **Unconfirmed** until checked against Incropera 7th ed. Example 11.1.

- `ka-counter-balanced`: Balanced counterflow, NTU = 1 (Confirmed; source: Analytic (ε = NTU/(1 + NTU) for C_r = 1))
- `ka-parallel-balanced`: Balanced parallel flow, NTU = 1 (Confirmed; source: Analytic (ε = (1 − e^(−2·NTU))/2 for C_r = 1))
- `ka-shell-tube`: 1-2 shell and tube, C_r = 0.5, NTU = 1: ε-NTU and F agree (Confirmed; source: Analytic (Incropera Table 11.3 shell-and-tube relation; F from R and P), 7th ed.)
- `ka-counter-sizing`: Counterflow sizing, C_r = 0.5, ε = 0.774600 (Confirmed; source: Analytic (inverse counterflow relation))
- `ka-isothermal-hot`: Hot side at constant temperature (condensing vapour), cold water 2 kg/s, NTU = 0.5 on the finite stream (Confirmed; source: Analytic (ε = 1 − e^(−NTU), C_r = 0, C_min = the cold stream))
- `ka-isothermal-cold`: Cold side at constant temperature (boiling liquid), hot water 1 kg/s, NTU = 1 on the finite stream (Confirmed; source: Analytic (ε = 1 − e^(−NTU), C_r = 0, C_min = the hot stream))
- `ka-incropera-11-1`: Oil cooler, counterflow concentric tube (Incropera Example 11.1) (Unconfirmed; source: Incropera et al., Fundamentals of Heat and Mass Transfer, 7th ed.)

## Changes

**Changes in 1.5.0 (C-2 consistency, phase C).** Layout and wording; no calculated value changes. The clean overall coefficient `U` is now `U_clean` (alias kept), the duty `Q` is now `Phi` and the correction factor `F` is now `F_corr`. The port `exchanger.duty` keeps `Q` as an alias, so a link or a pack binding that names `Q` still resolves; the pack bindings of the cooling chain now name `Phi`. The inputs sit in the groups Setup, Hot side, Cold side, Exchanger and Requirements, and the usual ranges lie inside the hard ranges. The checks read "Approach against minimum" and "Duty against required". The record kind of the specific heats is titled "Fluid properties (specific heats)".

**Port names (C-2).** Each port is listed under its public name, `<area>.<quantity>`; the output symbol stays as its symbol and as an alias, so a link, a pack binding or an inputs file written before the rename still resolves. `Phi` = `exchanger.duty`; `T_h_out` = `exchanger.temperature_hot_out`; `T_c_out` = `exchanger.temperature_cold_out`; `A_req` = `exchanger.area_required`; `dT_c` = `exchanger.temperature_rise_cold`; `T_c_m` = `exchanger.temperature_cold_mean`; `cp_c` = `exchanger.specific_heat_cold`.

**Dew point (1.3.0).** The duty is sensible only. The optional input `T_dew` (the dew point of the hot-side air, from the site's humidity) makes the sheet warn (rule `condensation`) when the cold-side inlet is below it: the coil surface lies between the water and the air temperature and can condense water, which this duty does not include and which needs a drain (35 °C and 60 % RH give a dew point of 26.07 °C, and the example coil's water enters at 22 °C). Blank: not checked.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** District heating plate exchanger sizing, counterflow ([sheet](../../worked-examples/building-services/bs.heat-exchanger/lthw-plate-hx-sizing/sheet.txt)).

```text
C_h = 1.2 kg/s × 4190 J/(kg·K) = 5028 W/K
C_c = 1.5 kg/s × 4180 J/(kg·K) = 6270 W/K
C_min = min(5028 W/K, 6270 W/K) = 5028 W/K
C_r = min(5028 W/K, 6270 W/K) / max(5028 W/K, 6270 W/K) = 0.80191
U_f = 1 / (1 / 3000 W/(m²·K) + 0.0001 m²·K/W) = 2307.7 W/(m²·K)
eps = 120000 / (5028 × 40) = 0.59666
NTU = ε = 0.59666, C_r = 0.80191 = 1.2973
A_req = 1.2973 × 5028 / 2307.7 = 2.8267 m²
UA = 1.2973 × 5028 = 6523 W/K
Phi = 0.59666 × 5028 × 40 = 120 kW
T_h_out = 80 °C − 120000 / 5028 = 56.134 degC
T_c_out = 40 °C + 120000 / 6270 = 59.139 degC
dT_c = 59.139 °C − 40 °C = 19.139 K
T_c_m = (40 °C + 59.139 °C)/2 = 49.569 degC
… 5 more step(s) on the sheet
Governing: PASS  Approach against minimum: 3 K vs 16.13 K  utilisation 0.19
```
<!-- hand-arithmetic:end -->
