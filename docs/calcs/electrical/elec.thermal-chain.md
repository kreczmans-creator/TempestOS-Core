# Heat sink thermal resistance chain (`elec.thermal-chain@1.1.1`)

**In plain terms.** The heat a component makes has to flow out to the air through a chain of resistances: from the chip to its case, from the case through the interface pad to the heat sink, and from the sink to the air. Each resistance in kelvin per watt times the power gives that stage's temperature rise. Adding the rises to the ambient gives the junction temperature. Turned round, the permitted rise divided by the power gives the largest total resistance the chain may have: the budget a heat sink is chosen against.

## Scope

Calculates the steady-state source temperature from a series chain of thermal resistances to ambient, with the rise across each stage. It gives the margin against the permitted maximum, negative over the limit, and the largest total resistance allowed.

## Method

- Temperature rise across each stage: ΔT_i = P·R_i.
- Total resistance of the chain and the total rise: R_tot = Σ R_i; ΔT_tot = P·R_tot.
- Source (hottest node) temperature, each node below it being the one above less that stage's rise: T_1 = T_a + ΔT_tot.
- Margin against the permitted maximum: margin = T_max − T_1.
- Largest total resistance the limit allows, the heat sink budget: R_max = (T_max − T_a)/P.
- Utilisation of the permitted rise: u = ΔT_tot/(T_max − T_a).

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group |
|---|---|---|---|---|---|
| `P` | Steady power dissipated (the heat flow through every stage) | kW | 0.025 | 0 to 10 | Source |
| `T_a` | Ambient the last stage rejects to | °C | 40 | -40 to 85 | Conditions |
| `stages` | Thermal resistances in series, hottest first |  | table | columns: name, R (K/W) | Chain |
| `T_max` | Maximum permitted temperature of the hottest node (junction or case) | °C | 125 | 40 to 200 | Source |

## Assumptions

- Steady state: the whole dissipated power flows through the one chain, and each resistance is constant. Basis: No thermal capacitance (nothing about warm-up or a transient overload); no parallel path through the board.
- The stated ambient is the temperature the last stage actually rejects to. Basis: Air preheated by neighbouring parts can be well above room temperature.

## Outputs

| Symbol | Description | Unit |
|---|---|---|
| `T_1` | Source temperature | °C |
| `dT_m` | Thermal margin | K |
| `u` | Utilisation |  |
| `R_max` | Heat sink budget | K/W |
| `R_tot` | Total thermal resistance | K/W |
| `dT_tot` | Total rise | K |
| `dT_1` | Rise across stage 1 | K |
| `dT_2` | Rise across stage 2 | K |
| `T_2` | Hot side of stage 2 | °C |
| `dT_3` | Rise across stage 3 | K |
| `T_3` | Hot side of stage 3 | °C |

## Ports

A port is listed under its public name, `<area>.<quantity>`. The output symbol stays as its first alias, so a saved link, a pack binding or an inputs file that uses the symbol still resolves.

| Public name | Output symbol | Unit | Description |
|---|---|---|---|
| `heatsink.temperature_source` | `T_1` | degC | Source temperature at the hot end of the chain |
| `heatsink.temperature_margin` | `dT_m` | K | Temperature margin to the permitted maximum, negative when over the limit |
| `heatsink.utilisation` | `u` |  | Rise from ambient over the permitted rise |
| `heatsink.resistance_max` | `R_max` | K/W | Largest total thermal resistance the limit allows, the heat sink budget |
| `heatsink.resistance_total` | `R_tot` | K/W | Total thermal resistance of the chain |

## Checks

Checks the sheet makes, governing first on the printed sheet:

- Rise from ambient against the permitted rise (Çengel §15-2)

Applicability rules:

| Rule | Severity | Effect on the result | Condition |
|---|---|---|---|
| `no-stages` | Block | Conservative | At least one thermal resistance stage is needed. |
| `zero-chain` | Block | Conservative | The total thermal resistance must be positive. |
| `limit` | Block | Conservative | The maximum source temperature must be above the ambient. |

## Limitations

Steady state only (no thermal capacitance or transient overload); one series path (no parallel path through the board); constant resistances. Rises are temperature differences in kelvin; only node temperatures carry Celsius's offset. A chain with no stages, a zero total resistance, or a maximum at or below ambient blocks.

## References

- JEDEC JESD51-1:1995: Integrated circuit thermal measurement method: electrical test method (junction-to-reference resistances)
- Çengel & Ghajar, Heat and Mass Transfer, §15-2, 5th ed. (edition to be checked): Thermal resistance networks in the cooling of electronic equipment
- Incropera & DeWitt, Fundamentals of Heat and Mass Transfer, §3.1, 7th ed. (edition to be checked): Thermal resistance in series

## Test cases

- `ka-legacy-three-stage`: Legacy example 1: three-stage chain at 25 W (Confirmed; source: TempestOS calc.thermal-resistance-chain (v0.21.0) hand calculation)
- `ka-legacy-sink-selection`: Legacy example 2: heat sink selection for a 60 W transistor (after Çengel & Ghajar) (Confirmed; source: TempestOS calc.thermal-resistance-chain (v0.21.0) hand calculation)
- `ka-legacy-over-limit`: Legacy example 3: the same chain at 40 W exceeds the limit (Confirmed; source: TempestOS calc.thermal-resistance-chain (v0.21.0) hand calculation)
- `ka-legacy-on-limit`: Legacy example 4: exactly on the limit at 34 W (Confirmed; source: TempestOS calc.thermal-resistance-chain (v0.21.0) hand calculation)

## Changes

**1.1.1.** Declares the unit change of 1.1.0 (ADR-0177 review): `P` was in W before 1.1.0, so a bare stored number from an earlier version is read in W, never kW. No calculated value changes.

**Changes in 1.1.0 (C-2 consistency, phase C).** Layout and wording only; no result changes. `P` is entered in kW (the default is 0.025 kW, the 25 W it was); a saved value keeps its value and unit. The check compares the rise `dT_tot` with the new step `dT_allow`, the permitted rise in K. The template declares the ports `heatsink.*`. The scope, the messages and the references follow the sheet standard. The stage column `R` keeps its key, because a table column has no alias mechanism and a renamed key would break saved tables.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Three-stage chain, 25 W, 40 °C ambient ([sheet](../../worked-examples/electrical/elec.thermal-chain/three-stage-25W/sheet.txt)).

```text
R_tot = 0.5 + 0.2 + 1.8 K/W = 2.5 K/W
dT_tot = 25 W × 2.5 K/W = 62.5 K
T_1 = 40 degC + 62.5 K = 102.5 degC
dT_1 = 25 W × 0.5 K/W = 12.5 K
dT_2 = 25 W × 0.2 K/W = 5 K
T_2 = 102.5 °C − 12.5 K = 90 degC
dT_3 = 25 W × 1.8 K/W = 45 K
T_3 = 90 °C − 5 K = 85 degC
dT_m = 125 degC − 102.5 degC = 22.5 K
dT_allow = 125 degC − 40 degC = 85 K
u = 62.5 K / 85 K = 0.73529
R_max = (125 degC − 40 degC) / 25 W = 3.4 K/W
Governing: PASS  Rise from ambient against the permitted rise: 62.50 K vs 85 K  utilisation 0.74  « Çengel §15-2
```
<!-- hand-arithmetic:end -->
