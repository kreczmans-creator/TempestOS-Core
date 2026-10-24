# Manual test — bs.pipe-pump (Pipe pressure drop and pump duty)
Liability: High. Time: ~45 min. Pre-requisites: a project open in the desktop app; the JSON sets at the end of this file.

How to open: press Ctrl+K, type `Open worksheet: Pipe pressure drop`, press Enter. Or use Engineering → Modules → Calculations → Worksheets, then pick the calculator under Building Services. The form recalculates at once. Results show five significant figures. "Show working" lists each formula step. The unit picker offers only matching units, so a wrong unit can only come in by import. Import reads the clipboard: copy the JSON, then press "Import inputs (CSV/JSON)". The form opens with the worked example of the first known-answer case, not the bare template defaults. To return to the template defaults, copy `[]` and press "Import inputs (CSV/JSON)". Start each step from a reset, so that values left from earlier steps cannot change the result. A step that says it continues from the last step is the exception.

| # | Step | Inputs (value + unit) | Expected output (symbol = value unit) | Tolerance | Expected PASS/FAIL/BLOCK/WARN | Basis | Result | Notes |
|---|---|---|---|---|---|---|---|---|
| 1 | Open the calculator. Copy JSON set A (below) and press "Import inputs (CSV/JSON)". The pipe segments, pump curve and fluid choice come in with it. | Custom fluid: rho = 888 kg/m^3; mu = 800 mPa*s; Q = 1 m^3/h; one segment 40 m long, 50 mm bore, smooth, no fittings | v_s1 = 0.14147 m/s; Re_s1 = 7.8516; f_s1 = 8.1512; h_f_total = 6.6542 m; dp_total = 57.947 kPa. Warning: segment is laminar. | ±0.1 % | PASS (velocity check) with WARN (laminar) | KAT ka-laminar-oil and independent script; Hagen-Poiseuille by hand: 32 x 0.8 x 40 x 0.14147 / 0.05^2 = 57.95 kPa |  | Display shows five significant figures. |
| 2 | Turbulent water against a solved Colebrook value. Import set B. | Custom fluid rho = 1000 kg/m^3; mu = 1 mPa*s; Q = 28.274334 m^3/h; 100 m of 100 mm pipe, roughness 0.1 mm | v_s1 = 1 m/s; Re_s1 = 100000; f_s1 = 0.022175; f_sj_s1 = 0.022342; h_f_total = 1.1306 m; dp_total = 11.087 kPa | ±0.1 % | PASS | KAT ka-turbulent-water and independent script (Colebrook solved to convergence) |  | f_sj is the explicit Swamee-Jain cross-check. |
| 3 | Duty point and affinity shift. Import set C (water 20 degC, fixed riser, pump curve, speed ratio 0.9). | Q = 30 m^3/h; riser 60 m, 80 mm, roughness 0.045 mm, sum K = 8, rise 10 m; pump (0,40), (20,36), (40,24), (60,4); n_ratio = 0.9 | H_static = 10 m; Q_duty = 46.3 m^3/h; H_duty = 17.67 m; Q_duty2 = 40.1 m^3/h | ±1 % | PASS (both duty-point checks) | KAT ka-duty-point and independent script |  | Product shows Q_duty 46.332 and Q_duty2 40.105. The water viscosity fit differs from the reference by up to 1 %. |
| 4 | Two segments, hot water, affinity with speed and diameter. Import set D. | Water 60 degC; Q = 50 m^3/h; suction segment 5 m x 100 mm (K 2, L_eq 1 m, dz -1.5 m); discharge segment 150 m x 80 mm (K 5, L_eq 10 m, dz +12 m); h_out 15 m; pump curve 0 to 100 m^3/h; n 0.9; D ratio 0.95; eta 0.65; p_atm 98 kPa; z_s 2 m | v_s1 = 1.7684 m/s; v_s2 = 2.7631 m/s; h_f_total = 16.524 m; H_static = 25.5 m; Q_duty = 56.73 m^3/h; H_duty = 46.77 m; P_shaft = 10.93 kW; Q_duty2 = 39.83 m^3/h; H_duty2 = 35.98 m; P_shaft2 = 5.905 kW; NPSH_a = 9.608 m | ±0.5 % | PASS | Independent script (probe A); v by hand: 50 / 3600 / (pi x 0.05^2) = 1.7684 m/s |  | The velocity values are exact. The rest depend on the water property fit, hence ±0.5 %. |
| 5 | Boundary: laminar side of the 2300 limit. Import set E. | Custom fluid rho 900, mu 20 mPa*s; 100 m of 50 mm pipe; Q = 7.222522 m^3/h (Re = 2299) | Re_s1 = 2299; f_s1 = 0.027838; h_f_total = 2.9637 m. Warning: laminar. | ±0.01 % | PASS with WARN (laminar) | Independent script (probe C1); f = 64 / 2299 |  | Re x mu x pi x D / (4 x rho) gives the flow. |
| 6 | Boundary: transitional side. Import set F. | Same fluid and pipe; Q = 7.228805 m^3/h (Re = 2301) | Re_s1 = 2301; f_s1 = 0.048001; h_f_total = 5.1192 m. Warning: transitional. | ±0.01 % | PASS with WARN (transition) | Independent script (probe C2; Colebrook solved to convergence) |  | The friction factor steps up by 72 % across Re = 2300. Both sides warn. |
| 7 | Boundary: very small flow. Import set H. | Water 20 degC; Q = 0.01 m^3/h; 10 m of 25 mm pipe | v_s1 = 0.0056588 m/s; Re_s1 = 140.7; f_s1 = 0.4548; h_f_total = 0.00029702 m. Warning: laminar. | ±0.5 % | PASS with WARN | Independent script (probe D): Re 140.99, f 0.4539, h_f 0.00029645 |  | The reference Re differs by 0.2 % because of the viscosity fit. |
| 8 | Boundary: boiling water on a suction line. Import set G. | Water 100 degC; Q = 10 m^3/h; 10 m x 50 mm suction (K 1); z_s = 0; NPSH_r = 3 m | NPSH_a = -0.5287 m (negative). The NPSH check fails. | ±0.002 m absolute | FAIL (NPSH check) | Independent script (probe B): -0.5285 m |  | Hand check: 101.325 kPa gives 10.781 m of head. Vapour pressure at 100 degC (101.42 kPa) takes 10.791 m. Friction takes 0.519 m. |
| 9 | Speed ratio above 1. Import set I. | KAT pump curve; riser with K 2 and 100 m; n_ratio = 1.3 | Q_duty = 45.375 m^3/h; Q_duty2 = 62.80 m^3/h; H_duty2 = 26.52 m | ±0.5 % | PASS | Independent script (probe V) |  | Affinity: Q scales with n, H with n squared. |
| 10 | NEGATIVE: out of range. Try each after importing set C: T = 101 degC; Q = 0; n_ratio = 11; eta_pump = 1.2. | See step | Block messages: for 101 degC, the water correlations hold for 0 to 100 degC. For Q = 0, Q must be at least 1e-9 m^3/h. For n_ratio = 11, at most 10. For eta_pump = 1.2, at most 1. | n/a | BLOCK | Template hard limits and rule water-range |  | A soft-range warning may show first. |
| 11 | NEGATIVE: unit mismatch. Import set C with `{"Symbol":"p_atm","Number":101.325,"Unit":"m"}` added. Then change a segment diameter unit to kg in the JSON. | p_atm = 101.325 m; segment D in kg | Block messages: p_atm must be entered in a unit of kPa, not m; segments row 1, Inner diameter must be in a unit of mm, not kg. | n/a | BLOCK | Hand check (a length is not a pressure) |  | The unit picker lists only matching units. |
| 12 | NEGATIVE: missing data. Import set A and remove rho_custom. Then import `[{"Symbol":"segments","Rows":[]}]`. Then import set C with the Q_pump list one point shorter than H_pump. | rho_custom blank; no segments; pump curve counts differ | Block messages: a custom fluid needs density and viscosity; at least one segment is needed; the pump curve needs at least two points with matching counts and increasing flows. | n/a | BLOCK | Template rules custom-fluid, segments and pump-curve |  |  |
| 13 | NEGATIVE: stale upstream. Import set C and save as "PP-Main" (Q_duty 46.332 m^3/h). Import set C again and save as "PP-Branch". Link Q of PP-Branch to PP-Main output Q_duty. In the Saved calculations panel select PP-Main and press Open. If the status line says the form has unsaved changes, press Open a second time to discard them. Change h_out from 0 to 5 m and press Save changes. Select PP-Branch and press Open. | PP-Main h_out 5 m (was 0 m) | PP-Branch first shows Q = 46.33 m^3/h and v_s1 = 2.560 m/s. Within a second the PP-Branch row in the Saved calculations list ends with · STALE. Select PP-Branch and press Open: status line Opened 'PP-Branch' · rev A · Draft. Orange banner: Stale — 'PP-Main' rev A now gives a different 'Q_duty' than the value 'Q' took. Button: Update linked inputs. Press Record check: status line reads Calculation 'PP-Branch' is stale: 'PP-Main' rev A now gives a different 'Q_duty' than the value 'Q' took. Update it before checking. The watermark stays DRAFT. Press Update linked inputs: the banner clears; Q takes the new Q_duty of PP-Main; the status line reads Linked inputs updated to the current revisions and recalculated. Q = 42.54 m^3/h; v_s1 = 2.3507 m/s; h_f_total = 6.32 m. | ±0.5 % | WARN (stale banner); BLOCK (check refused); then PASS after the update | Independent script (duty point at static head 15 m); code rule for staleness |  | Procedure L in README.md. Reopening uses the Saved calculations panel. An issued upstream goes stale the same way: Revise it, then the banner reads that the input is pinned to rev A, which is superseded by rev B. The Link panel offers only Q_duty and Q_duty2 for Q, because Q takes a flow in m^3/h. |
| 14 | Known defect P1-bs.pipe-pump-01. Custom fluid with NPSH required. Import set P. | Custom fluid rho 1050, mu 3 mPa*s; z_s = -8 m; NPSH_r = 3 m; suction segment | Correct: a notice that NPSH is not computed for a custom fluid, or a vapour-pressure input. Product shows no NPSH_a, no NPSH check and no notice. | n/a | WARN | P1 report |  | Known defect P1-bs.pipe-pump-01; expect FAIL until fixed. The sheet looks as if it passed. |
| 15 | Known defect P1-bs.pipe-pump-02. Impeller diameter ratio. Import set J. | D_ratio = 0.8; KAT pump curve; n_ratio = 1 | Product (similar-pump law, Q ~ D^3): Q_duty2 = 22.996 m^3/h; P_shaft2 = 1.0912 kW. Trim law (Q ~ D, H ~ D^2): Q_duty2 = 33.0 m^3/h; P_shaft2 = 1.866 kW. | ±0.5 % | FAIL | Independent script (trim law, finding evidence) |  | Known defect P1-bs.pipe-pump-02; expect FAIL until fixed. Pass means the label or the law is changed so the two agree. State the law you want. |
| 16 | Known defect P1-bs.pipe-pump-03. Laminar friction in the system curve. Import set K. | Oil rho 900, mu 200 mPa*s; Q = 5 m^3/h; 100 m of 50 mm; rise 2 m; pump (0,30), (5,27), (10,22), (15,14), (20,4) | Product: Q_duty = 5.467 m^3/h (friction scaled with Q squared). Correct for laminar flow (friction scaled with Q): 5.878 m^3/h. The laminar warning should say the duty point is approximate. | ±0.5 % | FAIL | Independent script (probe W) |  | Known defect P1-bs.pipe-pump-03; expect FAIL until fixed. The error is unconservative by about 7 %. |
| 17 | Known defect P1-bs.pipe-pump-04. Pump curve that crosses the system curve twice. Import set L. | Pump (0,8), (10,14), (40,18), (70,2); static head 10 m | Correct: two duty points, about 3.4 and 41.5 m^3/h, the stable one reported. Product says there is no crossing and fails the check. | ±1 % | FAIL | Independent script (probe R, scan for sign changes) |  | Known defect P1-bs.pipe-pump-04; expect FAIL until fixed. The failure is safe. |
| 18 | Known defect P1-bs.pipe-pump-07. Negative utilisation. Use set G. | Set G | NPSH check fails with u_npsh = -5.674. Correct: show infinity or n/a when NPSH_a is zero or negative, with a notice that the fluid is boiling. | n/a | FAIL | P1 report |  | Known defect P1-bs.pipe-pump-07; expect FAIL until fixed. |
| 19 | Known defect P1-bs.pipe-pump-R1. Friction left out of NPSH when no segment is marked suction. Import set M, then set N. | M: pump side blank. N: pump side = suction. Both: water 60 degC; 30 m^3/h; 60 m x 65 mm; K 6; z_s = -3 m; NPSH_r = 3 m | M: product NPSH_a = 5.440 m and the check passes with no notice. N: NPSH_a = -2.151 m and the check fails. Correct for M: a warning that no segment is marked suction. | ±0.5 % | WARN | Independent script (review R1) |  | Known defect P1-bs.pipe-pump-R1; expect FAIL until fixed. |
| 20 | Known defect P1-bs.pipe-pump-R2. NPSH at the duty flow. Import set O. | Water 60 degC; design 30 m^3/h; suction 10 m x 65 mm (K 4); discharge 100 m (K 6); pump (0,30), (20,28), (40,22), (60,10); z_s = -3 m; NPSH_r = 3 m | Product: Q_duty = 38.539 m^3/h; NPSH_a = 3.2105 m at the design flow; check passes. Correct: NPSH_a at the duty flow is 1.7785 m and the check fails. | ±0.5 % | FAIL | Independent script (review R2) |  | Known defect P1-bs.pipe-pump-R2; expect FAIL until fixed. |

