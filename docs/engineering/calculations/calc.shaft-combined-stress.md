# Shaft under combined torsion and bending — `calc.shaft-combined-stress`

**Status:** specified by `WP 21.7A` (v0.21.0) for
`Tempest.Core.Calculations.Modules.ShaftCombinedStressCalculationDefinition`.
Test vectors: `tests/Tempest.Core.Tests/Calculations/Modules/ShaftCombinedStressVectors.cs`.

## Method

Static strength of a solid circular shaft carrying a bending moment and a
torque at the same section: the elastic bending and torsional stresses of
a round bar, combined by the maximum-shear-stress (Tresca) and the
distortion-energy (von Mises) theories, as in Shigley's *Mechanical
Engineering Design* (shaft design for static loading, and the failure
theories of the chapter on static failure). Stress concentration is
applied through caller-supplied factors on each stress component.
Restated, not reproduced.

## Inputs

| Input | Dimension | Limits |
|---|---|---|
| `Diameter` d | Length | > 0 |
| `BendingMoment` M | Torque | ≥ 0 (magnitude) |
| `Torque` T | Torque | ≥ 0 (magnitude); M and T not both zero |
| `YieldStrength` S_y | Pressure | > 0; from the material record |
| `BendingStressConcentrationFactor` K_t | — | ≥ 1 (1 for none) |
| `TorsionalStressConcentrationFactor` K_ts | — | ≥ 1 |
| `RequiredSafetyFactor` n_req | — | ≥ 1 |

## Formulae

1. Bending stress σ = K_t · 32·M / (π·d³).
2. Torsional shear τ = K_ts · 16·T / (π·d³).
3. Maximum shear stress τ_max = √((σ/2)² + τ²) (Tresca).
4. Von Mises stress σ' = √(σ² + 3·τ²).
5. Factors of safety: n_T = (S_y / 2) / τ_max; n_VM = S_y / σ'.
6. Outcome meets criteria when min(n_T, n_VM) ≥ n_req (framework slack).

## Outputs and recorded intermediates

Results: σ, τ, τ_max, σ', n_T, n_VM, the governing (lower) factor, outcome.
Intermediates: π·d³, the two stress components, the two factors.

## Limits of applicability

This closed-form method has no applicability limit of its own beyond
linear elasticity in a solid round section: a factor of safety below the
required one is a failed criterion, not a refusal. Hollow shafts are not
covered (no inner diameter input); fatigue is `calc.fatigue-miner`'s job.

## Worked examples (hand calculations)

**Example 1.** d = 40 mm, M = 500 N·m, T = 800 N·m, S_y = 350 MPa,
K_t = K_ts = 1, n_req = 2. π·d³ = 201 061.9 mm³.
σ = 16 × 10⁶ / 201 061.9 = **79.577 MPa**; τ = 12.8 × 10⁶ / 201 061.9 =
**63.662 MPa**. τ_max = √(39.789² + 63.662²) = **75.073 MPa**.
σ' = √(79.577² + 3 × 63.662²) = **135.98 MPa**. n_T = 175 / 75.073 =
**2.3311**; n_VM = 350 / 135.98 = **2.5739**. Meets criteria.

**Example 2.** d = 25 mm, M = 120 N·m, T = 60 N·m, S_y = 250 MPa,
K_t = K_ts = 1, n_req = 3. π·d³ = 49 087.39 mm³.
σ = **78.228 MPa**; τ = **19.557 MPa**; τ_max = **43.731 MPa**;
σ' = **85.247 MPa**; n_T = **2.8584**; n_VM = **2.9327**. Does not meet
criteria (2.8584 < 3).

**Example 3 — Example 2 with a shoulder fillet.** K_t = 1.6, K_ts = 1.3,
n_req = 1.5. σ = **125.16 MPa**; τ = **25.424 MPa**; τ_max = **67.549 MPa**;
σ' = **132.69 MPa**; n_T = **1.8505**; n_VM = **1.8842**. Meets criteria.

Precision: five significant figures; relative tolerance 5 × 10⁻⁵ (half a unit in the fifth significant figure).

## Edge cases

- **Zero or negative:** d ≤ 0, S_y ≤ 0, a negative moment or torque, a
  concentration factor or required factor below 1, or M = T = 0 (nothing
  to check) → invalid input.
- **Out of range:** none — see the limits section.
- **Unit mismatch:** Example 1 in inches, lbf·in and psi gives the same
  base-unit results.
