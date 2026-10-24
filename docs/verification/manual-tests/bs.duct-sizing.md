# Manual test — bs.duct-sizing (Duct sizing)
Liability: Medium. Time: ~40 min. Pre-requisites: a project open in the desktop app; the JSON sets at the end of this file; the heat-exchanger worksheet for step 16.

How to open: press Ctrl+K, type `Open worksheet: Duct sizing`, press Enter. Or use Engineering → Modules → Calculations → Worksheets, then pick the calculator under Building Services. The form recalculates at once. Results show five significant figures. "Show working" lists each formula step. The unit picker offers only matching units, so a wrong unit can only come in by import. Import reads the clipboard: copy the JSON, then press "Import inputs (CSV/JSON)". The form opens with the worked example of the first known-answer case, not the bare template defaults. To return to the template defaults, copy `[]` and press "Import inputs (CSV/JSON)". Start each step from a reset, so that values left from earlier steps cannot change the result. A step that says it continues from the last step is the exception.

| # | Step | Inputs (value + unit) | Expected output (symbol = value unit) | Tolerance | Expected PASS/FAIL/BLOCK/WARN | Basis | Result | Notes |
|---|---|---|---|---|---|---|---|---|
| 1 | Open the calculator. Copy JSON set A (below) and press "Import inputs (CSV/JSON)". The duct sections table has no form field, so import is the only way to enter sections. | Method = equal friction; T = 20 degC; eps = 0.09 mm; one round section: 1 m^3/s, 10 m, 400 mm given | v_s1 = 7.9577 m/s; Re_s1 = 211370; f_s1 = 0.017137; dpL_s1 = 1.6333 Pa/m; dp_s1 = 16.333 Pa; dp_index = 16.333 Pa | ±0.05 % | PASS (velocity u = 0.99472, drop u = 0.054445) | KAT ka-round-given and independent script |  | Hand check: v = 4 x 1 / (pi x 0.4^2) = 7.9577 m/s. |
| 2 | Rectangular duct through its equivalent diameter. Import set B. | One rectangular section: 0.8 m^3/s, 12 m, 600 x 300 mm, C = 0.5 | De_s1 = 457.01 mm; v_s1 = 4.4444 m/s; dpL_s1 = 0.55875 Pa/m; dp_f_s1 = 6.705 Pa; dp_fit_s1 = 5.9462 Pa; dp_s1 = 12.651 Pa | ±0.05 % | PASS | KAT ka-rectangular-equivalent (expects De 457.3, dp/L 0.556) and independent script |  | Hand check: De = 1.30 x (0.6 x 0.3)^0.625 / (0.9)^0.25 = 0.45701 m. The KAT values are loose (P1-bs.duct-sizing-02). |
| 3 | Velocity method, round. Import set C. | Method = velocity; v_target = 6 m/s; round_to = 50 mm; one round section 0.5 m^3/s, 8 m, C = 1.2, equipment 50 Pa; size blank | D_s1 = 350 mm; v_s1 = 5.1969 m/s; dp_fit_s1 = 19.512 Pa; dp_s1 = 76.468 Pa | ±0.05 % | PASS | KAT ka-velocity-method (expects dp_fit 19.47) and independent script |  | Hand check: D = sqrt(4 x 0.5 / (pi x 6)) = 325.7 mm, rounded up to 350. |
| 4 | Equal-friction sizing and the rounding step. Import set D, then D10, then D1. | 0.3 m^3/s, 20 m, target 1 Pa/m; round_to = 50, then 10, then 1 | D_s1 = 300 mm (dpL 0.72275 Pa/m, v 4.2441 m/s); then 290 mm (0.85345, 4.5419); then 281 mm (0.99631, 4.8375) | ±0.05 % | PASS | Independent script (probes P1, P2, P17) |  | Each size is the smallest multiple of the step that keeps the rate at or below 1 Pa/m. |
| 5 | Rectangular duct with one side fixed. Import set E. | 0.8 m^3/s, 20 m, a = 500 mm fixed, b blank, target 1 Pa/m, step 50 | b_s1 = 300 mm; De_s1 = 419.98 mm; v_s1 = 5.3333 m/s; dpL_s1 = 0.84678 Pa/m; dp_s1 = 16.936 Pa | ±0.05 % | PASS | Independent script (probe P3) |  |  |
| 6 | Flat oval given. Import set F, then swap the sides (a = 400, D = 800). | 0.8 m^3/s, 20 m, flat oval 800 x 400 | De_s1 = 591.50 mm; v_s1 = 2.8005 m/s; dpL_s1 = 0.15823 Pa/m. The swapped entry gives the same results. | ±0.05 % | PASS | Independent script (probes P5, P15) |  | The swap is silent (see P1-bs.duct-sizing-03). |
| 7 | Three sections, index run and branch. Import set G. | s1 round 1.2 m^3/s (C 0.3, 20 Pa); s2 rectangular a = 400, 0.7 m^3/s (C 1.0); s3 round 0.5 m^3/s (C 2.0, 30 Pa), not on the index run; target 1 Pa/m | D_s1 = 500 mm, dp_s1 = 38.169 Pa; b_s2 = 350 mm, dp_s2 = 22.601 Pa; D_s3 = 350 mm, dp_s3 = 69.476 Pa; dp_index = 60.770 Pa; dp_all = 130.246 Pa | ±0.05 % | PASS | Independent script (probe P11) |  | dp_index = dp_s1 + dp_s2 only. |
| 8 | Boundary: laminar flow. Import set H. | 0.001 m^3/s in a given 400 mm duct | v_s1 = 0.0079577 m/s; Re_s1 = 211.4; f_s1 = 0.3028; dpL_s1 = 0.00002886 Pa/m. A warning says Re is below 4000. | ±0.1 % | PASS with WARN | Independent script (probe P8) |  | Hand check: f = 64 / Re. |
| 9 | Boundary: very large duct. Import set I. | 100 m^3/s, 50 m, target 0.3 Pa/m, step 50 | D_s1 = 3250 mm; v_s1 = 12.054 m/s; dpL_s1 = 0.29487 Pa/m; dp_s1 = 14.743 Pa. Velocity u = 1.5068 and a velocity warning. | ±0.2 % | FAIL (velocity) with WARN | Independent script (probe P10) |  |  |
| 10 | Boundary: hot air outside the usual range. Import set T. | T = 200 degC; set A section | dpL_s1 = 1.1444 Pa/m. A warning says T is outside its usual range -20 to 60 degC. | ±0.1 % | PASS with WARN | Independent script (probe P14b) |  | Hotter air is less dense, so the rate drops from 1.6333. |
| 11 | Velocity limit. Import set V. | 1 m^3/s in a given 300 mm duct; v_limit = 8 m/s | v_s1 = 14.147 m/s; dpL_s1 = 6.8591 Pa/m. The velocity check fails (u = 1.7684) with a warning. | ±0.05 % | FAIL (velocity) with WARN | Hand formula: v = 4 / (pi x 0.3^2) = 14.147 m/s; dp/L from the independent script |  | Hand check is exact for v. |
| 12 | Pressure-drop limit. Import set P. | dp_limit = 10 Pa; set A section | dp_index = 16.333 Pa; the drop check fails (u = 1.6333). A warning says dp_limit is outside its usual range. | ±0.05 % | FAIL (drop) with WARN | Independent script (probe KAT) |  |  |
| 13 | NEGATIVE: out of range. Try each after importing set A: Q = 0 m^3/s; D = 5 mm. | See step | Block messages: sections[1].Q must be at least 0.000001 m^3/s; sections[1].D must be at least 10 mm. | n/a | BLOCK | Template hard limits |  |  |
| 14 | NEGATIVE: unit mismatch. Copy set A with T replaced by `{"Symbol":"T","Number":20,"Unit":"kPa"}` and import it. Then import set A with the section airflow in kg. | T = 20 kPa; Q = 1 kg | Block messages: T must be entered in a unit of degC, not kPa; sections row 1, Airflow must be in a unit of m^3/s, not kg. | n/a | BLOCK | Hand check (a pressure is not a temperature) |  | The unit picker lists only matching units. |
| 15 | NEGATIVE: wrong geometry and no sections. Import set A with a = 300 added to the round section. Then import `[{"Symbol":"sections","Rows":[]}]`. | Round duct with a fixed side; empty table | Block messages: the section is round and a fixed side needs a rectangular or flat-oval shape; at least one duct section is needed. | n/a | BLOCK | Template rules geometry and sections |  |  |
| 16 | NEGATIVE: stale upstream (air heated by an exchanger). Open the heat-exchanger worksheet, save the default case as "HX-1" (T_c_out = 50 degC). Open this worksheet, import set A, save as "DUCT-1". Link T of DUCT-1 to output T_c_out of HX-1. Read the results. Then in the Saved calculations panel select HX-1 and press Open. If the status line says the form has unsaved changes, press Open a second time to discard them. Change A from 4.18 to 8.36 m^2 and press Save changes. Select DUCT-1 in the panel and press Open. | HX-1 A 8.36 m^2 (was 4.18 m^2), so T_c_out = 60 degC (bs.heat-exchanger.md step 15) | Before the change DUCT-1 shows T = 50 degC: f = 0.01753; dpL = 1.5157 Pa/m; dp = 15.157 Pa. Within a second the DUCT-1 row in the Saved calculations list ends with · STALE. Select DUCT-1 and press Open: status line Opened 'DUCT-1' · rev A · Draft. Orange banner: Stale — 'HX-1' rev A now gives a different 'T_c_out' than the value 'T' took. Button: Update linked inputs. Press Record check: status line reads Calculation 'DUCT-1' is stale: 'HX-1' rev A now gives a different 'T_c_out' than the value 'T' took. Update it before checking. The watermark stays DRAFT. Press Update linked inputs: the banner clears; T takes the new T_c_out of HX-1; the status line reads Linked inputs updated to the current revisions and recalculated. T = 60 degC; f = 0.017659; dpL = 1.4811 Pa/m; dp = 14.811 Pa. | ±0.1 % | WARN (stale banner); BLOCK (check refused); then PASS after the update | Independent script (probe set at T = 50 and 60 degC); code rule for staleness (CalcInstanceService.CheckAsync and StalenessAsync) |  | Procedure L in README.md. Reopening uses the Saved calculations panel. An issued upstream goes stale the same way: Revise it, then the banner reads that the input is pinned to rev A, which is superseded by rev B. The link joins two worksheets for test purposes. Step 15 in bs.heat-exchanger.md gives the HX-1 change. |
| 17 | Known defect P1-bs.duct-sizing-01. Velocity method on a rectangular duct. Import set J, then set K (flat oval). | Method = velocity; v_target = 6 m/s; step 10; a = 500 mm; 0.5 m^3/s | Product: rectangular b = 190 mm and v = 5.2632 m/s; oval b = 200 mm and v = 5.4695 m/s. Correct (area = Q / v): rectangular b = 170 mm and v = 5.882 m/s; oval b = 190 mm and v = 5.73 m/s. | ±0.5 % | FAIL | Independent script (area rule); hand check: 0.5 / 6 / 0.5 = 0.1667 m, rounded up to 170 mm |  | Known defect P1-bs.duct-sizing-01; expect FAIL until fixed. The error is conservative (duct too large). |
| 18 | Known defect P1-bs.duct-sizing-03. Aspect ratio and default side. Import set L, then set O. | L: rectangular 1000 x 100 mm, 0.8 m^3/s. O: rectangular, a blank, b = 300 mm | Product: L runs with no note on a 10:1 aspect ratio (v = 8 m/s, dpL = 4.4179 Pa/m). O gives a = 650 mm with no note on how a was chosen. Correct: a warning above the chosen aspect-ratio limit, and a note on the default. | n/a | WARN | P1 report; independent script (probe P18) |  | Known defect P1-bs.duct-sizing-03; expect FAIL until fixed. |
| 19 | Known defect P1-bs.duct-sizing-04. Sizing outside 20 to 5000 mm. Import set M, then set N. | M: 400 m^3/s at 0.3 Pa/m. N: 0.0001 m^3/s at 5 Pa/m | Product: Block "engine: The bracket does not contain a sign change; no root was found". Correct: a result (D = 5508 mm for M; D = 11 mm for N) or a clear message that the size is outside 20 to 5000 mm. | ±0.5 % | BLOCK | Independent script (finding evidence) |  | Known defect P1-bs.duct-sizing-04; expect FAIL until fixed. Only the message is poor. |

