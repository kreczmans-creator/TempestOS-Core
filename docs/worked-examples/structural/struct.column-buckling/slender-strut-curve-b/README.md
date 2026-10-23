# Slender strut, curve b, S355 — fails (`struct.column-buckling`)

**Source.** TempestOS calc.column-buckling (v0.21.0) hand calculation, docs/engineering/calculations/calc.column-buckling.md, Example 2 — Confirmed.

A slender strut at λ = 150. Its strength is governed by buckling, not by the higher S355 design strength: 78 MPa against an Euler stress of 90 MPa, so 100 kN is 7 % over the 93.6 kN resistance. The sheet shows the failing check; a shorter effective length or a stockier section is the fix.

| Input | Value | Source |
|---|---|---|
| `A` | 1200 mm^2 | spec: SPEC-ST-02 section schedule |
| `E` | 205 GPa | spec: BS 5950-1 3.1.3: E = 205 GPa |
| `I` | 12 cm^4 | spec: SPEC-ST-02 section schedule |
| `L_E` | 1.5 m | drawing: DRG-ST-411 rev A |
| `P` | 100 kN | assumption: Factored brace force |
| `curve` | b | spec: BS 5950-1 Table 23: hot-finished tube, curve b |
| `p_y` | 355 MPa | spec: SPEC-ST-02: S355 |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `lambda` | 150  | 150  | PASS |
| `p_c` | 77.973 MPa | 77.9728 MPa | PASS |
| `P_c` | 93.567 kN | 93.5674 kN | PASS |
| `u` | 1.0688  | 1.06875  | PASS |

Governing check: Axial load against compression resistance — FAIL (utilisation 1.069). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
