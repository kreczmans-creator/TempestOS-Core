# Manual test — struct.load-combinations (Load combinations (EN 1990))
Liability: Medium. Time: ~25 min. Pre-requisites: None. Have your National Annex to EN 1990 (Table NA.A1.1 and NA.A1.2(B)) for steps 15 and 16.

| # | Step | Inputs (value + unit) | Expected output (symbol = value unit) | Tolerance | Expected PASS/FAIL/BLOCK/WARN | Basis | Result | Notes |
|---|---|---|---|---|---|---|---|---|
| 1 | Press Ctrl+K. Type "Open worksheet: Load combinations (EN 1990)". Press Enter. Or use Engineering tree, Modules, Calculations, Worksheets, then Structural. | None. The form opens on unit, method, gamma_G,sup 1.35, gamma_G,inf 1.0, gamma_Q 1.5, xi 0.85, K_FI 1. | Header shows struct.load-combinations@1.0.0. Bar reads: Blocked — actions needs a value (at least one action). | Text match | BLOCK | product behaviour (no number) |  | The actions table has no form field. Import blocks supply it. The bar reads No pass/fail check when results show. This sheet has no limit check. |
| 2 | Import Block K1. | Block K1: Dead 10. Office 5. method 6.10. Unit kN/m | E_d_max = 21 kN/m. E_d_min = 10 kN/m. E_k_max = 15 kN/m. E_fr_max = 12.5 kN/m. E_qp_max = 11.5 kN/m. | KAT: 1e-9 | PASS | KAT ka-610-single; independent script |  | Hand check: 1.35 x 10 + 1.5 x 5 = 21 kN/m. Characteristic 10 + 5 = 15. Frequent 10 + 0.5 x 5 = 12.5. Quasi-permanent 10 + 0.3 x 5 = 11.5. |
| 3 | Import Block K2. | Block K2: method = 6.10a and 6.10b. xi = 0.85 | E_d_max = 18.975 kN/m. The governing text names 6.10b. | KAT: 1e-9 | PASS | KAT ka-610ab-single; independent script |  | Hand check: 6.10b = 0.85 x 1.35 x 10 + 1.5 x 5 = 18.975. 6.10a = 1.35 x 10 + 1.5 x 0.7 x 5 = 18.75. |
| 4 | Import Block K3. | Block K3: add Wind 4 kN/m | E_d_max = 24.75 kN/m. E_k_max = 17.5 kN/m. E_fr_max = 12.5 kN/m. E_qp_max = 11.5 kN/m. | KAT: 1e-9 | PASS | KAT ka-610-two-variables; independent script |  | Each variable is tried as leading. Office leading: 13.5 + 7.5 + 1.5 x 0.6 x 4 = 24.6. Wind leading gives 13.5 + 6 + 1.5 x 0.7 x 5 = 24.75. |
| 5 | Import Block K4. | Block K4: Roof 10. Uplift -20 | E_d_max = 13.5 kN/m. E_d_min = -20 kN/m. | KAT: 1e-9 | PASS | KAT ka-uplift; independent script |  | Minimum: 1.0 x 10 - 1.5 x 20 = -20. The favourable dead load takes gamma_G,inf. |
| 6 | Import Block K5. | Block K5: Perm 20. Dom 8. Snow 3. 6.10a and 6.10b | E_d_max = 37.65 kN/m (6.10a gives 37.65. 6.10b gives 37.2). | KAT: 1e-9 | PASS | KAT ka-designers-guide (Unconfirmed); independent script |  | The source book example is not confirmed (kat-sources). Hand check: 6.10a = 27 + 1.5 x (0.7 x 8 + 0.5 x 3) = 37.65. 6.10b = 0.85 x 27 + 1.5 x 8 + 1.5 x 0.5 x 3 = 37.2. |
| 7 | Import Block U. | Block U: xi = 0.925 | E_d_max = 37.478 kN/m. E_d_min = 18 kN/m. E_k_max = 28 kN/m. E_fr_max = 23 kN/m. E_qp_max = 21 kN/m. | Shown digits | PASS | independent script (tools/verification/struct.load-combinations.py) |  | Typical case. The worked example office-floor-beam-uk-na gives the same figures. |
| 8 | Import Block M. | Block M: five actions | E_d_max = 25.5 kN/m. E_d_min = -4.05 kN/m. E_k_max = 17.2 kN/m. E_k_min = 1 kN/m. E_fr_max = 12 kN/m. E_qp_max = 10.8 kN/m. | Shown digits | PASS | independent script (tools/verification/struct.load-combinations.py) |  | The opposing permanent takes gamma_G,inf in the maximum and gamma_G,sup in the minimum. |
| 9 | Import Block X. | Block X: Impact (accidental) 30 kN/m | E_acc_max = 42.5 kN/m. E_acc_min = 10 kN/m. E_d_max = 24.75 kN/m (the accidental row is left out of the ULS). | Shown digits | PASS | independent script (tools/verification/struct.load-combinations.py) |  | Accidental: 10 + 30 + 0.5 x 5 (psi_1 on the leader) = 42.5. |
| 10 | Import Block F. | Block F: gamma_G,sup = 1.1. gamma_G,inf = 0.9. K_FI = 1.1 | E_d_max = 15.95 kN/m. E_d_min = -24 kN/m. E_k_max = 13 kN/m. E_k_min = -10 kN/m. E_fr_max = 10 kN/m. E_qp_max = 10 kN/m. | Shown digits | PASS | independent script (tools/verification/struct.load-combinations.py) |  | Changed factors and K_FI. Boundary: K_FI at its upper limit 1.1. |
| 11 | Import Block P. | Block P: user psi table for wind and office | E_d_max = 31.8 kN/m. E_k_max = 22.2 kN/m. E_fr_max = 13.4 kN/m. E_qp_max = 11.8 kN/m. | Shown digits | PASS | independent script (tools/verification/struct.load-combinations.py) |  | The user table replaces the recommended set. |
| 12 | Import Block K1. Change the unit choice to kN*m, then to kN/m^2. | unit = kN*m. unit = kN/m^2 | Every result carries the chosen unit, with the same numbers as step 2. | Shown digits | PASS | hand formula: unit is a label only |  | Each choice of unit. |
| 13 | Import Block E (no actions). Then Block 31 (31 actions). Then Block H (psi cell blank). Then set gamma_G,sup = 3.1 and K_FI = 1.2 on Block K1. | Block E. Block 31. Block H. gamma_G,sup = 3.1. K_FI = 1.2 | Each case blocks. No actions: enter at least one action. 31 rows: more than 30 actions. Blank psi cell: refused. gamma_G,sup: must be at most 3. K_FI: must be at most 1.1. | Text match | BLOCK | product behaviour (no number) |  | Negative test: out of range. The 31-row and blank-cell wordings may differ. Each must block. |
| 14 | Import Block U2. Look at the unit choice list. | Block U2: unit = mm | The import is refused, or the unit flags an error. The unit must not silently become kN/m. | Text match | BLOCK | product behaviour (no number) |  | Negative test: unit mismatch. Probe result: the engine blocks with unit must be one of the listed choices. Check the form does the same after an import. Suspected import-path gap (CalcSheetView.BuildField) if the unit shows kN/m with no message. |
| 15 | Import Block U. Set xi to 0.85, then 0.925. Look for a note on the factor set. | Block U with xi = 0.85 and then 0.925 | xi = 0.925 (UK NA): E_d_max = 37.478 kN/m. xi = 0.85 (EN recommended): 35.655 kN/m, 4.9 % lower. The sheet names the factor set (UK NA or EN recommended) beside the factors. Today it does not. | Shown digits | FAIL | independent script (tools/verification/struct.load-combinations.py) |  | Known defect P1-struct.load-combinations-01 (Low, rated Medium in review); expect FAIL until fixed. Check xi in your National Annex. |
| 16 | Import Block R. Compare psi_0 for roofs and wind with your National Annex Table NA.A1.1. | Block R: G 10. Wind 6. Roof imposed 4 | With the recommended set E_d_max = 24.9 kN. With roofs psi_0 = 0.7 and wind psi_0 = 0.5 (reviewer's recollection of the UK NA) it is 26.7 kN. The sheet states which set it uses. | Shown digits | WARN | independent script (tools/verification/struct.load-combinations.py); UK values unconfirmed |  | Suspected defect P1-struct.load-combinations-02 (Low). Write the psi_0 values in your NA in Notes. |
| 17 | Import Block K1. Set K_FI = 1.1. Compare with your reading of EN 1990 Annex B and the NA. | K_FI = 1.1 | Product: E_d_max = 21.75 kN/m (K_FI multiplies gamma_Q only). If K_FI also multiplies gamma_G for unfavourable permanent actions, E_d_max = 23.1 kN/m. | Shown digits | WARN | hand formula: 1.35 x 10 + 1.5 x 1.1 x 5, or 1.1 x (1.35 x 10 + 1.5 x 5) |  | Open question P1-struct.load-combinations-03 (Info). Record which reading your NA supports. |

## Import blocks
Copy the whole line of a block to the clipboard. Then press *Import inputs (CSV/JSON)*. The status line must read "Imported n input(s)".

**Block K1** — Dead 10 kN/m and Office imposed 5 kN/m. Unit kN/m. Method 6.10. Factors at their defaults.
```json
[{"Symbol":"actions","Rows":[{"Cells":{"name":{"Text":"Dead"},"category":{"Text":"G permanent"},"F_k":{"Number":10}}},{"Cells":{"name":{"Text":"Office"},"category":{"Text":"B office"},"F_k":{"Number":5}}}]},{"Symbol":"unit","Text":"kN/m"},{"Symbol":"method","Text":"6.10"}]
```

**Block K2** — Block K1 with method 6.10a and 6.10b and xi at the default 0.85.
```json
[{"Symbol":"actions","Rows":[{"Cells":{"name":{"Text":"Dead"},"category":{"Text":"G permanent"},"F_k":{"Number":10}}},{"Cells":{"name":{"Text":"Office"},"category":{"Text":"B office"},"F_k":{"Number":5}}}]},{"Symbol":"unit","Text":"kN/m"},{"Symbol":"method","Text":"6.10a and 6.10b"}]
```

**Block K3** — Dead 10, Office 5 and Wind 4 kN/m. Method 6.10.
```json
[{"Symbol":"actions","Rows":[{"Cells":{"name":{"Text":"Dead"},"category":{"Text":"G permanent"},"F_k":{"Number":10}}},{"Cells":{"name":{"Text":"Office"},"category":{"Text":"B office"},"F_k":{"Number":5}}},{"Cells":{"name":{"Text":"Wind"},"category":{"Text":"wind"},"F_k":{"Number":4}}}]},{"Symbol":"unit","Text":"kN/m"},{"Symbol":"method","Text":"6.10"}]
```

**Block K4** — Roof dead 10 kN/m and wind uplift -20 kN/m.
```json
[{"Symbol":"actions","Rows":[{"Cells":{"name":{"Text":"Roof"},"category":{"Text":"G permanent"},"F_k":{"Number":10}}},{"Cells":{"name":{"Text":"Uplift"},"category":{"Text":"wind"},"F_k":{"Number":-20}}}]},{"Symbol":"unit","Text":"kN/m"},{"Symbol":"method","Text":"6.10"}]
```

**Block K5** — Permanent 20, Domestic imposed 8, Snow 3 kN/m. Method 6.10a and 6.10b, xi 0.85.
```json
[{"Symbol":"actions","Rows":[{"Cells":{"name":{"Text":"Perm"},"category":{"Text":"G permanent"},"F_k":{"Number":20}}},{"Cells":{"name":{"Text":"Dom"},"category":{"Text":"A domestic"},"F_k":{"Number":8}}},{"Cells":{"name":{"Text":"Snow"},"category":{"Text":"snow ≤ 1000 m"},"F_k":{"Number":3}}}]},{"Symbol":"unit","Text":"kN/m"},{"Symbol":"method","Text":"6.10a and 6.10b"}]
```

**Block U** — UK office slab: Slab 18, Office 10 kN/m. Method 6.10a and 6.10b, xi 0.925.
```json
[{"Symbol":"actions","Rows":[{"Cells":{"name":{"Text":"Slab"},"category":{"Text":"G permanent"},"F_k":{"Number":18}}},{"Cells":{"name":{"Text":"Office"},"category":{"Text":"B office"},"F_k":{"Number":10}}}]},{"Symbol":"unit","Text":"kN/m"},{"Symbol":"method","Text":"6.10a and 6.10b"},{"Symbol":"xi","Number":0.925}]
```

**Block M** — Two permanent actions of opposite sign (12 and -3), office 6, wind suction -8, snow 4 kN/m.
```json
[{"Symbol":"actions","Rows":[{"Cells":{"name":{"Text":"G1"},"category":{"Text":"G permanent"},"F_k":{"Number":12}}},{"Cells":{"name":{"Text":"G2 opposing"},"category":{"Text":"G permanent"},"F_k":{"Number":-3}}},{"Cells":{"name":{"Text":"Imposed"},"category":{"Text":"B office"},"F_k":{"Number":6}}},{"Cells":{"name":{"Text":"Wind suction"},"category":{"Text":"wind"},"F_k":{"Number":-8}}},{"Cells":{"name":{"Text":"Snow"},"category":{"Text":"snow ≤ 1000 m"},"F_k":{"Number":4}}}]},{"Symbol":"unit","Text":"kN/m"},{"Symbol":"method","Text":"6.10"}]
```

**Block X** — Dead 10, Office 5, Wind 4 and an accidental impact 30 kN/m.
```json
[{"Symbol":"actions","Rows":[{"Cells":{"name":{"Text":"G"},"category":{"Text":"G permanent"},"F_k":{"Number":10}}},{"Cells":{"name":{"Text":"Imposed"},"category":{"Text":"B office"},"F_k":{"Number":5}}},{"Cells":{"name":{"Text":"Wind"},"category":{"Text":"wind"},"F_k":{"Number":4}}},{"Cells":{"name":{"Text":"Impact"},"category":{"Text":"accidental"},"F_k":{"Number":30}}}]},{"Symbol":"unit","Text":"kN/m"},{"Symbol":"method","Text":"6.10"}]
```

**Block F** — Roof dead 10, wind uplift -20, roof imposed 3 kN/m. gamma_G,sup 1.1, gamma_G,inf 0.9, K_FI 1.1.
```json
[{"Symbol":"actions","Rows":[{"Cells":{"name":{"Text":"Roof G"},"category":{"Text":"G permanent"},"F_k":{"Number":10}}},{"Cells":{"name":{"Text":"Wind uplift"},"category":{"Text":"wind"},"F_k":{"Number":-20}}},{"Cells":{"name":{"Text":"Roof imposed"},"category":{"Text":"H roofs"},"F_k":{"Number":3}}}]},{"Symbol":"unit","Text":"kN/m"},{"Symbol":"method","Text":"6.10"},{"Symbol":"gamma_G_sup","Number":1.1},{"Symbol":"gamma_G_inf","Number":0.9},{"Symbol":"K_FI","Number":1.1}]
```

**Block P** — Dead 10, Wind 8, Office 6 kN/m with a user psi table: wind 0.5/0.2/0 and office 0.7/0.5/0.3.
```json
[{"Symbol":"actions","Rows":[{"Cells":{"name":{"Text":"G"},"category":{"Text":"G permanent"},"F_k":{"Number":10}}},{"Cells":{"name":{"Text":"Wind"},"category":{"Text":"wind"},"F_k":{"Number":8}}},{"Cells":{"name":{"Text":"Imposed"},"category":{"Text":"B office"},"F_k":{"Number":6}}}]},{"Symbol":"unit","Text":"kN/m"},{"Symbol":"method","Text":"6.10"},{"Symbol":"psi","Rows":[{"Cells":{"category":{"Text":"wind"},"psi_0":{"Number":0.5},"psi_1":{"Number":0.2},"psi_2":{"Number":0.0}}},{"Cells":{"category":{"Text":"B office"},"psi_0":{"Number":0.7},"psi_1":{"Number":0.5},"psi_2":{"Number":0.3}}}]}]
```

**Block E** — No actions.
```json
[{"Symbol":"actions","Rows":[]},{"Symbol":"unit","Text":"kN/m"},{"Symbol":"method","Text":"6.10"}]
```

**Block 31** — 31 actions of 1 kN/m each (one over the limit).
```json
[{"Symbol":"actions","Rows":[{"Cells":{"name":{"Text":"G0"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G1"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G2"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G3"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G4"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G5"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G6"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G7"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G8"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G9"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G10"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G11"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G12"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G13"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G14"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G15"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G16"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G17"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G18"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G19"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G20"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G21"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G22"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G23"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G24"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G25"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G26"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G27"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G28"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G29"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}},{"Cells":{"name":{"Text":"G30"},"category":{"Text":"G permanent"},"F_k":{"Number":1}}}]},{"Symbol":"unit","Text":"kN/m"},{"Symbol":"method","Text":"6.10"}]
```

**Block H** — Block K1 with a user psi table that has a blank psi_1 for office.
```json
[{"Symbol":"actions","Rows":[{"Cells":{"name":{"Text":"Dead"},"category":{"Text":"G permanent"},"F_k":{"Number":10}}},{"Cells":{"name":{"Text":"Office"},"category":{"Text":"B office"},"F_k":{"Number":5}}}]},{"Symbol":"unit","Text":"kN/m"},{"Symbol":"method","Text":"6.10"},{"Symbol":"psi","Rows":[{"Cells":{"category":{"Text":"B office"},"psi_0":{"Number":0.7},"psi_2":{"Number":0.3}}}]}]
```

**Block U2** — Block K1 with the unit given as mm.
```json
[{"Symbol":"actions","Rows":[{"Cells":{"name":{"Text":"Dead"},"category":{"Text":"G permanent"},"F_k":{"Number":10}}},{"Cells":{"name":{"Text":"Office"},"category":{"Text":"B office"},"F_k":{"Number":5}}}]},{"Symbol":"unit","Text":"mm"},{"Symbol":"method","Text":"6.10"}]
```

**Block R** — Dead 10, Wind 6, Roof imposed 4 kN. Method 6.10, recommended psi set.
```json
[{"Symbol":"actions","Rows":[{"Cells":{"name":{"Text":"G"},"category":{"Text":"G permanent"},"F_k":{"Number":10}}},{"Cells":{"name":{"Text":"Wind"},"category":{"Text":"wind"},"F_k":{"Number":6}}},{"Cells":{"name":{"Text":"Roof imposed"},"category":{"Text":"H roofs"},"F_k":{"Number":4}}}]},{"Symbol":"unit","Text":"kN/m"},{"Symbol":"method","Text":"6.10"}]
```

## Notes for the tester
- Stale-upstream test: not applicable. All inputs are table cells or choices, so no input can be linked to another calculation.
- Steps 15 to 17 are finding steps. Step 14 is the unit test.
