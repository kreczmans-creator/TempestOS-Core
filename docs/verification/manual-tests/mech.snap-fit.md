# Manual test — mech.snap-fit (Snap-fits: cantilever, annular, torsion)
Liability: Medium. Time: ~55 min. Pre-requisites: a project open in the desktop app. A pocket calculator for the hand checks. Step 11 also needs the calculator mech.tolerance-stack in the same project.

| # | Step | Inputs (value + unit) | Expected output (symbol = value unit) | Tolerance | Expected PASS/FAIL/BLOCK/WARN | Basis | Result | Notes |
|---|---|---|---|---|---|---|---|---|
| 1 | Open the calculator. Press Ctrl+K, type "Open worksheet: Snap-fits: cantilever, annular, torsion", press Enter. Or use Engineering tree, Modules, Calculations, Worksheets, Mechanical. Leave every input at its default. | Defaults: Cantilever, constant section; l 15; h 2; b 6; y 1 mm; alpha 30 deg; alpha_r 60 deg; One-time; Typed secant modulus; no mu; no E_s; no permissible strain | Header shows mech.snap-fit@1.2.0. No results. The bar reads Blocked. The messages ask for the coefficient of friction mu, then for the permissible strain and the secant modulus. None has a default. | Text match | BLOCK | Template rule (mu, Es and the permissible strain have no default); probe confirms the mu message |  | The sheet does not guess plastic data. Steps 2 to 8 type them. |
| 2 | Import Block K1 and read the results. | Block K1: l 15; h 2; b 6; y 1 mm; E_s 2400 MPa; eps_perm_once 0.04; mu 0.3; alpha 30; alpha_r 60 deg | K_y = 0.66667; eps = 1.3333 %; P = 8.5333 N; strain_util = 0.33333; y_perm = 3 mm; assembly_force = 9.0551 N; alpha_lock = 73.301 deg; retention_force = 36.096 N. Bar: PASS. | ±0.01 % | PASS | KAT ka-constant and independent script |  | Hand check: K_y = 2/3. eps = y h / (K_y l²) = 2 / (0.6667 x 225) = 1.3333 %. P = b h² E_s eps / (6 l phi_max) = 8.5333 N. arctan(1/0.3) = 73.30 deg. |
| 3 | Import Block K2 and read the results. Then import Block K3, then Block K4, and read the results each time. | K2: mode Cantilever, tapered thickness; h_ratio 0.5; else as K1. K3: tapered thickness; h_ratio 0.3; l 20; h 2.5; b 8; y 1.2; E_s 2200; 5 %; mu 0.25; 25 and 70 deg. K4: tapered width; b_ratio 0.25; l 12; h 1.5; b 8; y 0.8; E_s 3000; 3 %; mu 0.35; 35 and 45 deg | K2: K_y = 1.0904; phi_max = 1; eps = 0.81523 %; P = 5.2175 N; strain_util = 0.20381; y_perm = 4.9066 mm; assembly_force = 5.5365 N; retention_force = 22.07 N. K3: t_crit = 0.57143; phi_max = 1.1905; K_y = 1.2684; eps = 0.59128 %; P = 4.5528 N; y_perm = 10.148 mm; retention_force = 43.583 N. K4: K_y = 0.8552; eps = 0.97443 %; P = 7.3082 N; strain_util = 0.32481; y_perm = 2.463 mm; assembly_force = 10.167 N; retention_force = 15.179 N. | ±0.01 % | K2: PASS. K3: PASS. K4: PASS | KATs ka-taper-thickness, ka-taper-thickness-deep, ka-taper-width; independent script |  | Compare K_y with your printed guide: 1.09 for a tip thickness of h/2 and 0.86 for a tip width of b/4. The constant section uses 2/3, not 0.67. |
| 4 | Import Block K5, then Block K6, then Block K7. Read the results each time. | K5: mode Annular (closed ring); D_ring 30; dD 0.4; h 1.5; b 5 mm; K_ann 1.2; E_s 2400; 4 %; mu 0.3; 30 and 60 deg. K6: h 3; K_ann 1. K7: K5 with K_F 1.25 | K5: eps_nom = 1.3333 %; eps = 1.6 %; eps_inner = 1.4035 %; P = 1508 N; strain_util = 0.4; assembly_force = 1600.2 N; retention_force = 6378.8 N. K6: eps = 1.4815 %; strain_util = 0.37037; y_perm = 1.08 mm; P = 3015.9 N. K7: eps = 1.6 %; K_F = 1.25; P = 1885 N; strain_util = 0.4; assembly_force = 2000.2 N. | ±0.01 % | K5: PASS. K6: PASS. K7: PASS | KATs ka-annular, ka-annular-inner-surface, ka-annular-force-factor; independent script |  | Hand check K5: eps_nom = dD / D = 0.4 / 30 = 1.3333 %. eps = K_ann x eps_nom = 1.6 %. K6 shows the inner surface of a thick wall governing: 1.4815 %. |
| 5 | Import Block K8, then Block K9, then Block K10. Read the results each time. | K8: mode Torsion bar; l 20; r_bar 2; a_arm 10; y 1.5 mm; nu 0.38; E_s 2000; 3 %; mu 0.3; 30 and 60 deg. K9: bar_support Held at both ends. K10: constant section; y 1.5 mm; W3 secant curve of three points | K8: phi = 8.5944 deg; gamma = 0.015; eps = 1.087 %; gamma_perm = 4.14 %; strain_util = 0.36232; G_sec = 724.64 MPa; T_bar = 0.13659 N·m; P = 13.659 N; y_perm = 4.14 mm. K9: l_eff = 10 mm; gamma = 0.03; eps = 2.1739 %; strain_util = 0.72464; T_bar = 0.54636 N·m; P = 54.636 N; y_perm = 2.07 mm. K10: eps = 2 %; Es = 2100 MPa; P = 11.2 N; strain_util = 0.5. | ±0.01 % | K8: PASS. K9: PASS. K10: PASS | KATs ka-torsion, ka-torsion-both-ends, ka-curve; independent script |  | Hand check K8: the lever angle is 1.5 / 10 = 0.15 rad = 8.594 deg. The ends-held bar has twice the strain and four times the force. |
| 6 | Typical case, constant section. Import Block T1 and read the results. | Block T1: l 12; h 1.2; b 5; y 0.8 mm; E_s 1800 MPa; eps_perm_once 0.03; mu 0.4; alpha 25; alpha_r 70 deg | K_y = 0.66667; eps = 1 %; P = 1.8 N; strain_util = 0.33333; y_perm = 2.4 mm; assembly_force = 1.9169 N; alpha_lock = 68.199 deg; retention_force = "self-locking (infinite)". Bar: PASS. | ±0.01 % | PASS | Independent script (extra row, typical constant section) |  | The return angle 70 deg is above arctan(1/0.4) = 68.2 deg. So the joint cannot be pulled apart. A warning says so. |
| 7 | Typical case, a short beam. Import Block T2 and read the results and the warnings. | Block T2: l 4; h 1.5; b 5; y 0.2 mm; E_s 1800; 3 %; mu 0.4; 25 and 70 deg | l_h = 2.6667; K_y = 0.66667; eps = 2.8125 %; P = 23.73 N; strain_util = 0.9375; y_perm = 0.21333 mm; assembly_force = 25.272 N. Bar: PASS. A warning says that l/h is below 5 and beam theory is unreliable. | ±0.01 % | PASS | Independent script (extra row, slender l/h < 5) |  | A short thick beam also shears. The sheet only warns. |
| 8 | Boundary cases. Import Block L1 and read the results and the warning. Then import Block L2 and read them. | L1: Block K1 with alpha_r 75 deg. L2: Block K1 with mu 1 and alpha 45 deg | L1: assembly_force = 9.0551 N; alpha_lock = 73.301 deg; retention_force = "self-locking (infinite)"; a self-locking warning. L2: assembly_force = "jams (μ·tan α ≥ 1)"; an assembly-jam warning; the bar reads FAIL (u = Infinity). | Text match; ±0.01 % on numbers | L1: PASS with WARN. L2: FAIL (assembly jams) | Independent script (self-locking row); hand: arctan(1/0.3) = 73.30 deg; mu tan 45 deg = 1 |  | L1 is right for a permanent join and wrong for a joint you must open. The warning says which. |
| 9 | NEGATIVE: values and units that the sheet must refuse. Import Block K1, then change one input at a time: l 0.05 mm; h 0.005 mm; alpha 90 deg; mu 3; eps_perm_once 2; E_s 0. Then import `[{"Symbol":"l","Number":15,"Unit":"N"}]` and the same with unit `furlong`. | l 0.05 mm; h 0.005 mm; alpha 90 deg; mu 3; eps_perm_once 2; E_s 0 MPa; l = 15 N; l = 15 furlong | l: must be at least 0.1 mm. h: must be at least 0.01 mm; 0.005 mm was given. alpha: must be at most 89 deg; 90 deg was given. mu: must be at most 2; 3 was given. eps_perm_once: must be at most 1; 2 was given. E_s: must be at least 0.01 MPa. l in N: l must be entered in a unit of mm, not N. l in furlong: Unknown unit 'furlong'. No results in each case. | Text match | BLOCK | Template hard limits; probe confirms the block messages |  | Use eight separate changes. Suspected defect, not yet in a P1 report. The form may keep the number and fall back to millimetres with no message. Record FAIL in that case. |
| 10 | NEGATIVE: stale upstream. Follow Procedure L in README.md. UP is Tolerance stack-up with Block U1. DOWN is this calculator with Block K1. Link y to UP output x_mean. Then open UP, import Block U2 and press Save changes. Open DOWN. | UP: Block U1, then Block U2. DOWN: Block K1 with y linked to UP output x_mean | After the link: y = 0.61 mm (source link). eps = 0.81333 %; P = 5.2053 N; strain_util = 0.20333; y_perm = 3 mm. Bar: PASS. The DOWN row ends with STALE. The banner reads that UP now gives a different 'x_mean' than the value 'y' took. Record check is refused: Calculation 'DOWN' is stale. After Update linked inputs: y = 3.61 mm. eps = 4.8133 %; P = 30.805 N; strain_util = 1.2033; y_perm = 3 mm. Bar: FAIL (Strain at the critical section against the permissible strain u = 1.20333). | ±0.01 % on values | WARN (stale banner); BLOCK (check refused); then FAIL (Strain at the critical section against the permissible strain u = 1.20333) after the update | Independent script (snap function with the linked y); code rule (staleness) |  | Procedure L in README.md. The mean gap of the stack stands in for the undercut to be cleared. The longer plate makes the undercut larger than the beam can take. |
| 11 | Known defect P1-mech.snap-fit-01 (Medium). Import Block D1 and read eps, strain_util, P and the warnings. | Block D1: l 20; h 1.2; b 6; y 8.2 mm; E_s 1800 MPa; eps_perm_once 0.04; mu 0.3; alpha 30; alpha_r 60 deg | Product: eps = 3.69 %; strain_util = 0.9225; P = 4.7822 N; no warning on the large deflection. Correct: the exact large-deflection solution gives eps = 4 %, strain_util = 1 and P = 5.81 N. The sheet should warn when y/l is above 0.3. | ±2 % | FAIL | Independent script (elastica, tip load of fixed direction; mech.snap-fit.py elastica_ratios) |  | Known defect P1-mech.snap-fit-01; expect FAIL until fixed. The product is low by 8.5 % in strain and 22 % in force. At y/l 0.3 the errors are 4 % and 10 %. |
| 12 | Known defect P1-mech.snap-fit-03 (Low). Import Block K1 and set alpha_r to 63.43 deg. Then set mu to 0.40, 0.45, 0.48 and 0.50 in turn. Read retention_force each time. | Block K1 with alpha_r 63.43 deg; mu 0.40, 0.45, 0.48, 0.50 (Block D2 is the last one) | Product: retention_force = 0.40: 102.29 N; 0.45: 208.62 N; 0.48: 526.25 N; 0.50: 98796 N. There is no warning before the locking angle (alpha_lock = 63.435 deg). Correct: a warning when 1 - mu tan(alpha_r) is below 0.1, because the force grows without limit. | ±0.01 % | FAIL | Independent script (retention force P (mu + tan alpha_r) / (1 - mu tan alpha_r)); P1 report |  | Known defect P1-mech.snap-fit-03; expect FAIL until fixed. A 0.005 deg change near the lock changes the force by a factor of 100. Hand check at mu 0.40: 8.533 x (0.4 + 1.9996) / (1 - 0.7998) = 102.3 N. |
| 13 | Known defect P1-mech.snap-fit-02 (Low). Import Block K8 (step 5). Read the result named eps and its label. | Block K8 (step 5) | Product: the output eps is labelled as the maximum strain and reads 1.087 %. The surface shear strain is gamma = 1.5 %. Correct: the label reads equivalent tensile strain, gamma / (1 + nu) = 1.5 / 1.38 = 1.087 %. | Text match | FAIL | Independent script (ka-torsion: eps = gamma / (1 + nu)); P1 report |  | Known defect P1-mech.snap-fit-02; expect FAIL until fixed. The number is right. The label is not. Findings -04 and -05 have no step. |

