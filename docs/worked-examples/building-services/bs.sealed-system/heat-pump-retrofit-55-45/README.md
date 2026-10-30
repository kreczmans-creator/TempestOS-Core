# Heat pump retrofit at 55/45 °C: existing radiators and expansion vessel (`bs.sealed-system`)

**Source.** Analytic (characteristic equation; Kell 1975 density; vessel relation), Five rooms with their existing radiators re-rated from ΔT 50 K to 55/45/21 °C; 12 L vessel at 7 m static — Confirmed.

At 55/45 °C with rooms at 21 °C the radiators give 49 % of their catalogue output. The lounge, kitchen and bedrooms fall 1.5 % short and the bathroom 11 % short (utilisation 1.107), so the check fails and those radiators need upsizing. The 91 L system needs a 3.5 L vessel, so the existing 12 L vessel is ample.

| Input | Value | Source |
|---|---|---|
| `T_fill` | 10 degC | assumption: Mains water at fill. |
| `T_flow` | 55 degC | spec: Heat pump design flow temperature |
| `T_max` | 60 degC | spec: Heat pump maximum flow temperature |
| `T_return` | 45 degC | spec: Heat pump design return temperature |
| `T_room` | 21 degC | spec: Living-room design temperature |
| `V_pipe` | 40 L | assumption: 15 mm and 22 mm copper, about 120 m. |
| `V_plant` | 15 L | spec: Heat pump and plate exchanger content, datasheet |
| `V_vessel` | 12 L | spec: Site survey: existing 12 L vessel |
| `dT_method` | arithmetic mean (catalogue rating) | assumption: Catalogue ratings use the arithmetic mean. |
| `h_static` | 7 m | drawing: M-SEC-001 rev A |
| `n` | 1.3 | assumption: Panel radiators, manufacturer exponent about 1.3. |
| `p_f` | 250 kPa | assumption: 50 kPa below the safety valve. |
| `p_margin` | 30 kPa | assumption: Usual allowance over static head. |
| `p_prv` | 300 kPa | spec: Heat pump safety valve |
| `rooms` | 5 row(s) | spec: Room heat loss calculation (B4) and radiator survey |

| Output | Expected | Computed | Matches expected |
|---|---|---|---|
| `f_dT` | 0.492557  | 0.492557  | yes |
| `R1` | 3.6544 kW | 3.6544 kW | yes |
| `u_emit` | 1.10739  | 1.10739  | yes |
| `V_s` | 91 L | 91 L | yes |
| `V_e` | 3.53118 L | 3.53118 L | yes |

Governing check: Selected emitters against room heat (governing room) — FAIL (utilisation 1.11). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
