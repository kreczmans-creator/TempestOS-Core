# Heat sink thermal resistance chain — `calc.thermal-resistance-chain`

**Status:** recovered from the v0.16.0 release-candidate calculation suite
(the Heat Sink / Thermal Calculator, lost with
`feature/v0.16.0-integration`) and ported into the `WP 21.7A` module
architecture for
`Tempest.Core.Calculations.Modules.ThermalResistanceChainCalculationDefinition`.
Test vectors: `tests/Tempest.Core.Tests/Calculations/Modules/ThermalResistanceChainVectors.cs`.

## Method

**In plain terms.** Heat leaving a chip has to cross a series of
obstacles to reach the air: out of the silicon into the package case,
across the grease or pad into the heat sink, and off the fins into the
room. Each obstacle has a thermal resistance, in kelvin per watt — the
temperature it costs to push one watt through it. Like resistors in
series they simply add, and the temperature rise across the whole chain is
the power times the total, exactly as a voltage drop is current times
resistance. Start at the ambient and climb: the sink, then the case, then
the junction, which must stay below the device's maximum. Turned round,
the limit tells you the largest total resistance — the heat sink budget —
the design can afford.

The steady-state series thermal-resistance model ("thermal Ohm's law",
ΔT = P·R_θ) used for every heat-sink sizing: Mohan, Undeland & Robbins,
*Power Electronics: Converters, Applications and Design*, the chapter on
heat sinks and thermal management; Çengel & Ghajar, *Heat and Mass
Transfer*, chapter 3 (thermal resistance networks, and heat-sink selection
for a power transistor); the junction/case/ambient resistances are those
defined by JEDEC JESD51. The v0.16.0 calculator fixed the chain at three
stages; this module takes any number. Restated, not reproduced.

## Inputs

| Input | Dimension | Limits |
|---|---|---|
| `PowerDissipation` P | Power (W, kW, MW, hp) | ≥ 0 |
| `AmbientTemperature` T_a | Temperature (°C, K, °F, °R) | any |
| `Stages` | list of (name, resistance in K/W, °C/W, K/mW, °F·h/BTU), hottest first | ≥ 1 row; named; each ≥ 0; total > 0 |
| `MaximumSourceTemperature` T_max | Temperature | > T_a |

A row is typed `Junction to case, 0.5 K/W`.

## Formulae

1. Total resistance R = Σ R_i.
2. Stage rise ΔT_i = P · R_i (a temperature difference, in K).
3. Source temperature T_j = T_a + P · R; node i (hot side of stage i) is
   T_j − Σ_{k<i} ΔT_k, so the last stage's cold side is T_a.
4. Margin T_max − T_j; utilisation (T_j − T_a)/(T_max − T_a).
5. Heat sink budget R_max = (T_max − T_a)/P (none when P = 0).
6. Outcome meets criteria when utilisation ≤ 1 (framework slack).

## Outputs and recorded intermediates

Results: outcome, R, each stage's rise, each node temperature, T_j, the
total rise, the margin, R_max, utilisation, criterion met.
Intermediates: each stage's rise, R, the node temperatures, T_j, R_max,
utilisation.

## Limits of applicability

Steady state only: no thermal capacitance, so nothing about warm-up or a
transient overload. One path: all the power through the one chain, no
parallel conduction into the board. Resistances constant with
temperature, and stated rather than derived — for a resistance from a
layer's thickness and conductivity, or a convection film, see
`calc.plane-wall-heat-transfer`. Nothing here is a refusal: an exceeded
limit is a failed criterion.

## Worked examples (hand calculations)

**Example 1 — three-stage chain, 25 W** (the v0.16.0 suite's golden
case). T_a = 40 °C, R_jc = 0.5, R_cs = 0.2, R_sa = 1.8 K/W, T_max = 125 °C.
R = **2.5 K/W**; rises **12.5, 5, 45 K**; heat sink **85 °C**, case
**90 °C**, junction **102.5 °C**; margin **22.5 K**; utilisation
62.5/85 = **0.73529**; budget 85/25 = **3.4 K/W**. Meets criteria.

**Example 2 — heat sink selection for a 60 W transistor** (after Çengel's
transistor heat-sink example). The case must not exceed 90 °C in 30 °C
air: budget (90 − 30)/60 = **1.0 K/W**, so a sink below 1 K/W is needed.
With a commercial sink of **0.9 K/W**: rise **54 K**, case **84 °C**,
margin **6 K**, utilisation **0.9**. Meets criteria.

**Example 3 — 40 W exceeds the limit.** Example 1's chain at 40 W: rises
20, 8, 72 K; nodes **140, 120, 112 °C**; margin **−15 K**; utilisation
**1.1765**; budget **2.125 K/W**. Does not meet criteria.

**Example 4 — exactly on the limit.** 34 W: rise 85 K, junction exactly
**125 °C**, margin 0, utilisation 1. Meets criteria (the framework slack
absorbs the conversion rounding).

Precision: five significant figures; relative tolerance 5 × 10⁻⁵ (half a
unit in the fifth significant figure).

## Edge cases

- **Invalid:** negative power, no stages, an unnamed stage, a negative
  resistance, a zero total resistance, T_max not above T_a.
- **Zero power:** every node at ambient, no budget, meets criteria.
- **Unit mismatch:** Example 1 in kW, kelvin, °C/W, K/mW and °F
  (0.025 kW, 313.15 K, 0.5 °C/W, 0.0002 K/mW, 1.8 K/W, 257 °F) gives the
  same results. Rises are temperature differences in kelvin, never
  temperatures with Celsius's 273.15 offset.