Not tested here: P1 findings 02 (KAT wording), 05 and 06 (Info).

## JSON sets

Each import resets every input not listed to its default. Copy one block, then press "Import inputs (CSV/JSON)".

Set A:

```json
[{"Symbol": "method", "Text": "equal friction"}, {"Symbol": "T", "Number": 20, "Unit": "degC"}, {"Symbol": "eps", "Number": 0.09, "Unit": "mm"}, {"Symbol": "sections", "Rows": [{"Cells": {"name": {"Text": "main"}, "Q": {"Number": 1.0, "Unit": "m^3/s"}, "L": {"Number": 10, "Unit": "m"}, "shape": {"Text": "round"}, "D": {"Number": 400, "Unit": "mm"}, "index": {"Flag": true}}}]}]
```

Set B:

```json
[{"Symbol": "method", "Text": "equal friction"}, {"Symbol": "T", "Number": 20, "Unit": "degC"}, {"Symbol": "sections", "Rows": [{"Cells": {"name": {"Text": "branch"}, "Q": {"Number": 0.8, "Unit": "m^3/s"}, "L": {"Number": 12, "Unit": "m"}, "shape": {"Text": "rectangular"}, "a": {"Number": 600, "Unit": "mm"}, "D": {"Number": 300, "Unit": "mm"}, "C": {"Number": 0.5}, "index": {"Flag": true}}}]}]
```

