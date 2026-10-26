# Lifting lug, 50 kN — passes, pin bending governs (`mech.lifting-lug`)

**Source.** TempestOS calc.lifting-lug-pin-joint (v0.21.0) hand calculation, docs/engineering/calculations/calc.lifting-lug-pin-joint.md, Example 1 — Confirmed.

A 20 mm lug on a 30 mm pin between 12 mm cheek plates. All five checks pass; the pin in bending governs at 0.49, as it often does — the lug plate itself is lightly stressed.

| Input | Value | Source |
|---|---|---|
| `P` | 50 kN | spec: LOAD-SCHEDULE-01 |
| `W` | 100 mm | drawing: DRG-LL-101 rev A |
| `a` | 40 mm | drawing: DRG-LL-101 rev A |
| `d_h` | 32 mm | drawing: DRG-LL-101 rev A |
| `d_p` | 30 mm | drawing: DRG-LL-101 rev A |
| `f_b_pin` | 250 MPa | spec: SPEC-MECH-03 design criteria |
| `f_br` | 200 MPa | spec: SPEC-MECH-03 design criteria |
| `f_t` | 150 MPa | spec: SPEC-MECH-03 design criteria |
| `f_v` | 90 MPa | spec: SPEC-MECH-03 design criteria |
| `f_v_pin` | 150 MPa | spec: SPEC-MECH-03 design criteria |
| `g` | 2 mm | drawing: DRG-LL-101 rev A |
| `t` | 20 mm | drawing: DRG-LL-101 rev A |
| `t_s` | 12 mm | drawing: DRG-LL-101 rev A |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `sigma_t` | 36.765 MPa | 36.7647 MPa | PASS |
| `sigma_br` | 83.333 MPa | 83.3333 MPa | PASS |
| `sigma_b` | 122.61 MPa | 122.608 MPa | PASS |
| `u_b` | 0.49043  | 0.490433  | PASS |

Governing check: Pin bending — PASS (utilisation 0.49). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
