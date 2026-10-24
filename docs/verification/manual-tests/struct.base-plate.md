# Manual test — struct.base-plate (Column base plate, EN 1993-1-8)
Liability: High. Time: ~75 min. Pre-requisites: a project open in the desktop app. The JSON sets at the end of this file. A second user account for the independent check. A pocket calculator for the hand checks.

How to open: press Ctrl+K, type `Open worksheet: Column base plate`, press Enter. Or use Engineering → Modules → Calculations → Worksheets, then pick the calculator under Structural. The form opens on the first known-answer case, which equals the template defaults. This calculator has no tables, so every input is a form field. The results panel shows five significant figures. Press "Show working" to see each formula step. The unit picker offers only matching units. Import reads the clipboard: copy the JSON, then press "Import inputs (CSV/JSON)". The status line reads "Imported n input(s)". Every import resets each input it does not list to the template default. So each set below lists only the inputs that differ from the defaults. To return to the defaults, copy `[]` and import it. Start each step from the import it names. Two warnings appear in almost every run, one for the alpha basis and one for the missing PROFIS reference. They are expected.

| # | Step | Inputs (value + unit) | Expected output (symbol = value unit) | Tolerance | Expected PASS/FAIL/BLOCK/WARN | Basis | Result | Notes |
|---|---|---|---|---|---|---|---|---|
| 1 | Open the calculator. Leave every input at its default. | Defaults (the first known-answer case): I/H 260 x 256 x 17 x 10; plate 400 x 400 x 20 S275; C25/30; alpha 1.5; N_Ed 300 kN; M_Ed 100 kN*m; V_Ed 80 kN; n_t 2; n_b 4; x_b 165 mm; m 28 mm; l_eff 200 mm; prying off | f_jd = 14.167 MPa; c = 50.875 mm; A_eff = 98850 mm²; N_j_Rd = 1400.4 kN; u_N = 0.21423; z_C = 121.5 mm; z = 286.5 mm; C_Ed = 521.82 kN; T_Ed = 221.82 kN; F_t_Ed = 110.91 kN; C_Rd = 601.84 kN; u_C = 0.86704; M_pl_Rd = 5.5 kN·m; T_Rd_1 = 392.86 kN; T_Rd_3 = 282 kN; T_Rd = 282 kN; u_T = 0.78658; alpha_bc = 0.248; F_2_vb_Rd = 38.853 kN; F_f_Rd = 60 kN; V_Rd = 215.41 kN; u_V = 0.37138; V_b_Ed = 5 kN; t_req = 17.609 mm; u_max = 0.86704 | ±0.01 % | PASS (five checks); WARN (alpha basis, anchorage) | KAT ka-ih-moment-bolts and independent script |  | Two warnings are expected. One asks for the foundation geometry behind alpha. The other asks for a PROFIS report reference. Hand check: z_C = (260 - 17) / 2 = 121.5 mm. C_Ed = (100 + 300 x 0.165) / 0.2865 = 521.8 kN. The regime reads: tension at one bolt row, compression under the other flange. |
| 2 | Prying. Import Set P. | prying on; e_min 35 mm; all else default | T_Rd_1 = 785.71 kN; n_pry = 35 mm; T_Rd_2 = 331.27 kN; T_Rd_3 = 282 kN; T_Rd = 282 kN; u_T = 0.78658 | ±0.01 % | PASS (five checks); WARN (prying force Q) | KAT ka-ih-moment-prying and independent script |  | A warning says the prying force Q is not in F_t_Ed and PROFIS must add it. Mode 3 still governs, so T_Rd stays at 282 kN. Hand check: mode 2 = (2 x 5.5e6 + 35 x 282e3) / (28 + 35) = 331.27 kN. |
| 3 | Both flanges in compression. Import Set C. | N_Ed 600 kN; M_Ed 40 kN*m; V_Ed 0 | C_Ed = 464.61 kN; C_Ed_2 = 135.39 kN; T_Ed = 0 kN; F_t_Ed = 0 kN; u_C = 0.77199; regime = both sides in compression (Table 6.7, z = 2·z_C) | ±0.01 % | PASS | KAT ka-ih-both-compression and independent script |  | Hand check: e = 40 / 600 = 0.0667 m, below z_C. C = 600 / 2 + 40 / (2 x 0.1215) = 464.61 kN. The other flange takes 135.39 kN. |
| 4 | Both bolt rows in tension. Import Set T. | N_Ed -300 kN (tension); M_Ed 20 kN*m; V_Ed 0 | T_Ed = 210.61 kN; T_Ed_2 = 89.394 kN; F_t_Ed = 105.3 kN; C_Ed = 0 kN; F_f_Rd = 0 kN; u_T = 0.74683; regime = both bolt rows in tension (Table 6.7, z = 2·x_b) | ±0.01 % | PASS | KAT ka-ih-both-tension and independent script |  | No friction acts on a column in net tension, so F_f_Rd is 0. Hand check: 300 / 2 + 20 / (2 x 0.165) = 210.61 kN. The other row takes 89.39 kN. |
| 5 | Axial load only. Import Set X. | N_Ed 1200 kN; M_Ed 0; V_Ed 0 | f_cd = 14.167 MPa; f_jd = 14.167 MPa; c = 50.875 mm; A_eff = 98850 mm²; N_j_Rd = 1400.4 kN; u_N = 0.85691; t_req_N = 17.075 mm; t_req = 17.075 mm; F_t_Ed = 0 kN | ±0.01 % | PASS | KAT ka-ih-axial and independent script |  | Hand check: f_cd = 0.85 x 25 / 1.5 = 14.167 MPa. c = 20 x sqrt(275 / (3 x 14.167)) = 50.875 mm. |
| 6 | Hollow section. Import Set R. | column RHS; h 200; b 200; t 10; B 350; L 350; N_Ed 900 kN; M_Ed 0; V_Ed 0 | A_eff = 84930 mm²; N_j_Rd = 1203.2 kN; u_N = 0.74802; t_req = 14.465 mm | ±0.01 % | PASS | KAT ka-rhs-axial and independent script |  | Hand check: A_eff = (200 + 101.749)² - (200 - 20 - 101.749)² = 84930 mm². |
| 7 | Circular hollow section. Import Set H. | column CHS; D 220; t 10; B 350; L 350; N_Ed 800 kN; M_Ed 0; V_Ed 0 | A_eff = 73725 mm²; N_j_Rd = 1044.4 kN; u_N = 0.76596; t_req = 14.859 mm | ±0.01 % | PASS | KAT ka-chs-axial and independent script |  | Hand check: A_eff = pi x ((110 + 50.875)² - (110 - 10 - 50.875)²) = 73725 mm². |
| 8 | Strips overlap and the outstand reaches the plate edge. Import Set O. | I/H 150 x 150 x 10 x 6; plate B 300, L 400, t_p 40; N_Ed 1000 kN; M_Ed 0; V_Ed 0 | c = 101.75 mm; A_eff = 1.0605e+05 mm²; N_j_Rd = 1502.4 kN | ±0.01 % | PASS | KAT ka-ih-overlap-clip and independent script |  | Hand check: the outstand across the width is cut to (300 - 150) / 2 = 75 mm. A_eff = (150 + 150) x (150 + 203.5) = 106050 mm². The uncut rectangle would give 120000 mm². |
| 9 | Circular section with a moment. Import Set M. | column CHS; D 220; t 10; B 350; L 350; N_Ed 200 kN; M_Ed 60 kN*m; V_Ed 0; x_b 140 mm; m 25 mm | z_C = 73.155 mm; z = 213.15 mm; C_Ed = 412.85 kN; T_Ed = 212.85 kN; F_t_Ed = 106.42 kN; C_Rd = 522.22 kN; u_C = 0.79056 | ±0.01 % | PASS (five checks); WARN (alpha basis, anchorage) | KAT ka-chs-moment and independent script |  | z_C is the centroid of the half annulus. Read t_req in step 23, not here. Hand check: z_C = 4 x (160.87³ - 49.13³) / (3 x pi x (160.87² - 49.13²)) = 73.155 mm. |
| 10 | Typical case, I/H column on a larger plate. Import Set U. | B 450; L 450; t_p 30; f_ck 30; alpha 1.8; N_Ed 800 kN; M_Ed 150 kN*m (V_Ed 80 kN, other inputs default) | c = 63.593 mm; A_eff = 1.2406e+05 mm²; N_j_Rd = 2530.8 kN; u_N = 0.31611; C_Ed = 984.29 kN; T_Ed = 184.29 kN; F_t_Ed = 92.147 kN; C_Rd = 1127.1 kN; u_C = 0.87329; u_T = 0.65352; u_V = 0.25364; t_req = 26.786 mm; u_max = 0.87329 | ±0.01 % | PASS (five checks) | Independent script (calc()) |  | Hand check: f_cd = 0.85 x 30 / 1.5 = 17 MPa. f_jd = 2/3 x 1.8 x 17 = 20.4 MPa. c = 30 x sqrt(275 / (3 x 20.4)) = 63.59 mm. |
| 11 | Typical case, hollow section with a moment that overloads the flange. Import Set Q. | column RHS; h 250; b 150; t 8; N_Ed 250 kN; M_Ed 80 kN*m; V_Ed 80 kN; plate 400 x 400 x 20 | z_C = 121 mm; c = 50.875 mm; A_eff = 84288 mm²; C_Ed = 423.95 kN; T_Ed = 173.95 kN; F_t_Ed = 86.976 kN; C_Rd = 391.42 kN; u_C = 1.0831; u_T = 0.61685; t_req = 21.228 mm; u_max = 1.0831 | ±0.01 % | FAIL (compression flange bearing); other checks PASS | Independent script (calc()) |  | The bar reads FAIL with utilisation 1.0831. A thicker plate of 21.228 mm clears it. Hand check: z_C = (250 - 8) / 2 = 121 mm. |
| 12 | Boundary between regimes. Import Set L1. Then import Set L2. | L1: N_Ed 600 kN; M_Ed 72.9 kN*m. L2: N_Ed -300 kN; M_Ed 49.5 kN*m | L1: C_Ed = 600 kN; T_Ed = 0 kN; u_C = 0.99695; t_req = 19.947 mm. L2: T_Ed = 300 kN; F_t_Ed = 150 kN; u_T = 1.0638 (FAIL); t_req = bolts govern. | ±0.01 % | L1: PASS. L2: FAIL (tension T-stub, bolt tension) | Hand formula: M = N x z_C = 600 x 0.1215 = 72.9 kN*m; M = \|N\| x x_b = 300 x 0.165 = 49.5 kN*m |  | The forces join smoothly across both limits. At L1 the bolt tension is just zero. At L2 the compression is just zero, and 300 kN exceeds the two-bolt resistance of 282 kN. |
| 13 | Net tension. Import Set N. | N_Ed -1000 kN; M_Ed 0; n_t 3; n_b 6 | T_Ed = 500 kN; F_t_Ed = 166.67 kN; T_Rd = 392.86 kN; u_T = 1.2727; t_req = bolts govern; regime = both bolt rows in tension | ±0.01 % | FAIL (tension T-stub) | Hand formula: 1000 / 2 = 500 kN a row; 500 / 3 = 166.67 kN a bolt |  | Mode 1-2 gives 392.86 kN. The bolts give 3 x 141 = 423 kN. So the plate governs. |
| 14 | Pure moment. Import Set Z. | N_Ed 0; M_Ed 100 kN*m; V_Ed 80 kN | C_Ed = 349.04 kN; T_Ed = 349.04 kN; F_t_Ed = 174.52 kN; u_C = 0.57996; u_T = 1.2377; t_req = bolts govern | ±0.01 % | FAIL (tension T-stub, bolt tension) | Hand formula: M / z = 100 / 0.2865 = 349.04 kN |  | With N = 0 the two forces are equal. Friction is zero, so the shear check relies on the four anchors alone. |
| 15 | Boundary, largest values. Import Set G1. Then import Set G2. | G1: t_p 100 mm; B = L = 1500 mm; N_Ed 3000 kN; M_Ed 400 kN*m. G2: N_Ed 10000 kN; M_Ed 0 | G1: c = 254.37 mm; A_eff = 5.879e+05 mm²; N_j_Rd = 8328.5 kN; u_N = 0.36021; C_Ed = 3123.9 kN; T_Ed = 123.91 kN; u_C = 0.75017; t_req = 79.823 mm. G2: u_N = 7.1409 (FAIL); t_req = > 100 mm. | ±0.01 % | G1: PASS. G2: FAIL (concrete bearing) | Independent script (calc()) |  | All inputs are accepted at their upper limits. At G2 no plate thickness helps, so t_req reads > 100 mm. |
| 16 | Boundary, smallest values. Import Set B. | RHS 50 x 50 x 3; plate 120 x 120 x 10; f_y 235 MPa; f_ck 20 MPa; alpha 1; x_b 50 mm; m 20 mm; l_eff 50 mm; n_t 1; n_b 2; N_Ed 50 kN | z_C = 23.5 mm; c = 32.199 mm; A_eff = 13087 mm²; N_j_Rd = 98.878 kN; u_N = 0.50567; t_req = 10 mm | ±0.01 % | PASS (WARN: anchorage) | Independent script (calc()) |  | Every one of these inputs sits at its lower limit. The plate is 120 mm because x_b must stay inside the plate edge. t_req reads the 10 mm floor. |
| 17 | NEGATIVE: value outside the limit. Import `[{"Symbol":"t_p","Number":9,"Unit":"mm"}]`. Then import the same with M_Ed -5 kN*m and with N_Ed 10001 kN. | t_p 9 mm; then M_Ed -5 kN*m; then N_Ed 10001 kN | t_p ... must be at least 10 mm; 9 mm was given. M_Ed ... must be at least 0 kN*m; -5 kN*m was given. N_Ed ... must be at most 10000 kN; 10001 kN was given. No results in each case. | n/a | BLOCK | Template limits (hard minimum 10 mm and 0; hard maximum 10000 kN) |  | Use three separate one-line imports. The field shows the problem inline and the bar reads Blocked. Reset each field afterwards. |
| 18 | NEGATIVE: geometry that cannot work. Import `[{"Symbol":"x_b","Number":130,"Unit":"mm"}]`. Then import `[{"Symbol":"n_b","Number":3}]`. Then `[{"Symbol":"B","Number":250,"Unit":"mm"}]`. | x_b 130 mm (equals h/2); then n_b 3 (below 2 x n_t); then B 250 mm (below b) | x_b: the bolt row (130 mm from the axis) must lie outside the column face (130 mm) and inside the plate edge (200 mm). Then: n_b: the total (3) must be at least twice the bolts in each row (2). Then: the column (260 x 256 mm) must stand inside the plate (L = 400, B = 250 mm). | Text match | BLOCK | Template rules "x_b", "bolts" and "plate"; hand check |  | Equal values block, so the rule is strict. The third message names h, although b is the width that fails (see the defect step for P1-struct.base-plate-04). |
| 19 | NEGATIVE: wrong unit by import. Copy `[{"Symbol":"N_Ed","Number":300,"Unit":"m"}]`, press "Import inputs (CSV/JSON)", and read N_Ed and its unit. Repeat with the unit `furlong`. | N_Ed = 300 m, then N_Ed = 300 furlong | Correct: a refusal. For example: N_Ed must be entered in a unit of kN, not m. Then: N_Ed: Unknown unit 'furlong'. | n/a | BLOCK | Hand check (a length is not a force); probe confirms the block message |  | Suspected defect, not yet in a P1 report. The form keeps the number and falls back to the default unit if it does not know the unit. N_Ed may then show 300 kN with no message. Record FAIL in that case. |
| 20 | NEGATIVE: independent check. Press "Save as calculation", name it BP check, then press "Record check" as the same user. | Defaults; same signed-in user | Refusal: the calculation needs an independent check; sign in as someone other than its author. | n/a | BLOCK | Code rule (this calculator demands an independent check) |  | A second user passes this rule. That user must give every input a source other than manual. Otherwise the check is refused for a missing source. |
| 21 | NEGATIVE: stale upstream. Open "Open worksheet: Bolts and fillet welds", leave the defaults (M20 class 8.8) and save it as "BP upstream". Reset this calculator with `[]`, save it as "BP downstream". In the Link panel link F_t_Rd to "BP upstream" output F_t_Rd. Then try to reopen the upstream: look in the worksheet catalogue, the Calculation register and the Tasks list. | Upstream M20 class 8.8; once reopening works: bolt size M24 | After the link: F_t_Rd = 141.00 kN; T_Rd = 282.00 kN; u_T = 0.78657. As built, no screen reopens a saved worksheet calculation (known gap P4-01), so no stale marker can appear. Once reopening works, change the upstream to M24 and save. Reopen the downstream. Expect a Stale banner naming F_t_Rd. "Record check" refuses: the calculation is stale. After "Update linked inputs": F_t_Rd = 203.04 kN; T_Rd = 392.86 kN (mode 1-2 now governs); u_T = 0.56462. | ±0.01 % on values | BLOCK (cannot reopen); once fixed: BLOCK (check refused), then PASS after the update | Independent script (calc() with F_t_Rd); code rule (staleness) |  | Known gap P4-01. The link carries the unrounded 141.0016 kN, so the figures differ slightly from the default 141 kN. Other inputs from the bolt sheet (A_s, F_b_Rd) can be linked the same way. |
| 22 | Known defect P1-struct.base-plate-01 (High). Required thickness of a CHS base. Import Set D1 and read t_req. Then import Set D2 and read u_C and u_T. Then import Set D3 and read t_req. | CHS 219.1 x 8; plate 500 x 500; N_Ed 100 kN; M_Ed 150 kN*m; V_Ed 0; x_b 200 mm; m 25 mm; l_eff 400 mm; F_t_Rd 250 kN. t_p 60 mm, then 21.67 mm, then 25 mm | Product at 60 mm: t_req = 21.666 mm. At t_p = 21.67 mm the sheet fails its own number: u_C = 1.1161 and u_T = 1.0399. At 25 mm it says bolts govern. Correct: t_req = 33.649 mm in all three cases. At 33.65 mm the utilisation is 1.000 or less. | ±0.01 % | FAIL | Independent script (struct.base-plate.py calc(), z_C re-evaluated at each trial thickness) |  | Known defect P1-struct.base-plate-01; expect FAIL until fixed. The centroid z_C depends on t_p, so the forces move as the plate thickens. I/H, RHS and an entered z_C are not affected. Step 9 shows the same effect on the KAT value: t_req 15.400 mm in the product, 15.587 mm when consistent. |
| 23 | Known defect P1-struct.base-plate-01 (High), second case. Import Set M (step 9). Read t_req. Then type t_p = t_req and read u_C. | Set M; then t_p 15.4 mm | Product: t_req = 15.4 mm. At t_p = 15.4 mm the compression check reads u_C = 1.011 and FAILS. Correct: t_req = 15.587 mm. | ±0.01 % | FAIL | Independent script (struct.base-plate.py calc()) |  | Known defect P1-struct.base-plate-01; expect FAIL until fixed. The built-in KAT pins the product value 15.39956 mm. The consistent value is 15.58735 mm. Fixing this changes the KAT. |
| 24 | Known defect P1-struct.base-plate-02 (Medium). No upper guard on z_C_in. Import Set Y1. Then import Set Y2. | N_Ed -50 kN; M_Ed 100 kN*m; z_C_in blank, then 1000 mm (plate half-length is 200 mm) | Y1 (blank): T_Ed = 370.24 kN; u_T = 1.3129 (FAIL). Y2 (1000 mm): T_Ed = 128.76 kN; u_T = 0.45658 (pass). Correct: Y2 is blocked, because the compression resultant lies outside the plate. Or a warning appears. | ±0.01 % | FAIL | Independent script (calc() with z_C_in); P1 report |  | Known defect P1-struct.base-plate-02; expect FAIL until fixed. The error is unconservative. A user who enters the full lever arm z (286.5 mm) instead of z_C (121.5 mm) triggers the same effect. |
| 25 | Known defect P1-struct.base-plate-03 (Medium, upgraded). Anchor strength range. Import Set W. | f_ub 1000 MPa; f_yb 640 MPa (the largest value accepted); V_Ed 250 kN | Product: alpha_bc = 0.248; F_2_vb_Rd = 48.566 kN; V_Rd = 254.27 kN; u_V = 0.98322 (pass); no warning. A true f_yb of 900 MPa gives alpha_bc 0.17; F_2_vb_Rd = 33.291 kN; V_Rd = 193.17 kN; u_V = 1.2942 (FAIL). Correct: a block above f_ub 800 MPa, or a warning that the anchor shear rule does not cover it. | ±0.01 % | FAIL | Hand formula: alpha_bc = 0.44 - 0.0003 x 900 = 0.17; F_2 = 0.17 x 1000 x 244.79 / 1.25 = 33.29 kN; V_Rd = 0.2 x 300 + 4 x 33.29 = 193.17 kN |  | Known defect P1-struct.base-plate-03; expect FAIL until fixed. The error is unconservative, because a strong anchor cannot be entered truthfully. Check the validity range of the rule in your own copy. |
| 26 | Known defect P1-struct.base-plate-04 (Low). Wrong symbol in the plate block. Import `[{"Symbol":"B","Number":250,"Unit":"mm"}]`. | B 250 mm; h 260 mm; b 256 mm; L 400 mm | Product: h: the column (260 x 256 mm) must stand inside the plate (L = 400, B = 250 mm). Correct: the message names b. The width b (256 mm) exceeds B (250 mm). The depth h (260 mm) fits L (400 mm). | Text match | FAIL | Hand check (b > B; h < L); P1 report |  | Known defect P1-struct.base-plate-04; expect FAIL until fixed. The block itself is right. Only the named symbol misleads. |
| 27 | Known defect P1-struct.base-plate-05 (Info). Friction uses N_Ed. Read F_f_Rd in step 1. | Defaults | Product: F_f_Rd = 60 kN (0.2 x 300 kN). The contact force under the moment is C_Ed = 521.82 kN, which would give 104.36 kN. The product is conservative. Correct: the sheet notes which force it uses. | ±0.01 % | WARN | Hand formula: 0.2 x 521.82 = 104.36 kN |  | Known defect P1-struct.base-plate-05; expect FAIL until fixed. Info only. No action if you accept the present wording. |
| 28 | Known defect P1-struct.base-plate-R1 (Medium, new). One yield strength for every thickness. Import Set Y. Read M_pl_Rd and T_Rd_1. | t_p 50 mm; l_eff 40 mm; f_y 275 MPa (default) | Product: M_pl_Rd = 6.875 kN·m; T_Rd_1 = 491.07 kN. With f_y 255 MPa, the value the review recalls for a plate over 40 mm: M_pl_Rd = 6.375 kN·m; T_Rd_1 = 455.36 kN (7.8 % lower). Correct: the sheet warns when t_p passes a thickness band of the entered f_y. Or it takes f_y from the record by thickness. | ±0.01 % | WARN | Hand formula: 0.25 x 40 x 50² x 275 / 10⁶ = 6.875 kN*m; with 255 MPa: 6.375 kN*m |  | Known defect P1-struct.base-plate-R1; expect FAIL until fixed. The review recalls 265 MPa above 16 mm and 255 MPa above 40 mm for S275 in the product standard. Confirm the values in your own copy. The default 275 MPa at 20 mm is 3.8 % high on that basis. Another route in the design standard allows 275 MPa up to 40 mm. |