Set C:

```json
[{"Symbol": "method", "Text": "velocity"}, {"Symbol": "v_target", "Number": 6, "Unit": "m/s"}, {"Symbol": "round_to", "Number": 50, "Unit": "mm"}, {"Symbol": "sections", "Rows": [{"Cells": {"name": {"Text": "riser"}, "Q": {"Number": 0.5, "Unit": "m^3/s"}, "L": {"Number": 8, "Unit": "m"}, "shape": {"Text": "round"}, "C": {"Number": 1.2}, "dp_eq": {"Number": 50, "Unit": "Pa"}, "index": {"Flag": true}}}]}]
```

Set D:

```json
[{"Symbol": "dp_target", "Number": 1, "Unit": "Pa/m"}, {"Symbol": "round_to", "Number": 50, "Unit": "mm"}, {"Symbol": "sections", "Rows": [{"Cells": {"name": {"Text": "main"}, "Q": {"Number": 0.3, "Unit": "m^3/s"}, "L": {"Number": 20, "Unit": "m"}, "shape": {"Text": "round"}, "index": {"Flag": true}}}]}]
```

Set D10:

```json
[{"Symbol": "dp_target", "Number": 1, "Unit": "Pa/m"}, {"Symbol": "round_to", "Number": 10, "Unit": "mm"}, {"Symbol": "sections", "Rows": [{"Cells": {"name": {"Text": "main"}, "Q": {"Number": 0.3, "Unit": "m^3/s"}, "L": {"Number": 20, "Unit": "m"}, "shape": {"Text": "round"}, "index": {"Flag": true}}}]}]
```

