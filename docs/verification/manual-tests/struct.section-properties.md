# Manual test — struct.section-properties (Section properties)
Liability: High. Time: ~45 min. Pre-requisites: None. Have your section tables (UB 457x191x98, SHS 100x100x5) for step 2. Step 19 needs a project open and two saved calculations. It reopens them from the Saved calculations panel (procedure L in README.md).

| # | Step | Inputs (value + unit) | Expected output (symbol = value unit) | Tolerance | Expected PASS/FAIL/BLOCK/WARN | Basis | Result | Notes |
|---|---|---|---|---|---|---|---|---|
| 1 | Press Ctrl+K. Type "Open worksheet: Section properties". Press Enter. Or use Engineering tree, Modules, Calculations, Worksheets, then Structural. | None. The form opens on the I-section with h 467.2, b 192.8, t 11.4, t_f 19.6, r 10.2 mm (UB 457x191x98). | A = 125.26 cm². I_xx = 45727 cm⁴. I_yy = 2347 cm⁴. i_x = 19.106 cm. i_y = 4.3286 cm. W_el_x = 1957.5 cm³. W_el_y = 243.47 cm³. W_pl_x = 2232.4 cm³. W_pl_y = 378.9 cm³. J = 121.27 cm⁴. I_w = 1.1755 dm⁶. A warning says J is an open thin-walled sum with a fillet term. | KAT: ±0.5 % on A, I, W; ±1 % on J and I_w. Script: ±0.05 % | PASS | KAT ka-ub-457x191x98 (table 125, 45730, 2347, 19.1, 4.33, 1957, 243, 2232, 379, 121, 1.18); independent script (J by finite elements) |  | Header shows struct.section-properties@1.0.0. The bar reads No pass/fail check. The Blue Book rounds A to 125. The outline gives 125.26. |
| 2 | Import Block SHS. | Block SHS: shape hollow rectangle. h = b = 100 mm. t = 5 mm. r = 7.5 mm. r_i = 5 mm | A = 18.732 cm². I_xx = 279.43 cm⁴. W_pl_x = 66.358 cm³. J = 438.06 cm⁴. | KAT: ±0.5 % on A, I, W; ±1 % on J. Script: ±0.05 % (J ±0.5 %) | PASS | KAT ka-shs-100x100x5 (18.7, 279, 66.4, 439); independent script (Bredt) |  | The default r of 10.2 mm is seeded in the form. The block sets 7.5 and 5 so the corners are 1.5 t and 1.0 t. |
| 3 | Import Block RECT. | Block RECT: shape rectangle. b = 100 mm. h = 200 mm | A = 200 cm². I_xx = 6666.7 cm⁴. I_yy = 1666.7 cm⁴. W_el_x = 666.67 cm³. W_pl_x = 1000 cm³. W_pl_y = 500 cm³. i_x = 5.7735 cm. J = 4573.6 cm⁴. | KAT: 1e-5 on I and W; J 0.2 % | PASS | KAT ka-rectangle (J 4580); independent script (exact series) |  | The KAT J of 4580 uses the rounded coefficient 0.229. The exact series gives 4573.6 cm⁴. |
| 4 | Import Block TUBE. | Block TUBE: shape tube. d = 200 mm. t = 10 mm | A = 59.69 cm². I_xx = 2701 cm⁴. W_el_x = 270.1 cm³. W_pl_x = 361.33 cm³. J = 5402 cm⁴. | KAT: 1e-4 | PASS | KAT ka-tube; independent script (closed forms) |  | Hand check: A = pi (200² - 180²)/4 = 5969 mm². |
| 5 | Import Block ANG. | Block ANG: polygon of 6 vertices, loop 1 | A = 27.36 cm². x_c = 21.395 mm. y_c = 51.395 mm. I_xx = 631.8 cm⁴. I_yy = 174.31 cm⁴. I_xy = -191.2 cm⁴. A warning says no torsion constant is computed for a polygon. | KAT: 2e-4 | PASS | KAT ka-angle-polygon; independent script |  | Hand check: A = 90 x 12 + 138 x 12 = 2736 mm². |
| 6 | Pick circle. Enter d = 100 mm. | d = 100 mm | A = 78.54 cm². I_xx = 490.87 cm⁴. W_pl_x = 166.67 cm³. J = 981.75 cm⁴. | 1e-9 (closed forms) | PASS | hand formula: pi d²/4; pi d⁴/64; d³/6; pi d⁴/32 |  | Closed-form shape. |
| 7 | Pick channel. Enter h = 200, b = 75, t = 6, t_f = 11, r = 12 mm. | h = 200 mm. b = 75 mm. t = 6 mm. t_f = 11 mm. r = 12 mm | A = 27.798 cm². x_c = 23.604 mm. I_xx = 1803.2 cm⁴. I_yy = 156.27 cm⁴. W_pl_y = 55.078 cm³. J = 7.9366 cm⁴. A warning says J is an open thin-walled sum. | ±0.05 % | PASS | independent script (tools/verification/struct.section-properties.py); J by the open-section sum b t³/3 |  | J is not the full torsion constant. See step 16. |
| 8 | Pick tee. Enter h = 150, b = 150, t = 10, t_f = 15, r = 0. Then pick angle. Enter h = 100, b = 75, t = 10, r = 12. | Tee: h = b = 150 mm. t = 10. t_f = 15. r = 0. Angle: h = 100. b = 75. t = 10. r = 12 mm | Tee: A = 36 cm². y_c = 114.38 mm. I_xx = 683.86 cm⁴. W_pl_x = 106.65 cm³. Angle: A = 16.809 cm². I_xx = 164.83 cm⁴. I_xy = -66.065 cm⁴. W_pl_x = 43.821 cm³. | ±0.05 % | PASS | independent script (tools/verification/struct.section-properties.py) |  | The y_c of the tee is measured from the form's own origin. Compare I and W only if y_c differs. |
| 9 | Pick hollow rectangle. Enter h = 200, b = 100, t = 8, r = 12, r_i = 4 mm. | h = 200 mm. b = 100 mm. t = 8 mm. r = 12 mm. r_i = 4 mm | A = 44.341 cm². I_xx = 2200.3 cm⁴. I_yy = 732.43 cm⁴. W_pl_x = 278.25 cm³. J = 1799.7 cm⁴. | ±0.05 % | PASS | independent script (tools/verification/struct.section-properties.py); J by Bredt on the mid-line |  | Explicit corner radii. |
| 10 | Import Block CA. Then Block CB. Then Block CC. | Blocks CA, CB, CC: composite sections made of rectangles | CA: A = 60 cm². y_c = 130 mm. I_xx = 2420 cm⁴. I_yy = 300 cm⁴. CB: A = 20 cm². I_xx = 46.667 cm⁴. I_yy = 126.67 cm⁴. I_xy = 69.282 cm⁴. theta = -60 °. CC: A = 180 cm². x_c = -3.3333 mm. y_c = -1.1111 mm. I_xx = 1617.8 cm⁴. I_xy = -66.667 cm⁴. Each shows a no-torsion warning. | ±0.01 % | PASS | independent script (tools/verification/struct.section-properties.py) |  | Composite mode with add, subtract and rotation. Tube and hollow parts are step 14. |
| 11 | Pick rectangle: b = 0.001 mm, h = 0.002 mm. Then b = h = 100000 mm. Then pick I-section: h = 1000, b = 300, t = 0.5, t_f = 0.5, r = 0. | Extreme sizes | Tiny rectangle: I_xx = b h³/12 = 0 cm⁴. 1e5 mm square: I_xx = 8.3333e14 cm⁴. W_pl_x = 2.5e11 cm³. Thin I: A = 7.995 cm². J = 0.0066625 cm⁴. | Relative 1e-8 | PASS | hand formula: b h³/12; b h²/4; open-section sum |  | Boundaries of the allowed sizes. |
| 12 | Pick tube. Set d = 200 mm and t = 100 mm. Then pick rectangle and clear h. | tube t = 100 mm (half the diameter). Rectangle with h blank | Each case blocks. The wall must be below half the size. A missing dimension is named in the message. | Text match | BLOCK | product behaviour (no number) |  | Negative test: out of range or missing input. |
| 13 | Look at the unit list of h. Then paste Block N. | Block N: h = 200 N | The import is refused, or h flags a unit error. h must not quietly show 200 mm. | Text match | BLOCK | product behaviour (no number) |  | Negative test: unit mismatch. Suspected defect in the import path (CalcSheetView.BuildField falls back to the default unit). Not yet in a P1 report. Record FAIL if h shows 200 mm without a message. |
| 14 | Import Block CT. Then Block CS. Then Block CF. Read A and I_xx. | Blocks CT, CS, CF | CT: A = 59.69 cm². I_xx = 2701 cm⁴. CS: A = 18.732 cm². CF: A = 840.31 cm². Today CT shows 568.6 cm² and 13006 cm⁴. CS shows 180.3 cm². CF shows 331.4 cm². | ±0.1 % | FAIL | independent script (tools/verification/struct.section-properties.py); hand: tube area pi (200² - 180²)/4 |  | Known defect P1-struct.section-properties-01 (rated Critical in review); expect FAIL until fixed. Every tube or hollow part in a composite is wrong. There is no warning. |
| 15 | Import Block CH. | Block CH: channel. h = 100 mm. b = 20 mm. t = 10 mm. t_f = 5 mm. r = 15 mm | The sheet blocks: t + r (25 mm) is larger than b (20 mm). so the root radius does not fit. Today it returns A = 11.97 cm². | Text match | BLOCK | P1 report (no number); hand: t + r above b |  | Known defect P1-struct.section-properties-02 (Low); expect FAIL until fixed. |
| 16 | Compare J for the channel (step 7). The angle and the tee (step 8) with your section tables or a torsion program. | Channel 200 x 75. angle 100 x 75 x 10. tee 150 x 150 | J includes the root-fillet effect, or the warning states the size of the error. Reviewer values by finite elements: channel 8.29 cm⁴ (sheet 7.94, 4.3 % low). angle 6.04 cm⁴ (sheet 5.50, 8.9 % low), tee 20.79 cm⁴ (sheet 21.38, 2.8 % high). | ±1 % | FAIL | independent script (finite-element torsion) |  | Known defect P1-struct.section-properties-03 (Medium); expect FAIL until fixed. Channel and angle are conservative. The sharp tee is high by 2.8 %. |
| 17 | Import Block X. Then Block HO. | Blocks X and HO | Both block with a clear geometry message: the outline crosses itself, and the hole lies outside the outline. Today X blocks with a message about a square root of a negative number. HO runs with A = 99 cm² and no notice. | Text match | BLOCK | P1 report (no number); hand: 100 x 100 = 100 cm², a hole outside removes nothing |  | Known defect P1-struct.section-properties-04 (Low, Medium in review); expect FAIL until fixed. |
| 18 | Import Block OV. | Block OV: two added parts that overlap | A warning says the added parts overlap. Their union has A = 40 cm². Today A = 60 cm² (the shared area is counted twice) with no warning. | Text match | WARN | hand formula: union of two rectangles 200 x 20 and 100 x 20 = 4000 mm² |  | Known defect P1-struct.section-properties-R1 (Low); expect FAIL until fixed. |
| 19 | Stale upstream. Follow procedure L in README.md. Upstream: mech.iso286-fit on its defaults (25 mm H7/g6), saved as UP (D_hole_max 25.021 mm). Downstream: this sheet with shape rectangle, b = 100 mm, saved as DOWN. Link h of DOWN to D_hole_max of UP. The pairing is artificial: no section dimension comes from another calculation in practice. Then in the Saved calculations panel select UP and press Open. If the status line says the form has unsaved changes, press Open a second time to discard them. UP: set D to 30 mm and press Save changes. Select DOWN and press Open. | Link: h to D_hole_max | Within a second the DOWN row in the Saved calculations list ends with · STALE. Select DOWN and press Open: status line Opened 'DOWN' · rev A · Draft. Orange banner: Stale — 'UP' rev A now gives a different 'D_hole_max' than the value 'h' took. Button: Update linked inputs. Press Record check: status line reads Calculation 'DOWN' is stale: 'UP' rev A now gives a different 'D_hole_max' than the value 'h' took. Update it before checking. The watermark stays DRAFT. Press Update linked inputs: the banner clears; h takes the new D_hole_max of UP; the status line reads Linked inputs updated to the current revisions and recalculated. | Text match | WARN (stale banner); BLOCK (check refused); then PASS after the update | Product behaviour (no number): staleness rules in CalcInstanceService; after the update, compare the linked input with the output of UP |  | Procedure L in README.md. Reopening uses the Saved calculations panel. An issued upstream goes stale the same way: Revise it, then the banner reads that the input is pinned to rev A, which is superseded by rev B. |

