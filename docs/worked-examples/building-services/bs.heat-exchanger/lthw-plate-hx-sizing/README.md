# District heating plate exchanger sizing, counterflow (`bs.heat-exchanger`)

**Source.** Analytic (ε-NTU sizing; Incropera 7th ed. Table 11.4), 7th ed., Primary 1.2 kg/s from 80 °C, secondary 1.5 kg/s from 40 °C, 120 kW, U 3000 W/m²K with 0.0001 m²K/W fouling — Confirmed.

The secondary circuit takes 120 kW from a 1.2 kg/s primary at 80 °C. ε = 0.597 and NTU = 1.30 give 2.83 m² at U_f = 2307.7 W/m²K. The primary leaves at 56.1 °C and the secondary at 59.1 °C; the approach of 16.1 K clears the 3 K minimum.

| Input | Value | Source |
|---|---|---|
| `Q_req` | 120 kW | spec: Building peak heat load |
| `R_f` | 0.0001 m^2*K/W | assumption: Treated closed circuits on both sides. |
| `T_c_in` | 40 degC | drawing: M-SCH-201 rev B |
| `T_h_in` | 80 degC | spec: Network supply temperature |
| `U` | 3000 W/(m^2*K) | assumption: Typical clean plate exchanger, water/water. |
| `arrangement` | counterflow | assumption: Plate exchanger treated as pure counterflow. |
| `cp_c` | 4180 J/(kg*K) | assumption: Water at about 50 °C. |
| `cp_h` | 4190 J/(kg*K) | assumption: Water at about 70 °C. |
| `dT_min` | 3 K | assumption: Practical approach for a plate exchanger. |
| `m_c` | 1.5 kg/s | drawing: M-SCH-201 rev B |
| `m_h` | 1.2 kg/s | spec: Network connection agreement, primary flow |
| `mode` | sizing (given duty) | assumption: Sizing a new exchanger. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `eps` | 0.596659  | 0.596659  | PASS |
| `NTU` | 1.29734  | 1.29734  | PASS |
| `A_req` | 2.82665 m^2 | 2.82665 m^2 | PASS |
| `T_h_out` | 56.1337 degC | 56.1337 degC | PASS |
| `T_c_out` | 59.1388 degC | 59.1388 degC | PASS |
| `dT_app` | 16.1336 K | 16.1337 K | PASS |

Governing check: Approach against minimum — PASS (utilisation 0.186). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
