# Copper heating riser, 24 m between anchors: a 2.5 m offset leg is too short, and a bellows would put 2.4 kN on the anchors (`bs.pipe-expansion`)

**Source.** Analytic (ΔL = α·L·ΔT; offset leg as one guided cantilever; bellows thrust P·A_eff; recomputed in python3), 54 × 1.5 mm copper, 10 → 70 °C, 24 m between anchors; E 120 GPa and α 16.6 µm/(m·K) are textbook generic values, Sa and K1 are invented illustrative records, not code values; the bellows is a placeholder from the same invented datasheet — Confirmed.

The 24 m riser grows 23.9 mm. The 2.5 m offset leg would have to be 2.78 m to keep the root stress within Sa (utilisation 1.113, FAIL) and the screening ratio 230 mm²/m² is more than twice the K1 record: the line needs a formal stress analysis or a longer leg. The alternative of a bellows replaces the leg and puts its pressure thrust, 6 bar × 40 cm² = 2.4 kN, on the anchors (spring force and guide friction come from the manufacturer's data).

| Input | Value | Source |
|---|---|---|
| `A_eff` | 40 cm^2 | assumption: Placeholder effective area; replace with the manufacturer's datasheet value. |
| `E` | 120 GPa | spec: Generic copper, indicative; take from the project's material record |
| `H` | 2.5 m | drawing: DRG-M-212 rev A |
| `K_scr` | 100 mm^2/m^2 | assumption: Invented illustrative screening limit; take K1 from the project's piping code. |
| `L` | 24 m | drawing: DRG-M-212 rev A |
| `OD` | 54 mm | drawing: DRG-M-212 rev A |
| `P_b` | 6 bar | spec: Heating circuit design pressure, gauge |
| `Sa` | 60 MPa | assumption: Invented illustrative allowable range; take Sa from the project's piping code and its formula. |
| `T_install` | 10 degC | assumption: Mean site temperature when the riser is anchored. |
| `T_operate` | 70 degC | spec: SPEC-M-04 §3: heating flow temperature |
| `alpha` | 16.6 um/(m*K) | spec: Generic copper, mean 10–70 °C, indicative |
| `layout` | offset leg (L- or Z-bend: the leg takes the whole movement) | drawing: DRG-M-212 rev A |
| `t_wall` | 1.5 mm | drawing: DRG-M-212 rev A |
| `temp_mode` | install and operating temperatures | assumption: Install and operating temperatures are both known. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `dL` | 23.904 mm | 23.904 mm | PASS |
| `sigma_res` | 119.52 MPa | 119.52 MPa | PASS |
| `F_res` | 29.5693 kN | 29.5693 kN | PASS |
| `L_req` | 2.78297 m | 2.78297 m | PASS |
| `u_leg` | 1.11319  | 1.11319  | PASS |
| `anchor_force` | 187.93 N | 187.93 N | PASS |
| `anchor_moment` | 234.913 N*m | 234.913 N*m | PASS |
| `scr` | 229.782 mm^2/m^2 | 229.782 mm^2/m^2 | PASS |
| `u_scr` | 2.29782  | 2.29782  | PASS |
| `F_thrust` | 2.4 kN | 2.4 kN | PASS |
| `pass` | 0  | 0  | PASS |

Governing check: Screening only: OD·Y/(L − U)² ≤ K1 (not met: the line needs a formal stress analysis) — FAIL (utilisation 2.298). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
