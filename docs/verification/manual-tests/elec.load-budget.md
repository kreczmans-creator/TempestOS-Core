# Manual test — elec.load-budget (Load budget, PSU, transformer and battery)
Liability: High. Time: ~40 min. Pre-requisites: a project open in the desktop app; the JSON sets at the end of this file; the voltage-drop worksheet for step 12.

How to open: press Ctrl+K, type `Open worksheet: Load budget`, press Enter. Or use Engineering → Modules → Calculations → Worksheets, then pick the calculator under Electrical. The form recalculates at once. Results show five significant figures. "Show working" lists each formula step. The unit picker offers only matching units, so a wrong unit can only come in by import. Import reads the clipboard: copy the JSON, then press "Import inputs (CSV/JSON)". The form opens with the worked example of the first known-answer case, not the bare template defaults. To return to the template defaults, copy `[]` and press "Import inputs (CSV/JSON)". Start each step from a reset, so that values left from earlier steps cannot change the result. A step that says it continues from the last step is the exception.

| # | Step | Inputs (value + unit) | Expected output (symbol = value unit) | Tolerance | Expected PASS/FAIL/BLOCK/WARN | Basis | Result | Notes |
|---|---|---|---|---|---|---|---|---|
| 1 | Open the calculator. Copy JSON set A (below) to the clipboard. Press "Import inputs (CSV/JSON)". The loads table has no form field, so import is the only way to enter loads. | Loads: pump 7.5 kW AC3 0.85/0.9/1/1/6; fan 2.2 kW AC3 0.8/0.85/0.5/1/6; lighting 0.5 kW AC1 0.95/1/1/0.8/1; controls 0.12 kW DC backed. t_auto = 4 h | P_dem = 9.12 kW; S_dem = 10.74 kVA; I_3ph = 14.72 A; I_1ph = 1.8307 A; I_DC = 5 A; Phi = 0.927 kW; P_PSU_req = 150 W; S_T_req = 13.424 kVA; S_peak = 54.857 kVA; C_req = 32.895 A*h | ±0.01 % | PASS (no selected PSU, transformer or battery, so no checks) | KAT ka-mixed-buses and independent script |  | Hand check: P_dem = 7.5 + 2.2 x 0.5 + 0.5 x 0.8 + 0.12 = 9.12 kW. |
| 2 | Battery with all derates. Import set B. | Two backed DC loads (0.12 and 0.048 kW); t_auto = 4 h; k_temp = 0.9; C_batt = 55 A*h | I_b = 7 A; C_req = 51.170 A*h; u_batt = 0.93036 | ±0.01 % | PASS | KAT ka-battery and independent script |  | Hand check: 28 A*h / (0.8 x 0.95 x 0.9 x 0.8) = 51.17 A*h. |
| 3 | Peukert capacity. Import set C. | One backed DC load 0.12 kW; t_auto = 4 h; k_p = 1.2; H_rate = 20 h | Ah = 26.153 A*h; C_req = 43.015 A*h | ±0.01 % | PASS | KAT ka-peukert (status Unconfirmed) and independent script |  | Hand check: 5 x 20 x 0.2^(1/1.2) = 26.153. Check the Peukert form in your copy of the handbook. |
| 4 | PSU derating. Import set D. | One DC load 0.2 kW; theta = 70 degC; f_derate = 0.75; P_PSU = 240 W | P_PSU_req = 333.33 W; u_PSU = 1.3889 | ±0.01 % | FAIL (PSU check) | KAT ka-psu-derating (status Unconfirmed) and independent script |  | Hand check: 1.25 x 200 / 0.75 = 333.3 W. Check the 0.75 factor against the datasheet curve. |
| 5 | Transformer and starting peak. Import set E. | Pump 7.5 kW and fan 2.2 kW; S_T = 15 kVA; k_T = 3; margin = 1.5 | S_dem = 10.199 kVA; S_T_req = 15.298 kVA; S_peak = 54.316 kVA; u_T = 1.0199; u_inrush = 1.207 | ±0.01 % | FAIL (both transformer checks) | Independent script (x8) |  | Product convention for S_peak; see the P1-01 step. |
| 6 | Boundary: top of every range. Import set J. | One load 200 kW AC3, cosphi 0.5, eta 0.5, inrush 20; U_3ph = 12 V | I_3ph = 19245 A; S_dem = 400 kVA; Phi = 100 kW; S_T_req = 500 kVA; S_peak = 8000 kVA | ±0.01 % | PASS (no checks selected) | Independent script (x10) |  | 200 kW is the highest allowed row power. 12 V is the lowest allowed voltage. |
| 7 | Boundary: battery limits. Import set L. | One backed DC load 0.12 kW; t_auto = 10 h; eta_conv = 0.5; U_b = 2 V; DoD = 0.05 | I_b = 120 A; Ah = 1200 A*h; C_req = 31579 A*h | ±0.01 % | PASS | Independent script (x9) |  | Hand check: 120 A x 10 h / (0.05 x 0.95 x 1 x 0.8) = 31579. |
| 8 | Soft warning. Import set O (55 degC, f_derate left at 1). | One DC load 0.2 kW; theta = 55 degC | P_PSU_req = 250 W. A warning says the ambient is above 40 degC and no PSU derating is entered. | ±0.01 % | WARN | Independent script (x7); template rule derate |  | Hand check: 1.25 x 200 / 1 = 250 W. |
| 9 | NEGATIVE: no loads. Import `[{"Symbol":"loads","Rows":[]}]`. | Empty loads table | Block message: loads, enter at least one load. No results. | n/a | BLOCK | Template rule no-loads |  |  |
| 10 | NEGATIVE: out of range. Import set A, then change margin to 2. Repeat with a single load P = 250 kW. Repeat with cosphi = 0.3. Repeat with t_auto = 80 h. | margin = 2; P = 250 kW; cosphi = 0.3; t_auto = 80 h | Block messages: margin must be at most 1.5; loads[1].P must be at most 200 kW; loads[1].cosphi must be at least 0.5; t_auto must be at most 72 h. | n/a | BLOCK | Template hard limits |  |  |
| 11 | NEGATIVE: unit mismatch. Import set A with `{"Symbol":"U_3ph","Number":400,"Unit":"A"}` added. Repeat with a load power in V. | U_3ph = 400 A; load P = 5 V | Block messages: U_3ph must be entered in a unit of V, not A; loads row 1, Electrical input power must be in a unit of kW, not V. | n/a | BLOCK | Hand check (current is not voltage) |  |  |
| 12 | NEGATIVE: stale downstream. Import set A and save as "LB-1". In the voltage-drop worksheet, reset to the template defaults and save as "VD-1". Link its I to LB-1 output I_3ph. Read VD-1. In the Saved calculations panel select LB-1 and press Open. If the status line says the form has unsaved changes, press Open a second time to discard them. Import set A2 (the same loads with the fan duty d = 1) and press Save changes. Select VD-1 and press Open. | LB-1 fan duty d = 1 (was 0.5), by importing set A2 | LB-1 gives I_3ph = 16.705 A after the change. VD-1 before the update: I = 14.720 A, dV = 1.1078 V. Within a second the VD-1 row in the Saved calculations list ends with · STALE. Select VD-1 and press Open: status line Opened 'VD-1' · rev A · Draft. Orange banner: Stale — 'LB-1' rev A now gives a different 'I_3ph' than the value 'I' took. Button: Update linked inputs. Press Record check: status line reads Calculation 'VD-1' is stale: 'LB-1' rev A now gives a different 'I_3ph' than the value 'I' took. Update it before checking. The watermark stays DRAFT. Press Update linked inputs: the banner clears; I takes the new I_3ph of LB-1; the status line reads Linked inputs updated to the current revisions and recalculated. I = 16.705 A; dV = 1.2571 V (0.31428 %). | ±0.01 % | WARN (stale banner); BLOCK (check refused); then PASS after the update | Independent script (budget and voltage-drop scripts); code rule for staleness |  | Procedure L in README.md. Reopening uses the Saved calculations panel. An issued upstream goes stale the same way: Revise it, then the banner reads that the input is pinned to rev A, which is superseded by rev B. The full voltage-drop steps are in elec.voltage-drop.md. |
| 13 | Known defect P1-elec.load-budget-01. Starting peak with duty below 1. Import set F. | One pump 11 kW, cosphi 0.85, duty 0.5, inrush 6 | Correct: S_peak = 77.647 kVA (the pump starts at full rating). Product shows 38.824 kVA. | ±0.01 % | FAIL | Independent script (s_peak_full) |  | Known defect P1-elec.load-budget-01; expect FAIL until fixed. Hand check: 6 x 11 / 0.85 = 77.65 kVA. |
| 14 | Known defect P1-elec.load-budget-02. DC-only budget. Import set G. | One backed DC load 4 kW, eta 0.9; t_auto = 2 h | Correct: a non-zero AC input current for the PSU, or a notice saying which supply feeds it. Product shows I_3ph = 0 A and I_1ph = 0 A. | n/a | FAIL | P1 report; hand check (4 kW at 230 V is at least 17.4 A) |  | Known defect P1-elec.load-budget-02; expect FAIL until fixed. Other values: I_DC 166.67 A; P_PSU_req 5000 W; C_req 548.25 A*h. |
| 15 | Known defect P1-elec.load-budget-03. Blank optional cells. Import set H (cosphi, eta, duty, diversity and inrush omitted). | One AC3 load 5 kW with only the power given | Correct: a notice for each blank cell, or cosphi required. Product shows no notice and I_3ph = 7.2169 A. With cosphi 0.85 the current is 8.49 A. | ±0.5 % on 8.49 A | WARN | Independent script (x6); hand check: 5000 / (sqrt(3) x 400 x 0.85) = 8.49 A |  | Known defect P1-elec.load-budget-03; expect FAIL until fixed. |
| 16 | Known defect P1-elec.load-budget-04. PSU also has to charge the battery. Import set K. | Backed DC load 0.168 kW; t_auto = 4 h; P_PSU = 240 W; C_batt = 55 A*h | Product: u_PSU = 0.875 (PASS). Correct: a notice that the PSU must also charge the battery. A 0.1C charge at 24 V needs about 132 W. | ±1 % on 132 W | WARN | Hand formula: 0.1 x 55 A*h x 24 V = 132 W |  | Known defect P1-elec.load-budget-04; expect FAIL until fixed. |
| 17 | Known defect P1-elec.load-budget-05. Heat output label and scope. Use set A. Read the Phi port label in the Link panel. | Set A | Product: Phi = 0.927 kW, labelled heat dissipated (to the enclosure calculation). Correct: only the controls row, 0.012 kW, is inside a panel, plus PSU loss. | ±1 % | FAIL | Hand formula: (1 - 0.9) x 0.12 kW = 0.012 kW |  | Known defect P1-elec.load-budget-05; expect FAIL until fixed. |
| 18 | Known defect P1-elec.load-budget-06. Peukert credit beyond the rated hour. Import set I. | Backed DC 0.24 kW (10 A); t_auto = 72 h; k_p = 1.3; H_rate = 20 h | Product: Ah = 535.74 A*h with no notice. Correct: a warning that t_auto exceeds H_rate. Plain I x t is 720 A*h. | ±0.01 % on values | WARN | Independent script (x4) |  | Known defect P1-elec.load-budget-06; expect FAIL until fixed. |
| 19 | Known defect P1-elec.load-budget-07. Autonomy with no battery-backed load. Import set M. | Unbacked DC load 0.2 kW; t_auto = 4 h | Product: C_req = 0 A*h, no notice. Correct: a warning that no row is battery-backed. | n/a | WARN | P1 report |  | Known defect P1-elec.load-budget-07; expect FAIL until fixed. |
| 20 | Known defect P1-elec.load-budget-08. Unsourced default k_T. Read the k_T field tooltip and the clause list. | k_T = 3 (default) | Correct: the default cites a source, or the field is empty, and a note says voltage dip is not assessed. Product shows 3 with no source. | n/a | FAIL | P1 report |  | Known defect P1-elec.load-budget-08; expect FAIL until fixed. |
| 21 | Known defect P1-elec.load-budget-R1. Duty applied to currents and the PSU. Import set N, then set P. | N: pump 7.5 kW, cosphi 0.85, duty 0.5. P: brake 0.2 kW DC, duty 0.1, P_PSU = 30 W | N: product I_3ph = 6.368 A; correct running current 12.736 A. P: product P_PSU_req = 25 W and a 30 W PSU passes; correct 250 W. | ±0.1 % | FAIL | Hand formula: 7500 / (sqrt(3) x 400 x 0.85) = 12.736 A; 1.25 x 200 W = 250 W |  | Known defect P1-elec.load-budget-R1; expect FAIL until fixed. Duty is right for energy and heat, wrong for cable and PSU current. |

