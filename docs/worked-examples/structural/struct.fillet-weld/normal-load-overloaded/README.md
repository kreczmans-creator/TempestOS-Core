# 4 mm weld, 120 kN normal to the parts — fails (`struct.fillet-weld`)

**Source.** TempestOS calc.fillet-weld-throat-stress (v0.21.0) hand calculation, docs/engineering/calculations/calc.fillet-weld-throat-stress.md, Example 3 — Confirmed.

A 100 mm weld with a 4 mm throat under 120 kN. The throat stress, 300 MPa, exceeds the design strength by 19 %; a 5 mm throat (4.77 mm needed) would pass.

| Input | Value | Source |
|---|---|---|
| `F_n` | 120 kN | spec: LOAD-SCHEDULE-01 |
| `F_par` | 0 kN | spec: LOAD-SCHEDULE-01 |
| `F_tr` | 0 kN | spec: LOAD-SCHEDULE-01 |
| `L` | 100 mm | drawing: DRG-WD-702 rev A |
| `a` | 4 mm | drawing: DRG-WD-702 rev A |
| `beta_w` | 0.9 | spec: SPEC-STR-02 design criteria |
| `f_u` | 490 MPa | spec: SPEC-STR-02 design criteria |
| `gamma_M2` | 1.25 | spec: SPEC-STR-02 design criteria |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `tau_w` | 300 MPa | 300 MPa | PASS |
| `u` | 1.193  | 1.19299  | PASS |
| `a_calc` | 4.7719 mm | 4.77198 mm | PASS |

Governing check: Weld resistance — FAIL (utilisation 1.20). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
