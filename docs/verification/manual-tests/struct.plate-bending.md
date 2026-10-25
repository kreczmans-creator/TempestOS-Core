# Manual test — struct.plate-bending (Plate bending)
Liability: Medium. Time: ~60 min. Pre-requisites: a project open in the desktop app. A pocket calculator for the hand checks. Step 11 also needs the calculator fp.cylinder in the same project.

| # | Step | Inputs (value + unit) | Expected output (symbol = value unit) | Tolerance | Expected PASS/FAIL/BLOCK/WARN | Basis | Result | Notes |
|---|---|---|---|---|---|---|---|---|
| 1 | Open the calculator. Press Ctrl+K, type "Open worksheet: Plate bending", press Enter. Or use Engineering tree, Modules, Calculations, Worksheets, Structural. Leave every input at its default. | Defaults: rectangular; simply supported; a 600 mm; b 400 mm; t 6 mm; uniform pressure 10 kPa; E 210 GPa; nu 0.3; rho 7850; f_y 275 MPa | Header shows struct.plate-bending@1.2.1. D = 4153.8 N·m; w_max = 0.47603 mm; w_over_t = 0.079338; M_x_c = 79.748 N·m/m; M_y_c = 129.86 N·m/m; M_xy_k = 68.691 N·m/m; sigma_max = 19.829 MPa; f1 = 133.17 Hz; u_stress = 0.072107; u_defl = 0.29752. Bar: PASS. | Shown digits | PASS | independent script (tools/verification/struct.plate-bending.py); hand: D = E t³ / (12 (1 - nu²)) = 4153.8 N·m |  | The 600 x 400 x 6 steel plate is the case of the KAT ka-rect-simple-natural. Check f1 against 133.17 Hz. |
| 2 | Import Block K1 and read the results. Then import Block K2 and read them again. | K1: circular; clamped; a 250; t 5 mm; q 20 kPa; E 200 GPa; uniform. K2: the same, simply supported | K1: D = 2289.4 N·m; w_max = 0.5332 mm; sigma_principal = 37.5 MPa; sigma_max = 33.331 MPa; f1 = 198.68 Hz. K2: w_max = 2.1738 mm; sigma_principal = 61.875 MPa; sigma_max = 61.875 MPa; f1 = 95.98 Hz. Bar: PASS / FAIL (Deflection w_max ≤ span/ratio u = 1.08691). | ±0.01 % | PASS / FAIL (Deflection w_max ≤ span/ratio u = 1.08691) | KATs ka-circular-clamped-uniform and ka-circular-simple-uniform; independent script |  | Hand check K1: w = q a⁴ / (64 D) = 20e3 x 0.25⁴ / (64 x 2289.4) = 0.5332 mm. Edge stress = 3 q a² / (4 t²) = 37.5 MPa. K2 centre stress = 3 (3 + nu) q a² / (8 t²) = 61.875 MPa. |
| 3 | Import Block K3 and read the results. Then import Block K4 and read them. | K3: simply supported; central patch; r0 100 mm; q 20 kPa. K4: clamped; ring (circular only); r_ring 125 mm; load_basis total force P; P 4 kN | K3: w_max = 0.73327 mm; sigma_principal = 25.958 MPa; sigma_max = 25.958 MPa. K4: w_max = 0.87643 mm; sigma_principal = 57.296 MPa; sigma_max = 50.926 MPa. | ±0.01 % (K4 stresses ±0.0001 %) | PASS / PASS | KATs ka-circular-simple-patch and ka-circular-clamped-ring; independent script |  | In K4 the stress is greatest at the clamped edge or at the ring. The sheet scans both. Hand check K4: w = F a² (1 - b² + 2 b² ln b) / (16 pi D), b = 0.5, gives 0.8764 mm. |
| 4 | Import Block K5 and read the results. Then import Block K6 and read them. | K5: rectangular; simply supported; a 500; b 500; t 8 mm; uniform 10 kPa. K6: a 600; b 400; t 6 mm; central patch; u 150; v 100 mm; q 50 kPa | K5: w_max = 0.25786 mm; M_x_c = 119.72 N·m/m; M_xy_k = 81.206 N·m/m; sigma_max = 13.186 MPa. K6: w_max = 0.39813 mm; M_x_c = 117.06 N·m/m; M_y_c = 152.72 N·m/m. | ±0.001 % | PASS / PASS | KATs ka-rect-simple-square-series and ka-rect-simple-patch; independent script (Navier series) |  | Hand check K5: w = 0.00406 q a⁴ / D = 0.2577 mm (book coefficient, 0.1 % from the series). The corner twist governs the stress: sqrt(3) x 6 x 81.2 / 64 = 13.19 MPa. |
| 5 | Import Block K7 and read the results and the warnings. | Block K7: rectangular; clamped; a 500; b 500; t 8 mm; uniform 10 kPa | w_max = 0.080318 mm; M_x_c = 57.263 N·m/m; M_edge = 128.33 N·m/m; sigma_max = 10.694 MPa; f1 = 286.85 Hz. Warning: the clamped solution is approximate (ritz-approx). | ±0.1 % (w_max ±0.0001 %) | PASS | KATs ka-rect-clamped-square and ka-rect-clamped-natural; independent script (Ritz with beam functions) |  | These KATs are Unconfirmed. Book values: w = 0.00126 q a⁴ / D = 0.0800 mm and edge moment 0.0513 q a² = 128.3 N·m/m. Please check your copy of the book. |
| 6 | Typical case, circular plate with a large patch. Import Block T1 and read the results. | Block T1: circular; simply supported; a 300; t 5 mm; central patch; r0 240 mm; q 40 kPa; E 200 GPa | w_max = 8.0574 mm; w_over_t = 1.6115; sigma_principal = 162.86 MPa; sigma_max = 162.86 MPa; f1 = 66.653 Hz; u_stress = 0.59221; u_defl = 3.3573. Bar: FAIL (Deflection w_max ≤ span/ratio u = 3.35727). | ±0.01 % | FAIL (Deflection w_max ≤ span/ratio u = 3.35727) | Independent script (circular patch, extra row) |  | w/t is above 0.5, so a large-deflection warning is expected. Read the warnings. |
| 7 | Typical case, clamped 2:1 plate. Import Block T2 and read the results. | Block T2: rectangular; clamped; a 1000; b 500; t 8 mm; uniform 10 kPa | w_max = 0.16078 mm; M_x_c = 39.52 N·m/m; M_y_c = 102.89 N·m/m; M_edge = 207.16 N·m/m; sigma_max = 17.262 MPa; f1 = 195.92 Hz; u_stress = 0.062772; u_defl = 0.080392. Bar: PASS. | ±0.1 % | PASS | Independent script (Ritz, clamped 2:1 row) |  | Hand check: the edge moment at the long side is near q b² / 12 = 208.3 N·m/m for a long plate. Here it is 207.2. |
| 8 | Typical case, aluminium plate with a patch. Import Block T3 and read the results. | Block T3: rectangular; simply supported; a 1200; b 400; t 6 mm; E 70 GPa; nu 0.33; rho 2700; central patch u 240, v 80 mm; q 50 kPa | w_max = 1.595 mm; M_x_c = 118.39 N·m/m; M_y_c = 192.52 N·m/m; sigma_max = 28.031 MPa; sigma_principal = 32.087 MPa; f1 = 101.91 Hz; u_stress = 0.10193; u_defl = 0.99688. Bar: PASS. | ±0.01 % | PASS | Independent script (Navier patch, 3:1 row) |  | The yield strength stays 275 MPa. Change it to match your alloy. |
| 9 | Boundary cases. Import Block L1 and read the results. Then import Block L2 and read them. | L1: clamped; a 5000; b 500; t 8 mm; 10 kPa. L2: circular; simply supported; a 300; t 5 mm; patch r0 300 mm; q 40 kPa | L1: M_edge = 208.33 N·m/m, hand value (the sheet shows 208.39 N·m/m). L2: w_max = 9.0153 mm; sigma_principal = 178.2 MPa; sigma_max = 178.2 MPa. | ±0.05 % (L1); ±0.01 % (L2) | L1: PASS. L2: FAIL (Deflection w_max ≤ span/ratio u = 3.75637) | Hand formula (L1, strip limit q b² / 12); independent script (L2, circular patch beta 1 row) |  | Hand check L1: a long clamped strip has edge moment q b² / 12 = 208.33 N·m/m. The independent clamped solver does not converge at this ratio, so only the strip limit is used. Hand check L2: a full patch equals a uniform load, w = (5 + nu) q a⁴ / (64 (1 + nu) D) = 9.0153 mm. |
| 10 | NEGATIVE: values, geometry and units that the sheet must refuse. Import each line on its own: t 0.05 mm; q 0 kPa; a 4 mm; nu 0.5; ring load on a rectangle; q in mm; q in furlong. Reset after each. | t 0.05 mm; q 0 kPa; a 4 mm; nu 0.5; load_type ring (circular only) with a rectangular plate; q = 10 mm; q = 10 furlong | t: must be at least 0.1 mm; 0.05 mm was given. q: must be at least 0.000001 kPa; 0 kPa was given. a: must be at least 5 mm; 4 mm was given. nu: must be at most 0.49; 0.5 was given. Ring on a rectangle: a ring load applies to a circular plate. q in mm: q must be entered in a unit of kPa, not mm. q in furlong: Unknown unit 'furlong'. No results in each case. | Text match | BLOCK | Template hard limits and rules; probe confirms the block messages |  | Use seven separate imports. For a circular plate with a ring radius of 700 mm on a radius of 600 mm the sheet says the ring must lie between 1 % and 99 % of the plate radius. The unit refusals come from the import. Suspected defect, not yet in a P1 report. The form may keep the number and fall back to kPa with no message. Record FAIL in that case. |
| 11 | NEGATIVE: stale upstream. Follow Procedure L in README.md. UP is Cylinder and actuator circuit at its defaults. DOWN is this calculator with thickness t 20 mm and load basis total force P. Link P to UP output F_ext. Then open UP, change p from 160 to 200 bar and press Save changes. Open DOWN. | UP: p 160 bar, then 200 bar. DOWN: rectangular 600 x 400, simply supported, t 20 mm, uniform, load_basis total force P, with P linked to UP output F_ext | After the link: P = 119.38 kN, source link. w_max = 0.63932 mm; M_x_c = 3966.8 N·m/m; M_y_c = 6459.3 N·m/m; sigma_max = 88.772 MPa; u_stress = 0.32281; u_defl = 0.39958. The DOWN row ends with STALE. The banner reads that UP now gives a different 'F_ext' than the value 'P' took. Record check is refused: Calculation 'DOWN' is stale. After Update linked inputs: P = 149.23 kN. w_max = 0.79915 mm; M_x_c = 4958.5 N·m/m; M_y_c = 8074.1 N·m/m; sigma_max = 110.96 MPa; u_stress = 0.40351; u_defl = 0.49947. | ±0.01 % on values | WARN (stale banner); BLOCK (check refused); then PASS after the update | Independent script (Navier series; q = P / (a x b)); code rule (staleness) |  | Procedure L in README.md. This models a plate under a cylinder thrust. The thickness is 20 mm to keep the stress in range. Compare the deflection with the limit span/250. |
| 12 | Known defect P1-struct.plate-bending-04 (Low). Import Block K1 (step 2) and read M_edge. Then import Block T2 (step 7) and read M_edge. | Block K1: circular clamped plate. Block T2: clamped rectangle 1000 x 500 x 8 mm | Product: circular M_edge = -156.25 N·m/m (signed). Rectangle M_edge = 207.16 N·m/m (larger absolute value, positive). Correct: one sign convention, or a label that names the sign. For a clamped plate under downward load both edge moments are hogging (negative). | ±0.01 % | FAIL | Independent script (circular edge moment = -q a² / 8 = -156.25 N·m/m); P1 report |  | Known defect P1-struct.plate-bending-04; expect FAIL until fixed. Hand check: q a² / 8 = 20e3 x 0.0625 / 8 = 156.25 N·m/m. |
| 13 | Known defects P1-struct.plate-bending-01 and -02 (Low). Import Block L1 (step 9) and read the warnings and f1. Then import Block D2 and read the warnings, M_y_c and M_xy_k. | Block L1: clamped strip 5000 x 500 x 8 mm, 10 kPa. Block D2: simply supported strip with a 100 x 500 mm patch at 100 kPa | Product L1: f1 = 178.82 Hz, with a warning that the frequency is approximate (last change 3.06e-11, limit 1e-11). D2: M_y_c = 565.36 N·m/m; M_xy_k = 0.0016966 N·m/m, with a warning that the Navier series reached its cap (last change 0.017). Correct: no warning in either case. The frequency has converged to 1e-8. The governing moment M_y_c has converged. The corner twist is 0.0017 N·m/m, so it does not matter. | Text match | FAIL | Independent script (f1 = 178.81680 Hz from orders 40 x 5 to 48 x 8; corner M_xy = 0.0017 against M_y_c = 565 N·m/m) |  | Known defects P1-struct.plate-bending-01 and -02; expect FAIL until fixed. Both warnings are false alarms. They teach the user to ignore real ones. |

