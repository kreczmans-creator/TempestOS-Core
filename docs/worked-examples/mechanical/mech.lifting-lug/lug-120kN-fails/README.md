# Lifting lug, 120 kN — fails in bearing, tear-out and pin bending (`mech.lifting-lug`)

**Source.** TempestOS calc.lifting-lug-pin-joint (v0.21.0) hand calculation, docs/engineering/calculations/calc.lifting-lug-pin-joint.md, Example 2 — Confirmed.

The same detail scaled down and loaded to 120 kN. Bearing (1.60), tear-out (1.48) and pin bending (1.53) all fail; bearing governs. A thicker lug fixes the first two; the pin needs a larger diameter.

| Input | Value | Source |
|---|---|---|
| `F_lug` | 120 kN | spec: LOAD-SCHEDULE-01 |
| `W` | 90 mm | drawing: DRG-LL-102 rev A |
| `a` | 30 mm | drawing: DRG-LL-102 rev A |
| `d_h` | 26 mm | drawing: DRG-LL-102 rev A |
| `d_p` | 25 mm | drawing: DRG-LL-102 rev A |
| `f_b_pin` | 250 MPa | spec: SPEC-MECH-03 design criteria |
| `f_br` | 200 MPa | spec: SPEC-MECH-03 design criteria |
| `f_t` | 150 MPa | spec: SPEC-MECH-03 design criteria |
| `f_v` | 90 MPa | spec: SPEC-MECH-03 design criteria |
| `f_v_pin` | 150 MPa | spec: SPEC-MECH-03 design criteria |
| `g` | 1 mm | drawing: DRG-LL-102 rev A |
| `t_lug` | 15 mm | drawing: DRG-LL-102 rev A |
| `t_s` | 10 mm | drawing: DRG-LL-102 rev A |

| Output | Expected | Computed | Matches expected |
|---|---|---|---|
| `sigma_br` | 320 MPa | 320 MPa | yes |
| `tau_to` | 133.33 MPa | 133.333 MPa | yes |
| `sigma_b` | 381.36 MPa | 381.361 MPa | yes |
| `u_br` | 1.6  | 1.6  | yes |

Governing check: Pin bearing against the allowable bearing stress — FAIL (utilisation 1.60). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
