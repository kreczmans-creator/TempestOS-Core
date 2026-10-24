# Simply supported beam, off-centre point load (`struct.beam-formulas`)

**Source.** Roark's Formulas for Stress and Strain, 8th ed., Table 8.1 case 2a — Confirmed.

A 50 kN plant load 4 m along a 6 m span: R_A = Pb/L = 16.67 kN, M under the load = Pab/L = 66.67 kN·m, maximum deflection at x = √((L² − b²)/3) = 3.27 m.

| Input | Value | Source |
|---|---|---|
| `E` | 210 GPa | spec: BS EN 1993-1-1 3.2.6 |
| `I` | 8503 cm^4 | reference: UB 305×165×40 (Blue Book) |
| `L` | 6 m | drawing: DRG-FL-201 rev A |
| `P` | 50 kN | spec: Load schedule LS-01: plant load |
| `a` | 4 m | drawing: DRG-FL-201 rev A |
| `case` | simply supported, point load at a | assumption: Single span, pin–roller. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `R_A` | 16.6667 kN | 16.6667 kN | PASS |
| `M_max` | 66.6667 kN*m | 66.6667 kN*m | PASS |
| `delta_max` | 10.839 mm | 10.8387 mm | PASS |

Governing check: none. Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
