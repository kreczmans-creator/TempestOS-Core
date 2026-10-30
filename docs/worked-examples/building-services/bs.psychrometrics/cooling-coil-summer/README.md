# Summer cooling coil: 28 °C / 60 % to 12 °C / 95 % (`bs.psychrometrics`)

**Source.** ASHRAE Handbook — Fundamentals, 2017, Ch. 1, cooling and dehumidification process (energy and mass balance form) — Confirmed.

Entering 28 °C / 60 % (W ≈ 14.3 g/kg) leaves the coil at 12 °C / 95 % (W ≈ 8.3 g/kg): the sheet gives the condensate rate, total, sensible and latent loads and the sensible heat ratio for the coil selection.

| Input | Value | Source |
|---|---|---|
| `RH` | 60 | spec: Design condition |
| `RH_2` | 95 | assumption: Off-coil 95 % RH (typical contact factor). |
| `T_2` | 12 degC | spec: AHU schedule AHU-01 off-coil 12 °C |
| `T_db` | 28 degC | spec: CIBSE Guide A summer design condition for the site, assumed |
| `altitude` | 0 m | assumption: Sea-level site. |
| `h_steam` | 2676 | assumption: Steam humidification only; this cooling coil does not use it, so the template default is kept. |
| `known` | relative humidity | assumption: RH given. |
| `m_da` | 2.4 kg/s | spec: AHU schedule AHU-01: 2.0 m³/s ≈ 2.4 kg/s dry air |
| `process` | cooling with dehumidification | assumption: Chilled-water coil. |

| Output | Expected | Computed | Matches expected |
|---|---|---|---|
| `T_2_out` | 12 degC | 12 degC | yes |
| `RH_2` | 95  | 95  | yes |

Governing check: none. Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