Not tested here: P1 findings 05 (spec text) and 09 (display only). Findings 06 and 08 are Info.

## JSON sets

Each import resets every input not listed to its default. Copy one block, then press "Import inputs (CSV/JSON)".

Set A:

```json
[{"Symbol": "fluid", "Text": "custom (ρ and μ given)"}, {"Symbol": "rho_custom", "Number": 888, "Unit": "kg/m^3"}, {"Symbol": "mu_custom", "Number": 800, "Unit": "mPa*s"}, {"Symbol": "Q", "Number": 1, "Unit": "m^3/h"}, {"Symbol": "segments", "Rows": [{"Cells": {"name": {"Text": "oil line"}, "L": {"Number": 40, "Unit": "m"}, "D": {"Number": 50, "Unit": "mm"}, "eps": {"Number": 0, "Unit": "mm"}, "K": {"Number": 0}, "L_eq": {"Number": 0, "Unit": "m"}, "dz": {"Number": 0, "Unit": "m"}, "side": {"Text": "discharge"}}}]}]
```

Set B:

```json
[{"Symbol": "fluid", "Text": "custom (ρ and μ given)"}, {"Symbol": "rho_custom", "Number": 1000, "Unit": "kg/m^3"}, {"Symbol": "mu_custom", "Number": 1, "Unit": "mPa*s"}, {"Symbol": "Q", "Number": 28.274334, "Unit": "m^3/h"}, {"Symbol": "segments", "Rows": [{"Cells": {"name": {"Text": "main"}, "L": {"Number": 100, "Unit": "m"}, "D": {"Number": 100, "Unit": "mm"}, "eps": {"Number": 0.1, "Unit": "mm"}, "K": {"Number": 0}, "L_eq": {"Number": 0, "Unit": "m"}, "dz": {"Number": 0, "Unit": "m"}, "side": {"Text": "discharge"}}}]}]
```

