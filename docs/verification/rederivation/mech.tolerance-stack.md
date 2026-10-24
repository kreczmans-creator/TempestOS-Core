# mech.tolerance-stack — independent re-derivation (Track P, P1)
Calculator: Tolerance stack-up (1D) v2, version 2.0.0. Source file: src/Tempest.Calc/Tempest.Calc.Mechanical/ToleranceStackCalculator.cs. Spec: docs/calcs/mechanical/mech.tolerance-stack.md.
Script: tools/verification/mech.tolerance-stack.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS (reviewed: no Critical, High or Medium findings)

The method is correct. Both KATs match. The only defects are a loss of precision in the far upper tail of DPMO and a silent skip when the spread is zero.

## Method re-derived
- Size contributor: mid-limit m = n + (u + l)/2 + shift. Half-band h = (u − l)/2. Unit: mm. Source: the spec method and standard one-dimensional stack practice.
- Position or profile contributor: h = (zone + bonus)/2. Mid-limit m = n + shift. The zone is bilateral about true position (spec limitation).
- Mean result x_mean = Σ s_i·m_i, with s_i = +1 for "adds" and −1 for "subtracts".
- Worst case t_wc = Σ h_i. Limits are x_mean ± t_wc.
- RSS t_rss = √Σ h_i². Modified RSS t_mrss = f·t_rss, with f = 1.5 by default (Bender factor; Bender, 1968, as cited in Shigley and in Drake, Dimensioning and Tolerancing Handbook).
- Spread: σ_i = h/k (normal), h/√3 (uniform), h/√6 (triangular). Total σ = √Σ σ_i².
- Capability: C_p = (USL − LSL)/(6σ). C_pk = min(x − LSL, USL − x)/(3σ).
- DPMO = 10⁶·[Q((x − LSL)/σ) + Q((USL − x)/σ)], where Q is the upper normal tail. Yield = 1 − DPMO/10⁶.
- Monte Carlo is seeded and cannot be reproduced bit for bit. The script only checks its spread against σ.

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-shigley-screw | x_nom | 0.005 | 0.005 | 8.7e-16 | 1e-09 | PASS |
| ka-shigley-screw | t_wc | 0.01 | 0.01 | 0.0e+00 | 1e-09 | PASS |
| ka-shigley-screw | x_wc_min | -0.005 | -0.005 | 8.7e-16 | 1e-06 | PASS |
| ka-shigley-screw | t_rss | 0.006 | 0.006 | 0.0e+00 | 1e-06 | PASS |
| ka-shigley-screw | sigma | 0.002 | 0.002 | 0.0e+00 | 1e-06 | PASS |
| ka-shigley-screw | cpk | 0.33333333 | 0.33333333 | 2.2e-15 | 1e-06 | PASS |
| ka-rss-analytic | x_mean | 0.61 | 0.61 | 6.7e-15 | 1e-09 | PASS |
| ka-rss-analytic | t_wc | 0.13 | 0.13 | 0.0e+00 | 1e-09 | PASS |
| ka-rss-analytic | t_rss | 0.080622577 | 0.080622577 | 6.0e-09 | 1e-06 | PASS |
| ka-rss-analytic | sigma | 0.026874192 | 0.026874192 | 1.8e-08 | 1e-06 | PASS |
| ka-rss-analytic | cpk | 2.35666 | 2.35666 | 1.8e-08 | 1e-06 | PASS |
| ka-rss-analytic | dpmo | 7.7755e-07 | 7.7754678e-07 | 4.1e-06 | 5e-02 | PASS |

Product KAT results (probe `kat`): 2 of 2 cases pass. The product DPMO is 7.7759e-7 against my 7.77547e-7, a difference of 5.2e-5. See P1-mech.tolerance-stack-01.
The Shigley case is marked Unconfirmed in the source for its page number. The arithmetic matches Shigley's shouldered-screw stack as I know it.