## Import blocks
Copy the whole line of a block to the clipboard. Then press *Import inputs (CSV/JSON)*. The status line must read "Imported n input(s)".

**Block SHS** — SHS 100 x 100 x 5: hollow rectangle, h 100, b 100. t 5 mm, outer radius 7.5 mm, inner radius 5 mm.
```json
[{"Symbol":"shape","Text":"hollow rectangle"},{"Symbol":"h","Number":100,"Unit":"mm"},{"Symbol":"b","Number":100,"Unit":"mm"},{"Symbol":"t","Number":5,"Unit":"mm"},{"Symbol":"r","Number":7.5,"Unit":"mm"},{"Symbol":"r_i","Number":5,"Unit":"mm"}]
```

**Block RECT** — Rectangle b 100 mm, h 200 mm.
```json
[{"Symbol":"shape","Text":"rectangle"},{"Symbol":"h","Number":200,"Unit":"mm"},{"Symbol":"b","Number":100,"Unit":"mm"}]
```

**Block TUBE** — Tube, outside diameter d 200 mm, wall t 10 mm.
```json
[{"Symbol":"shape","Text":"tube"},{"Symbol":"d","Number":200,"Unit":"mm"},{"Symbol":"t","Number":10,"Unit":"mm"}]
```

**Block ANG** — Unequal angle as a polygon: (0,0), (90,0), (90,12), (12,12), (12,150), (0,150) mm, one loop.
```json
[{"Symbol":"shape","Text":"polygon"},{"Symbol":"vertices","Rows":[{"Cells":{"x":{"Number":0,"Unit":"mm"},"y":{"Number":0,"Unit":"mm"},"loop":{"Number":1}}},{"Cells":{"x":{"Number":90,"Unit":"mm"},"y":{"Number":0,"Unit":"mm"},"loop":{"Number":1}}},{"Cells":{"x":{"Number":90,"Unit":"mm"},"y":{"Number":12,"Unit":"mm"},"loop":{"Number":1}}},{"Cells":{"x":{"Number":12,"Unit":"mm"},"y":{"Number":12,"Unit":"mm"},"loop":{"Number":1}}},{"Cells":{"x":{"Number":12,"Unit":"mm"},"y":{"Number":150,"Unit":"mm"},"loop":{"Number":1}}},{"Cells":{"x":{"Number":0,"Unit":"mm"},"y":{"Number":150,"Unit":"mm"},"loop":{"Number":1}}}]}]
```