Not tested here: P1 Info findings 09 and 10.

## JSON sets

Each import resets every input not listed to its default. Copy one block, then press "Import inputs (CSV/JSON)".

Set A:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "Pump motor"}, "supply": {"Text": "AC three-phase"}, "P": {"Number": 7.5, "Unit": "kW"}, "cosphi": {"Number": 0.85}, "eta": {"Number": 0.9}, "d": {"Number": 1}, "k": {"Number": 1}, "inrush": {"Number": 6}, "backed": {"Flag": false}}}, {"Cells": {"name": {"Text": "Supply fan"}, "supply": {"Text": "AC three-phase"}, "P": {"Number": 2.2, "Unit": "kW"}, "cosphi": {"Number": 0.8}, "eta": {"Number": 0.85}, "d": {"Number": 0.5}, "k": {"Number": 1}, "inrush": {"Number": 6}, "backed": {"Flag": false}}}, {"Cells": {"name": {"Text": "Lighting"}, "supply": {"Text": "AC single-phase"}, "P": {"Number": 0.5, "Unit": "kW"}, "cosphi": {"Number": 0.95}, "eta": {"Number": 1}, "d": {"Number": 1}, "k": {"Number": 0.8}, "inrush": {"Number": 1}, "backed": {"Flag": false}}}, {"Cells": {"name": {"Text": "Controls"}, "supply": {"Text": "DC"}, "P": {"Number": 0.12, "Unit": "kW"}, "cosphi": {"Number": 1}, "eta": {"Number": 0.9}, "d": {"Number": 1}, "k": {"Number": 1}, "inrush": {"Number": 1}, "backed": {"Flag": true}}}]}, {"Symbol": "t_auto", "Number": 4, "Unit": "h"}]
```

Set A2:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "Pump motor"}, "supply": {"Text": "AC three-phase"}, "P": {"Number": 7.5, "Unit": "kW"}, "cosphi": {"Number": 0.85}, "eta": {"Number": 0.9}, "d": {"Number": 1}, "k": {"Number": 1}, "inrush": {"Number": 6}, "backed": {"Flag": false}}}, {"Cells": {"name": {"Text": "Supply fan"}, "supply": {"Text": "AC three-phase"}, "P": {"Number": 2.2, "Unit": "kW"}, "cosphi": {"Number": 0.8}, "eta": {"Number": 0.85}, "d": {"Number": 1}, "k": {"Number": 1}, "inrush": {"Number": 6}, "backed": {"Flag": false}}}, {"Cells": {"name": {"Text": "Lighting"}, "supply": {"Text": "AC single-phase"}, "P": {"Number": 0.5, "Unit": "kW"}, "cosphi": {"Number": 0.95}, "eta": {"Number": 1}, "d": {"Number": 1}, "k": {"Number": 0.8}, "inrush": {"Number": 1}, "backed": {"Flag": false}}}, {"Cells": {"name": {"Text": "Controls"}, "supply": {"Text": "DC"}, "P": {"Number": 0.12, "Unit": "kW"}, "cosphi": {"Number": 1}, "eta": {"Number": 0.9}, "d": {"Number": 1}, "k": {"Number": 1}, "inrush": {"Number": 1}, "backed": {"Flag": true}}}]}, {"Symbol": "t_auto", "Number": 4, "Unit": "h"}]
```

