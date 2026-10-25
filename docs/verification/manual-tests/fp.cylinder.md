# Manual test — fp.cylinder (Cylinder and actuator circuit)
Liability: Medium. Time: ~55 min. Pre-requisites: a project open in the desktop app. A pocket calculator for the hand checks.

| # | Step | Inputs (value + unit) | Expected output (symbol = value unit) | Tolerance | Expected PASS/FAIL/BLOCK/WARN | Basis | Result | Notes |
|---|---|---|---|---|---|---|---|---|
| 1 | Open the calculator. Press Ctrl+K, type "Open worksheet: Cylinder and actuator circuit", press Enter. Or use Engineering tree, Modules, Calculations, Worksheets, Fluid Power. Leave every input at its default. | Defaults: Standard; p 160 bar; Q 36 L/min; D 100 mm; d_r 70 mm; stroke 500 mm; F_push 80 kN; F_pull 40 kN; d_prs 16 mm; no pump | Header shows fp.cylinder@1.2.0. A_b = 78.54 cm²; A_a = 40.055 cm²; F_ext = 119.38 kN; F_ret = 60.884 kN; v_ext = 74.866 mm/s; v_ret = 146.8 mm/s; t_cycle = 10.085 s; v_prs = 5.8513 m/s; u_prs = 1.1703; u_buck = 0.24601. Bar: 5 of 6 PASS; FAIL: Pressure line velocity within its limit (u = 1.17026). | Shown digits | FAIL (pressure line velocity) | independent script (tools/verification/fp.cylinder.py); hand: A_b = pi x 100² / 4 = 7853.98 mm² |  | The default 16 mm pressure line is too small for the larger stroke flow. The FAIL is by design. Step 2 uses the 20 mm line. |
| 2 | Import Block K1 and read the results. | Block K1: p 160 bar; Q 36 L/min; D 100; d_r 70; stroke 500 mm; V_g 28 cm³/rev; n_p 1450 rpm; V_gm 16 cm³/rev; dp_m 160 bar; dp_o 10 bar; d_o 3 mm; hoses 8 to 50 mm | F_ext = 119.38 kN; F_ret = 60.884 kN; v_ext = 74.866 mm/s; v_ret = 146.8 mm/s; t_cycle = 10.085 s; Q_p = 38.57 L/min; P_pump = 11.688 kW; T_shaft = 76.973 N·m; T_motor = 36.669 N·m; Q_prs = 75.627 L/min; v_prs = 6.269 m/s; u_prs = 1.2538; d_rec_prs = 20 mm; Q_o = 12.608 L/min; F_cr = 1787.8 kN; u_buck = 0.24601. Bar: 6 of 7 PASS; FAIL: Pressure line velocity within its limit (u = 1.2538). | ±0.01 % | FAIL (Pressure line velocity within its limit) | KAT ka-circuit-standard and independent script |  | Hand check: F_ext = (160e5 x 7.854e-3) x 0.95 = 119.38 kN. Q_p = 28 x 1450 x 0.95 = 38.57 L/min. The recommended line is 20 mm. |
| 3 | Import Block K2 and read the results. | Block K2: mode Regenerative extend; F_push 40 kN; V_g 28; n_p 1450; d_prs 25 mm; d_ret 25 mm; other inputs default | F_std = 119.38 kN; F_ext = 58.496 kN; v_ext = 152.79 mm/s; t_ext = 3.2725 s; t_cycle = 6.6786 s; dV_ext = 1.9242 L; dV = 2.0028 L; Q_prs = 78.714 L/min; v_prs = 2.6726 m/s; u_prs = 0.53452; u_push = 0.6838. Bar: 7 of 7 checks PASS. | ±0.01 % | PASS | KAT ka-regenerative and independent script |  | Hand check: F_ext = p x A_r x eta_m = 160e5 x 3.845e-3 x 0.95 = 58.50 kN. The cap-side flow is Q x A_b / A_r = 36 x 2.0408 = 73.5 L/min before the pump; the sheet uses the pump delivery. |
| 4 | Import Block K3 and read the rod results. Then import Block K4 and read the forces. Then import Block K5 and read the orifice. | K3: D 80; d_r 40; stroke 2000 mm; F_push 15 kN. K4: p_back 10 bar; K_m 2; stroke 300 mm. K5: Q 22.6195 L/min; rho 800; C_d 0.6; dp_o 10 bar; d_o 4 mm | K3: L_eff = 2100 mm; lam = 210; lam_c = 91.052; F_cr = 59.06 kN; F_stall = 80.425 kN; u_buck = 4.7661 (Euler branch; FAIL). K4: F_ext = 115.58 kN; F_ret = 53.423 kN; L_eff = 800 mm; F_cr = 1681.7 kN; u_buck = 0.26153. K5: d_o_req = 4 mm; Q_o = 22.619 L/min. | ±0.01 % | K3: FAIL (Rod buckling at the larger of the push load and the stall thrust). K4: FAIL (Pressure line velocity within its limit). K5: PASS | KATs ka-euler-slender, ka-back-pressure-fixed-free, ka-orifice and independent script |  | Hand check K5: Q = 0.6 x pi x 0.004² / 4 x sqrt(2 x 10e5 / 800) = 22.62 L/min. Rod slenderness K3: lam = 2100 / 10 = 210, above 91.05, so Euler applies. |
| 5 | Typical case, regenerative extend. Import Block T1 and read the results. | Block T1: mode Regenerative extend; p 120 bar; Q 60 L/min; D 63; d_r 36; stroke 800 mm; F_push 25; F_pull 20 kN; V_g 25; n_p 1500; d_prs 20; d_ret 20 mm; K_m 0.7; hoses | F_ext = 11.604 kN; F_std = 35.537 kN; v_ext = 962.79 mm/s; t_cycle = 2.5447 s; dV = 1.6795 L; Q_prs = 183.75 L/min; v_prs = 9.7482 m/s; d_rec_prs = 32 mm; P_pump = 8.0966 kW; T_shaft = 51.544 N·m; Vg_req = 42.105 cm³/rev; F_cr = 358.54 kN; u_buck = 0.36516; u_push = 2.1545. Bar: 3 of 7 PASS; FAIL: Extend force covers the push load (u = 2.15447), Pressure line velocity within its limit (u = 1.94965), Return line velocity within its limit (u = 1.57547), Pump delivery covers the circuit flow (u = 1.68421). | ±0.01 % | FAIL (Extend force covers the push load; Pressure line velocity within its limit; Return line velocity within its limit; Pump delivery covers the circuit flow) | Independent script (row x1) |  | Hand check: F_ext = 120e5 x pi x 0.036² / 4 x 0.95 = 11.60 kN. It misses the 25 kN push, so the push check fails. Read which checks fail. |
| 6 | Typical case, back pressure and motor. Import Block T2 and read the results. | Block T2: p 250 bar; p_back 40 bar; Q 90 L/min; D 125; d_r 90; stroke 1200 mm; K_m 0.7; F_push 200; F_pull 100 kN; V_gm 32 cm³/rev; dp_m 200 bar | F_ext = 269 kN; F_ret = 93.732 kN; u_pull = 1.0669; T_motor = 91.673 N·m; P_pump = 42.614 kW; v_prs = 15.491 m/s; lam = 40.444; F_cr = 2867.1 kN; u_buck = 0.37453. Bar: 3 of 6 PASS; FAIL: Retract force covers the pull load (u = 1.06687), Pressure line velocity within its limit (u = 3.09817), Return line velocity within its limit (u = 2.11502). | ±0.01 % | FAIL (Retract force covers the pull load; Pressure line velocity within its limit; Return line velocity within its limit) | Independent script (row x2) |  | Hand check: F_ret = (250e5 x 59.10e-4 - 40e5 x 122.72e-4) x 0.95 = 93.73 kN. T_motor = 200e5 x 32e-6 / (2 pi) x 0.9 = 91.67 N·m. |
| 7 | Boundary cases. Import Block L1 and read the results. Then import Block L2 and read the results. | L1: smallest values as listed. L2: largest values as listed | L1: F_ext = 0.0074613 kN; F_ret = 0.0062675 kN; v_ext = 20.796 mm/s; t_cycle = 0.088478 s; lam = 1; F_cr = 6.2828 kN; u_buck = 0.0043753. L2: F_ext = 13057 kN; v_ext = 166.37 mm/s; dV = 981.75 L; Q_p = 1900 L/min; P_pump = 2518.9 kW; T_shaft = 12027 N·m; u_pump = 1.0526; F_cr = 6675.1 kN; u_buck = 7.865. | ±0.01 % | L1: PASS. L2: FAIL (Extend force covers the push load; Rod buckling at the larger of the push load and the stall thrust; Pressure line velocity within its limit; Return line velocity within its limit; Pump delivery covers the circuit flow) | Independent script (rows x3 and x4) |  | All inputs are accepted at their limits. In L2 the pump delivers 1900 L/min against 2000 L/min, so u_pump is above 1. |
| 8 | Boundary between the Johnson and Euler branches. Import Block C1 and read lam, F_cr and u_buck. Then import Block C2. | C1: stroke 910 mm. C2: stroke 911 mm. Rod 40 mm, bore 80 mm, L_add 0, F_push 10 kN | C1: lam = 91; lam_c = 91.052; F_cr = 314.52 kN; u_buck = 0.89498 (Johnson). C2: lam = 91.1; F_cr = 313.83 kN; u_buck = 0.89694 (Euler). The two branches meet within 0.2 %. | ±0.01 % | C1: PASS. C2: PASS | Independent script (rows x5 and x6) |  | The critical load is nearly continuous across lam_c = 91.05. A jump would signal a branch error. |
| 9 | NEGATIVE: values and units that the sheet must refuse. Import each line on its own: d_r 100 mm; D 9 mm; p 100 bar with p_back 51 bar; V_g 28 cm³/rev alone; p 160 mm; p 160 furlong. Reset after each. | d_r 100 mm (equals the bore); D 9 mm; p 100 bar and p_back 51 bar; V_g 28 cm³/rev without n_p; p = 160 mm; p = 160 furlong | d_r: the rod diameter (100 mm) must be smaller than the bore D (100 mm). D: must be at least 10 mm; 9 mm was given. p_back: the back pressure (51 bar) takes all the force on the retract stroke at p = 100 bar. V_g: enter the pump speed as well as its displacement, or clear both. p in mm: p must be entered in a unit of bar, not mm. p in furlong: Unknown unit 'furlong'. No results in each case. | Text match | BLOCK | Template rules and hard limits; probe confirms the block messages |  | Use six separate imports. p_back 50.9 bar passes at p 100 bar. The unit refusals come from the import. Suspected defect, not yet in a P1 report: the form may keep the number and fall back to bar with no message. Record FAIL in that case. |
| 10 | Known defect P1-fp.cylinder-01 (Low). Import Block D0 and read F_ext. Then import Block D1 and read F_ext again. | Block D0: Regenerative extend, p_back 0. Block D1: p_back 30 bar | Product: F_ext = 58.496 kN in both cases. Correct: the 30 bar drop from the annulus to the cap lowers the force by p_back x A_a, about 12 kN, or the sheet warns that F_ext is a lossless upper bound. | ±0.1 % | FAIL | Independent script (row x7); hand: 30e5 x 40.06e-4 = 12.0 kN before efficiency |  | Known defect P1-fp.cylinder-01; expect FAIL until fixed. The sheet states the omission in its assumption text only. The report estimate is 11.4 kN. |
| 11 | Known defect P1-fp.cylinder-03 (Low). Import Block D2 and read Q_p, u_pump, v_ext and t_cycle. | Block D2: V_g 20 cm³/rev; n_p 1450 rpm; other inputs default | Product: Q_p = 27.55 L/min; u_pump = 1.3067 (FAIL); v_ext = 74.866 mm/s and t_cycle = 10.085 s, both worked from 36 L/min. Correct: speed follows the delivery, v_ext = 57.294 mm/s, or a second speed is shown. | ±0.1 % | FAIL | Independent script (row x4 shows the same effect); hand: v = Q_p x eta_vc / A_b |  | Known defect P1-fp.cylinder-03; expect FAIL until fixed. The pump check does fail, but the speed and the cycle time stay optimistic. |
| 12 | Known defect P1-fp.cylinder-04 (Low). Import Block K1 again. Read Q_line, Q_prs and u_prs. | Block K1 (step 2) | Product: Q_line = 38.57 L/min (the pump delivery) but Q_prs = 75.627 L/min and u_prs = 1.2538 (FAIL). Correct: the output offered to the pressure-line sheet is a pump-line flow of 38.57 L/min, or Q_prs is named as the cap-side retract flow. | Shown digits | FAIL | Independent script (KAT ka-circuit-standard: Q_prs 75.627 L/min is Q_p x A_b / A_a) |  | Known defect P1-fp.cylinder-04; expect FAIL until fixed. In the hydraulic power unit pack this flow feeds the pressure line. See chain.hydraulic-power-unit.md. |

