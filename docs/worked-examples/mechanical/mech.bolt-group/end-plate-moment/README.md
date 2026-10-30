# End-plate with two bolt rows under 20 kN·m (`mech.bolt-group`)

**Source.** Analytic (elastic method), t = M_x y / Σ y² = 20e6 × 100 / 40000 per unit area = 50 kN per bolt — Confirmed.

Four bolts in two rows 200 mm apart under a 20 kN·m moment: the tension row sees 50 kN per bolt, 83 % of a 60 kN tension resistance; no shear.

| Input | Value | Source |
|---|---|---|
| `F_t_Rd` | 60 kN | spec: M12 8.8 tension resistance per project calc |
| `F_v_Rd` | 40 kN | spec: M12 8.8 shear resistance, EN 1993-1-8 Table 3.4 per project calc |
| `F_x` | 0 kN | spec: LOAD-CASE-1 |
| `F_y` | 0 kN | spec: LOAD-CASE-1 |
| `M_x` | 20 kN*m | spec: LOAD-CASE-1 |
| `M_y` | 0 kN*m | spec: LOAD-CASE-1 |
| `M_z` | 0 kN*m | spec: LOAD-CASE-1 |
| `N` | 0 kN | spec: LOAD-CASE-1 |
| `bolts` | 4 row(s) | drawing: DRG-BRK-09 rev B |
| `e_x` | 0 mm | drawing: DRG-BRK-09 rev B |
| `e_y` | 0 mm | drawing: DRG-BRK-09 rev B |

| Output | Expected | Computed | Matches expected |
|---|---|---|---|
| `F_t_Ed_max` | 50 kN | 50 kN | yes |
| `u_max` | 0.83333  | 0.833333  | yes |

Governing check: Circular shear–tension interaction against its limit (governing bolt) — PASS (utilisation 0.83). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
