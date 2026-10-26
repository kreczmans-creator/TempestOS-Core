# Rotary index table through a 10:1 gearbox (`elec.motor-sizing`)

**Source.** Analytic servo-sizing relations, Reflected inertia J_L/(i² η); inertia ratio above 10 warns — Confirmed.

A 90° index in 0.3 s through a 10:1 gearbox: the motor sees 5.9e-3 kg·m² of reflected inertia, nearly twelve times its own rotor inertia, so the sheet warns on the inertia ratio even though the torque and speed checks pass.

| Input | Value | Source |
|---|---|---|
| `J_L` | 0.5 kg*m^2 | spec: S1-style inertia calc of the 600 mm table with fixtures: 0.5 kg·m² |
| `J_mech` | 0 kg*m^2 | assumption: Sizing convention. |
| `J_motor` | 0.0005 kg*m^2 | spec: Motor datasheet SM-060: 5.0e-4 kg·m², 3.0 / 9.0 N·m, 3000 rpm |
| `J_ratio_max` | 10 | assumption: Sizing convention. |
| `SF` | 1.25 | assumption: Sizing convention. |
| `T_L` | 0 N*m | assumption: Sizing convention. |
| `T_peak` | 9 N*m | spec: Motor datasheet SM-060: 5.0e-4 kg·m², 3.0 / 9.0 N·m, 3000 rpm |
| `T_rated` | 3 N*m | spec: Motor datasheet SM-060: 5.0e-4 kg·m², 3.0 / 9.0 N·m, 3000 rpm |
| `eta` | 0.85 | spec: Gearbox datasheet PG-10: 10:1, 85 % |
| `f_acc` | 0.333333 | spec: Cycle specification CS-7: 90° index in 0.3 s, 0.7 s dwell |
| `i` | 10 | spec: Gearbox datasheet PG-10: 10:1, 85 % |
| `mechanism` | gearbox rotary | assumption: Sizing convention. |
| `n_max` | 3000 rpm | spec: Motor datasheet SM-060: 5.0e-4 kg·m², 3.0 / 9.0 N·m, 3000 rpm |
| `profile` | trapezoidal | assumption: Sizing convention. |
| `s` | 0.25 | spec: Cycle specification CS-7: 90° index in 0.3 s, 0.7 s dwell |
| `t_dwell` | 0.7 s | spec: Cycle specification CS-7: 90° index in 0.3 s, 0.7 s dwell |
| `t_move` | 0.3 s | spec: Cycle specification CS-7: 90° index in 0.3 s, 0.7 s dwell |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `T_pk` | 5.0126 N*m | 5.01269 N*m | PASS |
| `J_ratio` | 11.76  | 11.7647  | PASS |

Governing check: RMS torque SF·T_rms ≤ T_rated — PASS (utilisation 0.93). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
