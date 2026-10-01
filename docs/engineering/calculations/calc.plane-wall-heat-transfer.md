# Plane wall heat transfer, conduction layers and convection films — `calc.plane-wall-heat-transfer`

**Status:** added with the recovery of the v0.16.0 suite's thermal
calculator, for
`Tempest.Core.Calculations.Modules.PlaneWallHeatTransferCalculationDefinition`.
The v0.16.0 heat-sink calculator took every resistance as stated and named
the conduction resistance t/(kA) as the next equation to add; this module
is that equation, with the convection film beside it.
Test vectors: `tests/Tempest.Core.Tests/Calculations/Modules/PlaneWallHeatTransferVectors.cs`.

## Method

**In plain terms.** Heat leaking through a wall — a window, a furnace
lining, an enclosure panel — crosses a thin film of slow-moving fluid on
each face and then each solid layer in turn. Each crossing resists the
heat like a resistor: a layer resists more the thicker it is and the
poorer a conductor it is (t / kA); a film resists less the more vigorously
the fluid moves (1 / hA). In series they add, the heat flow is the overall
temperature difference divided by the total, and each surface's
temperature follows by stepping down the drops one at a time. A thin layer
of still air is a remarkably good insulator, which is why a double-glazed
window loses a quarter of the heat a single pane does.

Steady one-dimensional conduction through a composite plane wall by the
thermal-circuit method: Incropera & DeWitt, *Fundamentals of Heat and Mass
Transfer*, chapter 3 (the plane wall, thermal resistance, the composite
wall and the overall heat transfer coefficient); Çengel & Ghajar, *Heat
and Mass Transfer*, chapter 3 (steady heat conduction), whose single-pane
and double-pane window examples are Examples 1 and 2 below. Restated, not
reproduced.

## Inputs

| Input | Dimension | Limits |
|---|---|---|
| `HotSideTemperature` T₁ | Temperature | any; the fluid's, or the surface's when no h₁ |
| `HotSideFilmCoefficient` h₁ | Heat transfer coefficient (W/(m²·K), W/(cm²·K), BTU/(h·ft²·°F)), or none | > 0 when given |
| `Layers` | list of (name, thickness, conductivity), hot side first | ≥ 1 row; named; thickness and conductivity > 0 |
| `ColdSideFilmCoefficient` h₂ | Heat transfer coefficient, or none | > 0 when given |
| `ColdSideTemperature` T₂ | Temperature | any |
| `Area` A | Area | > 0 |

A row is typed `Glass, 8 mm, 0.78 W/(m.K)`.

## Formulae

1. Film resistance R = 1/(h A); layer resistance R = t/(k A).
2. Total R_tot = Σ R (films and layers, hot side first).
3. Heat flow Q = (T₁ − T₂)/R_tot (negative when T₁ < T₂); flux q = Q/A.
4. Overall coefficient U = 1/(R_tot A).
5. Drop across each resistance Q·R; surface and interface temperatures by
   stepping down from T₁: the hot surface, each interface, the cold
   surface (layers + 1 of them).

## Outputs and recorded intermediates

Results: the circuit's names and resistances, R_tot, U, Q, q, each drop,
each surface temperature. There is no criterion, so the run is "Computed".
Intermediates: each resistance, R_tot, U, Q, the surface temperatures.

## Limits of applicability

Steady, one-dimensional, no internal heat generation, constant
conductivities, perfect contact between layers (a contact resistance can
be entered as a thin equivalent layer), equal layer areas (no fins, no
cylinders — a pipe wall needs the logarithmic form). Radiation is not
modelled; a combined film coefficient may include it. No refusal.

## Worked examples (hand calculations)

**Example 1 — single-pane window (Çengel).** 0.8 × 1.5 m (A = 1.2 m²),
8 mm glass k = 0.78 W/(m·K), room 20 °C with h₁ = 10 W/(m²·K), outdoors
−10 °C with h₂ = 40 W/(m²·K).
R₁ = 1/(10 × 1.2) = **0.083333**, R_glass = 0.008/(0.78 × 1.2) =
**0.0085470**, R₂ = 1/(40 × 1.2) = **0.020833 K/W**;
R_tot = **0.112714 K/W**; Q = 30/0.112714 = **266.161 W**
(Çengel: 266 W); q = **221.801 W/m²**; U = **7.39336 W/(m²·K)**.
Inner surface 20 − 266.161 × 0.083333 = **−2.1801 °C** (Çengel: −2.2 °C);
outer surface **−4.4550 °C**.

**Example 2 — double-pane window (Çengel).** The same window with two
4 mm panes and a 10 mm stagnant air gap (k = 0.026 W/(m·K)).
R_glass = **0.0042735** each, R_air = 0.01/(0.026 × 1.2) = **0.32051 K/W**;
R_tot = **0.433226 K/W**; Q = **69.2478 W** (Çengel: 69.2 W);
q = **57.7065 W/m²**; U = **1.92355 W/(m²·K)**. Surfaces **14.229 °C**
(Çengel: 14.2 °C), **13.933**, **−8.2614**, **−8.5573 °C**.

**Example 3 — surface temperatures given, no films.** A = 3 m²; 200 mm
brick k = 0.72 and 50 mm insulation k = 0.04 W/(m·K); inner surface
25 °C, outer 5 °C. R = **0.092593** + **0.41667** = **0.50926 K/W**;
Q = 20/0.50926 = **39.273 W**; q = **13.091 W/m²**;
U = **0.65455 W/(m²·K)**; interface 25 − 39.273 × 0.092593 =
**21.364 °C**.

Precision: five significant figures; relative tolerance 5 × 10⁻⁵ (half a
unit in the fifth significant figure).

## Edge cases

- **Invalid:** no layers, an unnamed layer, a thickness or conductivity
  ≤ 0, a film coefficient ≤ 0, an area ≤ 0.
- **Reversed temperatures:** T₁ < T₂ gives the same magnitude with a
  negative heat flow.
- **Unit mismatch:** Example 1 in °F, BTU/(h·ft²·°F), inches,
  BTU/(h·ft·°F) and ft² (68 °F, 14 °F) gives the same results.
