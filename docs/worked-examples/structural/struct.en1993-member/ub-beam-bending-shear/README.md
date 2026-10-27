# UB 305×165×40 S275 floor beam, bending and shear (`struct.en1993-member`)

**Source.** Analytic (EN 1993-1-1 6.2.5, 6.2.6, 6.2.8) with Blue Book properties, M_c,Rd = 171.3 kN·m; A_v = 20.07 cm²; V_pl,Rd = 318.6 kN; utilisations 0.700 and 0.314 — Confirmed.

The two-span floor beam from the S2 worked example, checked in bending and shear with the rolled-I shear-area helper. Bending governs at 0.70; shear is low enough that no 6.2.8 reduction applies. Lateral-torsional buckling is not checked on this sheet: the deck restraint is stated in the input ltb.

| Input | Value | Source |
|---|---|---|
| `A` | 51.3 cm^2 | reference: S1 section sheet / Blue Book UB 305×165×40 |
| `E` | 210 GPa | spec: BS EN 1993-1-1 3.2.6 |
| `I_y` | 8503 cm^4 | reference: S1 section sheet / Blue Book |
| `I_z` | 764 cm^4 | reference: S1 section sheet / Blue Book |
| `L_cr_y` | 6 m | assumption: Span 6 m. |
| `L_cr_z` | 1.5 m | assumption: Compression flange restrained at 1.5 m by the deck. |
| `M_y_Ed` | 120 kN*m | reference: S2 frame sheet, two-span beam, ULS (rounded) |
| `M_z_Ed` | 0 kN*m | reference: S2 frame sheet |
| `N_Ed` | 0 kN | reference: S2 frame sheet, two-span beam, ULS |
| `V_Ed` | 100 kN | reference: S2 frame sheet, two-span beam, ULS (rounded) |
| `W_el_y` | 560 cm^3 | reference: S1 section sheet / Blue Book |
| `W_el_z` | 92.6 cm^3 | reference: S1 section sheet / Blue Book |
| `W_pl_y` | 623 cm^3 | reference: S1 section sheet / Blue Book |
| `W_pl_z` | 142 cm^3 | reference: S1 section sheet / Blue Book |
| `b` | 165 mm | spec: BS EN 10365 UB 305×165×40 |
| `curve_y` | a | spec: BS EN 1993-1-1 Table 6.2: rolled, h/b > 1.2, t_f ≤ 40, y-y |
| `curve_z` | b | spec: BS EN 1993-1-1 Table 6.2: rolled, h/b > 1.2, t_f ≤ 40, z-z |
| `eta` | 1 | spec: UK NA to BS EN 1993-1-5 NA.2.4 |
| `f_y` | 275 MPa | spec: BS EN 10025-2 S275, t ≤ 16 mm |
| `gamma_M0` | 1 | spec: UK NA to BS EN 1993-1-1 NA.2.15 |
| `gamma_M1` | 1 | spec: UK NA to BS EN 1993-1-1 NA.2.15 |
| `h` | 303.4 mm | spec: BS EN 10365 UB 305×165×40 |
| `k_yy` | 1 | assumption: No axial force; interaction factors irrelevant. |
| `k_zz` | 1 | assumption: No axial force; interaction factors irrelevant. |
| `ltb` | Restrained | assumption: The deck restrains the compression flange (at 1.5 m, as L_cr_z), so lateral-torsional buckling does not govern; 6.3.2 is outside this sheet. |
| `r` | 8.9 mm | spec: BS EN 10365 UB 305×165×40 |
| `section_class` | 1 | assumption: UB 305×165×40 S275 is class 1 in bending (Blue Book). |
| `t_f` | 10.2 mm | spec: BS EN 10365 UB 305×165×40 |
| `t_w` | 6 mm | spec: BS EN 10365 UB 305×165×40 |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `M_y_c_Rd` | 171.325 kN*m | 171.325 kN*m | PASS |
| `V_pl_Rd` | 318.62 kN | 318.616 kN | PASS |
| `u_M_y` | 0.70043  | 0.700423  | PASS |

Governing check: Bending moment y-y against bending resistance — PASS (utilisation 0.70). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