## JSON sets

Copy the whole line of a set. Press "Import inputs (CSV/JSON)".

Set P: Prying on, edge distance 35 mm.

```json
[{"Symbol":"prying","Flag":true},{"Symbol":"e_min","Number":35,"Unit":"mm"}]
```

Set C: Both flanges in compression.

```json
[{"Symbol":"N_Ed","Number":600,"Unit":"kN"},{"Symbol":"M_Ed","Number":40,"Unit":"kN*m"},{"Symbol":"V_Ed","Number":0,"Unit":"kN"}]
```

Set T: Both bolt rows in tension.

```json
[{"Symbol":"N_Ed","Number":-300,"Unit":"kN"},{"Symbol":"M_Ed","Number":20,"Unit":"kN*m"},{"Symbol":"V_Ed","Number":0,"Unit":"kN"}]
```

Set X: Axial load only.

```json
[{"Symbol":"N_Ed","Number":1200,"Unit":"kN"},{"Symbol":"M_Ed","Number":0,"Unit":"kN*m"},{"Symbol":"V_Ed","Number":0,"Unit":"kN"}]
```

Set R: RHS 200 x 200 x 10 on a 350 x 350 plate.

```json
[{"Symbol":"column","Text":"RHS"},{"Symbol":"h","Number":200,"Unit":"mm"},{"Symbol":"b","Number":200,"Unit":"mm"},{"Symbol":"t","Number":10,"Unit":"mm"},{"Symbol":"B","Number":350,"Unit":"mm"},{"Symbol":"L","Number":350,"Unit":"mm"},{"Symbol":"N_Ed","Number":900,"Unit":"kN"},{"Symbol":"M_Ed","Number":0,"Unit":"kN*m"},{"Symbol":"V_Ed","Number":0,"Unit":"kN"}]
```