## Import blocks
Copy the whole line of a block to the clipboard. Then press *Import inputs (CSV/JSON)*. The status line must read "Imported n input(s)".

**Block K1** — Constant section l 15, h 2, b 6 mm, deflection 1 mm, Es 2400 MPa, permissible strain 4 %, friction 0.3, entry 30 deg, return 60 deg. Known-answer case ka-constant.
```json
[{"Symbol":"mode","Text":"Cantilever, constant section"},{"Symbol":"l","Number":15,"Unit":"mm"},{"Symbol":"h","Number":2,"Unit":"mm"},{"Symbol":"b","Number":6,"Unit":"mm"},{"Symbol":"y","Number":1,"Unit":"mm"},{"Symbol":"Es","Number":2400,"Unit":"MPa"},{"Symbol":"eps_perm_once","Number":0.04},{"Symbol":"mu","Number":0.3},{"Symbol":"alpha","Number":30,"Unit":"deg"},{"Symbol":"alpha_r","Number":60,"Unit":"deg"}]
```

**Block K2** — Block K1 with a tip thickness of h/2. Known-answer case ka-taper-thickness.
```json
[{"Symbol":"mode","Text":"Cantilever, tapered thickness"},{"Symbol":"h_ratio","Number":0.5},{"Symbol":"l","Number":15,"Unit":"mm"},{"Symbol":"h","Number":2,"Unit":"mm"},{"Symbol":"b","Number":6,"Unit":"mm"},{"Symbol":"y","Number":1,"Unit":"mm"},{"Symbol":"Es","Number":2400,"Unit":"MPa"},{"Symbol":"eps_perm_once","Number":0.04},{"Symbol":"mu","Number":0.3},{"Symbol":"alpha","Number":30,"Unit":"deg"},{"Symbol":"alpha_r","Number":60,"Unit":"deg"}]
```