## Import blocks
Copy the whole line of a block to the clipboard. Then press *Import inputs (CSV/JSON)*. The status line must read "Imported n input(s)".

**Block K1** — Circular plate, clamped, radius 250 mm, t 5 mm, uniform 20 kPa, E 200 GPa. Known-answer case ka-circular-clamped-uniform.
```json
[{"Symbol":"shape","Text":"circular"},{"Symbol":"support","Text":"clamped"},{"Symbol":"a","Number":250,"Unit":"mm"},{"Symbol":"t","Number":5,"Unit":"mm"},{"Symbol":"q","Number":20,"Unit":"kPa"},{"Symbol":"E","Number":200,"Unit":"GPa"},{"Symbol":"load_type","Text":"uniform"}]
```

**Block K2** — Block K1 simply supported. Known-answer case ka-circular-simple-uniform.
```json
[{"Symbol":"shape","Text":"circular"},{"Symbol":"support","Text":"simply supported"},{"Symbol":"a","Number":250,"Unit":"mm"},{"Symbol":"t","Number":5,"Unit":"mm"},{"Symbol":"q","Number":20,"Unit":"kPa"},{"Symbol":"E","Number":200,"Unit":"GPa"},{"Symbol":"load_type","Text":"uniform"}]
```

**Block K3** — Block K2 with a central patch of radius 100 mm. Known-answer case ka-circular-simple-patch.
```json
[{"Symbol":"shape","Text":"circular"},{"Symbol":"support","Text":"simply supported"},{"Symbol":"a","Number":250,"Unit":"mm"},{"Symbol":"t","Number":5,"Unit":"mm"},{"Symbol":"q","Number":20,"Unit":"kPa"},{"Symbol":"E","Number":200,"Unit":"GPa"},{"Symbol":"load_type","Text":"central patch"},{"Symbol":"r0","Number":100,"Unit":"mm"}]
```

