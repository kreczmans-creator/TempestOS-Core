# 🟢 Green team — engineering correctness

**Method.** Every `Template.TestCases` entry of the 16 calculators under `src/Tempest.Calc/Tempest.Calc.{Mechanical,Structural,BuildingServices,Electrical}` was read from the C# source (inputs and `CalcExpectedValue` outputs) and recomputed independently in a Python 3 script kept outside the repository (scratchpad, never shipped). Formulas were re-derived from first principles or the cited references in my own notation: Lamé constants and von Mises/Tresca; Shigley press-fit compliances; ISO 898-1 stress area, VDI 2230 R2–R9 and Shigley frusta; elastic bolt-group method; ISO 286-1 tolerance unit, IT multipliers and fundamental-deviation formulae at the step geometric mean; worst-case/RSS/C_pk/DPMO with `erfc`; Roark Table 8.1 closed forms; a separate numpy direct-stiffness solver (Euler–Bernoulli, static condensation of releases, exact fixed-end forces) for the frame cases; EN 1993-1-1 6.2.3–6.2.8 and 6.3.1 clause formulae; Green's-theorem integration of fine polygons plus El Darwish–Johnston/Bredt/Saint-Venant torsion; Hyland–Wexler and the ASHRAE ideal-gas psychrometric relations; Darcy–Weisbach with my own Colebrook iteration and Swamee–Jain; Kell/Vogel water properties; servo reflected-inertia and RMS-torque relations; resistivity and √3·I·(R cos φ + X sin φ); effective-surface enclosure balance. The pass criterion is the repository's own (`KnownAnswerRunner.cs:80`): |recomputed − expected| ≤ tol·|expected|, absolute tol when expected = 0; default tol 0.01. Unit handling, sign conventions, guards, rounding and standards reproduction were audited by reading each `Run` method and the engine helpers it calls (`UnitCatalog`, `ScalarValue`, `RunResult.CheckUtilisation`, `FrameSolver`, `PolygonSection`, `MoistAir`, `Water/Friction`, `Iso286`, `Statistics`).

**Result: 47 cases, all 219 C# expected values recomputed (plus one Hagen–Poiseuille cross-check row): 219 PASS, 0 FAIL.** Borderline passes (within 50 % of their tolerance) are called out in the findings (G-05, G-06, G-12, G-14).

## Recomputation tables

### mech.thick-cylinder

| Case | Symbol: C# expected → recomputed (rel. diff / tol.) | Verdict |
|---|---|---|
| ka-internal-closed | sigma_t_a 83.333→83.3333 (4.0e-06/0.0001); sigma_r_a -50→-50 (2.8e-16/1e-06); sigma_t_b 33.333→33.3333 (1.0e-05/0.0001); sigma_z 16.667→16.6667 (2.0e-05/0.0001); sigma_vm_a 115.47→115.47 (4.7e-07/0.0001); sigma_tr_a 133.33→133.333 (2.5e-05/0.0001); u 0.57735→0.57735 (4.7e-07/0.0001); sigma_thin 75→75 (0.0e+00/1e-06) | PASS (8/8) |
| ka-external-closed | sigma_t_a -72→-72 (0.0e+00/1e-06); sigma_t_b -52→-52 (0.0e+00/1e-06); sigma_z -36→-36 (0.0e+00/1e-06); sigma_vm_a 62.354→62.3538 (2.7e-06/0.0001); sigma_vm_b 27.713→27.7128 (6.8e-06/0.0001); u 0.62354→0.623538 (2.7e-06/0.0001) | PASS (6/6) |
| ka-internal-open | sigma_vm_a 116.67→116.667 (2.9e-05/0.0001); sigma_vm_b 33.333→33.3333 (1.0e-05/0.0001); u 0.58333→0.583333 (5.7e-06/0.0001) | PASS (3/3) |

### mech.press-fit

| Case | Symbol: C# expected → recomputed (rel. diff / tol.) | Verdict |
|---|---|---|
| ka-steel-steel-solid | p 62.1→62.1 (0.0e+00/0.0001); sigma_t_hub 103.5→103.5 (0.0e+00/0.0001); F_a 87.8→87.7918 (9.3e-05/0.001); T 2195→2194.8 (9.3e-05/0.001); dT 100→100 (2.8e-16/1e-06) | PASS (5/5) |
| ka-hollow-shaft | p 49.63→49.68 (1.0e-03/0.002); sigma_t_shaft -82.7→-82.8 (1.2e-03/0.002) | PASS (2/2) |

### mech.bolted-joint

| Case | Symbol: C# expected → recomputed (rel. diff / tol.) | Verdict |
|---|---|---|
| ka-m12-88-nut-factor | A_s 84.27→84.2664 (4.3e-05/0.001); F_M 38.75→38.75 (0.0e+00/0.0001); sigma_M 459.8→459.851 (1.1e-04/0.002) | PASS (3/3) |
| ka-shigley-stiffness | k_P 1.9078e+06→1.9078e+06 (1.0e-05/0.002); k_S 3.2119e+05→3.2119e+05 (1.3e-05/0.002); Phi 0.07205→0.0720469 (4.3e-05/0.003); F_M 40.69→40.6899 (3.0e-06/0.002) | PASS (4/4) |

### mech.bolt-group

| Case | Symbol: C# expected → recomputed (rel. diff / tol.) | Verdict |
|---|---|---|
| ka-four-bolt-eccentric | T -7.5→-7.5 (0.0e+00/1e-06); J 1.686e+06→1.686e+06 (0.0e+00/1e-06); v_max 36.443→36.4434 (1.2e-05/0.001); u_max 0.91109→0.911086 (4.1e-06/0.001) | PASS (4/4) |
| ka-moment-tension | t_max 50→50 (0.0e+00/1e-06); u_max 0.83333→0.833333 (4.0e-06/0.0001) | PASS (2/2) |

### mech.iso286-fit

| Case | Symbol: C# expected → recomputed (rel. diff / tol.) | Verdict |
|---|---|---|
| ka-h7g6-25 | IT_hole 21→20.918 (3.9e-03/0.03); IT_shaft 13→13.0738 (5.7e-03/0.03); es -7→-7.28529 (4.1e-02/0.06); c_min 7→7.28529 (4.1e-02/0.06); c_max 41→41.277 (6.8e-03/0.03) | PASS (5/5) |
| ka-h7p6-50 | IT_hole 25→24.9799 (8.0e-04/0.03); ei 26→24.9799 (3.9e-02/0.06); i_max 42→40.5923 (3.4e-02/0.05); i_min 0→0 (0.0e+00/1.5) | PASS (4/4) |

### mech.tolerance-stack

| Case | Symbol: C# expected → recomputed (rel. diff / tol.) | Verdict |
|---|---|---|
| ka-shigley-screw | x_nom 0.005→0.005 (8.7e-16/1e-09); t_wc 0.01→0.01 (0.0e+00/1e-09); x_wc_min -0.005→-0.005 (8.7e-16/1e-06); t_rss 0.006→0.006 (0.0e+00/1e-06); sigma 0.002→0.002 (0.0e+00/1e-06); cpk 0.333333→0.333333 (2.2e-15/1e-06) | PASS (6/6) |
| ka-rss-analytic | x_mean 0.61→0.61 (6.7e-15/1e-09); t_wc 0.13→0.13 (0.0e+00/1e-09); t_rss 0.0806226→0.0806226 (6.0e-09/1e-06); sigma 0.0268742→0.0268742 (1.8e-08/1e-06); cpk 2.35666→2.35666 (1.8e-08/1e-06); dpmo 7.7755e-07→7.7759e-07 (5.2e-05/0.05) | PASS (6/6) |

### struct.beam-formulas

| Case | Symbol: C# expected → recomputed (rel. diff / tol.) | Verdict |
|---|---|---|
| ka-ss-udl | R_A 30→30 (0.0e+00/1e-09); M_max 45→45 (0.0e+00/1e-09); x_M 3→3 (0.0e+00/1e-09); delta_max 10.0446→10.0446 (4.3e-06/0.0001) | PASS (4/4) |
| ka-ss-point | R_A 16.6667→16.6667 (2.0e-06/0.0001); R_B 33.3333→33.3333 (1.0e-06/0.0001); M_max 66.6667→66.6667 (5.0e-07/0.0001); x_delta 3.266→3.26599 (4.2e-06/0.0001); delta_max 11.5202→11.5202 (2.9e-06/0.0001) | PASS (5/5) |
| ka-cantilever-point | R_A 20→20 (0.0e+00/1e-09); M_min -60→-60 (0.0e+00/1e-09); delta_max 45→45 (1.6e-16/1e-09) | PASS (3/3) |
| ka-fixed-udl | M_min -30→-30 (0.0e+00/1e-09); M_max 15→15 (0.0e+00/1e-09); delta_max 2.00893→2.00893 (7.1e-07/0.0001) | PASS (3/3) |

### struct.frame-2d

| Case | Symbol: C# expected → recomputed (rel. diff / tol.) | Verdict |
|---|---|---|
| ka-ss-udl | R1_y 30→30 (0.0e+00/1e-09); R2_y 30→30 (0.0e+00/1e-09); M_max 45→45 (0.0e+00/1e-09); x_Mmax 3→3 (0.0e+00/1e-06); V_max 30→30 (0.0e+00/1e-09); d_max 10.0446→10.0446 (4.3e-06/0.0001) | PASS (6/6) |
| ka-cantilever-point | R1_y 20→20 (0.0e+00/1e-09); R1_m 60→60 (0.0e+00/1e-09); M_min -60→-60 (0.0e+00/1e-09); d_max 45→45 (1.6e-16/1e-09) | PASS (4/4) |
| ka-propped-cantilever | R2_y 36→36 (0.0e+00/1e-09); R1_y 60→60 (0.0e+00/1e-09); M_min -96→-96 (0.0e+00/1e-09); M_max 54→54 (0.0e+00/1e-09); x_Mmax 5→5 (0.0e+00/1e-06) | PASS (5/5) |
| ka-portal-sway | R1_x -15→-15 (4.3e-08/1e-05); R4_x -15→-15 (4.3e-08/1e-05); R1_y -20→-20 (2.1e-10/1e-09); R4_y 20→20 (2.1e-10/1e-09); M_max 60→60 (4.3e-08/1e-05); N_e1 20→20 (2.1e-10/1e-09) | PASS (6/6) |
| ka-two-span-release | R3_y 20→20 (0.0e+00/1e-09); R2_y 20→20 (0.0e+00/1e-09); R1_y 0→0 (0.0e+00/1e-09); R1_m 0→0 (0.0e+00/1e-09); M_max 40→40 (0.0e+00/1e-09) | PASS (5/5) |

### struct.en1993-member

| Case | Symbol: C# expected → recomputed (rel. diff / tol.) | Verdict |
|---|---|---|
| ka-uc-compression | N_pl_Rd 2083.85→2083.85 (0.0e+00/0.0001); N_cr_z 2005.3→2005.26 (2.2e-05/0.001); lambda_z 1.0194→1.01941 (8.3e-06/0.001); chi_z 0.5288→0.528759 (7.8e-05/0.001); N_b_z_Rd 1101.9→1101.85 (4.1e-05/0.001); u_buckling 0.8168→0.816805 (5.9e-06/0.001) | PASS (6/6) |
| ka-ub-bending-shear | M_y_c_Rd 171.325→171.325 (0.0e+00/0.0001); A_v 20.068→20.0676 (2.0e-05/0.0001); V_pl_Rd 318.62→318.616 (1.3e-05/0.001); u_M_y 0.70043→0.700423 (9.7e-06/0.001); u_V 0.31385→0.313857 (2.4e-05/0.001) | PASS (5/5) |
| ka-tension-class3 | N_pl_Rd 710→710 (0.0e+00/1e-09); M_z_c_Rd 10.65→10.65 (0.0e+00/1e-09); u_section 1.03294→1.03286 (7.4e-05/0.0001) | PASS (3/3) |

### struct.section-properties

| Case | Symbol: C# expected → recomputed (rel. diff / tol.) | Verdict |
|---|---|---|
| ka-ub-457x191x98 | A 125→125.263 (2.1e-03/0.005); I_xx 45730→45727 (6.7e-05/0.005); I_yy 2347→2347.02 (7.4e-06/0.005); i_x 19.1→19.1062 (3.3e-04/0.005); i_y 4.33→4.3286 (3.2e-04/0.005); W_el_x 1957→1957.49 (2.5e-04/0.005); W_el_y 243→243.467 (1.9e-03/0.005); W_pl_x 2232→2232.41 (1.8e-04/0.005); W_pl_y 379→378.902 (2.6e-04/0.005); J 121→121.287 (2.4e-03/0.01); I_w 1.18→1.17554 (3.8e-03/0.01) | PASS (11/11) |
| ka-shs-100x100x5 | A 18.7→18.7317 (1.7e-03/0.005); I_xx 279→279.432 (1.5e-03/0.005); W_pl_x 66.4→66.3583 (6.3e-04/0.005); J 439→439.411 (9.4e-04/0.01) | PASS (4/4) |
| ka-rectangle | A 200→200 (0.0e+00/1e-09); I_xx 6666.67→6666.67 (5.0e-07/1e-05); I_yy 1666.67→1666.67 (2.0e-06/1e-05); W_el_x 666.667→666.667 (5.0e-07/1e-05); W_pl_x 1000→1000 (0.0e+00/1e-06); W_pl_y 500→500 (0.0e+00/1e-06); i_x 5.7735→5.7735 (4.7e-07/0.0001); J 4580→4573.63 (1.4e-03/0.002) | PASS (8/8) |
| ka-tube | A 59.69→59.6903 (4.4e-06/0.0001); I_xx 2701.1→2700.98 (4.3e-05/0.0001); W_el_x 270.11→270.098 (4.3e-05/0.0001); W_pl_x 361.33→361.333 (9.2e-06/0.0001); J 5402.2→5401.97 (4.3e-05/0.0001) | PASS (5/5) |
| ka-angle-polygon | A 27.36→27.36 (0.0e+00/1e-06); x_c 21.3947→21.3947 (1.5e-07/1e-05); y_c 51.3947→51.3947 (6.1e-08/1e-05); I_xx 631.8→631.801 (9.0e-07/0.0002); I_yy 174.31→174.313 (1.5e-05/0.0002); I_xy -191.2→-191.203 (1.4e-05/0.0002) | PASS (6/6) |

### bs.psychrometrics

| Case | Symbol: C# expected → recomputed (rel. diff / tol.) | Verdict |
|---|---|---|
| ka-saturation-20 | p_ws_1 2.3392→2.3388 (1.7e-04/0.001); W_1 14.758→14.6951 (4.3e-03/0.005); h_1 57.555→57.419 (2.4e-03/0.005); v_1 0.8498→0.850082 (3.3e-04/0.002); t_dp_1 20→20 (4.3e-15/0.0001); t_wb_1 20→20 (0.0e+00/0.0001) | PASS (6/6) |
| ka-ashrae-example-1 | W_1 6.4→6.40079 (1.2e-04/0.02); h_1 56.6→56.7246 (2.2e-03/0.01); v_1 0.897→0.896248 (8.4e-04/0.005); RH_1 13.9→13.9795 (5.7e-03/0.03); t_dp_1 7.4→7.43361 (4.5e-03/0.05) | PASS (5/5) |
| ka-sensible-heating | Q 20.306→20.306 (1.7e-16/1e-06); W_2 5→5 (0.0e+00/1e-09); t_2_out 30→30 (0.0e+00/1e-09) | PASS (3/3) |
| ka-mixing | W_2 8.333→8.31729 (1.9e-03/0.002); h_2 46.41→46.3696 (8.7e-04/0.002); t_2_out 25.07→25.0306 (1.6e-03/0.002) | PASS (3/3) |

### bs.duct-sizing

| Case | Symbol: C# expected → recomputed (rel. diff / tol.) | Verdict |
|---|---|---|
| ka-round-given | v_s1 7.958→7.95775 (3.2e-05/0.001); f_s1 0.01714→0.0171366 (2.0e-04/0.005); dpL_s1 1.633→1.63334 (2.1e-04/0.005); dp_index 16.33→16.3334 (2.1e-04/0.005) | PASS (4/4) |
| ka-rectangular-equivalent | De_s1 457.3→457.012 (6.3e-04/0.002); v_s1 4.444→4.44444 (1.0e-04/0.001) | PASS (2/2) |
| ka-velocity-method | D_s1 350→350 (1.6e-16/1e-09); v_s1 5.197→5.1969 (2.0e-05/0.001); dp_fit_s1 19.47→19.512 (2.2e-03/0.005) | PASS (3/3) |

