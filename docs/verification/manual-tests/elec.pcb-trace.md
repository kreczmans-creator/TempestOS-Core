# Manual test — elec.pcb-trace (PCB trace width (IPC-2221 legacy))
Liability: Low. Time: ~40 min. Pre-requisites: a project open in the desktop app. A pocket calculator for the hand checks. Step 9 also needs the calculator elec.load-budget in the same project.

| # | Step | Inputs (value + unit) | Expected output (symbol = value unit) | Tolerance | Expected PASS/FAIL/BLOCK/WARN | Basis | Result | Notes |
|---|---|---|---|---|---|---|---|---|
| 1 | Open the calculator. Press Ctrl+K, type "Open worksheet: PCB trace width", press Enter. Or use Engineering tree, Modules, Calculations, Worksheets, Electrical. Leave every input at its default. | Defaults: I 3 A; dT 10 K; T_amb 25 degC; External layer; 1 oz/ft²; w_act 2 mm; L 50 mm; k_ext 0.048; k_int 0.024; exponents 0.44 and 0.725; 1.378 mil per oz | Header shows elec.pcb-trace@1.0.1. A_req = 74.162 mil²; w_min = 1.367 mm; u_w = 0.68349; trace_R = 13.04 mohm; vdrop = 39.119 mV; trace_Ploss = 117.36 mW. Bar: PASS. Read the sheet header: it should say legacy IPC-2221 curve-fit, conservative for multilayer boards. | ±0.01 % | PASS | KAT ka-3a-10k-1oz-external and independent script; Secondary KAT ka-published-3a-10k-1oz: IPC chart reading 1.37 mm (±0.5 %) |  | Hand check: A = (3 / (0.048 x 10^0.44))^(1/0.725) = 74.16 mil². w = 74.16 / 1.378 = 53.82 mil = 1.367 mm. The published chart value is 1.37 mm. |
| 2 | Import Block K1 and read the results. Then import Block K2 and read them. | K1: layer Internal layer; I 3 A; dT 10 K; Cu_oz 1; w_act 4 mm. K2: External layer; I 5 A; dT 20 K; Cu_oz 2; w_act 2.5 mm; L 100 mm | K1: A_req = 192.93 mil²; w_min = 3.5561 mm; u_w = 0.88903. K2: w_min = 0.9079 mm; u_w = 0.36316; trace_R = 10.819 mohm; vdrop = 54.095 mV; trace_Ploss = 270.47 mW. | ±0.01 % | K1: PASS. K2: PASS | KATs ka-3a-10k-1oz-internal and ka-5a-20k-2oz-resistance; independent script; Secondary KAT ka-published-3a-10k-1oz-internal: chart 3.56 mm |  | Hand check K2: the trace runs at 25 + 20 = 45 degC. rho = 0.01724 x (1 + 0.00393 x 25) = 0.018934 ohm mm²/m. t = 2 x 0.035 = 0.07 mm. R = 0.018934 x 0.1 / (2.5 x 0.07) = 10.82 mohm. |
| 3 | Import Block K3 and read the results and the warnings. | K3: Internal layer; I 1 A; dT 30 K; T_amb 40 degC; Cu_oz 0.5; w_act 0.5 mm; L 200 mm | w_min = 0.80232 mm; u_w = 1.6046; trace_R = 471.47 mohm; vdrop = 471.47 mV; trace_Ploss = 471.47 mW. Bar: FAIL (Trace width as drawn against the minimum for the rise u = 1.60463). A warning says 0.5 oz is outside the 1 to 3 oz range of the curve-fit. | ±0.01 % | FAIL (width) with WARN (copper weight) | KAT ka-half-oz-internal-narrow and independent script |  | The drawn trace is too narrow, so the width check fails. Step 10 explains why R, V and P are low here. |
| 4 | Typical case, power trace. Import Block T1 and read the results. | Block T1: External layer; I 10 A; dT 20 K; Cu_oz 1; w_act 5 mm; L 80 mm; T_amb 25 degC | A_req = 256.27 mil²; w_min = 4.7237 mm; u_w = 0.94474; trace_R = 8.6552 mohm; vdrop = 86.552 mV; trace_Ploss = 865.52 mW. Bar: PASS. | ±0.01 % | PASS | Independent script (extra row P1) |  | Hand check: A = (10 / (0.048 x 20^0.44))^(1/0.725) = 256.3 mil². w = 256.3 / 1.378 x 0.0254 = 4.724 mm. The 5 mm trace uses 94.5 % of the minimum. |
| 5 | Typical cases, heavy copper. Import Block T2 and read the results. Then import Block T3 and read the results and the warnings. | T2: External layer; I 35 A; dT 100 K; Cu_oz 3; w_act 10 mm; L 300 mm; T_amb 85 degC. T3: I 100 A; dT 40 K; Cu_oz 2; w_act 30 mm; L 500 mm; T_amb 25 degC | T2: A_req = 543.17 mil²; w_min = 3.3373 mm; u_w = 0.33373; trace_R = 8.1195 mohm; vdrop = 284.18 mV; trace_Ploss = 9946.4 mW. Bar: PASS. T3: A_req = 4030.1 mil²; w_min = 37.142 mm; u_w = 1.2381; trace_R = 4.8305 mohm; vdrop = 483.05 mV; trace_Ploss = 48305 mW. Bar: FAIL (Trace width as drawn against the minimum for the rise u = 1.23807). T3 warns that the current, the width and the minimum width (172 mm) are outside the usual range. | ±0.01 % | T2: PASS. T3: FAIL (Trace width as drawn against the minimum for the rise u = 1.23807) | Independent script (extra rows P2 and P4) |  | T2 runs the trace at 185 degC and gives no warning about it. Finding -03 asks for a warning at a laminate limit. Note what you think. |
| 6 | Boundary cases. Import Block L1 and read the results. Then import Block L2 and read them. | L1: Internal layer; I 0.01 A; dT 1 K; w_act 0.05 mm; L 1 mm. L2: Internal layer; I 1 A; dT 300 K; T_amb 500 degC; w_act 1 mm; L 100 mm | L1: A_req = 0.29893 mil²; w_min = 0.00551 mm; u_w = 0.1102; trace_R = 10.083 mohm; vdrop = 0.10083 mV; trace_Ploss = 0.0010083 mW. L2: A_req = 5.3807 mil²; w_min = 0.099179 mm; u_w = 0.099179; trace_R = 200.24 mohm; vdrop = 200.24 mV; trace_Ploss = 200.24 mW. Both are accepted. L1 warns that the rise is below 10 K. | ±0.01 % | L1: PASS. L2: PASS | Independent script (extra rows P3 and P7) |  | L2 runs the copper at 800 degC and prints only range notices. The curve-fit is not valid there. Treat L2 as a test of the arithmetic only. |
| 7 | Unit conversion. Import Block U and read the results. Then change one unit at a time with the unit list and check that the value converts. | Block U: I 3000 mA; T_amb 77 degF; w_act 78.74 mil; L 0.05 m; the other inputs default | A_req = 74.162 mil²; w_min = 1.367 mm; u_w = 0.68349; trace_R = 13.04 mohm; vdrop = 39.119 mV; trace_Ploss = 117.36 mW. The results match step 1 within 0.003 %. | ±0.01 % | PASS | Independent script (extra row, unit entry); hand: 77 degF = 25 degC; 78.74 mil = 2.000 mm |  | The unit list offers only units of the right kind. A current box never offers volts. The result must not move when you change a unit. |
| 8 | NEGATIVE: values and units that the sheet must refuse. Import each line on its own: I 0 A; I 1001 A; dT 0.05 K; w_act 0.005 mm; T_amb 501 degC; Cu_oz 0.05; I in V; I in furlong. Reset after each. | I 0 A; I 1001 A; dT 0.05 K; w_act 0.005 mm; T_amb 501 degC; Cu_oz 0.05; I = 3 V; I = 3 furlong | I: must be at least 0.001 A; 0 A was given. I: must be at most 1000 A; 1001 A was given. dT: must be at least 0.1 K; 0.05 K was given. w_act: must be at least 0.01 mm; 0.005 mm was given. T_amb: must be at most 500 degC; 501 degC was given. Cu_oz: must be at least 0.1; 0.05 was given. I in V: I must be entered in a unit of A, not V. I in furlong: Unknown unit 'furlong'. No results in each case. | Text match | BLOCK | Template hard limits; probe confirms the block messages |  | Use eight separate imports. The unit refusals come from the import. Suspected defect, not yet in a P1 report. The form may keep the number and fall back to amperes with no message. Record FAIL in that case. |
| 9 | NEGATIVE: stale upstream. Follow Procedure L in README.md. UP is Load budget with Block U1. DOWN is this calculator at its defaults. Link I to UP output I_DC. Then open UP, import Block U2 and press Save changes. Open DOWN. | UP: DC load 0.12 kW, then 0.36 kW. DOWN: defaults with I linked to UP output I_DC | After the link: I = 5 A (source link). A_req = 150.03 mil²; w_min = 2.7654 mm; u_w = 1.3827; trace_R = 13.04 mohm; vdrop = 65.199 mV. Bar: FAIL (Trace width as drawn against the minimum for the rise u = 1.38271). The DOWN row ends with STALE. The banner reads that UP now gives a different 'I_DC' than the value 'I' took. Record check is refused: Calculation 'DOWN' is stale. After Update linked inputs: I = 15 A. A_req = 682.77 mil²; w_min = 12.585 mm; u_w = 6.2926; trace_R = 13.04 mohm; vdrop = 195.6 mV. Bar: FAIL (Trace width as drawn against the minimum for the rise u = 6.2926). | ±0.01 % on values | WARN (stale banner); BLOCK (check refused); then FAIL (Trace width as drawn against the minimum for the rise u = 6.2926) after the update | Independent script (trace function with the linked I); hand: I_DC = 0.12 kW / 24 V = 5 A; code rule (staleness) |  | Procedure L in README.md. The trace carries the DC bus current of the load budget. The wider current needs a trace wider than 2 mm. |
| 10 | Known defect P1-elec.pcb-trace-02 (Medium). Import Block K3 (step 3). Read trace_R, vdrop, trace_Ploss, their labels and the warnings. | Block K3 (step 3) | Product: trace_R = 471.47 mohm; vdrop = 471.47 mV; trace_Ploss = 471.47 mW, labelled as values at the operating temperature. Correct: the drawn 0.5 mm trace would rise 65.4 K, not 30 K. So R = 526.3 mohm, vdrop = 526.3 mV and P = 526.3 mW. Or a warning appears when the width check fails, or the labels say 'at the allowed rise'. | ±0.1 % | FAIL | Independent script (actual_rise: the curve-fit inverted for the drawn cross-section) |  | Known defect P1-elec.pcb-trace-02; expect FAIL until fixed. The product R is 10.4 % low. The width check already fails, so the user is warned about the trace. The numbers beside it still mislead. |
| 11 | Known defect P1-elec.pcb-trace-01 (Low). Leave the defaults. Read w_min. Then set t_oz to 1.37 mil and read w_min again. | Defaults; then t_oz 1.37 mil | Product: w_min = 1.367 mm at 1.378 mil. At 1.37 mil: w_min = 1.375 mm, which is 0.58 % wider. Correct: the sheet states which thickness it uses (35.0 um or 34.8 um per oz) and that the other gives a 0.6 % wider trace. | ±0.01 % | FAIL | Independent script (thickness note: 1.378 mil against 1.37 mil) |  | Known defect P1-elec.pcb-trace-01; expect FAIL until the sheet states the thickness. The difference is under 1 %. Confirm 1.378 mil in your copy of IPC-2221. |
| 12 | Known defects P1-elec.pcb-trace-03 and -04 (Low). Enter I 35 A, dT 100 K, T_amb 125 degC, Cu_oz 3, w_act 10 mm. Read every notice. Then import `[]` and read the notices of the default run. | I 35 A; dT 100 K; T_amb 125 degC; Cu_oz 3; w_act 10 mm. Then the defaults | Product: w_min = 3.3373 mm. The trace reaches 225 degC and no notice names the temperature. The default run prints seven notices about default values for k_ext, k_int, n_dT, n_A, t_oz, rho_20 and alpha. Correct: one notice warns about the trace temperature, for example above 105 or 130 degC. The seven default notices become one grouped notice about the method record. | Text match | FAIL | P1 report (probe P7 and P1 notices) |  | Known defects P1-elec.pcb-trace-03 and -04; expect FAIL until fixed. The seven default notices hide real warnings. Write down which ones you see. |

