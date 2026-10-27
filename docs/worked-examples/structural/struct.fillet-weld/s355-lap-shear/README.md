# S355 lap weld, 150 kN in longitudinal shear (`struct.fillet-weld`)

**Source.** TempestOS calc.fillet-weld-throat-stress (v0.21.0) hand calculation, docs/engineering/calculations/calc.fillet-weld-throat-stress.md, Example 1 — Confirmed.

A 5 mm throat, 200 mm long, on S355. The throat stress, 150 MPa, is 60 % of the 251 MPa design shear strength; a 3 mm throat would carry the load, so the minimum governs the required size.

| Input | Value | Source |
|---|---|---|
| `F_n` | 0 kN | spec: LOAD-SCHEDULE-01 |
| `F_par` | 150 kN | spec: LOAD-SCHEDULE-01 |
| `F_tr` | 0 kN | spec: LOAD-SCHEDULE-01 |
| `L` | 200 mm | drawing: DRG-WD-701 rev A |
| `a` | 5 mm | drawing: DRG-WD-701 rev A |
| `beta_w` | 0.9 | spec: SPEC-STR-02 design criteria |
| `f_u` | 490 MPa | spec: SPEC-STR-02 design criteria |
| `gamma_M2` | 1.25 | spec: SPEC-STR-02 design criteria |
| `joint` | Lap joint | drawing: DRG-WD-701 rev A |

| Output | Expected | Computed | Matches expected |
|---|---|---|---|
| `f_vw_d` | 251.47 MPa | 251.468 MPa | yes |
| `tau_w` | 150 MPa | 150 MPa | yes |
| `F_w_Rd` | 251.468 kN | 251.468 kN | yes |
| `u` | 0.5965  | 0.596497  | yes |

Governing check: Weld force against weld resistance — PASS (utilisation 0.60). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
