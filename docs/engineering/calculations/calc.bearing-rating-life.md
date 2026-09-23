# Rolling bearing basic rating life L10 — `calc.bearing-rating-life`

**Status:** specified by `WP 21.7A` (v0.21.0) for
`Tempest.Core.Calculations.Modules.BearingRatingLifeCalculationDefinition`.
Test vectors: `tests/Tempest.Core.Tests/Calculations/Modules/BearingRatingLifeVectors.cs`.

## Method

**In plain terms.** A rolling bearing does not wear out; it fails by
fatigue of its rolling surfaces after a number of revolutions that varies
widely from one bearing to the next. ISO 281 therefore states the life
that 90 % of a batch will reach, the L10 life: the load rating over the
load, cubed for balls and to the power 10/3 for rollers, in millions of
revolutions. Halving the load gives eight times the life. Speed converts
revolutions to hours, and a reliability factor shortens the figure when
more than 90 % must survive.

The basic rating life of ISO 281 (rolling bearings — dynamic load ratings
and rating life): the life that 90 % of a group of identical bearings
reach, as the basic dynamic load rating over the equivalent dynamic load
raised to the life exponent — 3 for ball bearings, 10/3 for roller
bearings — in millions of revolutions, converted to hours at the running
speed. The equivalent dynamic load is the catalogue's radial and axial
factors applied to the radial and axial loads. The reliability factor
a₁ of ISO 281 scales the life for reliabilities above 90 % and is a
caller input (ISO 281 tabulates it: 1 at 90 %, 0.64 at 95 %, 0.55 at
96 %, 0.47 at 97 %, 0.37 at 98 %, 0.25 at 99 %). The life modification
factor a_ISO (lubrication, contamination, fatigue load limit) is **not**
included; the result is the basic or a₁-modified life only. Restated,
not reproduced.

## Inputs

| Input | Dimension | Limits |
|---|---|---|
| `BearingType` | `Ball` or `Roller` | selects the exponent |
| `BasicDynamicLoadRating` C | Force | > 0; from the bearing record |
| `RadialLoad` F_r, `AxialLoad` F_a | Force | ≥ 0 |
| `RadialFactor` X, `AxialFactor` Y | — | ≥ 0; from the bearing catalogue |
| `Speed` n | Rotational speed | > 0 |
| `ReliabilityFactor` a₁ | — | 0 < a₁ ≤ 1 |
| `RequiredLife` | Duration | ≥ 0 (0 = no criterion) |

## Formulae

1. Equivalent dynamic load P = X·F_r + Y·F_a.
2. Load ratio P / C (must not exceed 0.5 — method limit).
3. L₁₀ = (C / P)^p million revolutions, p = 3 (ball) or 10/3 (roller).
4. L₁₀h = L₁₀ × 10⁶ / (60 · n) with n in revolutions per minute.
5. L_nm = a₁ · L₁₀; L_nmh = a₁ · L₁₀h.
6. Outcome meets criteria when L_nmh ≥ required life (or no criterion).

## Outputs and recorded intermediates

Results: P, P/C, L₁₀ (million revolutions), L₁₀h (Duration), L_nm, L_nmh,
outcome. Intermediates: the exponent, P, the load ratio, a₁.

## Limits of applicability (refusals, not exceptions)

- **P > 0.5·C**: ISO 281's basic rating life equation is stated for
  equivalent loads up to half the dynamic load rating; beyond it the
  module refuses rather than extrapolate.

## Worked examples (hand calculations)

**Example 1 — deep-groove ball bearing, pure radial load.** C = 35.1 kN,
F_r = 4 kN, X = 1, Y = 0, n = 1 500 r/min, a₁ = 1, required 5 000 h.
P = **4 000 N**, P/C = **0.11396**. L₁₀ = 8.775³ = **675.68** million rev.
L₁₀h = 675.68 × 10⁶ / 90 000 = **7 507.6 h**. Meets criteria.

**Example 2 — cylindrical roller bearing.** C = 78 kN, F_r = 12 kN,
X = 1, Y = 0, n = 3 000 r/min, a₁ = 1, required 4 000 h.
P = **12 000 N**, P/C = **0.15385**. L₁₀ = 6.5^(10/3) = **512.52** million
rev. L₁₀h = 512.52 × 10⁶ / 180 000 = **2 847.3 h**. Does not meet criteria.

**Example 3 — ball bearing with combined load at 95 % reliability.**
C = 25 kN, F_r = 3 kN, F_a = 1.5 kN, X = 0.56, Y = 1.5, n = 1 000 r/min,
a₁ = 0.64, required 2 000 h. P = 1 680 + 2 250 = **3 930 N**,
P/C = **0.15720**. L₁₀ = (25 000 / 3 930)³ = **257.42** million rev;
L₁₀h = **4 290.3 h**. L₅ = **164.75** million rev; L₅h = **2 745.8 h**.
Meets criteria.

Precision: five significant figures; relative tolerance 5 × 10⁻⁵ (half a unit in the fifth significant figure).

## Edge cases

- **Zero or negative:** C ≤ 0, n ≤ 0, a negative load or factor, a₁
  outside (0, 1], a negative required life, or P = 0 (an unloaded
  bearing has no finite life to report) → invalid input.
- **Out of range:** C = 35.1 kN with F_r = 20 kN (P/C = 0.57) → refused.
- **Unit mismatch:** Example 1 in lbf and rad/s gives the same base-unit
  results.
