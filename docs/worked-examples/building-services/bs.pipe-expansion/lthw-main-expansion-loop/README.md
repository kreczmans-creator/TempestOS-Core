# Low-temperature hot-water main, 50 m between anchors: a U-loop of two 4 m legs (`bs.pipe-expansion`)

**Source.** Analytic (ΔL = α·L·ΔT; U-loop as two guided cantilevers L = √(3·E·D·Δ/Sa), F = 12·E·I·Δ/L³, M = 6·E·I·Δ/L²; recomputed in python3), 88.9 × 3.2 mm steel main, 10 → 80 °C, 50 m between anchors; E 210 GPa and α 12 µm/(m·K) are the W3 seed values 'Carbon steel (generic)', indicative; Sa and K1 are invented illustrative records, not code values — Confirmed.

The 50 m main grows 42 mm; fully restrained it would carry 176 MPa and push 152 kN on its anchors. A U-loop in the middle of the run gives two 4 m legs that take 21 mm each; they need 3.43 m (utilisation 0.857), push 655 N on the anchors and put 1310 N·m into the loop corners (73.5 MPa). The screening ratio 58.3 mm²/m² is under the 120 mm²/m² K1 record, so the line passes the screen. Screening only — not a piping stress analysis: the real routing, supports and guides still need checking by the designer.

| Input | Value | Source |
|---|---|---|
| `E` | 210 GPa | assumption: W3 seed 'Carbon steel (generic)', indicative; replace with the project's material record. |
| `H` | 4 m | drawing: DRG-M-210 rev B |
| `K_scr` | 120 mm^2/m^2 | assumption: Invented illustrative screening limit; take K1 from the project's piping code. |
| `L` | 50 m | drawing: DRG-M-210 rev B |
| `OD` | 88.9 mm | drawing: DRG-M-210 rev B |
| `Sa` | 100 MPa | assumption: Invented illustrative allowable range; take Sa from the project's piping code and its formula. |
| `T_install` | 10 degC | assumption: Mean site temperature when the main is anchored. |
| `T_operate` | 80 degC | spec: SPEC-M-04 §3: LTHW flow temperature |
| `alpha` | 12 um/(m*K) | assumption: W3 seed 'Carbon steel (generic)' (12e-6 1/K), indicative; replace with the project's material record. |
| `layout` | U-loop (two legs, each takes half the movement) | drawing: DRG-M-210 rev B |
| `t_wall` | 3.2 mm | drawing: DRG-M-210 rev B |
| `temp_mode` | install and operating temperatures | assumption: Install and operating temperatures are both known. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `dL` | 42 mm | 42 mm | PASS |
| `Delta` | 21 mm | 21 mm | PASS |
| `L_req` | 3.4295 m | 3.4295 m | PASS |
| `u_leg` | 0.857375  | 0.857375  | PASS |
| `anchor_force` | 0.654934 kN | 0.654934 kN | PASS |
| `anchor_moment` | 1.30987 kN*m | 1.30987 kN*m | PASS |
| `sigma_leg` | 73.5092 MPa | 73.5092 MPa | PASS |
| `Y_scr` | 42 mm | 42 mm | PASS |
| `scr` | 58.3406 mm^2/m^2 | 58.3406 mm^2/m^2 | PASS |
| `u_scr` | 0.486172  | 0.486172  | PASS |
| `pass` | 1  | 1  | PASS |

Governing check: Available leg against the required leg — PASS (utilisation 0.86). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