Set B:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "Controls"}, "supply": {"Text": "DC"}, "P": {"Number": 0.12, "Unit": "kW"}, "cosphi": {"Number": 1}, "eta": {"Number": 1}, "d": {"Number": 1}, "k": {"Number": 1}, "inrush": {"Number": 1}, "backed": {"Flag": true}}}, {"Cells": {"name": {"Text": "Comms"}, "supply": {"Text": "DC"}, "P": {"Number": 0.048, "Unit": "kW"}, "cosphi": {"Number": 1}, "eta": {"Number": 1}, "d": {"Number": 1}, "k": {"Number": 1}, "inrush": {"Number": 1}, "backed": {"Flag": true}}}]}, {"Symbol": "t_auto", "Number": 4, "Unit": "h"}, {"Symbol": "k_temp", "Number": 0.9}, {"Symbol": "C_batt", "Number": 55, "Unit": "A*h"}]
```

Set C:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "Controls"}, "supply": {"Text": "DC"}, "P": {"Number": 0.12, "Unit": "kW"}, "cosphi": {"Number": 1}, "eta": {"Number": 1}, "d": {"Number": 1}, "k": {"Number": 1}, "inrush": {"Number": 1}, "backed": {"Flag": true}}}]}, {"Symbol": "t_auto", "Number": 4, "Unit": "h"}, {"Symbol": "k_p", "Number": 1.2}, {"Symbol": "H_rate", "Number": 20, "Unit": "h"}]
```