## Import blocks
Copy the whole line of a block to the clipboard. Then press *Import inputs (CSV/JSON)*. The status line must read "Imported n input(s)".

**Block K1** — Standard circuit with pump 28 cm³/rev at 1450 rpm, motor 16 cm³/rev at 160 bar drop, orifice 3 mm at 10 bar, hose list 8 to 50 mm. Known-answer case ka-circuit-standard.
```json
[{"Symbol":"p","Number":160,"Unit":"bar"},{"Symbol":"p_back","Number":0,"Unit":"bar"},{"Symbol":"Q","Number":36,"Unit":"L/min"},{"Symbol":"rho","Number":870,"Unit":"kg/m^3"},{"Symbol":"F_push","Number":80,"Unit":"kN"},{"Symbol":"F_pull","Number":40,"Unit":"kN"},{"Symbol":"D","Number":100,"Unit":"mm"},{"Symbol":"d_r","Number":70,"Unit":"mm"},{"Symbol":"stroke","Number":500,"Unit":"mm"},{"Symbol":"V_g","Number":28,"Unit":"cm^3/rev"},{"Symbol":"n_p","Number":1450,"Unit":"rpm"},{"Symbol":"V_gm","Number":16,"Unit":"cm^3/rev"},{"Symbol":"dp_m","Number":160,"Unit":"bar"},{"Symbol":"d_prs","Number":16,"Unit":"mm"},{"Symbol":"dp_o","Number":10,"Unit":"bar"},{"Symbol":"d_o","Number":3,"Unit":"mm"},{"Symbol":"hoses","Rows":[{"Cells":{"ID":{"Number":8,"Unit":"mm"}}},{"Cells":{"ID":{"Number":10,"Unit":"mm"}}},{"Cells":{"ID":{"Number":12.5,"Unit":"mm"}}},{"Cells":{"ID":{"Number":16,"Unit":"mm"}}},{"Cells":{"ID":{"Number":20,"Unit":"mm"}}},{"Cells":{"ID":{"Number":25,"Unit":"mm"}}},{"Cells":{"ID":{"Number":32,"Unit":"mm"}}},{"Cells":{"ID":{"Number":40,"Unit":"mm"}}},{"Cells":{"ID":{"Number":50,"Unit":"mm"}}}]}]
```

