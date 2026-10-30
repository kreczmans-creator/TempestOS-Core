# Ball-screw axis of a pick-and-place machine (`elec.motor-sizing`)

**Source.** Analytic servo-sizing relations (Hughes & Drury; manufacturer sizing guides), Reflected inertia, trapezoidal 1/3 profile, RMS torque with dwell — Confirmed.

A 50 kg carriage on a 10 mm-lead ball screw moving 0.5 m in 0.5 s. The candidate motor's peak (6 N·m) and continuous (2 N·m) torques are checked with a 1.25 margin; the 9000 rpm peak speed is the sizing driver, suggesting a coarser lead or a gearbox. The RMS mechanical power over the cycle is 0.784 kW against a 2.43 kW instantaneous peak: the RMS figure is the one to carry to a supply calculation.

| Input | Value | Source |
|---|---|---|
| `F_p` | 0 N | assumption: Sizing convention. |
| `J_mech` | 0.0001 kg*m^2 | spec: Screw datasheet BS-1610 + coupling: 1.0e-4 kg·m² |
| `J_motor` | 0.0002 kg*m^2 | spec: Motor datasheet SM-040: 2.0e-4 kg·m², 2.0 / 6.0 N·m, 10 000 rpm |
| `J_ratio_max` | 10 | assumption: Sizing convention. |
| `SF` | 1.25 | assumption: Sizing convention. |
| `T_L` | 0 N*m | assumption: Continuous rotary duty only; this point-to-point linear axis does not use it. |
| `T_basis` | Load peak (T_pk) | assumption: Design torque for the coupling taken at the load peak; template default. |
| `T_peak` | 6 N*m | spec: Motor datasheet SM-040: 2.0e-4 kg·m², 2.0 / 6.0 N·m, 10 000 rpm |
| `T_rated` | 2 N*m | spec: Motor datasheet SM-040: 2.0e-4 kg·m², 2.0 / 6.0 N·m, 10 000 rpm |
| `brake` | no | assumption: No brake: the axis is horizontal, so the dwell needs no holding torque. |
| `duty_mode` | point-to-point | spec: Cycle specification CS-3: point-to-point moves with a dwell |
| `eta` | 0.9 | spec: Screw datasheet BS-1610: 90 % |
| `f_acc` | 0.333333 | spec: Cycle specification CS-3: 0.5 m in 0.5 s, 0.5 s dwell, 1/3 ramps |
| `i` | 1 | assumption: Sizing convention. |
| `m` | 50 kg | spec: Mechanical layout ML-12: carriage + gripper 50 kg |
| `mechanism` | lead / ball screw | assumption: Sizing convention. |
| `mu` | 0.1 | assumption: Linear guides with seals: μ = 0.1. |
| `n_max` | 10000 rpm | spec: Motor datasheet SM-040: 2.0e-4 kg·m², 2.0 / 6.0 N·m, 10 000 rpm |
| `p_screw` | 10 mm | spec: Screw datasheet BS-1610: 10 mm lead |
| `profile` | trapezoidal | assumption: Sizing convention. |
| `s` | 0.5 | spec: Cycle specification CS-3: 0.5 m in 0.5 s, 0.5 s dwell, 1/3 ramps |
| `t_dwell` | 0.5 s | spec: Cycle specification CS-3: 0.5 m in 0.5 s, 0.5 s dwell, 1/3 ramps |
| `t_move` | 0.5 s | spec: Cycle specification CS-3: 0.5 m in 0.5 s, 0.5 s dwell, 1/3 ramps |
| `theta` | 0 deg | assumption: Sizing convention. |

| Output | Expected | Computed | Matches expected |
|---|---|---|---|
| `T_pk` | 2.579 N*m | 2.57894 N*m | yes |
| `T_rms` | 1.4402 N*m | 1.4402 N*m | yes |
| `n_pk` | 9000 rpm | 9000 rpm | yes |
| `P_rms` | 0.784142 kW | 0.784142 kW | yes |

Governing check: RMS torque with the safety factor against the rated torque — PASS (utilisation 0.90). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
