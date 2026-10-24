# Manual test — bs.sealed-system (Sealed heating system: emitters and expansion vessel)
Liability: High. Time: ~40 min. Pre-requisites: a project open in the desktop app; the JSON sets at the end of this file; the heat-exchanger worksheet for step 14.

How to open: press Ctrl+K, type `Open worksheet: Sealed heating system`, press Enter. Or use Engineering → Modules → Calculations → Worksheets, then pick the calculator under Building Services. The form recalculates at once. Results show five significant figures. "Show working" lists each formula step. The unit picker offers only matching units, so a wrong unit can only come in by import. Import reads the clipboard: copy the JSON, then press "Import inputs (CSV/JSON)". The form opens with the worked example of the first known-answer case, not the bare template defaults. To return to the template defaults, copy `[]` and press "Import inputs (CSV/JSON)". Start each step from a reset, so that values left from earlier steps cannot change the result. A step that says it continues from the last step is the exception.

| # | Step | Inputs (value + unit) | Expected output (symbol = value unit) | Tolerance | Expected PASS/FAIL/BLOCK/WARN | Basis | Result | Notes |
|---|---|---|---|---|---|---|---|---|
| 1 | Open the calculator. Copy JSON set A (below) and press "Import inputs (CSV/JSON)". The rooms table has no form field, so import is the only way to enter rooms. | One room: needs 1500 W, selected emitter 1600 W at DT 50 K, 8 L; V_pipe 20 L; V_plant 10 L; 75/65/20 degC | dT = 50 K; f_dT = 1; R1 = 1500 W; u_emit = 0.9375; V_s = 38 L; e_x = 0.032086; p_i = 0.79019 bar; V_e = 2.4959 L | ±0.01 % | PASS (emitter check) | KAT ka-dt50 and independent script |  | Hand check: DT = (75 + 65) / 2 - 20 = 50 K; 1500 / 1600 = 0.9375. V_s = 8 + 20 + 10 = 38 L. |
| 2 | Low-temperature circuit, arithmetic mean. Import set B. | Room needs 1500 W; t_f = 55 degC; t_r = 45 degC | dT = 30 K; f_dT = 0.51475; R1 = 2914.0 W | ±0.01 % | PASS | KAT ka-dt30 and independent script |  | Hand check: (30 / 50)^1.3 = 0.51475; 1500 / 0.51475 = 2914.0 W. Compare 0.5148 with your emitter catalogue. |
| 3 | Log-mean option. Import set C. | As step 2 with the mean temperature difference set to log mean | dT = 29.720 K; f_dT = 0.50852; R1 = 2949.8 W | ±0.01 % | PASS | KAT ka-lmtd and independent script |  | Hand check: DT = 10 / ln(35 / 25) = 29.720 K. |
| 4 | Expansion vessel. Reset, then enter the volumes (the reset also removes the rooms). | V_pipe = 90 L; V_plant = 10 L; V_vessel = 8 L; t_fill 10 degC; t_max 85 degC; h_static 5 m; p_margin 0.3 bar; p_f 2.5 bar | V_s = 100 L; e_x = 0.032086; p_i = 0.79019 bar; V_e = 6.5681 L; u_vessel = 0.82101 | ±0.01 % | PASS (vessel check) | KAT ka-vessel and independent script (Kell water density) |  | Hand check: V_e = 0.032086 x 100 / (1 - 1.79019 / 3.5) = 6.568 L. |
| 5 | Three rooms at 70/50/21. Import set E. | Rooms 2200 W (2400 W emitter), 1100 W (1000 W), 900 W (no emitter); V_pipe 60; V_plant 15; vessel 12 L | dT = 39 K; f_dT = 0.72397; R1 = 3038.8 W; R2 = 1519.4 W; R3 = 1243.1 W; V_s = 81.6 L; V_e = 5.3595 L; u_emit = 1.5194; u_vessel = 0.44663 | ±0.01 % | FAIL (emitter check: the kitchen emitter is too small) | Independent script (probe S1) |  | Rooms without a selected emitter are not checked. |
| 6 | Large system, tall building. Enter the values by hand. | V_pipe 1000 L; V_plant 100 L; t_fill 5 degC; t_max 95 degC; h_static 30 m; p_margin 0.5 bar; p_f 5 bar; p_prv 6 bar | V_s = 1100 L; e_x = 0.039571; p_i = 3.4419 bar; V_e = 167.62 L | ±0.01 % | PASS | Independent script (probe S4) |  | Hand check: p_i = 999.97 x 9.80665 x 30 / 1e5 + 0.5 = 3.442 bar. |
| 7 | Glycol record. | V_pipe 90; V_plant 10; e_fluid = 0.05 | e_x = 0.05; V_e = 10.235 L (instead of 6.568 L) | ±0.01 % | PASS with WARN (value not from a reference record) | Independent script (probe S5) |  | Hand check: 0.05 x 100 / (1 - 1.79019 / 3.5) = 10.235 L. |
| 8 | Boundary: highest permitted system volume, and the two limits. | (a) V_pipe 9990 L, V_plant 10 L. (b) V_pipe 4 L, V_plant 5 L. (c) V_pipe 10000 L, V_plant 10 L. | (a) V_s = 10000 L; V_e = 656.81 L. (b) Block: the system volume 9 L is outside 10 to 10000 L. (c) Block: 10010 L is outside the range. | ±0.01 % on (a) | PASS for (a); BLOCK for (b) and (c) | Independent script (probes S8 and S13) |  |  |
| 9 | Boundary: fill temperature equal to the maximum. | t_fill = 30 degC; t_max = 30 degC; t_f = 30; t_r = 30; t_i = 16; V_pipe 90; V_plant 10 | e_x = 0; V_e = 0 L; dT = 14 K; f_dT = 0.19112 | ±0.01 % | PASS | Independent script (probe S11) |  | A vessel of zero size is a degenerate result. |
| 10 | Boundary: highest exponent and widest temperature difference. Import set G. | One room 1000 W (1500 W emitter); t_f = t_r = 90 degC; t_i = 16 degC; n = 1.5; t_max = 99 degC | dT = 74 K; f_dT = 1.8005; R1 = 555.40 W; e_x = 0.042352; V_e = 2.6009 L; u_emit = 0.37027 | ±0.01 % | PASS | Independent script (probe S9) |  | With t_f equal to t_r both mean-difference methods give the same value. |
| 11 | Mixed units through the unit pickers. | V_pipe unit m^3 and value 0.09; p_f unit kPa and value 250; t_max unit K and value 358.15; t_fill unit degF and value 50; h_static unit ft and value 16.4042 | V_s = 100 L; p_i = 0.79019 bar; V_e = 6.5681 L (same as step 4) | ±0.01 % | PASS | Independent script (probe S12); hand conversion: 0.09 m^3 = 90 L; 358.15 K = 85 degC; 50 degF = 10 degC; 16.4042 ft = 5 m |  | Pick the unit first, then retype the number, because a unit change converts the old number. |
| 12 | NEGATIVE: out of range and order. Try t_f = 95 degC. Then t_f = 60 and t_r = 70. Then p_f = 3.5 bar with p_prv = 3 bar. | See step | Block messages: t_f must be at most 90 degC; the flow must be at least the return, and the return above the room temperature; the pre-charge must be below the final pressure and that below the safety valve setting. | n/a | BLOCK | Template hard limits and rules temperatures, pressures |  |  |
| 13 | NEGATIVE: unit mismatch. Copy `[{"Symbol":"V_pipe","Number":20,"Unit":"kg"}]` and import it. | V_pipe = 20 kg | Block message: V_pipe must be entered in a unit of L, not kg. | n/a | BLOCK | Hand check (a mass is not a volume) |  | The unit picker lists only volume units. |
| 14 | NEGATIVE: stale upstream (flow temperature from an exchanger). In the heat-exchanger worksheet save the default case as "HX-1" (T_c_out = 50 degC). Here import set S and save as "SS-1". Link t_f of SS-1 to output T_c_out of HX-1. In the Saved calculations panel select HX-1 and press Open. If the status line says the form has unsaved changes, press Open a second time to discard them. Change A from 4.18 to 8.36 m^2 and press Save changes. Select SS-1 and press Open. | HX-1 A 8.36 m^2 (was 4.18 m^2), so T_c_out = 60 degC | Before the change SS-1 shows t_f = 50 degC: dT = 25 K; f_dT = 0.40613; R1 = 3693.4 W; u_emit = 2.3084. Within a second the SS-1 row in the Saved calculations list ends with · STALE. Select SS-1 and press Open: status line Opened 'SS-1' · rev A · Draft. Orange banner: Stale — 'HX-1' rev A now gives a different 'T_c_out' than the value 't_f' took. Button: Update linked inputs. Press Record check: status line reads Calculation 'SS-1' is stale: 'HX-1' rev A now gives a different 'T_c_out' than the value 't_f' took. Update it before checking. The watermark stays DRAFT. Press Update linked inputs: the banner clears; t_f takes the new T_c_out of HX-1; the status line reads Linked inputs updated to the current revisions and recalculated. t_f = 60 degC; dT = 30 K; f_dT = 0.51475; R1 = 2914.0 W; u_emit = 1.8213 (the emitter check still FAILS). The vessel results do not change. | ±0.01 % | WARN (stale banner); BLOCK (check refused); then FAIL (emitter check) after the update | Independent script (sealed-system model at 50 and 60 degC); code rule for staleness |  | Procedure L in README.md. Reopening uses the Saved calculations panel. An issued upstream goes stale the same way: Revise it, then the banner reads that the input is pinned to rev A, which is superseded by rev B. |
| 15 | Known defect P1-bs.sealed-system-01. Fill temperature above the maximum. | V_pipe 90; V_plant 10; t_fill = 40 degC; t_max = 30 degC | Product: e_x negative; V_e = -0.704 L; only a range notice and a note that t_max is below t_f. Correct: Block (t_max must exceed t_fill), or at least a warning that V_e is not positive. | ±0.5 % | BLOCK | Independent script (finding evidence) |  | Known defect P1-bs.sealed-system-01; expect FAIL until fixed. |
| 16 | Known defect P1-bs.sealed-system-02. Final pressure at the valve setting. | V_pipe 90; V_plant 10; p_f = 2.95 bar; p_prv = 3 bar | Product accepts it: V_e = 5.868 L (10.7 % below the 6.568 L at p_f 2.5 bar). Correct: a block or warning that p_f is too close to the safety valve setting. The margin comes from your copy of the standard (recalled as 0.5 bar). | ±0.01 % on 5.868 | WARN | Independent script (probe S7); margin value to be confirmed by the owner in the standard |  | Known defect P1-bs.sealed-system-02; expect FAIL until fixed. |
| 17 | Known defect P1-bs.sealed-system-03. No water reserve in the vessel size. Use step 4. | As step 4 (V_e = 6.568 L; vessel 8 L) | Product: V_e = 6.5681 L and the 8 L vessel passes. With an assumed 3 L water reserve: V_e = 12.71 L and the 8 L vessel fails. Check whether your standard or vessel maker requires a reserve. | ±0.1 % on 12.71 | FAIL | P1 report: V = (3.2086 + 3) x 3.5 / (2.5 - 0.79019); reserve value from memory of the standard |  | Known defect P1-bs.sealed-system-03; expect FAIL until fixed if a reserve is required. |
| 18 | Known defect P1-bs.sealed-system-04. Very small temperature difference. Import set P. | One room 1000 W; t_f = t_r = 30 degC; t_i = 24 degC | Product: dT = 6 K; f_dT = 0.0635; R1 = 15742 W and no warning. Correct: a warning that dT is outside the range the exponent covers (about 10 to 70 K). | ±0.01 % on R1 | WARN | Independent script (finding evidence) |  | Known defect P1-bs.sealed-system-04; expect FAIL until fixed. |
| 19 | Known defect P1-bs.sealed-system-05. Glycol and static head. Use step 7. | e_fluid = 0.05; assume a fluid 4 % denser than water | Product: V_e = 10.235 L (static head from water density). Correct for a 4 % denser fluid: p_i = 0.8098 bar and V_e = 10.354 L. | ±0.1 % | FAIL | Independent script (finding evidence) |  | Known defect P1-bs.sealed-system-05; expect FAIL until fixed. The form has no fluid-density input to enter. |
| 20 | Known defect P1-bs.sealed-system-R1. Pre-charge margin of zero. | V_pipe 90; V_plant 10; p_margin = 0 bar | Product: p_i = 0.49019 bar; V_e = 5.5877 L; no warning. Correct: a warning that the margin is below the minimum (recalled as 0.2 bar; confirm in the standard). | ±0.01 % | WARN | Independent script; margin value to be confirmed by the owner |  | Known defect P1-bs.sealed-system-R1; expect FAIL until fixed. |