**Block K3** — Tapered thickness, ratio 0.3, l 20, h 2.5, b 8 mm, y 1.2 mm, Es 2200, 5 % strain, mu 0.25, 25 and 70 deg. Known-answer case ka-taper-thickness-deep.
```json
[{"Symbol":"mode","Text":"Cantilever, tapered thickness"},{"Symbol":"h_ratio","Number":0.3},{"Symbol":"l","Number":20,"Unit":"mm"},{"Symbol":"h","Number":2.5,"Unit":"mm"},{"Symbol":"b","Number":8,"Unit":"mm"},{"Symbol":"y","Number":1.2,"Unit":"mm"},{"Symbol":"Es","Number":2200,"Unit":"MPa"},{"Symbol":"eps_perm_once","Number":0.05},{"Symbol":"mu","Number":0.25},{"Symbol":"alpha","Number":25,"Unit":"deg"},{"Symbol":"alpha_r","Number":70,"Unit":"deg"}]
```

**Block K4** — Tapered width, ratio 0.25, l 12, h 1.5, b 8 mm, y 0.8 mm, Es 3000, 3 % strain, mu 0.35, 35 and 45 deg. Known-answer case ka-taper-width.
```json
[{"Symbol":"mode","Text":"Cantilever, tapered width"},{"Symbol":"b_ratio","Number":0.25},{"Symbol":"l","Number":12,"Unit":"mm"},{"Symbol":"h","Number":1.5,"Unit":"mm"},{"Symbol":"b","Number":8,"Unit":"mm"},{"Symbol":"y","Number":0.8,"Unit":"mm"},{"Symbol":"Es","Number":3000,"Unit":"MPa"},{"Symbol":"eps_perm_once","Number":0.03},{"Symbol":"mu","Number":0.35},{"Symbol":"alpha","Number":35,"Unit":"deg"},{"Symbol":"alpha_r","Number":45,"Unit":"deg"}]
```