**Block CA** — Composite T. Rectangle 20 x 180 mm at (0, 90). Rectangle 120 x 20 mm at (0, 190). Both add.
```json
[{"Symbol":"shape","Text":"composite"},{"Symbol":"parts","Rows":[{"Cells":{"shape":{"Text":"rectangle"},"x0":{"Number":0,"Unit":"mm"},"y0":{"Number":90,"Unit":"mm"},"b":{"Number":20,"Unit":"mm"},"h":{"Number":180,"Unit":"mm"},"sign":{"Text":"add"}}},{"Cells":{"shape":{"Text":"rectangle"},"x0":{"Number":0,"Unit":"mm"},"y0":{"Number":190,"Unit":"mm"},"b":{"Number":120,"Unit":"mm"},"h":{"Number":20,"Unit":"mm"},"sign":{"Text":"add"}}}]}]
```

**Block CB** — Composite: rectangle 100 x 20 mm at (50, 40), rotated 30 degrees, add.
```json
[{"Symbol":"shape","Text":"composite"},{"Symbol":"parts","Rows":[{"Cells":{"shape":{"Text":"rectangle"},"x0":{"Number":50,"Unit":"mm"},"y0":{"Number":40,"Unit":"mm"},"b":{"Number":100,"Unit":"mm"},"h":{"Number":20,"Unit":"mm"},"rot":{"Number":30},"sign":{"Text":"add"}}}]}]
```