**Block K2** — Regenerative extend, pump 28 cm³/rev at 1450 rpm, 25 mm lines. Known-answer case ka-regenerative.
```json
[{"Symbol":"mode","Text":"Regenerative extend"},{"Symbol":"p","Number":160,"Unit":"bar"},{"Symbol":"Q","Number":36,"Unit":"L/min"},{"Symbol":"F_push","Number":40,"Unit":"kN"},{"Symbol":"F_pull","Number":40,"Unit":"kN"},{"Symbol":"D","Number":100,"Unit":"mm"},{"Symbol":"d_r","Number":70,"Unit":"mm"},{"Symbol":"stroke","Number":500,"Unit":"mm"},{"Symbol":"V_g","Number":28,"Unit":"cm^3/rev"},{"Symbol":"n_p","Number":1450,"Unit":"rpm"},{"Symbol":"d_prs","Number":25,"Unit":"mm"},{"Symbol":"d_ret","Number":25,"Unit":"mm"}]
```

**Block K3** — Slender rod: bore 80 mm, rod 40 mm, stroke 2000 mm, push load 15 kN. Known-answer case ka-euler-slender.
```json
[{"Symbol":"p","Number":160,"Unit":"bar"},{"Symbol":"Q","Number":36,"Unit":"L/min"},{"Symbol":"F_push","Number":15,"Unit":"kN"},{"Symbol":"F_pull","Number":40,"Unit":"kN"},{"Symbol":"D","Number":80,"Unit":"mm"},{"Symbol":"d_r","Number":40,"Unit":"mm"},{"Symbol":"stroke","Number":2000,"Unit":"mm"}]
```