### bs.pipe-pump

| Case | Symbol: C# expected → recomputed (rel. diff / tol.) | Verdict |
|---|---|---|
| ka-laminar-oil | v_s1 0.14147→0.141471 (7.5e-06/0.0001); Re_s1 7.852→7.85164 (4.5e-05/0.001); f_s1 8.151→8.15116 (2.0e-05/0.001); h_f_total 6.654→6.65417 (2.6e-05/0.001); dp_total 57.946→57.9465 (9.4e-06/0.001); dp_total (Hagen–Poiseuille 32µLv/D²) 57.946→57.9465 (9.4e-06/0.001) | PASS (6/6) |
| ka-turbulent-water | v_s1 1→1 (4.2e-09/1e-06); Re_s1 1e+05→1e+05 (4.2e-09/1e-06); f_s1 0.022175→0.0221745 (2.1e-05/0.0005); h_f_total 1.1306→1.13059 (1.2e-05/0.001); f_sj_s1 0.022342→0.0223424 (1.8e-05/0.001) | PASS (5/5) |
| ka-duty-point | H_static 10→10 (0.0e+00/1e-09); H_duty 17.67→17.6682 (1.0e-04/0.01); Q_duty 46.3→46.3318 (6.9e-04/0.01); Q_duty2 40.1→40.1049 (1.2e-04/0.01) | PASS (4/4) |

### elec.motor-sizing

| Case | Symbol: C# expected → recomputed (rel. diff / tol.) | Verdict |
|---|---|---|
| ka-ball-screw | v_pk 1.5→1.5 (1.5e-16/1e-09); n_pk 9000→9000 (2.0e-16/1e-09); J_refl 0.00014072→0.00014072 (2.7e-05/0.0001); T_acc 2.4923→2.49223 (2.6e-05/0.0001); T_load 0.0867→0.0867098 (1.1e-04/0.001); T_pk 2.579→2.57894 (2.1e-05/0.001); T_rms 1.4402→1.4402 (1.3e-06/0.001); J_ratio 1.2036→1.20362 (1.6e-05/0.001) | PASS (8/8) |
| ka-geared-index-table | n_pk 750→750 (0.0e+00/1e-06); J_refl 0.005882→0.00588235 (6.0e-05/0.001); T_pk 5.0126→5.01269 (1.8e-05/0.001); T_rms 2.2417→2.24174 (1.9e-05/0.001); J_ratio 11.76→11.7647 (4.0e-04/0.002) | PASS (5/5) |

### elec.voltage-drop

| Case | Symbol: C# expected → recomputed (rel. diff / tol.) | Verdict |
|---|---|---|
| ka-dc | R 0.10314→0.103138 (1.6e-05/0.0001); dV 4.1256→4.12553 (1.6e-05/0.0001); dV_pct 8.595→8.59486 (1.6e-05/0.001); P_loss 82.51→82.5106 (7.8e-06/0.001) | PASS (4/4) |
| ka-three-phase | R 0.04715→0.0471489 (2.3e-05/0.0001); dV 7.526→7.52542 (7.8e-05/0.001); dV_pct 1.8815→1.88135 (7.8e-05/0.001); P_loss 1414.5→1414.47 (2.3e-05/0.001); T_c_est 58.25→58.2466 (5.8e-05/0.001) | PASS (5/5) |
| ka-mvam | dV 7.56→7.56 (1.2e-16/1e-09); dV_pct 1.89→1.89 (0.0e+00/1e-09) | PASS (2/2) |

### elec.enclosure-heat

| Case | Symbol: C# expected → recomputed (rel. diff / tol.) | Verdict |
|---|---|---|
| ka-wall-mounted | P_total 300→300 (0.0e+00/1e-09); A_eff 1.14→1.14 (0.0e+00/1e-09); dT_nat 47.847→47.8469 (2.3e-06/0.0001); V_req 62.48→62.4806 (9.6e-06/0.001); P_cool 205.95→205.95 (0.0e+00/0.0001) | PASS (5/5) |
| ka-fan-duty | V_duty 113.75→113.746 (3.6e-05/0.001); p_duty 25.88→25.8762 (1.5e-04/0.002); u_fan 0.5493→0.5493 (2.2e-08/0.002) | PASS (3/3) |

## Audit notes (units, signs, guards, rounding, standards)

Unit paths checked and found consistent: µm→m and GPa in the press-fit compliances (`PressFitCalculator.cs:187-190`); affine °C handled as `(T_c − 20 degC)/(1 K)` (`VoltageDropCalculator.cs:265`) and absolute `T_amb` in the air density (`EnclosureHeatCalculator.cs:128`); `rpm` = 2π/60 rad/s (`UnitCatalog.cs:61`); bolt-group `J` computed in SI m⁴ and displayed through `ScalarValue(si, L⁴, mm⁴)` (`BoltGroupCalculator.cs:478`); frame reactions as Σ member end forces − nodal loads with anticlockwise moments (`FrameSolver.cs:331`, cantilever `R1_m = +60`). Sign conventions verified by the recomputation: sagging-positive moments and downward-positive deflections in the beam library, tension-positive `N_Ed` (`MemberChecksCalculator.cs:234`), tension-positive `N` and "tension on +y side" moments in the bolt group (`:383-385`), torque anticlockwise about the centroid (`:471`, `:499-500`). Rounding: `Snap()` zeroes 1e-9-relative noise only (`SectionPropertiesCalculator.cs:236-247`); `Clean()` zeroes < 1e-9 N (`FrameCalculator.cs:304`); ISO 286 results are deliberately unrounded formula values (assumption at `Iso286FitCalculator.cs:28`).

## Findings

