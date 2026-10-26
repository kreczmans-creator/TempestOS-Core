# Four-bolt bracket, 50 kN at 150 mm eccentricity (`mech.bolt-group`)

**Source.** Analytic (elastic method; AISC Manual Part 7 procedure), governing bolt resultant 36.44 kN — Confirmed.

A 100 mm square pattern carrying 50 kN downward 150 mm from its centroid: 12.5 kN direct plus 26.5 kN torsional shear per bolt; the two bolts on the far side govern at 36.4 kN, 91 % of a 40 kN shear resistance.

| Input | Value | Source |
|---|---|---|
| `F_x` | 0 kN | spec: LOAD-CASE-1 |
| `F_y` | -50 kN | spec: LOAD-CASE-1 |
| `M_x` | 0 kN*m | spec: LOAD-CASE-1 |
| `M_y` | 0 kN*m | spec: LOAD-CASE-1 |
| `N` | 0 kN | spec: LOAD-CASE-1 |
| `T_R` | 45 kN | spec: M12 8.8 tension resistance per project calc |
| `T_z` | 0 kN*m | spec: LOAD-CASE-1 |
| `V_R` | 40 kN | spec: M12 8.8 shear resistance, EN 1993-1-8 Table 3.4 per project calc |
| `bolts` | 4 row(s) | drawing: DRG-BRK-09 rev B |
| `e_x` | 200 mm | drawing: DRG-BRK-09 rev B |
| `e_y` | 50 mm | drawing: DRG-BRK-09 rev B |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `v_max` | 36.443 kN | 36.4434 kN | PASS |
| `u_max` | 0.91109  | 0.911086  | PASS |

Governing check: Circular shear–tension interaction (governing bolt) — PASS (utilisation 0.91). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