Set C:

```json
[{"Symbol": "Q", "Number": 30, "Unit": "m^3/h"}, {"Symbol": "segments", "Rows": [{"Cells": {"name": {"Text": "riser"}, "L": {"Number": 60, "Unit": "m"}, "D": {"Number": 80, "Unit": "mm"}, "eps": {"Number": 0.045, "Unit": "mm"}, "K": {"Number": 8}, "L_eq": {"Number": 0, "Unit": "m"}, "dz": {"Number": 10, "Unit": "m"}, "side": {"Text": "discharge"}}}]}, {"Symbol": "Q_pump", "Vector": [0, 20, 40, 60], "Unit": "m^3/h"}, {"Symbol": "H_pump", "Vector": [40, 36, 24, 4], "Unit": "m"}, {"Symbol": "n_ratio", "Number": 0.9}]
```

Set D:

```json
[{"Symbol": "T", "Number": 60, "Unit": "degC"}, {"Symbol": "Q", "Number": 50, "Unit": "m^3/h"}, {"Symbol": "segments", "Rows": [{"Cells": {"name": {"Text": "suction"}, "L": {"Number": 5, "Unit": "m"}, "D": {"Number": 100, "Unit": "mm"}, "eps": {"Number": 0.045, "Unit": "mm"}, "K": {"Number": 2}, "L_eq": {"Number": 1, "Unit": "m"}, "dz": {"Number": -1.5, "Unit": "m"}, "side": {"Text": "suction"}}}, {"Cells": {"name": {"Text": "discharge"}, "L": {"Number": 150, "Unit": "m"}, "D": {"Number": 80, "Unit": "mm"}, "eps": {"Number": 0.045, "Unit": "mm"}, "K": {"Number": 5}, "L_eq": {"Number": 10, "Unit": "m"}, "dz": {"Number": 12, "Unit": "m"}, "side": {"Text": "discharge"}}}]}, {"Symbol": "h_out", "Number": 15, "Unit": "m"}, {"Symbol": "Q_pump", "Vector": [0, 25, 50, 75, 100], "Unit": "m^3/h"}, {"Symbol": "H_pump", "Vector": [60, 57, 50, 38, 20], "Unit": "m"}, {"Symbol": "n_ratio", "Number": 0.9}, {"Symbol": "D_ratio", "Number": 0.95}, {"Symbol": "eta_pump", "Number": 0.65}, {"Symbol": "p_atm", "Number": 98, "Unit": "kPa"}, {"Symbol": "z_s", "Number": 2, "Unit": "m"}]
```

