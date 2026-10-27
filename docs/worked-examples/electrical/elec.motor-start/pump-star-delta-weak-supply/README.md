# 160 kW pump motor started star-delta from a 500 kVA transformer on a weak 15 MVA network (`elec.motor-start`)

**Source.** Analytic (complex potential divider, recomputed with Python complex arithmetic; test values invented for this example), I_n = 276.244 A, I_start = 7·I_n/3 = 644.570 A, |Z_m| = 0.358286 Ω at cos φ_st 0.3; transformer |Z_T| = 0.0128 Ω at X/R 4.2 in series with the 15 MVA network |Z_Q| = 0.0106667 Ω at X/R 10, |Z_s| = 0.0234144 Ω; cable ρ_70 = 0.020628, R = 0.0130282 Ω, X = 0.0048 Ω; |V_m| = 367.412 V (dip 8.1470 %), V_pcc dip 5.9289 %, |I| = 592.057 A; direct on line the dip would be 21.08 % at the motor; Q_c = 35.547 kvar to 0.95, h = 13.86 — Confirmed.

A 160 kW pump on a 500 kVA, 4 % transformer fed from a weak 15 MVA network (minimum fault level) would dip 21.1 % at the motor if started direct on line, well past the 10 % limit. Started star-delta (one third of the current, 645 A at power factor 0.3) it dips to 367.4 V at the motor terminals, 8.1 % against 10 % (utilisation 0.81), and 5.9 % at the point of common coupling against the 7 % agreed with the network operator (utilisation 0.85, the governing check). The upstream network is modelled in series with the transformer (0.0107 Ω at X/R 10 on top of 0.0128 Ω at X/R 4.2), which a transformer on an infinite bus would miss. The current spike when the winding is switched from star to delta is not modelled. Correcting the running power factor from 0.88 to 0.95 needs 35.5 kvar (51.3 A); the 6.8 MVA short-circuit level puts the parallel resonance at order 13.9. The transformer, network, motor and agreement figures are test values invented for this example; replace them with the datasheets and the operator's letter.

| Input | Value | Source |
|---|---|---|
| `A_c` | 95 mm^2 | spec: Cable schedule CS-31: 95 mm² copper |
| `I_LR_r` | 7 | reference: record: motor MD-21 locked-rotor current ratio 7.0 (datasheet; test value invented for this example) |
| `I_k` | 15 kA | assumption: The source is the transformer, not a fault level, so this is not used; template default kept. |
| `L_cab` | 60 m | drawing: E-LAY-031 rev B |
| `P` | 160 kW | spec: Pump schedule PS-05: 160 kW motor rated output (test value invented for this example) |
| `S_k` | 10 MVA | assumption: The source is the transformer, not a fault level, so this is not used; template default kept. |
| `S_kQ` | 15 MVA | spec: Network operator letter NO-7: minimum fault level 15 MVA at the transformer primary (test value invented for this example) |
| `S_n` | 500 kVA | spec: Transformer datasheet TR-01: 500 kVA rating plate |
| `T_c` | 70 degC | assumption: Cable warm from the running load before a restart. |
| `U` | 400 V | spec: Site supply SS-1: 400 V three-phase |
| `XR` | 4.2 | reference: record: transformer TR-01 X/R 4.2 (from the load-loss figure; test value invented for this example) |
| `XR_Q` | 10 | assumption: Network X/R taken as 10, mostly reactive. |
| `X_km` | 0.08 ohm/km | reference: record: cable reactance 0.08 Ω/km (generic value as the E3 default; replace with the cable datasheet figure) |
| `alpha` | 0.00393 1/K | reference: record: annealed copper, 0.00393 per K at 20 °C, derived physical constant |
| `cable_data` | resistivity and area | spec: Cable schedule CS-31: resistivity route as E3 |
| `cos_phi` | 0.88 | spec: Motor datasheet MD-21: 0.88 at full load (test value invented for this example) |
| `cos_phi_st` | 0.3 | reference: record: motor MD-21 locked-rotor power factor 0.3 (datasheet; test value invented for this example) |
| `cos_phi_target` | 0.95 | spec: Connection agreement CA-2: power factor not below 0.95 |
| `cos_phi_vfd` | 0.95 | assumption: The starter is not a soft starter or drive, so this is not used; template default kept. |
| `dip_limit` | 10 % | spec: Machine specification MS-3: motor terminal voltage not below 90 % while starting |
| `dip_limit_pcc` | 7 % | spec: Connection agreement CA-2: 7 % at the point of common coupling (test value invented for this example) |
| `eta` | 0.95 | spec: Motor datasheet MD-21: 95 % at full load (test value invented for this example) |
| `fault_basis` | Fault level S_k (MVA) | assumption: The source is the transformer, not a fault level, so this is not used; template default kept. |
| `harmonics` | no | assumption: No drives or other harmonic loads on the supply. |
| `k_soft` | 3.5 | assumption: The starter is not a soft starter or drive, so this is not used; template default kept. |
| `k_vfd` | 1.5 | assumption: The starter is not a soft starter or drive, so this is not used; template default kept. |
| `n_par` | 1 | drawing: E-SCH-031 rev A |
| `rho_20` | 0.01724 ohm*mm^2/m | reference: record: annealed copper 1.724e-8 Ω·m at 20 °C, derived physical constant |
| `source` | Transformer | drawing: E-SCH-031 rev A |
| `starter` | Star-delta | drawing: E-SCH-031 rev A |
| `u_k` | 4 % | reference: record: transformer TR-01 impedance voltage 4 % (rating plate; test value invented for this example) |

| Output | Expected | Computed | Matches expected |
|---|---|---|---|
| `I_rated` | 276.244 A | 276.244 A | yes |
| `I_start` | 644.57 A | 644.57 A | yes |
| `Z_src` | 0.0234144 ohm | 0.0234144 ohm | yes |
| `I_dip` | 592.057 A | 592.057 A | yes |
| `V_m` | 367.412 V | 367.412 V | yes |
| `start_dip` | 8.14701 % | 8.14701 % | yes |
| `dip_pcc` | 5.92895 % | 5.92895 % | yes |
| `u_dip` | 0.814701  | 0.814701  | yes |
| `u_dip_pcc` | 0.846992  | 0.846992  | yes |
| `kvar` | 35.5467 kvar | 35.5467 kvar | yes |
| `I_cap` | 51.3073 A | 51.3073 A | yes |
| `S_sc` | 6.8334 MVA | 6.8334 MVA | yes |
| `h_res` | 13.865  | 13.865  | yes |

Governing check: Point-of-common-coupling dip against the limit — PASS (utilisation 0.85). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
