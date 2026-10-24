# Workshop radial: 16 A type C MCB, 40 m of 4/2.5 mm² copper (`elec.fault-protection`)

**Source.** Analytic (C_min relation; derived k), TN-C-S supply with the declared Z_e of 0.8 Ω; let-through I²t from the MCB datasheet — Confirmed.

A 40 m radial on a 16 A type C MCB. With Z_e = 0.8 Ω the loop reaches 1.336 Ω against a maximum of 1.366 Ω (utilisation 0.98): it passes, narrowly, and a measured value above the 1.09 Ω field-test figure would need investigating. A type B device would leave a wide margin. The protective conductor easily passes the adiabatic check (0.34 mm² needed).

| Input | Value | Source |
|---|---|---|
| `C_min` | 0.95 | assumption: Minimum voltage factor. |
| `I2t_let` | 1500 A^2*s | assumption: Read from the MCB let-through chart at the fault current. |
| `I_n` | 16 A | drawing: E-SCH-002 rev B |
| `L` | 40 m | drawing: E-LAY-002 rev B |
| `Q_c` | 0.00345 J/(K*mm^3) | assumption: Copper volumetric heat capacity. |
| `S_cpc` | 2.5 mm^2 | drawing: E-SCH-002 rev B |
| `S_line` | 4 mm^2 | drawing: E-SCH-002 rev B |
| `U_0` | 230 V | spec: Supply characteristics |
| `Z_e` | 0.8 ohm | spec: DNO declared maximum for TN-C-S |
| `alpha` | 0.00393 1/K | assumption: Copper temperature coefficient. |
| `beta` | 234.5 K | assumption: Copper reciprocal temperature coefficient. |
| `c_max` | 1.1 | assumption: Maximum voltage factor. |
| `device` | MCB type C (I_a = 10 I_n) | drawing: E-SCH-002 rev B |
| `rho_20` | 0.000017241 ohm*mm | assumption: Annealed copper resistivity. |
| `t_req` | 0.4 s | spec: Final circuit up to 32 A, TN system |
| `theta` | 70 degC | assumption: Thermoplastic insulation at full load. |
| `theta_f` | 160 degC | assumption: Thermoplastic limiting temperature. |
| `theta_i` | 70 degC | assumption: Thermoplastic operating temperature. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `Z_s` | 1.33635 ohm | 1.33635 ohm | PASS |
| `Z_s_max` | 1.36563 ohm | 1.36563 ohm | PASS |
| `u_Zs` | 0.978563  | 0.978563  | PASS |
| `S_min` | 0.337262 mm^2 | 0.337262 mm^2 | PASS |

Governing check: Loop impedance within maximum (disconnection in time) — PASS (utilisation 0.979). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
