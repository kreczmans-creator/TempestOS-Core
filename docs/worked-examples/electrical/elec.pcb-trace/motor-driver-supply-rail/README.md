# Motor-driver supply rail on a 2 oz outer layer, 5 A at 20 K rise (`elec.pcb-trace`)

**Source.** Analytic (IPC-2221 legacy curve-fit I = k·ΔT^0.44·A^0.725, recomputed; copper resistivity with temperature coefficient; test values invented for this example), A = [5/(0.048·20^0.44)]^(1/0.725) = 98.5107 mil², t = 2.756 mil, w_min = 0.907900 mm; T = 45 °C, ρ = 0.01893383 Ω·mm²/m; R = 10.81896 mΩ over 100 mm at 2.5 mm; ΔV = 54.0948 mV; P = 270.474 mW — Confirmed.

A 5 A motor supply rail on a 2 oz outer layer with 20 K allowed above 25 °C ambient needs only 0.91 mm by the legacy IPC-2221 curve-fit (conservative for a multilayer board); the 2.5 mm trace drawn over 100 mm is 36 % utilised, drops 54 mV and dissipates 0.27 W at its 45 °C operating temperature. The constants are published closed-form curve-fit values and copper physical constants; the IPC-2152 charts are not used.

| Input | Value | Source |
|---|---|---|
| `Cu_oz` | 2 | spec: Fabrication note FN-7: 2 oz/ft² finished copper on the outer layers |
| `I` | 5 A | spec: Driver datasheet DD-12: 5 A continuous motor supply |
| `L` | 100 mm | drawing: PCB-LAY-014 rev B |
| `T_amb` | 25 degC | spec: Environmental specification ES-2: 25 °C at the board |
| `alpha` | 0.00393 1/K | reference: record: annealed copper, 0.00393 per K at 20 °C, derived physical constant |
| `dT` | 20 K | spec: Board design rule BDR-3: 20 K allowed rise on power rails |
| `k_ext` | 0.048 | reference: record: IPC-2221 legacy curve-fit constants (published closed form; the issuer enters the edition) |
| `k_int` | 0.024 | reference: record: IPC-2221 legacy curve-fit constants (published closed form; the issuer enters the edition) |
| `layer` | External layer | drawing: PCB-LAY-014 rev B |
| `n_A` | 0.725 | reference: record: IPC-2221 legacy curve-fit constants (published closed form; the issuer enters the edition) |
| `n_dT` | 0.44 | reference: record: IPC-2221 legacy curve-fit constants (published closed form; the issuer enters the edition) |
| `rho_20` | 0.01724 ohm*mm^2/m | reference: record: annealed copper 1.724e-8 Ω·m at 20 °C, derived physical constant |
| `t_oz` | 1.378 mil | reference: record: IPC-2221 legacy curve-fit constants; 1.378 mil = 35 µm per oz/ft², derived |
| `w_act` | 2.5 mm | drawing: PCB-LAY-014 rev B |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `A_req` | 98.5107 mil^2 | 98.5107 mil^2 | PASS |
| `w_min` | 0.9079 mm | 0.9079 mm | PASS |
| `u_w` | 0.36316  | 0.36316  | PASS |
| `trace_R` | 10.819 mohm | 10.819 mohm | PASS |
| `vdrop` | 54.0948 mV | 54.0948 mV | PASS |
| `trace_Ploss` | 270.474 mW | 270.474 mW | PASS |

Governing check: Minimum trace width against the width as drawn — PASS (utilisation 0.36). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
