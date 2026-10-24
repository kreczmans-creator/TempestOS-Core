# UC 203×203×46 S355 column, compression with major-axis moment (`struct.en1993-member`)

**Source.** Analytic (EN 1993-1-1 6.3.1, 6.3.3 simplified) with Blue Book properties, in the manner of an SCI P362 column example, N_b,z,Rd = 1101.9 kN; M_y,c,Rd = 497e3·355 = 176.4 kN·m; member interaction 900/1101.9 + 1.0·40/176.4 = 0.8168 + 0.2268 = 1.0435 → FAIL — Unconfirmed.

A 4 m pinned UC column carrying 900 kN with a 40 kN·m major-axis moment from the S2 frame sheet. Flexural buckling about z-z governs the axial term (χ_z = 0.529); the simplified member interaction reaches 1.04, so the section fails by 4 % and the next size up is needed.

| Input | Value | Source |
|---|---|---|
| `A` | 58.7 cm^2 | reference: S1 section sheet / Blue Book UC 203×203×46 |
| `E` | 210 GPa | spec: BS EN 1993-1-1 3.2.6 |
| `I_y` | 4568 cm^4 | reference: S1 section sheet / Blue Book |
| `I_z` | 1548 cm^4 | reference: S1 section sheet / Blue Book |
| `L_cr_y` | 4 m | assumption: Pinned both ends: L_cr = L = 4 m. |
| `L_cr_z` | 4 m | assumption: Pinned both ends: L_cr = L = 4 m. |
| `M_y_Ed` | 40 kN*m | reference: S2 frame sheet, column 1, ULS |
| `M_z_Ed` | 0 kN*m | reference: S2 frame sheet |
| `N_Ed` | -900 kN | reference: S2 frame sheet, column 1, ULS |
| `V_Ed` | 20 kN | reference: S2 frame sheet |
| `W_el_y` | 450 cm^3 | reference: S1 section sheet / Blue Book |
| `W_el_z` | 152 cm^3 | reference: S1 section sheet / Blue Book |
| `W_pl_y` | 497 cm^3 | reference: S1 section sheet / Blue Book |
| `W_pl_z` | 231 cm^3 | reference: S1 section sheet / Blue Book |
| `b` | 203.6 mm | spec: BS EN 10365 UC 203×203×46 |
| `curve_y` | b | spec: BS EN 1993-1-1 Table 6.2: rolled, h/b ≤ 1.2, t_f ≤ 100, S355, y-y |
| `curve_z` | c | spec: BS EN 1993-1-1 Table 6.2: rolled, h/b ≤ 1.2, t_f ≤ 100, S355, z-z |
| `eta` | 1 | spec: UK NA to BS EN 1993-1-5 NA.2.4 |
| `f_y` | 355 MPa | spec: BS EN 10025-2 S355, t ≤ 16 mm |
| `gamma_M0` | 1 | spec: UK NA to BS EN 1993-1-1 NA.2.15 |
| `gamma_M1` | 1 | spec: UK NA to BS EN 1993-1-1 NA.2.15 |
| `h` | 203.2 mm | spec: BS EN 10365 UC 203×203×46 |
| `k_yy` | 1 | assumption: Simplified interaction: k_yy = 1.0. |
| `k_zz` | 1 | assumption: Simplified interaction: k_zz = 1.0. |
| `r` | 10.2 mm | spec: BS EN 10365 UC 203×203×46 |
| `section_class` | 1 | assumption: UC 203×203×46 S355 is class 1 in compression (Blue Book). |
| `t_f` | 11 mm | spec: BS EN 10365 UC 203×203×46 |
| `t_w` | 7.2 mm | spec: BS EN 10365 UC 203×203×46 |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `N_b_z_Rd` | 1101.9 kN | 1101.85 kN | PASS |
| `M_y_c_Rd` | 176.435 kN*m | 176.435 kN*m | PASS |
| `u_member` | 1.0435  | 1.04352  | PASS |

Governing check: Member interaction (simplified) — FAIL (utilisation 1.044). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