**Block CC** — Composite. Rectangle 200 x 100 mm at (0, 0), add. Rectangle 50 x 40 mm at (30, 10), subtract.
```json
[{"Symbol":"shape","Text":"composite"},{"Symbol":"parts","Rows":[{"Cells":{"shape":{"Text":"rectangle"},"x0":{"Number":0,"Unit":"mm"},"y0":{"Number":0,"Unit":"mm"},"b":{"Number":200,"Unit":"mm"},"h":{"Number":100,"Unit":"mm"},"sign":{"Text":"add"}}},{"Cells":{"shape":{"Text":"rectangle"},"x0":{"Number":30,"Unit":"mm"},"y0":{"Number":10,"Unit":"mm"},"b":{"Number":50,"Unit":"mm"},"h":{"Number":40,"Unit":"mm"},"sign":{"Text":"subtract"}}}]}]
```

**Block N** — Depth entered in newtons.
```json
[{"Symbol":"shape","Text":"rectangle"},{"Symbol":"h","Number":200,"Unit":"N"},{"Symbol":"b","Number":100,"Unit":"mm"}]
```

**Block CT** — Composite with one tube, d 200 mm, t 10 mm, add.
```json
[{"Symbol":"shape","Text":"composite"},{"Symbol":"parts","Rows":[{"Cells":{"shape":{"Text":"tube"},"x0":{"Number":0,"Unit":"mm"},"y0":{"Number":0,"Unit":"mm"},"h":{"Number":200,"Unit":"mm"},"t":{"Number":10,"Unit":"mm"},"sign":{"Text":"add"}}}]}]
```