Set E:

```json
[{"Symbol": "fluid", "Text": "custom (ρ and μ given)"}, {"Symbol": "rho_custom", "Number": 900, "Unit": "kg/m^3"}, {"Symbol": "mu_custom", "Number": 20, "Unit": "mPa*s"}, {"Symbol": "Q", "Number": 7.222522, "Unit": "m^3/h"}, {"Symbol": "segments", "Rows": [{"Cells": {"name": {"Text": "pipe"}, "L": {"Number": 100, "Unit": "m"}, "D": {"Number": 50, "Unit": "mm"}, "eps": {"Number": 0.045, "Unit": "mm"}, "K": {"Number": 0}, "L_eq": {"Number": 0, "Unit": "m"}, "dz": {"Number": 0, "Unit": "m"}, "side": {"Text": "discharge"}}}]}]
```

Set F:

```json
[{"Symbol": "fluid", "Text": "custom (ρ and μ given)"}, {"Symbol": "rho_custom", "Number": 900, "Unit": "kg/m^3"}, {"Symbol": "mu_custom", "Number": 20, "Unit": "mPa*s"}, {"Symbol": "Q", "Number": 7.228805, "Unit": "m^3/h"}, {"Symbol": "segments", "Rows": [{"Cells": {"name": {"Text": "pipe"}, "L": {"Number": 100, "Unit": "m"}, "D": {"Number": 50, "Unit": "mm"}, "eps": {"Number": 0.045, "Unit": "mm"}, "K": {"Number": 0}, "L_eq": {"Number": 0, "Unit": "m"}, "dz": {"Number": 0, "Unit": "m"}, "side": {"Text": "discharge"}}}]}]
```

