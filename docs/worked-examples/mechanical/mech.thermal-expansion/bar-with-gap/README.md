# Steel bar heated 60 K against a 0.5 mm gap (`mech.thermal-expansion`)

**Source.** TempestOS calc.thermal-expansion-stress (v0.21.0) hand calculation, docs/engineering/calculations/calc.thermal-expansion-stress.md, Example 2 — Confirmed.

The bar wants to grow 1.44 mm; the first 0.5 mm closes the gap freely and the rest is resisted by rigid walls, giving 94 MPa compression.

| Input | Value | Source |
|---|---|---|
| `A` | 500 mm^2 | drawing: DRG-TX-501 rev A |
| `E` | 200 GPa | drawing: DRG-TX-501 rev A |
| `L` | 2 m | drawing: DRG-TX-501 rev A |
| `alpha` | 0.000012 1/K | drawing: DRG-TX-501 rev A |
| `dT` | 60 K | spec: LOAD-SCHEDULE-01 |
| `g` | 0.5 mm | drawing: DRG-TX-501 rev A |
| `sigma_allow` | 150 MPa | spec: SPEC-MECH-03 design criteria |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `F` | 47 kN | 47 kN | PASS |
| `sigma` | -94 MPa | -94 MPa | PASS |
| `delta` | 0.5 mm | 0.5 mm | PASS |

Governing check: Thermal stress against allowable — PASS (utilisation 0.63). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