**Block CS** — Composite with one SHS 100 x 100 x 5 mm (corner radius 7.5 mm), add.
```json
[{"Symbol":"shape","Text":"composite"},{"Symbol":"parts","Rows":[{"Cells":{"shape":{"Text":"hollow rectangle"},"x0":{"Number":0,"Unit":"mm"},"y0":{"Number":0,"Unit":"mm"},"b":{"Number":100,"Unit":"mm"},"h":{"Number":100,"Unit":"mm"},"t":{"Number":5,"Unit":"mm"},"r":{"Number":7.5,"Unit":"mm"},"sign":{"Text":"add"}}}]}]
```

**Block CF** — Composite: 300 mm square minus a tube d 200 mm, t 10 mm.
```json
[{"Symbol":"shape","Text":"composite"},{"Symbol":"parts","Rows":[{"Cells":{"shape":{"Text":"rectangle"},"x0":{"Number":0,"Unit":"mm"},"y0":{"Number":0,"Unit":"mm"},"b":{"Number":300,"Unit":"mm"},"h":{"Number":300,"Unit":"mm"},"sign":{"Text":"add"}}},{"Cells":{"shape":{"Text":"tube"},"x0":{"Number":0,"Unit":"mm"},"y0":{"Number":0,"Unit":"mm"},"h":{"Number":200,"Unit":"mm"},"t":{"Number":10,"Unit":"mm"},"sign":{"Text":"subtract"}}}]}]
```