Set D1:

```json
[{"Symbol": "dp_target", "Number": 1, "Unit": "Pa/m"}, {"Symbol": "round_to", "Number": 1, "Unit": "mm"}, {"Symbol": "sections", "Rows": [{"Cells": {"name": {"Text": "main"}, "Q": {"Number": 0.3, "Unit": "m^3/s"}, "L": {"Number": 20, "Unit": "m"}, "shape": {"Text": "round"}, "index": {"Flag": true}}}]}]
```

Set E:

```json
[{"Symbol": "dp_target", "Number": 1, "Unit": "Pa/m"}, {"Symbol": "sections", "Rows": [{"Cells": {"name": {"Text": "branch"}, "Q": {"Number": 0.8, "Unit": "m^3/s"}, "L": {"Number": 20, "Unit": "m"}, "shape": {"Text": "rectangular"}, "a": {"Number": 500, "Unit": "mm"}, "index": {"Flag": true}}}]}]
```

Set F:

```json
[{"Symbol": "sections", "Rows": [{"Cells": {"name": {"Text": "branch"}, "Q": {"Number": 0.8, "Unit": "m^3/s"}, "L": {"Number": 20, "Unit": "m"}, "shape": {"Text": "flat oval"}, "a": {"Number": 800, "Unit": "mm"}, "D": {"Number": 400, "Unit": "mm"}, "index": {"Flag": true}}}]}]
```