**Block K4** — Block K1 with a ring load of 4 kN at radius 125 mm, entered as total force. Known-answer case ka-circular-clamped-ring.
```json
[{"Symbol":"shape","Text":"circular"},{"Symbol":"support","Text":"clamped"},{"Symbol":"a","Number":250,"Unit":"mm"},{"Symbol":"t","Number":5,"Unit":"mm"},{"Symbol":"q","Number":20,"Unit":"kPa"},{"Symbol":"E","Number":200,"Unit":"GPa"},{"Symbol":"load_type","Text":"ring (circular only)"},{"Symbol":"r_ring","Number":125,"Unit":"mm"},{"Symbol":"P","Number":4,"Unit":"kN"},{"Symbol":"load_basis","Text":"total force P"}]
```

**Block K5** — Square plate 500 x 500 x 8 mm, simply supported, uniform 10 kPa. Known-answer case ka-rect-simple-square-series.
```json
[{"Symbol":"shape","Text":"rectangular"},{"Symbol":"support","Text":"simply supported"},{"Symbol":"a","Number":500,"Unit":"mm"},{"Symbol":"b","Number":500,"Unit":"mm"},{"Symbol":"t","Number":8,"Unit":"mm"},{"Symbol":"load_type","Text":"uniform"}]
```

**Block K6** — Plate 600 x 400 x 6 mm, simply supported, central patch 150 x 100 mm at 50 kPa. Known-answer case ka-rect-simple-patch.
```json
[{"Symbol":"shape","Text":"rectangular"},{"Symbol":"support","Text":"simply supported"},{"Symbol":"load_type","Text":"central patch"},{"Symbol":"q","Number":50,"Unit":"kPa"},{"Symbol":"u","Number":150,"Unit":"mm"},{"Symbol":"v","Number":100,"Unit":"mm"}]
```

