# Portal-frame column base: UC 254×254×89 on a 450×400×25 S275 plate, four M24 8.8 bolts (`struct.base-plate`)

**Source.** Analytic (EN 1993-1-8 cl. 6.2.5, 6.2.6.11, 6.2.8.3, 6.2.2 simplified); section dimensions from BS EN 10365 / the manufacturer's datasheet, to be checked against the owner's copy, EN 1993-1-8:2005, f_jd = (2/3)·1.5·0.85·30/1.5 = 17 MPa; c = 25·√(265/(3·17)) = 56.987 mm; A_eff = 111 099.9 mm², N_j,Rd = 1888.7 kN; z_C = (260.3 − 17.3)/2 = 121.5 mm, x_b = 185 mm, z = 306.5 mm: C_Ed = 436.70 kN, T_Ed = 226.70 kN, 113.35 kN per bolt; no-prying plate mode 618.0 kN, bolts 406 kN → 406 kN; α_bc = 0.248, F_2,vb,Rd = 55.95 kN (below the 364.4 kN bearing), V_Rd = 0.2·210 + 4·55.95 = 265.8 kN, V_b,Ed = 4.5 kN — Unconfirmed.

A rigid-base portal column: 210 kN compression, 95 kN·m and 60 kN shear from the S5 combination and the S2 frame. The tension row sits 185 mm from the axis and the compression resultant 121.5 mm on the other side (the flange centre), so the lever arm is 306.5 mm and each bolt takes 113 kN, the figure that goes to Hilti PROFIS together with 4.5 kN of shear per anchor (the PROFIS report is not attached in this example, so the sheet carries a warning and no anchorage check: attaching the report as Evidence is what turns that into a recorded reference). The bolts (T-stub mode 3) govern at 0.56; bearing is light and the anchor shear (α_bc, not the Table 3.4 bolt shear) is 0.23. The plate would need 15.1 mm for bearing and both T-stubs together. The UC dimensions, the grout placeholder and the l_eff estimate are the engineer's to confirm.

| Input | Value | Source |
|---|---|---|
| `A_s` | 352.5 mm^2 | reference: S4 bolt sheet, M24 stress area (352.50 mm²) |
| `B` | 400 mm | drawing: PF-BP-001 rev B |
| `C_fd` | 0.2 | spec: EN 1993-1-8 cl. 6.2.2, sand-cement grout |
| `F_1_vb_Rd` | 364.4 kN | reference: S4 bolt sheet, M24 bearing on 25 mm S275 plate, e₁ = 60 mm, d₀ = 27 mm (364.44 kN) |
| `F_t_Rd` | 203 kN | reference: S4 bolt sheet, M24 8.8 (203.04 kN), rounded down |
| `L` | 450 mm | drawing: PF-BP-001 rev B |
| `M_Ed` | 95 kN*m | reference: S5 load combinations sheet / S2 frame sheet, column base A |
| `N_Ed` | 210 kN | reference: S5 load combinations sheet, ULS 6.10b, wind leading, column base A |
| `V_Ed` | 60 kN | reference: S5 load combinations sheet / S2 frame sheet, column base A |
| `alpha` | 1.5 | assumption: Conservative: a 1.2 m square pad under the 0.45 × 0.40 m plate gives A_c1/A_c0 well above 2.25; checked on drawing PF-FN-001. |
| `alpha_basis` | Pad 1.2 × 1.2 m, 0.8 m deep (PF-FN-001): A_c1/A_c0 above 2.25, so α = 1.5 is taken | drawing: PF-FN-001 rev A |
| `alpha_cc` | 0.85 | spec: UK NA to BS EN 1992-1-1, NA to cl. 3.1.6(1)P |
| `b` | 256.3 mm | spec: BS EN 10365 UC 254×254×89, to be checked against the owner's copy |
| `column` | I/H | drawing: PF-GA-001 rev C |
| `f_ck` | 30 MPa | spec: BS EN 206 C30/37 pad foundation, drawing PF-FN-001 |
| `f_ck_g` | 60 MPa | spec: Non-shrink grout datasheet, 28-day strength (placeholder GR-DS-01) |
| `f_ub` | 800 MPa | spec: EN ISO 898-1 class 8.8 |
| `f_y` | 265 MPa | spec: BS EN 10025-2 S275, 16 mm < t ≤ 40 mm |
| `f_yb` | 640 MPa | spec: EN ISO 898-1 class 8.8 |
| `gamma_M0` | 1 | spec: UK NA to BS EN 1993-1-1, NA.2.15 |
| `gamma_M2` | 1.25 | spec: UK NA to BS EN 1993-1-8, NA.2.3 |
| `gamma_c` | 1.5 | spec: UK NA to BS EN 1992-1-1, Table NA.1 |
| `h` | 260.3 mm | spec: BS EN 10365 UC 254×254×89, to be checked against the owner's copy |
| `l_eff` | 350 mm | assumption: Total for the two bolts of the row, about 175 mm each from the cl. 6.2.6.5 patterns (engineer's estimate). |
| `m` | 46.9 mm | assumption: Bolt at 185 mm from the axis, flange face at 130.15 mm: 54.85 mm, less 0.8·√2 × 7 mm (a 10 mm fillet leg has a 7 mm throat) = 7.9 mm. |
| `n_b` | 4 | drawing: PF-BP-001 rev B |
| `n_t` | 2 | drawing: PF-BP-001 rev B |
| `profis_ref` | PROFIS-BP1 (to be filed) | assumption: The PROFIS report is not attached in this example, so the sheet warns that the anchorage is not checked. |
| `prying` | no | assumption: Cast-in holding-down bolts: prying not assumed (min(2·M_pl/m, ΣF_t) is the conservative case). |
| `t_f` | 17.3 mm | spec: BS EN 10365 UC 254×254×89, to be checked against the owner's copy |
| `t_g` | 30 mm | drawing: PF-BP-001 rev B |
| `t_p` | 25 mm | drawing: PF-BP-001 rev B |
| `t_w` | 10.3 mm | spec: BS EN 10365 UC 254×254×89, to be checked against the owner's copy |
| `x_b` | 185 mm | drawing: PF-BP-001 rev B |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `f_jd` | 17 MPa | 17 MPa | PASS |
| `c` | 56.9873 mm | 56.9873 mm | PASS |
| `A_eff` | 111100 mm^2 | 111100 mm^2 | PASS |
| `N_j_Rd` | 1888.7 kN | 1888.7 kN | PASS |
| `z_C` | 121.5 mm | 121.5 mm | PASS |
| `z` | 306.5 mm | 306.5 mm | PASS |
| `C_Ed` | 436.705 kN | 436.705 kN | PASS |
| `T_Ed` | 226.705 kN | 226.705 kN | PASS |
| `F_t_Ed` | 113.352 kN | 113.352 kN | PASS |
| `C_Rd` | 826.33 kN | 826.33 kN | PASS |
| `T_Rd_1` | 618.004 kN | 618.004 kN | PASS |
| `T_Rd` | 406 kN | 406 kN | PASS |
| `u_T` | 0.558386  | 0.558386  | PASS |
| `F_2_vb_Rd` | 55.9488 kN | 55.9488 kN | PASS |
| `V_Rd` | 265.795 kN | 265.795 kN | PASS |
| `u_V` | 0.225738  | 0.225738  | PASS |
| `V_b_Ed` | 4.5 kN | 4.5 kN | PASS |
| `t_req` | 15.1417 mm | 15.1417 mm | PASS |
| `u_max` | 0.558386  | 0.558386  | PASS |

Governing check: Tension at the bolt row against the plate T-stub resistance — PASS (utilisation 0.56). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
