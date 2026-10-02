# Linear tolerance stack-up, worst case and RSS — `calc.tolerance-stack`

**Status:** recovered from the v0.16.0 release-candidate calculation suite
(the Tolerance Analysis Calculator, lost with `feature/v0.16.0-integration`)
and ported into the `WP 21.7A` module architecture for
`Tempest.Core.Calculations.Modules.ToleranceStackCalculationDefinition`.
Test vectors: `tests/Tempest.Core.Tests/Calculations/Modules/ToleranceStackVectors.cs`.

## Method

**In plain terms.** An assembly's gap is a chain of part dimensions: some
add to it, some take it away. Every part is made somewhere inside its
tolerance, so the gap varies too. The *worst case* asks what happens if
every part lands at the unluckiest end of its band at once — a guarantee
that holds for every conforming set of parts, but pessimistic, because the
extremes rarely coincide. The *statistical* (root-sum-square) answer asks
what a population of assemblies will actually look like if each part
varies at random about the middle of its band: the bands add like the
sides of a right-angled triangle rather than end to end, so the spread is
much narrower. That second answer is a prediction, not a guarantee, and
true only when the processes behave that way — so it is produced only when
the engineer states why they do, and the pass/fail verdict always comes
from the worst case.

A linear (one-dimensional) dimension chain, closed by the arithmetic and
root-sum-square stacks of every tolerancing text: Shigley's *Mechanical
Engineering Design* (Budynas & Nisbett), chapter 1, "Dimensions and
Tolerances", whose shouldered-screw gap is Example 1 below; Fortini,
*Dimensioning for Interchangeable Manufacture*; Spotts, *Dimensioning and
Tolerancing for Quantity Production*. Asymmetric limits are handled the
textbook way, by restating each dimension as its mid-limit value with a
symmetric half-band, which shifts the mean of the result. The process
capability index C_pk and the normal-tail fraction outside the requirement
are the standard definitions (Montgomery, *Introduction to Statistical
Quality Control*). Restated, not reproduced.

## Inputs

| Input | Dimension | Limits |
|---|---|---|
| `Contributors` | list of (name, Adds/Subtracts, nominal, upper deviation, lower deviation) | ≥ 1 row; named; lower deviation ≤ upper deviation |
| `MinimumResult` | Length, or none | not above the maximum |
| `MaximumResult` | Length, or none | not below the minimum |
| `SigmaPerTolerance` k | — | > 0 (3 for a capable, centred process) |
| `StatisticalBasis` | text, or none | empty means worst case only |

A row is typed `Housing bore, Adds, 40 mm, 0.1 mm, 0 mm`. Deviations are
signed: a dimension `20 +0/−0.12` has upper deviation 0 and lower −0.12. A
name may not contain a comma.

## Formulae

For contributor i with direction s_i = +1 (Adds) or −1 (Subtracts), nominal
n_i, upper deviation ES_i and lower deviation EI_i:

1. Mid-limit m_i = n_i + (ES_i + EI_i)/2; half-band t_i = (ES_i − EI_i)/2.
2. Nominal result N = Σ s_i n_i; mean result μ = Σ s_i m_i.
3. Worst-case tolerance T_wc = Σ t_i; limits μ − T_wc and μ + T_wc.
4. Margins: worst-case minimum − stated minimum; stated maximum −
   worst-case maximum (negative when outside).
5. Outcome meets criteria when both worst-case limits lie inside the
   stated requirement (slack of one part in a billion of the stack's own
   magnitude); no requirement stated → computed, no verdict.
6. Only with a stated basis: T_rss = √(Σ t_i²); limits μ ∓ T_rss;
   σ = T_rss / k.
7. Only with a basis and a requirement: Z_lo = (μ − LSL)/σ,
   Z_hi = (USL − μ)/σ; C_pk = min(Z)/3; fraction outside
   = Σ Q(Z) with Q(z) = ½ erfc(z/√2), reported in parts per million.
   erfc is the *Numerical Recipes* Chebyshev fit (fractional error
   < 1.2 × 10⁻⁷).

