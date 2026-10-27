# Rotary index table through a 10:1 gearbox (`elec.motor-sizing`)

**Source.** Analytic servo-sizing relations, Reflected inertia J_L/(i² η); inertia ratio above 10 warns — Confirmed.

A 90° index in 0.3 s through a 10:1 gearbox: the motor sees 5.9e-3 kg·m² of reflected inertia, nearly twelve times its own rotor inertia, so the sheet warns on the inertia ratio even though the torque and speed checks pass.

| Input | Value | Source |
|---|---|---|
| `F_p` | 0 N | assumption: Linear axes only; the rotary table does not use it. |
| `J_L` | 0.5 kg*m^2 | spec: S1-style inertia calc of the 600 mm table with fixtures: 0.5 kg·m² |
| `J_mech` | 0 kg*m^2 | assumption: Sizing convention. |
| `J_motor` | 0.0005 kg*m^2 | spec: Motor datasheet SM-060: 5.0e-4 kg·m², 3.0 / 9.0 N·m, 3000 rpm |
| `J_ratio_max` | 10 | assumption: Sizing convention. |
| `SF` | 1.25 | assumption: Sizing convention. |
| `T_L` | 0 N*m | assumption: Sizing convention. |
| `T_basis` | Load peak (T_pk) | assumption: Design torque for the coupling taken at the load peak; template default. |
| `T_peak` | 9 N*m | spec: Motor datasheet SM-060: 5.0e-4 kg·m², 3.0 / 9.0 N·m, 3000 rpm |
| `T_rated` | 3 N*m | spec: Motor datasheet SM-060: 5.0e-4 kg·m², 3.0 / 9.0 N·m, 3000 rpm |
| `brake` | no | assumption: Linear axes only; the rotary table does not use it. |
| `duty_mode` | point-to-point | spec: Cycle specification CS-7: 90° index then dwell |
| `eta` | 0.85 | spec: Gearbox datasheet PG-10: 10:1, 85 % |
| `f_acc` | 0.333333 | spec: Cycle specification CS-7: 90° index in 0.3 s, 0.7 s dwell |
| `i` | 10 | spec: Gearbox datasheet PG-10: 10:1, 85 % |
| `mechanism` | gearbox rotary | assumption: Sizing convention. |
| `mu` | 0.1 | assumption: Linear axes only; the rotary table does not use it, so the template default is kept. |
| `n_max` | 3000 rpm | spec: Motor datasheet SM-060: 5.0e-4 kg·m², 3.0 / 9.0 N·m, 3000 rpm |
| `profile` | trapezoidal | assumption: Sizing convention. |
| `s` | 0.25 | spec: Cycle specification CS-7: 90° index in 0.3 s, 0.7 s dwell |
| `t_dwell` | 0.7 s | spec: Cycle specification CS-7: 90° index in 0.3 s, 0.7 s dwell |
| `t_move` | 0.3 s | spec: Cycle specification CS-7: 90° index in 0.3 s, 0.7 s dwell |
| `theta` | 0 deg | assumption: Linear axes only; the rotary table does not use it. |

| Output | Expected | Computed | Matches expected |
|---|---|---|---|
| `T_pk` | 5.0126 N*m | 5.01269 N*m | yes |
| `J_ratio` | 11.76  | 11.7647  | yes |

Governing check: RMS torque with the safety factor against the rated torque — PASS (utilisation 0.93). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