Not tested here: P1 Info findings 06 and 07.

## JSON sets

Each import resets every input not listed to its default. Copy one block, then press "Import inputs (CSV/JSON)".

Set A:

```json
[{"Symbol": "rooms", "Rows": [{"Cells": {"name": {"Text": "Lounge"}, "Phi": {"Number": 1500, "Unit": "W"}, "Phi50": {"Number": 1600, "Unit": "W"}, "V_em": {"Number": 8, "Unit": "L"}}}]}, {"Symbol": "V_pipe", "Number": 20, "Unit": "L"}, {"Symbol": "V_plant", "Number": 10, "Unit": "L"}]
```

Set B:

```json
[{"Symbol": "rooms", "Rows": [{"Cells": {"name": {"Text": "Lounge"}, "Phi": {"Number": 1500, "Unit": "W"}}}]}, {"Symbol": "t_f", "Number": 55, "Unit": "degC"}, {"Symbol": "t_r", "Number": 45, "Unit": "degC"}]
```

Set C:

```json
[{"Symbol": "rooms", "Rows": [{"Cells": {"name": {"Text": "Lounge"}, "Phi": {"Number": 1500, "Unit": "W"}}}]}, {"Symbol": "t_f", "Number": 55, "Unit": "degC"}, {"Symbol": "t_r", "Number": 45, "Unit": "degC"}, {"Symbol": "dT_method", "Text": "log mean"}]
```