## Import blocks
Copy the whole line of a block to the clipboard. Then press *Import inputs (CSV/JSON)*. The status line must read "Imported n input(s)".

**Block K1** — Internal layer, 3 A, 10 K, 1 oz, trace 4 mm wide. Known-answer case ka-3a-10k-1oz-internal.
```json
[{"Symbol":"I","Number":3,"Unit":"A"},{"Symbol":"dT","Number":10,"Unit":"K"},{"Symbol":"layer","Text":"Internal layer"},{"Symbol":"Cu_oz","Number":1},{"Symbol":"w_act","Number":4,"Unit":"mm"},{"Symbol":"L","Number":50,"Unit":"mm"},{"Symbol":"T_amb","Number":25,"Unit":"degC"}]
```

**Block K2** — External layer, 5 A, 20 K, 2 oz, trace 2.5 mm wide and 100 mm long. Known-answer case ka-5a-20k-2oz-resistance.
```json
[{"Symbol":"I","Number":5,"Unit":"A"},{"Symbol":"dT","Number":20,"Unit":"K"},{"Symbol":"layer","Text":"External layer"},{"Symbol":"Cu_oz","Number":2},{"Symbol":"w_act","Number":2.5,"Unit":"mm"},{"Symbol":"L","Number":100,"Unit":"mm"},{"Symbol":"T_amb","Number":25,"Unit":"degC"}]
```

