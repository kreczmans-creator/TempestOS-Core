# Beam bending and deflection — `calc.beam-deflection`

**Status:** specified by `WP 21.7A` (v0.21.0) for
`Tempest.Core.Calculations.Modules.BeamDeflectionCalculationDefinition`.
Test vectors: `tests/Tempest.Core.Tests/Calculations/Modules/BeamDeflectionVectors.cs`.

## Method

**In plain terms.** A beam carries a load by bending. The load makes it
curve; the curvature stretches one face and squeezes the other, and the
stress at the faces is the bending moment divided by the section's
resistance to bending, I over c. How far the beam sags depends on the load,
the cube of the span, and the product of the material's stiffness E and the
section's I: doubling the span gives eight times the deflection. This
module answers the two questions a designer asks of a simple beam: is the
material overstressed, and does it sag too much?

Euler–Bernoulli beam theory: a linear-elastic prismatic beam whose plane
sections stay plane, with shear deformation neglected. The four closed-form
cases are the ones every strength-of-materials text tabulates (for example
Roark's *Formulas for Stress and Strain*, Table 8.1, or Gere & Goodno,
*Mechanics of Materials*, Appendix G). No text is reproduced; the formulae
below are the standard results restated.

Four cases, selected by two inputs:

| Support | Loading | Where the load acts | Maximum moment | Maximum deflection |
|---|---|---|---|---|
| Simply supported | Point load W | midspan | W·L / 4 | W·L³ / (48·E·I) |
| Simply supported | Uniformly distributed, total W | over the whole span | W·L / 8 | 5·W·L³ / (384·E·I) |
| Cantilever | Point load W | free end | W·L | W·L³ / (3·E·I) |
| Cantilever | Uniformly distributed, total W | over the whole span | W·L / 2 | W·L³ / (8·E·I) |

The distributed load is entered as its **total** W (= w·L), so every case
takes a force and a length; the familiar w·L²/8 and w·L²/2 forms are the
same expressions with w = W/L substituted.

## Inputs

| Input | Dimension (typical units) | Limits |
|---|---|---|
| `Support` | `SimplySupported` or `Cantilever` | — |
| `Loading` | `PointLoad` or `UniformlyDistributed` | — |
| `Load` W | Force (N, kN, lbf) | > 0 |
| `Span` L | Length (mm, m, in) | > 0 |
| `YoungsModulus` E | Pressure (GPa, MPa, psi) | > 0; from the material record where one is used |
| `SecondMomentOfArea` I | Second moment of area (mm⁴, m⁴, in⁴) | > 0 |
| `ExtremeFibreDistance` c | Length | > 0; neutral axis to the most stressed fibre |
| `AllowableBendingStress` | Pressure | > 0 |
| `DeflectionLimit` | Length | > 0 (for example span/250) |

## Formulae

1. M_max from the table above.
2. δ_max from the table above.
3. σ_max = M_max · c / I (elastic bending stress at the extreme fibre).
4. Stress utilisation = σ_max / allowable; deflection utilisation = δ_max / limit.
5. Outcome: meets criteria when both utilisations are ≤ 1 (with the
   framework's one-part-in-a-billion slack, as the bracket check uses).

## Outputs and recorded intermediates

Results: maximum moment (Torque), maximum bending stress (Pressure),
maximum deflection (Length), the two utilisations, the two criterion flags
and the outcome. Intermediates recorded through the context: the case
selected, the moment, the span-to-depth ratio, both utilisations.

## Limits of applicability (refusals, not exceptions)

- **Span-to-depth ratio.** With the section depth taken as 2·c (a doubly
  symmetric section), the calculation refuses when L / (2·c) < 10: below
  that, shear deflection is no longer negligible against bending
  deflection and Euler–Bernoulli under-predicts it. The result reports the
  refusal and its reason; no figures are computed.
- The result is elastic. Exceeding the allowable is reported as a failed
  criterion, not a refusal.

## Worked examples (hand calculations)

**Example 1 — simply supported, central point load.**
W = 10 kN, L = 2 m, E = 210 GPa, I = 2.0 × 10⁶ mm⁴, c = 50 mm,
allowable 165 MPa, limit L/250 = 8 mm.
M = 10 000 × 2 / 4 = **5 000 N·m**.
σ = 5 000 × 0.05 / 2.0 × 10⁻⁶ = **125.00 MPa** (utilisation 0.7576).
δ = 10 000 × 2³ / (48 × 210 × 10⁹ × 2.0 × 10⁻⁶) = 80 000 / 2.016 × 10⁷
= **3.9683 mm** (utilisation 0.4960). Meets criteria.

**Example 2 — cantilever, uniformly distributed load.**
W = 6 kN total, L = 1.5 m, E = 70 GPa, I = 1.0 × 10⁶ mm⁴, c = 40 mm,
allowable 150 MPa, limit 20 mm.
M = 6 000 × 1.5 / 2 = **4 500 N·m**. σ = 4 500 × 0.04 / 10⁻⁶ = **180.00 MPa**
(utilisation 1.2 — stress criterion not met).
δ = 6 000 × 1.5³ / (8 × 70 × 10⁹ × 10⁻⁶) = 20 250 / 5.6 × 10⁵ = **36.161 mm**
(utilisation 1.808 — deflection criterion not met). Does not meet criteria.

**Example 3 — simply supported, uniformly distributed load** (IPE 200-like
section). W = 20 kN, L = 4 m, E = 210 GPa, I = 1.943 × 10⁷ mm⁴, c = 100 mm,
allowable 235 MPa, limit 16 mm.
M = 20 000 × 4 / 8 = **10 000 N·m**. σ = 10 000 × 0.1 / 1.943 × 10⁻⁵ =
**51.467 MPa**. δ = 5 × 20 000 × 64 / (384 × 210 × 10⁹ × 1.943 × 10⁻⁵) =
**4.0847 mm**. Meets criteria.

**Example 4 — cantilever, end point load.** W = 2 kN, L = 500 mm,
E = 200 GPa, I = 5.0 × 10⁴ mm⁴, c = 12.5 mm, allowable 250 MPa, limit 10 mm.
M = 2 000 × 0.5 = **1 000 N·m**. σ = 1 000 × 0.0125 / 5 × 10⁻⁸ =
**250.00 MPa** (utilisation exactly 1 — met, on the limit). δ = 2 000 × 0.125 /
(3 × 200 × 10⁹ × 5 × 10⁻⁸) = 250 / 30 000 = **8.3333 mm**. Meets criteria.

Precision: every expected figure above is stated to five significant
figures; the vectors compare to a relative tolerance of 5 × 10⁻⁵ (half a unit in the fifth significant figure).

## Edge cases

- **Zero or negative** load, span, E, I, c, allowable or limit: invalid
  input (`CalculationInputInvalidException`), recorded as a failed
  constraint check first.
- **Out of range:** L / (2c) < 10 → refused (see above); Example 1's
  geometry with L = 200 mm is the vector.
- **Unit mismatch:** a length cannot be passed where a force belongs — the
  dimension is a compile-time type. Within a dimension, Example 1
  restated in inches, lbf and psi gives the same base-unit results
  (vector `Example 1 in imperial units`); the CsCheck property proves the
  same for random units.
