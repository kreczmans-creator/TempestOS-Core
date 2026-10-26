# The same copper riser on an axial bellows: 2.4 kN thrust and 2.0 kN spring on the anchors (`bs.pipe-expansion`)

**Source.** Analytic (ΔL = α·L·ΔT; anchor force = P·A_eff + k·ΔL; recomputed in python3), 54 × 1.5 mm copper, 10 → 70 °C, 24 m between anchors, α 17 µm/(m·K) (W3 seed 'Copper (generic)'); the bellows data are an invented illustrative datasheet, to be replaced by the manufacturer's — Confirmed.

With the bellows in the run there is no leg to check and no screening. The anchors take the pressure thrust 6 bar × 40 cm² = 2.4 kN plus the spring force 80 N/mm × 24.48 mm = 1.96 kN, 4.36 kN in all (the port anchor_force), against the 0.19 kN of the offset leg of the previous example; the 24.5 mm movement uses 61 % of the bellows' rated 40 mm. Guide friction and the bellows' other limits come from the manufacturer.

| Input | Value | Source |
|---|---|---|
| `A_eff` | 40 cm^2 | assumption: Placeholder effective area; replace with the manufacturer's datasheet value. |
| `E` | 120 GPa | assumption: W3 seed 'Copper (generic)', indicative; replace with the project's material record. |
| `L` | 24 m | drawing: DRG-M-212 rev B |
| `OD` | 54 mm | drawing: DRG-M-212 rev B |
| `P_b` | 6 bar | spec: Heating circuit design pressure, gauge |
| `T_install` | 10 degC | assumption: Mean site temperature when the riser is anchored. |
| `T_operate` | 70 degC | spec: SPEC-M-04 §3: heating flow temperature |
| `alpha` | 17 um/(m*K) | assumption: W3 seed 'Copper (generic)' (17e-6 1/K), indicative; replace with the project's material record. |
| `dL_rated` | 40 mm | assumption: Placeholder rated axial movement; replace with the manufacturer's datasheet value. |
| `k_b` | 80 N/mm | assumption: Placeholder spring rate; replace with the manufacturer's datasheet value. |
| `layout` | axial bellows (no leg or loop: the bellows absorbs the movement) | drawing: DRG-M-212 rev B |
| `t_wall` | 1.5 mm | drawing: DRG-M-212 rev B |
| `temp_mode` | install and operating temperatures | assumption: Install and operating temperatures are both known. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `dL` | 24.48 mm | 24.48 mm | PASS |
| `F_thrust` | 2.4 kN | 2.4 kN | PASS |
| `F_spring` | 1958.4 N | 1958.4 N | PASS |
| `anchor_force` | 4358.4 N | 4358.4 N | PASS |
| `anchor_moment` | 0 N*m | 0 N*m | PASS |
| `u_bellows` | 0.612  | 0.612  | PASS |
| `pass` | 1  | 1  | PASS |

Governing check: Movement against the rated axial movement of the bellows (40 mm) — PASS (utilisation 0.61). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
