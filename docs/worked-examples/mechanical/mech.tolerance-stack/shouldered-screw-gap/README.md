# Shouldered-screw assembly gap (Shigley) (`mech.tolerance-stack`)

**Source.** Shigley's Mechanical Engineering Design, 9th ed., Ch. 1 tolerance stack-up example — Unconfirmed.

Four inch-dimensioned parts on a shouldered screw. The worst-case gap can go negative (−0.005 in), so the stack fails the 0.003 in minimum; the RSS band ±0.006 in predicts most assemblies within 0.005 ± 0.006 in.

| Input | Value | Source |
|---|---|---|
| `contributors` | 4 row(s) | spec: Shigley 9e Ch. 1 example, part dimensions |
| `cpk_target` | 1.33 | assumption: Usual capability target. |
| `f_rss` | 1.5 | assumption: Bender factor 1.5. |
| `k` | 3 | assumption: Each part at ±3σ within its band. |
| `minimum` | 0.003 in | spec: Shigley 9e Ch. 1 example, required gap |
| `n_mc` | 10000 | assumption: Default sample count. |
| `notation` | ASME Y14.5 | assumption: Inch drawing. |
| `seed` | 1 | assumption: Fixed seed for reproducibility. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `x_nom` | 0.005 in | 0.005 in | PASS |
| `t_wc` | 0.01 in | 0.01 in | PASS |
| `t_rss` | 0.006 in | 0.006 in | PASS |
| `x_wc_min` | -0.005 in | -0.005 in | PASS |

Governing check: Worst case within requirement — FAIL (utilisation —). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