## Outputs and recorded intermediates

Results: outcome (or none), contributor count, N, μ, T_wc, worst-case
limits and margins, the statistical note, and — with a basis — T_rss, its
limits, σ, C_pk and ppm outside. Intermediates: each contributor's
mid-limit and half-band, N, μ, T_wc, the worst-case limits, the basis,
T_rss, σ, each Z, C_pk and ppm.

## Limits of applicability

One axis only: no angles, datum shift, geometric controls, bonus tolerance
or assembly float — a GD&T stack needs a different method. The statistical
figures assume independent, normal contributors centred on their
mid-limits; they are not computed at all without a stated basis, so this
module has no refusal.

## Worked examples (hand calculations)

**Example 1 — Shigley's shouldered screw, worst case.** The gap
w = a − b − c − d must be at least 0.003 in, with a = 1.750 ± 0.003,
b = 0.750 ± 0.001, c = 0.120 ± 0.005, d = 0.875 ± 0.001 in.
N = μ = 1.750 − 0.750 − 0.120 − 0.875 = **0.005 in**;
T_wc = 0.003 + 0.001 + 0.005 + 0.001 = **0.010 in**; limits
**−0.005 in** to **0.015 in**. The worst case can interfere: minimum
margin **−0.008 in**, does not meet criteria (Shigley's own conclusion).

**Example 2 — the same stack, statistical.** Basis stated, k = 3.
T_rss = √(0.003² + 0.001² + 0.005² + 0.001²) = √(36 × 10⁻⁶) = **0.006 in**;
limits −0.001 to 0.011 in; σ = **0.002 in**. Z_lo = (0.005 − 0.003)/0.002
= 1.0, C_pk = **0.33333**, predicted **158 655 ppm** below the minimum
(Q(1) = 0.158655). Verdict still from the worst case: does not meet.

**Example 3 — asymmetric deviations, both limits met.** Housing bore depth
Adds 40 +0.10/0; bearing width Subtracts 20 0/−0.12; spacer Subtracts
19.5 ±0.02 mm; requirement 0.40 to 0.80 mm; basis stated, k = 3.
Mid-limits 40.05, 19.94, 19.5; half-bands 0.05, 0.06, 0.02.
N = **0.5 mm**; μ = 40.05 − 19.94 − 19.5 = **0.61 mm**; T_wc = **0.13 mm**;
limits **0.48** to **0.74 mm** (check: 40 − 20 − 19.52 = 0.48;
40.1 − 19.88 − 19.48 = 0.74); margins **0.08** and **0.06 mm**: meets.
T_rss = √0.0065 = **0.080623 mm**; σ = **0.026874 mm**;
Z_lo = 7.8142, Z_hi = 7.0700; C_pk = **2.3567**;
**7.7755 × 10⁻⁷ ppm** outside.

**Example 4 — worst case fails, prediction given.** Example 3 with the
minimum raised to 0.55 mm: minimum margin **−0.07 mm**, does not meet.
Z_lo = 0.06/0.026874 = 2.2326, C_pk = **0.74421**, **12 787 ppm** below.

**Example 5 — no requirement.** Example 3 with neither limit: every
figure as Example 3, no outcome, no C_pk, no ppm.

Precision: five significant figures; relative tolerance 5 × 10⁻⁵ (half a
unit in the fifth significant figure).

## Edge cases

- **Invalid:** no contributors, an unnamed contributor, a lower deviation
  above its upper, a minimum above the maximum, k ≤ 0 or not a number.
- **Exactly on a limit:** 50 ± 0.10 less 20 ± 0.05 against 29.85..30.15 mm
  sits on both limits in decimal and a few parts in 10¹⁸ outside in
  binary; it meets (the v0.16.0 suite's own regression, carried over). One
  micrometre outside fails.
- **Unit mismatch:** Example 1 in millimetres (× 25.4) gives the same
  figures.