**Block CH** — Channel with a root radius that does not fit. h 100, b 20, t 10, t_f 5, r 15 mm.
```json
[{"Symbol":"shape","Text":"channel"},{"Symbol":"h","Number":100,"Unit":"mm"},{"Symbol":"b","Number":20,"Unit":"mm"},{"Symbol":"t","Number":10,"Unit":"mm"},{"Symbol":"t_f","Number":5,"Unit":"mm"},{"Symbol":"r","Number":15,"Unit":"mm"}]
```

**Block X** — Polygon: self-crossing outline (0,0), (100,100), (100,0), (0,60), one loop.
```json
[{"Symbol":"shape","Text":"polygon"},{"Symbol":"vertices","Rows":[{"Cells":{"x":{"Number":0,"Unit":"mm"},"y":{"Number":0,"Unit":"mm"},"loop":{"Number":1}}},{"Cells":{"x":{"Number":100,"Unit":"mm"},"y":{"Number":100,"Unit":"mm"},"loop":{"Number":1}}},{"Cells":{"x":{"Number":100,"Unit":"mm"},"y":{"Number":0,"Unit":"mm"},"loop":{"Number":1}}},{"Cells":{"x":{"Number":0,"Unit":"mm"},"y":{"Number":60,"Unit":"mm"},"loop":{"Number":1}}}]}]
```

**Block HO** — Polygon. A 100 mm square outline. A 10 x 10 mm hole at x = 200 to 210 mm, outside the outline.
```json
[{"Symbol":"shape","Text":"polygon"},{"Symbol":"vertices","Rows":[{"Cells":{"x":{"Number":0,"Unit":"mm"},"y":{"Number":0,"Unit":"mm"},"loop":{"Number":1}}},{"Cells":{"x":{"Number":100,"Unit":"mm"},"y":{"Number":0,"Unit":"mm"},"loop":{"Number":1}}},{"Cells":{"x":{"Number":100,"Unit":"mm"},"y":{"Number":100,"Unit":"mm"},"loop":{"Number":1}}},{"Cells":{"x":{"Number":0,"Unit":"mm"},"y":{"Number":100,"Unit":"mm"},"loop":{"Number":1}}},{"Cells":{"x":{"Number":200,"Unit":"mm"},"y":{"Number":0,"Unit":"mm"},"loop":{"Number":2}}},{"Cells":{"x":{"Number":210,"Unit":"mm"},"y":{"Number":0,"Unit":"mm"},"loop":{"Number":2}}},{"Cells":{"x":{"Number":210,"Unit":"mm"},"y":{"Number":10,"Unit":"mm"},"loop":{"Number":2}}},{"Cells":{"x":{"Number":200,"Unit":"mm"},"y":{"Number":10,"Unit":"mm"},"loop":{"Number":2}}}]}]
```

**Block OV** — Composite. Rectangle 200 x 20 mm. Rectangle 100 x 20 mm placed over it. Both add and they overlap.
```json
[{"Symbol":"shape","Text":"composite"},{"Symbol":"parts","Rows":[{"Cells":{"shape":{"Text":"rectangle"},"x0":{"Number":0,"Unit":"mm"},"y0":{"Number":0,"Unit":"mm"},"b":{"Number":200,"Unit":"mm"},"h":{"Number":20,"Unit":"mm"},"sign":{"Text":"add"}}},{"Cells":{"shape":{"Text":"rectangle"},"x0":{"Number":0,"Unit":"mm"},"y0":{"Number":0,"Unit":"mm"},"b":{"Number":100,"Unit":"mm"},"h":{"Number":20,"Unit":"mm"},"sign":{"Text":"add"}}}]}]
```

## Notes for the tester
- Steps 14 to 18 are finding steps. Step 13 is the unit test. Step 16 uses reviewer values. Check them against your own source. Step 19 is the stale test.