**Block K4** — Back pressure 10 bar and a fixed-free mounting (K 2), stroke 300 mm. Known-answer case ka-back-pressure-fixed-free.
```json
[{"Symbol":"p","Number":160,"Unit":"bar"},{"Symbol":"p_back","Number":10,"Unit":"bar"},{"Symbol":"Q","Number":36,"Unit":"L/min"},{"Symbol":"F_push","Number":80,"Unit":"kN"},{"Symbol":"F_pull","Number":40,"Unit":"kN"},{"Symbol":"D","Number":100,"Unit":"mm"},{"Symbol":"d_r","Number":70,"Unit":"mm"},{"Symbol":"stroke","Number":300,"Unit":"mm"},{"Symbol":"K_m","Number":2}]
```

**Block K5** — Orifice screening: density 800 kg/m³, C_d 0.6, 10 bar across a 4 mm orifice. Known-answer case ka-orifice.
```json
[{"Symbol":"p","Number":160,"Unit":"bar"},{"Symbol":"Q","Number":22.619467105846514,"Unit":"L/min"},{"Symbol":"rho","Number":800,"Unit":"kg/m^3"},{"Symbol":"D","Number":100,"Unit":"mm"},{"Symbol":"d_r","Number":70,"Unit":"mm"},{"Symbol":"stroke","Number":500,"Unit":"mm"},{"Symbol":"C_d","Number":0.6},{"Symbol":"dp_o","Number":10,"Unit":"bar"},{"Symbol":"d_o","Number":4,"Unit":"mm"}]
```

