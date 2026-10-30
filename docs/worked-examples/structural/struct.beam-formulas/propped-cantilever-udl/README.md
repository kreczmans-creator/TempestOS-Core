# Propped cantilever, UDL (`struct.beam-formulas`)

**Source.** Roark's Formulas for Stress and Strain, 8th ed., Table 8.1 case 2c: R_prop = 3wL/8, M_fixed = −wL²/8, M_sag = 9wL²/128 at 5L/8, δ_max = wL⁴/(185EI) — Confirmed.

The same propped cantilever as the frame solver's known-answer case ka-propped-cantilever: the closed forms and the stiffness solution agree (R_prop 36 kN, M_fixed −96 kN·m, sag 54 kN·m at 5 m).

| Input | Value | Source |
|---|---|---|
| `E` | 210 GPa | spec: BS EN 1993-1-1 3.2.6 |
| `I` | 20000 cm^4 | reference: Fabricated section, S1 sheet |
| `L` | 8 m | drawing: DRG-CA-033 rev A |
| `case` | propped cantilever (fixed left, pinned right), UDL | assumption: Fixed at the wall, propped at the free end. |
| `w` | 12 kN/m | spec: Load schedule LS-03 |

| Output | Expected | Computed | Matches expected |
|---|---|---|---|
| `R_B` | 36 kN | 36 kN | yes |
| `M_min` | -96 kN*m | -96 kN*m | yes |
| `M_max` | 54 kN*m | 54 kN*m | yes |
| `x_M` | 5 m | 5 m | yes |

Governing check: none. Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
