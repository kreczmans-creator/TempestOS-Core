# Tolerance stack-up (1D) (`mech.tolerance-stack@2.3.0`)

**In plain terms.** A tolerance stack adds up the dimensions along a chain of parts to find a gap or overall size, where every part varies within its own tolerance. The worst-case method adds every tolerance at its limit, so the gap is guaranteed but the tolerances must be tight. The statistical methods assume the variations are independent and rarely all at their extremes together, so they predict a narrower spread; the Monte Carlo run samples the real distributions to show the same thing directly. The capability figures say how centred and how wide that spread is against the requirement.

## Scope

Calculates the worst-case, root-sum-square and modified RSS limits of a linear tolerance loop, with the predicted spread and capability. It adds a contribution Pareto and a Monte Carlo prediction against a stated requirement.

The loop has size dimensions and GD&T position and profile contributors on one axis. The sheet supersedes `calc.tolerance-stack` (v0.16) on the worksheet engine, and the old calculator stays. Results are shown in mm whatever unit the contributors are entered in.

## Method

- Mid-limit: `m_i = n_i + (u_i + l_i)/2 + shift_i`.
- Half-band: `h_i = (u_i − l_i)/2` (size) or `(zone_i + bonus_i)/2` (position and profile).
- Worst case and RSS: `x_mean = Σ s_i m_i`; `t_wc = Σ h_i`; `t_rss = √Σ h_i²`; `t_mrss = f·t_rss`.
- Spread of each contributor: `σ_i = h_i/k` (normal), `h_i/√3` (uniform), `h_i/√6` (triangular); `σ = √Σ σ_i²`.
- Capability: `C_pk = min(x_mean − LSL, USL − x_mean)/(3σ)`; `C_p = (USL − LSL)/(6σ)`; DPMO from the normal tails.
- Monte Carlo: each contributor is sampled from its distribution about `m_i` and summed with its sign. The run is seeded, with N up to 1 000 000.

**Stated σ (2.1.0).** An optional `sigma` per contributor (a measured population, as the scan import writes) replaces `half/k` for a *normal* contributor in the predicted σ, in C_p, C_pk and DPMO, and in the Monte Carlo run. The worst case and RSS keep using the band. The statistical contribution Pareto weighs a contributor with a stated σ by (kσ)², the band-equivalent of that σ, and every other contributor as before. Blank means band/k, exactly as before. On a uniform or triangular contributor the value is ignored with the warning `sigma-ignored`.

## Inputs

| Symbol | Meaning | Unit | Group |
|---|---|---|---|
| `contributors` | Contributors in the loop: a table of name, direction, nominal, upper, lower, kind (size, position or profile), bonus, distribution, shift and sigma (at most 200 rows) | mm | Setup |
| `notation` | Drawing notation: BS 8888 or ASME Y14.5 (labels only) | choice | Setup |
| `k` | Standard deviations per half-band for normal contributors (default 3) | none | Factors |
| `f_rss` | Modified-RSS factor (default 1.5) | none | Factors |
| `cpk_req` | Required C_pk (default 1.33) | none | Requirements |
| `minimum` | Minimum acceptable result (optional; blank = no minimum) | mm | Requirements |
| `maximum` | Maximum acceptable result (optional; blank = no maximum) | mm | Requirements |
| `n_mc` | Monte Carlo samples (1 000 to 100 000 usual) | none | Monte Carlo |
| `seed` | Monte Carlo seed | none | Monte Carlo |

`cpk_req` was called `cpk_target` before 2.3.0. The old symbol still loads, so a saved calculation, an inputs file, a CSV file and a pack binding written with it give the same values.

## Assumptions

- Every contributor acts along the one axis of the stack.
- Contributors vary independently about their mid-limit and any stated mean shift, each with its stated distribution.
- A position or profile contributor contributes ± half its tolerance zone, plus half the stated MMC bonus.

## Outputs

`x_nom`, `x_mean`, `t_wc`, `x_wc_min`, `x_wc_max`, `m_min`, `m_max`, `t_rss`, `t_mrss`, `sigma`, `cp`, `cpk`, `dpmo`, `yield` and `mc_*`. The series are the contribution Pareto (worst case and statistical) and the Monte Carlo histogram.

## Ports