Set H: CHS 220 x 10 on a 350 x 350 plate.

```json
[{"Symbol":"column","Text":"CHS"},{"Symbol":"D","Number":220,"Unit":"mm"},{"Symbol":"t","Number":10,"Unit":"mm"},{"Symbol":"B","Number":350,"Unit":"mm"},{"Symbol":"L","Number":350,"Unit":"mm"},{"Symbol":"N_Ed","Number":800,"Unit":"kN"},{"Symbol":"M_Ed","Number":0,"Unit":"kN*m"},{"Symbol":"V_Ed","Number":0,"Unit":"kN"}]
```

Set O: Small I-section on a 300 x 400 x 40 plate.

```json
[{"Symbol":"h","Number":150,"Unit":"mm"},{"Symbol":"b","Number":150,"Unit":"mm"},{"Symbol":"t_f","Number":10,"Unit":"mm"},{"Symbol":"t_w","Number":6,"Unit":"mm"},{"Symbol":"B","Number":300,"Unit":"mm"},{"Symbol":"L","Number":400,"Unit":"mm"},{"Symbol":"t_p","Number":40,"Unit":"mm"},{"Symbol":"N_Ed","Number":1000,"Unit":"kN"},{"Symbol":"M_Ed","Number":0,"Unit":"kN*m"},{"Symbol":"V_Ed","Number":0,"Unit":"kN"}]
```