Set D:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "PLC and I/O"}, "supply": {"Text": "DC"}, "P": {"Number": 0.2, "Unit": "kW"}, "cosphi": {"Number": 1}, "eta": {"Number": 1}, "d": {"Number": 1}, "k": {"Number": 1}, "inrush": {"Number": 1}, "backed": {"Flag": false}}}]}, {"Symbol": "theta", "Number": 70, "Unit": "degC"}, {"Symbol": "f_derate", "Number": 0.75}, {"Symbol": "P_PSU", "Number": 240, "Unit": "W"}]
```

Set E:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "Pump"}, "supply": {"Text": "AC three-phase"}, "P": {"Number": 7.5, "Unit": "kW"}, "cosphi": {"Number": 0.85}, "eta": {"Number": 0.9}, "d": {"Number": 1}, "k": {"Number": 1}, "inrush": {"Number": 6}, "backed": {"Flag": false}}}, {"Cells": {"name": {"Text": "Fan"}, "supply": {"Text": "AC three-phase"}, "P": {"Number": 2.2, "Unit": "kW"}, "cosphi": {"Number": 0.8}, "eta": {"Number": 0.85}, "d": {"Number": 0.5}, "k": {"Number": 1}, "inrush": {"Number": 6}, "backed": {"Flag": false}}}]}, {"Symbol": "S_T", "Number": 15, "Unit": "kVA"}, {"Symbol": "k_T", "Number": 3}, {"Symbol": "margin", "Number": 1.5}]
```