**Block T1** — Regenerative extend on a 63/36 cylinder at 120 bar, 60 L/min, stroke 800 mm, pump 25 cm³/rev at 1500 rpm, K 0.7, hose list 8 to 50 mm.
```json
[{"Symbol":"mode","Text":"Regenerative extend"},{"Symbol":"p","Number":120,"Unit":"bar"},{"Symbol":"Q","Number":60,"Unit":"L/min"},{"Symbol":"D","Number":63,"Unit":"mm"},{"Symbol":"d_r","Number":36,"Unit":"mm"},{"Symbol":"stroke","Number":800,"Unit":"mm"},{"Symbol":"F_push","Number":25,"Unit":"kN"},{"Symbol":"F_pull","Number":20,"Unit":"kN"},{"Symbol":"V_g","Number":25,"Unit":"cm^3/rev"},{"Symbol":"n_p","Number":1500,"Unit":"rpm"},{"Symbol":"d_prs","Number":20,"Unit":"mm"},{"Symbol":"d_ret","Number":20,"Unit":"mm"},{"Symbol":"K_m","Number":0.7},{"Symbol":"hoses","Rows":[{"Cells":{"ID":{"Number":8,"Unit":"mm"}}},{"Cells":{"ID":{"Number":10,"Unit":"mm"}}},{"Cells":{"ID":{"Number":12.5,"Unit":"mm"}}},{"Cells":{"ID":{"Number":16,"Unit":"mm"}}},{"Cells":{"ID":{"Number":20,"Unit":"mm"}}},{"Cells":{"ID":{"Number":25,"Unit":"mm"}}},{"Cells":{"ID":{"Number":32,"Unit":"mm"}}},{"Cells":{"ID":{"Number":40,"Unit":"mm"}}},{"Cells":{"ID":{"Number":50,"Unit":"mm"}}}]}]
```

**Block T2** — Standard circuit at 250 bar with 40 bar back pressure and a hydraulic motor 32 cm³/rev at 200 bar drop.
```json
[{"Symbol":"p","Number":250,"Unit":"bar"},{"Symbol":"p_back","Number":40,"Unit":"bar"},{"Symbol":"Q","Number":90,"Unit":"L/min"},{"Symbol":"D","Number":125,"Unit":"mm"},{"Symbol":"d_r","Number":90,"Unit":"mm"},{"Symbol":"stroke","Number":1200,"Unit":"mm"},{"Symbol":"K_m","Number":0.7},{"Symbol":"F_push","Number":200,"Unit":"kN"},{"Symbol":"F_pull","Number":100,"Unit":"kN"},{"Symbol":"V_gm","Number":32,"Unit":"cm^3/rev"},{"Symbol":"dp_m","Number":200,"Unit":"bar"}]
```

