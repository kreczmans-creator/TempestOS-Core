# Three-phase motor feeder, 100 A over 80 m (`elec.voltage-drop`)

**Source.** Analytic (√3 I (R cos φ + X sin φ)); BS 7671 Appendix 4 method for the temperature estimate, R from resistivity at 70 °C; X′ = 0.08 Ω/km — Confirmed.

A 35 mm² copper feeder carrying 100 A over 80 m drops 7.5 V (1.9 %) against the 5 % project limit; the conductor runs at an estimated 58 °C, below the 70 °C used for the resistance, so the drop figure is on the safe side.

| Input | Value | Source |
|---|---|---|
| `A_c` | 35 mm^2 | spec: Cable schedule CS-01: 35 mm² Cu XLPE/SWA |
| `I` | 100 A | spec: Motor schedule MS-01: 100 A full-load current |
| `I_z` | 119 A | spec: Cable schedule CS-01: I_z = 119 A for the installation method |
| `L` | 80 m | drawing: DRG-E-120 rev B |
| `T_amb` | 30 degC | assumption: Design convention. |
| `T_c` | 70 degC | assumption: Design convention. |
| `T_rated` | 70 degC | assumption: Design convention. |
| `V_n` | 400 V | assumption: Design convention. |
| `X_km` | 0.08 ohm/km | spec: Cable datasheet: 0.08 Ω/km |
| `alpha` | 0.00393 | reference: Reference record: annealed copper 0.01724 Ω·mm²/m, 0.00393/K (Hughes) |
| `cos_phi` | 0.85 | assumption: Design convention. |
| `data` | resistivity and area | assumption: Design convention. |
| `dv_limit` | 5 | spec: Project spec ES-3 §4: 5 % (BS 7671 Appendix 4 §6.4 default for other uses) |
| `n_par` | 1 | assumption: Design convention. |
| `rho_20` | 0.01724 ohm*mm^2/m | reference: Reference record: annealed copper 0.01724 Ω·mm²/m, 0.00393/K (Hughes) |
| `system` | AC three-phase | assumption: Design convention. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `dV_pct` | 1.8815  | 1.88135  | PASS |
| `T_c_est` | 58.25 degC | 58.2466 degC | PASS |

Governing check: I ≤ I_z — PASS (utilisation 0.84). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
