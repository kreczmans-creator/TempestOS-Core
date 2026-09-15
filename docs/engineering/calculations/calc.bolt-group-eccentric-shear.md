# Bolt group under eccentric in-plane load — `calc.bolt-group-eccentric-shear`

**Status:** specified by `WP 21.7A` (v0.21.0) for
`Tempest.Core.Calculations.Modules.BoltGroupEccentricShearCalculationDefinition`.
Test vectors: `tests/Tempest.Core.Tests/Calculations/Modules/BoltGroupEccentricShearVectors.cs`.

## Method

The elastic (vector) method for a bolt group loaded in its own plane by a
force that does not pass through the group's centroid — Shigley's
*Mechanical Engineering Design*, "shear joints with eccentric loading",
and the "elastic method" of the AISC *Steel Construction Manual*, Part 7.
Every bolt is the same size, the connected plates are rigid, and each
bolt's torsional share is proportional to its distance from the centroid.
The relations below restate the method; no text is reproduced.

## Inputs

| Input | Dimension (typical units) | Limits |
|---|---|---|
| `Bolts` | list of (x, y) positions, Length | ≥ 1 bolt, no two coincident |
| `LoadX`, `LoadY` | Force | not both zero |
| `LoadPointX`, `LoadPointY` | Length | any |
| `AllowableShearPerBolt` | Force | > 0 |

Coordinates and forces share one right-handed in-plane frame; a positive
moment is counter-clockwise.

## Formulae

1. Centroid: x̄ = Σxᵢ / n, ȳ = Σyᵢ / n (equal bolt areas).
2. Moment about the centroid, M = (x_p − x̄)·F_y − (y_p − ȳ)·F_x.
3. Direct shear on every bolt: (F_x / n, F_y / n).
4. Unit polar moment J = Σ [(xᵢ − x̄)² + (yᵢ − ȳ)²] (an area, since the
   bolt area cancels out of the ratio).
5. Torsional shear on bolt i, perpendicular to its radius:
   (−M·(yᵢ − ȳ) / J, +M·(xᵢ − x̄) / J).
6. Resultant on bolt i: the vector sum of 3 and 5; its magnitude is the
   bolt force. The largest is the governing bolt.
7. Utilisation = governing force / allowable shear per bolt; the outcome
   meets criteria when it is ≤ 1 (with the framework's slack).

## Outputs and recorded intermediates

Results: centroid (x̄, ȳ), moment about the centroid (Torque), unit polar
moment (Area), each bolt's resultant (list of Force, in input order), the
governing bolt's index and force, utilisation, outcome. Intermediates:
the centroid, M, J, the direct shear per bolt, the governing bolt.

## Limits of applicability (refusals, not exceptions)

- **A group that cannot resist the moment**: J = 0 (a single bolt, or
  every bolt at one point) with M ≠ 0. Refused with the reason; a single
  bolt with the load through it is an ordinary direct-shear case and is
  computed.

## Worked examples (hand calculations)

**Example 1 — four bolts, vertical load beside the group.**
Bolts at (±75, ±50) mm, F_y = −20 kN at (250, 0) mm, allowable 25 kN.
Centroid (0, 0). M = 0.25 × (−20 000) = **−5 000 N·m** (clockwise).
J = 4 × (75² + 50²) = **32 500 mm²**. Direct shear −5 000 N on each bolt.
Torsional shear on (75, 50): (−(−5×10⁶)(50)/32 500, (−5×10⁶)(75)/32 500)
= (+7 692.3, −11 538.5) N. Resultant (7 692.3, −16 538.5) N →
**18 239.85 N** — the same on (75, −50). On the two bolts at x = −75 the
torsional y-component reverses: (±7 692.3, +6 538.5) → **10 095.70 N**.
Governing 18 239.85 N, utilisation **0.72959**. Meets criteria.

**Example 2 — three bolts in a line, load beside the line.**
Bolts (0, 0), (0, 80), (0, 160) mm; F_y = −30 kN at (120, 80) mm;
allowable 20 kN. Centroid (0, 80). M = 0.12 × (−30 000) = **−3 600 N·m**.
J = 80² + 0 + 80² = **12 800 mm²**. Direct shear −10 000 N each.
Torsional: outer bolts ∓22 500 N in x (the end bolts), middle bolt zero.
Resultants: outer √(22 500² + 10 000²) = **24 622.14 N**, middle 10 000 N.
Utilisation **1.2311**. Does not meet criteria.

**Example 3 — Example 2 with an inclined load.** F_x = +15 kN, F_y = −30 kN
at (120, 80); allowable 40 kN. The x-component passes through the
centroid's height, so M is still **−3 600 N·m**. Direct shear
(5 000, −10 000) N each. Bolt (0, 160): (5 000 + 22 500, −10 000) →
**29 261.75 N**; bolt (0, 0): (5 000 − 22 500, −10 000) → 20 155.64 N;
middle 11 180.34 N. Utilisation **0.73154**. Meets criteria.

**Example 4 — load through the centroid.** Example 1's group with the load
at (0, 0): M = 0, every bolt carries exactly **5 000 N**.

Precision: five significant figures; relative tolerance 1 × 10⁻⁵.

## Edge cases

- **Zero or negative:** no bolts, a non-positive allowable, or a load with
  both components zero → invalid input. Two bolts at the same point →
  invalid input.
- **Out of range:** one bolt with the load 100 mm from it → refused (J = 0
  with M ≠ 0).
- **Unit mismatch:** Example 1 restated in inches and lbf gives the same
  base-unit results.
