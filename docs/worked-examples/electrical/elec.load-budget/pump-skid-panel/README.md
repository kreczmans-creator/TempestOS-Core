# Pump skid control panel: supply, PSU, transformer and UPS battery (`elec.load-budget`)

**Source.** Analytic (load budget arithmetic), Two pumps, a fan, panel lighting, PLC and comms; 2 h autonomy on the 24 V controls — Confirmed.

Duty and standby pumps (standby at diversity 0), a panel fan and light, and a 24 V PLC with its router on a 2 h battery. Demand is 11.18 kW and 12.99 kVA (the 24 V supply's input counted on the single-phase bus at 90 % efficiency), well inside the 25 kVA allocation, but the duty pump start lifts the peak to 83.3 kVA, above three times the allocation (75 kVA): the starting check fails and calls for a soft starter or a larger allocation. The 240 W PSU and the 17 A·h battery are checked against the minimums.

| Input | Value | Source |
|---|---|---|
| `C_batt` | 17 A*h | drawing: E-SCH-010 rev A |
| `DoD` | 0.8 | assumption: VRLA, 80 % depth of discharge. |
| `P_PSU` | 240 W | drawing: E-SCH-010 rev A |
| `P_add` | 0 kW | assumption: No further load linked into this panel: every load is in the table. |
| `SF` | 1.25 | assumption: 25 % growth margin. |
| `S_T` | 25 kVA | spec: Existing site transformer allocation |
| `T_amb` | 45 degC | spec: Plant room design maximum |
| `U_1ph` | 230 V | spec: Site supply |
| `U_3ph` | 400 V | spec: Site supply |
| `U_DC` | 24 V | drawing: E-SCH-010 rev A |
| `U_b` | 24 V | drawing: E-SCH-010 rev A |
| `cosphi_add` | 0.85 | assumption: Not used while P_add is 0; template default kept. |
| `eta_add` | 0.9 | assumption: Not used while P_add is 0; template default kept. |
| `eta_b` | 0.95 | assumption: Discharge efficiency. |
| `eta_conv` | 1 | assumption: DC UPS module, loads on the battery bus. |
| `eta_psu` | 0.9 | assumption: 24 V supply efficiency 90 %, power factor taken as 1. |
| `f_derate` | 1 | assumption: PSU rated to 60 °C without derating (datasheet). |
| `heat_add` | 0 | assumption: Not used while P_add is 0; template default kept. |
| `inrush_add` | 1 | assumption: Not used while P_add is 0; template default kept. |
| `k_T` | 3 | assumption: Short-time overload allowed by the supply authority. |
| `k_age` | 0.8 | assumption: End-of-life capacity 80 %. |
| `k_temp` | 1 | assumption: Battery kept at 20 °C. |
| `loads` | 6 row(s) | drawing: E-SCH-010 rev A |
| `psu_bus` | AC single-phase | drawing: E-SCH-010 rev A |
| `t_auto` | 2 h | spec: Telemetry to stay up for 2 h on mains loss |

| Output | Expected | Computed | Matches expected |
|---|---|---|---|
| `P_dem` | 11.182 kW | 11.182 kW | yes |
| `S_dem` | 12.9928 kVA | 12.9928 kVA | yes |
| `S_peak` | 83.3416 kVA | 83.3416 kVA | yes |
| `I_b` | 5 A | 5 A | yes |
| `C_req` | 16.4474 A*h | 16.4474 A*h | yes |

Governing check: Starting peak against the transformer short-time rating — FAIL (utilisation 1.12). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
