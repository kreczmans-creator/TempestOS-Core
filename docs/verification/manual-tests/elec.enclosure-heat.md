# Manual test — elec.enclosure-heat (Enclosure heat dissipation and fan sizing)
Liability: Medium. Time: ~35 min. Pre-requisites: a project open in the desktop app; the JSON sets at the end of this file.

How to open: press Ctrl+K, type `Open worksheet: Enclosure heat`, press Enter. Or use Engineering → Modules → Calculations → Worksheets, then pick the calculator under Electrical. The form recalculates at once. Results show five significant figures. "Show working" lists each formula step. The unit picker offers only matching units, so a wrong unit can only come in by import. Import reads the clipboard: copy the JSON, then press "Import inputs (CSV/JSON)". The form opens with the worked example of the first known-answer case, not the bare template defaults. To return to the template defaults, copy `[]` and press "Import inputs (CSV/JSON)". Start each step from a reset, so that values left from earlier steps cannot change the result. A step that says it continues from the last step is the exception.

| # | Step | Inputs (value + unit) | Expected output (symbol = value unit) | Tolerance | Expected PASS/FAIL/BLOCK/WARN | Basis | Result | Notes |
|---|---|---|---|---|---|---|---|---|
| 1 | Open the calculator. Copy JSON set A (below) and press "Import inputs (CSV/JSON)". The loads table has no form field. The geometry and temperatures stay at their defaults, which are the KAT values. | Loads: 80 W x 1 and 55 W x 4; enclosure 0.6 x 0.8 x 0.3 m, wall-mounted; 35 degC ambient; T_max 50 degC; h = 5.5 W/(m^2*K) | P_total = 0.3 kW; A_eff = 1.14 m^2; dT_nat = 47.847 K; T_int = 82.847 degC; P_nat_max = 0.09405 kW; P_cool = 0.20595 kW; rho_air = 1.1455 kg/m^3; V_req = 62.481 m^3/h; V_req_net = 42.893 m^3/h; u_nat = 3.1898 | ±0.01 % | FAIL (natural dissipation) with WARN (cooling unit of 206 W) | KAT ka-wall-mounted and independent script |  | Hand check: A_eff = 0.18 + 0.48 + 2 x 0.24 = 1.14 m^2; dT = 300 / (5.5 x 1.14) = 47.85 K. |
| 2 | Fan duty point. Import set B. | Load 300 W; fan curve (0, 60), (100, 30), (200, 0) in m^3/h and Pa; p_sys = 20 Pa at V_ref = 100 m^3/h | V_duty = 113.75 m^3/h; p_duty = 25.876 Pa; u_fan = 0.5493 | ±0.01 % | PASS (fan check); FAIL (natural check) as in step 1 | KAT ka-fan-duty and independent script |  | Hand check: 0.002 V^2 + 0.3 V - 60 = 0 gives V = 113.75 m^3/h. |
| 3 | High site. Import set C (step 1 loads with altitude 2000 m). | altitude = 2000 m | rho_air = 0.89870 kg/m^3; V_req = 79.638 m^3/h; V_req_net = 54.672 m^3/h | ±0.01 % | FAIL (natural) with WARN | Independent script. Hand check: p = 101325 x (1 - 2.25577e-5 x 2000)^5.25588; V = P / (rho x 1006 x 15) |  | The cooling-unit values do not change. |
| 4 | Free-standing cabinet at altitude. Import set D. | Load 800 W; 0.6 x 1.8 x 0.4 m; T_max = 55 degC; altitude = 2000 m | A_eff = 2.76 m^2; dT_nat = 52.701 K; T_int = 87.701 degC; P_nat_max = 0.3036 kW; P_cool = 0.4964 kW; V_req = 159.28 m^3/h | ±0.01 % | FAIL (natural) with WARN | Independent script (P1) |  | Rear and bottom faces are covered by default. |
| 5 | Sun load. Import set E. | Load 200 W; rear face free (f_rear = 1); I_sun = 1000 W/m^2; A_sun = 1.5 m^2; alpha_sun = 0.6 | P_solar = 0.9 kW; P_total = 1.1 kW; A_eff = 1.62 m^2; dT_nat = 123.46 K; T_int = 158.46 degC; P_cool = 0.96635 kW | ±0.01 % | FAIL (natural) with WARN | Independent script (P2) |  | Hand check: 0.6 x 1000 x 1.5 = 900 W. |
| 6 | Quantity and duty columns. Import set F. | Rows: 100 W x 3 at duty 0.5; 20 W x 2 at duty 1 | P_total = 0.19 kW; dT_nat = 30.303 K; T_int = 65.303 degC; P_cool = 0.09595 kW; V_req = 39.571 m^3/h | ±0.01 % | FAIL (natural) with WARN | Independent script (P9) |  | Hand check: 150 + 40 = 190 W. |
| 7 | Six-point fan curve. Import set G. | Load 450 W; 45 degC ambient; T_max 60 degC; 1500 m; curve 0 to 250 m^3/h; p_sys = 30 Pa at 120 m^3/h | rho_air = 0.92586 kg/m^3; V_req = 115.95 m^3/h; V_duty = 143.20 m^3/h; p_duty = 42.721 Pa; u_fan = 0.80973 | ±0.01 % | PASS (fan check) | Independent script (P11) |  |  |
| 8 | Boundary: zero heat load. Import set H. | Load 0 W | P_total = 0 kW; dT_nat = 0 K; T_int = 35 degC; P_cool = 0 kW; V_req = 0; u_nat = 0 | ±0.01 % | PASS | Independent script (P3) |  | No warnings. |
| 9 | Boundary: zero system resistance. Import set I. | Fan curve as step 2; p_sys = 0 Pa | V_duty = 200 m^3/h; p_duty = 0 Pa; u_fan = 0.3124 | ±0.01 % | PASS (fan check) | Independent script (P6) |  | The duty point is the end of the fan curve. |
| 10 | NEGATIVE: out of range. Try each in turn after importing set A: W = 0 m; f_top = 1.5; T_max = 35 degC (equal to ambient). | See step | Block messages: W must be at least 0.01 m; f_top must be at most 1; T_max, the maximum internal temperature must exceed the ambient. | n/a | BLOCK | Template hard limits and rule limit |  | Reset each field afterwards. |
| 11 | NEGATIVE: unit mismatch. Import set A with `{"Symbol":"W","Number":0.6,"Unit":"kg"}` added. Repeat with a load power in V. | W = 0.6 kg; load P = 5 V | Block messages: W must be entered in a unit of m, not kg; loads row 1, Heat loss per item must be in a unit of W, not V. | n/a | BLOCK | Hand check (a mass is not a width) |  | The unit picker lists only matching units. |
| 12 | NEGATIVE: no loads and a bad fan curve. Import `[{"Symbol":"loads","Rows":[]}]`. Then import set B with p_pts reduced to two values (60, 30). | Empty loads; V_pts with 3 points and p_pts with 2 | Block messages: loads, at least one heat load is needed; V_pts/p_pts, give at least two points with the same count and increasing airflows. | n/a | BLOCK | Template rules loads and fan-curve |  |  |
| 13 | NEGATIVE: stale upstream (an inner box inside an outer box). Import set A and save as "Encl-1". Import set N and save as "Encl-2". Link T_amb of Encl-2 to T_int of Encl-1. In the Saved calculations panel select Encl-1 and press Open. If the status line says the form has unsaved changes, press Open a second time to discard them. Change T_amb from 35 to 17.456 degC and press Save changes. Select Encl-2 and press Open. | Encl-1 T_amb 17.456 degC (was 35 degC), so T_int falls from 82.847 to 65.303 degC | Before the change Encl-2 shows T_amb = 82.847 degC and T_int = 130.69 degC. Within a second the Encl-2 row in the Saved calculations list ends with · STALE. Select Encl-2 and press Open: status line Opened 'Encl-2' · rev A · Draft. Orange banner: Stale — 'Encl-1' rev A now gives a different 'T_int' than the value 'T_amb' took. Button: Update linked inputs. Press Record check: status line reads Calculation 'Encl-2' is stale: 'Encl-1' rev A now gives a different 'T_int' than the value 'T_amb' took. Update it before checking. The watermark stays DRAFT. Press Update linked inputs: the banner clears; T_amb takes the new T_int of Encl-1; the status line reads Linked inputs updated to the current revisions and recalculated. T_amb = 65.303 degC; T_int = 113.15 degC; P_cool = 0 kW; u_nat = 0.8748. | ±0.01 % | WARN (stale banner); BLOCK (check refused); then PASS after the update | Independent script (enclosure model); hand check: dT_nat = 47.847 K does not depend on T_amb, so T_int = 17.456 + 47.847 = 65.303 degC; code rule for staleness |  | Procedure L in README.md. Reopening uses the Saved calculations panel. An issued upstream goes stale the same way: Revise it, then the banner reads that the input is pinned to rev A, which is superseded by rev B. Warnings about T_amb and T_max outside the usual range are expected. |
| 14 | Known defect P1-elec.enclosure-heat-01. Read the A_eff step label under "Show working". Import set M (0.6 x 1.8 x 0.4 m cabinet, all vertical faces free, 300 W). | Set M | Correct: the label says plain exposed-area method and not a verification to the temperature-rise standard. Product: A_eff = 3.840 m^2 and dT_nat = 14.205 K, labelled with the standard. With surface factors the area is 3.576 m^2 and dT is 15.253 K (7 % higher). | ±0.5 % on the hand values | FAIL | Hand formula with surface factors 1.4 (top) and 0.9 (vertical), from memory; confirm in your copy of the standard |  | Known defect P1-elec.enclosure-heat-01; expect FAIL until fixed. |
| 15 | Known defect P1-elec.enclosure-heat-02. Cover every exposed face. Import set K. | f_top = f_front = f_left = f_right = 0 | Correct: Block message that the effective area must be above zero. Product shows Block "engine: Division by zero". | n/a | BLOCK | Template limits; P1 report |  | Known defect P1-elec.enclosure-heat-02; expect FAIL until fixed. Only the message is wrong. |
| 16 | Known defect P1-elec.enclosure-heat-03. Large rise with no validity warning. Use set E. | Set E | Product: T_int = 158.46 degC from a constant h, with only the cooling-unit warning. Correct: a warning that the rise is too large for the method, and T_int labelled as a mean value. | n/a | WARN | P1 report; independent script (P2) |  | Known defect P1-elec.enclosure-heat-03; expect FAIL until fixed. |
| 17 | Known defect P1-elec.enclosure-heat-R1. Exhaust fan air volume. Use set B. | Set B (fan at the outlet at 50 degC) | Product: u_fan = 0.5493, using air at 35 degC. Correct for an exhaust fan: V_req = 62.481 x 323.15 / 308.15 = 65.52 m^3/h and u_fan = 0.5761. Or a notice that V_req is at inlet conditions. | ±0.5 % | FAIL | Hand formula: density ratio 323.15 / 308.15 = 1.0487 |  | Known defect P1-elec.enclosure-heat-R1; expect FAIL until fixed. |
| 18 | Known defect P1-elec.enclosure-heat-R2. Ambient above the limit. Import set L. | T_amb = 45 degC; T_max = 40 degC; load 300 W | Product: Block message (T_max must exceed the ambient). Correct: a cooling-unit duty P_cool = 300 + 5.5 x 1.14 x 5 = 331.35 W. | ±0.01 % | WARN | Hand formula |  | Known defect P1-elec.enclosure-heat-R2; expect FAIL until fixed. |

