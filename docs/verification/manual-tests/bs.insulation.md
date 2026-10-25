# Manual test — bs.insulation (Insulation thickness (ISO 12241 formula route))
Liability: Medium. Time: ~55 min. Pre-requisites: a project open in the desktop app. A pocket calculator for the hand checks. Step 10 also needs the calculator bs.psychrometrics in the same project.

| # | Step | Inputs (value + unit) | Expected output (symbol = value unit) | Tolerance | Expected PASS/FAIL/BLOCK/WARN | Basis | Result | Notes |
|---|---|---|---|---|---|---|---|---|
| 1 | Open the calculator. Press Ctrl+K, type "Open worksheet: Insulation thickness", press Enter. Or use Engineering tree, Modules, Calculations, Worksheets, Building Services. Leave every input at its default. | Defaults: pipe (cylinder); D_i 100 mm; 80 degC fluid; 20 degC air; lambda 0.04 W/(m*K); h_se as given, 10 W/(m²*K); q_max_m 25 W/m; RH_a 60 %; t_chosen 40 mm | Header shows bs.insulation@1.1.0. thickness = 37.304 mm; t_q = 37.304 mm; R = 2.5156 m·K/W; theta_m = 52.109 degC; theta_se = 24.218 degC; q_per_m = 23.851 W/m; u_q = 0.95406; D_crit = 8 mm. Bar: PASS. | Shown digits | PASS | independent script (tools/verification/bs.insulation.py); hand: R = ln(Do / Di) / (2 pi lambda) + 1 / (h pi Do) |  | The form opens with a 40 mm chosen thickness and a 25 W/m limit. Clear t_chosen to see the required thickness alone, 37.304 mm. Steps 2 to 4 tell you when to clear it. |
| 2 | Import Block K1 and read the results. Then import Block K2, clear t_chosen, and read the results. Then import Block K3, clear t_chosen, and read them. | K1: t_chosen 30 mm; q_max_m 30 W/m; theta_se_max 45 degC. K2: q_max_m 25 W/m; t_chosen blank. K3: q_max_m 25 W/m; theta_se_max 45 degC; t_chosen blank | K1: R = 2.069 m·K/W; q_per_m = 28.999 W/m; theta_se = 25.769 degC; u_q = 0.96664 (and u_se). K2: thickness = 37.304 mm; t_q = 37.304 mm; theta_se = 24.558 degC. K3: t_se = 5.3259 mm; t_q = 37.304 mm; thickness = 37.304 mm. | ±0.01 % | K1: PASS. K2: PASS. K3: PASS | KATs ka-pipe-chosen, ka-pipe-heat-loss, ka-pipe-surface; independent script |  | Hand check K1: R = ln(160 / 100) / (2 pi x 0.04) + 1 / (10 x pi x 0.16) = 1.8701 + 0.1989 = 2.0690 K*m/W. q = 60 / 2.0690 = 29.0 W/m. |
| 3 | Import Block K4, clear t_chosen, and read the results. Then import Block K5 and Block K6 in turn, clear t_chosen and q_max_m each time, and read the results. | K4: flat surface; q_max_A 20 W/m². K5: flat surface; theta_i 5; theta_a 25 degC; RH_a 70 %; dew_margin 1 K. K6: pipe with the same temperatures, humidity and margin | K4: thickness = 116 mm; theta_se = 22 degC; q_area = 20 W/m². K5: theta_dew = 19.145 degC; thickness = 12.479 mm. K6: theta_dew = 19.145 degC; thickness = 11.291 mm; t_dew = 11.291 mm. | ±0.01 % | K4: PASS. K5: PASS. K6: PASS | KATs ka-flat-closed-form, ka-flat-condensation, ka-pipe-condensation; independent script |  | Hand check K4: t / lambda + 1 / h = 60 / 20 = 3, so t / lambda = 2.9 and t = 0.04 x 2.9 = 116 mm. The surface sits at 20 + 20 / 10 = 22 degC. The dew point of 25 degC at 70 % is 19.145 degC. |
| 4 | Import Block K7 and read the results. Then import Block K8 and read them. Then import Block K9, clear t_chosen, and read the results and the warnings. | K7: h_i 500; t_w 3 mm; lambda_w 50; q_max_m 30; t_chosen 30. K8: D_i 114; theta_i 120; theta_a 15; correlation; eps 0.1; v_wind 0; q_max_m 60; theta_se_max 60; t_chosen 50. K9: D_i 10 mm; lambda 0.2; q_max_m 25; t_chosen blank | K7: R = 2.076 m·K/W; R_oth = 0.0069695 m·K/W; q_per_m = 28.902 W/m; theta_se = 25.75 degC; u_q = 0.96339. K8: h_c = 3.6748 W/(m²·K); h_r = 0.58007 W/(m²·K); h_se = 4.2549 W/(m²·K); R = 2.8554 m·K/W; q_per_m = 36.773 W/m; theta_se = 27.855 degC; u_q = 0.61288; u_se = 0.28567. K9: thickness = 74.292 mm; D_crit = 40 mm and the bare-complies and critical-radius warnings. | ±0.01 % | K7: PASS. K8: PASS. K9: PASS | KATs ka-wall-and-film, ka-correlation-still, ka-bare-complies-window; independent script |  | K8 uses the correlation, so the sheet is indicative. Compare h_c and h_r with your own convection data. K9 shows that a small hot pipe can lose more heat with a thin layer than bare: D_crit = 2 lambda / h_se = 40 mm. |
| 5 | Typical cases, a hot pipe and a chilled pipe. Import Block T1 and read the results. Then import Block T2, clear t_chosen, and read them. | T1: D_i 168.3; theta_i 150; theta_a 10; lambda 0.035; h_se 8; q_max_m 40; theta_se_max 50; t_chosen 60. T2: D_i 22; theta_i 6; theta_a 28; lambda 0.035; h_se 8; RH_a 80; dew_margin 0; q_max_m 10; t_chosen blank | T1: thickness = 93.109 mm; t_q = 93.109 mm; t_se = 10.328 mm; R = 2.5856 m·K/W; q_per_m = 54.146 W/m; theta_se = 17.473 degC; u_q = 1.3536; u_se = 0.18682. Bar: FAIL (Chosen thickness against the heat-loss limit u = 1.35365). T2: thickness = 14.316 mm; t_q = 1.6151 mm; t_dew = 14.316 mm; theta_dew = 24.222 degC; theta_se = 24.222 degC; q_per_m = 4.8075 W/m. | ±0.01 % | T1: FAIL (Chosen thickness against the heat-loss limit u = 1.35365). T2: PASS | Independent script (extra rows Y1 and Y2) |  | In T1 the heat-loss limit governs: 93.1 mm against 10.3 mm for the surface limit. In T2 the dew point governs: 14.3 mm against 1.6 mm for the heat gain. |
| 6 | Typical cases with the correlation. Import Block T3, clear t_chosen, and read the results. Then import Block T4, clear t_chosen, and read them. | T3: flat surface; theta_i 70; theta_a 5; correlation; eps 0.3; v_wind 1; L_surf 1.5; q_max_A 40; theta_se_max 35. T4: pipe D_i 60.3; theta_i 200; theta_a 0; lambda 0.045; correlation; eps 0.9; v_wind 8; q_max_m 50; theta_se_max 55 | T3: thickness = 57.315 mm; t_q = 57.315 mm; t_se = 7.3404 mm; h_c = 3.6792 W/(m²·K); h_r = 1.5261 W/(m²·K); h_se = 5.2053 W/(m²·K); theta_se = 12.685 degC. T4: thickness = 61.956 mm; t_q = 61.956 mm; t_se = 2.3553 mm; h_c = 30.15 W/(m²·K); h_r = 4.218 W/(m²·K); h_se = 34.368 W/(m²·K); theta_se = 2.5139 degC; D_crit = 1.6689 mm. | ±0.01 % | T3: PASS. T4: PASS | Independent script (extra rows Y3 and Y8) |  | The surface coefficient depends on the surface temperature, so the sheet iterates. The correlation is indicative. Prefer a recorded h_se for design. |
| 7 | Boundary cases. Import Block L1, then Block L2, then Block L3. Clear t_chosen when the block says so. Read the results each time. | L1: theta_i 20.05; theta_a 20; q_max_m 25. L2: flat; theta_i 150; theta_a -20; correlation; eps 0.1; v_wind 40; L_surf 3; q_max_A 25; t_chosen 100. L3: D_i 300; q_max_m 1000 | L1: thickness = 0 mm; theta_se = 20.05 degC; q_per_m = 0.15708 W/m; D_crit = 8 mm. L2: thickness = 271.58 mm; h_c = 95.424 W/(m²·K); h_r = 0.36951 W/(m²·K); h_se = 95.794 W/(m²·K); theta_se = -19.293 degC; q_area = 67.717 W/m²; u_q = 2.7087 and a wind-speed warning. L3: thickness = 0 mm; q_per_m = 565.49 W/m; theta_se = 80 degC. | ±0.01 % | L1: PASS. L2: FAIL (Chosen thickness against the heat-loss limit u = 2.70869). L3: PASS | Independent script (extra rows Y5, Y11, Y10) |  | Required thickness is zero in L1 and L3. The bare surface already meets the limit. Read t_q = 0. |
| 8 | Unit conversion. Import Block U and read the results. | Block U: D_i 6.625 in; theta_i 300 degF; theta_a 70 degF; lambda 0.036; q_max_m 30 W/m; t_chosen 2 in | thickness = 132.73 mm; R = 2.2062 m·K/W; theta_se = 27.942 degC; q_per_m = 57.917 W/m; u_q = 1.9306; D_crit = 7.2 mm. Bar: FAIL (Chosen thickness against the heat-loss limit u = 1.93056). | ±0.01 % | FAIL (Chosen thickness against the heat-loss limit u = 1.93056) | Independent script (extra row Y9, imperial units converted) |  | Hand check: 6.625 in = 168.275 mm. 300 degF = 148.89 degC. 70 degF = 21.11 degC. 2 in = 50.8 mm. |
| 9 | NEGATIVE: values and units that the sheet must refuse. Import each line on its own: D_i 4 mm; lambda 0.003; RH_a 0; theta_i 700 degC; dew_margin 25 K; t_chosen 1001 mm; D_i in K; D_i in furlong. Reset after each. | D_i 4 mm; lambda 0.003 W/(m*K); RH_a 0 %; theta_i 700 degC; dew_margin 25 K; t_chosen 1001 mm; D_i = 100 K; D_i = 100 furlong | D_i: must be at least 5 mm; 4 mm was given. lambda: must be at least 0.005 W/(m*K); 0.003 was given. RH_a: must be at least 1 %; 0 % was given. theta_i: must be at most 650 degC; 700 degC was given. dew_margin: must be at most 20 K; 25 K was given. t_chosen: must be at most 1000 mm; 1001 mm was given. D_i in K: D_i must be entered in a unit of mm, not K. D_i in furlong: Unknown unit 'furlong'. No results in each case. | Text match | BLOCK | Template hard limits; probe confirms the block messages |  | Use eight separate imports. The unit refusals come from the import. Suspected defect, not yet in a P1 report. The form may keep the number and fall back to millimetres with no message. Record FAIL in that case. |
| 10 | NEGATIVE: stale upstream. Follow Procedure L in README.md. UP is Psychrometrics with dry-bulb 25 degC and relative humidity 70 %. DOWN is this calculator with Block S, t_chosen cleared. Link theta_dew to UP output t_dp_1. Then open UP, change the relative humidity from 70 to 80 % and press Save changes. Open DOWN. | UP: RH 70 %, then 80 %. DOWN: Block S with theta_dew linked to UP output t_dp_1 | After the link: theta_dew = 19.15 degC (source link). thickness = 12.495 mm; t_dew = 12.495 mm; theta_se = 20.15 degC. The DOWN row ends with STALE. The banner reads that UP now gives a different 't_dp_1' than the value 'theta_dew' took. Record check is refused: Calculation 'DOWN' is stale. After Update linked inputs: theta_dew = 21.309 degC. thickness = 25.733 mm; t_dew = 25.733 mm; theta_se = 22.309 degC. | ±0.01 % on values | WARN (stale banner); BLOCK (check refused); then PASS after the update | Independent script (ins function with the linked dew point); code rule (staleness) |  | Procedure L in README.md. The spec names this link: the dew point comes from the psychrometrics sheet. The psychrometrics dew point differs from the Magnus value in the KAT by a few thousandths of a kelvin. |
| 11 | Known defect P1-bs.insulation-01 (Medium). Import Block D1, clear t_chosen, and read theta_dew and the thickness. Then import Block D2, clear t_chosen, and read them again. | Block D1: flat; theta_i -30; theta_a -10 degC; RH_a 90 %; dew_margin 0. Block D2: theta_dew -10.10045758 degC instead of RH_a | Product D1: theta_dew = -11.179 degC; thickness = 63.866 mm, with no warning. D2: theta_dew = -10.1 degC; thickness = 792.36 mm. Correct: weather and instrument humidity is relative to water. So 90 % at -10 degC gives the frost point -10.10 degC and a thickness near 792 mm. The sheet warns when RH_a is used below 0 degC. | ±0.1 % | FAIL | Independent script (probes Y13 and Y14); the Magnus form over ice is the sheet's reading, so a user who enters a weather-station RH gets the wrong dew point |  | Known defect P1-bs.insulation-01; expect FAIL until fixed. The error is unconservative: the thickness is 12 times too small. The assumption text names the ice reading. No runtime warning appears. |
| 12 | Known defect P1-bs.insulation-02 (Low). Import Block D3 and read h_c. Then change D_i to 30 mm and theta_i to 24 degC and read h_c again. | Block D3: pipe D_i 100 mm; theta_i 45; theta_a 20 degC; correlation; eps 0.9; v_wind 0; t_chosen 0 mm. Then D_i 30 mm and theta_i 24 degC | Product: h_c = 5.2488 W/(m²·K) for the 100 mm pipe and 4.4855 W/(m²·K) for the 30 mm pipe. The Churchill and Chu relation gives 4.8 W/(m²*K) and 3.83 W/(m²*K). Correct: h_c at or below those values. Or a warning says the sheet takes the larger of two free-convection forms and is high for small pipes. | ±1 % | FAIL | Independent script (Churchill and Chu cylinder correlation with the sheet's air properties); P1 report |  | Known defect P1-bs.insulation-02; expect FAIL until fixed. The error is unconservative: a high h_c lowers the surface temperature. The sheet is labelled indicative. Use a recorded h_se for design. |

## Import blocks
Copy the whole line of a block to the clipboard. Then press *Import inputs (CSV/JSON)*. The status line must read "Imported n input(s)".

**Block K1** — Default pipe with 30 mm chosen, heat-loss limit 30 W/m and surface limit 45 degC. Known-answer case ka-pipe-chosen.
```json
[{"Symbol":"geometry","Text":"pipe (cylinder)"},{"Symbol":"t_chosen","Number":30.0,"Unit":"mm"},{"Symbol":"q_max_m","Number":30.0,"Unit":"W/m"},{"Symbol":"theta_se_max","Number":45.0,"Unit":"degC"}]
```

**Block K2** — Default pipe with a heat-loss limit 25 W/m and no chosen thickness. Known-answer case ka-pipe-heat-loss. Then clear the box t_chosen.
```json
[{"Symbol":"geometry","Text":"pipe (cylinder)"},{"Symbol":"q_max_m","Number":25.0,"Unit":"W/m"}]
```

**Block K3** — Block K2 with a surface limit of 45 degC. Known-answer case ka-pipe-surface. Then clear the box t_chosen.
```json
[{"Symbol":"geometry","Text":"pipe (cylinder)"},{"Symbol":"q_max_m","Number":25.0,"Unit":"W/m"},{"Symbol":"theta_se_max","Number":45.0,"Unit":"degC"}]
```

**Block K4** — Flat surface, 60 K difference, heat-loss limit 20 W/m². Known-answer case ka-flat-closed-form. Then clear the box t_chosen.
```json
[{"Symbol":"geometry","Text":"flat surface"},{"Symbol":"q_max_A","Number":20.0,"Unit":"W/m^2"}]
```

**Block K5** — Cold flat surface at 5 degC in 25 degC air, RH 70 %, margin 1 K. Known-answer case ka-flat-condensation. Then clear the box t_chosen.
```json
[{"Symbol":"geometry","Text":"flat surface"},{"Symbol":"theta_i","Number":5.0,"Unit":"degC"},{"Symbol":"theta_a","Number":25.0,"Unit":"degC"},{"Symbol":"RH_a","Number":70.0,"Unit":"%"},{"Symbol":"dew_margin","Number":1.0,"Unit":"K"}]
```

**Block K6** — The same conditions on the 100 mm pipe. Known-answer case ka-pipe-condensation. Then clear the boxes t_chosen and q_max_m.
```json
[{"Symbol":"geometry","Text":"pipe (cylinder)"},{"Symbol":"theta_i","Number":5.0,"Unit":"degC"},{"Symbol":"theta_a","Number":25.0,"Unit":"degC"},{"Symbol":"RH_a","Number":70.0,"Unit":"%"},{"Symbol":"dew_margin","Number":1.0,"Unit":"K"}]
```

**Block K7** — Default pipe with an inner film h_i 500 W/(m²*K) and a 3 mm steel wall (50 W/(m*K)), 30 mm chosen, limit 30 W/m. Known-answer case ka-wall-and-film.
```json
[{"Symbol":"geometry","Text":"pipe (cylinder)"},{"Symbol":"h_i","Number":500.0,"Unit":"W/(m^2*K)"},{"Symbol":"t_w","Number":3.0,"Unit":"mm"},{"Symbol":"lambda_w","Number":50.0,"Unit":"W/(m*K)"},{"Symbol":"q_max_m","Number":30.0,"Unit":"W/m"},{"Symbol":"t_chosen","Number":30.0,"Unit":"mm"}]
```

**Block K8** — A 114 mm pipe at 120 degC in 15 degC still air, radiation and convection from a correlation, emissivity 0.1, 50 mm chosen. Known-answer case ka-correlation-still.
```json
[{"Symbol":"geometry","Text":"pipe (cylinder)"},{"Symbol":"D_i","Number":114.0,"Unit":"mm"},{"Symbol":"theta_i","Number":120.0,"Unit":"degC"},{"Symbol":"theta_a","Number":15.0,"Unit":"degC"},{"Symbol":"h_mode","Text":"convection + radiation (correlation)"},{"Symbol":"eps","Number":0.1},{"Symbol":"v_wind","Number":0.0,"Unit":"m/s"},{"Symbol":"q_max_m","Number":60.0,"Unit":"W/m"},{"Symbol":"theta_se_max","Number":60.0,"Unit":"degC"},{"Symbol":"t_chosen","Number":50.0,"Unit":"mm"}]
```

**Block K9** — A 10 mm pipe with lambda 0.2 W/(m*K) and a 25 W/m limit: the bare pipe already complies. Known-answer case ka-bare-complies-window. Then clear the box t_chosen.
```json
[{"Symbol":"geometry","Text":"pipe (cylinder)"},{"Symbol":"D_i","Number":10.0,"Unit":"mm"},{"Symbol":"lambda","Number":0.2,"Unit":"W/(m*K)"},{"Symbol":"q_max_m","Number":25.0,"Unit":"W/m"}]
```

**Block T1** — Hot pipe 168.3 mm, 150 degC fluid, 10 degC air, lambda 0.035, h_se 8, limits 40 W/m and 50 degC, 60 mm chosen.
```json
[{"Symbol":"geometry","Text":"pipe (cylinder)"},{"Symbol":"D_i","Number":168.3,"Unit":"mm"},{"Symbol":"theta_i","Number":150,"Unit":"degC"},{"Symbol":"theta_a","Number":10,"Unit":"degC"},{"Symbol":"lambda","Number":0.035,"Unit":"W/(m*K)"},{"Symbol":"h_se","Number":8,"Unit":"W/(m^2*K)"},{"Symbol":"q_max_m","Number":40,"Unit":"W/m"},{"Symbol":"theta_se_max","Number":50,"Unit":"degC"},{"Symbol":"t_chosen","Number":60,"Unit":"mm"}]
```

**Block T2** — Chilled pipe 22 mm at 6 degC in 28 degC air, RH 80 %, margin 0, gain limit 10 W/m. Then clear the box t_chosen.
```json
[{"Symbol":"geometry","Text":"pipe (cylinder)"},{"Symbol":"D_i","Number":22,"Unit":"mm"},{"Symbol":"theta_i","Number":6,"Unit":"degC"},{"Symbol":"theta_a","Number":28,"Unit":"degC"},{"Symbol":"lambda","Number":0.035,"Unit":"W/(m*K)"},{"Symbol":"h_se","Number":8,"Unit":"W/(m^2*K)"},{"Symbol":"RH_a","Number":80,"Unit":"%"},{"Symbol":"dew_margin","Number":0,"Unit":"K"},{"Symbol":"q_max_m","Number":10,"Unit":"W/m"}]
```

**Block T3** — A hot flat surface 70 degC in 5 degC air, correlation, cladding emissivity 0.3, 1 m/s over 1.5 m, limits 40 W/m² and 35 degC. Then clear the box t_chosen.
```json
[{"Symbol":"geometry","Text":"flat surface"},{"Symbol":"theta_i","Number":70,"Unit":"degC"},{"Symbol":"theta_a","Number":5,"Unit":"degC"},{"Symbol":"h_mode","Text":"convection + radiation (correlation)"},{"Symbol":"eps","Number":0.3},{"Symbol":"v_wind","Number":1,"Unit":"m/s"},{"Symbol":"L_surf","Number":1.5,"Unit":"m"},{"Symbol":"q_max_A","Number":40,"Unit":"W/m^2"},{"Symbol":"theta_se_max","Number":35,"Unit":"degC"}]
```

**Block T4** — A 60.3 mm pipe at 200 degC in 0 degC air, correlation, 8 m/s wind, limits 50 W/m and 55 degC. Then clear the box t_chosen.
```json
[{"Symbol":"geometry","Text":"pipe (cylinder)"},{"Symbol":"D_i","Number":60.3,"Unit":"mm"},{"Symbol":"theta_i","Number":200,"Unit":"degC"},{"Symbol":"theta_a","Number":0,"Unit":"degC"},{"Symbol":"lambda","Number":0.045,"Unit":"W/(m*K)"},{"Symbol":"h_mode","Text":"convection + radiation (correlation)"},{"Symbol":"eps","Number":0.9},{"Symbol":"v_wind","Number":8,"Unit":"m/s"},{"Symbol":"q_max_m","Number":50,"Unit":"W/m"},{"Symbol":"theta_se_max","Number":55,"Unit":"degC"}]
```

**Block L1** — Near zero: the fluid is 0.05 K above the air, limit 25 W/m. Then clear the box t_chosen.
```json
[{"Symbol":"geometry","Text":"pipe (cylinder)"},{"Symbol":"theta_i","Number":20.05,"Unit":"degC"},{"Symbol":"theta_a","Number":20,"Unit":"degC"},{"Symbol":"q_max_m","Number":25,"Unit":"W/m"}]
```

**Block L2** — Extreme wind: a flat surface at 150 degC in -20 degC air, 40 m/s over 3 m, emissivity 0.1, 100 mm chosen, limit 25 W/m².
```json
[{"Symbol":"geometry","Text":"flat surface"},{"Symbol":"theta_i","Number":150,"Unit":"degC"},{"Symbol":"theta_a","Number":-20,"Unit":"degC"},{"Symbol":"h_mode","Text":"convection + radiation (correlation)"},{"Symbol":"eps","Number":0.1},{"Symbol":"v_wind","Number":40,"Unit":"m/s"},{"Symbol":"L_surf","Number":3,"Unit":"m"},{"Symbol":"q_max_A","Number":25,"Unit":"W/m^2"},{"Symbol":"t_chosen","Number":100,"Unit":"mm"}]
```

**Block L3** — A 300 mm hot pipe with a very loose limit of 1000 W/m: the bare pipe complies. Then clear the box t_chosen.
```json
[{"Symbol":"geometry","Text":"pipe (cylinder)"},{"Symbol":"D_i","Number":300,"Unit":"mm"},{"Symbol":"q_max_m","Number":1000,"Unit":"W/m"}]
```

**Block U** — Imperial entry: 6.625 in pipe at 300 degF in 70 degF air, lambda 0.036 W/(m*K), 2 in chosen, limit 30 W/m.
```json
[{"Symbol":"geometry","Text":"pipe (cylinder)"},{"Symbol":"D_i","Number":6.625,"Unit":"in"},{"Symbol":"theta_i","Number":300,"Unit":"degF"},{"Symbol":"theta_a","Number":70,"Unit":"degF"},{"Symbol":"lambda","Number":0.036,"Unit":"W/(m*K)"},{"Symbol":"q_max_m","Number":30,"Unit":"W/m"},{"Symbol":"t_chosen","Number":2,"Unit":"in"}]
```

**Block S** — DOWN for step 10: the cold flat surface of Block K5 without the humidity: theta_i 5, theta_a 25 degC, margin 1 K.
```json
[{"Symbol":"geometry","Text":"flat surface"},{"Symbol":"theta_i","Number":5,"Unit":"degC"},{"Symbol":"theta_a","Number":25,"Unit":"degC"},{"Symbol":"dew_margin","Number":1,"Unit":"K"}]
```

**Block D1** — Flat cold surface at -30 degC in -10 degC air, RH 90 %, margin 0.
```json
[{"Symbol":"geometry","Text":"flat surface"},{"Symbol":"theta_i","Number":-30,"Unit":"degC"},{"Symbol":"theta_a","Number":-10,"Unit":"degC"},{"Symbol":"RH_a","Number":90,"Unit":"%"},{"Symbol":"dew_margin","Number":0,"Unit":"K"}]
```

**Block D2** — Block D1 with the dew point entered as -10.10045758 degC, the frost point for 90 % relative to water.
```json
[{"Symbol":"geometry","Text":"flat surface"},{"Symbol":"theta_i","Number":-30,"Unit":"degC"},{"Symbol":"theta_a","Number":-10,"Unit":"degC"},{"Symbol":"theta_dew","Number":-10.10045758,"Unit":"degC"},{"Symbol":"dew_margin","Number":0,"Unit":"K"}]
```

**Block D3** — A bare 100 mm pipe at 45 degC in 20 degC air, correlation mode, still air, 0 mm chosen (surface at fluid temperature).
```json
[{"Symbol":"geometry","Text":"pipe (cylinder)"},{"Symbol":"D_i","Number":100,"Unit":"mm"},{"Symbol":"theta_i","Number":45,"Unit":"degC"},{"Symbol":"theta_a","Number":20,"Unit":"degC"},{"Symbol":"h_mode","Text":"convection + radiation (correlation)"},{"Symbol":"eps","Number":0.9},{"Symbol":"v_wind","Number":0,"Unit":"m/s"},{"Symbol":"t_chosen","Number":0,"Unit":"mm"},{"Symbol":"q_max_m","Number":25,"Unit":"W/m"}]
```

## Notes for the tester
- Compare one hot pipe with a manufacturer insulation calculator and one table of BS 5422 thicknesses. This sheet uses neither. See oracles.md.
- Findings -03 and -04 have no step. The conductivity is a fixed input, so repeat a run with lambda at the reported mean temperature. The default h_se of 10 W/(m²*K) is a plain default.