Set G:

```json
[{"Symbol": "dp_target", "Number": 1, "Unit": "Pa/m"}, {"Symbol": "sections", "Rows": [{"Cells": {"name": {"Text": "s1"}, "Q": {"Number": 1.2, "Unit": "m^3/s"}, "L": {"Number": 15, "Unit": "m"}, "shape": {"Text": "round"}, "C": {"Number": 0.3}, "dp_eq": {"Number": 20, "Unit": "Pa"}, "index": {"Flag": true}}}, {"Cells": {"name": {"Text": "s2"}, "Q": {"Number": 0.7, "Unit": "m^3/s"}, "L": {"Number": 10, "Unit": "m"}, "shape": {"Text": "rectangular"}, "a": {"Number": 400, "Unit": "mm"}, "C": {"Number": 1.0}, "index": {"Flag": true}}}, {"Cells": {"name": {"Text": "s3"}, "Q": {"Number": 0.5, "Unit": "m^3/s"}, "L": {"Number": 8, "Unit": "m"}, "shape": {"Text": "round"}, "C": {"Number": 2.0}, "dp_eq": {"Number": 30, "Unit": "Pa"}, "index": {"Flag": false}}}]}]
```

Set H:

```json
[{"Symbol": "sections", "Rows": [{"Cells": {"name": {"Text": "duct"}, "Q": {"Number": 0.001, "Unit": "m^3/s"}, "L": {"Number": 20, "Unit": "m"}, "shape": {"Text": "round"}, "D": {"Number": 400, "Unit": "mm"}, "index": {"Flag": true}}}]}]
```

Set I:

```json
[{"Symbol": "dp_target", "Number": 0.3, "Unit": "Pa/m"}, {"Symbol": "sections", "Rows": [{"Cells": {"name": {"Text": "main"}, "Q": {"Number": 100, "Unit": "m^3/s"}, "L": {"Number": 50, "Unit": "m"}, "shape": {"Text": "round"}, "index": {"Flag": true}}}]}]
```

Set T:

```json
[{"Symbol": "T", "Number": 200, "Unit": "degC"}, {"Symbol": "sections", "Rows": [{"Cells": {"name": {"Text": "main"}, "Q": {"Number": 1, "Unit": "m^3/s"}, "L": {"Number": 10, "Unit": "m"}, "shape": {"Text": "round"}, "D": {"Number": 400, "Unit": "mm"}, "index": {"Flag": true}}}]}]
```