Set G:

```json
[{"Symbol": "T", "Number": 100, "Unit": "degC"}, {"Symbol": "Q", "Number": 10, "Unit": "m^3/h"}, {"Symbol": "segments", "Rows": [{"Cells": {"name": {"Text": "s"}, "L": {"Number": 10, "Unit": "m"}, "D": {"Number": 50, "Unit": "mm"}, "eps": {"Number": 0.045, "Unit": "mm"}, "K": {"Number": 1}, "L_eq": {"Number": 0, "Unit": "m"}, "dz": {"Number": 0, "Unit": "m"}, "side": {"Text": "suction"}}}]}, {"Symbol": "z_s", "Number": 0, "Unit": "m"}, {"Symbol": "NPSH_r", "Number": 3, "Unit": "m"}]
```

Set H:

```json
[{"Symbol": "Q", "Number": 0.01, "Unit": "m^3/h"}, {"Symbol": "segments", "Rows": [{"Cells": {"name": {"Text": "s"}, "L": {"Number": 10, "Unit": "m"}, "D": {"Number": 25, "Unit": "mm"}, "eps": {"Number": 0.045, "Unit": "mm"}, "K": {"Number": 0}, "L_eq": {"Number": 0, "Unit": "m"}, "dz": {"Number": 0, "Unit": "m"}, "side": {"Text": "discharge"}}}]}]
```

