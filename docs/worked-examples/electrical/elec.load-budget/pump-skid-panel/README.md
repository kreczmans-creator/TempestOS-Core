# Pump skid control panel: supply, PSU, transformer and UPS battery (`elec.load-budget`)

**Source.** Analytic (load budget arithmetic), Two pumps, a fan, panel lighting, PLC and comms; 2 h autonomy on the 24 V controls — Confirmed.

Duty and standby pumps (standby at diversity 0), a panel fan and light, and a 24 V PLC with its router on a 2 h battery. Demand is 11.18 kW and 12.99 kVA (the 24 V supply's input counted on the single-phase bus at 90 % efficiency), well inside the 25 kVA allocation, but the duty pump start lifts the peak to 83.3 kVA, above three times the allocation (75 kVA): the starting check fails and calls for a soft starter or a larger allocation. The 240 W PSU and the 17 A·h battery are checked against the minimums.

| Input | Value | Source |
|---|---|---|
| `C_batt` | 17 A*h | drawing: E-SCH-010 rev A |
| `DoD` | 0.8 | assumption: VRLA, 80 % depth of discharge. |
| `P_PSU` | 240 W | drawing: E-SCH-010 rev A |
| `S_T` | 25 kVA | spec: Existing site transformer allocation |
| `U_1ph` | 230 V | spec: Site supply |
| `U_3ph` | 400 V | spec: Site supply |
| `U_DC` | 24 V | drawing: E-SCH-010 rev A |
| `U_b` | 24 V | drawing: E-SCH-010 rev A |
| `eta_b` | 0.95 | assumption: Discharge efficiency. |
| `eta_conv` | 1 | assumption: DC UPS module, loads on the battery bus. |
| `eta_psu` | 0.9 | assumption: 24 V supply efficiency 90 %, power factor taken as 1. |
| `f_derate` | 1 | assumption: PSU rated to 60 °C without derating (datasheet). |
| `k_T` | 3 | assumption: Short-time overload allowed by the supply authority. |
| `k_age` | 0.8 | assumption: End-of-life capacity 80 %. |
| `k_temp` | 1 | assumption: Battery kept at 20 °C. |
| `loads` | 6 row(s) | drawing: E-SCH-010 rev A |
| `margin` | 1.25 | assumption: 25 % growth margin. |
| `psu_bus` | AC single-phase | drawing: E-SCH-010 rev A |
| `t_auto` | 2 h | spec: Telemetry to stay up for 2 h on mains loss |
| `theta` | 45 degC | spec: Plant room design maximum |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `P_dem` | 11.182 kW | 11.182 kW | PASS |
| `S_dem` | 12.9928 kVA | 12.9928 kVA | PASS |
| `S_peak` | 83.3416 kVA | 83.3416 kVA | PASS |
| `I_b` | 5 A | 5 A | PASS |
| `C_req` | 16.4474 A*h | 16.4474 A*h | PASS |

Governing check: Starting peak against transformer overload — FAIL (utilisation 1.111). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
