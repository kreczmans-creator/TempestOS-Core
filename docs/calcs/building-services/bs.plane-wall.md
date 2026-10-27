# Plane wall heat transfer (layers and films) (`bs.plane-wall@1.1.0`)

**In plain terms.** Heat crossing a wall passes through a chain of resistances: the air film on the warm side, each layer of the wall, and the air film on the cold side. Adding the resistances gives the whole wall's resistance, and the temperature difference divided by it gives the heat flow. Each surface temperature then follows by stepping down the drops one at a time. A thin layer of still air is a good insulator, which is why a double-glazed window loses about a quarter of the heat a single pane does.

## Scope

Calculates the steady heat flow through a composite plane wall, between two fluids or between two known surface temperatures. It gives the overall U-value, the heat flux and every surface and interface temperature.

## Method

- Resistance of each surface film: R = 1/(h·A).
- Resistance of each conducting layer: R_i = t_i/(k_i·A).
- Total resistance, hot side first: R_tot = Σ R.
- Heat flow through the wall: Phi = (T_1 − T_2)/R_tot.
- Heat flux and overall U-value: q = Phi/A; U_wall = 1/(R_tot·A).
- Surface and interface temperatures, stepping down from T_1 across each resistance in turn: T_next = T_previous − Phi·R.

### Notes on the method

Film `R = 1/(h·A)`; layer `R_i = t_i/(k_i·A)`; `R_tot = ΣR`; `Phi = (T_1 − T_2)/R_tot`; `q = Phi/A`; `U_wall = 1/(R_tot·A)`; surface temperatures step down from `T_1` by `Phi·R` across each resistance.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Hard range | Group |
|---|---|---|---|---|---|---|
| `layers` | Conducting layers, side 1 first |  | table |  |  | Setup |
| `layers.name` | Layer (column) |  | optional | | | Setup |
| `layers.t` | Thickness (column) | mm |  | | | Setup |
| `layers.k` | Thermal conductivity (column) | W/(m·K) |  | | | Setup |
| `A` | Wall area normal to the heat flow | m² | 1 | 0.001 to 10000 | 1e-09 to 10000000 | Geometry |
| `T_1` | Side 1 temperature (the fluid's, or the surface's when no film is given) | °C | 20 | -60 to 400 | -273.15 to 5000 | Conditions |
| `T_2` | Side 2 temperature (the fluid's, or the surface's when no film is given) | °C | 0 | -60 to 400 | -273.15 to 5000 | Conditions |
| `h_1` | Side 1 film coefficient (optional; blank = no film, so T_1 is the surface temperature) | W/(m²·K) | 7.7 | 2 to 10000 | 1e-06 to 1000000 | Conditions |
| `h_2` | Side 2 film coefficient (optional; blank = no film, so T_2 is the surface temperature) | W/(m²·K) | 25 | 2 to 10000 | 1e-06 to 1000000 | Conditions |

## Assumptions

1. Steady, one-dimensional conduction through plane layers of constant conductivity, with no internal heat generation. Basis: Thermal-circuit idealisation: equal layer areas, no edge losses.
2. Layers are in perfect thermal contact. Basis: A contact resistance can be entered as a thin layer of equivalent thickness and conductivity.
3. Each film coefficient is constant over its surface. Basis: Thermal-circuit idealisation.

## Outputs

| Symbol | Description |
|---|---|
| `Phi` | Heat flow |
| `q` | Heat flux |
| `U_wall` | Overall heat transfer coefficient |
| `R_tot` | Total thermal resistance |
| `R_h1` | Side 1 film resistance |
| `R_1` | Layer 1 resistance |
| `R_h2` | Side 2 film resistance |
| `T_s0` | Side 1 surface temperature |
| `T_s1` | Side 2 surface temperature |
| `R_2` | Layer 2 resistance |
| `R_3` | Layer 3 resistance |
| `T_s2` | Interface 2 temperature |
| `T_s3` | Side 2 surface temperature |

## Ports

A port is listed under its public name, `<area>.<quantity>`. The output symbol stays as the port's key, so a saved link, a pack binding or an inputs file that uses the symbol or an alias still resolves.