Set F:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "Pump"}, "supply": {"Text": "AC three-phase"}, "P": {"Number": 11, "Unit": "kW"}, "cosphi": {"Number": 0.85}, "eta": {"Number": 0.9}, "d": {"Number": 0.5}, "k": {"Number": 1}, "inrush": {"Number": 6}, "backed": {"Flag": false}}}]}]
```

Set G:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "DC load"}, "supply": {"Text": "DC"}, "P": {"Number": 4, "Unit": "kW"}, "cosphi": {"Number": 1}, "eta": {"Number": 0.9}, "d": {"Number": 1}, "k": {"Number": 1}, "inrush": {"Number": 1}, "backed": {"Flag": true}}}]}, {"Symbol": "t_auto", "Number": 2, "Unit": "h"}]
```

Set H:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "Blank"}, "supply": {"Text": "AC three-phase"}, "P": {"Number": 5, "Unit": "kW"}}}]}]
```

Set I:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "Cell"}, "supply": {"Text": "DC"}, "P": {"Number": 0.24, "Unit": "kW"}, "cosphi": {"Number": 1}, "eta": {"Number": 1}, "d": {"Number": 1}, "k": {"Number": 1}, "inrush": {"Number": 1}, "backed": {"Flag": true}}}]}, {"Symbol": "t_auto", "Number": 72, "Unit": "h"}, {"Symbol": "k_p", "Number": 1.3}, {"Symbol": "H_rate", "Number": 20, "Unit": "h"}]
```

