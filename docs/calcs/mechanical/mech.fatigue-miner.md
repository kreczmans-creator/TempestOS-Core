# Fatigue damage (Palmgren–Miner) (`mech.fatigue-miner@1.1.0`)

**In plain terms.** Every stress cycle uses up a little of a part's fatigue life, and a bigger cycle uses up much more: on a slope-3 curve, doubling the stress range divides the life by eight. Miner's rule adds the fraction of life each block of cycles consumes. When the fractions reach one, the part is expected to crack. Stress ranges below an endurance limit, when the curve has one, are taken to do no damage at all.

## Scope

Calculates the cumulative fatigue damage of a stress-range spectrum against an S–N curve of one slope, with an optional endurance limit, and the spectrum repetitions to failure. It excludes life below 10⁴ cycles and a second S–N slope.

## Method

- Cycles to failure at each stress range, from the S–N curve (unbounded at or below the endurance limit S_e): N_i = N_ref·(S_ref/S_i)^m.
- Damage done by each block of cycles: d_i = n_i/N_i.
- Total damage, and how many repetitions of the spectrum the part survives: D_tot = Σ d_i; repetitions = 1/D_tot.
- Acceptance by the Palmgren–Miner rule: D_tot ≤ 1.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group |
|---|---|---|---|---|---|
| `S_ref` | Reference stress range (detail category or material fatigue strength) | MPa | 71 | 1 to 2000 |  |
| `N_ref` | Cycles at the reference stress range |  | 2000000 | 10000 to 1000000000 |  |
| `m` | S–N slope |  | 3 | 2 to 15 |  |
| `S_e` | Endurance limit (ranges at or below it do no damage) | MPa | optional | 0.1 to 2000 |  |
| `blocks` | Stress-range blocks |  |  |  |  |
| `blocks.S` | Stress range (column) | MPa |  | |  |
| `blocks.n` | Cycles (column) |  |  | |  |

## Assumptions

1. Damage accumulates linearly and independently of the order of the blocks. Basis: Palmgren–Miner rule.
2. The S–N curve has one slope over the whole range, with no second slope below a knee. Basis: A simplified curve: enter a two-slope curve as two sheets or with a lower slope.

## Outputs

| Symbol | Description |
|---|---|
| `D_tot` | Total damage |
| `R_f` | Repetitions to failure |
| `N_1` | Life at block 1 |
| `d_1` | Damage of block 1 |
| `N_2` | Life at block 2 |
| `d_2` | Damage of block 2 |
| `N_3` | Life at block 3 |
| `d_3` | Damage of block 3 |

## Ports

A port keeps its output symbol; a link, a pack binding or an inputs file written with the symbol or an alias still resolves.

| Public name | Symbol | Unit | Description |
|---|---|---|---|
| `fatigue.damage_total` | `D_tot` (also `D`) |  | Total damage by the Palmgren–Miner rule |
| `fatigue.repetitions_failure` | `R_f` |  | Repetitions of the spectrum to failure |

## Checks

- Total damage against the damage limit (`D_tot` against `D_lim` = 1); the utilisation is `D_tot`.

Applicability rules:

| Rule | Severity | Effect | Condition |
|---|---|---|---|
| `no-blocks` | Block | Conservative | At least one stress-range block is needed. |
| `low-cycle` | Block | Conservative | A block with a predicted life below 10⁴ cycles is in the low-cycle (strain-life) regime. |

## Limitations

One slope only: there is no knee or cut-off below the endurance limit other than `S_e`. A block with a predicted life below 10⁴ cycles blocks the sheet, because it is in the low-cycle regime. The cycle-count column of the block table is named `n`; a saved table keeps that key, so it is not renamed.

## Changes

1.1.0 (C-2): the total damage `D` is now `D_tot` (the port `fatigue.damage_total` keeps `D` as an alias), the sheet declares the ports `fatigue.damage_total` and `fatigue.repetitions_failure`, the check shows the damage and its limit, the scope, assumption, messages and references follow the standard. No calculated value changes. The ported legacy module is `calc.fatigue-miner` (ADR-0167).

## References

- BS EN 1993-1-9:2005, cl. 8: Fatigue resistance (S–N curves by detail category).
- Budynas and Nisbett, Shigley's Mechanical Engineering Design, §6-15, 11th ed.: Cumulative fatigue damage.

## Test cases

The legacy module's two hand calculations: `ka-legacy-cat71` (detail category 71, no cut-off, `D_tot` 0.46939, 2.13 repetitions) and `ka-legacy-slope5-limit` (slope 5 with an endurance limit, `D_tot` 1.4330, fails).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Welded detail category 71 under a three-block spectrum ([sheet](../../worked-examples/mechanical/mech.fatigue-miner/detail-71-spectrum/sheet.txt)).

```text
N_1 = 2000000 × (71 MPa / 100 MPa)^3 = 715820
d_1 = 100000 / 715820 = 0.1397
N_2 = 2000000 × (71 MPa / 60 MPa)^3 = 3314000
d_2 = 500000 / 3314000 = 0.15088
N_3 = 2000000 × (71 MPa / 40 MPa)^3 = 11185000
d_3 = 2000000 / 11185000 = 0.17882
D_tot = 0.1397 + 0.15088 + 0.17882 = 0.46939
D_lim = 1
R_f = 1 / 0.46939 = 2.1304
Governing: PASS  Total damage against the damage limit: 0.46939 vs 1  utilisation 0.47  « Shigley §6-15
```
<!-- hand-arithmetic:end -->
