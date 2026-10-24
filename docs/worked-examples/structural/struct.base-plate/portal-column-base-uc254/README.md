# Portal-frame column base: UC 254×254×89 on a 450×400×25 S275 plate, four M24 8.8 bolts (`struct.base-plate`)

**Source.** Analytic (EN 1993-1-8 cl. 6.2.5, 6.2.6.11, 6.2.2 simplified); section dimensions from BS EN 10365 as the S1 sheet lists them, EN 1993-1-8:2005, f_jd = (2/3)·1.5·0.85·30/1.5 = 17 MPa; c = 25·√(265/(3·17)) = 56.987 mm; A_eff = 111 099.9 mm², N_j,Rd = 1888.7 kN; z = 185 + 121.5 = 306.5 mm: C_Ed = 436.70 kN, T_Ed = 226.70 kN, 113.35 kN per bolt; T-stub modes 1332.6, 541.6, 406 kN → 406 kN; V_Rd = 0.2·210 + 4·135.4 = 583.6 kN. The UC 254×254×89 dimensions are entered from memory of the Blue Book and are to be checked against the owner's copy — Unconfirmed.

A rigid-base portal column: 210 kN compression, 95 kN·m and 60 kN shear from the S5 combination and the S2 frame. The tension row sits 185 mm from the axis and the compression resultant 121.5 mm on the other side, so the lever arm is 306.5 mm and the bolts take 113 kN each (the figure that goes to Hilti PROFIS, whose report is filed as Evidence under the placeholder reference EVD-PROFIS-PLACEHOLDER). The bolts (T-stub mode 3) govern at 0.56; bearing and shear are light. The UC dimensions and the l_eff estimate are the engineer's to confirm.

| Input | Value | Source |
|---|---|---|
| `B` | 400 mm | drawing: PF-BP-001 rev B |
| `C_fd` | 0.2 | spec: EN 1993-1-8 cl. 6.2.2, sand-cement grout |
| `F_t_Rd` | 203 kN | reference: S4 bolt sheet, M24 8.8 (203.04 kN), rounded down |
| `F_vb_Rd` | 135.4 kN | reference: S4 bolt sheet, M24 8.8 shear through the threads (135.36 kN), rounded |
| `L` | 450 mm | drawing: PF-BP-001 rev B |
| `M_Ed` | 95 kN*m | reference: S5 load combinations sheet / S2 frame sheet, column base A |
| `N_Ed` | 210 kN | reference: S5 load combinations sheet, ULS 6.10b, wind leading, column base A |
| `V_Ed` | 60 kN | reference: S5 load combinations sheet / S2 frame sheet, column base A |
| `alpha` | 1.5 | assumption: Conservative: a 1.2 m square pad under the 0.45 × 0.40 m plate gives A_c1/A_c0 well above 2.25; checked on drawing PF-FN-001. |
| `alpha_basis` | Pad 1.2 × 1.2 m, 0.8 m deep (PF-FN-001): A_c1/A_c0 above 2.25, so α = 1.5 is taken | drawing: PF-FN-001 rev A |
| `alpha_cc` | 0.85 | spec: UK NA to BS EN 1992-1-1, NA to cl. 3.1.6(1)P |
| `b` | 256.3 mm | spec: BS EN 10365 UC 254×254×89 |
| `column` | I/H | drawing: PF-GA-001 rev C |
| `e_min` | 40 mm | drawing: PF-BP-001 rev B |
| `f_ck` | 30 MPa | spec: BS EN 206 C30/37 pad foundation, drawing PF-FN-001 |
| `f_y` | 265 MPa | spec: BS EN 10025-2 S275, 16 mm < t ≤ 40 mm |
| `gamma_M0` | 1 | spec: UK NA to BS EN 1993-1-1, NA.2.15 |
| `gamma_c` | 1.5 | spec: UK NA to BS EN 1992-1-1, Table NA.1 |
| `h` | 260.3 mm | spec: BS EN 10365 UC 254×254×89 |
| `l_eff` | 350 mm | assumption: Two bolts in the row, about 175 mm each from the cl. 6.2.6.5 patterns (engineer's estimate). |
| `m` | 43.5 mm | assumption: Bolt at 185 mm from the axis, flange face at 130.15 mm, less 0.8·√2 × 10 mm fillet leg. |
| `n_b` | 4 | drawing: PF-BP-001 rev B |
| `n_t` | 2 | drawing: PF-BP-001 rev B |
| `profis_ref` | EVD-PROFIS-PLACEHOLDER | evidence: EVD-PROFIS-PLACEHOLDER |
| `prying` | yes | assumption: Short holding-down bolts; prying forces assumed to develop. |
| `t_f` | 17.3 mm | spec: BS EN 10365 UC 254×254×89 |
| `t_p` | 25 mm | drawing: PF-BP-001 rev B |
| `t_w` | 10.3 mm | spec: BS EN 10365 UC 254×254×89 |
| `z` | 306.5 mm | drawing: PF-BP-001 rev B |
| `z_C` | 121.5 mm | assumption: Compression resultant at the centre of the compression flange: (h − t_f)/2 = 121.5 mm. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `f_jd` | 17 MPa | 17 MPa | PASS |
| `c` | 56.9873 mm | 56.9873 mm | PASS |
| `A_eff` | 111100 mm^2 | 111100 mm^2 | PASS |
| `N_j_Rd` | 1888.7 kN | 1888.7 kN | PASS |
| `C_Ed` | 436.705 kN | 436.705 kN | PASS |
| `T_Ed` | 226.705 kN | 226.705 kN | PASS |
| `F_t_Ed` | 113.352 kN | 113.352 kN | PASS |
| `C_Rd` | 826.33 kN | 826.33 kN | PASS |
| `T_Rd` | 406 kN | 406 kN | PASS |
| `u_T` | 0.558386  | 0.558386  | PASS |
| `u_V` | 0.10281  | 0.10281  | PASS |
| `u_max` | 0.558386  | 0.558386  | PASS |

Governing check: Base plate tension T-stub (bending at the bolts) — PASS (utilisation 0.558). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