**Block K5** — Annular snap-fit: ring D 30 mm, interference 0.4 mm, wall h 1.5 mm, length b 5 mm, K_ann 1.2, Es 2400, 4 %. Known-answer case ka-annular.
```json
[{"Symbol":"mode","Text":"Annular (closed ring)"},{"Symbol":"b","Number":5.0,"Unit":"mm"},{"Symbol":"D_ring","Number":30.0,"Unit":"mm"},{"Symbol":"dD","Number":0.4,"Unit":"mm"},{"Symbol":"Es","Number":2400,"Unit":"MPa"},{"Symbol":"eps_perm_once","Number":0.04},{"Symbol":"mu","Number":0.3},{"Symbol":"alpha","Number":30,"Unit":"deg"},{"Symbol":"alpha_r","Number":60,"Unit":"deg"},{"Symbol":"h","Number":1.5,"Unit":"mm"},{"Symbol":"K_ann","Number":1.2}]
```

**Block K6** — Annular, wall h 3 mm, K_ann 1.0. Known-answer case ka-annular-inner-surface.
```json
[{"Symbol":"mode","Text":"Annular (closed ring)"},{"Symbol":"b","Number":5.0,"Unit":"mm"},{"Symbol":"D_ring","Number":30.0,"Unit":"mm"},{"Symbol":"dD","Number":0.4,"Unit":"mm"},{"Symbol":"Es","Number":2400,"Unit":"MPa"},{"Symbol":"eps_perm_once","Number":0.04},{"Symbol":"mu","Number":0.3},{"Symbol":"alpha","Number":30,"Unit":"deg"},{"Symbol":"alpha_r","Number":60,"Unit":"deg"},{"Symbol":"h","Number":3.0,"Unit":"mm"},{"Symbol":"K_ann","Number":1.0}]
```

