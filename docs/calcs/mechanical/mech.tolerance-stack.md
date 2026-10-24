# M1 — Tolerance stack-up (1D) v2 (`mech.tolerance-stack@2.0.0`)

**Scope.** A linear (one-axis) tolerance loop of size dimensions and GD&T position/profile contributors. Worst case, RSS, modified RSS, predicted σ, C_p/C_pk, DPMO, yield, contribution Pareto, seeded Monte Carlo. Supersedes `calc.tolerance-stack` (v0.16) on the worksheet engine; the old calculator stays.

**Method.** Mid-limit `m_i = n_i + (u_i + l_i)/2 + shift_i`; half-band `h_i = (u_i − l_i)/2` (size) or `(zone_i + bonus_i)/2` (position/profile). `x_mean = Σ s_i m_i`, `t_wc = Σ h_i`, `t_rss = √Σ h_i²`, `t_mrss = f·t_rss`. `σ_i = h_i/k` (normal), `h_i/√3` (uniform), `h_i/√6` (triangular); `σ = √Σ σ_i²`. `C_pk = min(x_mean − LSL, USL − x_mean)/(3σ)`, `C_p = (USL − LSL)/(6σ)`, DPMO from the normal tails. Monte Carlo: each contributor sampled from its distribution about `m_i`, summed with sign; seeded, N ≤ 1 000 000.

| Input | Unit | Notes |
|---|---|---|
| `contributors` (table) | mm | name, direction, nominal, upper, lower, kind (size/position/profile), bonus, distribution, shift; ≤ 200 rows |
| `minimum`, `maximum` | mm | optional requirement |
| `k` | — | σ per half-band for normal contributors (default 3) |
| `f_rss` | — | modified-RSS factor (default 1.5) |
| `cpk_target` | — | required C_pk (default 1.33) |
| `n_mc`, `seed` | — | Monte Carlo samples (1 000–100 000 soft) and seed |
| `notation` | — | BS 8888 or ASME Y14.5 (labels only) |

**Outputs.** `x_nom`, `x_mean`, `t_wc`, `x_wc_min/max`, `m_min/m_max`, `t_rss`, `t_mrss`, `sigma`, `cp`, `cpk`, `dpmo`, `yield`, `mc_*`; series: contribution Pareto (worst case and statistical), Monte Carlo histogram.

**Checks.** Worst case within requirement (hard); C_pk ≥ target.

**References.** BS 8888; ASME Y14.5; ISO 2768-1 (numbers and titles only).

**Limitations.** One axis; independence assumed for statistics; no datum-shift or angular effects beyond entered contributors; a position zone is treated as bilateral about true position.

**Test cases.** `ka-shigley-screw` (Shigley 9th ed. Ch. 1 shouldered-screw gap: 0.005 ± 0.010 in worst case, RSS ± 0.006 in — *Unconfirmed* page); `ka-rss-analytic` (housing stack: RSS 0.080623 mm, C_pk 2.3567 — analytic, Confirmed).