Not tested here: P1 Info findings 04 and 05.

## JSON sets

Each import resets every input not listed to its default. Copy one block, then press "Import inputs (CSV/JSON)".

Set A:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "PLC and I/O"}, "P": {"Number": 80, "Unit": "W"}, "qty": {"Number": 1}, "duty": {"Number": 1}}}, {"Cells": {"name": {"Text": "drives"}, "P": {"Number": 55, "Unit": "W"}, "qty": {"Number": 4}, "duty": {"Number": 1}}}]}]
```

Set B:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "load"}, "P": {"Number": 300, "Unit": "W"}, "qty": {"Number": 1}, "duty": {"Number": 1}}}]}, {"Symbol": "V_pts", "Vector": [0, 100, 200], "Unit": "m^3/h"}, {"Symbol": "p_pts", "Vector": [60, 30, 0], "Unit": "Pa"}]
```

Set C:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "PLC and I/O"}, "P": {"Number": 80, "Unit": "W"}, "qty": {"Number": 1}, "duty": {"Number": 1}}}, {"Cells": {"name": {"Text": "drives"}, "P": {"Number": 55, "Unit": "W"}, "qty": {"Number": 4}, "duty": {"Number": 1}}}]}, {"Symbol": "altitude", "Number": 2000, "Unit": "m"}]
```

Set D:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "load"}, "P": {"Number": 800, "Unit": "W"}, "qty": {"Number": 1}, "duty": {"Number": 1}}}]}, {"Symbol": "W", "Number": 0.6, "Unit": "m"}, {"Symbol": "H", "Number": 1.8, "Unit": "m"}, {"Symbol": "D", "Number": 0.4, "Unit": "m"}, {"Symbol": "T_max", "Number": 55, "Unit": "degC"}, {"Symbol": "altitude", "Number": 2000, "Unit": "m"}]
```

