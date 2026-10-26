# Steel bar heated 60 K against an elastic restraint (`mech.thermal-expansion`)

**Source.** TempestOS calc.thermal-expansion-stress (v0.21.0) hand calculation, docs/engineering/calculations/calc.thermal-expansion-stress.md, Example 3 — Confirmed.

A 100 kN/mm restraint in series with the bar's own 50 kN/mm takes a third of the movement as deflection; the force is 48 kN instead of the rigid 72 kN.

| Input | Value | Source |
|---|---|---|
| `A` | 500 mm^2 | drawing: DRG-TX-502 rev A |
| `E` | 200 GPa | drawing: DRG-TX-502 rev A |
| `L` | 2 m | drawing: DRG-TX-502 rev A |
| `alpha` | 0.000012 1/K | drawing: DRG-TX-502 rev A |
| `dT` | 60 K | spec: LOAD-SCHEDULE-01 |
| `g` | 0 mm | drawing: DRG-TX-502 rev A |
| `k_s` | 100 kN/mm | spec: SPEC-MECH-03 design criteria |
| `sigma_allow` | 150 MPa | spec: SPEC-MECH-03 design criteria |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `F` | 48000 N | 48000 N | PASS |
| `sigma` | -96 MPa | -96 MPa | PASS |
| `delta` | 0.48 mm | 0.48 mm | PASS |

Governing check: Thermal stress against allowable — PASS (utilisation 0.64). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