**Block K7** — Square plate 500 x 500 x 8 mm, clamped, uniform 10 kPa. Known-answer cases ka-rect-clamped-square and ka-rect-clamped-natural.
```json
[{"Symbol":"shape","Text":"rectangular"},{"Symbol":"support","Text":"clamped"},{"Symbol":"a","Number":500,"Unit":"mm"},{"Symbol":"b","Number":500,"Unit":"mm"},{"Symbol":"t","Number":8,"Unit":"mm"},{"Symbol":"load_type","Text":"uniform"}]
```

**Block T1** — Circular plate, simply supported, radius 300 mm, t 5 mm, patch of radius 240 mm at 40 kPa.
```json
[{"Symbol":"shape","Text":"circular"},{"Symbol":"support","Text":"simply supported"},{"Symbol":"a","Number":300,"Unit":"mm"},{"Symbol":"t","Number":5,"Unit":"mm"},{"Symbol":"q","Number":40,"Unit":"kPa"},{"Symbol":"E","Number":200,"Unit":"GPa"},{"Symbol":"load_type","Text":"central patch"},{"Symbol":"r0","Number":240,"Unit":"mm"}]
```

**Block T2** — Clamped plate 1000 x 500 x 8 mm, uniform 10 kPa.
```json
[{"Symbol":"shape","Text":"rectangular"},{"Symbol":"support","Text":"clamped"},{"Symbol":"a","Number":1000,"Unit":"mm"},{"Symbol":"b","Number":500,"Unit":"mm"},{"Symbol":"t","Number":8,"Unit":"mm"},{"Symbol":"load_type","Text":"uniform"}]
```

