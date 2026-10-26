# Copper heating riser, 24 m between anchors: a 2.5 m offset leg is too short (`bs.pipe-expansion`)

**Source.** Analytic (ΔL = α·L·ΔT; offset leg as one guided cantilever with the run bending for the leg's growth; screening Y = α·ΔT·U; recomputed in python3), 54 × 1.5 mm copper, 10 → 70 °C, 24 m between anchors; E 120 GPa and α 17 µm/(m·K) are the W3 seed values 'Copper (generic)', indicative; Sa and K1 are invented illustrative records, not code values — Confirmed.

The 24 m riser grows 24.5 mm. The 2.5 m offset leg would have to be 2.82 m to keep the root stress within Sa (utilisation 1.127, FAIL); the run is not the problem (it needs 0.91 m of its 24 m for the leg's 2.55 mm growth). The screening ratio 237 mm²/m² is more than twice the K1 record: the line needs a formal stress analysis or a longer leg. The next example replaces the leg with a bellows.

| Input | Value | Source |
|---|---|---|
| `E` | 120 GPa | assumption: W3 seed 'Copper (generic)', indicative; replace with the project's material record. |
| `H` | 2.5 m | drawing: DRG-M-212 rev A |
| `K_scr` | 100 mm^2/m^2 | assumption: Invented illustrative screening limit; take K1 from the project's piping code. |
| `L` | 24 m | drawing: DRG-M-212 rev A |
| `OD` | 54 mm | drawing: DRG-M-212 rev A |
| `Sa` | 60 MPa | assumption: Invented illustrative allowable range; take Sa from the project's piping code and its formula. |
| `T_install` | 10 degC | assumption: Mean site temperature when the riser is anchored. |
| `T_operate` | 70 degC | spec: SPEC-M-04 §3: heating flow temperature |
| `alpha` | 17 um/(m*K) | assumption: W3 seed 'Copper (generic)' (17e-6 1/K), indicative; replace with the project's material record. |
| `layout` | offset leg (L- or Z-bend: the leg takes the whole movement) | drawing: DRG-M-212 rev A |
| `t_wall` | 1.5 mm | drawing: DRG-M-212 rev A |
| `temp_mode` | install and operating temperatures | assumption: Install and operating temperatures are both known. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `dL` | 24.48 mm | 24.48 mm | PASS |
| `sigma_res` | 122.4 MPa | 122.4 MPa | PASS |
| `F_res` | 30.2818 kN | 30.2818 kN | PASS |
| `L_req` | 2.8163 m | 2.8163 m | PASS |
| `u_leg` | 1.12652  | 1.12652  | PASS |
| `u_run` | 0.0378731  | 0.0378731  | PASS |
| `anchor_force` | 192.459 N | 192.459 N | PASS |
| `anchor_moment` | 240.573 N*m | 240.573 N*m | PASS |
| `Y_scr` | 24.6125 mm | 24.6125 mm | PASS |
| `scr` | 236.592 mm^2/m^2 | 236.592 mm^2/m^2 | PASS |
| `u_scr` | 2.36592  | 2.36592  | PASS |
| `pass` | 0  | 0  | PASS |

Governing check: Screening only: OD·Y/(L − U)² ≤ K1 (not met: the line needs a formal stress analysis) — FAIL (utilisation 2.37). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
