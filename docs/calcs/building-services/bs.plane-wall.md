# B4 — Plane wall heat transfer (`bs.plane-wall@1.0.1`)

**In plain terms.** Heat crossing a wall passes through a chain of resistances: the air film on the warm side, each layer of the wall, and the air film on the cold side. Adding the resistances gives the whole wall's resistance, and the temperature difference divided by it gives the heat flow. Each surface temperature then follows by stepping down the drops one at a time. A thin layer of still air is a good insulator, which is why a double-glazed window loses about a quarter of the heat a single pane does.

**Scope.** Steady one-dimensional heat flow through a composite plane wall between two fluids, or between two known surface temperatures. Ported from the legacy `calc.plane-wall-heat-transfer` (ADR-0167). Method after Incropera & DeWitt §3.1 and Çengel & Ghajar §3-3; clause numbers only (BS EN ISO 6946 §6).

**Method.** Film `R = 1/(h·A)`; layer `R_i = t_i/(k_i·A)`; `R_tot = ΣR`; `Q = (T_1 − T_2)/R_tot`; `q = Q/A`; `U = 1/(R_tot·A)`; surface temperatures step down from `T_1` by `Q·R` across each resistance.

**Inputs.** `T_1`, optional `h_1`, table `layers` (name, thickness, conductivity), optional `h_2`, `T_2`, `A`. Omit a film coefficient when its temperature is the surface's own. **Outputs.** `R_h1`, `R_i`, `R_h2`, `R_tot`, `Q`, `q`, `U`, surface and interface temperatures `T_s0 … T_sN`. **Checks.** None: the result is a heat flow, not a pass or fail. A warning states when side 2 is the warmer.

**Limitations.** Plane layers of constant conductivity, perfect contact (enter a contact resistance as a thin layer), no internal heat generation, no edge losses, constant film coefficients. No condensation (dew-point) check.

**Test cases.** The legacy module's three hand calculations (`ka-legacy-single-pane`, `ka-legacy-double-pane`, `ka-legacy-surfaces`).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Single-pane window, 20 °C inside, −10 °C outside ([sheet](../../worked-examples/building-services/bs.plane-wall/single-pane-window/sheet.txt)).

```text
R_h1 = 1 / (10 W/(m²·K) × 1.2 m²) = 0.083333 K/W
R_1 = 8 mm / (0.78 W/(m·K) × 1.2 m²) = 0.008547 K/W
R_h2 = 1 / (40 W/(m²·K) × 1.2 m²) = 0.020833 K/W
R_tot = 0.083333 + 0.008547 + 0.020833 K/W = 0.11271 K/W
Q = (20 degC − (-10 degC)) / 0.11271 K/W = 266.16 W
q = 266.16 W / 1.2 m² = 221.8 W/m²
U = 1 / (0.11271 K/W × 1.2 m²) = 7.3934 W/(m²·K)
T_s0 = 20 °C − 266.16 W × 0.083333 K/W = -2.1801 degC
T_s1 = -2.1801 °C − 266.16 W × 0.008547 K/W = -4.455 degC
```
<!-- hand-arithmetic:end -->