Set E:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "load"}, "P": {"Number": 200, "Unit": "W"}, "qty": {"Number": 1}, "duty": {"Number": 1}}}]}, {"Symbol": "f_rear", "Number": 1}, {"Symbol": "I_sun", "Number": 1000, "Unit": "W/m^2"}, {"Symbol": "A_sun", "Number": 1.5, "Unit": "m^2"}, {"Symbol": "alpha_sun", "Number": 0.6}]
```

Set F:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "Group 1"}, "P": {"Number": 100, "Unit": "W"}, "qty": {"Number": 3}, "duty": {"Number": 0.5}}}, {"Cells": {"name": {"Text": "Group 2"}, "P": {"Number": 20, "Unit": "W"}, "qty": {"Number": 2}, "duty": {"Number": 1}}}]}]
```

Set G:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "load"}, "P": {"Number": 450, "Unit": "W"}, "qty": {"Number": 1}, "duty": {"Number": 1}}}]}, {"Symbol": "T_amb", "Number": 45, "Unit": "degC"}, {"Symbol": "T_max", "Number": 60, "Unit": "degC"}, {"Symbol": "altitude", "Number": 1500, "Unit": "m"}, {"Symbol": "V_pts", "Vector": [0, 50, 100, 150, 200, 250], "Unit": "m^3/h"}, {"Symbol": "p_pts", "Vector": [80, 75, 60, 40, 18, 0], "Unit": "Pa"}, {"Symbol": "p_sys", "Number": 30, "Unit": "Pa"}, {"Symbol": "V_ref", "Number": 120, "Unit": "m^3/h"}]
```

Set H:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "load"}, "P": {"Number": 0, "Unit": "W"}, "qty": {"Number": 1}, "duty": {"Number": 1}}}]}]
```

