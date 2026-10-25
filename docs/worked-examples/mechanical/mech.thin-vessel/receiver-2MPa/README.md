# Air receiver shell, 2 MPa (`mech.thin-vessel`)

**Source.** Hand calculation, ASME VIII-1 UG-27(c) forms, t_c = 8.6133 mm, t_l = 4.2481 mm, t_req = 11.613 mm — Confirmed.

A 1 m diameter receiver at 2 MPa with spot-radiographed seams (E = 0.85) and 3 mm corrosion allowance needs 11.6 mm; the 12 mm plate provided passes at 0.97.

| Input | Value | Source |
|---|---|---|
| `E_j` | 0.85 | spec: SPEC-MECH-03 design criteria |
| `P` | 2 MPa | spec: LOAD-SCHEDULE-01 |
| `R` | 500 mm | drawing: DRG-PV-601 rev A |
| `S` | 138 MPa | spec: SPEC-MECH-03 design criteria |
| `c` | 3 mm | spec: SPEC-MECH-03 design criteria |
| `n` | 1 | spec: SPEC-MECH-03 design criteria |
| `t_p` | 12 mm | drawing: DRG-PV-601 rev A |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `t_c` | 8.6133 mm | 8.61326 mm | PASS |
| `t_req` | 11.613 mm | 11.6133 mm | PASS |
| `u` | 0.96777  | 0.967772  | PASS |

Governing check: Provided thickness against required — PASS (utilisation 0.968). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
