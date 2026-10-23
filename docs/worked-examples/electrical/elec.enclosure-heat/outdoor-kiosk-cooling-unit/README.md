# Outdoor kiosk in the sun: cooling-unit capacity (`elec.enclosure-heat`)

**Source.** Effective-surface method with solar gain, P_cool = P_total − h A_eff (T_max − T_amb) — Confirmed.

With 192 W of solar gain on top of 300 W internal load and only 5 K allowed above a 40 °C ambient, natural dissipation holds a fraction of the load; ventilation at 5 K rise would need a large airflow, so the sheet reports the cooling-unit capacity needed.

| Input | Value | Source |
|---|---|---|
| `A_sun` | 0.48 m^2 | assumption: One 600 × 800 face in the sun. |
| `D` | 0.3 m | spec: Enclosure datasheet AE-1380 |
| `H` | 0.8 m | spec: Enclosure datasheet AE-1380 |
| `I_sun` | 800 W/m^2 | spec: Site conditions SC-5: 800 W/m² on the sunlit side |
| `T_amb` | 40 degC | spec: Site conditions SC-5 |
| `T_max` | 45 degC | spec: Site conditions SC-5 |
| `W` | 0.6 m | spec: Enclosure datasheet AE-1380 |
| `alpha_sun` | 0.5 | assumption: Light-grey paint. |
| `altitude` | 0 m | assumption: Free-standing kiosk, bottom on a plinth. |
| `f_bottom` | 0 | assumption: Free-standing kiosk, bottom on a plinth. |
| `f_front` | 1 | assumption: Free-standing kiosk, bottom on a plinth. |
| `f_left` | 1 | assumption: Free-standing kiosk, bottom on a plinth. |
| `f_rear` | 1 | assumption: Free-standing. |
| `f_right` | 1 | assumption: Free-standing kiosk, bottom on a plinth. |
| `f_top` | 1 | assumption: Free-standing kiosk, bottom on a plinth. |
| `h` | 5.5 W/(m^2*K) | reference: Reference record: painted steel enclosure, still air, 5.5 W/m²K |
| `loads` | 2 row(s) | spec: Panel schedule PNL-09 |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `P_total` | 492 W | 492 W | PASS |

Governing check: Natural dissipation holds T_max — FAIL (utilisation 11.044). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
