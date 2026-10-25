# M8 — Fatigue damage, Palmgren–Miner (`mech.fatigue-miner@1.0.1`)

**In plain terms.** Every stress cycle uses up a little of a part's fatigue life, and a bigger cycle uses up much more: on a slope-3 curve, doubling the stress range divides the life by eight. Miner's rule adds the fraction of life each block of cycles consumes; when the fractions reach one, the part is expected to crack. Stress ranges below an endurance limit, when the curve has one, are taken to do no damage at all.

**Scope.** A stress-range spectrum against an S–N curve of one slope through a reference point (a detail category or the material's fatigue strength). Ported from the legacy `calc.fatigue-miner` (ADR-0167). Clause numbers only (BS EN 1993-1-9 §8).

**Method.** `N_i = N_ref·(S_ref/S_i)^m` (unbounded when `S_i ≤ S_e`); `d_i = n_i/N_i`; `D = Σd_i`; repetitions to failure `1/D`.

**Inputs.** `S_ref`, `N_ref`, `m`, optional `S_e`, table `blocks` (stress range, cycles). **Outputs.** `N_i`, `d_i` per block, `D`, `R_f`. **Checks.** D ≤ 1.

**Limitations.** One slope (no knee or cut-off below the endurance limit other than S_e); blocks with a predicted life below 10⁴ cycles block (low-cycle regime).

**Test cases.** The legacy module's two hand calculations (`ka-legacy-cat71`, `ka-legacy-slope5-limit`).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Welded detail category 71 under a three-block spectrum ([sheet](../../worked-examples/mechanical/mech.fatigue-miner/detail-71-spectrum/sheet.txt)).

```text
N_1 = 2000000 × (71 MPa / 100 MPa)^3 = 715820
d_1 = 100000 / 715820 = 0.1397
N_2 = 2000000 × (71 MPa / 60 MPa)^3 = 3314000
d_2 = 500000 / 3314000 = 0.15088
N_3 = 2000000 × (71 MPa / 40 MPa)^3 = 11185000
d_3 = 2000000 / 11185000 = 0.17882
D = 0.1397 + 0.15088 + 0.17882 = 0.46939
R_f = 1 / 0.46939 = 2.1304
Governing: PASS  Cumulative damage D ≤ 1: D = 0.46939 vs D ≤ 1  utilisation 0.469  « Shigley §6-15
```
<!-- hand-arithmetic:end -->