**Block K7** — Block K5 with the force factor K_F 1.25. Known-answer case ka-annular-force-factor.
```json
[{"Symbol":"mode","Text":"Annular (closed ring)"},{"Symbol":"b","Number":5.0,"Unit":"mm"},{"Symbol":"D_ring","Number":30.0,"Unit":"mm"},{"Symbol":"dD","Number":0.4,"Unit":"mm"},{"Symbol":"Es","Number":2400,"Unit":"MPa"},{"Symbol":"eps_perm_once","Number":0.04},{"Symbol":"mu","Number":0.3},{"Symbol":"alpha","Number":30,"Unit":"deg"},{"Symbol":"alpha_r","Number":60,"Unit":"deg"},{"Symbol":"h","Number":1.5,"Unit":"mm"},{"Symbol":"K_ann","Number":1.2},{"Symbol":"K_F","Number":1.25}]
```

**Block K8** — Torsion bar held at one end: free length 20 mm, radius 2 mm, lever 10 mm, deflection 1.5 mm, nu 0.38, Es 2000, 3 %. Known-answer case ka-torsion.
```json
[{"Symbol":"mode","Text":"Torsion bar"},{"Symbol":"l","Number":20.0,"Unit":"mm"},{"Symbol":"r_bar","Number":2.0,"Unit":"mm"},{"Symbol":"a_arm","Number":10.0,"Unit":"mm"},{"Symbol":"y","Number":1.5,"Unit":"mm"},{"Symbol":"nu","Number":0.38},{"Symbol":"Es","Number":2000,"Unit":"MPa"},{"Symbol":"eps_perm_once","Number":0.03},{"Symbol":"mu","Number":0.3},{"Symbol":"alpha","Number":30,"Unit":"deg"},{"Symbol":"alpha_r","Number":60,"Unit":"deg"}]
```

**Block K9** — Block K8 held at both ends with the lever in the middle. Known-answer case ka-torsion-both-ends.
```json
[{"Symbol":"mode","Text":"Torsion bar"},{"Symbol":"l","Number":20.0,"Unit":"mm"},{"Symbol":"r_bar","Number":2.0,"Unit":"mm"},{"Symbol":"a_arm","Number":10.0,"Unit":"mm"},{"Symbol":"y","Number":1.5,"Unit":"mm"},{"Symbol":"nu","Number":0.38},{"Symbol":"Es","Number":2000,"Unit":"MPa"},{"Symbol":"eps_perm_once","Number":0.03},{"Symbol":"mu","Number":0.3},{"Symbol":"alpha","Number":30,"Unit":"deg"},{"Symbol":"alpha_r","Number":60,"Unit":"deg"},{"Symbol":"bar_support","Text":"Held at both ends, lever in the middle"}]
```

