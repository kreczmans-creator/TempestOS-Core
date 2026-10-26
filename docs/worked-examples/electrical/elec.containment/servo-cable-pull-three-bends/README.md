# Servo power and encoder cables pulled through 40 mm conduit with three bends (`elec.containment`)

**Source.** Analytic (geometry and the horizontal-bend form with the cable weight on the conduit floor, recomputed in python3; test values invented for this example), A_cab = 2·π·14²/4 + 4·π·9²/4 = 562.345 mm²; A_int = π·40²/4 = 1256.637 mm²; fill = 44.75 %; w = 2 × 0.35 + 4 × 0.12 = 1.18 kg/m, w·g·μ = 3.471554 N/m (μ 0.3); R_min = 10 × 14 = 140 mm; T₁ = 150 N; as listed 693.105 N, reversed 666.255 N; SWP 1290.85 N/m (reversed); T_cab = 6 × min(600, 200) = 1200 N (equal share, the 200 N encoder cables govern) — Confirmed.

Two 14 mm servo power cables and four 9 mm encoder cables fill 44.75 % of a 40 mm conduit against a 0.5 space factor (89.5 % of it), and the 140 mm minimum bend radius is 35 % of the tightest bend, 400 mm. Pulled from a 150 N start, the route needs 693 N as drawn and 666 N reversed (the weight term of the horizontal bends adds 0.02 % to the capstan figures here: a light bundle on small radii); the reverse direction has the lower tension, presses 1291 N/m on the bends and uses 56 % of the 1200 N bundle limit, six cables sharing the pull equally against the 200 N limit of the encoder cables. The space factor, bend multiple, friction coefficient and pressure limit are values invented for this example and entered as if from the issuer's records; replace them with the issuer's own before relying on the sheet.

| Input | Value | Source |
|---|---|---|
| `D_c` | 40 mm | drawing: CND-ROUTE-07 rev C |
| `P_lim` | 3000 N/m | reference: record: sidewall pressure limit (a value invented for this example; enter the cable maker's figure) |
| `T_1` | 150 N | spec: Pulling method statement PMS-4: 150 N rope and pulling-head tension at the start of the pull |
| `cables` | 2 row(s) | spec: Cable data sheets CDS-21 (servo power) and CDS-22 (encoder): overall diameter, mass and maximum pulling tension |
| `cont_type` | Conduit (round) | drawing: CND-ROUTE-07 rev C |
| `f_max` | 0.5 | reference: record: space factor for this arrangement (a value invented for this example; enter your own from your installation standard or company rule) |
| `mu` | 0.3 | reference: record: coefficient of friction, sheath on lubricated conduit (a value invented for this example; enter your own) |
| `n_bend` | 10 | reference: record: minimum bend radius multiple (a value invented for this example; enter the largest multiple from the cable data sheets) |
| `route` | 6 row(s) | drawing: CND-ROUTE-07 rev C |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `A_cab` | 562.345 mm^2 | 562.345 mm^2 | PASS |
| `A_int` | 1256.64 mm^2 | 1256.64 mm^2 | PASS |
| `fill` | 44.75 % | 44.75 % | PASS |
| `u_fill` | 0.895  | 0.895  | PASS |
| `R_min` | 140 mm | 140 mm | PASS |
| `u_bend` | 0.35  | 0.35  | PASS |
| `T_max_a` | 693.105 N | 693.105 N | PASS |
| `T_max_b` | 666.255 N | 666.255 N | PASS |
| `max_tension` | 666.255 N | 666.255 N | PASS |
| `P_max` | 1290.85 N/m | 1290.85 N/m | PASS |
| `T_cab` | 1200 N | 1200 N | PASS |
| `u_T` | 0.555213  | 0.555213  | PASS |
| `u_P` | 0.430284  | 0.430284  | PASS |

Governing check: Fill against the space factor — PASS (utilisation 0.895). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
