# Two-span continuous beam, ULS and SLS combinations (`struct.frame-2d`)

**Source.** Roark's Formulas for Stress and Strain, 8th ed., Table 8.1 case 2c applied to each span of a symmetric two-span beam under UDL: interior support moment −wL²/8, end reactions 3wL/8, interior reaction 10wL/8 — Confirmed.

Two equal 6 m spans on UB 305×165×40 under permanent 8 kN/m and imposed 12 kN/m. ULS (1.35G + 1.5Q = 28.8 kN/m): hogging −wL²/8 = −129.6 kN·m over the middle support, sagging 9wL²/128 = 72.9 kN·m, interior reaction 1.25wL = 216 kN; deflection wL⁴ (39 + 55√33)/(65536 EI) = 11.32 mm. The SLS combination sits in the envelope.

| Input | Value | Source |
|---|---|---|
| `E` | 210 GPa | spec: BS EN 1993-1-1 3.2.6: E = 210 GPa |
| `combinations` | 4 row(s) | spec: BS EN 1990 6.4.3.2 eq. 6.10 (γ_G 1.35, γ_Q 1.5); SLS characteristic |
| `elements` | 2 row(s) | reference: UB 305×165×40 (Blue Book) |
| `loads` | 4 row(s) | spec: Load schedule LS-01: G = 8 kN/m, Q = 12 kN/m |
| `n_div` | 24 | assumption: 24 stations per span. |
| `nodes` | 3 row(s) | drawing: DRG-FL-201 rev A |
| `report` | ULS | assumption: ULS reported in detail; SLS in the envelope. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `R2_y` | 216 kN | 216 kN | PASS |
| `R1_y` | 64.8 kN | 64.8 kN | PASS |
| `M_min` | -129.6 kN*m | -129.6 kN*m | PASS |
| `M_max` | 72.9 kN*m | 72.9 kN*m | PASS |
| `d_max_env` | 11.322 mm | 11.3213 mm | PASS |

Governing check: none. Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
