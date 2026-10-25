# Chilled-water pipe in a plant room: heat-gain limit and anti-condensation, 25 mm chosen (`bs.insulation`)

**Source.** Analytic (ISO 12241 formula route: cylinder resistance, Magnus dew point, root find on the thickness; recomputed in python3), 90 mm pipe at 6 °C in 28 °C plant-room air at 70 % RH, λ 0.035, h_se 8 W/m²K; every value is an invented illustrative input, not design data — Confirmed.

The dew point of 28 °C air at 70 % is 22.01 °C, so the surface must stay above 23.01 °C. That needs 13.2 mm, but the 12 W/m heat-gain limit needs 17.8 mm and governs. The chosen 25 mm sleeve gains 9.59 W/m with the surface at 25.27 °C: both criteria pass (utilisation 0.80 and 0.55). The pipe is above the 8.8 mm critical diameter, so insulation reduces the gain at every thickness.

| Input | Value | Source |
|---|---|---|
| `D_i` | 90 mm | drawing: M-PIP-014 rev A |
| `L_surf` | 1 m | assumption: Not used: a pipe. |
| `RH_a` | 70 % | spec: SPEC-M-11 §2: plant-room design humidity |
| `dew_margin` | 1 K | spec: SPEC-M-11 §5: surface at least 1 K above the dew point |
| `eps` | 0.9 | assumption: Not used: h_se is given. |
| `geometry` | pipe (cylinder) | assumption: A round pipe. |
| `h_mode` | h_se as given | assumption: A surface coefficient is given, not computed. |
| `h_se` | 8 W/(m^2*K) | assumption: Still air in a plant room; replace with the project's own record. |
| `lambda` | 0.035 W/(m*K) | assumption: Closed-cell elastomeric insulation; take the maker's value at the mean temperature (W3 material) |
| `q_max_m` | 12 W/m | spec: SPEC-M-11 §5: heat gain not to exceed 12 W/m |
| `t_chosen` | 25 mm | spec: Pipework schedule M-SCH-014: 25 mm sleeves |
| `theta_a` | 28 degC | spec: SPEC-M-11 §2: plant-room design temperature |
| `theta_i` | 6 degC | spec: SPEC-M-11 §3: chilled-water flow temperature |
| `v_wind` | 0 m/s | assumption: Not used: h_se is given. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `theta_dew` | 22.0122 degC | 22.0122 degC | PASS |
| `t_dew` | 13.1616 mm | 13.1616 mm | PASS |
| `t_q` | 17.8144 mm | 17.8144 mm | PASS |
| `thickness` | 17.8144 mm | 17.8144 mm | PASS |
| `q_per_m` | 9.59298 W/m | 9.59298 W/m | PASS |
| `theta_se` | 25.2736 degC | 25.2736 degC | PASS |
| `u_q` | 0.799415  | 0.799415  | PASS |
| `u_dew` | 0.546608  | 0.546608  | PASS |

Governing check: Chosen thickness against the heat-loss limit — PASS (utilisation 0.799). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
