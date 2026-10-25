# Manual test — fp.accumulator (Accumulator sizing (gas law))
Liability: High. Time: ~60 min. Pre-requisites: a project open in the desktop app. A pocket calculator for the hand checks. Step 10 also needs the calculator fp.cylinder in the same project.

| # | Step | Inputs (value + unit) | Expected output (symbol = value unit) | Tolerance | Expected PASS/FAIL/BLOCK/WARN | Basis | Result | Notes |
|---|---|---|---|---|---|---|---|---|
| 1 | Open the calculator. Press Ctrl+K, type "Open worksheet: Accumulator sizing (gas law)", press Enter. Or use Engineering tree, Modules, Calculations, Worksheets, Fluid Power. Leave every input at its default. | Defaults: Energy storage; dV 10 L; Gauge; p_0 70 bar; p_1 100 bar; p_2 180 bar; n 1; f_rg 1; T_max 50 degC; T_min 20 degC; T_c 20 degC | Header shows fp.accumulator@1.2.0. V0 = 35.479 L; p0 = 64.421 bar; p0_set = 63.407 bar; p0_min = 64.421 bar; f_T = 1.1023; u_r0 = 0.78112; u_rp = 0.70247. Bar: PASS (two checks). PSSR line: indication not available. Notice about the 200 bar limit does not appear. | Shown digits | PASS | independent script (tools/verification/fp.accumulator.py); hand: f_T = 323.15 / 293.15 = 1.1023 |  | The form opens on the template defaults, not on a known-answer case. A gauge entry adds p_atm 1.01325 bar to each pressure. |
| 2 | Import Block A and read the results. Then import Block B and read them again. | Block A: p_basis Absolute; dV 14 L; p_0 90; p_1 100; p_2 210 bar; T_min 50 degC. Block B: the same with Gauge | A: V0 = 29.697 L; p0 = 81.645 bar; u_r0 = 1; u_rp = 0.58333. Bar: PASS. Notice: p_2 is above 200 bar, ideal-gas value shown. B: V0 = 29.807 L; u_r0 = 1.0011; u_rp = 0.57962. Bar: FAIL on the p0/p1 rule (u_r0 above 1). | ±0.01 % | A: PASS. B: FAIL (pre-charge rule) | KAT ka-parker-numbers-absolute and ka-parker-numbers-gauge; independent script |  | Hand check A: V0 = 14 / (90/100 - 90/210) = 29.697 L. The gauge reading adds 1.01325 bar to each pressure. Then p0 sits at 91.01 bar abs. The limit is 0.9 x 101.01 = 90.91 bar. |
| 3 | Import Block C and read V0. Then import Block D and read the results. | Block C: Block A with n 1.4. Block D: Block A with T_min 20 degC (default) and sizes 10, 20, 32, 50 L | C: V0 = 36.693 L. D: f_T = 1.1023; V0 = 32.736 L; V_sel = 50 L; u_V = 0.65472; p0_min = 81.645 bar; u_rp = 0.64303. Bar: PASS. | ±0.01 % | PASS | KAT ka-adiabatic and ka-temperature-range; independent script |  | Hand check D: V0 = 29.697 x 323.15 / 293.15 = 32.736 L. The next listed size is 50 L. |
| 4 | Typical case, shock duty. Import Block S and read the results and the warnings. | Block S: mode Shock / pulsation (screening); dV 3 L; Gauge; p_0 18; p_1 20; p_2 30 bar; n 1.4; T_max 40; T_min 0 degC | V0 = 15.218 L; p0 = 17.799 bar; p0_set = 16.786 bar; p0_min = 16.585 bar; f_T = 1.1464; u_r0 = 1.0054; u_rp = 0.4675. Bar: FAIL by a hair on the p0/p1 rule (u_r0 just above 1). A warning says shock sizing is a gas-volume screening only. | ±0.01 % | FAIL (u_r0 = 1.0054); WARN (shock) | Independent script (tools/verification/fp.accumulator.py, row X10) |  | Hand check: f_T = 313.15 / 273.15 = 1.14644. Lower p_0 to 17.9 bar to clear the FAIL. |
| 5 | Typical case, energy storage with a size list and a vessel rating. Import Block E and read the results. | Block E: dV 5 L; Gauge; p_0 100; p_1 140; p_2 200 bar; n 1.2; T_max 60; T_min 10 degC; PS 220 bar; sizes 25, 32, 40, 50 L | V0 = 30.37 L; f_T = 1.1766; V_sel = 32 L; u_V = 0.94906; u_r0 = 0.79593; u_rp = 0.58534; bar_litres = 7040 bar·L. pssr: "7040 bar·L is at or above 250 bar·L: the exception does not apply; PSSR and PED / UK PER duties follow." Bar: PASS (two checks). | ±0.01 % | PASS; WARN (p_2 above 200 bar absolute) | Independent script (acc function in fp.accumulator.py) |  | Hand check: bar·L = PS x V_sel = 220 x 32 = 7040. The 250 bar·L line is an indication only. |
| 6 | Typical case, emergency stroke. Import Block H and read the results. | Block H: mode Emergency stroke; dV 3.92699 L; Gauge; p_0 105; p_1 120; p_2 160 bar; n 1.4; T_max 50; T_min 10 degC | V0 = 26.695 L; p0 = 96.171 bar; p0_min = 92.891 bar; f_T = 1.1413; u_r0 = 0.97339; u_rp = 0.43334. Bar: PASS. No warning appears in this mode. | ±0.01 % | PASS | Independent script (row X16, the pack accumulator) |  | This is the sizing in the hydraulic power unit pack. Steps 11 and 12 show why 26.695 L is short. The sheet gives no emergency-stroke warning, even though the mode changes the physics. |
| 7 | Boundary cases. Import Block L1 and read V0. Then import Block L2 and read V0 and u_r0. | L1: dV 0.001 L; p_0 0 bar gauge; p_1 5; p_2 10 bar; T 20 degC. L2: Absolute; p_0 100; p_1 100; p_2 200 bar; T 20 degC | L1: V0 = 0.013072 L; p0 = 1.0132 bar; u_r0 = 0.18723; u_rp = 2.7173. L2: V0 = 20 L; u_r0 = 1.1111; u_rp = 0.5 (FAIL on the p0/p1 rule). | ±0.01 % | L1: FAIL (u_rp above 1). L2: FAIL (u_r0 = 1.1111) | Independent script (rows X08 and X03) |  | Hand check L2: V0 = 10 / (100/100 - 100/200) = 20 L. The sheet accepts p_0 equal to p_1 but fails the 0.9 rule. |
| 8 | NEGATIVE: values outside the limits. Import each line on its own: dV 0.0009 L; p_2 701 bar; f_rg 0.9; n 1.5. Reset after each. | `[{"Symbol":"dV","Number":0.0009,"Unit":"L"}]`, then p_2 701 bar, f_rg 0.9, n 1.5 (same form) | dV: must be at least 0.001 L; 0.0009 L was given. p_2: must be at most 700 bar; 701 bar was given. f_rg: must be at least 1; 0.9 was given. n: must be at most 1.4; 1.5 was given. No results in each case. | Text match | BLOCK | Template hard limits; probe confirms the message wording |  | Use four separate one-line imports. The field shows the problem and the bar reads Blocked. Save as calculation stays disabled. |
| 9 | NEGATIVE: wrong unit and impossible pressures. Import `[{"Symbol":"dV","Number":14,"Unit":"bar"}]`, then the same with unit `furlong`. Then import p_0 120 bar and p_1 100 bar. | dV 14 bar; dV 14 furlong; p_0 120 bar with p_1 100 bar (gauge) | dV in bar: dV must be entered in a unit of L, not bar. dV in furlong: Unknown unit 'furlong'. p_0 above p_1: the pre-charge pressure (121.01 bar abs) must not be above p_1 (101.01 bar abs). The text adds: lower p_0. No results in each case. | Text match | BLOCK | Hand check (a pressure is not a volume); probe confirms the block messages |  | Suspected defect, not yet in a P1 report. The form may keep the number and fall back to litres with no message. If dV shows 14 L after the first import, record FAIL and write what you saw. |
| 10 | NEGATIVE: stale upstream. Follow Procedure L in README.md. UP is Cylinder and actuator circuit at its defaults. DOWN is this calculator at its defaults. Link p_2 to UP output p. Then open UP, change p from 160 to 200 bar and press Save changes. Open DOWN. | UP: p 160 bar (default), then 200 bar. DOWN: defaults with p_2 linked to UP output p | After the link: p_2 = 160 bar, source link; V0 = 42.079 L. The DOWN row ends with STALE. The banner reads that UP now gives a different 'p' than the value 'p_2' took. Record check is refused: Calculation 'DOWN' is stale. After Update linked inputs: p_2 = 200 bar; V0 = 31.519 L; u_rp = 0.78008. | ±0.01 % on values | WARN (stale banner); BLOCK (check refused); then PASS after the update | Independent script (acc function); code rule (staleness) |  | Procedure L in README.md. The link carries gauge pressure. Leave the basis on Gauge. With Absolute basis the sheet warns that the linked value reads 1.0132 bar low. Try that as a bonus. |
| 11 | Known defect P1-fp.accumulator-R1 (Critical). Import Block R1 and read V0. | Block R1: mode Emergency stroke; Absolute; dV 14 L; p_0 90; p_1 100; p_2 200 bar; n 1.4; T_max = T_min = 20 degC | Product: V0 = 38.654 L. Correct: the gas is held at p_2 and cools, so the charge is isothermal and only the stroke is adiabatic. V0 = dV / [(p0/p2) x ((p2/p1)^(1/n) - 1)] = 48.56 L. The product is 79.6 % of that. | ±0.1 % | FAIL | Independent script (fp.accumulator.py, Model note 4: v0_hold) |  | Known defect P1-fp.accumulator-R1; expect FAIL until fixed. The sheet undersizes by about 20 %. The pack example in step 6 is short by 11 % for an ideal gas. |
| 12 | Known defect P1-fp.accumulator-R2 (High). Import Block R2 and read V0 and the warnings. | Block R2: Absolute; dV 10 L; p_0 90; p_1 100; p_2 200 bar; n 1.4; T_max = T_min = 20 degC | Product: V0 = 27.61 L and no real-gas notice. Correct: real nitrogen has an effective exponent near 1.68 between 100 and 200 bar. The adiabatic volume needs a factor of 1.144, so V0 = 31.59 L. A warning is expected for every n above 1 from about 50 bar absolute. | ±1 % | FAIL | Independent script (Model note 3, reference equation of state for nitrogen; the factor 1.144 is the reviewer's value) |  | Known defect P1-fp.accumulator-R2; expect FAIL until fixed. Owner to confirm the exponent against a nitrogen property table. |
| 13 | Known defect P1-fp.accumulator-01 (High). Import Block P1 and read V0 and the warning. Then import Block P1b. | Block P1 as listed. Block P1b adds f_rg 2.007 | Product P1: V0 = 1518.7 L, the ideal-gas value, with a warning that asks for f_rg. Product P1b: the form refuses f_rg 2.007 because the limit is 1.5. Correct: the sheet accepts f_rg 2.007 or computes the factor. V0 is then about 3048 L. Or it blocks p_2 above 200 bar absolute while f_rg is 1. | ±1 % | FAIL | Independent script (Model note 2; factors 1.745 at 450/500/700 and 2.007 at 600/650/700 bar abs from the reference equation, reviewer values) |  | Known defect P1-fp.accumulator-01; expect FAIL until fixed. No valid input gives a safe volume at these pressures. Owner to confirm Z against a NIST or REFPROP table. |
| 14 | Known defect P1-fp.accumulator-02 (High). Import Block P2 and read V0 and the warnings. | Block P2: Absolute; dV 10 L; p_0 170; p_1 190; p_2 200 bar; T_max = T_min = 20 degC | Product: V0 = 223.53 L and no real-gas notice. Correct: the real-gas factor for this window is 1.134, so V0 = 253.5 L. A warning appears from about 100 bar absolute. | ±1 % | FAIL | Independent script (Model note 2, reference equation of state, reviewer value 1.134) |  | Known defect P1-fp.accumulator-02; expect FAIL until fixed. The sheet is 12 % short here and says nothing. |

## Import blocks
Copy the whole line of a block to the clipboard. Then press *Import inputs (CSV/JSON)*. The status line must read "Imported n input(s)".

**Block A** — Parker numbers on absolute pressures, isothermal. Known-answer case ka-parker-numbers-absolute.
```json
[{"Symbol":"p_basis","Text":"Absolute"},{"Symbol":"dV","Number":14,"Unit":"L"},{"Symbol":"p_0","Number":90,"Unit":"bar"},{"Symbol":"p_1","Number":100,"Unit":"bar"},{"Symbol":"p_2","Number":210,"Unit":"bar"},{"Symbol":"T_min","Number":50,"Unit":"degC"}]
```

**Block B** — The same numbers entered as gauge pressures.
```json
[{"Symbol":"dV","Number":14,"Unit":"L"},{"Symbol":"p_0","Number":90,"Unit":"bar"},{"Symbol":"p_1","Number":100,"Unit":"bar"},{"Symbol":"p_2","Number":210,"Unit":"bar"},{"Symbol":"T_min","Number":50,"Unit":"degC"}]
```

**Block C** — Block A with n 1.4. Known-answer case ka-adiabatic.
```json
[{"Symbol":"p_basis","Text":"Absolute"},{"Symbol":"dV","Number":14,"Unit":"L"},{"Symbol":"p_0","Number":90,"Unit":"bar"},{"Symbol":"p_1","Number":100,"Unit":"bar"},{"Symbol":"p_2","Number":210,"Unit":"bar"},{"Symbol":"n","Number":1.4},{"Symbol":"T_min","Number":50,"Unit":"degC"}]
```

**Block D** — Block A with the default temperatures 50 and 20 degC and a size list 10, 20, 32 and 50 L. Known-answer case ka-temperature-range.
```json
[{"Symbol":"p_basis","Text":"Absolute"},{"Symbol":"dV","Number":14,"Unit":"L"},{"Symbol":"p_0","Number":90,"Unit":"bar"},{"Symbol":"p_1","Number":100,"Unit":"bar"},{"Symbol":"p_2","Number":210,"Unit":"bar"},{"Symbol":"sizes","Rows":[{"Cells":{"V":{"Number":10,"Unit":"L"}}},{"Cells":{"V":{"Number":20,"Unit":"L"}}},{"Cells":{"V":{"Number":32,"Unit":"L"}}},{"Cells":{"V":{"Number":50,"Unit":"L"}}}]}]
```

**Block S** — Shock duty, gauge pressures, adiabatic, 0 to 40 degC.
```json
[{"Symbol":"mode","Text":"Shock / pulsation (screening)"},{"Symbol":"dV","Number":3,"Unit":"L"},{"Symbol":"p_0","Number":18,"Unit":"bar"},{"Symbol":"p_1","Number":20,"Unit":"bar"},{"Symbol":"p_2","Number":30,"Unit":"bar"},{"Symbol":"n","Number":1.4},{"Symbol":"T_max","Number":40,"Unit":"degC"},{"Symbol":"T_min","Number":0,"Unit":"degC"}]
```

**Block E** — Energy storage, gauge, n 1.2, 10 to 60 degC, size list 25, 32, 40 and 50 L, PS 220 bar.
```json
[{"Symbol":"dV","Number":5,"Unit":"L"},{"Symbol":"p_0","Number":100,"Unit":"bar"},{"Symbol":"p_1","Number":140,"Unit":"bar"},{"Symbol":"p_2","Number":200,"Unit":"bar"},{"Symbol":"n","Number":1.2},{"Symbol":"T_max","Number":60,"Unit":"degC"},{"Symbol":"T_min","Number":10,"Unit":"degC"},{"Symbol":"PS","Number":220,"Unit":"bar"},{"Symbol":"sizes","Rows":[{"Cells":{"V":{"Number":25,"Unit":"L"}}},{"Cells":{"V":{"Number":32,"Unit":"L"}}},{"Cells":{"V":{"Number":40,"Unit":"L"}}},{"Cells":{"V":{"Number":50,"Unit":"L"}}}]}]
```

**Block H** — The accumulator step of the hydraulic power unit pack: emergency stroke, gauge, n 1.4, 10 to 50 degC.
```json
[{"Symbol":"mode","Text":"Emergency stroke"},{"Symbol":"dV","Number":3.92699,"Unit":"L"},{"Symbol":"p_0","Number":105,"Unit":"bar"},{"Symbol":"p_1","Number":120,"Unit":"bar"},{"Symbol":"p_2","Number":160,"Unit":"bar"},{"Symbol":"n","Number":1.4},{"Symbol":"T_max","Number":50,"Unit":"degC"},{"Symbol":"T_min","Number":10,"Unit":"degC"}]
```

**Block L1** — Smallest dV and p_0 at zero gauge.
```json
[{"Symbol":"dV","Number":0.001,"Unit":"L"},{"Symbol":"p_0","Number":0,"Unit":"bar"},{"Symbol":"p_1","Number":5,"Unit":"bar"},{"Symbol":"p_2","Number":10,"Unit":"bar"},{"Symbol":"T_max","Number":20,"Unit":"degC"},{"Symbol":"T_min","Number":20,"Unit":"degC"}]
```

**Block L2** — p_0 equal to p_1, absolute, isothermal.
```json
[{"Symbol":"p_basis","Text":"Absolute"},{"Symbol":"dV","Number":10,"Unit":"L"},{"Symbol":"p_0","Number":100,"Unit":"bar"},{"Symbol":"p_1","Number":100,"Unit":"bar"},{"Symbol":"p_2","Number":200,"Unit":"bar"},{"Symbol":"T_max","Number":20,"Unit":"degC"},{"Symbol":"T_min","Number":20,"Unit":"degC"}]
```

**Block R1** — Emergency stroke after a long hold at p_2: 14 L, absolute, n 1.4, 20 degC.
```json
[{"Symbol":"mode","Text":"Emergency stroke"},{"Symbol":"p_basis","Text":"Absolute"},{"Symbol":"dV","Number":14,"Unit":"L"},{"Symbol":"p_0","Number":90,"Unit":"bar"},{"Symbol":"p_1","Number":100,"Unit":"bar"},{"Symbol":"p_2","Number":200,"Unit":"bar"},{"Symbol":"n","Number":1.4},{"Symbol":"T_max","Number":20,"Unit":"degC"},{"Symbol":"T_min","Number":20,"Unit":"degC"}]
```

**Block R2** — Energy storage, absolute, n 1.4, 90/100/200 bar, 20 degC.
```json
[{"Symbol":"p_basis","Text":"Absolute"},{"Symbol":"dV","Number":10,"Unit":"L"},{"Symbol":"p_0","Number":90,"Unit":"bar"},{"Symbol":"p_1","Number":100,"Unit":"bar"},{"Symbol":"p_2","Number":200,"Unit":"bar"},{"Symbol":"n","Number":1.4},{"Symbol":"T_max","Number":20,"Unit":"degC"},{"Symbol":"T_min","Number":20,"Unit":"degC"}]
```

**Block P1** — Highest pressures: 100 L, gauge 600/650/700 bar, 40 degC, PS 700 bar, vessel 5000 L.
```json
[{"Symbol":"dV","Number":100,"Unit":"L"},{"Symbol":"p_0","Number":600,"Unit":"bar"},{"Symbol":"p_1","Number":650,"Unit":"bar"},{"Symbol":"p_2","Number":700,"Unit":"bar"},{"Symbol":"T_max","Number":40,"Unit":"degC"},{"Symbol":"T_min","Number":40,"Unit":"degC"},{"Symbol":"PS","Number":700,"Unit":"bar"},{"Symbol":"V_vessel","Number":5000,"Unit":"L"}]
```

**Block P1b** — Block P1 with the real-gas factor 2.007 that nitrogen needs here.
```json
[{"Symbol":"dV","Number":100,"Unit":"L"},{"Symbol":"p_0","Number":600,"Unit":"bar"},{"Symbol":"p_1","Number":650,"Unit":"bar"},{"Symbol":"p_2","Number":700,"Unit":"bar"},{"Symbol":"T_max","Number":40,"Unit":"degC"},{"Symbol":"T_min","Number":40,"Unit":"degC"},{"Symbol":"PS","Number":700,"Unit":"bar"},{"Symbol":"V_vessel","Number":5000,"Unit":"L"},{"Symbol":"f_rg","Number":2.007}]
```

**Block P2** — Narrow window: absolute 170/190/200 bar, 10 L, 20 degC.
```json
[{"Symbol":"p_basis","Text":"Absolute"},{"Symbol":"dV","Number":10,"Unit":"L"},{"Symbol":"p_0","Number":170,"Unit":"bar"},{"Symbol":"p_1","Number":190,"Unit":"bar"},{"Symbol":"p_2","Number":200,"Unit":"bar"},{"Symbol":"T_max","Number":20,"Unit":"degC"},{"Symbol":"T_min","Number":20,"Unit":"degC"}]
```

## Notes for the tester
- Warnings in the panel print below the results as Warning lines. The sheet also prints a default-value notice for each input you did not type. Ignore those.
- The pack file chain.hydraulic-power-unit.md runs this calculator with linked inputs and explains the pack consequence of steps 11 and 12.