Set I:

```json
[{"Symbol": "Q", "Number": 30, "Unit": "m^3/h"}, {"Symbol": "segments", "Rows": [{"Cells": {"name": {"Text": "pipe"}, "L": {"Number": 100, "Unit": "m"}, "D": {"Number": 80, "Unit": "mm"}, "eps": {"Number": 0.045, "Unit": "mm"}, "K": {"Number": 2}, "L_eq": {"Number": 0, "Unit": "m"}, "dz": {"Number": 10, "Unit": "m"}, "side": {"Text": "discharge"}}}]}, {"Symbol": "Q_pump", "Vector": [0, 20, 40, 60], "Unit": "m^3/h"}, {"Symbol": "H_pump", "Vector": [40, 36, 24, 4], "Unit": "m"}, {"Symbol": "n_ratio", "Number": 1.3}]
```

Set J:

```json
[{"Symbol": "Q", "Number": 30, "Unit": "m^3/h"}, {"Symbol": "segments", "Rows": [{"Cells": {"name": {"Text": "pipe"}, "L": {"Number": 100, "Unit": "m"}, "D": {"Number": 80, "Unit": "mm"}, "eps": {"Number": 0.045, "Unit": "mm"}, "K": {"Number": 2}, "L_eq": {"Number": 0, "Unit": "m"}, "dz": {"Number": 10, "Unit": "m"}, "side": {"Text": "discharge"}}}]}, {"Symbol": "Q_pump", "Vector": [0, 20, 40, 60], "Unit": "m^3/h"}, {"Symbol": "H_pump", "Vector": [40, 36, 24, 4], "Unit": "m"}, {"Symbol": "D_ratio", "Number": 0.8}]
```

Set K:

```json
[{"Symbol": "fluid", "Text": "custom (ρ and μ given)"}, {"Symbol": "rho_custom", "Number": 900, "Unit": "kg/m^3"}, {"Symbol": "mu_custom", "Number": 200, "Unit": "mPa*s"}, {"Symbol": "Q", "Number": 5, "Unit": "m^3/h"}, {"Symbol": "segments", "Rows": [{"Cells": {"name": {"Text": "oil"}, "L": {"Number": 100, "Unit": "m"}, "D": {"Number": 50, "Unit": "mm"}, "eps": {"Number": 0.045, "Unit": "mm"}, "K": {"Number": 0}, "L_eq": {"Number": 0, "Unit": "m"}, "dz": {"Number": 2, "Unit": "m"}, "side": {"Text": "discharge"}}}]}, {"Symbol": "Q_pump", "Vector": [0, 5, 10, 15, 20], "Unit": "m^3/h"}, {"Symbol": "H_pump", "Vector": [30, 27, 22, 14, 4], "Unit": "m"}]
```

Set L:

```json
[{"Symbol": "Q", "Number": 30, "Unit": "m^3/h"}, {"Symbol": "segments", "Rows": [{"Cells": {"name": {"Text": "pipe"}, "L": {"Number": 100, "Unit": "m"}, "D": {"Number": 80, "Unit": "mm"}, "eps": {"Number": 0.045, "Unit": "mm"}, "K": {"Number": 2}, "L_eq": {"Number": 0, "Unit": "m"}, "dz": {"Number": 10, "Unit": "m"}, "side": {"Text": "discharge"}}}]}, {"Symbol": "Q_pump", "Vector": [0, 10, 40, 70], "Unit": "m^3/h"}, {"Symbol": "H_pump", "Vector": [8, 14, 18, 2], "Unit": "m"}]
```

Set M:

```json
[{"Symbol": "T", "Number": 60, "Unit": "degC"}, {"Symbol": "Q", "Number": 30, "Unit": "m^3/h"}, {"Symbol": "segments", "Rows": [{"Cells": {"name": {"Text": "line"}, "L": {"Number": 60, "Unit": "m"}, "D": {"Number": 65, "Unit": "mm"}, "eps": {"Number": 0.045, "Unit": "mm"}, "K": {"Number": 6}, "L_eq": {"Number": 0, "Unit": "m"}, "dz": {"Number": 0, "Unit": "m"}}}]}, {"Symbol": "z_s", "Number": -3, "Unit": "m"}, {"Symbol": "NPSH_r", "Number": 3, "Unit": "m"}]
```

