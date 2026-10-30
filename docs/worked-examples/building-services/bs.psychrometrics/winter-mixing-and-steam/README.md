# Winter: mix return and outdoor air, then steam-humidify (`bs.psychrometrics`)

**Source.** Analytic mass and energy balance (ASHRAE Ch. 1 mixing and humidification relations), W₃ and h₃ mass-weighted; steam adds (W₂ − W₁) h_steam — Confirmed.

Three parts return air at 22 °C / 40 % mixed with one part outdoor air at −3 °C / 90 % (over ice): the mixed state follows the mass-weighted W and h. A second run of the sheet with the steam process takes the mixed state to the supply humidity.

| Input | Value | Source |
|---|---|---|
| `RH` | 40 | spec: Return air 22 °C / 40 % |
| `RH_b` | 90 | spec: Winter design condition |
| `T_db` | 22 degC | spec: Return air 22 °C / 40 % |
| `T_db_b` | -3 degC | spec: CIBSE Guide A winter design −3 °C / 90 %, assumed |
| `altitude` | 120 m | drawing: Site survey SRV-01: 120 m AOD |
| `h_steam` | 2676 | assumption: Dry saturated steam at atmospheric pressure, about 2676 kJ/kg from steam tables; used by the steam-humidification run only. |
| `known` | relative humidity | assumption: RH given. |
| `m_da` | 3 kg/s | spec: AHU-02 return 3 kg/s |
| `m_da_b` | 1 kg/s | spec: AHU-02 minimum outdoor air 1 kg/s |
| `process` | adiabatic mixing | assumption: Mixing box. |

| Output | Expected | Computed | Matches expected |
|---|---|---|---|
| `m_da_3` | 4 kg/s | 4 kg/s | yes |

Governing check: none. Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
