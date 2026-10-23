# Bearing housing axial clearance (analytic) (`mech.tolerance-stack`)

**Source.** Analytic check, RSS = √(0.05² + 0.06² + 0.02²) mm; C_pk from the nearer limit — Confirmed.

Three metric parts with asymmetric deviations. Worst case 0.48–0.74 mm sits inside the 0.40–0.80 mm requirement with margins 0.08 and 0.06 mm; C_pk 2.36 against the 1.33 target.

| Input | Value | Source |
|---|---|---|
| `contributors` | 3 row(s) | drawing: DRG-HSG-001 rev B |
| `cpk_target` | 1.33 | assumption: Usual capability target. |
| `f_rss` | 1.5 | assumption: Bender factor 1.5. |
| `k` | 3 | assumption: Each part at ±3σ within its band. |
| `maximum` | 0.8 mm | spec: SPEC-BRG-7 §4.2 |
| `minimum` | 0.4 mm | spec: SPEC-BRG-7 §4.2 |
| `n_mc` | 10000 | assumption: Default sample count. |
| `notation` | BS 8888 | assumption: Metric drawing. |
| `seed` | 1 | assumption: Fixed seed for reproducibility. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `x_mean` | 0.61 mm | 0.61 mm | PASS |
| `t_wc` | 0.13 mm | 0.13 mm | PASS |
| `t_rss` | 0.0806226 mm | 0.0806226 mm | PASS |
| `cpk` | 2.35666  | 2.35666  | PASS |

Governing check: C_pk against target — PASS (utilisation 0.564). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
