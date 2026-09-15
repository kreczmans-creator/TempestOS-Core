# Thick-walled cylinder stresses (Lamé) — `calc.thick-walled-cylinder`

**Status:** specified by `WP 21.7A` (v0.21.0) for
`Tempest.Core.Calculations.Modules.ThickWalledCylinderCalculationDefinition`.
Sits beside `calc.pressure-vessel-wall-thickness`, the thin-wall check,
and reports the thin-wall hoop estimate for comparison.
Test vectors: `tests/Tempest.Core.Tests/Calculations/Modules/ThickWalledCylinderVectors.cs`.

## Method

Lamé's solution for a thick-walled cylinder under internal and external
pressure (Timoshenko, *Strength of Materials* Part II; Roark's *Formulas
for Stress and Strain*, thick-walled vessels): radial and hoop stresses
are the sum of a constant term and a term in 1/r², with the constants
fixed by the pressures at the two surfaces. For closed ends the axial
stress is the constant term; for open ends it is zero. The principal
stresses at the bore and at the outer surface are combined into von Mises
and Tresca equivalents. Restated, not reproduced.

## Inputs

| Input | Dimension | Limits |
|---|---|---|
| `InnerRadius` a, `OuterRadius` b | Length | 0 < a < b |
| `InternalPressure` p_i, `ExternalPressure` p_o | Pressure | ≥ 0 (gauge); not both zero |
| `ClosedEnds` | boolean | — |
| `AllowableStress` | Pressure | > 0; compared with the von Mises stress |

## Formulae

With A = (a²·p_i − b²·p_o) / (b² − a²) and B = a²·b²·(p_i − p_o) / (b² − a²):

1. σ_r(r) = A − B / r²; σ_θ(r) = A + B / r².
2. σ_z = A (closed ends) or 0 (open ends).
3. At r = a and r = b: von Mises
   σ' = √(½·[(σ_θ − σ_r)² + (σ_r − σ_z)² + (σ_z − σ_θ)²]);
   Tresca = the largest absolute difference between two principal stresses.
4. Maximum von Mises = the larger of the two surfaces; utilisation =
   maximum / allowable; outcome meets criteria when ≤ 1 (framework slack).
5. Thin-wall hoop estimate for comparison: (p_i − p_o) · (a + b)/2 / (b − a).

## Outputs and recorded intermediates

Results: σ_r, σ_θ at both surfaces, σ_z, von Mises and Tresca at both
surfaces, the maximum von Mises and where it occurs, utilisation, the
thin-wall estimate and the ratio of Lamé's maximum hoop stress to it,
outcome. Intermediates: A, B, the wall ratio b/a.

## Limits of applicability

Lamé's solution is exact for any wall ratio within linear elasticity, so
this module has no applicability refusal; a von Mises stress above the
allowable is a failed criterion. Negative gauge pressures (vacuum) are
outside the inputs accepted.

## Worked examples (hand calculations)

**Example 1 — internal pressure, closed ends.** a = 50 mm, b = 100 mm,
p_i = 50 MPa, p_o = 0, allowable 200 MPa. b² − a² = 7 500 mm².
A = 2 500 × 50 / 7 500 = 16.667 MPa; B = 2 500 × 10 000 × 50 / 7 500 =
166 667 MPa·mm².
Bore: σ_r = **−50.000**, σ_θ = 16.667 + 66.667 = **83.333**, σ_z = **16.667 MPa**;
σ' = √(½·[133.33² + 66.667² + 66.667²]) = **115.47 MPa**; Tresca **133.33 MPa**.
Outer: σ_r = **0**, σ_θ = **33.333**; σ' = **28.868 MPa**.
Maximum von Mises 115.47 MPa at the bore; utilisation **0.57735**.
Thin-wall estimate 50 × 75 / 50 = **75.000 MPa**; Lamé/thin-wall **1.1111**.
Meets criteria.

**Example 2 — external pressure, closed ends.** a = 40 mm, b = 60 mm,
p_i = 0, p_o = 20 MPa, allowable 100 MPa. b² − a² = 2 000 mm².
A = −3 600 × 20 / 2 000 = −36 MPa; B = 1 600 × 3 600 × (−20) / 2 000 =
−57 600 MPa·mm².
Bore: σ_r = **0**, σ_θ = **−72.000**, σ_z = **−36.000 MPa**; σ' = **62.354 MPa**;
Tresca **72.000 MPa**. Outer: σ_r = **−20.000**, σ_θ = **−52.000 MPa**;
σ' = **27.713 MPa**. Utilisation **0.62354**. Thin-wall estimate
−20 × 50 / 20 = **−50.000 MPa**. Meets criteria.

**Example 3 — Example 1 with open ends.** σ_z = 0: bore
σ' = √(½·[133.33² + 50² + 83.333²]) = **116.67 MPa**; utilisation **0.58333**.

Precision: five significant figures; relative tolerance 1 × 10⁻⁵.

## Edge cases

- **Zero or negative:** a ≤ 0, b ≤ a, a negative pressure, both pressures
  zero, or a non-positive allowable → invalid input.
- **Out of range:** none — see the limits section.
- **Unit mismatch:** Example 1 in inches, psi and bar gives the same
  base-unit results.