## Extra probes (product vs independent)
All values are in mm unless stated. The script re-runs each probe. Product values are hard-coded from the probe tool.
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| p1 typical | 5 size rows, limits 0.1 to 1.0 | sigma / cpk | 0.0213437 / 6.40312 | 0.0213437 / 6.40312 | < 1e-10 | PASS |
| p2 distributions | uniform, triangular, shifts, limits 0.2 to 1.2 | x_mean / sigma / cpk / dpmo | 0.535 / 0.0666041 / 1.67657 / 0.245601 | same | < 1.5e-8 | PASS |
| p3 GD&T | position 0.2 + bonus 0.1, profile 0.1 | t_wc / sigma / cpk | 0.35 / 0.0645497 / 2.58199 | same | < 2e-10 | PASS |
| p3 GD&T | same | dpmo | 9.5168e-9 | 9.4857e-9 | 3.3e-3 | FINDING 01 |
| p4 mean below min | min 0.3 only, mean 0.1 | cpk / dpmo | −1.41421 / 999988.95 | same | < 1e-9 | PASS |
| p5 zero bands | all bands 0 | sigma | 0 | 0 | 0 | PASS (see 02) |
| p6 mixed units | rows in in and mm | sigma / cp / cpk | 0.0263012 / 95.0525 / −58.2989 | same | < 2e-10 | PASS |
| p8 large | nominals near 9e5 mm | t_rss / cpk | 0.707107 / 7.07107 | same | < 2e-11 | PASS |
| p16 max only | USL 0.54, z = 6 | cpk / dpmo | 2.0 / 9.86588e-4 | 2.0 / 9.86588e-4 | 5.6e-8 | PASS |
| p9 upper tail | USL at z = 7 | dpmo | 1.27987e-6 | 1.27981e-6 | 4.1e-5 | FINDING 01 |
| p10 upper tail | USL at z = 8 | dpmo | 6.6613e-10 | 6.2210e-10 | 7.1e-2 | FINDING 01 |
| p10b upper tail | USL at z = 8.6 | dpmo | 0 | 3.99e-12 | 1.0 | FINDING 01 |