Set J:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "Big"}, "supply": {"Text": "AC three-phase"}, "P": {"Number": 200, "Unit": "kW"}, "cosphi": {"Number": 0.5}, "eta": {"Number": 0.5}, "d": {"Number": 1}, "k": {"Number": 1}, "inrush": {"Number": 20}, "backed": {"Flag": false}}}]}, {"Symbol": "U_3ph", "Number": 12, "Unit": "V"}]
```

Set K:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "DC"}, "supply": {"Text": "DC"}, "P": {"Number": 0.168, "Unit": "kW"}, "cosphi": {"Number": 1}, "eta": {"Number": 1}, "d": {"Number": 1}, "k": {"Number": 1}, "inrush": {"Number": 1}, "backed": {"Flag": true}}}]}, {"Symbol": "t_auto", "Number": 4, "Unit": "h"}, {"Symbol": "P_PSU", "Number": 240, "Unit": "W"}, {"Symbol": "C_batt", "Number": 55, "Unit": "A*h"}]
```

Set L:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "Cell"}, "supply": {"Text": "DC"}, "P": {"Number": 0.12, "Unit": "kW"}, "cosphi": {"Number": 1}, "eta": {"Number": 1}, "d": {"Number": 1}, "k": {"Number": 1}, "inrush": {"Number": 1}, "backed": {"Flag": true}}}]}, {"Symbol": "t_auto", "Number": 10, "Unit": "h"}, {"Symbol": "eta_conv", "Number": 0.5}, {"Symbol": "U_b", "Number": 2, "Unit": "V"}, {"Symbol": "DoD", "Number": 0.05}]
```

Set M:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "DC"}, "supply": {"Text": "DC"}, "P": {"Number": 0.2, "Unit": "kW"}, "cosphi": {"Number": 1}, "eta": {"Number": 1}, "d": {"Number": 1}, "k": {"Number": 1}, "inrush": {"Number": 1}, "backed": {"Flag": false}}}]}, {"Symbol": "t_auto", "Number": 4, "Unit": "h"}]
```

Set N:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "Pump"}, "supply": {"Text": "AC three-phase"}, "P": {"Number": 7.5, "Unit": "kW"}, "cosphi": {"Number": 0.85}, "eta": {"Number": 0.9}, "d": {"Number": 0.5}, "k": {"Number": 1}, "inrush": {"Number": 6}, "backed": {"Flag": false}}}]}]
```

Set O:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "DC"}, "supply": {"Text": "DC"}, "P": {"Number": 0.2, "Unit": "kW"}, "cosphi": {"Number": 1}, "eta": {"Number": 1}, "d": {"Number": 1}, "k": {"Number": 1}, "inrush": {"Number": 1}, "backed": {"Flag": false}}}]}, {"Symbol": "theta", "Number": 55, "Unit": "degC"}]
```

Set P:

```json
[{"Symbol": "loads", "Rows": [{"Cells": {"name": {"Text": "Brake"}, "supply": {"Text": "DC"}, "P": {"Number": 0.2, "Unit": "kW"}, "cosphi": {"Number": 1}, "eta": {"Number": 1}, "d": {"Number": 0.1}, "k": {"Number": 1}, "inrush": {"Number": 1}, "backed": {"Flag": false}}}]}, {"Symbol": "P_PSU", "Number": 30, "Unit": "W"}]
```


