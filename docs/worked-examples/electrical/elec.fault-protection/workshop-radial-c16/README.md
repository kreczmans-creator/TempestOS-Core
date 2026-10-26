# Workshop radial: 16 A type C MCB, 40 m of 4/2.5 mm² copper (`elec.fault-protection`)

**Source.** Analytic (C_min relation; derived k), TN-S supply with the declared Z_e of 0.8 Ω; let-through I²t from the MCB datasheet — Confirmed.

A 40 m radial on a 16 A type C MCB. With Z_e = 0.8 Ω the loop reaches 1.403 Ω against a maximum of 1.366 Ω (utilisation 1.03): it fails, narrowly. At the pure-copper resistance it would have passed (1.336 Ω, utilisation 0.98); the conductor resistance used here is at or above the cable standard's maximum (k_R 1.125; the standard's maxima are 5 to 12 % above pure copper, 4.9 % for these 4 and 2.5 mm² conductors, so the verdict holds at the true maxima), which is what the tabulated loop impedances assume. The fault current, 155.7 A, is below the 160 A instantaneous trip, so the disconnection time and the adiabatic check are not evaluated. A run of about 37 m or less, larger conductors or a type B device (wide margin) would pass.

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
| `Z_e` | 0.8 ohm | spec: DNO declared maximum for TN-S |
| `alpha` | 0.00393 1/K | assumption: Copper temperature coefficient. |
| `beta` | 234.5 K | assumption: Copper reciprocal temperature coefficient. |
| `c_max` | 1.1 | assumption: Maximum voltage factor. |
| `device` | MCB type C (I_a = 10 I_n) | drawing: E-SCH-002 rev B |
| `k_R` | 1.125 | assumption: Cable standard maximum conductor resistance over the pure-copper resistivity (5 to 12 % higher); 1.125 covers the sizes used. |
| `rho_20` | 0.000017241 ohm*mm | assumption: Annealed copper resistivity. |
| `t_req` | 0.4 s | spec: Final circuit up to 32 A, TN system |
| `theta` | 70 degC | assumption: Thermoplastic insulation at full load. |
| `theta_f` | 160 degC | assumption: Thermoplastic limiting temperature. |
| `theta_i` | 70 degC | assumption: Thermoplastic operating temperature. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `Z_s` | 1.40339 ohm | 1.40339 ohm | PASS |
| `Z_s_max` | 1.36563 ohm | 1.36563 ohm | PASS |
| `u_Zs` | 1.02766  | 1.02766  | PASS |

Governing check: Disconnection achieved — FAIL (utilisation —). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
