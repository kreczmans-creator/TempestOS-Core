# Bolted joint preload and clamp force — `calc.bolted-joint-preload`

**Status:** specified by `WP 21.7A` (v0.21.0) for
`Tempest.Core.Calculations.Modules.BoltedJointPreloadCalculationDefinition`.
Test vectors: `tests/Tempest.Core.Tests/Calculations/Modules/BoltedJointPreloadVectors.cs`.

## Method

The joint-diagram method for a preloaded bolted joint in tension, as set
out in Shigley's *Mechanical Engineering Design* (the chapter on
non-permanent joints, "tension joints — the external load") and, in its
fuller form, VDI 2230. The bolt and the clamped members are two springs in
parallel sharing an external tensile load in proportion to their
stiffnesses; the preload keeps the members in compression until the load
reaches the separation load. Nothing from either text is reproduced; the
relations below are the standard ones restated.

## Inputs

| Input | Dimension (typical units) | Limits |
|---|---|---|
| `Preload` F_i | Force | > 0 and < proof load S_p·A_t |
| `ExternalLoad` P | Force | ≥ 0 (tensile; a compressive external load is outside this method) |
| `BoltStiffness` k_b | Stiffness (N/mm, kN/mm, lbf/in) | > 0 |
| `MemberStiffness` k_m | Stiffness | > 0 |
| `TensileStressArea` A_t | Area (mm², ft²) | > 0 |
| `ProofStrength` S_p | Pressure (MPa, psi) | > 0; from the fastener grade (600 MPa for class 8.8, 830 MPa for class 10.9) |

## Formulae

1. Joint constant C = k_b / (k_b + k_m): the fraction of P the bolt sees.
2. Bolt load F_b = F_i + C·P while the joint is clamped.
3. Member (clamp) load F_m = F_i − (1 − C)·P; positive means the members
   are still in compression.
4. Separation load P_0 = F_i / (1 − C): the external load at which F_m = 0.
5. Load factor against separation n_0 = P_0 / P.
6. Bolt stress σ_b = F_b / A_t.
7. Load factor against bolt yielding n_L = (S_p·A_t − F_i) / (C·P): the
   multiple of P at which the bolt reaches proof stress.
8. **Separated joint** (P > P_0): the members carry nothing and the bolt
   carries all of P, so F_b = P and F_m = 0 are reported, with
   `IsSeparated` set; n_0 < 1 and the outcome does not meet criteria.
9. Outcome meets criteria when the joint is not separated and σ_b ≤ S_p.

## Outputs and recorded intermediates

Results: joint constant, bolt load, clamp load, separation load, both
load factors (null when P = 0, never infinity; n_L is also null once the joint has separated, the joint-diagram relation no longer holding), bolt stress,
`IsSeparated`, outcome. Intermediates: joint constant, proof load S_p·A_t,
preload as a fraction of proof load.

## Limits of applicability (refusals, not exceptions)

- **Preload at or above proof load** (F_i ≥ S_p·A_t): the bolt yields at
  assembly and the joint diagram no longer applies. Refused, with the
  preload-to-proof ratio in the reason.

## Worked examples (hand calculations)

**Example 1 — M12 class 8.8, preload 75 % of proof.**
A_t = 84.3 mm², S_p = 600 MPa, F_i = 0.75 × 84.3 × 600 = 37 935 N,
k_b = 200 kN/mm, k_m = 600 kN/mm, P = 20 kN.
C = 200 / 800 = **0.25**. F_b = 37 935 + 0.25 × 20 000 = **42 935 N**.
F_m = 37 935 − 0.75 × 20 000 = **22 935 N**. P_0 = 37 935 / 0.75 =
**50 580 N**, n_0 = **2.5290**. σ_b = 42 935 / 84.3 = **509.31 MPa**.
n_L = (600 × 84.3 − 37 935) / (0.25 × 20 000) = 12 645 / 5 000 = **2.5290**
(equal to n_0 here only because the preload is 75 % of proof and C = 0.25).
Meets criteria.

**Example 2 — M16 class 10.9.** A_t = 157 mm², S_p = 830 MPa,
F_i = 90 000 N, k_b = 500 kN/mm, k_m = 2 000 kN/mm, P = 40 kN.
C = **0.20**. F_b = **98 000 N**. F_m = 90 000 − 0.8 × 40 000 = **58 000 N**.
P_0 = 90 000 / 0.8 = **112 500 N**, n_0 = **2.8125**. σ_b = 98 000 / 157 =
**624.20 MPa**. n_L = (130 310 − 90 000) / 8 000 = **5.0388**. Meets criteria.

**Example 3 — a joint that separates.** A_t = 84.3 mm², S_p = 600 MPa,
F_i = 10 000 N, k_b = 3 kN/mm, k_m = 7 kN/mm (C = 0.3), P = 20 kN.
F_m (clamped formula) = 10 000 − 0.7 × 20 000 = −4 000 N < 0, so the joint
has separated: P_0 = 10 000 / 0.7 = **14 285.7 N**, n_0 = **0.71429**.
Reported: F_b = P = **20 000 N**, F_m = **0**, σ_b = 20 000 / 84.3 =
**237.25 MPa**, `IsSeparated` = true. Does not meet criteria.

Precision: five significant figures; relative tolerance 5 × 10⁻⁵ (half a unit in the fifth significant figure).

## Edge cases

- **Zero or negative** preload, stiffnesses, area or proof strength:
  invalid input. Negative external load: invalid input (compression is
  outside the method).
- **Out of range:** F_i ≥ S_p·A_t → refused (vectors: F_i = 60 000 N and
  F_i = 50 580 N on Example 1's M12, whose proof load is 50 580 N).
- **Unit mismatch:** dimensions are compile-time types. Example 1 restated
  in lbf, lbf/in, ft² and psi gives the same base-unit results.