**Block L1** — Smallest values: p 1 bar, Q 0.1 L/min, D 10 mm, d_r 4 mm, stroke 1 mm, no loads, L_add 0.
```json
[{"Symbol":"p","Number":1,"Unit":"bar"},{"Symbol":"Q","Number":0.1,"Unit":"L/min"},{"Symbol":"D","Number":10,"Unit":"mm"},{"Symbol":"d_r","Number":4,"Unit":"mm"},{"Symbol":"stroke","Number":1,"Unit":"mm"},{"Symbol":"F_push","Number":0,"Unit":"kN"},{"Symbol":"F_pull","Number":0,"Unit":"kN"},{"Symbol":"L_add","Number":0,"Unit":"mm"}]
```

**Block L2** — Largest values: 700 bar, 2000 L/min, D 500 mm, rod 450 mm, stroke 5000 mm, K 2.5, pump 1000 cm³/rev at 2000 rpm, 200 mm lines.
```json
[{"Symbol":"p","Number":700,"Unit":"bar"},{"Symbol":"Q","Number":2000,"Unit":"L/min"},{"Symbol":"D","Number":500,"Unit":"mm"},{"Symbol":"d_r","Number":450,"Unit":"mm"},{"Symbol":"stroke","Number":5000,"Unit":"mm"},{"Symbol":"K_m","Number":2.5},{"Symbol":"L_add","Number":5000,"Unit":"mm"},{"Symbol":"F_push","Number":15000,"Unit":"kN"},{"Symbol":"F_pull","Number":100,"Unit":"kN"},{"Symbol":"V_g","Number":1000,"Unit":"cm^3/rev"},{"Symbol":"n_p","Number":2000,"Unit":"rpm"},{"Symbol":"d_suc","Number":200,"Unit":"mm"},{"Symbol":"d_prs","Number":200,"Unit":"mm"},{"Symbol":"d_ret","Number":200,"Unit":"mm"}]
```

**Block C1** — Rod 40 mm, bore 80 mm, stroke 910 mm, L_add 0, push 10 kN: slenderness just below the limit.
```json
[{"Symbol":"d_r","Number":40,"Unit":"mm"},{"Symbol":"D","Number":80,"Unit":"mm"},{"Symbol":"stroke","Number":910.0,"Unit":"mm"},{"Symbol":"L_add","Number":0,"Unit":"mm"},{"Symbol":"F_push","Number":10,"Unit":"kN"}]
```

**Block C2** — As Block C1 with stroke 911 mm: slenderness just above the limit.
```json
[{"Symbol":"d_r","Number":40,"Unit":"mm"},{"Symbol":"D","Number":80,"Unit":"mm"},{"Symbol":"stroke","Number":911.0,"Unit":"mm"},{"Symbol":"L_add","Number":0,"Unit":"mm"},{"Symbol":"F_push","Number":10,"Unit":"kN"}]
```

**Block D1** — Regenerative extend with 30 bar back pressure and the pump of step 2.
```json
[{"Symbol":"mode","Text":"Regenerative extend"},{"Symbol":"p_back","Number":30,"Unit":"bar"},{"Symbol":"V_g","Number":28,"Unit":"cm^3/rev"},{"Symbol":"n_p","Number":1450,"Unit":"rpm"}]
```

**Block D0** — As Block D1 with no back pressure.
```json
[{"Symbol":"mode","Text":"Regenerative extend"},{"Symbol":"p_back","Number":0,"Unit":"bar"},{"Symbol":"V_g","Number":28,"Unit":"cm^3/rev"},{"Symbol":"n_p","Number":1450,"Unit":"rpm"}]
```

**Block D2** — Standard circuit with an undersized pump: 20 cm³/rev at 1450 rpm, delivering 27.55 L/min against the 36 L/min circuit flow.
```json
[{"Symbol":"V_g","Number":20,"Unit":"cm^3/rev"},{"Symbol":"n_p","Number":1450,"Unit":"rpm"}]
```

## Notes for the tester
- This calculator is the first step of the hydraulic power unit pack, so no input here is linked. Its stale-upstream behaviour appears in chain.hydraulic-power-unit.md, where a change here makes every later step stale.
- Findings -02, -05, -06, -07 and -08 have no step. Finding -02 is a missing line-drop input. Finding -05 is a missing piston-speed check. Findings -06 to -08 are wording and screening limits. Note any surprise in the defect log.