Guard probes also behave as specified. A lower deviation above the upper blocks. A minimum above the maximum blocks. An empty table blocks. A negative position zone blocks. Row 201 blocks.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| Mid-limit and half-band | m = n + (u + l)/2 + shift; h = (u − l)/2 | Same | Yes |
| GD&T half-band | (zone + bonus)/2 | Same; lower deviation ignored | Yes |
| Sign | adds +1, subtracts −1 | Any text other than "subtracts" counts as adds; the column is a choice list | Yes |
| RSS, modified RSS | √Σh², f·RSS | Same | Yes |
| σ by distribution | h/k, h/√3, h/√6 | Same | Yes |
| C_p, C_pk | as above | Same; C_p only with both limits | Yes |
| DPMO | Q tails | Lower tail: 0.5·erfc. Upper tail: 1 − Φ(z), with an erfc fit of 1.2e-7 error | Partly (finding 01) |
| Units | mm default, per-cell units | Each cell converted to SI; output in the first row's unit | Yes |
| Worst-case check | within requirement | Uses x_mean ± t_wc with a 1e-9 slack | Yes |
| Few contributors | warn below 4 | Warning at fewer than 4 | Yes |
| No requirement | warn | Warning; no C_pk, DPMO or yield | Yes |
| Zero spread | not specified | C_pk, DPMO and the C_pk check are skipped without a notice | Partly (finding 02) |
| Mean shift | enters m_i | Moves x_mean and the worst-case limits; t_wc is unchanged | Yes (note 04) |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) |
|---|---|---|---|---|---|
| P1-mech.tolerance-stack-01 | Low | Low: confirmed. It only matters below 1e-6 DPMO, where no decision changes | The upper-tail DPMO is computed as 1 − Φ(z). This loses all precision beyond z of about 7. The result is only wrong when DPMO is below 1e-6. The KAT tolerance of 5 % hides it. | Four rows of 10 ± 0.01, 9, 0.5, 0 and σ = 0.0066667 mm. USL at z = 7: product 1.27987e-6, true 1.27981e-6. At z = 8: 6.66e-10 against 6.22e-10. At z = 8.6: 0 against 4.0e-12. KAT ka-rss-analytic gives 7.7759e-7 against 7.77547e-7. | Compute the upper tail as 0.5·erfc(z/√2). Use a higher-precision erfc. Tighten the KAT tolerance to 1e-4. |
| P1-mech.tolerance-stack-02 | Low | Low: confirmed. The worst-case check still runs | When every band is zero, σ = 0. C_p, C_pk, DPMO, yield and the C_pk check are all skipped. No notice says so. | Rows 10 ± 0 and 9 ± 0, limits 0.5 to 2.0. Outputs hold only the worst-case results. | Add a notice such as "zero spread: capability not computed". |
| P1-mech.tolerance-stack-03 | Info | Info: confirmed | A blank lower deviation on a size row counts as 0. Upper 0.1 with a blank lower gives +0.1/0, not ±0.1. This matches the column help, but a user may expect a symmetric band. | Probe p18: upper 0.1, lower blank gives h = 0.05 and mid +0.05. | Optional: show the resolved band in the row label. The worksheet already does this. |
| P1-mech.tolerance-stack-04 | Info | Info: confirmed | A mean shift moves x_mean and so the worst-case limits. It does not widen t_wc. A shift of a drifting process is therefore not a worst-case allowance. | Probe p2. Spec describes this behaviour. | Say so in the help text for the shift column. |
| P1-mech.tolerance-stack-R1 | — | Low | A size row with lower equal to upper gives a zero band with no notice. A user who types the lower deviation as a magnitude (0.1 for −0.1) loses that row's tolerance. The row label does show "+0.1/0.1". | Probe: row C, 0.5 mm, upper 0.1, lower 0.1: half-band 0, mid 0.6 mm, no notice. | Warn when a size row has a zero band, or when lower equals upper and is not zero. |
| P1-mech.tolerance-stack-R2 | — | Low | The statistical Pareto uses h² for each row, not σ_i². With mixed distributions the shares and the named largest contributor are wrong. | Probe: A normal ±0.1 and B uniform ±0.1. Product: A 50 %, B 50 %, largest "A". By σ²: A 25 %, B 75 %. | Use σ_i² for the statistical share. |

## Notes for the owner's manual test
- Enter the Shigley stack in inches: 1.750 ± 0.003 adds; 0.750, 0.120 (± 0.005) and 0.875 subtract. Expect gap 0.005, t_wc 0.010, RSS 0.006, σ 0.002.
- Enter the housing stack with minimum 0.40 and maximum 0.80 mm. Expect x_mean 0.61, C_p 2.4807, C_pk 2.3567. DPMO is near 7.8e-7.
- Add a position row with zone 0.2 and bonus 0.1. Expect a half-band of 0.15.
- Set one contributor to uniform. Expect σ_i = h/√3, which is larger than the normal value for k = 3.
- Set the maximum so that C_pk is above 2.7. The DPMO then reads zero or a rounded value. That is finding 01.

## Independent review
- Reviewer: senior reviewer (Claude), 2026-10-24.
- I ran the script (exit 0). It computes the stack, σ, C_pk and DPMO on its own and does not translate the C#.
- No finding was rated Medium or above, so there was nothing to reproduce at that level. I re-ran the DPMO rows in the script.
- I read Run line by line. Mid-limits, half-bands, worst case, RSS, σ by distribution, C_p and C_pk are correct.
- New: R1 (zero band from a magnitude-typed lower deviation) and R2 (Pareto ignores the distribution). Both Low, both reproduced with the probe.
- Residual doubt: none on the arithmetic.
