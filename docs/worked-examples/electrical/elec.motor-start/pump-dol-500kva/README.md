# 75 kW pump motor started direct on line from a 500 kVA transformer (`elec.motor-start`)

**Source.** Analytic (complex potential divider, recomputed with Python complex arithmetic; test values invented for this example), I_n = 131.671 A, I_start = 7·I_n = 921.696 A, |Z_m| = 0.250560 Ω at cos φ_st 0.3; |Z_s| = 0.04·400²/500 kVA = 0.0128 Ω at X/R 4.2; cable ρ_70 = 0.020628 Ω·mm²/m, R = 0.0165021 Ω, X = 0.0032 Ω; |V_m| = 368.897 V (dip 7.7757 %), V_pcc dip 4.6746 %, |I| = 850.027 A; Q_c = 18.892 kvar to 0.95, h = 25.72 — Confirmed.

A 75 kW pump started direct on line (7 × I_n = 922 A at power factor 0.3) from a 500 kVA, 4 % transformer through 40 m of 50 mm² copper dips to 368.9 V at the motor terminals, a 7.8 % dip against the 10 % limit (utilisation 0.78), and 4.7 % at the point of common coupling against the 5 % agreed with the network operator (utilisation 0.93, the governing check). The complex divider is used because the source, cable and motor are at different angles (76.6°, 11.0° and 72.5°); a scalar divider of the magnitudes would over-state the dip. Correcting the running power factor from 0.87 to 0.95 needs 18.9 kvar (27.3 A of capacitor current); the 12.5 MVA short-circuit level puts the parallel resonance at order 25.7, far above the usual harmonics. The impedance and starting figures are test values invented for this example; replace them with the transformer and motor datasheets.

| Input | Value | Source |
|---|---|---|
| `A_c` | 50 mm^2 | spec: Cable schedule CS-31: 50 mm² copper |
| `I_LR_r` | 7 | reference: record: motor MD-12 locked-rotor current ratio 7.0 (datasheet; test value invented for this example) |
| `L_cab` | 40 m | drawing: E-LAY-031 rev B |
| `P` | 75 kW | spec: Pump schedule PS-03: 75 kW motor rated output |
| `S_n` | 500 kVA | spec: Transformer datasheet TR-01: 500 kVA rating plate |
| `T_c` | 70 degC | assumption: Cable warm from the running load before a restart. |
| `U` | 400 V | spec: Site supply SS-1: 400 V three-phase |
| `XR` | 4.2 | reference: record: transformer TR-01 X/R 4.2 (from the load-loss figure; test value invented for this example) |
| `X_km` | 0.08 ohm/km | reference: record: cable reactance 0.08 Ω/km (generic value as the E3 default; replace with the cable datasheet figure) |
| `alpha` | 0.00393 1/K | reference: record: annealed copper, 0.00393 per K at 20 °C, derived physical constant |
| `cable_data` | resistivity and area | spec: Cable schedule CS-31: resistivity route as E3 |
| `cos_phi` | 0.87 | spec: Motor datasheet MD-12: 0.87 at full load |
| `cos_phi2` | 0.95 | spec: Connection agreement CA-2: power factor not below 0.95 |
| `cos_phi_st` | 0.3 | reference: record: motor MD-12 locked-rotor power factor 0.3 (datasheet; test value invented for this example) |
| `dip_limit` | 10 % | spec: Machine specification MS-3: motor terminal voltage not below 90 % while starting |
| `dip_limit_pcc` | 5 % | spec: Connection agreement CA-2: 5 % at the point of common coupling (test value invented for this example) |
| `eta` | 0.945 | spec: Motor datasheet MD-12: 94.5 % at full load |
| `harmonics` | no | assumption: No drives or other harmonic loads on the supply. |
| `n_par` | 1 | drawing: E-SCH-031 rev A |
| `rho_20` | 0.01724 ohm*mm^2/m | reference: record: annealed copper 1.724e-8 Ω·m at 20 °C, derived physical constant |
| `source` | Transformer | drawing: E-SCH-031 rev A |
| `starter` | Direct on line (DOL) | drawing: E-SCH-031 rev A |
| `u_k` | 4 % | reference: record: transformer TR-01 impedance voltage 4 % (rating plate; test value invented for this example) |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `I_rated` | 131.671 A | 131.671 A | PASS |
| `I_start` | 921.696 A | 921.696 A | PASS |
| `Z_src` | 0.0128 ohm | 0.0128 ohm | PASS |
| `I_dip` | 850.027 A | 850.027 A | PASS |
| `V_m` | 368.897 V | 368.897 V | PASS |
| `start_dip` | 7.77574 % | 7.77574 % | PASS |
| `dip_pcc` | 4.67456 % | 4.67456 % | PASS |
| `u_dip` | 0.777574  | 0.777574  | PASS |
| `u_dip_pcc` | 0.934912  | 0.934912  | PASS |
| `kvar` | 18.8922 kvar | 18.8922 kvar | PASS |
| `I_cap` | 27.2686 A | 27.2686 A | PASS |
| `h_res` | 25.7225  | 25.7225  | PASS |

Governing check: Point-of-common-coupling dip within the limit — PASS (utilisation 0.935). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