Set M: CHS 220 x 10 with a moment.

```json
[{"Symbol":"column","Text":"CHS"},{"Symbol":"D","Number":220,"Unit":"mm"},{"Symbol":"t","Number":10,"Unit":"mm"},{"Symbol":"B","Number":350,"Unit":"mm"},{"Symbol":"L","Number":350,"Unit":"mm"},{"Symbol":"N_Ed","Number":200,"Unit":"kN"},{"Symbol":"M_Ed","Number":60,"Unit":"kN*m"},{"Symbol":"V_Ed","Number":0,"Unit":"kN"},{"Symbol":"x_b","Number":140,"Unit":"mm"},{"Symbol":"m","Number":25,"Unit":"mm"}]
```

Set U: Typical column base: 450 x 450 x 30 plate, C30/37, alpha 1.8.

```json
[{"Symbol":"B","Number":450,"Unit":"mm"},{"Symbol":"L","Number":450,"Unit":"mm"},{"Symbol":"t_p","Number":30,"Unit":"mm"},{"Symbol":"f_ck","Number":30,"Unit":"MPa"},{"Symbol":"alpha","Number":1.8},{"Symbol":"N_Ed","Number":800,"Unit":"kN"},{"Symbol":"M_Ed","Number":150,"Unit":"kN*m"}]
```

