# Vessel shell at 3 MPa — plate too thin (`mech.thin-vessel`)

**Source.** Hand calculation, ASME VIII-1 UG-27(c) forms, t_c = 2250/136.2 = 16.520 mm; t_req = 18.020 mm against 15 mm — Confirmed.

A 1.5 m vessel at 3 MPa needs 18 mm; the 15 mm plate fails at 1.20, and its hoop stress at the corroded thickness, 168 MPa, is above the 138 MPa allowable.

| Input | Value | Source |
|---|---|---|
| `E_j` | 1 | spec: SPEC-MECH-03 design criteria |
| `R_in` | 750 mm | drawing: DRG-PV-602 rev A |
| `S` | 138 MPa | spec: SPEC-MECH-03 design criteria |
| `c` | 1.5 mm | spec: SPEC-MECH-03 design criteria |
| `n_design` | 1 | spec: SPEC-MECH-03 design criteria |
| `p_design` | 3 MPa | spec: LOAD-SCHEDULE-01 |
| `t_p` | 15 mm | drawing: DRG-PV-602 rev A |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `t_c` | 16.52 mm | 16.5198 mm | PASS |
| `t_req` | 18.02 mm | 18.0198 mm | PASS |
| `sigma_h` | 168.47 MPa | 168.467 MPa | PASS |
| `u` | 1.2013  | 1.20132  | PASS |

Governing check: Provided thickness against required — FAIL (utilisation 1.21). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