Set N:

```json
[{"Symbol": "T", "Number": 60, "Unit": "degC"}, {"Symbol": "Q", "Number": 30, "Unit": "m^3/h"}, {"Symbol": "segments", "Rows": [{"Cells": {"name": {"Text": "line"}, "L": {"Number": 60, "Unit": "m"}, "D": {"Number": 65, "Unit": "mm"}, "eps": {"Number": 0.045, "Unit": "mm"}, "K": {"Number": 6}, "L_eq": {"Number": 0, "Unit": "m"}, "dz": {"Number": 0, "Unit": "m"}, "side": {"Text": "suction"}}}]}, {"Symbol": "z_s", "Number": -3, "Unit": "m"}, {"Symbol": "NPSH_r", "Number": 3, "Unit": "m"}]
```

Set O:

```json
[{"Symbol": "T", "Number": 60, "Unit": "degC"}, {"Symbol": "Q", "Number": 30, "Unit": "m^3/h"}, {"Symbol": "segments", "Rows": [{"Cells": {"name": {"Text": "suction"}, "L": {"Number": 10, "Unit": "m"}, "D": {"Number": 65, "Unit": "mm"}, "eps": {"Number": 0.045, "Unit": "mm"}, "K": {"Number": 4}, "L_eq": {"Number": 0, "Unit": "m"}, "dz": {"Number": 0, "Unit": "m"}, "side": {"Text": "suction"}}}, {"Cells": {"name": {"Text": "discharge"}, "L": {"Number": 100, "Unit": "m"}, "D": {"Number": 65, "Unit": "mm"}, "eps": {"Number": 0.045, "Unit": "mm"}, "K": {"Number": 6}, "L_eq": {"Number": 0, "Unit": "m"}, "dz": {"Number": 0, "Unit": "m"}, "side": {"Text": "discharge"}}}]}, {"Symbol": "Q_pump", "Vector": [0, 20, 40, 60], "Unit": "m^3/h"}, {"Symbol": "H_pump", "Vector": [30, 28, 22, 10], "Unit": "m"}, {"Symbol": "z_s", "Number": -3, "Unit": "m"}, {"Symbol": "NPSH_r", "Number": 3, "Unit": "m"}]
```

Set P:

```json
[{"Symbol": "fluid", "Text": "custom (ρ and μ given)"}, {"Symbol": "rho_custom", "Number": 1050, "Unit": "kg/m^3"}, {"Symbol": "mu_custom", "Number": 3, "Unit": "mPa*s"}, {"Symbol": "Q", "Number": 30, "Unit": "m^3/h"}, {"Symbol": "segments", "Rows": [{"Cells": {"name": {"Text": "line"}, "L": {"Number": 60, "Unit": "m"}, "D": {"Number": 65, "Unit": "mm"}, "eps": {"Number": 0.045, "Unit": "mm"}, "K": {"Number": 6}, "L_eq": {"Number": 0, "Unit": "m"}, "dz": {"Number": 0, "Unit": "m"}, "side": {"Text": "suction"}}}]}, {"Symbol": "z_s", "Number": -8, "Unit": "m"}, {"Symbol": "NPSH_r", "Number": 3, "Unit": "m"}]
```

Set Q:

```json
[{"Symbol": "Q", "Number": 30, "Unit": "m^3/h"}, {"Symbol": "segments", "Rows": [{"Cells": {"name": {"Text": "riser"}, "L": {"Number": 60, "Unit": "m"}, "D": {"Number": 80, "Unit": "mm"}, "eps": {"Number": 0.045, "Unit": "mm"}, "K": {"Number": 8}, "L_eq": {"Number": 0, "Unit": "m"}, "dz": {"Number": 10, "Unit": "m"}, "side": {"Text": "discharge"}}}]}, {"Symbol": "h_out", "Number": 5, "Unit": "m"}, {"Symbol": "Q_pump", "Vector": [0, 20, 40, 60], "Unit": "m^3/h"}, {"Symbol": "H_pump", "Vector": [40, 36, 24, 4], "Unit": "m"}]
```