Set Q: RHS 250 x 150 x 8 with a moment.

```json
[{"Symbol":"column","Text":"RHS"},{"Symbol":"h","Number":250,"Unit":"mm"},{"Symbol":"b","Number":150,"Unit":"mm"},{"Symbol":"t","Number":8,"Unit":"mm"},{"Symbol":"N_Ed","Number":250,"Unit":"kN"},{"Symbol":"M_Ed","Number":80,"Unit":"kN*m"}]
```

Set L1: Limit between regimes 1 and 2.

```json
[{"Symbol":"N_Ed","Number":600,"Unit":"kN"},{"Symbol":"M_Ed","Number":72.9,"Unit":"kN*m"}]
```

Set L2: Limit between regimes 1 and 3.

```json
[{"Symbol":"N_Ed","Number":-300,"Unit":"kN"},{"Symbol":"M_Ed","Number":49.5,"Unit":"kN*m"}]
```

Set N: Net tension, three bolts a row.

```json
[{"Symbol":"N_Ed","Number":-1000,"Unit":"kN"},{"Symbol":"M_Ed","Number":0,"Unit":"kN*m"},{"Symbol":"n_t","Number":3},{"Symbol":"n_b","Number":6}]
```

Set Z: Zero axial force.

```json
[{"Symbol":"N_Ed","Number":0,"Unit":"kN"},{"Symbol":"M_Ed","Number":100,"Unit":"kN*m"}]
```

