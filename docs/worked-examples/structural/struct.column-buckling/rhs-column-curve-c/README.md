# Pin-ended column, curve c, intermediate slenderness (`struct.column-buckling`)

**Source.** TempestOS calc.column-buckling (v0.21.0) hand calculation, docs/engineering/calculations/calc.column-buckling.md, Example 1 — Confirmed.

A column of intermediate slenderness. The Perry factor reduces the strength from the 275 MPa design strength to 134 MPa, about 60 % of Euler's 225 MPa; the column carries 150 kN at a utilisation of 0.56. The legacy module's hand calculation is reproduced to five significant figures.

| Input | Value | Source |
|---|---|---|
| `A` | 2000 mm^2 | spec: SPEC-ST-02 section schedule |
| `E` | 205 GPa | spec: BS 5950-1 3.1.3: E = 205 GPa |
| `I` | 200 cm^4 | spec: SPEC-ST-02 section schedule |
| `L_E` | 3 m | drawing: DRG-ST-410 rev B |
| `P` | 150 kN | assumption: Factored axial load from the frame analysis |
| `curve` | c | spec: BS 5950-1 Table 23: welded box, curve c |
| `p_y` | 275 MPa | spec: SPEC-ST-02: S275, t ≤ 16 mm |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `lambda` | 94.868  | 94.8683  | PASS |
| `p_c` | 133.78 MPa | 133.781 MPa | PASS |
| `P_c` | 267.56 kN | 267.562 kN | PASS |
| `u` | 0.56062  | 0.560619  | PASS |

Governing check: Axial load against compression resistance — PASS (utilisation 0.561). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