Set I:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "load"}, "P": {"Number": 300, "Unit": "W"}, "qty": {"Number": 1}, "duty": {"Number": 1}}}]}, {"Symbol": "V_pts", "Vector": [0, 100, 200], "Unit": "m^3/h"}, {"Symbol": "p_pts", "Vector": [60, 30, 0], "Unit": "Pa"}, {"Symbol": "p_sys", "Number": 0, "Unit": "Pa"}]
```

Set K:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "load"}, "P": {"Number": 300, "Unit": "W"}, "qty": {"Number": 1}, "duty": {"Number": 1}}}]}, {"Symbol": "f_top", "Number": 0}, {"Symbol": "f_front", "Number": 0}, {"Symbol": "f_left", "Number": 0}, {"Symbol": "f_right", "Number": 0}]
```

Set L:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "load"}, "P": {"Number": 300, "Unit": "W"}, "qty": {"Number": 1}, "duty": {"Number": 1}}}]}, {"Symbol": "T_amb", "Number": 45, "Unit": "degC"}, {"Symbol": "T_max", "Number": 40, "Unit": "degC"}]
```

Set M:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "load"}, "P": {"Number": 300, "Unit": "W"}, "qty": {"Number": 1}, "duty": {"Number": 1}}}]}, {"Symbol": "W", "Number": 0.6, "Unit": "m"}, {"Symbol": "H", "Number": 1.8, "Unit": "m"}, {"Symbol": "D", "Number": 0.4, "Unit": "m"}, {"Symbol": "f_rear", "Number": 1}]
```

Set N:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "load"}, "P": {"Number": 300, "Unit": "W"}, "qty": {"Number": 1}, "duty": {"Number": 1}}}]}, {"Symbol": "T_max", "Number": 120, "Unit": "degC"}]
```

Set O:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "Group 1"}, "P": {"Number": 100, "Unit": "W"}, "qty": {"Number": 3}, "duty": {"Number": 0.5}}}, {"Cells": {"name": {"Text": "Group 2"}, "P": {"Number": 20, "Unit": "W"}, "qty": {"Number": 2}, "duty": {"Number": 1}}}]}]
```