Set G1: Largest plate and thickness.

```json
[{"Symbol":"t_p","Number":100,"Unit":"mm"},{"Symbol":"B","Number":1500,"Unit":"mm"},{"Symbol":"L","Number":1500,"Unit":"mm"},{"Symbol":"N_Ed","Number":3000,"Unit":"kN"},{"Symbol":"M_Ed","Number":400,"Unit":"kN*m"}]
```

Set G2: Largest axial force.

```json
[{"Symbol":"N_Ed","Number":10000,"Unit":"kN"},{"Symbol":"M_Ed","Number":0,"Unit":"kN*m"}]
```

Set B: Smallest section, thinnest plate, weakest materials.

```json
[{"Symbol":"column","Text":"RHS"},{"Symbol":"h","Number":50,"Unit":"mm"},{"Symbol":"b","Number":50,"Unit":"mm"},{"Symbol":"t","Number":3,"Unit":"mm"},{"Symbol":"B","Number":120,"Unit":"mm"},{"Symbol":"L","Number":120,"Unit":"mm"},{"Symbol":"t_p","Number":10,"Unit":"mm"},{"Symbol":"f_y","Number":235,"Unit":"MPa"},{"Symbol":"f_ck","Number":20,"Unit":"MPa"},{"Symbol":"alpha","Number":1},{"Symbol":"N_Ed","Number":50,"Unit":"kN"},{"Symbol":"M_Ed","Number":0,"Unit":"kN*m"},{"Symbol":"V_Ed","Number":0,"Unit":"kN"},{"Symbol":"x_b","Number":50,"Unit":"mm"},{"Symbol":"m","Number":20,"Unit":"mm"},{"Symbol":"l_eff","Number":50,"Unit":"mm"},{"Symbol":"n_t","Number":1},{"Symbol":"n_b","Number":2}]
```

