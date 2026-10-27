# Hot pipe in aluminium cladding: surface coefficient from convection and radiation (`bs.insulation`)

**Source.** Analytic (the stated convection and radiation correlation solved with the surface temperature; recomputed in python3 with an independent implementation), 114 mm pipe at 120 °C in 15 °C air, λ 0.04, bright cladding ε 0.1, 50 mm; every value is an invented illustrative input, not design data — Confirmed.

The bright cladding radiates little (h_r 0.58), so h_se is only 4.25 W/m²K and the 60 W/m heat-loss limit needs 23.4 mm; the 60 °C personnel-protection limit needs only 7.8 mm. At the chosen 50 mm the loss is 36.8 W/m with the cladding at 27.9 °C; both criteria pass with room to spare. The correlation is indicative: check h_se against the cladding maker's data before relying on a thin margin.

| Input | Value | Source |
|---|---|---|
| `D_i` | 114 mm | drawing: M-PIP-021 rev B |
| `L_surf` | 1 m | assumption: Not used: a pipe. |
| `RH_a` | 60 % | assumption: Not used: a hot pipe has no condensation criterion. |
| `T_amb` | 15 degC | spec: SPEC-M-12 §2: ambient design temperature |
| `T_proc` | 120 degC | spec: SPEC-M-12 §3: heating flow temperature |
| `eps` | 0.1 | assumption: Bright aluminium cladding; replace with the cladding maker's emissivity. |
| `geometry` | pipe (cylinder) | assumption: A round pipe. |
| `h_mode` | convection + radiation (correlation) | assumption: Surface coefficient computed from the correlation. |
| `h_se` | 10 W/(m^2*K) | assumption: Not used: h_se is computed. |
| `lambda` | 0.04 W/(m*K) | assumption: Mineral wool; take the maker's value at the mean temperature (W3 material) |
| `q_max_m` | 60 W/m | spec: SPEC-M-12 §5: heat loss not to exceed 60 W/m |
| `t_chosen` | 50 mm | spec: Pipework schedule M-SCH-021: 50 mm sections |
| `theta_se_max` | 60 degC | spec: SPEC-M-12 §6: personnel protection, surface not above 60 °C |
| `v_wind` | 0 m/s | assumption: Still air, indoors. |

| Output | Expected | Computed | Matches expected |
|---|---|---|---|
| `t_req_q` | 23.3743 mm | 23.3743 mm | yes |
| `t_req_se` | 7.84229 mm | 7.84229 mm | yes |
| `thickness` | 23.3743 mm | 23.3743 mm | yes |
| `h_c` | 3.67484 W/(m^2*K) | 3.67484 W/(m^2*K) | yes |
| `h_r` | 0.580066 W/(m^2*K) | 0.580066 W/(m^2*K) | yes |
| `h_se` | 4.2549 W/(m^2*K) | 4.2549 W/(m^2*K) | yes |
| `q_per_m` | 36.7726 W/m | 36.7726 W/m | yes |
| `T_se` | 27.855 degC | 27.855 degC | yes |

Governing check: Chosen thickness against the heat-loss limit — PASS (utilisation 0.61). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