**Block K3** — Internal layer, 1 A, 30 K, 0.5 oz, trace only 0.5 mm wide and 200 mm long, ambient 40 degC. Known-answer case ka-half-oz-internal-narrow.
```json
[{"Symbol":"I","Number":1,"Unit":"A"},{"Symbol":"dT","Number":30,"Unit":"K"},{"Symbol":"layer","Text":"Internal layer"},{"Symbol":"Cu_oz","Number":0.5},{"Symbol":"w_act","Number":0.5,"Unit":"mm"},{"Symbol":"L","Number":200,"Unit":"mm"},{"Symbol":"T_amb","Number":40,"Unit":"degC"}]
```

**Block T1** — External layer, 10 A, 20 K, 1 oz, trace 5 mm wide and 80 mm long.
```json
[{"Symbol":"I","Number":10,"Unit":"A"},{"Symbol":"dT","Number":20,"Unit":"K"},{"Symbol":"layer","Text":"External layer"},{"Symbol":"Cu_oz","Number":1},{"Symbol":"w_act","Number":5,"Unit":"mm"},{"Symbol":"L","Number":80,"Unit":"mm"},{"Symbol":"T_amb","Number":25,"Unit":"degC"}]
```

**Block T2** — Upper edge of the usual range: 35 A, 100 K, 3 oz, 10 mm wide, ambient 85 degC.
```json
[{"Symbol":"I","Number":35,"Unit":"A"},{"Symbol":"dT","Number":100,"Unit":"K"},{"Symbol":"layer","Text":"External layer"},{"Symbol":"Cu_oz","Number":3},{"Symbol":"w_act","Number":10,"Unit":"mm"},{"Symbol":"L","Number":300,"Unit":"mm"},{"Symbol":"T_amb","Number":85,"Unit":"degC"}]
```