Set V:

```json
[{"Symbol": "sections", "Rows": [{"Cells": {"name": {"Text": "main"}, "Q": {"Number": 1, "Unit": "m^3/s"}, "L": {"Number": 10, "Unit": "m"}, "shape": {"Text": "round"}, "D": {"Number": 300, "Unit": "mm"}, "index": {"Flag": true}}}]}]
```

Set P:

```json
[{"Symbol": "dp_limit", "Number": 10, "Unit": "Pa"}, {"Symbol": "sections", "Rows": [{"Cells": {"name": {"Text": "main"}, "Q": {"Number": 1, "Unit": "m^3/s"}, "L": {"Number": 10, "Unit": "m"}, "shape": {"Text": "round"}, "D": {"Number": 400, "Unit": "mm"}, "index": {"Flag": true}}}]}]
```

Set J:

```json
[{"Symbol": "method", "Text": "velocity"}, {"Symbol": "v_target", "Number": 6, "Unit": "m/s"}, {"Symbol": "round_to", "Number": 10, "Unit": "mm"}, {"Symbol": "sections", "Rows": [{"Cells": {"name": {"Text": "duct"}, "Q": {"Number": 0.5, "Unit": "m^3/s"}, "L": {"Number": 10, "Unit": "m"}, "shape": {"Text": "rectangular"}, "a": {"Number": 500, "Unit": "mm"}, "index": {"Flag": true}}}]}]
```

Set K:

```json
[{"Symbol": "method", "Text": "velocity"}, {"Symbol": "v_target", "Number": 6, "Unit": "m/s"}, {"Symbol": "round_to", "Number": 10, "Unit": "mm"}, {"Symbol": "sections", "Rows": [{"Cells": {"name": {"Text": "duct"}, "Q": {"Number": 0.5, "Unit": "m^3/s"}, "L": {"Number": 10, "Unit": "m"}, "shape": {"Text": "flat oval"}, "a": {"Number": 500, "Unit": "mm"}, "index": {"Flag": true}}}]}]
```

Set L:

```json
[{"Symbol": "sections", "Rows": [{"Cells": {"name": {"Text": "duct"}, "Q": {"Number": 0.8, "Unit": "m^3/s"}, "L": {"Number": 20, "Unit": "m"}, "shape": {"Text": "rectangular"}, "a": {"Number": 1000, "Unit": "mm"}, "D": {"Number": 100, "Unit": "mm"}, "index": {"Flag": true}}}]}]
```

Set M:

```json
[{"Symbol": "dp_target", "Number": 0.3, "Unit": "Pa/m"}, {"Symbol": "sections", "Rows": [{"Cells": {"name": {"Text": "main"}, "Q": {"Number": 400, "Unit": "m^3/s"}, "L": {"Number": 50, "Unit": "m"}, "shape": {"Text": "round"}, "index": {"Flag": true}}}]}]
```

Set N:

```json
[{"Symbol": "dp_target", "Number": 5, "Unit": "Pa/m"}, {"Symbol": "sections", "Rows": [{"Cells": {"name": {"Text": "tiny"}, "Q": {"Number": 0.0001, "Unit": "m^3/s"}, "L": {"Number": 10, "Unit": "m"}, "shape": {"Text": "round"}, "index": {"Flag": true}}}]}]
```

Set O:

```json
[{"Symbol": "sections", "Rows": [{"Cells": {"name": {"Text": "duct"}, "Q": {"Number": 0.8, "Unit": "m^3/s"}, "L": {"Number": 20, "Unit": "m"}, "shape": {"Text": "rectangular"}, "D": {"Number": 300, "Unit": "mm"}, "index": {"Flag": true}}}]}]
```


