# E1 — Motor and actuator sizing (`elec.motor-sizing@1.0.1`)

**In plain terms.** A motor must give enough torque to accelerate the load and overcome friction and gravity, at the speed the move needs. The worksheet turns the motion profile and the mechanics (screw, belt, rack or gearbox) into the torque at the motor shaft for each part of the cycle. The peak torque must stay within the motor's short-term rating, the root-mean-square torque within its continuous rating so it does not overheat, and the load's inertia should not be too large compared with the motor's for stable control.

**Scope.** Linear (screw, belt/pulley, rack/pinion) or rotary (direct, gearbox) loads: friction, gravity on an incline, process force, load inertia; reflected inertia and load torque at the motor; trapezoidal or S-curve (sine-acceleration) point-to-point move with dwell; peak and RMS torque, peak speed and power, thermal duty, inertia ratio; candidate-motor checks (peak, continuous, speed) with a safety margin.

**Method.** k = travel per motor radian; J_refl = m k²/η or J_L/(i² η); T_load = F k/η or T_L/(i η); trapezoid v_pk = s/(t_move − t_acc), a = v_pk/t_acc; S-curve a_pk = (π/2) a_trap (D-22); T_acc = J_total α; T_pk = T_acc + T_load; T_rms = √(Σ T_i² t_i / t_cycle) with the dwell at zero torque.

**Inputs.** `mechanism`, `i`, `eta`, `p_screw`, `D_pulley`, `J_mech`, `m`, `mu`, `theta`, `F_p`, `J_L`, `T_L`, `profile`, `s`, `t_move`, `f_acc`, `t_dwell`, `J_motor`, `T_rated`, `T_peak`, `n_max`, `SF`, `J_ratio_max`. **Outputs.** `k`, `T_load`, `J_refl`, `J_total`, `v_pk`/`theta_m`, `a_pk`, `alpha_pk`, `T_acc`, `T_pk`, `T_dec`, `T_rms`, `n_pk`, `P_pk`, `J_ratio`, `duty`, `u_peak`, `u_rms`, `u_speed`. **Checks.** peak torque, RMS torque, speed (with SF); warnings for inertia ratio, overhauling, triangular profile.

**Test cases.** Ball-screw slide and geared index table, both analytic (Confirmed).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Ball-screw axis of a pick-and-place machine ([sheet](../../worked-examples/electrical/elec.motor-sizing/ball-screw-pick-and-place/sheet.txt)).

```text
k = 10 mm / (2 × π × 1) = 1.5915 mm
F_load = 50 kg × 9.80665 m/s^2 × (sin(0 deg) + 0.1 × cos(0 deg)) + 0 N = 49.033 N
T_load = 49.033 N × 1.5915 mm / 0.9 = 0.08671 N·m
J_refl = 50 kg × (1.5915 mm)^2 / 0.9 = 0.00014072 kg·m²
v_pk = 0.5 × 1 m / (0.5 s × (1 − 0.33333)) = 1.5 m/s
a_trap = 1.5 m/s / (0.33333 × 0.5 s) = 9 m/s²
omega_pk = 1.5 m/s / 1.5915 mm = 942.48 rad/s
a_pk = 9 m/s²
alpha_pk = 9 m/s² / 1.5915 mm = 5654.9 rad/s²
J_total = 0.0002 kg·m² + 0.0001 kg·m² + 0.00014072 kg·m² = 0.00044072 kg·m²
T_acc = 0.00044072 kg·m² × 5654.9 rad/s² = 2.4922 N·m
T_pk = 2.4922 N·m + 0.08671 N·m = 2.5789 N·m
T_dec = −2.4922 N·m + 0.08671 N·m = -2.4055 N·m
n_pk = 942.48 rad/s = 9000 rpm
… 10 more step(s) on the sheet
Governing: PASS  RMS torque SF·T_rms ≤ T_rated: u_rms = 0.90012 vs u_rms ≤ 1  utilisation 0.9
```
<!-- hand-arithmetic:end -->