**Block T3** — Aluminium plate 1200 x 400 x 6 mm, simply supported, patch 240 x 80 mm at 50 kPa.
```json
[{"Symbol":"shape","Text":"rectangular"},{"Symbol":"support","Text":"simply supported"},{"Symbol":"a","Number":1200,"Unit":"mm"},{"Symbol":"b","Number":400,"Unit":"mm"},{"Symbol":"t","Number":6,"Unit":"mm"},{"Symbol":"q","Number":50,"Unit":"kPa"},{"Symbol":"E","Number":70,"Unit":"GPa"},{"Symbol":"nu","Number":0.33},{"Symbol":"rho","Number":2700,"Unit":"kg/m^3"},{"Symbol":"load_type","Text":"central patch"},{"Symbol":"u","Number":240,"Unit":"mm"},{"Symbol":"v","Number":80,"Unit":"mm"}]
```

**Block L1** — Clamped strip: 5000 x 500 x 8 mm, uniform 10 kPa (aspect ratio 10).
```json
[{"Symbol":"shape","Text":"rectangular"},{"Symbol":"support","Text":"clamped"},{"Symbol":"a","Number":5000,"Unit":"mm"},{"Symbol":"b","Number":500,"Unit":"mm"},{"Symbol":"t","Number":8,"Unit":"mm"},{"Symbol":"load_type","Text":"uniform"}]
```

**Block L2** — Circular plate, simply supported, with the patch covering the whole plate: r0 = a = 300 mm.
```json
[{"Symbol":"shape","Text":"circular"},{"Symbol":"support","Text":"simply supported"},{"Symbol":"a","Number":300,"Unit":"mm"},{"Symbol":"t","Number":5,"Unit":"mm"},{"Symbol":"q","Number":40,"Unit":"kPa"},{"Symbol":"E","Number":200,"Unit":"GPa"},{"Symbol":"load_type","Text":"central patch"},{"Symbol":"r0","Number":300,"Unit":"mm"}]
```

**Block D2** — Simply supported strip 5000 x 500 x 8 mm with a patch 100 x 500 mm at 100 kPa.
```json
[{"Symbol":"shape","Text":"rectangular"},{"Symbol":"support","Text":"simply supported"},{"Symbol":"a","Number":5000,"Unit":"mm"},{"Symbol":"b","Number":500,"Unit":"mm"},{"Symbol":"t","Number":8,"Unit":"mm"},{"Symbol":"q","Number":100,"Unit":"kPa"},{"Symbol":"load_type","Text":"central patch"},{"Symbol":"u","Number":100,"Unit":"mm"},{"Symbol":"v","Number":500,"Unit":"mm"}]
```

## Notes for the tester
- Findings -03, -05, -06 and -07 have no step. They are Low or Info. Finding -07 asks you to check the clamped-plate coefficients in your copy of the book.
- Raise q until w/t passes 0.5 on any plate. The sheet must warn that the thin-plate result is unconservative. Try a span/t below 10 for the same warning.