**Block K10** — Constant section with a secant-modulus curve of three points (0.5 %: 2600; 1 %: 2400; 3 %: 1800 MPa), y 1.5 mm. Known-answer case ka-curve.
```json
[{"Symbol":"mode","Text":"Cantilever, constant section"},{"Symbol":"l","Number":15,"Unit":"mm"},{"Symbol":"h","Number":2,"Unit":"mm"},{"Symbol":"b","Number":6,"Unit":"mm"},{"Symbol":"y","Number":1.5,"Unit":"mm"},{"Symbol":"Es_source","Text":"W3 secant curve Es(ε)"},{"Symbol":"Es_pts","Rows":[{"Cells":{"eps":{"Number":0.005},"Es":{"Number":2600,"Unit":"MPa"}}},{"Cells":{"eps":{"Number":0.01},"Es":{"Number":2400,"Unit":"MPa"}}},{"Cells":{"eps":{"Number":0.03},"Es":{"Number":1800,"Unit":"MPa"}}}]},{"Symbol":"eps_perm_once","Number":0.04},{"Symbol":"mu","Number":0.3},{"Symbol":"alpha","Number":30,"Unit":"deg"},{"Symbol":"alpha_r","Number":60,"Unit":"deg"}]
```

**Block T1** — Constant section l 12, h 1.2, b 5 mm, deflection 0.8 mm, Es 1800, 3 %, mu 0.4, 25 and 70 deg.
```json
[{"Symbol":"mode","Text":"Cantilever, constant section"},{"Symbol":"l","Number":12,"Unit":"mm"},{"Symbol":"h","Number":1.2,"Unit":"mm"},{"Symbol":"b","Number":5,"Unit":"mm"},{"Symbol":"y","Number":0.8,"Unit":"mm"},{"Symbol":"Es","Number":1800,"Unit":"MPa"},{"Symbol":"eps_perm_once","Number":0.03},{"Symbol":"mu","Number":0.4},{"Symbol":"alpha","Number":25,"Unit":"deg"},{"Symbol":"alpha_r","Number":70,"Unit":"deg"}]
```

**Block T2** — Short beam: l 4 mm, h 1.5 mm, l/h below 5.
```json
[{"Symbol":"mode","Text":"Cantilever, constant section"},{"Symbol":"l","Number":4,"Unit":"mm"},{"Symbol":"h","Number":1.5,"Unit":"mm"},{"Symbol":"b","Number":5,"Unit":"mm"},{"Symbol":"y","Number":0.2,"Unit":"mm"},{"Symbol":"Es","Number":1800,"Unit":"MPa"},{"Symbol":"eps_perm_once","Number":0.03},{"Symbol":"mu","Number":0.4},{"Symbol":"alpha","Number":25,"Unit":"deg"},{"Symbol":"alpha_r","Number":70,"Unit":"deg"}]
```

**Block L1** — Block K1 with return angle 75 deg: above the locking angle 73.3 deg.
```json
[{"Symbol":"mode","Text":"Cantilever, constant section"},{"Symbol":"l","Number":15,"Unit":"mm"},{"Symbol":"h","Number":2,"Unit":"mm"},{"Symbol":"b","Number":6,"Unit":"mm"},{"Symbol":"y","Number":1,"Unit":"mm"},{"Symbol":"Es","Number":2400,"Unit":"MPa"},{"Symbol":"eps_perm_once","Number":0.04},{"Symbol":"mu","Number":0.3},{"Symbol":"alpha","Number":30,"Unit":"deg"},{"Symbol":"alpha_r","Number":75,"Unit":"deg"}]
```

**Block L2** — Block K1 with mu 1 and entry angle 45 deg: the entry face jams.
```json
[{"Symbol":"mode","Text":"Cantilever, constant section"},{"Symbol":"l","Number":15,"Unit":"mm"},{"Symbol":"h","Number":2,"Unit":"mm"},{"Symbol":"b","Number":6,"Unit":"mm"},{"Symbol":"y","Number":1,"Unit":"mm"},{"Symbol":"Es","Number":2400,"Unit":"MPa"},{"Symbol":"eps_perm_once","Number":0.04},{"Symbol":"mu","Number":1},{"Symbol":"alpha","Number":45,"Unit":"deg"},{"Symbol":"alpha_r","Number":60,"Unit":"deg"}]
```