Set E:

```json
[{"Symbol": "rooms", "Rows": [{"Cells": {"name": {"Text": "Lounge"}, "Phi": {"Number": 2200, "Unit": "W"}, "Phi50": {"Number": 2400, "Unit": "W"}, "V_em": {"Number": 3.1, "Unit": "L"}}}, {"Cells": {"name": {"Text": "Kitchen"}, "Phi": {"Number": 1100, "Unit": "W"}, "Phi50": {"Number": 1000, "Unit": "W"}, "V_em": {"Number": 1.5, "Unit": "L"}}}, {"Cells": {"name": {"Text": "Bedroom"}, "Phi": {"Number": 900, "Unit": "W"}, "V_em": {"Number": 2.0, "Unit": "L"}}}]}, {"Symbol": "t_f", "Number": 70, "Unit": "degC"}, {"Symbol": "t_r", "Number": 50, "Unit": "degC"}, {"Symbol": "t_i", "Number": 21, "Unit": "degC"}, {"Symbol": "V_pipe", "Number": 60, "Unit": "L"}, {"Symbol": "V_plant", "Number": 15, "Unit": "L"}, {"Symbol": "V_vessel", "Number": 12, "Unit": "L"}]
```

Set G:

```json
[{"Symbol": "rooms", "Rows": [{"Cells": {"name": {"Text": "Room"}, "Phi": {"Number": 1000, "Unit": "W"}, "Phi50": {"Number": 1500, "Unit": "W"}}}]}, {"Symbol": "t_f", "Number": 90, "Unit": "degC"}, {"Symbol": "t_r", "Number": 90, "Unit": "degC"}, {"Symbol": "t_i", "Number": 16, "Unit": "degC"}, {"Symbol": "n", "Number": 1.5}, {"Symbol": "t_max", "Number": 99, "Unit": "degC"}]
```

Set P:

```json
[{"Symbol": "rooms", "Rows": [{"Cells": {"name": {"Text": "Room"}, "Phi": {"Number": 1000, "Unit": "W"}}}]}, {"Symbol": "t_f", "Number": 30, "Unit": "degC"}, {"Symbol": "t_r", "Number": 30, "Unit": "degC"}, {"Symbol": "t_i", "Number": 24, "Unit": "degC"}]
```

Set S:

```json
[{"Symbol": "rooms", "Rows": [{"Cells": {"name": {"Text": "Lounge"}, "Phi": {"Number": 1500, "Unit": "W"}, "Phi50": {"Number": 1600, "Unit": "W"}, "V_em": {"Number": 8, "Unit": "L"}}}]}, {"Symbol": "t_r", "Number": 40, "Unit": "degC"}, {"Symbol": "t_f", "Number": 50, "Unit": "degC"}]
```


