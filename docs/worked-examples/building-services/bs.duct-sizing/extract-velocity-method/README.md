# Workshop extract by the velocity method (`bs.duct-sizing`)

**Source.** Analytic, D = √(4Q/πv) rounded up to the next 50 mm; Δp/L by Darcy–Weisbach / Colebrook — Confirmed.

Sizing at 10 m/s transport velocity rounds the hood branch to 250 mm and the main and stack to 400 mm; the actual velocities, friction rates and the index-run total (with the 120 Pa hood entry and 30 Pa cowl) follow, against the fan's 600 Pa.

| Input | Value | Source |
|---|---|---|
| `T` | 25 degC | assumption: Workshop air 25 °C. |
| `dp_limit` | 600 Pa | spec: Extract fan external static 600 Pa |
| `eps` | 0.15 mm | assumption: Spiral-wound galvanised duct with joints. |
| `method` | velocity | assumption: Velocity method for an extract system. |
| `p_atm` | 100.5 kPa | assumption: Site 70 m AOD, ≈ 100.5 kPa. |
| `round_to` | 50 mm | spec: BS EN 1506 preferred sizes |
| `sections` | 3 row(s) | drawing: DRG-M-310 rev A |
| `v_limit` | 15 m/s | spec: SPEC-M-09 noise limit |
| `v_target` | 10 m/s | spec: Dust-carrying extract: 10 m/s minimum transport velocity, SPEC-M-09 |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `D_s1` | 250 mm | 250 mm | PASS |
| `D_s2` | 400 mm | 400 mm | PASS |

Governing check: Velocity ≤ limit — PASS (utilisation 0.56). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
