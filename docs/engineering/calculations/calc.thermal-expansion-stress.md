# Thermal expansion and restrained thermal stress — `calc.thermal-expansion-stress`

**Status:** specified by `WP 21.7A` (v0.21.0) for
`Tempest.Core.Calculations.Modules.ThermalExpansionStressCalculationDefinition`.
Test vectors: `tests/Tempest.Core.Tests/Calculations/Modules/ThermalExpansionStressVectors.cs`.

## Method

Linear thermal expansion of a prismatic bar and the axial stress that
develops when its restraints stop it moving — the compatibility problem
every mechanics-of-materials text treats under "thermal stress"
(Hibbeler, *Mechanics of Materials*, axial load chapter; Gere & Goodno,
thermal effects). The bar and its restraint are two springs in series:
the free thermal movement, less any clearance that must close first, is
shared between straining the bar and deflecting the restraint. A rigid
restraint is the limiting case. Restated, not reproduced.

## Inputs

| Input | Dimension | Limits |
|---|---|---|
| `Length` L | Length | > 0 |
| `Area` A | Area | > 0 |
| `YoungsModulus` E | Pressure | > 0; from the material record |
| `ExpansionCoefficient` α | Thermal expansion (1/K, µm/(m·K), µin/(in·°F)) | > 0; from the material record |
| `TemperatureChange` ΔT | Temperature delta (K, Δ°C, Δ°F) | any sign |
| `Gap` g | Length | ≥ 0; clearance closed before the restraint engages, in either direction |
| `RestraintStiffness` k_s | Stiffness, or none | > 0 when given; none means rigid |
| `AllowableStress` | Pressure | > 0 |

## Formulae

1. Free movement δ_free = α · L · ΔT (signed: positive for heating).
2. Engaged movement δ_e = sign(δ_free) · max(|δ_free| − g, 0).
3. Bar axial stiffness k_bar = E·A / L.
4. Restraint force F = δ_e · k_bar (rigid) or δ_e / (1/k_bar + 1/k_s)
   (elastic restraint) — positive means the restraint pushes back on a
   bar that wants to expand.
5. Stress σ = −F / A (negative compressive on heating, positive tensile on
   cooling).
6. Actual movement δ = δ_free − F / k_bar.
7. Utilisation = |σ| / allowable; outcome meets criteria when ≤ 1.

## Outputs and recorded intermediates

Results: δ_free, δ_e, k_bar, F, σ, δ, utilisation, outcome.
Intermediates: δ_free, the gap consumed, k_bar, k_s or "rigid".

## Limits of applicability

The coefficient is taken as constant over ΔT and the bar stays elastic;
neither is a refusal the module can decide, so it has none. A stress above
the allowable is a failed criterion.

## Worked examples (hand calculations)

Steel bar throughout: L = 2 m, A = 500 mm², E = 200 GPa, α = 12 × 10⁻⁶ /K,
allowable 150 MPa. k_bar = 200 000 × 500 / 2 000 = 50 000 N/mm.

**Example 1 — rigid walls, no gap, +60 K.** δ_free = 12 × 10⁻⁶ × 2 000 × 60
= **1.4400 mm**. F = 1.44 × 50 000 = **72 000 N**. σ = **−144.00 MPa**
(utilisation 0.96). δ = **0**. Meets criteria.

**Example 2 — rigid walls, 0.5 mm gap, +60 K.** δ_e = 0.94 mm.
F = **47 000 N**, σ = **−94.000 MPa**, δ = **0.50000 mm**. Meets criteria.

**Example 3 — elastic restraint 100 kN/mm, no gap, +60 K.**
F = 1.44 / (1/50 000 + 1/100 000) = 1.44 / 3 × 10⁻⁵ = **48 000 N**.
σ = **−96.000 MPa**. δ = 1.44 − 48 000 / 50 000 = **0.48000 mm**
(= F / k_s). Meets criteria.

**Example 4 — cooling −40 K, rigid, no gap.** δ_free = **−0.96000 mm**.
F = **−48 000 N** (the restraint pulls), σ = **+96.000 MPa** tensile,
δ = **0**. Meets criteria.

**Example 5 — +10 K against a 0.5 mm gap.** δ_free = 0.24 mm < gap:
F = **0**, σ = **0**, δ = **0.24000 mm**.

Precision: five significant figures; relative tolerance 5 × 10⁻⁵ (half a unit in the fifth significant figure).

## Edge cases

- **Zero or negative:** L, A, E, α or allowable ≤ 0, g < 0, k_s ≤ 0 →
  invalid input. ΔT = 0 computes zero everywhere.
- **Out of range:** none — see the limits section.
- **Unit mismatch:** Example 1 in inches, ft², psi, µin/(in·°F) and Δ°F
  (α = 6.6667 µin/(in·°F), ΔT = 108 Δ°F) gives the same base-unit results.
