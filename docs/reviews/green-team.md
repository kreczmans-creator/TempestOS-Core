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

## v0.27.0 Phase 5 — legacy vectors recomputed (2026-10-23)

Every "Computes" vector of the 14 legacy module test suites was traced to a worksheet known-answer case (a read-only inventory, then each gap closed and run). All 19 legacy calculations are now carried at a relative tolerance of 5 × 10⁻⁵ or tighter; `CommonCalculatorTests.Every_Known_Answer_Case_Passes` runs them.

| ID | Sev | Location | Finding | Fix | Status |
|---|---|---|---|---|---|
| G-05 | High | `mech.bolted-joint` | After separation the template still took the bolt load as F_M + Φ·F_A and reported a negative clamp; the legacy module (correctly) gave the bolt the whole working load and zero clamp. The overloaded worked example showed yield at 89 % (pass) where the bolt is at 115 % of proof. | `F_KR = max(…, 0)`, `F_S,max = max(F_M + Φ F_A, F_A)`, fatigue amplitude from the bolt-load range; separation-aware yield factor (D-42); version 1.1.0. | Fixed |
| G-06 | Medium | `mech.bolt-group` | A moment about an axis all the bolts lie on was silently dropped; the single-bolt case the review promised (retired `calc.bolt-shear-capacity`) was blocked. | `unresisted-moment` block; a single bolt takes direct shear and blocks only under a torque; version 1.1.0. | Fixed |
| G-07 | Medium | `Statistics.PartsPerMillionOutside` | The upper tail was 1 − Φ(z): at z ≈ 7 the subtraction kept four digits (7.77591e-7 against the exact 7.77547e-7 the legacy module gave). | Φ(−z); regression test at ±7σ to 1e-6 relative. | Fixed |
| G-08 | Low | Carried vectors | Not carried before this pass: bolted joint (3 vectors: the template could not take a given preload), bolt group (5), tolerance stack Example 4 and the no-requirement vector, and outputs left unasserted on thick cylinder, fillet weld and tolerance stack. | Bolted joint gains a given-preload method with optional given k_S, k_P, A_s (A_s fillable from a fastener record); every vector added. | Fixed |