| Public name | Output symbol | Unit | Description |
|---|---|---|---|
| `wall.heat_flow` | `Phi` (was `Q`) | kW | Heat flow from side 1 to side 2 |
| `wall.heat_flux` | `q` | W/m² | Heat flux |
| `wall.u_value` | `U_wall` (was `U`) | W/(m²·K) | Overall heat transfer coefficient, the U-value of the wall |
| `wall.resistance_total` | `R_tot` | K/W | Total thermal resistance |

## Checks

Applicability rules:

| Rule | Severity | Effect on the result | Condition |
|---|---|---|---|
| `no-layers` | Block | Conservative | At least one conducting layer is required. |
| `reversed` | Warning | Conservative | Side 2 is warmer than side 1, so the heat flows from side 2 to side 1 and the heat flow Phi is negative. |

## Limitations

Plane layers of constant conductivity, perfect contact (enter a contact resistance as a thin layer), no internal heat generation, no edge losses, constant film coefficients. No condensation (dew-point) check.

## References

- Incropera & DeWitt, Fundamentals of Heat and Mass Transfer, §3.1, 7th ed.: The plane wall: thermal resistance and the composite wall.
- Çengel & Ghajar, Heat and Mass Transfer, §3-3, 5th ed.: Generalized thermal resistance networks (multilayer plane walls).
- BS EN ISO 6946:2017, cl. 6: Thermal resistance of building components (method).

## Test cases

### Notes on the cases

The legacy module's three hand calculations (`ka-legacy-single-pane`, `ka-legacy-double-pane`, `ka-legacy-surfaces`).

- `ka-legacy-single-pane`: Legacy example 1: single-pane window (Confirmed; source: TempestOS calc.plane-wall-heat-transfer (v0.21.0) hand calculation, after Çengel & Ghajar)
- `ka-legacy-double-pane`: Legacy example 2: double-pane window with a 10 mm air gap (Confirmed; source: TempestOS calc.plane-wall-heat-transfer (v0.21.0) hand calculation, after Çengel & Ghajar)
- `ka-legacy-surfaces`: Legacy example 3: surface temperatures given, no films (Confirmed; source: TempestOS calc.plane-wall-heat-transfer (v0.21.0) hand calculation)

## Changes

**Changes in 1.1.0 (C-2 consistency, phase C).** Layout and wording; no calculated value changes. The heat flow `Q` is now `Phi` (in kW, the port `wall.heat_flow` keeps `Q` as an alias) and the overall coefficient `U` is now `U_wall` (the port `wall.u_value` keeps `U` as an alias). The inputs are ordered layers, A, T_1, T_2, h_1, h_2 in the groups Setup, Geometry and Conditions. The ports are `wall.heat_flow`, `wall.heat_flux`, `wall.u_value` and `wall.resistance_total`. The book references name the section and the edition (§3.1, 7th ed.; §3-3, 5th ed.), and the surface-resistance reference is BS EN ISO 6946:2017, cl. 6. The messages are one or two short sentences. The layer column `t` (thickness) keeps its key, because a saved layer table and its CSV use it.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Single-pane window, 20 °C inside, −10 °C outside ([sheet](../../worked-examples/building-services/bs.plane-wall/single-pane-window/sheet.txt)).

```text
R_h1 = 1 / (10 W/(m²·K) × 1.2 m²) = 0.083333 K/W
R_1 = 8 mm / (0.78 W/(m·K) × 1.2 m²) = 0.008547 K/W
R_h2 = 1 / (40 W/(m²·K) × 1.2 m²) = 0.020833 K/W
R_tot = 0.083333 + 0.008547 + 0.020833 K/W = 0.11271 K/W
Phi = (20 degC − (-10 degC)) / 0.11271 K/W = 0.26616 kW
q = 0.26616 kW / 1.2 m² = 221.8 W/m²
U_wall = 1 / (0.11271 K/W × 1.2 m²) = 7.3934 W/(m²·K)
T_s0 = 20 °C − 266.16 W × 0.083333 K/W = -2.1801 degC
T_s1 = -2.1801 °C − 266.16 W × 0.008547 K/W = -4.455 degC
```
<!-- hand-arithmetic:end -->
