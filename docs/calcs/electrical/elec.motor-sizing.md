# E1 — Motor and actuator sizing (`elec.motor-sizing@1.0.0`)

**Scope.** Linear (screw, belt/pulley, rack/pinion) or rotary (direct, gearbox) loads: friction, gravity on an incline, process force, load inertia; reflected inertia and load torque at the motor; trapezoidal or S-curve (sine-acceleration) point-to-point move with dwell; peak and RMS torque, peak speed and power, thermal duty, inertia ratio; candidate-motor checks (peak, continuous, speed) with a safety margin.

**Method.** k = travel per motor radian; J_refl = m k²/η or J_L/(i² η); T_load = F k/η or T_L/(i η); trapezoid v_pk = s/(t_move − t_acc), a = v_pk/t_acc; S-curve a_pk = (π/2) a_trap (D-22); T_acc = J_total α; T_pk = T_acc + T_load; T_rms = √(Σ T_i² t_i / t_cycle) with the dwell at zero torque.

**Inputs.** `mechanism`, `i`, `eta`, `p_screw`, `D_pulley`, `J_mech`, `m`, `mu`, `theta`, `F_p`, `J_L`, `T_L`, `profile`, `s`, `t_move`, `f_acc`, `t_dwell`, `J_motor`, `T_rated`, `T_peak`, `n_max`, `SF`, `J_ratio_max`. **Outputs.** `k`, `T_load`, `J_refl`, `J_total`, `v_pk`/`theta_m`, `a_pk`, `alpha_pk`, `T_acc`, `T_pk`, `T_dec`, `T_rms`, `n_pk`, `P_pk`, `J_ratio`, `duty`, `u_peak`, `u_rms`, `u_speed`. **Checks.** peak torque, RMS torque, speed (with SF); warnings for inertia ratio, overhauling, triangular profile.

**Test cases.** Ball-screw slide and geared index table, both analytic (Confirmed).