**Block U1** — UP for step 10: the housing stack of the tolerance-stack calculator (minimum 0.40, maximum 0.80 mm).
```json
[{"Symbol":"contributors","Rows":[{"Cells":{"name":{"Text":"housing"},"direction":{"Text":"adds"},"nominal":{"Number":40,"Unit":"mm"},"upper":{"Number":0.1,"Unit":"mm"},"lower":{"Number":0,"Unit":"mm"}}},{"Cells":{"name":{"Text":"bearing A"},"direction":{"Text":"subtracts"},"nominal":{"Number":20,"Unit":"mm"},"upper":{"Number":0,"Unit":"mm"},"lower":{"Number":-0.12,"Unit":"mm"}}},{"Cells":{"name":{"Text":"spacer"},"direction":{"Text":"subtracts"},"nominal":{"Number":19.5,"Unit":"mm"},"upper":{"Number":0.02,"Unit":"mm"},"lower":{"Number":-0.02,"Unit":"mm"}}}]},{"Symbol":"minimum","Number":0.4,"Unit":"mm"},{"Symbol":"maximum","Number":0.8,"Unit":"mm"}]
```

**Block U2** — Block U1 with the first contributor 3 mm longer.
```json
[{"Symbol":"contributors","Rows":[{"Cells":{"name":{"Text":"housing"},"direction":{"Text":"adds"},"nominal":{"Number":43,"Unit":"mm"},"upper":{"Number":0.1,"Unit":"mm"},"lower":{"Number":0,"Unit":"mm"}}},{"Cells":{"name":{"Text":"bearing A"},"direction":{"Text":"subtracts"},"nominal":{"Number":20,"Unit":"mm"},"upper":{"Number":0,"Unit":"mm"},"lower":{"Number":-0.12,"Unit":"mm"}}},{"Cells":{"name":{"Text":"spacer"},"direction":{"Text":"subtracts"},"nominal":{"Number":19.5,"Unit":"mm"},"upper":{"Number":0.02,"Unit":"mm"},"lower":{"Number":-0.02,"Unit":"mm"}}}]},{"Symbol":"minimum","Number":0.4,"Unit":"mm"},{"Symbol":"maximum","Number":0.8,"Unit":"mm"}]
```

**Block D1** — Constant section l 20, h 1.2, b 6 mm, tip deflection 8.2 mm (y/l = 0.41), Es 1800, 4 %, mu 0.3.
```json
[{"Symbol":"mode","Text":"Cantilever, constant section"},{"Symbol":"l","Number":20,"Unit":"mm"},{"Symbol":"h","Number":1.2,"Unit":"mm"},{"Symbol":"b","Number":6,"Unit":"mm"},{"Symbol":"y","Number":8.2,"Unit":"mm"},{"Symbol":"Es","Number":1800,"Unit":"MPa"},{"Symbol":"eps_perm_once","Number":0.04},{"Symbol":"mu","Number":0.3},{"Symbol":"alpha","Number":30,"Unit":"deg"},{"Symbol":"alpha_r","Number":60,"Unit":"deg"}]
```

**Block D2** — Block K1 with the return angle 63.43 deg and mu 0.50.
```json
[{"Symbol":"mode","Text":"Cantilever, constant section"},{"Symbol":"l","Number":15,"Unit":"mm"},{"Symbol":"h","Number":2,"Unit":"mm"},{"Symbol":"b","Number":6,"Unit":"mm"},{"Symbol":"y","Number":1,"Unit":"mm"},{"Symbol":"Es","Number":2400,"Unit":"MPa"},{"Symbol":"eps_perm_once","Number":0.04},{"Symbol":"mu","Number":0.5},{"Symbol":"alpha","Number":30,"Unit":"deg"},{"Symbol":"alpha_r","Number":63.43,"Unit":"deg"}]
```

## Notes for the tester
- Compare your plastic's permissible strain and secant modulus with the typed values. The values here are test numbers, not data for any plastic.
- Compare the sheet with a Covestro or BASF snap-fit design guide for the same geometry. See oracles.md.