| Port | Symbol | Unit | Meaning |
|---|---|---|---|
| `tolerance.result_nominal` | `x_nom` | mm | Nominal result of the stack |
| `tolerance.result_mean` | `x_mean` | mm | Mean result of the stack |
| `tolerance.band_worst_case` | `t_wc` | mm | Worst-case tolerance of the result (±) |
| `tolerance.band_rss` | `t_rss` | mm | Root-sum-square tolerance of the result (±) |
| `tolerance.deviation_standard` | `sigma` | mm | Predicted standard deviation of the result |

## Checks

- Worst-case range against the requirement (a yes or no check, hard).
- C_pk against the requirement: the required C_pk `cpk_req` against `cpk`.
- Rules `contributors` and `min-above-max` (blocks); `few-contributors`, `no-requirement` and `sigma-ignored` (warnings).

## Limitations

The sheet works on one axis and assumes independence for the statistics. It has no datum-shift or angular effects beyond entered contributors. A position zone is treated as bilateral about the true position.

## References

- BS 8888: technical product documentation and specification (number and title only).
- ASME Y14.5: dimensioning and tolerancing (number and title only).
- ISO 2768-1:1989: general tolerances (number and title only).

## Test cases

- `ka-shigley-screw`: Shigley 9th ed. Ch. 1 shouldered-screw gap, 0.005 ± 0.010 in worst case and RSS ± 0.006 in. The page is unconfirmed.
- `ka-rss-analytic`: housing stack, RSS 0.080623 mm, C_pk 2.3567. Analytic, confirmed.
- `ka-measured-sigma`: A 10 ± 0.03 normal; B 5 ± 0.06 with a stated σ of 0.015. Worst case 0.09, RSS 0.067082, σ 0.018027756, C_p = C_pk 1.849000654 against 14.9 to 15.1 mm. Analytic, confirmed, recomputed in python3.
- The legacy module's hand calculations run as `ka-legacy-screw`, `ka-legacy-housing`, `ka-legacy-housing-fails` and `ka-legacy-no-requirement`.

**Changes in 2.3.0 (C-2).** Results are shown in mm for every input unit (the values are the same; a stack entered in inches printed in inches before). `cpk_target` is renamed `cpk_req` (the old symbol is an alias). The inputs are in groups, with soft limits on the requirement and the seed. The C_pk check shows the required and the predicted C_pk, and the worst-case check is named against the requirement. Messages name the table cell. Five ports are declared. The Monte Carlo points are called quantiles. No result changes.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Shouldered-screw assembly gap (Shigley) ([sheet](../../worked-examples/mechanical/mech.tolerance-stack/shouldered-screw-gap/sheet.txt)).

```text
c1 = mid 44.45 mm, ± 0.0762 mm, σ 0.0254 mm (normal) = 44.45 mm
c2 = mid 19.05 mm, ± 0.0254 mm, σ 0.00846667 mm (normal) = -19.05 mm
c3 = mid 3.048 mm, ± 0.127 mm, σ 0.0423333 mm (normal) = -3.048 mm
c4 = mid 22.225 mm, ± 0.0254 mm, σ 0.00846667 mm (normal) = -22.225 mm
x_nom = 44.45 − 19.05 − 3.048 − 22.225 = 0.127 mm
x_mean = 44.45 − 19.05 − 3.048 − 22.225 = 0.127 mm
t_wc = 0.0762 + 0.0254 + 0.127 + 0.0254 = 0.254 mm
x_wc_min = 0.127 mm − 0.254 mm = -0.127 mm
x_wc_max = 0.127 mm + 0.254 mm = 0.381 mm
t_rss = √(0.0762² + 0.0254² + 0.127² + 0.0254²) = 0.1524 mm
x_rss_min = 0.127 mm − 0.1524 mm = -0.0254 mm
x_rss_max = 0.127 mm + 0.1524 mm = 0.2794 mm
t_mrss = 1.5 × 0.1524 mm = 0.2286 mm
sigma = √(0.0254² + 0.00846667² + 0.0423333² + 0.00846667²) = 0.0508 mm
… 11 more step(s) on the sheet
Governing: FAIL  Worst-case range against the requirement: -0.127 … 0.381 mm vs 0.0762 … open mm
```
<!-- hand-arithmetic:end -->
