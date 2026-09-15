# Fatigue under variable amplitude, S-N curve with Miner's rule — `calc.fatigue-miner`

**Status:** specified by `WP 21.7A` (v0.21.0) for
`Tempest.Core.Calculations.Modules.FatigueMinerCalculationDefinition`.
Test vectors: `tests/Tempest.Core.Tests/Calculations/Modules/FatigueMinerVectors.cs`.

## Method

A single-slope stress-life (Basquin) curve through one reference point,
with an optional constant-amplitude endurance limit, and the linear
damage summation of Palmgren and Miner over a block-loading spectrum —
the method of every fatigue text (Shigley, the fatigue-failure chapter;
Dowling, *Mechanical Behavior of Materials*) and the form the detail
categories of EN 1993-1-9 take, where the reference point is the detail
category at two million cycles with slope 3. Only the single-slope curve
with a cut-off is implemented; EN 1993-1-9's second slope between the
constant-amplitude and cut-off limits is not. Restated, not reproduced.

## Inputs

| Input | Dimension | Limits |
|---|---|---|
| `ReferenceStressRange` S_ref | Pressure | > 0; from the material record's fatigue strength or the detail category |
| `ReferenceCycles` N_ref | — | > 0 (2 × 10⁶ for a Eurocode detail category) |
| `Slope` m | — | > 0 (3 for welded steel details, 5 for some non-welded) |
| `EnduranceLimit` | Pressure, or none | > 0 when given; ranges at or below it cause no damage |
| `Blocks` | list of (stress range, cycles) | ≥ 1 block; each range > 0, cycles ≥ 0 |

## Formulae

1. Life at range S_i: N_i = N_ref · (S_ref / S_i)^m, or unbounded when
   S_i ≤ endurance limit.
2. Damage of block i: d_i = n_i / N_i (zero when unbounded).
3. Total damage D = Σ d_i; spectrum repetitions to failure = 1 / D
   (unbounded when D = 0).
4. Outcome meets criteria when D ≤ 1 (framework slack).

## Outputs and recorded intermediates

Results: per block N_i (null when unbounded) and d_i, in input order; D;
repetitions to failure; outcome. Intermediates: each block's life and
damage, D.

## Limits of applicability (refusals, not exceptions)

- **Low-cycle regime.** A block whose predicted life is below 10⁴ cycles
  lies where a stress-life curve is not valid (strain-life territory):
  refused, naming the block. This is the module's own limit, set at the
  conventional high-cycle boundary.

## Worked examples (hand calculations)

**Example 1 — Eurocode detail category 71, no cut-off.** S_ref = 71 MPa at
N_ref = 2 × 10⁶, m = 3. Blocks: 100 MPa × 10⁵; 60 MPa × 5 × 10⁵;
40 MPa × 2 × 10⁶.
N(100) = 2 × 10⁶ × 0.71³ = **715 822**; d = **0.13970**.
N(60) = 2 × 10⁶ × 1.18333³ = **3 313 991**; d = **0.15088**.
N(40) = 2 × 10⁶ × 1.775³ = **11 184 719**; d = **0.17882**.
D = **0.46939**; repetitions to failure **2.1304**. Meets criteria.

**Example 2 — slope 5 with an endurance limit; damage exceeds one.**
S_ref = 150 MPa at 10⁶, m = 5, endurance limit 60 MPa. Blocks:
120 MPa × 2 × 10⁶; 90 MPa × 10⁷; 50 MPa × 10⁷.
N(120) = 10⁶ × 1.25⁵ = **3 051 758**; d = **0.65536**.
N(90) = 10⁶ × (5/3)⁵ = **12 860 082**; d = **0.77760**.
50 MPa is below the limit: unbounded, d = 0.
D = **1.4330**; repetitions **0.69786**. Does not meet criteria.

Precision: five significant figures; relative tolerance 5 × 10⁻⁵ (half a unit in the fifth significant figure).

## Edge cases

- **Zero or negative:** S_ref, N_ref or m ≤ 0, an endurance limit ≤ 0, no
  blocks, a block range ≤ 0 or negative cycles → invalid input. A block
  with zero cycles contributes nothing.
- **Out of range:** a 500 MPa block on category 71 (N = 5 727 cycles) →
  refused.
- **Unit mismatch:** Example 1 in psi gives the same results.
