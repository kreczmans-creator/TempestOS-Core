# Column buckling, Euler with the Perry-Robertson correction — `calc.column-buckling`

**Status:** specified by `WP 21.7A` (v0.21.0) for
`Tempest.Core.Calculations.Modules.ColumnBucklingCalculationDefinition`.
Test vectors: `tests/Tempest.Core.Tests/Calculations/Modules/ColumnBucklingVectors.cs`.

## Method

**In plain terms.** A slender strut fails not by crushing but by bowing
sideways at Euler's critical load, which falls with the square of its
slenderness (length over radius of gyration). A real strut is never
perfectly straight, so it bows a little from the start and reaches yield
on its concave face before the Euler load. Perry's formula puts that
initial bow into the yield condition, and Robertson chose the size of the
bow to fit tests, giving the strut curves: a stocky strut reaches its
yield strength, a slender one tends to Euler's value, and the curve
bridges the two. The Robertson constant selects the curve for the section
shape.

Euler's elastic critical stress for a pin-ended strut of effective length
L_E, corrected for initial imperfection by the Perry-Robertson formula in
the form BS 5950-1:2000 gives in Annex C (the basis of its strut curves,
Table 24, with the Robertson constant of Table 23 selecting the curve).
The Perry factor grows linearly with slenderness beyond a limiting
slenderness, so a stocky strut reaches its yield stress and a slender one
tends to the Euler stress. The formulae are restated, not reproduced.

## Inputs

| Input | Dimension | Limits |
|---|---|---|
| `EffectiveLength` L_E | Length | > 0 |
| `Area` A | Area | > 0 |
| `SecondMomentOfArea` I (about the weaker axis) | Second moment of area | > 0 |
| `YoungsModulus` E | Pressure | > 0 (205 GPa for steel in BS 5950) |
| `YieldStrength` p_y | Pressure | > 0; from the material record |
| `RobertsonConstant` a | — | > 0 (2.0 curve a, 3.5 curve b, 5.5 curve c, 8.0 curve d) |
| `AppliedLoad` P | Force | ≥ 0 |

## Formulae

1. Radius of gyration r = √(I / A); slenderness λ = L_E / r.
2. Euler stress p_E = π²·E / λ²; Euler load P_E = p_E·A.
3. Limiting slenderness λ_0 = 0.2·√(π²·E / p_y).
4. Perry factor η = 0.001·a·(λ − λ_0), not less than zero.
5. φ = (p_y + (η + 1)·p_E) / 2.
6. Compressive strength p_c = p_E·p_y / (φ + √(φ² − p_E·p_y)).
7. Compression resistance P_c = p_c·A; utilisation = P / P_c. Outcome
   meets criteria when utilisation ≤ 1 (framework slack).

For λ ≤ λ_0 the formula reduces to p_c = min(p_y, p_E), which is what a
stocky strut should give.

## Outputs and recorded intermediates

Results: r, λ, p_E, P_E, λ_0, η, p_c, P_c, utilisation, outcome.
Intermediates: r, λ, λ_0, η, φ, p_E.

## Limits of applicability (refusals, not exceptions)

- **Slenderness above 180** (BS 5950-1 clause 4.7.3.2, members resisting
  loads other than wind): refused, with λ in the reason.

## Worked examples (hand calculations)

E = 205 GPa throughout.

**Example 1 — curve c, intermediate slenderness.** p_y = 275 MPa, a = 5.5,
L_E = 3 m, A = 2 000 mm², I = 2.0 × 10⁶ mm⁴, P = 150 kN.
r = √1 000 = **31.623 mm**; λ = **94.868**. p_E = π² × 205 000 / 9 000 =
**224.81 MPa**; P_E = 449.62 kN. λ_0 = 0.2 × √(2 023 269 / 275) = **17.155**.
η = 0.0055 × 77.713 = **0.42742**. φ = (275 + 1.42742 × 224.81) / 2 =
**297.95**. p_c = 224.81 × 275 / (297.95 + √(297.95² − 61 822)) =
61 822 / 462.12 = **133.78 MPa**. P_c = **267.56 kN**; utilisation
**0.56062**. Meets criteria.

**Example 2 — slender, curve b, S355.** p_y = 355 MPa, a = 3.5,
L_E = 1.5 m, A = 1 200 mm², I = 1.2 × 10⁵ mm⁴, P = 100 kN.
r = **10.000 mm**; λ = **150.00**. p_E = 2 023 269 / 22 500 = **89.923 MPa**.
λ_0 = **15.099**. η = 0.0035 × 134.90 = **0.47215**. φ = **243.69**.
p_c = **77.973 MPa**; P_c = **93.567 kN**; utilisation **1.0688**.
Does not meet criteria.

**Example 3 — stocky, curve c.** Example 1's section with L_E = 1 m,
P = 300 kN. λ = **31.623**; p_E = **2 023.3 MPa**; η = 0.0055 × 14.468 =
**0.079573**; φ = **1 229.6**; p_c = **252.09 MPa**; P_c = **504.17 kN**;
utilisation **0.59504**. Meets criteria.

Precision: five significant figures; relative tolerance 5 × 10⁻⁵ (half a unit in the fifth significant figure).

## Edge cases

- **Zero or negative:** non-positive length, area, I, E, p_y or Robertson
  constant → invalid; negative applied load → invalid (P = 0 is accepted,
  utilisation 0).
- **Out of range:** Example 1's section at L_E = 5.75 m gives λ = 181.83 →
  refused.
- **Unit mismatch:** Example 1 in inches, in⁴, ft², psi and lbf gives the
  same base-unit results.