| ID | Severity | Location | Description | Evidence | Fix or justification | Status |
|---|---|---|---|---|---|---|
| G-01 | High | `DuctSizingCalculator.cs:226-229` | Rectangular and flat-oval friction is computed with the *actual* velocity `v = Q/(ab)` but the *equivalent* diameter `D_e`. Huebscher's `D_e` is defined so that a round duct of diameter `D_e` carrying the same `Q` has the same friction, so the friction velocity must be `4Q/(πD_e²)` (equivalently: actual `v` with the hydraulic diameter). Mixing the two underestimates Δp for every non-round section and the index-run total. | 600 × 300 mm, 0.8 m³/s, 20 °C, ε = 0.09 mm: sheet method 0.471 Pa/m; `(v, D_h)` 0.554 Pa/m; `(Q at D_e)` 0.559 Pa/m → −16 %. `ka-rectangular-equivalent` only checks `De_s1` and `v_s1`, so the KA suite cannot see it. | Use `v_e = 4Q/(πD_e²)` and `D_e` in `Re` and `dpL` (keep the actual `v` for velocity pressure, fittings and the velocity limit); add `dpL_s1` to the rectangular KA case. | Open |
| G-02 | Medium | `BoltGroupCalculator.cs:375, 504, 525` | The interaction is circular, `√((v/V_R)² + (t/T_R)²) ≤ 1`, but the check cites "EN 1993-1-8 Table 3.4", whose combined shear-and-tension rule is linear, `F_v,Ed/F_v,Rd + F_t,Ed/(1.4 F_t,Rd) ≤ 1`. The circular form passes bolts the cited clause fails. | `v/V_R = t/T_R = 0.6`: circular 0.85 (pass) vs Table 3.4 0.60 + 0.43 = 1.03 (fail). The two KA cases are pure shear and pure tension, where both forms agree. | Implement the Table 3.4 form for the cited check (keep the circular form only as a labelled sheet assumption), and add a mixed shear + tension KA case. | Open |
| G-03 | Medium | `RunResult.cs:148`; `PipeFlowCalculator.cs:299-306`; `BoltedJointCalculator.cs:336, 347` | `CheckUtilisation` passes whenever `u ≤ 1`, including `u < 0`. `NPSH_a` can be negative (suction lift plus friction exceeding `p_atm/ρg − p_v/ρg`), so `u_npsh = NPSH_r/NPSH_a < 0` reports "NPSH_r ≤ NPSH_a" as a pass. Same pattern for `u_clamp = F_KR_req/F_KR` when `F_KR ≤ 0` (mitigated by the separate `CheckCondition` at `:346`). | `u.Si <= limit + 1e-9` is the only test; no sign guard on the denominator in either sheet. | Make `CheckUtilisation` fail (or require a positive denominator) when `u < 0`; in the pipe sheet block or fail explicitly when `NPSH_a ≤ 0`. | Open |
| G-04 | Medium | `ToleranceStackCalculator.cs:503, 572` | Yield is recorded as `new ScalarValue((1 − dpmo/1e6)·100, Dimension.None, "%")`. The constructor takes an SI value and `%` has factor 0.01 (`UnitCatalog.cs:62`, `Unit.FromSi = si/Factor` at `Unit.cs:17`), so the sheet displays 100× the yield ("9999.9 %"). | `ToleranceStackTests.cs:117` asserts `OutputSi("mc_yield")` in [99.9, 100], i.e. the SI slot holds a percentage, which the display unit then multiplies by 100 again. | Store the fraction as SI (`new ScalarValue(1 − dpmo/1e6, Dimension.None, "%")`) or use `ScalarValue.Of(value, "%")`; fix the test to read `Displayed`. | Open |
| G-05 | Low | `PsychrometricsCalculator.cs:93-94` | `ka-mixing` inputs (`RH_b = 46.93 %` at 15 °C) give `W_b = 4.953 g/kg`, but the expected values were derived with `W_b = 5.000 g/kg`. | Recomputed `W_2 = 8.317` vs expected 8.333 (1.9e-3 against tol 2e-3); `t_2_out` 25.03 vs 25.07 (1.6e-3). Passes by 5 % of the tolerance. | Set `RH_b = 47.37 %` (gives 5.000 g/kg) or set expected `W_2 = 8.317`, `h_2 = 46.37`, `t_2_out = 25.03` at 1e-3. | Open |
| G-06 | Low | `PressFitCalculator.cs:148-150` | `ka-hollow-shaft` expected `p = 49.63 MPa` was produced from compliances rounded to 3 s.f. in the reference text; the exact Lamé value is 49.68 MPa (`σ_t,shaft = −82.80`). | Rel. diff 1.0e-3 against tol 2e-3 (half the tolerance consumed by rounding). | Expect 49.68 / −82.80 at 1e-4. | Open |
| G-07 | Low | `MemberChecksCalculator.cs:356-359, 386` | `ρ` from the vertical shear `V_Ed` reduces `M_z,c,Rd` as well as `M_y,c,Rd`; 6.2.8 reduces the resistance in the plane of the shear only. The 6.2.8(5) rolled-I web-only reduction is not offered. `u_member` combines `N_b,Rd` (γ_M1) with `M_c,Rd` (γ_M0) where 6.3.3 uses γ_M1 throughout. | Conservative in all three respects; invisible in the KA cases (ρ = 0, γ_M0 = γ_M1 = 1). | Apply ρ to `M_y` only (or state the conservatism in an assumption); use γ_M1 in the 6.3.3 moment terms. | Open |
| G-08 | Low | `Iso286.cs:367, 406` | `p` uses `IT7 + 0` from the standard's "IT7 + 0 to 5 µm" range, so `ei` is 24.98 vs the table's 26 µm at 50 mm (3.9 %, hidden by the 6 % tolerance). The Δ rule is applied at every size, whereas ISO 286-1 restricts `ES = −ei + Δ` to sizes above 3 mm. | `ka-h7p6-50 ei` rel. diff 3.9e-2 of 6e-2; `c_min` for `ka-h7g6-25` 4.1e-2 of 6e-2. | Confirm the ≤ 3 mm rule against ISO 286-1 §5; tighten the KA tolerances by supplying overrides from the table, as the sheet itself recommends. | Open |
| G-09 | Low | `FrameCalculator.cs:60-65`; `FrameSolver.cs:393-402` | The sign of `w` flips with the gravity flag: plain `udl`/`trapezoid` is positive towards local +y (upward on a left-to-right beam), gravity is positive downward. The KA cases enter `w1 = −10` for a downward load; a user following the gravity label will enter +10 and get an uplift. | Column label `"w at a"` carries no sign; only the gravity column says "positive downward". | State the convention in the `w1`/`w2` labels, or adopt one sign convention for both modes. | Open |
| G-10 | Low | `BeamFormulasCalculator.cs:110` | `SsMomentEnd` labels `R_A = −M_0/L` as "downward positive" while every other case reports reactions upward-positive and the couple direction is not stated. | Mixed sign convention within one sheet; `M_0` is `Min: 0` so the couple direction is implicit. | Keep upward-positive, state "M_0 anticlockwise at x = 0", and label `R_A` as a downward reaction of magnitude `M_0/L`. | Open |
| G-11 | Info | `EnclosureHeatCalculator.cs:71, 108`; `PsychrometricsCalculator.cs:75` | The declared rules `ventilation-limit` and `range` are never raised (the hard limits and the `limit` block cover them). `A_eff` is a plain sum of exposed faces, more conservative than the IEC TR 60890 effective-surface factors the description invokes (1.14 m² here vs ≈ 1.36 m² by the TR 60890 wall-mounted formula). | Dead rule ids; wording "IEC TR 60890 approach". | Remove the dead rules or raise them; say "face sum with user `h`" rather than "IEC TR 60890-style". | Open |
| G-12 | Info | `DuctSizingCalculator.cs:205-206, 86` | Comment says default `a = 2× target D_e`, code uses 1.5×. `ka-velocity-method dp_fit_s1 = 19.47 Pa` corresponds to ρ ≈ 1.2016; the sheet's ρ = 1.2041 gives 19.51 (2.2e-3 of 5e-3). | Comment/code mismatch; expected value from a different density. | Fix the comment; expect 19.51 at 1e-3. | Open |
| G-13 | Info | `Iso286.cs:246-336`; `MemberChecksCalculator.cs:219-227` | Standards reproduction: none found. `Iso286` evaluates the published ISO 286-1 formulae (tolerance unit, IT multipliers, deviation formulae) and states that the tables are not reproduced; the five imperfection factors are EN 1993-1-1 Table 6.1 values attributed to Gardner & Nethercot (textbook values). Fitting coefficients, `I_z`, mV/A/m figures, loss coefficients and `h` are user inputs with provenance notes. | Clause numbers and titles only elsewhere (`CalcClauseRef`s). | None required. | Open |
| G-14 | Info | `SectionPropertiesCalculator.cs:102`; `PsychrometricsCalculator.cs:81` | Borderline but legitimate: rectangle `J` expected 4580 (Roark's rounded K = 0.229) vs series 4573.6 (1.4e-3 of 2e-3); saturated `W` at 20 °C is −0.43 % of the 0.5 % tolerance because the enhancement factor is taken as 1 (documented). `ka-ashrae-example-1` and `ka-uc-compression` are marked `Unconfirmed` and were confirmed here against the relations, not the printed sources. | Rel. diffs in the table above. | Widen the rectangle `J` tolerance to 5e-3 or expect 4573.6; keep the psychrometric note. | Open |

## Addendum A — mechanical and structural

**Scope.** M4 `mech.shaft`, M5 `mech.bearing-life`, M6 `mech.weld-group`, S4 `struct.connection-ec3`, S5 `struct.load-combinations`, S6 `struct.base-plate` at `origin/v0.26.0/addA-8-integration`, plus the drive-train and steel-support chains (`CalcPackTemplates.cs`, `CalcPackExamples.cs`, `docs/worked-examples/packs/{drive-train,steel-support}`). Read-only; no test suite run.

**Method.** Every `CalcExpectedValue` of the six templates' `TestCases` (30 cases, 131 values) was recomputed by independent Python written from the relations as stated in the standards and textbooks (Shigley 10th ed. §6-9, §7-4, §7-6, §7-7 and Ch. 9 line properties; ISO 281:2007 cl. 6–9 and ISO 76; EN 1993-1-8 Tables 3.4, 4.1, 6.2, cl. 3.10.2, 4.5.3, 6.2.2, 6.2.5, 6.2.8; EN 1990 6.10/6.10a/b, 6.11b, 6.14b–6.16b with Annex A1 ψ; EN 1992-1-1 3.1.6, 6.7), not ported from the C#. Beam slopes and deflections were obtained by direct double integration of M/EI on a 400 001-point grid; the normal quantile from Python's `statistics.NormalDist`. Each value is compared at the case's own tolerance. The 63 numbers of the two issued chain examples were recomputed the same way (frame statics by hand, stepped-shaft beam by integration). Scripts: `scratchpad/reviews/green-ms/recompute.py`, `chains.py`.

**Result.** 131 of 131 known-answer values agree at their tolerances (none uses more than 22 % of its tolerance); 63 of 63 chain values agree with the issued sheets to the printed precision. The defects found are in method coverage, defaults, check semantics and chain meaning (G-A-01 to G-A-15), not in the pinned numbers.

### Known-answer cases (every `TestCases` entry)

| Template | Case | Symbol | Expected | Recomputed | Rel. diff | Verdict |
|---|---|---|---|---|---|---|
| `mech.shaft` | `ka-simply-supported` | R_A [kN] | 1 | 1 | 0.0e+00 | agree (tol 1e-06) |
| `mech.shaft` | `ka-simply-supported` | M_max [kN·m] | 0.2 | 0.2 | 0.0e+00 | agree (tol 1e-06) |
| `mech.shaft` | `ka-simply-supported` | theta_A [rad] | 0.002429991 | 0.002429991 | 1.6e-07 | agree (tol 1e-05) |
| `mech.shaft` | `ka-simply-supported` | y_load [mm] | 0.3239989 | 0.3239989 | 1.5e-07 | agree (tol 1e-05) |
| `mech.shaft` | `ka-simply-supported` | n_c [rpm] | 5305.174 | 5305.174 | 3.4e-08 | agree (tol 1e-05) |
| `mech.shaft` | `ka-simply-supported` | n_c_D [rpm] | 5305.174 | 5305.174 | 3.4e-08 | agree (tol 1e-05) |
| `mech.shaft` | `ka-key` | tau_key [MPa] | 36.99405 | 36.99405 | 6.4e-08 | agree (tol 1e-06) |
| `mech.shaft` | `ka-key` | sigma_key [MPa] | 84.55782 | 84.55782 | 3.7e-08 | agree (tol 1e-06) |
| `mech.shaft` | `ka-shigley-7-1` | S_e [MPa] | 203.7091 | 203.7091 | 1.0e-07 | agree (tol 1e-05) |
| `mech.shaft` | `ka-shigley-7-1` | n_f_min | 1.650015 | 1.650015 | 9.3e-08 | agree (tol 1e-05) |
| `mech.shaft` | `ka-shigley-7-1` | n_y_min | 4.585856 | 4.585856 | 1.1e-07 | agree (tol 1e-05) |
| `mech.bearing-life` | `ka-ball-95` | L_10 | 1000 | 1000 | 0.0e+00 | agree (tol 1e-09) |
| `mech.bearing-life` | `ka-ball-95` | L_10h [h] | 16666.67 | 16666.67 | 2.0e-07 | agree (tol 1e-06) |
| `mech.bearing-life` | `ka-ball-95` | a_1 | 0.637912 | 0.6379117 | 5.3e-07 | agree (tol 1e-05) |
| `mech.bearing-life` | `ka-ball-95` | L_nmh [h] | 10631.86 | 10631.86 | 9.9e-08 | agree (tol 1e-05) |
| `mech.bearing-life` | `ka-ball-95` | u_life | 0.940569 | 0.9405691 | 1.1e-07 | agree (tol 1e-05) |
| `mech.bearing-life` | `ka-roller-99` | L_10 | 2154.435 | 2154.435 | 1.4e-07 | agree (tol 1e-06) |
| `mech.bearing-life` | `ka-roller-99` | L_10h [h] | 71814.49 | 71814.49 | 4.6e-09 | agree (tol 1e-06) |
| `mech.bearing-life` | `ka-roller-99` | a_1 | 0.248332 | 0.2483317 | 1.3e-06 | agree (tol 1e-05) |
| `mech.bearing-life` | `ka-roller-99` | L_nm | 1070.029 | 1070.029 | 2.6e-07 | agree (tol 1e-05) |
| `mech.bearing-life` | `ka-roller-99` | L_nmh [h] | 35667.62 | 35667.62 | 1.1e-07 | agree (tol 1e-05) |
| `mech.bearing-life` | `ka-combined` | P [kN] | 4.68 | 4.68 | 0.0e+00 | agree (tol 1e-09) |
| `mech.bearing-life` | `ka-combined` | L_10 | 263.4063 | 263.4063 | 1.1e-07 | agree (tol 1e-06) |
| `mech.bearing-life` | `ka-combined` | L_10h [h] | 3658.421 | 3658.421 | 6.3e-08 | agree (tol 1e-06) |
| `mech.bearing-life` | `ka-combined` | P_0 [kN] | 3 | 3 | 0.0e+00 | agree (tol 1e-09) |
| `mech.bearing-life` | `ka-combined` | s_0 | 5.333333 | 5.333333 | 6.3e-08 | agree (tol 1e-06) |
| `mech.bearing-life` | `ka-duty-cycle` | P [kN] | 3.301927 | 3.301927 | 7.5e-08 | agree (tol 1e-06) |
| `mech.bearing-life` | `ka-duty-cycle` | L_10h [h] | 9042.245 | 9042.245 | 4.1e-08 | agree (tol 1e-06) |
| `mech.bearing-life` | `ka-duty-cycle` | P_0 [kN] | 4 | 4 | 0.0e+00 | agree (tol 1e-09) |
| `mech.bearing-life` | `ka-skf-6206` | L_10 | 535.3873 | 535.3873 | 5.2e-08 | agree (tol 1e-06) |
| `mech.bearing-life` | `ka-skf-6206` | L_10h [h] | 5948.748 | 5948.748 | 1.5e-08 | agree (tol 1e-06) |
| `mech.bearing-life` | `ka-skf-6206` | s_0 | 4.48 | 4.48 | 2.0e-16 | agree (tol 1e-06) |
| `mech.weld-group` | `ka-two-vertical` | L_w [mm] | 200 | 200 | 0.0e+00 | agree (tol 1e-09) |
| `mech.weld-group` | `ka-two-vertical` | J_u [mm³] | 291666.7 | 291666.7 | 1.1e-07 | agree (tol 1e-06) |
| `mech.weld-group` | `ka-two-vertical` | T [kN·m] | -3.5 | -3.5 | 0.0e+00 | agree (tol 1e-09) |
| `mech.weld-group` | `ka-two-vertical` | f_r_max [N/mm] | 721.1103 | 721.1103 | 6.2e-08 | agree (tol 1e-06) |
| `mech.weld-group` | `ka-two-vertical` | tau [MPa] | 169.993 | 169.993 | 7.1e-08 | agree (tol 1e-05) |
| `mech.weld-group` | `ka-box-torsion` | J_u [mm³] | 1333333 | 1333333 | 2.5e-08 | agree (tol 1e-06) |
| `mech.weld-group` | `ka-box-torsion` | f_r_max [N/mm] | 106.066 | 106.066 | 1.6e-07 | agree (tol 1e-06) |
| `mech.weld-group` | `ka-two-horizontal-bending` | I_ux [mm³] | 500000 | 500000 | 0.0e+00 | agree (tol 1e-09) |
| `mech.weld-group` | `ka-two-horizontal-bending` | f_r_max [N/mm] | 502.4938 | 502.4938 | 3.8e-08 | agree (tol 1e-06) |
| `mech.weld-group` | `ka-two-horizontal-bending` | s_req [mm] | 3.23064 | 3.23064 | 7.1e-08 | agree (tol 1e-05) |
| `struct.connection-ec3` | `ka-m20-88-shear-bearing` | A_s [mm²] | 244.794 | 244.7944 | 1.6e-06 | agree (tol 1e-05) |
| `struct.connection-ec3` | `ka-m20-88-shear-bearing` | F_v_Rd [kN] | 94.00105 | 94.00105 | 3.8e-08 | agree (tol 1e-06) |
| `struct.connection-ec3` | `ka-m20-88-shear-bearing` | F_t_Rd [kN] | 141.0016 | 141.0016 | 2.2e-07 | agree (tol 1e-06) |
| `struct.connection-ec3` | `ka-m20-88-shear-bearing` | F_b_Rd [kN] | 99.39394 | 99.39394 | 6.1e-09 | agree (tol 1e-06) |
| `struct.connection-ec3` | `ka-m20-88-shear-bearing` | B_p_Rd [kN] | 199.8265 | 199.8265 | 1.5e-07 | agree (tol 1e-06) |
| `struct.connection-ec3` | `ka-m20-88-shear-bearing` | u_vt | 0.6838832 | 0.683883 | 3.4e-07 | agree (tol 1e-06) |
| `struct.connection-ec3` | `ka-m20-109-threads` | alpha_v | 0.5 | 0.5 | 0.0e+00 | agree (tol 1e-12) |
| `struct.connection-ec3` | `ka-m20-109-threads` | F_v_Rd [kN] | 97.91776 | 97.91776 | 3.4e-08 | agree (tol 1e-06) |
| `struct.connection-ec3` | `ka-weld-s275` | f_vw_d [MPa] | 222.7893 | 222.7893 | 8.8e-08 | agree (tol 1e-06) |
| `struct.connection-ec3` | `ka-weld-s275` | F_w_Rd [N/mm] | 935.715 | 935.715 | 2.4e-08 | agree (tol 1e-06) |
| `struct.connection-ec3` | `ka-p363-m20-88` | F_v_Rd [kN] | 94.1 | 94.00105 | 1.1e-03 | agree (tol 0.01) |
| `struct.connection-ec3` | `ka-p363-m20-88` | F_t_Rd [kN] | 141 | 141.0016 | 1.1e-05 | agree (tol 0.01) |
| `struct.connection-ec3` | `ka-p358-fin-plate` | F_b_Rd [kN] | 99.4 | 99.39394 | 6.1e-05 | agree (tol 0.01) |
| `struct.load-combinations` | `ka-610-single` | E_d_max [kN] | 21 | 21 | 0.0e+00 | agree (tol 1e-09) |
| `struct.load-combinations` | `ka-610-single` | E_d_min [kN] | 10 | 10 | 0.0e+00 | agree (tol 1e-09) |
| `struct.load-combinations` | `ka-610-single` | E_k_max [kN] | 15 | 15 | 0.0e+00 | agree (tol 1e-09) |
| `struct.load-combinations` | `ka-610-single` | E_fr_max [kN] | 12.5 | 12.5 | 0.0e+00 | agree (tol 1e-09) |
| `struct.load-combinations` | `ka-610-single` | E_qp_max [kN] | 11.5 | 11.5 | 0.0e+00 | agree (tol 1e-09) |
| `struct.load-combinations` | `ka-610ab-single` | E_d_max [kN] | 18.975 | 18.975 | 0.0e+00 | agree (tol 1e-09) |
| `struct.load-combinations` | `ka-610-two-variables` | E_d_max [kN] | 24.75 | 24.75 | 0.0e+00 | agree (tol 1e-09) |
| `struct.load-combinations` | `ka-610-two-variables` | E_k_max [kN] | 17.5 | 17.5 | 0.0e+00 | agree (tol 1e-09) |
| `struct.load-combinations` | `ka-uplift` | E_d_min [kN] | -20 | -20 | 0.0e+00 | agree (tol 1e-09) |
| `struct.load-combinations` | `ka-uplift` | E_d_max [kN] | 13.5 | 13.5 | 0.0e+00 | agree (tol 1e-09) |
| `struct.load-combinations` | `ka-designers-guide` | E_d_max [kN/m] | 37.65 | 37.65 | 0.0e+00 | agree (tol 1e-09) |
| `struct.base-plate` | `ka-ih-moment-bolts` | z_C | 121.5 | 121.5 | 0.0e+00 | agree (tol 1e-09) |
| `struct.base-plate` | `ka-ih-moment-bolts` | z | 286.5 | 286.5 | 0.0e+00 | agree (tol 1e-09) |
| `struct.base-plate` | `ka-ih-moment-bolts` | C_Ed | 521.815 | 521.815 | 5.0e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-moment-bolts` | T_Ed | 221.815 | 221.815 | 1.2e-10 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-moment-bolts` | F_t_Ed | 110.9075 | 110.9075 | 3.3e-10 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-moment-bolts` | C_Rd | 601.8358 | 601.8358 | 8.3e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-moment-bolts` | u_C | 0.8670388 | 0.8670388 | 3.9e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-moment-bolts` | M_pl_Rd | 5.5 | 5.5 | 0.0e+00 | agree (tol 1e-09) |
| `struct.base-plate` | `ka-ih-moment-bolts` | T_Rd_1 | 392.8571 | 392.8571 | 1.1e-10 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-moment-bolts` | T_Rd_3 | 282 | 282 | 0.0e+00 | agree (tol 1e-09) |
| `struct.base-plate` | `ka-ih-moment-bolts` | T_Rd | 282 | 282 | 0.0e+00 | agree (tol 1e-09) |
| `struct.base-plate` | `ka-ih-moment-bolts` | u_T | 0.786578 | 0.786578 | 3.5e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-moment-bolts` | alpha_bc | 0.248 | 0.248 | 1.1e-16 | agree (tol 1e-09) |
| `struct.base-plate` | `ka-ih-moment-bolts` | F_2_vb_Rd | 38.85307 | 38.85307 | 1.8e-16 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-moment-bolts` | F_vb_Rd | 38.85307 | 38.85307 | 1.8e-16 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-moment-bolts` | F_f_Rd | 60 | 60 | 0.0e+00 | agree (tol 1e-09) |
| `struct.base-plate` | `ka-ih-moment-bolts` | V_Rd | 215.4123 | 215.4123 | 1.3e-16 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-moment-bolts` | u_V | 0.3713809 | 0.3713809 | 4.3e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-moment-bolts` | V_b_Ed | 5 | 5 | 0.0e+00 | agree (tol 1e-09) |
| `struct.base-plate` | `ka-ih-moment-bolts` | t_req | 17.60887 | 17.60887 | 1.2e-10 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-moment-bolts` | u_max | 0.8670388 | 0.8670388 | 3.9e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-moment-prying` | T_Rd_1 | 785.7143 | 785.7143 | 1.8e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-moment-prying` | n_pry | 35 | 35 | 0.0e+00 | agree (tol 1e-09) |
| `struct.base-plate` | `ka-ih-moment-prying` | T_Rd_2 | 331.2698 | 331.2698 | 9.1e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-moment-prying` | T_Rd_3 | 282 | 282 | 0.0e+00 | agree (tol 1e-09) |
| `struct.base-plate` | `ka-ih-moment-prying` | T_Rd | 282 | 282 | 0.0e+00 | agree (tol 1e-09) |
| `struct.base-plate` | `ka-ih-moment-prying` | u_T | 0.786578 | 0.786578 | 3.5e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-both-compression` | C_Ed | 464.6091 | 464.6091 | 4.4e-12 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-both-compression` | C_Ed_2 | 135.3909 | 135.3909 | 1.5e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-both-compression` | T_Ed | 0 | 0 | 0.0e+00 | agree (tol 1e-09) |
| `struct.base-plate` | `ka-ih-both-compression` | F_t_Ed | 0 | 0 | 0.0e+00 | agree (tol 1e-09) |
| `struct.base-plate` | `ka-ih-both-compression` | u_C | 0.7719864 | 0.7719864 | 5.8e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-both-tension` | T_Ed | 210.6061 | 210.6061 | 2.9e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-both-tension` | T_Ed_2 | 89.39394 | 89.39394 | 4.4e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-both-tension` | F_t_Ed | 105.303 | 105.303 | 2.9e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-both-tension` | C_Ed | 0 | 0 | 0.0e+00 | agree (tol 1e-09) |
| `struct.base-plate` | `ka-ih-both-tension` | F_f_Rd | 0 | 0 | 0.0e+00 | agree (tol 1e-09) |
| `struct.base-plate` | `ka-ih-axial` | f_cd | 14.16667 | 14.16667 | 2.4e-10 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-axial` | f_jd | 14.16667 | 14.16667 | 2.4e-10 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-axial` | c | 50.8747 | 50.8747 | 6.1e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-axial` | A_eff | 98849.99 | 98849.99 | 2.6e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-axial` | N_j_Rd | 1400.375 | 1400.375 | 1.2e-10 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-axial` | u_N | 0.8569134 | 0.8569134 | 2.1e-13 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-axial` | t_req_N | 17.0754 | 17.0754 | 1.4e-10 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-axial` | t_req | 17.0754 | 17.0754 | 1.4e-10 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-axial` | F_t_Ed | 0 | 0 | 0.0e+00 | agree (tol 1e-09) |
| `struct.base-plate` | `ka-rhs-axial` | A_eff | 84929.55 | 84929.55 | 1.8e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-rhs-axial` | N_j_Rd | 1203.169 | 1203.169 | 5.2e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-rhs-axial` | u_N | 0.7480249 | 0.7480249 | 2.3e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-rhs-axial` | t_req | 14.46521 | 14.46521 | 2.9e-10 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-chs-axial` | A_eff | 73724.93 | 73724.93 | 4.8e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-chs-axial` | N_j_Rd | 1044.437 | 1044.437 | 4.4e-10 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-chs-axial` | u_N | 0.7659632 | 0.7659632 | 1.8e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-chs-axial` | t_req | 14.85924 | 14.85924 | 8.5e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-chs-moment` | z_C | 73.15465 | 73.15465 | 3.8e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-chs-moment` | z | 213.1546 | 213.1546 | 2.2e-10 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-chs-moment` | C_Ed | 412.8458 | 412.8458 | 5.9e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-chs-moment` | T_Ed | 212.8458 | 212.8458 | 1.1e-10 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-chs-moment` | F_t_Ed | 106.4229 | 106.4229 | 3.6e-10 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-chs-moment` | C_Rd | 522.2183 | 522.2183 | 5.5e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-chs-moment` | u_C | 0.7905618 | 0.7905618 | 5.5e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-chs-moment` | t_req | 15.39956 | 15.39956 | 8.6e-11 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-overlap-clip` | c [mm] | 101.7494 | 101.7494 | 1.4e-10 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-overlap-clip` | A_eff [mm²] | 106049.6 | 106049.6 | 1.1e-10 | agree (tol 1e-06) |
| `struct.base-plate` | `ka-ih-overlap-clip` | N_j_Rd [kN] | 1502.37 | 1502.37 | 2.8e-10 | agree (tol 1e-06) |

The `ka-shigley-7-1` inputs are not the book's (see G-A-13); with the book's own data (S_ut 105 kpsi = 723.95 MPa, S_y 82 kpsi = 565.4 MPa, d 1.100 in, M 1260 lbf·in, T 1100 lbf·in) the same relations give S_e = 201.50 MPa (29.23 kpsi) and n_f = 1.621 (DE-Goodman), the book's printed 1.62.

### Chain worked examples (issued sheets)

Units carried by each link were checked against the receiving input: kN·m → kN·m (`T_rms`→`T`, `T_pk`→`T_z`), rpm → rpm, kN → kN (`R_A`→`F_r`, `E_d_min`→`F_y` cell, `R1_*`→S6), mm → mm (`d_A`→`D`), mm² and MPa (S4b → S6). Signs: S5's downward effects are negative and enter S2's `F_y` as a downward load; `R1_y` (upward reaction, +28.2 kN) is S6's compression-positive `N_Ed`; `N_e1` = −28.2 kN is S3's tension-positive compression; `M_abs_e2` puts tension on the +y (top) bolt rows, which is the physical tension side of a hogging arm root. All consistent.

| Pack | Step | Symbol | Issued | Recomputed | Rel. diff | Verdict |
|---|---|---|---|---|---|---|
| drive-train | E1 | T_rms [N·m] | 151.58 | 151.579 | 6.9e-06 | agree |
| drive-train | E1 | T_pk [N·m] | 159.38 | 159.378 | 1.4e-05 | agree |
| drive-train | E1 | n_pk [rpm] | 1500 | 1500 | 0.0e+00 | agree |
| drive-train | M4 | R_A [kN] | 1.346 | 1.34604 | 3.0e-05 | agree |
| drive-train | M4 | M_max [kN·m] | 0.16826 | 0.168255 | 2.9e-05 | agree |
| drive-train | M4 | theta_A [rad] | 0.00026987 | 0.000269873 | 1.0e-05 | agree |
| drive-train | M4 | theta_B [rad] | 0.00026987 | 0.000269873 | 1.0e-05 | agree |
| drive-train | M4 | y_load [mm] | 0.021323 | 0.0213233 | 1.5e-05 | agree |
| drive-train | M4 | k_a | 0.89055 | 0.890546 | 4.2e-06 | agree |
| drive-train | M4 | k_e | 0.81389 | 0.813892 | 2.7e-06 | agree |
| drive-train | M4 | k_b (d 35) | 0.84763 | 0.84763 | 2.0e-07 | agree |
| drive-train | M4 | S_e (d 45) [MPa] | 254.18 | 254.179 | 2.6e-06 | agree |
| drive-train | M4 | X1 sigma_a [MPa] | 12.751 | 12.7513 | 2.4e-05 | agree |
| drive-train | M4 | X1 tau_m [MPa] | 26.108 | 26.108 | 1.7e-06 | agree |
| drive-train | M4 | X1 n_f | 9.8005 | 9.80047 | 3.0e-06 | agree |
| drive-train | M4 | X1 n_y | 13.835 | 13.8346 | 3.1e-05 | agree |
| drive-train | M4 | X2 sigma_a [MPa] | 29.998 | 29.9979 | 2.1e-06 | agree |
| drive-train | M4 | X2 n_f = n_f_min | 6.9906 | 6.99056 | 5.1e-06 | agree |
| drive-train | M4 | X2 n_y | 17.674 | 17.674 | 1.5e-06 | agree |
| drive-train | M4 | n_c = n_c_D [rpm] | 61948 | 61948 | 1.8e-07 | agree |
| drive-train | M4 | r_c | 41.299 | 41.2987 | 7.9e-06 | agree |
| drive-train | M5 | L_10 [Mrev] | 6799 | 6799.02 | 2.6e-06 | agree |
| drive-train | M5 | L_10h [h] | 75545 | 75544.6 | 4.8e-06 | agree |
| drive-train | M5 | a_1 (95 %) | 0.63791 | 0.637912 | 2.6e-06 | agree |
| drive-train | M5 | L_nmh [h] | 48191 | 48190.8 | 4.0e-06 | agree |
| drive-train | M5 | s_0 | 11.367 | 11.3667 | 2.9e-05 | agree |
| drive-train | M2 | IT7 [µm] | 24.98 | 24.9799 | 4.5e-06 | agree |
| drive-train | M2 | IT6 [µm] | 15.612 | 15.6124 | 2.8e-05 | agree |
| drive-train | M2 | ei k [µm] | 2.03 | 2.03002 | 8.6e-06 | agree |
| drive-train | M3 | J [mm⁴] | 234240 | 234240 | 0.0e+00 | agree |
| drive-train | M3 | v_max [kN] | 0.99611 | 0.996111 | 1.3e-06 | agree |
| drive-train | M3 | u_max | 0.071151 | 0.0711508 | 2.7e-06 | agree |
| steel-support | S5 | E_d_min [kN] | -28.2 | -28.2 | 1.3e-16 | agree |
| steel-support | S5 | E_d_max [kN] (γ_G,inf·G) | -12 | -12 | 0.0e+00 | agree |
| steel-support | S5 | E_fr_min [kN] | -19.2 | -19.2 | 0.0e+00 | agree |
| steel-support | S5 | E_qp_min [kN] | -18.4 | -18.4 | 0.0e+00 | agree |
| steel-support | S2 | R1_y = N_Ed(S6) [kN] | 28.2 | 28.2 | 1.3e-16 | agree |
| steel-support | S2 | R1_m_abs [kN·m] | 43.2 | 43.2 | 0.0e+00 | agree |
| steel-support | S2 | M_abs_e2 [kN·m] | 28.2 | 28.2 | 1.3e-16 | agree |
| steel-support | M3 | v_max [kN] | 4.7 | 4.7 | 0.0e+00 | agree |
| steel-support | M3 | t_max [kN] | 47 | 47 | 1.5e-16 | agree |
| steel-support | S4A | F_v_Rd [kN] | 94.001 | 94.001 | 4.9e-07 | agree |
| steel-support | S4A | F_b_Rd [kN] | 198.79 | 198.788 | 1.1e-05 | agree |
| steel-support | S4A | B_p_Rd [kN] | 399.65 | 399.653 | 7.7e-06 | agree |
| steel-support | S4A | u_t | 0.33333 | 0.33333 | 1.1e-06 | agree |
| steel-support | S4A | u_vt | 0.28809 | 0.288092 | 7.0e-06 | agree |
| steel-support | S4A | V_eff_Rd [kN] | 774.65 | 774.651 | 1.5e-06 | agree |
| steel-support | S4B | F_b_Rd [kN] (p_1 blank) | 248.48 | 248.485 | 2.0e-05 | agree |
| steel-support | S4B | B_p_Rd [kN] | 499.57 | 499.566 | 7.4e-06 | agree |
| steel-support | S6 | c [mm] | 63.593 | 63.5934 | 5.9e-06 | agree |
| steel-support | S6 | A_eff [mm²] | 90171 | 90170.7 | 3.1e-06 | agree |
| steel-support | S6 | N_j_Rd [kN] | 1277.4 | 1277.42 | 1.4e-05 | agree |
| steel-support | S6 | C_Ed [kN] | 203.41 | 203.405 | 2.2e-05 | agree |
| steel-support | S6 | T_Ed [kN] | 175.21 | 175.205 | 2.6e-05 | agree |
| steel-support | S6 | F_t_Ed [kN] | 87.603 | 87.6027 | 3.1e-06 | agree |
| steel-support | S6 | A_flange [mm²] | 41456 | 41456 | 6.4e-07 | agree |
| steel-support | S6 | u_C | 0.34634 | 0.346344 | 1.1e-05 | agree |
| steel-support | S6 | M_pl_Rd [kN·m] | 8.5938 | 8.59375 | 5.8e-06 | agree |
| steel-support | S6 | T_Rd_1 [kN] | 613.84 | 613.839 | 1.2e-06 | agree |
| steel-support | S6 | u_T | 0.62129 | 0.621289 | 1.6e-06 | agree |
| steel-support | S6 | t_req [mm] | 13.356 | 13.3563 | 2.3e-05 | agree |
| steel-support | S6 | F_2_vb_Rd [kN] | 38.854 | 38.8538 | 6.0e-06 | agree |
| steel-support | S6 | V_Rd [kN] | 161.06 | 161.055 | 3.1e-05 | agree |

### Findings

| ID | Severity | Location | Wrong number or formula | Correct | Fix | Status |
|---|---|---|---|---|---|---|
| G-A-01 | Medium | `WeldGroupCalculator.cs:142`, `:102`; `GroupGeometry.cs:27-37` (also `:17-24` for M3) | Out-of-plane bending per unit length is `f_z = F_z/L_w + M_x·y′/I_ux + M_y·x′/I_uy` about the centroidal x/y axes; the product moment `I_uxy` is never computed. That form holds only for groups symmetric about x or y. Equal-leg L group (two 100 mm welds meeting at the origin), `M_x` = 1 kN·m: the sheet gives `f_r,max` = 360 N/mm. A 150 × 50 L gives 1292 N/mm. The `collinear` guard tests `I_x > 0` and `I_y > 0` separately, so a single inclined weld passes, although `I_x·I_y − I_xy²` = 0 for it. | Unsymmetric bending: `f_z = F_z/L_w + [(M_x I_y − M_y I_xy)·y′ + (M_y I_x − M_x I_xy)·x′]/(I_x I_y − I_xy²)`. This gives 450 N/mm for the equal-leg L (the sheet is 20 % low) and 1500 N/mm for the 150 × 50 L (14 % low). The three KA cases are symmetric, so none of them can detect this. | Add `I_xy` (Σ l·x′_m·y′_m + l·Δx·Δy/12) to `GroupProperties`, use the general form, block when `I_x I_y − I_xy²` ≈ 0, and add an L-group KA case. M3 `OfPoints` shares the kernel, so apply the same fix to bolt-group bending. | Open |
| G-A-02 | Medium | `CalcTemplateHasher.cs:62-74` with `ShaftCalculator.cs:104`, `:377` | New sheets take the first test case's inputs before the declared defaults. `ka-simply-supported` sets `rho` = 0, so every new shaft sheet opens with ρ = 0 instead of the declared 7850 kg/m³, and the source column labels it "Sheet default value". The issued drive-train M4 shows exactly that. With no carried masses, `masses.Sum()` = 0, and n_c, r_c and the critical-speed warning are silently skipped. Drive-train: n_c = 61 948 rpm as issued. The same leak puts R = 95 % on the drive-train M5 (declared default 90 %) and a phantom `p_1` = 70 mm on the steel-support s4b, where the base plate has one bolt row per side. | Defaults first, then a test case only for inputs that have no default. With ρ = 7850 the drive-train shaft (3.25 kg) gives n_c = 50 988 rpm (−18 %). For a bare shaft the critical-speed check must run. | Fill spec defaults before the test-case fallback (or use the fallback only for tables). Regenerate the drive-train and steel-support worked examples. Warn when ρ = 0 and no masses are entered. | Open |
| G-A-03 | Medium | `BasePlateCalculator.cs:678-681` | `CheckCondition("Anchorage designed externally…", true, …)` passes whenever `profis_ref` is any non-empty text. It cannot fail. The issued steel-support S6 shows PASS against `PROFIS-BP1-PLACEHOLDER.txt`, and the pack index reports "6 of 6 PASS". | The anchorage is not designed on this sheet, so the sheet can only record a reference. It must not report a passed check. | Make it an assumption or note (or a check that needs a linked Evidence record of PROFIS type, confirmed by the checker). Leave it out of the PASS count. Refuse issue while the reference names a placeholder. | Open |
| G-A-04 | Medium | Drive train: `CalcPackExamples.cs:55-58`, `CalcPackTemplates.cs:31` | "Bearing A seat fit" is run as ISO 286 H7/k6 on `D = d_A` = 35 mm, which treats the bearing inner-ring bore as an H7 hole. The issued M2 classifies the fit as "transition" with c_max = +22.95 µm of clearance. | A rolling-bearing bore has its own tolerance (ISO 492 normal class, lying entirely below nominal, taken from the manufacturer's record), not H7. With k6 (ei = +2.03 µm by the ISO 286 formula) the seat is an interference fit at every size combination. | Give M2 a "bearing bore" hole option whose deviations come from a user record (ISO 492 class), or relabel the step as a hub or housing fit. Re-issue the example. | Open |
| G-A-05 | Low | Drive train: `CalcPackExamples.cs:37-43` | The pinion forces are fixed inputs (F_y = −0.92, F_z = 2.53 kN) while T arrives by link. They are consistent with T = 151.58 N·m only if "60 mm pitch" is the pitch radius: 2.53 × 0.060 = 151.8 N·m. Read as pitch diameter (the usual meaning), F_t = 2T/d = 5.05 kN, F_r = 1.84 kN and R_A = 2.69 kN (issued 1.346). Then L_5mh = 6 048 h < 20 000 h, which FAILS. A change in E1 also leaves the mesh forces stale. | F_t = 2T/d_p, F_r = F_t·tan φ, both derived from the linked torque. | Say "120 mm pitch diameter" in the comment and README. Better, add gear rows (d_p, φ) to M4 so the torque link drives the bearing loads. | Open |
| G-A-06 | Low | `CalcPackTemplates.cs:12-14`, `:32`; drive-train README; `ShaftCalculator.cs:332-338` | `T_pk` → M3 `T_z` is described as "the worst torque the motor produces", but `T_pk` is the load's run-up demand (159.4 N·m). The motor's capability on the same E1 sheet is `T_peak` = 400 N·m. M4 takes `T_rms` as its only torque, so the yield factor n_y never sees a peak torque. | Size the coupling and the first-cycle yield check for the motor's maximum torque (or a service-factor torque). At 400 N·m: coupling v = 2.50 kN (u = 0.179); shaft n_y = 5.28 at x = 50 mm. Both still pass, so only the meaning is wrong. | Link E1 `T_peak` (or SF·T_pk) to M3. Add a peak-torque input to M4 for n_y. Correct the README wording. | Open |
| G-A-07 | Low | `ShaftCalculator.cs:404-411` | `r_c = ω_Rayleigh/ω_op`, and the "< 2" warning tests it. Rayleigh is the upper bound. | Use the Dunkerley lower bound (n_c_D) for the ratio and warning, or warn when either bound is under 2. | As stated. | Open |
| G-A-08 | Low | `BearingLifeCalculator.cs:177`, `:212` | For F_a/F_r ≤ e the code forces P = F_r (X = 1, Y = 0). | ISO 281 gives a pair X₁, Y₁ for F_a/F_r ≤ e. It is (1, 0) for single-row radial ball bearings, but Y₁ > 0 for double-row angular-contact bearings and for paired or tapered arrangements, so P is low for those types. | Add `X_1`, `Y_1` record inputs (default 1, 0) and use `P = X_1 F_r + Y_1 F_a` below e. | Open |
| G-A-09 | Low | `ConnectionCalculator.cs:204` | Punching uses d_m = (s + 2s/√3)/2, with the theoretical across-corners of a sharp hexagon (34.64 mm for M20, d_m = 32.32 mm, B_p,Rd = 199.83 kN). | Table 3.4 takes d_m from the actual head or nut. The product-standard minimum across-corners is about 33 mm for M20, which gives B_p,Rd ≈ 194.6 kN; the sheet is 2.7 % unconservative. | Take e from a nut/head record, or use d_m = s (conservative), and state it. | Open |
| G-A-10 | Low | `BasePlateCalculator.cs:248` (I/H), `:227` (RHS) | z_C = (h − t_f)/2 even when the outer outstand is cut to the plate edge (`ch < c`), which shifts the strip centroid inward by (c − ch)/2. Example: h = 300, t_f = 17, L = 340, t_p = 20 (c = 50.87), N = 300 kN, M = 100 kN·m, x_b = 165: the sheet uses z_C = 141.5 and C_Ed = 487.8 kN. | z_C = (h − t_f)/2 − (c − ch)/2 = 126.1 mm, giving C_Ed = 513.6 kN. The sheet's C_Ed and T_Ed are 5.0 % low. The KA cases are unclipped on L. | Use the clipped strip's centroid, and add a clipped-moment KA case. | Open |
| G-A-11 | Low | `BasePlateCalculator.cs:81`, `:90-91` | Unsafe silent defaults. α = 1.5 raises f_jd by 50 % (a warning appears only when `alpha_basis` is blank). `l_eff` = 200 mm and `m` = 28 mm depend on geometry but are not derived from it, and the steel-support S6 inherits them as "Sheet default value". | For that plate (m_x 28, e_x 40, b_p 300, w 150, e 75) the EN 1993-1-8 Table 6.6 patterns give l_eff,1 = l_eff,2 = 150 mm (0.5·b_p governs). T_Rd_1 is then 460.4 kN, not 613.8 kN (the sheet is 33 % high). Mode 3 (282 kN) still governs. | Compute l_eff from the cl. 6.2.6.5 / Table 6.6 patterns (inputs m_x, e_x, w, e, b_p) and m from x_b, h and the weld, or make l_eff, m and α required inputs. Default α to 1.0. | Open |
| G-A-12 | Low | `CalcPackTemplates.cs:148` | The steel-support chain carries one ULS scalar (`E_d_min`, all actions unfavourable) into S2, and the 5 kN horizontal design action is typed into S2 outside S5. The favourable-permanent case (1.0·G, no imposed, plus the horizontal action), which governs holding-down tension for light columns, is never analysed. | Here the maximum case governs (T_Ed 175.2 kN against 111.8 kN for 1.0·G + H), so the issued result stands. | Run S2 under both S5 envelopes (`LoadCombinationsCalculator.FrameCombinations` already exists), and put the horizontal action in S5. | Open |
| G-A-13 | Info | `ShaftCalculator.cs:110-114` | `ka-shigley-7-1` takes S_ut 735 and S_y 574 MPa where the example's 105 and 82 kpsi are 723.95 and 565.4 MPa, so the case cannot be confirmed against the book. | With the book's data: S_e = 201.50 MPa (29.23 kpsi), n_f = 1.621. This reproduces the printed 1.62. | Use the book's data (d 27.94 mm, M 142.36 N·m, T 124.28 N·m) and confirm the case. | Open |
| G-A-14 | Info | See text | §A5 data audit. | — | — | Open |
| G-A-15 | Info | Various | Coverage notes (no defect in what is computed). | — | — | Open |

**G-A-14, §A5 data present in source, tests and docs.** None of the banned tables were found: no BS 7671 or IET tables, no Peterson charts, no Roark tables, no SKF/Schaeffler aISO diagrams, no NA tables. The following is published data:
- SCI P363 bolt resistances, 94.1 and 141 kN (`ConnectionCalculator.cs:99-103`; `docs/calcs/structural/struct.connection-ec3.md:15`). These are values copied from a copyrighted resistance table. Move them to the owner test pack.
- SKF 6206 C = 20.3 kN and C₀ = 11.2 kN, taken from a distributor listing (`BearingLifeCalculator.cs:86-90`; `docs/worked-examples/mechanical/mech.bearing-life/skf-6206-pump/`). §A5 allows one attributed manufacturer set only where the manufacturer's terms allow, and no terms are recorded.
- The UC 203 × 203 × 46 section (A 58.7 cm², I 4568 cm⁴, h 203.2, b 203.6, t_f 11, t_w 7.2) in `CalcPackExamples.cs:226-227`, `:238`, `:279`. This contradicts the file's own header (`:9`), which says "no tabulated … catalogue data appears". Cite it as a section record or use round illustrative numbers.
- Small factor sets, acceptable under §A5 but listed for completeness:
  - EN 1990 Table A1.1 recommended ψ, the whole table (`LoadCombinationsCalculator.cs:27-41`).
  - EN 1993-1-8 Table 4.1 β_w (`ConnectionCalculator.cs:26`) and the Table 3.3 minima (`:51-54`).
  - Shigley Table 6-2 Marin a, b (`ShaftCalculator.cs:301`).
  - ISO 261 pitches and ISO 4032 across-flats (`ConnectionCalculator.cs:17-21`).
  - The ISO 281 a₁ values 0.64/0.25 (2007) and 0.62/0.21 (1990), quoted in a text assumption.

**G-A-15, coverage notes.**
- S4 omits:
  - β_Lf (long joints, cl. 3.8) and β_p (packing, 3.6.1(12))
  - the oversize and slotted hole reductions
  - the Table 3.3 maxima
  - the eccentric block-tearing form (cl. 3.10.2(3)).
- The S4 default d₀ = d + 2 for M12 is conservative (EN 1090-2 normal clearance is 1 mm for M12 and M14).
- S6 links F_1,vb,Rd from an S4 run with normal clearance, while holding-down holes are usually oversized (the assumption is stated).
- M4 has no slope check at the gears. Its Rayleigh estimate uses gravity-down deflections for overhung masses (Shigley §7-6 reverses the overhang loads).
- M5 has no minimum-load check.

Verified correct (no finding):
- S5 favourable and unfavourable selection with ξ applied only to γ_G,sup, and K_FI applied only to unfavourable actions.
- The S6 Table 6.7 regime boundaries, which coincide with e = z_C (N > 0) and e = x_b (N < 0).
- The S6 Table 6.2 mode formulae, including n = min(e_min, 1.25m).
- The 6.2.2 α_bc formula.
- The M6 torsion term, which is independent of I_xy because J is invariant.
- M5 a₁, which reproduces the ISO 281:2007 tabulated factors from the formula.
- The S4 α_v and k₁ rules.

## Addendum A — building services and electrical

**Scope.** Ref `origin/v0.26.0/addA-8-integration` (22a3bbfb). B4 `bs.room-heat-loss` 1.0.0, B5 `bs.heat-exchanger` 1.1.0, B6 `bs.sealed-system` 1.1.0, E4 `elec.conductor-sizing` 1.0.0, E5 `elec.fault-protection` 1.0.0 and E6 `elec.load-budget` 1.1.0: every `TestCases` entry. For the base calculators changed in Addendum A, only the cases added since the pre-addendum Green baseline (b30acbd5): B1 `bs.pipe-pump` 1.1.0 `ka-heat-load-flow` and E1 `elec.motor-sizing` 1.1.0 `ka-continuous-pump`. E2 `elec.enclosure-heat` 1.2.0 gained a `P_add` input and output ports but no new case, so its change was reviewed by reading the code. The three pack chains were checked from `CalcPackTemplates.cs`, `CalcPackExamples.cs` and the issued sheets and `inputs.json` under `docs/worked-examples/packs/{motor-circuit,wet-heating,cooling}/`.

**Method.** This follows the main report. Each expected value was recomputed in a Python 3 script written for this review and kept in the scratchpad, never in the repository (`reviews/green-be/py/recompute.py`, `chains.py`). The formulas are my own, re-derived in my own notation and not ported from the C#:
- B4: series resistance with the ISO 6946 conventional R_si/R_se, and fabric plus ventilation loss.
- B5: ε-NTU closed forms and their inverses, the Bowman 1-2 F relation, and an independent check Q = UA·F·ΔT_lm.
- B6: the characteristic equation, Kell (1975) density from the published constants, cross-checked against IAPWS-95 (`iapws` package), and the vessel acceptance relation.
- E4: the I_b formulas, a selection loop, and the ASTM B258 AWG definition in inches.
- E5: loop impedance, C_min, IEC 60949 `k = √[Q_c(β+20)/ρ₂₀·ln((β+θ_f)/(β+θ_i))]`, log-log interpolation, and the IEC 60909 simplified transformer fault.
- E6: the load arithmetic and the Peukert relation solved for C.
- E1: the run-up kinematics.
- Chain steps: the same functions, plus IAPWS-95 viscosity and my own Colebrook iteration for B1.

The pass criterion is the repository's own (`KnownAnswerRunner.cs:80`): |mine − expected| ≤ tol·|expected|. Clause and unit audits were done by reading each `Run` method and the engine helpers it uses: `UnitCatalog` defines rpm as 2π/60 rad/s, `bar` as 1e5 Pa and degC with an offset, and the new-sheet seeding is in `CalcTemplateHasher.DefaultInputs`.

**Result.**
- **Known-answer cases:** 32 cases and 143 expected values: **143 PASS, 0 FAIL**. By template: B4 32/32, B5 18/18, B6 14/14, B1 1/1, E4 34/34, E5 15/15, E6 17/17, E1 12/12.
- **Chain worked examples:** 91 issued-sheet values were recomputed from the issued inputs and **all 91 agree** (within 5e-4, or 1e-2 for B1/E1 values that depend on the viscosity correlation; the largest difference was 1.6e-3 on a Reynolds number). Every link carries a dimensionally consistent quantity in the right unit, and the engine converts kW→W, kVA and degC correctly.
- **Borderline passes:** B5 `ka-parallel-balanced` Q sits at 70 % of its tolerance and B6 `ka-manufacturer-dt30` at 93 % (G-B-18).
- **The arithmetic is right; the defects are in method, defaults and chain wiring.** One High finding (G-B-01) and seven Medium findings follow.

### Recomputation table — known-answer cases


| Template | Case | Symbol | Expected | Recomputed | Rel. diff | Tol. | Verdict |
|---|---|---|---|---|---|---|---|
| `bs.room-heat-loss` | ka-cavity-wall | R_1 | 5.538045 | 5.538045113 | 2.0e-08 | 1e-06 | PASS |
| `bs.room-heat-loss` | ka-cavity-wall | U_1 | 0.1805691 | 0.1805691322 | 1.8e-07 | 1e-06 | PASS |
| `bs.room-heat-loss` | ka-cavity-wall | u_1 | 0.6018971 | 0.6018971073 | 1.2e-08 | 1e-06 | PASS |
| `bs.room-heat-loss` | ka-cavity-wall | Phi_1 | 86.67318 | 86.67318345 | 4.0e-08 | 1e-06 | PASS |
| `bs.room-heat-loss` | ka-cavity-wall | Phi_2 | 100.8 | 100.8 | 1.4e-16 | 1e-09 | PASS |
| `bs.room-heat-loss` | ka-cavity-wall | Phi_T | 187.4732 | 187.4731834 | 8.8e-08 | 1e-06 | PASS |
| `bs.room-heat-loss` | ka-cavity-wall | q_v | 20 | 20 | 0.0e+00 | 1e-09 | PASS |
| `bs.room-heat-loss` | ka-cavity-wall | Phi_V | 163.2 | 163.2 | 1.7e-16 | 1e-09 | PASS |
| `bs.room-heat-loss` | ka-cavity-wall | Phi_HL | 350.6732 | 350.6731834 | 4.7e-08 | 1e-06 | PASS |
| `bs.room-heat-loss` | ka-cavity-wall | HLC | 14.61138 | 14.61138264 | 1.8e-07 | 1e-06 | PASS |
| `bs.room-heat-loss` | ka-mvhr-partition | U_2 | 1.851852 | 1.851851852 | 8.0e-08 | 1e-06 | PASS |
| `bs.room-heat-loss` | ka-mvhr-partition | Phi_1 | 65.00489 | 65.00488759 | 3.7e-08 | 1e-06 | PASS |
| `bs.room-heat-loss` | ka-mvhr-partition | Phi_2 | 66.66667 | 66.66666667 | 5.0e-08 | 1e-06 | PASS |
| `bs.room-heat-loss` | ka-mvhr-partition | Phi_3 | 36 | 36 | 0.0e+00 | 1e-09 | PASS |
| `bs.room-heat-loss` | ka-mvhr-partition | A_tb | 21 | 21 | 0.0e+00 | 1e-09 | PASS |
| `bs.room-heat-loss` | ka-mvhr-partition | Phi_tb | 25.2 | 25.2 | 1.4e-16 | 1e-09 | PASS |
| `bs.room-heat-loss` | ka-mvhr-partition | Phi_T | 192.8716 | 192.8715543 | 2.4e-07 | 1e-06 | PASS |
| `bs.room-heat-loss` | ka-mvhr-partition | Phi_V | 51 | 51 | 1.4e-16 | 1e-09 | PASS |
| `bs.room-heat-loss` | ka-mvhr-partition | Phi_HL | 243.8716 | 243.8715543 | 1.9e-07 | 1e-06 | PASS |
| `bs.room-heat-loss` | ka-mvhr-partition | HLC | 10.16131 | 10.16131476 | 4.7e-07 | 1e-06 | PASS |
| `bs.room-heat-loss` | ka-dhdg-allowance | Phi_T | 187.4732 | 187.4731834 | 8.8e-08 | 1e-06 | PASS |
| `bs.room-heat-loss` | ka-dhdg-allowance | Phi_V | 163.2 | 163.2 | 1.7e-16 | 1e-09 | PASS |
| `bs.room-heat-loss` | ka-dhdg-allowance | Phi_HL | 385.7405 | 385.7405018 | 4.6e-09 | 1e-06 | PASS |
| `bs.room-heat-loss` | ka-dhdg-allowance | HLC | 16.07253 | 16.07252091 | 5.7e-07 | 1e-06 | PASS |
| `bs.room-heat-loss` | ka-single-layer-target | U_1 | 3.868538 | 3.868538172 | 4.4e-08 | 1e-06 | PASS |
| `bs.room-heat-loss` | ka-single-layer-target | u_1 | 0.9671345 | 0.967134543 | 4.4e-08 | 1e-06 | PASS |
| `bs.room-heat-loss` | ka-single-layer-target | Phi_1 | 928.4492 | 928.4491612 | 4.2e-08 | 1e-06 | PASS |
| `bs.room-heat-loss` | ka-single-layer-target | Phi_V | 4.08 | 4.08 | 0.0e+00 | 1e-09 | PASS |
| `bs.room-heat-loss` | ka-mvhr-infiltration | Phi_V | 91.8 | 91.8 | 1.5e-16 | 1e-09 | PASS |
| `bs.room-heat-loss` | ka-mvhr-infiltration | Phi_T | 192.8716 | 192.8715543 | 2.4e-07 | 1e-06 | PASS |
| `bs.room-heat-loss` | ka-mvhr-infiltration | Phi_HL | 284.6716 | 284.6715543 | 1.6e-07 | 1e-06 | PASS |
| `bs.room-heat-loss` | ka-mvhr-infiltration | HLC | 11.86131 | 11.86131476 | 4.0e-07 | 1e-06 | PASS |
| `bs.heat-exchanger` | ka-counter-balanced | NTU | 1 | 1 | 0.0e+00 | 1e-09 | PASS |
| `bs.heat-exchanger` | ka-counter-balanced | eps | 0.5 | 0.5 | 0.0e+00 | 1e-09 | PASS |
| `bs.heat-exchanger` | ka-counter-balanced | Q | 125.4 | 125.4 | 0.0e+00 | 1e-09 | PASS |
| `bs.heat-exchanger` | ka-counter-balanced | T_h_out | 50 | 50 | 0.0e+00 | 1e-09 | PASS |
| `bs.heat-exchanger` | ka-counter-balanced | dT_lm | 30 | 30 | 0.0e+00 | 1e-09 | PASS |
| `bs.heat-exchanger` | ka-counter-balanced | F | 1 | 1 | 0.0e+00 | 1e-09 | PASS |
| `bs.heat-exchanger` | ka-parallel-balanced | eps | 0.4323324 | 0.4323323584 | 9.6e-08 | 1e-06 | PASS |
| `bs.heat-exchanger` | ka-parallel-balanced | Q | 108.4282 | 108.4289555 | 7.0e-06 | 1e-05 | PASS |
| `bs.heat-exchanger` | ka-shell-tube | eps | 0.5399396 | 0.5399395561 | 8.1e-08 | 1e-06 | PASS |
| `bs.heat-exchanger` | ka-shell-tube | Q | 135.4168 | 135.4168407 | 3.0e-07 | 1e-05 | PASS |
| `bs.heat-exchanger` | ka-shell-tube | T_c_out | 36.19819 | 36.19818668 | 9.2e-08 | 1e-06 | PASS |
| `bs.heat-exchanger` | ka-shell-tube | F | 0.9234561 | 0.9234561052 | 5.6e-09 | 1e-06 | PASS |
| `bs.heat-exchanger` | ka-counter-sizing | NTU | 2 | 1.999998214 | 8.9e-07 | 1e-05 | PASS |
| `bs.heat-exchanger` | ka-counter-sizing | A_req | 8.36 | 8.359992533 | 8.9e-07 | 1e-05 | PASS |
| `bs.heat-exchanger` | ka-incropera-11-1 | T_h_out | 60 | 60 | 1.2e-16 | 1e-06 | PASS |
| `bs.heat-exchanger` | ka-incropera-11-1 | T_c_out | 40.2 | 40.20105314 | 2.6e-05 | 0.001 | PASS |
| `bs.heat-exchanger` | ka-incropera-11-1 | dT_lm | 43.2 | 43.1999855 | 3.4e-07 | 0.001 | PASS |
| `bs.heat-exchanger` | ka-incropera-11-1 | UA | 197.31 | 197.314881 | 2.5e-05 | 0.001 | PASS |
| `bs.sealed-system` | ka-dt50 | dT | 50 | 50 | 0.0e+00 | 1e-09 | PASS |
| `bs.sealed-system` | ka-dt50 | f_dT | 1 | 1 | 0.0e+00 | 1e-09 | PASS |
| `bs.sealed-system` | ka-dt50 | R1 | 1500 | 1500 | 0.0e+00 | 1e-09 | PASS |
| `bs.sealed-system` | ka-dt50 | u_emit | 0.9375 | 0.9375 | 0.0e+00 | 1e-09 | PASS |
| `bs.sealed-system` | ka-dt50 | V_s | 38 | 38 | 0.0e+00 | 1e-09 | PASS |
| `bs.sealed-system` | ka-dt30 | f_dT | 0.5147503 | 0.5147503203 | 3.9e-08 | 1e-06 | PASS |
| `bs.sealed-system` | ka-dt30 | R1 | 2914.034 | 2914.034127 | 4.3e-08 | 1e-06 | PASS |
| `bs.sealed-system` | ka-lmtd | dT | 29.72013 | 29.72013412 | 1.4e-07 | 1e-06 | PASS |
| `bs.sealed-system` | ka-lmtd | f_dT | 0.5085164 | 0.5085164293 | 5.8e-08 | 1e-06 | PASS |
| `bs.sealed-system` | ka-vessel | e_x | 0.03208619 | 0.03208619029 | 9.0e-09 | 1e-06 | PASS |
| `bs.sealed-system` | ka-vessel | p_i | 0.7901852 | 0.790185221 | 2.7e-08 | 1e-06 | PASS |
| `bs.sealed-system` | ka-vessel | V_e | 6.56806 | 6.568060318 | 4.8e-08 | 1e-06 | PASS |
| `bs.sealed-system` | ka-vessel | u_vessel | 0.8210075 | 0.8210075398 | 4.8e-08 | 1e-06 | PASS |
| `bs.sealed-system` | ka-manufacturer-dt30 | f_dT | 0.51 | 0.5147503203 | 9.3e-03 | 0.01 | PASS |
| `bs.pipe-pump` | ka-heat-load-flow | Q_design | 0.5277375 | 0.5277375396 | 7.5e-08 | 1e-06 | PASS |
| `elec.conductor-sizing` | ka-three-phase-mcb-c | I_b | 14.15074189 | 14.15074189 | 1.3e-10 | 1e-06 | PASS |
| `elec.conductor-sizing` | ka-three-phase-mcb-c | I_n | 16 | 16 | 0.0e+00 | 1e-09 | PASS |
| `elec.conductor-sizing` | ka-three-phase-mcb-c | I_t_req | 22.83430855 | 22.83430855 | 6.2e-11 | 1e-06 | PASS |
| `elec.conductor-sizing` | ka-three-phase-mcb-c | CSA | 4 | 4 | 0.0e+00 | 1e-09 | PASS |
| `elec.conductor-sizing` | ka-three-phase-mcb-c | I_z | 16.88687 | 16.88687 | 2.1e-16 | 1e-06 | PASS |
| `elec.conductor-sizing` | ka-three-phase-mcb-c | u_Ib | 0.8844213682 | 0.8844213682 | 5.0e-11 | 1e-06 | PASS |
| `elec.conductor-sizing` | ka-three-phase-mcb-c | u_In | 0.9474816825 | 0.9474816825 | 1.5e-11 | 1e-06 | PASS |
| `elec.conductor-sizing` | ka-single-phase | I_b | 15.25553013 | 15.25553013 | 2.2e-11 | 1e-06 | PASS |
| `elec.conductor-sizing` | ka-single-phase | I_n | 16 | 16 | 0.0e+00 | 1e-09 | PASS |
| `elec.conductor-sizing` | ka-single-phase | I_t_req | 16 | 16 | 0.0e+00 | 1e-09 | PASS |
| `elec.conductor-sizing` | ka-single-phase | CSA | 2.5 | 2.5 | 0.0e+00 | 1e-09 | PASS |
| `elec.conductor-sizing` | ka-single-phase | I_z | 17.7 | 17.7 | 0.0e+00 | 1e-09 | PASS |
| `elec.conductor-sizing` | ka-single-phase | u_In | 0.9039548023 | 0.9039548023 | 4.4e-11 | 1e-06 | PASS |
| `elec.conductor-sizing` | ka-dc | I_b | 25 | 25 | 0.0e+00 | 1e-09 | PASS |
| `elec.conductor-sizing` | ka-dc | I_n | 25 | 25 | 0.0e+00 | 1e-09 | PASS |
| `elec.conductor-sizing` | ka-dc | I_t_req | 27.47252747 | 27.47252747 | 9.2e-11 | 1e-06 | PASS |
| `elec.conductor-sizing` | ka-dc | CSA | 6 | 6 | 0.0e+00 | 1e-09 | PASS |
| `elec.conductor-sizing` | ka-dc | I_z | 29.029 | 29.029 | 0.0e+00 | 1e-09 | PASS |
| `elec.conductor-sizing` | ka-dc | u_In | 0.8612077578 | 0.8612077578 | 4.7e-11 | 1e-06 | PASS |
| `elec.conductor-sizing` | ka-semi-enclosed | I_b | 18 | 18 | 0.0e+00 | 1e-09 | PASS |
| `elec.conductor-sizing` | ka-semi-enclosed | I_n | 20 | 20 | 0.0e+00 | 1e-09 | PASS |
| `elec.conductor-sizing` | ka-semi-enclosed | C_c | 0.725 | 0.725 | 0.0e+00 | 1e-09 | PASS |
| `elec.conductor-sizing` | ka-semi-enclosed | I_t_req | 27.5862069 | 27.5862069 | 1.2e-10 | 1e-06 | PASS |
| `elec.conductor-sizing` | ka-semi-enclosed | CSA | 6 | 6 | 0.0e+00 | 1e-09 | PASS |
| `elec.conductor-sizing` | ka-semi-enclosed | I_z | 31.9 | 31.9 | 0.0e+00 | 1e-09 | PASS |
| `elec.conductor-sizing` | ka-semi-enclosed | I2 | 40 | 40 | 0.0e+00 | 1e-09 | PASS |
| `elec.conductor-sizing` | ka-semi-enclosed | u_I2 | 0.8647713761 | 0.8647713761 | 3.8e-11 | 1e-06 | PASS |
| `elec.conductor-sizing` | ka-derived-ca | C_a | 0.8660254038 | 0.8660254038 | 1.8e-11 | 1e-09 | PASS |
| `elec.conductor-sizing` | ka-derived-ca | I_t_req | 23.99377742 | 23.99377742 | 3.6e-11 | 1e-06 | PASS |
| `elec.conductor-sizing` | ka-derived-ca | CSA | 4 | 4 | 0.0e+00 | 1e-09 | PASS |
| `elec.conductor-sizing` | ka-derived-ca | I_z | 16.07083342 | 16.07083342 | 1.2e-10 | 1e-06 | PASS |
| `elec.conductor-sizing` | ka-awg-10 | CSA | 5.261154955 | 5.261154955 | 9.3e-11 | 1e-09 | PASS |
| `elec.conductor-sizing` | ka-awg-10 | I_n | 25 | 25 | 0.0e+00 | 1e-09 | PASS |
| `elec.conductor-sizing` | ka-awg-10 | I_z | 26.3 | 26.3 | 0.0e+00 | 1e-09 | PASS |
| `elec.fault-protection` | ka-mcb-b32 | Z_s | 0.7900823 | 0.790082272 | 3.5e-08 | 1e-06 | PASS |
| `elec.fault-protection` | ka-mcb-b32 | I_f | 276.5535 | 276.5534777 | 8.1e-08 | 1e-06 | PASS |
| `elec.fault-protection` | ka-mcb-b32 | Z_s_max | 1.365625 | 1.365625 | 0.0e+00 | 1e-09 | PASS |
| `elec.fault-protection` | ka-mcb-b32 | u_Zs | 0.5785499 | 0.5785499475 | 8.2e-08 | 1e-06 | PASS |
| `elec.fault-protection` | ka-mcb-b32 | k | 114.8361 | 114.8361278 | 2.4e-07 | 1e-06 | PASS |
| `elec.fault-protection` | ka-mcb-b32 | S_min | 0.7615538 | 0.7615537906 | 1.2e-08 | 1e-06 | PASS |
| `elec.fault-protection` | ka-fuse-curve | I_a | 220 | 220 | 0.0e+00 | 1e-09 | PASS |
| `elec.fault-protection` | ka-fuse-curve | Z_s_max | 0.9931818 | 0.9931818182 | 1.8e-08 | 1e-06 | PASS |
| `elec.fault-protection` | ka-fuse-curve | t_d | 0.1715774 | 0.1715774007 | 4.4e-09 | 1e-06 | PASS |
| `elec.fault-protection` | ka-fuse-curve | S_min | 0.9975407 | 0.9975407051 | 5.1e-09 | 1e-06 | PASS |
| `elec.fault-protection` | ka-fault-level | Z_T | 0.01587 | 0.01587 | 0.0e+00 | 1e-06 | PASS |
| `elec.fault-protection` | ka-fault-level | I_k3 | 15.94203 | 15.94202899 | 6.4e-08 | 1e-06 | PASS |
| `elec.fault-protection` | ka-fault-level | u_Icn | 0.6376812 | 0.6376811594 | 6.4e-08 | 1e-06 | PASS |
| `elec.fault-protection` | ka-wiring-matters-b32 | Z_s_max | 1.37 | 1.365625 | 3.2e-03 | 0.005 | PASS |
| `elec.fault-protection` | ka-schneider-k | k | 143 | 142.8740485 | 8.8e-04 | 0.005 | PASS |
| `elec.load-budget` | ka-mixed-buses | P_dem | 9.12 | 9.12 | 1.9e-16 | 1e-09 | PASS |
| `elec.load-budget` | ka-mixed-buses | S_dem | 10.73958 | 10.73958204 | 1.9e-07 | 1e-06 | PASS |
| `elec.load-budget` | ka-mixed-buses | I_3ph | 14.72031 | 14.72030925 | 5.1e-08 | 1e-06 | PASS |
| `elec.load-budget` | ka-mixed-buses | I_1ph | 1.830664 | 1.830663616 | 2.1e-07 | 1e-06 | PASS |
| `elec.load-budget` | ka-mixed-buses | I_DC | 5 | 5 | 0.0e+00 | 1e-09 | PASS |
| `elec.load-budget` | ka-mixed-buses | Phi | 0.927 | 0.927 | 2.4e-16 | 1e-09 | PASS |
| `elec.load-budget` | ka-mixed-buses | P_PSU_req | 150 | 150 | 0.0e+00 | 1e-09 | PASS |
| `elec.load-budget` | ka-mixed-buses | S_T_req | 13.42448 | 13.42447755 | 1.8e-07 | 1e-06 | PASS |
| `elec.load-budget` | ka-mixed-buses | S_peak | 54.85723 | 54.8572291 | 1.6e-08 | 1e-06 | PASS |
| `elec.load-budget` | ka-mixed-buses | C_req | 32.89474 | 32.89473684 | 9.6e-08 | 1e-06 | PASS |
| `elec.load-budget` | ka-battery | I_b | 7 | 7 | 0.0e+00 | 1e-09 | PASS |
| `elec.load-budget` | ka-battery | C_req | 51.16959 | 51.16959064 | 1.3e-08 | 1e-06 | PASS |
| `elec.load-budget` | ka-battery | u_batt | 0.9303562 | 0.9303561935 | 7.0e-09 | 1e-06 | PASS |
| `elec.load-budget` | ka-peukert | Ah | 26.15321 | 26.15320972 | 1.1e-08 | 1e-06 | PASS |
| `elec.load-budget` | ka-peukert | C_req | 43.01515 | 43.01514757 | 5.7e-08 | 1e-06 | PASS |
| `elec.load-budget` | ka-psu-derating | P_PSU_req | 333.3333 | 333.3333333 | 1.0e-07 | 1e-06 | PASS |
| `elec.load-budget` | ka-psu-derating | u_PSU | 1.388889 | 1.388888889 | 8.0e-08 | 1e-06 | PASS |
| `elec.motor-sizing` | ka-continuous-pump | n_pk | 2800 | 2800 | 1.6e-16 | 1e-09 | PASS |
| `elec.motor-sizing` | ka-continuous-pump | T_load | 0.8333333 | 0.8333333333 | 4.0e-08 | 1e-06 | PASS |
| `elec.motor-sizing` | ka-continuous-pump | J_refl | 0.0006944444 | 0.0006944444444 | 6.4e-08 | 1e-06 | PASS |
| `elec.motor-sizing` | ka-continuous-pump | J_total | 0.001194444 | 0.001194444444 | 3.7e-07 | 1e-06 | PASS |
| `elec.motor-sizing` | ka-continuous-pump | T_acc | 0.1751147 | 0.1751147016 | 9.2e-09 | 1e-06 | PASS |
| `elec.motor-sizing` | ka-continuous-pump | T_pk | 1.008448 | 1.008448035 | 3.5e-08 | 1e-06 | PASS |
| `elec.motor-sizing` | ka-continuous-pump | T_rms | 0.8333333 | 0.8333333333 | 4.0e-08 | 1e-06 | PASS |
| `elec.motor-sizing` | ka-continuous-pump | P_cont | 244.3461 | 244.3460953 | 1.9e-08 | 1e-06 | PASS |
| `elec.motor-sizing` | ka-continuous-pump | u_peak | 0.2100933 | 0.2100933406 | 1.9e-07 | 1e-06 | PASS |
| `elec.motor-sizing` | ka-continuous-pump | u_rms | 0.5208333 | 0.5208333333 | 6.4e-08 | 1e-06 | PASS |
| `elec.motor-sizing` | ka-continuous-pump | u_speed | 0.8 | 0.8 | 0.0e+00 | 1e-09 | PASS |
| `elec.motor-sizing` | ka-continuous-pump | J_ratio | 1.388889 | 1.388888889 | 8.0e-08 | 1e-06 | PASS |

<!-- chain -->
| Chain | Step | Quantity | Issued sheet | Recomputed | Rel. diff | Verdict |
|---|---|---|---|---|---|---|
| motor-circuit | E1 | T_load | 0.08671 | 0.08670982 | 2.0e-06 | agrees |
| motor-circuit | E1 | J_refl | 0.00014072 | 0.0001407239 | 2.7e-05 | agrees |
| motor-circuit | E1 | T_pk | 2.5789 | 2.578945 | 1.7e-05 | agrees |
| motor-circuit | E1 | T_rms | 1.4402 | 1.440198 | 1.3e-06 | agrees |
| motor-circuit | E1 | n_pk | 9000 | 9000 | 2.0e-16 | agrees |
| motor-circuit | E1 | P_pk kW | 2.4306 | 2.430598 | 8.2e-07 | agrees |
| motor-circuit | E1 | u_rms | 0.90012 | 0.9001238 | 4.3e-06 | agrees |
| motor-circuit | E1 | u_peak | 0.53728 | 0.5372801 | 2.2e-07 | agrees |
| motor-circuit | E6 | P_add/eta kW | 2.5058 | 2.505771 | 1.2e-05 | agrees |
| motor-circuit | E6 | S_dem kVA | 3.1724 | 3.17241 | 3.3e-06 | agrees |
| motor-circuit | E6 | I_3ph A | 4.255 | 4.255022 | 5.3e-06 | agrees |
| motor-circuit | E6 | Phi W | 105.173 | 105.1731 | 1.3e-06 | agrees |
| motor-circuit | E6 | P_PSU_req W | 225 | 225 | 0.0e+00 | agrees |
| motor-circuit | E4 | I_n | 6 | 6 | 0.0e+00 | agrees |
| motor-circuit | E4 | I_t_req | 8.5629 | 8.562866 | 4.0e-06 | agrees |
| motor-circuit | E4 | CSA | 1.5 | 1.5 | 0.0e+00 | agrees |
| motor-circuit | E4 | I_z | 8.6186 | 8.61861 | 1.2e-06 | agrees |
| motor-circuit | E4 | u_Ib | 0.70917 | 0.7091704 | 5.8e-07 | agrees |
| motor-circuit | E4 | u_In | 0.69617 | 0.6961679 | 3.0e-06 | agrees |
| motor-circuit | E3 | R ohm | 0.34379 | 0.3437943 | 1.3e-05 | agrees |
| motor-circuit | E3 | dV V (DC formula, as issued) | 2.9257 | 2.925705 | 1.8e-06 | agrees |
| motor-circuit | E3 | P_loss W (DC formula, as issued) | 12.449 | 12.44894 | 4.7e-06 | agrees |
| motor-circuit | E5 | R_12 | 0.5501 | 0.5501028 | 5.2e-06 | agrees |
| motor-circuit | E5 | Z_s | 0.9001 | 0.9001028 | 3.2e-06 | agrees |
| motor-circuit | E5 | I_f | 242.75 | 242.75 | 1.6e-07 | agrees |
| motor-circuit | E5 | Z_s_max (type B, as issued) | 7.2833 | 7.283333 | 4.6e-06 | agrees |
| motor-circuit | E5 | u_Zs (type B) | 0.12358 | 0.1235839 | 3.2e-05 | agrees |
| motor-circuit | E5 | k | 114.84 | 114.8361 | 3.4e-05 | agrees |
| motor-circuit | E5 | S_min | 0.66847 | 0.6684682 | 2.7e-06 | agrees |
| motor-circuit | E5 | u_cpc | 0.26739 | 0.2673873 | 1.0e-05 | agrees |
| motor-circuit | E5 | u_line | 0.44565 | 0.4456455 | 1.0e-05 | agrees |
| motor-circuit | E2 | P_total W | 185.17 | 185.1731 | 1.7e-05 | agrees |
| motor-circuit | E2 | A_eff m2 | 1.9 | 1.9 | 1.2e-16 | agrees |
| motor-circuit | E2 | dT_nat K | 17.72 | 17.71992 | 4.7e-06 | agrees |
| motor-circuit | E2 | u_nat | 0.886 | 0.8859959 | 4.7e-06 | agrees |
| motor-circuit | E2 | P_nat_max W | 209 | 209 | 1.4e-16 | agrees |
| motor-circuit | E2 | rho_air | 1.1455 | 1.145485 | 1.3e-05 | agrees |
| motor-circuit | E2 | V_req m3/h | 28.924 | 28.92432 | 1.1e-05 | agrees |
| wet-heating | B4a | Phi_HL W | 350.673 | 350.6732 | 5.2e-07 | agrees |
| wet-heating | B4b | Phi_HL W | 243.872 | 243.8716 | 1.8e-06 | agrees |
| wet-heating | B4b | u_2 (partition vs global 0.3) | 6.1728 | 6.17284 | 6.4e-06 | agrees |
| wet-heating | B6 | Phi_tot W | 594.545 | 594.5447 | 4.4e-07 | agrees |
| wet-heating | B6 | dT_fr K | 10 | 10 | 0.0e+00 | agrees |
| wet-heating | B6 | t_m degC | 70 | 70 | 0.0e+00 | agrees |
| wet-heating | B6 | e_x | 0.032086 | 0.03208619 | 5.9e-06 | agrees |
| wet-heating | B6 | p_i bar | 0.79019 | 0.7901852 | 6.0e-06 | agrees |
| wet-heating | B6 | V_e L | 2.4959 | 2.495863 | 1.5e-05 | agrees |
| wet-heating | B1 | Q_design m3/h | 0.052294 | 0.05229393 | 1.3e-06 | agrees |
| wet-heating | B1 | v m/s | 0.023591 | 0.02359081 | 8.1e-06 | agrees |
| wet-heating | B1 | Re (IAPWS mu) | 1599.9 | 1600.441 | 3.4e-04 | agrees |
| wet-heating | B1 | f | 0.040002 | 0.03998897 | 3.3e-04 | agrees |
| wet-heating | B1 | h_f m | 0.001962 | 0.001961477 | 2.7e-04 | agrees |
| wet-heating | B1 | H_sys m | 3.002 | 3.001961 | 1.3e-05 | agrees |
| wet-heating | B1 | P_hyd W | 0.41813 | 0.4181298 | 5.1e-07 | agrees |
| wet-heating | B1 | T_shaft N*m | 0.0020372 | 0.002037166 | 1.7e-05 | agrees |
| wet-heating | E1 | T_load | 0.0022635 | 0.002263518 | 7.8e-06 | agrees |
| wet-heating | E1 | J_total | 0.0024222 | 0.002422222 | 9.2e-06 | agrees |
| wet-heating | E1 | T_acc | 0.14205 | 0.1420465 | 2.4e-05 | agrees |
| wet-heating | E1 | T_pk | 0.14431 | 0.14431 | 3.3e-07 | agrees |
| wet-heating | E1 | P_cont W | 0.6637 | 0.6636981 | 2.9e-06 | agrees |
| wet-heating | E1 | J_ratio | 11.111 | 11.11111 | 1.0e-05 | agrees |
| wet-heating | E1 | u_peak | 0.030065 | 0.03006459 | 1.4e-05 | agrees |
| wet-heating | E1 | u_speed | 0.28 | 0.28 | 0.0e+00 | agrees |
| cooling | E6 | Phi W | 3510 | 3510 | 7.8e-16 | agrees |
| cooling | E6 | S_dem kVA | 117.81 | 117.807 | 2.5e-05 | agrees |
| cooling | E6 | I_3ph A | 170.04 | 170.0398 | 1.3e-06 | agrees |
| cooling | E2 | P_total W | 3660 | 3660 | 7.5e-16 | agrees |
| cooling | E2 | A_eff m2 | 4.32 | 4.32 | 0.0e+00 | agrees |
| cooling | E2 | P_nat_max W | 237.6 | 237.6 | 1.2e-16 | agrees |
| cooling | E2 | P_cool W | 3422.4 | 3422.4 | 8.0e-16 | agrees |
| cooling | E2 | u_nat | 15.404 | 15.40404 | 2.6e-06 | agrees |
| cooling | B5 | C_r | 0.38507 | 0.3850718 | 4.6e-06 | agrees |
| cooling | B5 | eps | 0.36978 | 0.3697813 | 3.5e-06 | agrees |
| cooling | B5 | NTU | 0.501 | 0.5010013 | 2.6e-06 | agrees |
| cooling | B5 | A_req m2 | 3.3802 | 3.380209 | 2.6e-06 | agrees |
| cooling | B5 | UA W/K | 201.6 | 201.6029 | 1.4e-05 | agrees |
| cooling | B5 | T_h_out | 36.495 | 36.49503 | 8.2e-07 | agrees |
| cooling | B5 | T_c_out | 25.275 | 25.27502 | 9.5e-07 | agrees |
| cooling | B5 | dT_c K | 3.27502 | 3.275024 | 1.2e-06 | agrees |
| cooling | B5 | T_c_m | 23.6375 | 23.63751 | 5.1e-07 | agrees |
| cooling | B5 | dT_lm K | 16.976 | 16.97594 | 3.3e-06 | agrees |
| cooling | B5 | dT_app K | 14.495 | 14.49503 | 2.1e-06 | agrees |
| cooling | B5 | Q_lmtd = U_f A dT_lm (W) | 3422.4 | 3422.4 | 1.3e-16 | agrees |
| cooling | B1 | Q_design m3/h | 0.90236 | 0.9023593 | 7.5e-07 | agrees |
| cooling | B1 | v m/s | 0.51063 | 0.5106309 | 1.8e-06 | agrees |
| cooling | B1 | Re (IAPWS mu) | 13842 | 13864.28 | 1.6e-03 | agrees |
| cooling | B1 | f (Colebrook) | 0.031412 | 0.03140222 | 3.1e-04 | agrees |
| cooling | B1 | h_f m | 0.66065 | 0.6604933 | 2.4e-04 | agrees |
| cooling | B1 | H_sys m | 3.6606 | 3.660493 | 2.9e-05 | agrees |
| cooling | B1 | P_hyd W | 8.9747 | 8.974294 | 4.5e-05 | agrees |
| cooling | B1 | T_shaft N*m | 0.043725 | 0.04372357 | 3.3e-05 | agrees |

### Findings

| ID | Sev. | Where | Finding | Fix |
|---|---|---|---|---|
| G-B-01 | High | `CalcPackTemplates.cs` motor circuit; `CalcPackExamples.cs` MotorE5 | E4 selects an MCB type C (6 A) but E5 checks type B (its seeded default): Z_s,max 7.283 Ω instead of 3.642 Ω, u_Zs 0.124 instead of 0.247; loop impedances between 3.64 and 7.28 Ω would pass falsely. | Carry the device type E4 → E5 (or require it in the pack and refuse disagreement). |
| G-B-02 | Medium | `ConductorSizingCalculator.cs` ~356/374/396 | The gG "deemed I₂ ≤ 1.45 I_z" rule (BS 7671 433.1.201) is applied to any gG fuse in either context; IEC 60269-1 gG I₂ = 1.6 I_n. `ka-awg-10` would give u_I2 1.049 (FAIL). | Deem only in the BS 7671 context for BS 88 fuses; otherwise require I₂ and check. |
| G-B-03 | Medium | `FaultProtectionCalculator.cs` ~177/221–226/256 | Adiabatic checks use the minimum end-of-line fault × 0.1 s labelled "conservative"; reg. 434.5.2 for the line conductor needs the let-through at the maximum fault (3×10⁴ A²s needs 1.51 mm²; 1.5 mm² shows PASS 0.45). | Take I²t let-through at the maximum prospective fault for the line check; reword. |
| G-B-04 | Medium | `LoadBudgetCalculator.cs` ~173; motor-circuit chain | E6 Φ = Σ(1−η)P counts a linked motor's own loss as panel heat and electronics at (1−η) of input; corrected E2 ΔT ≈ 26 K FAIL instead of 17.7 K PASS. | Per-row "inside enclosure" flag and heat fraction; exclude the linked motor. |
| G-B-05 | Medium | `CalcPackExamples.cs` MotorE3 | E3 runs as DC on a 400 V three-phase circuit (ΔV 2.926 V vs 2.154 V; P_loss 12.45 W vs 18.67 W). | Set/link `system` = three-phase. |
| G-B-06 | Medium | `CalcTemplateHasher.cs` 62–72 | New sheets and pack steps are seeded from the first known-answer case and labelled "Sheet default value": root of G-B-01/05, synthetic E4 factors, E6 4 h battery, B4 U_target 0.3. | Seed from declared defaults only. |
| G-B-07 | Medium | `HeatExchangerCalculator.cs` ~131/173 | C_r = 0 arrangement keeps C_min = min(C_h, C_c) and computes the constant-temperature stream's outlet: 33.2 kW vs 127.2 kW correct. | Name the constant-temperature stream; C_min = the other stream; its outlet = inlet. |
| G-B-08 | Medium | `SealedSystemCalculator.cs` ~216/222/245 | Cites BS EN 12828 on the vessel but omits the water reserve (≥ 0.5 % V_s, ≥ 3 L) and the safety-valve margin: `ka-vessel` 6.57 L vs 12.71 L; wet-heating 2.50 L vs 8.64 L (Annex D to be verified). | Add V_wr and p_f ≤ p_SV − margin, or cite CIBSE B1 only and state the omission. |
| G-B-09 | Low | `RoomHeatLossCalculator.cs` ~355 | The global U_target is applied to internal partitions: issued wet-heating pack carries FAIL u 6.17. | Apply it to external elements only. |
| G-B-10 | Low | `CalcPackExamples.cs` wet heating, cooling | 3 m static lift on closed loops (99.9 % / 82 % of pump head). | dz = 0 for closed loops, or a closed-circuit option. |
| G-B-11 | Low | motor circuit E1 P_pk → E6 | Instantaneous peak (2.43 kW) linked as continuous demand (cycle average 27 W). | Link a thermal-equivalent continuous power. |
| G-B-12 | Low | `LoadBudgetCalculator.cs` ~167 | AC bus currents omit the PSU input feeding DC loads. | Add P_DC/η_PSU to a selectable AC bus. |
| G-B-13 | Low | `ConductorSizingCalculator.cs` ~374 | MCCB/MPCB without I₂ only warns; deemed condition not stated. | State it explicitly in the BS 7671 context; require I₂ elsewhere. |
| G-B-14 | Low | `RoomHeatLossCalculator.cs` ~327 | Blank R_si = 0.13 for every element; upward flow (roofs) is 0.10. | Per-element flow-direction choice. |
| G-B-15 | Medium (§A5) | `ConductorSizingTests.cs` ~233 | An ampacity set 21/28/36/50 A matches a published 70 °C PVC column. | Replace with synthetic values. |
| G-B-16 | Low (§A5) | `ConductorSizingTests.cs` ~214/224; B4 layer λ values; B6 0.51 factor | AWG 20/30 A (NEC), C_g 0.65 (4 circuits), common λ values, an unattributed catalogue factor. | Replace with invented values; attribute or drop the factor. |
| G-B-17 | Low | wet-heating chain | B6 system volume not linked from B1 pipework (20 L default vs 24.6 L). | B1 `V_pipe` port linked to B6. |
| G-B-18 | Info | B5, B6, E1, E5 | Loose expected values (B5 parallel Q at 70 % of tolerance; B6 0.51 at 93 %); direct-drive E1 keeps η 0.9; E5 √3·U₀ 398.4 V; Kell vs IAPWS negligible; 2.5 mm² CPC on 1.5 mm² line. | Tighten; note. |

## Addendum B

**Scope.**
- The 14 Addendum B calculators at `34f196cc` (`v0.26.0/addB-4-workflows`): M7 `mech.spring` 1.2.0, M8 `mech.snap-fit` 1.3.0, M9 `mech.mechanism` 1.2.0, S7 `struct.ltb-interaction` 1.1.0, S9 `struct.plate-bending` 1.2.1, B7 `bs.glaser` 1.2.0, B8 `bs.pipe-expansion` 1.2.0, B9 `bs.insulation` 1.1.0, E7 `elec.pcb-trace` 1.0.1, E8 `elec.containment` 1.1.0, E9 `elec.motor-start` 1.2.0, F1 `fp.cylinder` 1.3.0, F2 `fp.accumulator` 1.2.0 and F3 `fp.oring-gland` 1.2.0.
- The W2 statistics.
- The W3 seed materials.
- The §B7 chain packs.
- W1 does no arithmetic beyond unit conversion; that is covered by the security re-verification.
- Read-only.
- S8: reviewed separately in three Opus rounds (Security and Red sections, S8 lifting); no open Critical, High or Medium finding; the flag stays off by default.

**Method.**
- At least one worked example per calculator was run through `CalcRunner.Run` from its `inputs.json`, using a scratch harness outside the repository.
- Its outputs were recomputed in python3 from the relations in my own notation, not ported from the C#. The relations used:
  - Bergsträsser factor, Goodman in shear and both-ends-fixed surge for M7.
  - Simpson integration of a tapered cantilever for M8.
  - A Newton solution of the loop equations with an analytic velocity and acceleration loop, and a 0.01° scan polished by golden section for the peak torque, for M9.
  - EN 1993-1-1 6.3.1, 6.3.2.3 and Annex B with the end-moment C₁ fit for S7.
  - A 400 × 400-term Navier series for S9.
  - Monthly Glaser accumulation by a lower-hull construction for B7.
  - Guided cantilever and screening ratio for B8.
  - Bisection on the cylinder resistance, Magnus dew point and a 1.32·(Δθ/D)^¼ plus radiation surface balance for B9.
  - The IPC-2221 legacy fit for E7.
  - Capstan pull in both directions for E8.
  - A complex divider for E9.
  - Areas, Johnson buckling, pump power and the cap-port flow for F1.
  - Gas law with temperature scaling for F2.
  - Incompressible-ring section reduction and the cold-end worst case for F3.
  - Mean, s, Cp, Cpk and Anderson–Darling A*² with its p-value for W2.
- The pass criterion is the repository's own relative tolerance.
- Scripts: `scratchpad/b9colour/*.py`; table `scratchpad/b9colour/green-table.md`.

**Result.**
- 132 of 132 recomputed values agree.
- The worst agreements are a finite-difference acceleration (2e-6) and the classical plate coefficient used as a cross-check (5e-4). Every other value agrees to 1e-9 or better.
- The defects are in a model choice (G-AB-01) and chain coverage (G-AB-02), not in the pinned numbers.

| Calculator | Worked example | Values | Worst rel. diff |
|---|---|---|---|
| `mech.spring` | `plunger-return-spring` | 11 (k, k_c, L_s, S_a, F_c, τ₂, τ_c, u_c, f_n two ways, n_f) | 3e-16 |
| `mech.snap-fit` | `enclosure-lid-hook` | 6 (P, ε, u, W, W′, self-locking angle) | 1e-15 |
| `mech.mechanism` | `carton-tucker-crank-rocker` | 12 (θ₃, θ₄, ω₄, α₄, μ, T_in at 0°; peak T_in; T_rms; μ_min; θ₄ limits) | 2e-6 (finite-difference α₄) |
| `struct.ltb-interaction` | `ub-457x191x98-beam-column` | 13 (C₁, M_cr, λ̄_LT, χ_LT, M_b,Rd, χ_y, χ_z, k_yy, k_yz, k_zy, k_zz, 6.61, 6.62) | 5e-16 |
| `struct.plate-bending` | `enclosure-lid-aluminium` | 10 (D, w, M_x, M_y, M_xy, σ_vm, f₁, u_σ, u_w; classical coefficient) | 5e-4 (coefficient) |
| `bs.glaser` | `internal-insulation-no-vcl` | 10 (R_T, f_Rsi, M_c for Nov–Jun) | 7e-15 |
| `bs.pipe-expansion` | `lthw-main-expansion-loop`, `copper-axial-bellows` | 8 (ΔL, F_r, L_req, anchor F and M, σ_leg, screening; bellows anchor) | 0 |
| `bs.insulation` | `chilled-water-pipe-condensation`, `clad-hot-water-pipe-correlation` | 9 (θ_dew, t_q, t_dew, q, θ_se; h_c, h_r, q, θ_se) | 1e-12 |
| `elec.pcb-trace` | `motor-driver-supply-rail` | 5 | 4e-16 |
| `elec.containment` | `servo-cable-pull-three-bends` | 7 (fill, T and SWP both directions, u_T, u_P) | 0 |
| `elec.motor-start` | `pump-dol-500kva` | 8 (I_n, Z_m, Z_src, I, dip, PCC dip, kvar, h_res) | 2e-15 |
| `fp.cylinder` | `press-ram-125-90-pump-and-lines` | 10 | 0 |
| `fp.accumulator` | `clamp-circuit-bladder-accumulator` + both pack F2 steps | 7 | 4e-16 |
| `fp.oring-gland` | `locking-cylinder-piston-seal` | 6 (nominal stretch, section reduction, squeeze; worst stretch; cold-end stretch and minimum squeeze) | 4e-15 |
| W2 statistics | `gom-two-stage.csv`, both stages | 10 (mean, s, Cp, Cpk, A*², p) | 6e-14 |

W3 seeds: the four generic materials satisfy G ≈ E/(2(1 + ν)) within 1.3 %, are flagged indicative and are unlicensed. Temperature tables are read linearly and never extrapolated (`MaterialLibrary.cs:479-507`).

**Opus reviews of each item and how they were closed.** Each closure was checked in the code at `34f196cc`.

| Item | Review findings | Closed | Evidence |
|---|---|---|---|
| B-0 W3, gas law, stack engine | 6 Medium, 9 Low: material values credited to their own source; a temperature needs its unit; unit-free property staleness; malformed files; one licensed value licenses the record; licensed values withheld through links | `64342b37` | `MaterialLibraryReviewTests` (Review_M1…M5, LOW4–6); `LicensedLinkTests` (7 tests, any depth, table cells) |
| B-1 S7 | High: Annex A class 3 k_yz/k_zy carried a wrong 0.6 (40 % low); Medium: χ_LT,mod cap, an entered C₁ needs k_c and C_my, warping constant, Table 6.2 curves | `f7ccb5c8` (1.1.0) | `LtbInteractionTests.Review_H1_Annex_A_Class_3_*`, `Review_M1_*`, `Review_M7_*`, `Review_Low_*` |
| B-1 M7 | Medium: R_m(d) from the W3 curve; EN solid length and coil clearance; torsion q factor; travel utilisation | `f7ccb5c8` | `SpringTests.Review_M6_*`, `Review_Low_The_En_*`; `SpringMaterialTests.Review_M6_*` |
| B-1 B7, E7, F2 | High: F2 bar·L basis (now the vessel's PS × V); Medium: B7 R_si,f, moisture excess with dp_f, calendar month lengths, timber flag; E7 range warnings; F2 T_min scaling, f_rg ≥ 1 | `152d845f` | `AccumulatorTests` 173, 206, 235; `GlaserTests` 272, 292; `PcbTraceTests` 112 |
| B-2 F1 | High: rod buckling at max(push, stall thrust); Medium: cap-port line at Q·A_b/A_a | `a6e8a825`, `2f127253` | `CylinderTests` 207, 405; recomputed (u_buck at p·A_b) |
| B-2 F3 | Medium: hot fill on a volume basis, Parker quadratic range, held OD, face-seal pressure side; Low L5 (second metal α) | `a6e8a825` | `ORingGlandTests` 433, 460, 518, 656; L5 accepted: one W3 material per sheet, α_s typed |
| B-2 HPU pack | Three verification rounds: B1 on F1's own line; E4 on the nameplate | `2f127253`, DB-03 | `HydraulicPowerUnitChainTests` |
| B-3 M8 | Medium: torsion bar held at both ends; K_ann on the strain, not the force; derived relations credited to the guide | `cd3026a9`, `0f12b92f` | `SnapFitTests` 245, 322, 522 |
| B-3 B8 | High: screening Y = α·ΔT·U; Medium: run check, axial bellows layout, E at the reference temperature, no PASS without K1; second round: resultant anchor force, Z-bend | `7ee5afe4`, `118f3438` | `PipeExpansionTests.H1_*`, `M1_*`…`M4_*`, `N1_*`, `N2_*` |
| B-3 S9 | Medium: Navier convergence warning; Ritz cap 30 and clamped-patch minimum 25 % | `f53fc0f9` | `PlateBendingTests.Review_M1_*`, `Review_M2_*` |
| B-3 E8 | High: bundle limit N × weakest; Medium: a cable must fit, a zero T_max is refused, f_max 0.45 replaced by an invented 0.42 | `8969bcda`, `5293fea7` | `ContainmentTests` 130, 163, 203; `ka-trunking-fill` |
| B-3 B9 | Medium: a chosen thickness with no criterion, an unattainable criterion, the bare-pipe window, no condensation check, a θ_dew input for B2 | `d71cd8ac`, `e789bc51` | `InsulationTests` 235, 264, 284, 308; `no-condensation-check` warning seen in a run |
| B-4 E9 | High: soft starter and drive were fixed impedances (dip understated); Medium: c factor, drive PF, `I_start` port, entered I_n | `d2622586`, DB-04 | `MotorStartTests` 276, 302, 372, 402, 417 |
| B-4 M9 | High: flat-faced cusp check could not fail; peaks and ports unbounded at a toggle; Medium: change-point branch, force ports, port meaning | `90318147`, `7c4a0fc4`, `4c53f72e`, DB-04 | `MechanismTests` 444, 488, 546, 558, 852; probe: ports withheld at a toggle |
| B-4 W2 | High: whole-number decimal comma, O(n²) statistics; Medium: provenance, k at import, same file twice, σ with outliers, M2 push | `2ec42994`…`7da71ee1` | B9 security re-verification §1; `ScanReviewTests` 165, 193, 374; `ScanToSealChainTests` 386 |
| §B7 chains 7–10 | Medium: S9 not downstream, PSU heat missing, mean in place of line temperature, issue gate | `00cfac32`, DB-05 | `ElectronicsEnclosureChainTests` 242, 259; `ColdPipeChainTests` 257; `*Issue_Is_Refused_At_The_Staleness_Gate*` in each |
| §B7 chains 1, 4, 5 | Medium: star-delta physics, stiffest source, motor above nameplate speed, cam RMS torque into M4, spring data typed twice | `56e038b1`, `c97c958e`, `d6ab772b`, `f8d675a2`, DB-06 | HPU unloaded-start assertions; `MotorSizingCalculator.cs:278`; `MechanismReviewChainTests` 373; `SpringCalculator.cs:664` |
| B-5 S8 | — | S8: reviewed in three separate Opus rounds (see Security and Red, S8 lifting) | — |

### Findings

| ID | Severity | Location | Description | Evidence | Fix or justification | Status |
|---|---|---|---|---|---|---|
| G-AB-01 | High | `AccumulatorCalculator.cs:216, 337-341`; `GasLaw.cs:136` | Emergency stroke sizes V0 with one exponent from p0 to both p1 and p2. A held accumulator is charged slowly (isothermal) and discharged fast (n), which needs V0 = ΔV·(p2/p0)/[(p2/p1)^(1/n) − 1]. Unconservative. | HPU pack 26.70 L against 30.08 L (−12.7 %); actuated-linkage 1.078 against 1.310 L (−21.6 %); `ka-adiabatic` 36.7 against 46.7 L (−27.4 %). Both packs issued PASS. | Separate charge and discharge exponents in Emergency stroke, and as a choice in Energy storage. New known-answer case (Unconfirmed). Re-pin the two packs and their chain tests. | Closed (`4a19d7b8`, DB-09; verified) |
| G-AB-02 | Medium | `CadDrivenToleranceChainTests.cs`; `ScanToSealChainTests.cs`; `docs/worked-examples/packs/` | Chain 3 has no M8 leg. Chains 2 and 3 do not prove Issue blocked while stale and have no issued worked example. | No test maps CAD parameters into `mech.snap-fit`; the stale tests stop at the check. | Add the M8 leg and the Issue refusal; add the examples, or record a DECISIONS row. | Closed (`6819c014`, DB-10; verified) |
| G-AB-03 | Low | `ScanStatistics.cs:108-121` | Pooled within-subgroup σ without c4: Cp and Cpk about 3 % optimistic at ten parts. Documented. | Ten-value case: σ_w 0.0200 against 0.0206 unbiased. | Apply c4(d), or print "not bias-corrected" beside Cp and Cpk. | Closed (`df1f6424`, DB-11; verified) |

### Verification at `6a189078`

Each fix was checked in a fresh worktree (`scratchpad/b9colv`) with the scratch harness and independent python3 (`scratchpad/b9colour/f2_verify.py`).

| ID | Fix | Verdict |
|---|---|---|
| G-AB-01 | `4a19d7b8`: `fp.accumulator` 1.3.0 with a `process` input (by duty, one exponent, slow charge) and `GasLaw.ChargeVolumeSplit`; DB-09; pack READMEs `0b6e19cd`. | Verified. HPU F2 30.080873 L and actuated-linkage F2 1.310207 L both equal the held-accumulator recompute (rel. diff ≤ 1.2e-16). The new Unconfirmed case `ka-held-slow-charge-fast-discharge` gives 46.743040 L, as recomputed. The sheet states the gas process as an output, and the pack READMEs quote 30.1 L and 1.310 L. Closed. |
| G-AB-02 | `6819c014`: M8 leg of the CAD chain; Issue refused while stale in chains 2 and 3; DB-10 records them as integration tests, not packs. | Verified: `CadDrivenToleranceChainTests.The_M8_Hook_Takes_Its_Beam_From_The_File_…`, `Issue_Is_Refused_While_A_Checked_Fit_Is_Stale_…`, `ScanToSealChainTests.Issue_Is_Refused_While_A_Checked_Gland_Is_Stale_…`. Closed. |
| G-AB-03 | `df1f6424`: pooled σ divided by c4(d); DB-11. | Verified: c4(9) = 0.969311, σ_w 0.0206332, Cp 0.80776, Cpk 0.75929 by python3 `math.gamma`, equal to the pinned values; c4(7) = 0.959369 likewise. Closed. |

No Green item is open.

S8: reviewed separately in three Opus rounds (Security and Red sections, S8 lifting); no open Critical, High or Medium finding; the flag stays off by default.

> The sections below were written on `release/v0.27.0` (23–24 October 2026) for the legacy calculator replacement (ADR-0167). The owner decided that nothing ships as 0.27.0: the work is part of v0.26.0 and is kept here, unchanged, as a marked part of this file.

## v0.27.0 Phase 5 — legacy vectors recomputed (2026-10-23)

Every "Computes" vector of the 14 legacy module test suites was traced to a worksheet known-answer case (a read-only inventory, then each gap closed and run). All 19 legacy calculations are now carried at a relative tolerance of 5 × 10⁻⁵ or tighter; `CommonCalculatorTests.Every_Known_Answer_Case_Passes` runs them.

| ID | Sev | Location | Finding | Fix | Status |
|---|---|---|---|---|---|
| G-05 | High | `mech.bolted-joint` | After separation the template still took the bolt load as F_M + Φ·F_A and reported a negative clamp; the legacy module (correctly) gave the bolt the whole working load and zero clamp. The overloaded worked example showed yield at 89 % (pass) where the bolt is at 115 % of proof. | `F_KR = max(…, 0)`, `F_S,max = max(F_M + Φ F_A, F_A)`, fatigue amplitude from the bolt-load range; separation-aware yield factor (D-42); version 1.1.0. | Fixed |
| G-06 | Medium | `mech.bolt-group` | A moment about an axis all the bolts lie on was silently dropped; the single-bolt case the review promised (retired `calc.bolt-shear-capacity`) was blocked. | `unresisted-moment` block; a single bolt takes direct shear and blocks only under a torque; version 1.1.0. | Fixed |
| G-07 | Medium | `Statistics.PartsPerMillionOutside` | The upper tail was 1 − Φ(z): at z ≈ 7 the subtraction kept four digits (7.77591e-7 against the exact 7.77547e-7 the legacy module gave). | Φ(−z); regression test at ±7σ to 1e-6 relative. | Fixed |
| G-08 | Low | Carried vectors | Not carried before this pass: bolted joint (3 vectors: the template could not take a given preload), bolt group (5), tolerance stack Example 4 and the no-requirement vector, and outputs left unasserted on thick cylinder, fillet weld and tolerance stack. | Bolted joint gains a given-preload method with optional given k_S, k_P, A_s (A_s fillable from a fastener record); every vector added. | Fixed |

**Integration note (2026-10-25).** After the legacy-calculator line was folded into v0.26.0: G-06 fixed the unresisted moment under the rule `unresisted-moment`; the bolt group now shares Addendum A's group-geometry kernel, whose `collinear` rule blocks the same case (and a line at any angle), so `unresisted-moment` was removed and the G-06 test re-targeted at `collinear`. The legacy bearing vectors (G-08) run on `mech.bearing-life-basic` because `mech.bearing-life` is Addendum A's M5, which does not reproduce them (`BearingLifeBasicTests`).