Set D1: CHS 219.1 x 8 on a 500 x 500 plate, t_p 60 mm.

```json
[{"Symbol":"column","Text":"CHS"},{"Symbol":"D","Number":219.1,"Unit":"mm"},{"Symbol":"t","Number":8,"Unit":"mm"},{"Symbol":"B","Number":500,"Unit":"mm"},{"Symbol":"L","Number":500,"Unit":"mm"},{"Symbol":"N_Ed","Number":100,"Unit":"kN"},{"Symbol":"M_Ed","Number":150,"Unit":"kN*m"},{"Symbol":"V_Ed","Number":0,"Unit":"kN"},{"Symbol":"x_b","Number":200,"Unit":"mm"},{"Symbol":"m","Number":25,"Unit":"mm"},{"Symbol":"l_eff","Number":400,"Unit":"mm"},{"Symbol":"F_t_Rd","Number":250,"Unit":"kN"},{"Symbol":"t_p","Number":60,"Unit":"mm"}]
```

Set D2: As D1 with t_p 21.67 mm.

```json
[{"Symbol":"column","Text":"CHS"},{"Symbol":"D","Number":219.1,"Unit":"mm"},{"Symbol":"t","Number":8,"Unit":"mm"},{"Symbol":"B","Number":500,"Unit":"mm"},{"Symbol":"L","Number":500,"Unit":"mm"},{"Symbol":"N_Ed","Number":100,"Unit":"kN"},{"Symbol":"M_Ed","Number":150,"Unit":"kN*m"},{"Symbol":"V_Ed","Number":0,"Unit":"kN"},{"Symbol":"x_b","Number":200,"Unit":"mm"},{"Symbol":"m","Number":25,"Unit":"mm"},{"Symbol":"l_eff","Number":400,"Unit":"mm"},{"Symbol":"F_t_Rd","Number":250,"Unit":"kN"},{"Symbol":"t_p","Number":21.67,"Unit":"mm"}]
```

