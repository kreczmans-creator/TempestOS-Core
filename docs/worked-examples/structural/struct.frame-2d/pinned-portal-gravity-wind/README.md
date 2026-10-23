# Pinned-base portal frame, gravity and wind (`struct.frame-2d`)

**Source.** Hibbeler, Structural Analysis, 9th ed., Portal frame with pinned supports: lateral load shared equally by equal columns (exact for an axially stiff beam); gravity case by symmetry — Confirmed.

A 8 m × 5 m pinned portal under 15 kN/m roof load and a 24 kN eaves wind load. Equilibrium: ΣR_y = 120 kN, ΣR_x = −24 kN. The column-top moments combine the gravity frame action with the wind sway; the sheet reports every member's extremes and the envelope over G+W and G.

| Input | Value | Source |
|---|---|---|
| `E` | 210 GPa | spec: BS EN 1993-1-1 3.2.6 |
| `combinations` | 3 row(s) | assumption: Characteristic combinations for the worked example. |
| `elements` | 3 row(s) | reference: UC 203×203×46 columns, UB 305×165×40 rafter (Blue Book) |
| `loads` | 2 row(s) | spec: Load schedule LS-02: roof 15 kN/m; wind 24 kN at eaves |
| `n_div` | 16 | assumption: 16 stations per member. |
| `nodes` | 4 row(s) | drawing: DRG-PF-010 rev B |
| `report` | G+W | assumption: Gravity plus wind reported. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `sum_R_y` | 120 kN | 120 kN | PASS |
| `sum_R_x` | -24 kN | -24 kN | PASS |

Governing check: none. Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
