# M8 — Fatigue damage, Palmgren–Miner (`mech.fatigue-miner@1.0.0`)

**In plain terms.** Every stress cycle uses up a little of a part's fatigue life, and a bigger cycle uses up much more: on a slope-3 curve, doubling the stress range divides the life by eight. Miner's rule adds the fraction of life each block of cycles consumes; when the fractions reach one, the part is expected to crack. Stress ranges below an endurance limit, when the curve has one, are taken to do no damage at all.

**Scope.** A stress-range spectrum against an S–N curve of one slope through a reference point (a detail category or the material's fatigue strength). Ported from the legacy `calc.fatigue-miner` (ADR-0167). Clause numbers only (BS EN 1993-1-9 §8).

**Method.** `N_i = N_ref·(S_ref/S_i)^m` (unbounded when `S_i ≤ S_e`); `d_i = n_i/N_i`; `D = Σd_i`; repetitions to failure `1/D`.

**Inputs.** `S_ref`, `N_ref`, `m`, optional `S_e`, table `blocks` (stress range, cycles). **Outputs.** `N_i`, `d_i` per block, `D`, `R_f`. **Checks.** D ≤ 1.

**Limitations.** One slope (no knee or cut-off below the endurance limit other than S_e); blocks with a predicted life below 10⁴ cycles block (low-cycle regime).

**Test cases.** The legacy module's two hand calculations (`ka-legacy-cat71`, `ka-legacy-slope5-limit`).