Set D3: As D1 with t_p 25 mm.

```json
[{"Symbol":"column","Text":"CHS"},{"Symbol":"D","Number":219.1,"Unit":"mm"},{"Symbol":"t","Number":8,"Unit":"mm"},{"Symbol":"B","Number":500,"Unit":"mm"},{"Symbol":"L","Number":500,"Unit":"mm"},{"Symbol":"N_Ed","Number":100,"Unit":"kN"},{"Symbol":"M_Ed","Number":150,"Unit":"kN*m"},{"Symbol":"V_Ed","Number":0,"Unit":"kN"},{"Symbol":"x_b","Number":200,"Unit":"mm"},{"Symbol":"m","Number":25,"Unit":"mm"},{"Symbol":"l_eff","Number":400,"Unit":"mm"},{"Symbol":"F_t_Rd","Number":250,"Unit":"kN"},{"Symbol":"t_p","Number":25,"Unit":"mm"}]
```

Set Y1: Net tension with a moment, z_C from the section.

```json
[{"Symbol":"N_Ed","Number":-50,"Unit":"kN"},{"Symbol":"M_Ed","Number":100,"Unit":"kN*m"}]
```

Set Y2: As Y1 with z_C_in 1000 mm.

```json
[{"Symbol":"N_Ed","Number":-50,"Unit":"kN"},{"Symbol":"M_Ed","Number":100,"Unit":"kN*m"},{"Symbol":"z_C_in","Number":1000,"Unit":"mm"}]
```

Set W: Anchor with f_ub 1000 MPa and f_yb 640 MPa, shear 250 kN.

```json
[{"Symbol":"f_ub","Number":1000,"Unit":"MPa"},{"Symbol":"f_yb","Number":640,"Unit":"MPa"},{"Symbol":"V_Ed","Number":250,"Unit":"kN"}]
```

Set K: Plate narrower than the column width.

```json
[{"Symbol":"B","Number":250,"Unit":"mm"}]
```

Set Y: Thick plate with a short effective length.

```json
[{"Symbol":"t_p","Number":50,"Unit":"mm"},{"Symbol":"l_eff","Number":40,"Unit":"mm"}]
```

Not tested here: P1 findings -06 and -07. Finding -06 is platform behaviour for a missing input, and the form always shows every input. Finding -07 is wording in the spec only.
