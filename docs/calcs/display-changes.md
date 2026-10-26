# Display changes of the C-2 fix (phase A)

This page lists what a reader of a sheet sees differently after the review fix of C-2 phase A (decisions C2-20 to C2-28 in [`sheet-standard.md`](sheet-standard.md)).
No calculated value changes except for the five templates of item M1 (C2-24); a displayed number changes, its value, its SI value, its stored output, its hash and every
export stay as they were. "Old" is what phase A printed, "new" is what the sheet prints now. Every example below is a line of a regenerated worked example or a case of
`DisplayRulesTests`.

## 1. Significant figures (C2-21)

A result is shown with at least four and at most five significant figures; the decimals of the quantity type are the least shown. A whole number is printed exactly.

| Output | Old | New |
|---|---|---|
| `theta_se` (insulation) | `25.3 °C` | `25.27 °C` |
| `sigma_run` (frame) | `0.086 MPa` | `0.08606 MPa` |
| `tau_max` (lifting lug) | `67.5 MPa` | `67.55 MPa` |
| `theta_A` (shaft) | `0.00090 rad` | `0.0008961 rad` |
| `R_A` (shaft) | `3.20 kN` | `3.195 kN` |
| `m_min` (tolerance stack) | `0.080 mm` | `0.08000 mm` |
| `m_min` (tolerance stack, inch) | `-0.0080 in` | `-0.008000 in` |
| `X` (voltage drop) | `0.0064 Ω` | `0.006400 Ω` |
| `d_A` (shaft) | `35.0 mm` | `35 mm` |
| `sigma_r_a` (thick cylinder) | `-50.0 MPa` | `-50 MPa` |
| `mu_0` (mechanism) | `90.00°` | `90°` |
| a duration of 150000 h, a volume of 13824 m³ | not printed exactly | `150000 h`, `13824 m³` |

## 2. Safety factors and "at least" ratios round down (C2-21)

An output a template declares `AtLeast` (`c.MarkAtLeast(...)`: a safety factor, a margin, a ratio that must reach a target) is rounded down, so the text never reads at
or above its target when the value is below it.

| Value | Target | Old | New |
|---|---|---|---|
| `n_f_min` 1.69081 | 1.5 | `1.69` | `1.6908` |
| `n_y_min` 4.15309 | 1.5 | `4.15` | `4.1531` |
| `n_f` 1.5578 (spring) | 1.5 | `1.56` | `1.5578` |
| a safety factor of 1.4999 | 1.5 | `1.50` | `1.4999` |
| a safety factor of 1.495 | 1.5 | `1.50` | `1.495` |

## 3. Utilisation (C2-21)

A utilisation is what a template declares (`c.MarkUtilisation(...)`) or the symbol a utilisation check is made on, never what its symbol or its description says. It is two
decimals, rounded up whenever it exceeds its own limit, away from zero when negative, and two significant figures below 0.005.

| Value | Old | New |
|---|---|---|
| u = 1.004 | `1.00` (a failing check read as 1.00) | `1.01` |
| u = 0.504 against a limit of 0.5 (`CheckUtilisation(..., limit: 0.5)`) | `0.50` | `0.51` |
| u = -1.004 | `-1.00` | `-1.01` |
| u = 0.00022 | `0.00` | `0.00022` |
| `u_ratio`, "Factor applied to utilisation" | shown as a utilisation | a plain number |
| a failing check line, demand 40.004° against 40° | `40.00°` against `40°` | `40.01°` against `40°` |

## 4. Zero for solver noise (C2-20)

A value is shown as `0` for being small only when its template flags it noise-prone, and then only against the working values of its own dimension and unit in the same run.
Phase A compared any output with every other output of the same dimension and so printed real values as `0`.

| Case | Old | New |
|---|---|---|
| `Z_x` 3.1595 cm³ beside `S_run` 13824 m³ | `0 cm³` | `3.160 cm³` |
| 0.4 ms beside 150000 h | zeroed as noise | `0.4000 ms` |
| 3 mN·m beside 5 MJ | zeroed as noise | printed as it is, not zeroed |
| `sigma_r_b` of a thick cylinder, 1.8626e-15 MPa beside 144.9 MPa (flagged) | `0 MPa` | `0 MPa` |

## 5. One formatter for the form, the banner and the register (C2-22)

| Where | Old | New |
|---|---|---|
| governing banner, `u` = 1.2516 | `1.25` (three figures, not rounded up) | `1.26` (the figure the sheet prints) |
| register, stored utilisation 1.2516 | `1.25` | `1.26` |
| a yes/no check on the form | `u = 0` | the check text of the sheet (`FormCheckText`) |
| `docs/testing/v0.26.0-test-values.md`, Expected column | `kN*m`, `deg` | `kN·m`, `°` |

## 6. Units in table cells and ranges (L2)

Table-cell and range units are written with the unit symbols (`m³`, `kN·m`, `°C`), as the other units of the sheet are.

## 7. Not changed

The README expectation tables of the worked examples hold the same numbers. The pack READMEs change only where a check was added by item M1 (`4 of 4 PASS` becomes
`5 of 5 PASS` for `elec.motor-sizing` and `mech.shaft`, and for `mech.spring` in the mechanism-review pack), and the voltage-drop, shaft and press-fit hand arithmetic of their
spec pages follows the templates it describes.