**Block T3** — Very large current: 100 A, 40 K, 2 oz, trace 30 mm wide and 500 mm long.
```json
[{"Symbol":"I","Number":100,"Unit":"A"},{"Symbol":"dT","Number":40,"Unit":"K"},{"Symbol":"layer","Text":"External layer"},{"Symbol":"Cu_oz","Number":2},{"Symbol":"w_act","Number":30,"Unit":"mm"},{"Symbol":"L","Number":500,"Unit":"mm"},{"Symbol":"T_amb","Number":25,"Unit":"degC"}]
```

**Block L1** — Smallest usual values: 10 mA, 1 K rise, internal layer, trace 0.05 mm wide and 1 mm long.
```json
[{"Symbol":"I","Number":0.01,"Unit":"A"},{"Symbol":"dT","Number":1,"Unit":"K"},{"Symbol":"layer","Text":"Internal layer"},{"Symbol":"Cu_oz","Number":1},{"Symbol":"w_act","Number":0.05,"Unit":"mm"},{"Symbol":"L","Number":1,"Unit":"mm"},{"Symbol":"T_amb","Number":25,"Unit":"degC"}]
```

**Block L2** — Extreme: 1 A, 300 K rise at 500 degC ambient, internal layer, 1 mm wide and 100 mm long.
```json
[{"Symbol":"I","Number":1,"Unit":"A"},{"Symbol":"dT","Number":300,"Unit":"K"},{"Symbol":"layer","Text":"Internal layer"},{"Symbol":"Cu_oz","Number":1},{"Symbol":"w_act","Number":1,"Unit":"mm"},{"Symbol":"L","Number":100,"Unit":"mm"},{"Symbol":"T_amb","Number":500,"Unit":"degC"}]
```

**Block U** — The default case in other units: 3000 mA, 77 degF ambient, trace 78.74 mil wide, 0.05 m long.
```json
[{"Symbol":"I","Number":3000,"Unit":"mA"},{"Symbol":"dT","Number":10,"Unit":"K"},{"Symbol":"T_amb","Number":77,"Unit":"degF"},{"Symbol":"layer","Text":"External layer"},{"Symbol":"Cu_oz","Number":1},{"Symbol":"w_act","Number":78.74,"Unit":"mil"},{"Symbol":"L","Number":0.05,"Unit":"m"}]
```

**Block U1** — UP for step 9: Load budget with one DC load of 0.12 kW. The DC bus current is 5 A.
```json
[{"Symbol":"loads","Rows":[{"Cells":{"name":{"Text":"Logic supply"},"supply":{"Text":"DC"},"P":{"Number":0.12,"Unit":"kW"}}}]}]
```

**Block U2** — Block U1 with the DC load raised to 0.36 kW. The DC bus current is 15 A.
```json
[{"Symbol":"loads","Rows":[{"Cells":{"name":{"Text":"Logic supply"},"supply":{"Text":"DC"},"P":{"Number":0.36,"Unit":"kW"}}}]}]
```

## Notes for the tester
- Finding -05 is the sheet label. Step 1 asks you to read it in the sheet header.
- If you have IPC-2152 or a thermal tool, compare one case. The legacy curve-fit may not be conservative for thin boards in vacuum. See oracles.md.
