# Motor and actuator sizing (`elec.motor-sizing@1.11.1`)

**In plain terms.** A motor must give enough torque to accelerate the load and overcome friction and gravity, at the speed the move needs. The worksheet turns the motion profile and the mechanics (screw, belt, rack or gearbox) into the torque at the motor shaft for each part of the cycle. The peak torque must stay within the motor's short-term rating, the root-mean-square torque within its continuous rating so it does not overheat, and the load's inertia should not be too large compared with the motor's for stable control.

## Scope

Calculates the torque, speed and power a motor needs to move a load through a mechanism, and checks a candidate motor against them. A continuously running rotary load is sized from its steady torque, speed and run-up time.

## Method

- Linear mechanism: k = p/(2π i) (screw), D/(2 i) (belt), D_p/(2 i) (rack), in metres per motor radian; J_refl = m k²/η; T_load = F k/η, with F = m g (sin θ + μ cos θ) + F_p
- Rotary mechanism through a gearbox: J_refl = J_L/(i² η); T_load = T_L/(i η)
- Move profile: trapezoidal v_pk = s/(t_move − t_acc), a = v_pk/t_acc; S-curve (sine acceleration, same peak speed) a_pk = (π/2) a_trap
- Peak torque: T_acc = J_total α; T_pk = T_acc + T_load; T_dec = −T_acc + T_load; the check is SF·max(|T_pk|, |T_dec|) ≤ T_peak; braking power P_brake = max(0, −T_dec·ω_pk)
- Thermal duty: T_rms = √(Σ T_i² t_i / t_cycle) over the whole cycle, the dwell at T_hold = m g sin θ·k for a linear mechanism not held by a brake; P_rms = ω_pk √{[t_acc·(2/3)(T_load² + T_acc²) + T_load² t_const]/t_cycle}, and for the S-curve the acceleration term is t_acc·[(3/4) T_load² + (5/16) T_acc²]
- Output side and design torque: T_alt = (T_pk − T_dec)/2 (0 in continuous duty); T_out = T·i·η and n_out = n_pk/i for a rotary mechanism; F_ax_pk = F_load + m a_pk for a ball screw; T_design on the basis T_basis (load peak max(|T_pk|, |T_dec|), T_peak, SF_c·T_rated or T_lim)
- Continuous rotary duty: ω = 2π n_L i/60, n_pk = n_L i; T_load = T_L/(i η), T_rms = T_load; T_pk = J_total ω/t_start + T_load; P_cont = T_load ω, P_rms = P_cont
- Rated power of the candidate motor: P_rated = T_rated·2π n_rated/60 (without n_rated there is no P_rated); the check is P_cont ≤ P_rated, u_power = P_cont/P_rated

### Notes on the method

k = travel per motor radian; J_refl = m k²/η or J_L/(i² η); T_load = F k/η or T_L/(i η); trapezoid v_pk = s/(t_move − t_acc), a = v_pk/t_acc; S-curve a_pk = (π/2) a_trap (D-22); T_acc = J_total α; T_pk = T_acc + T_load; T_rms = √(Σ T_i² t_i / t_cycle) with the dwell at zero torque. **RMS power (1.2.0)**, the thermal-equivalent continuous power a supply sees (regeneration counts as power; the instantaneous peak `P_pk` is not a continuous demand): `P_rms = ω_pk √{[t_acc·(2/3)(T_load² + T_acc²) + T_load² t_const]/t_cycle}` for the trapezoid, with `t_acc·[(3/4) T_load² + (5/16) T_acc²]` for the sine acceleration, from integrating (T·ω)² over the phases; in continuous duty `P_rms = P_cont`. It is the port the motor-circuit pack links to load-budget (G-B-11).

**Continuous duty (1.1.0, rotary mechanisms only).** `ω = 2π n_L i/60`; `T_load = T_L/(i η)`; `T_rms = T_load`; `T_pk = J_total ω/t_start + T_load` (the whole inertia is accelerated linearly to speed in `t_start` while carrying the full load: conservative); `n_pk = n_L i`; `P_cont = T_load ω`; **`P_rated = T_rated·ω_rated` (1.3.0, rated speed 1.4.0)**, the candidate motor's nameplate rated shaft power from its rated torque `T_rated` and its rated speed `n_rated` (`ω_rated = 2π n_rated/60`; an optional input in the `motor.rating` record with the motor's other ratings): the power a supply cable or drive for the motor must be sized for, as distinct from `P_cont`, the power the load takes. The running speed is not a substitute (a 4-pole motor runs near, not at, its rated speed): without `n_rated` there is no `P_rated` and the sheet warns (`rated-speed`); a pack that binds `P_rated` downstream (the cable step) is refused without it, with the reason naming `n_rated`, and `n_rated` entered on a point-to-point sheet is ignored with the warning `rated-speed-ignored`. When the motor speed `n_pk = n_L·i` is above `n_rated` the sheet warns `speed-above-rated`: the motor runs past its nameplate point, where its rated torque and power no longer hold (a speed within a relative 10⁻⁶ of the nameplate is not warned about). The hydraulic power unit pack links `P_rated` to the cable step. The move distance, move time, acceleration fraction, dwell and profile are ignored, with the notice `continuous-ignored`. `n_L`, `t_start` and `J_L` are required (block `continuous-input`, message starting with the symbol), and a linear mechanism is refused. The input is named `duty_mode`, because `duty` is already the thermal-duty output. Point-to-point is the default and its numbers are unchanged.

**Torque split, output side and thrust (1.6.0).** For a shaft the motor drives: `T_alt = (T_pk − T_dec)/2`, the alternating amplitude of the cycle about the mean `T_load` (0 in continuous duty, where running is steady; with `T_load` as the steady torque it is what the shaft's fatigue check needs, the RMS torque being a thermal figure). Torques at the motor shaft are not the torques behind the ratio: for a rotary mechanism `T_out_m = T_load i η`, `T_out_a = T_alt i η`, `T_out_pk = T_pk i η` (conservative: the rotor's own inertia torque does not reach the output) and `n_out = n_pk/i` are declared ports, for a shaft after the gearbox. `F_ax_pk = F_load + m a_pk` is the peak axial thrust of a lead / ball screw on its support bearings (0 for every other mechanism: a belt or rack force reaches its bearings as a radial force; helical-gear thrust is not modelled).

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group |
|---|---|---|---|---|---|
| `mechanism` | Mechanism |  | lead / ball screw | lead / ball screw, belt and pulley, rack and pinion, gearbox rotary, direct rotary | Setup |
| `duty_mode` | Duty (point-to-point move cycle, or continuous rotary running with a run-up) |  | point-to-point | point-to-point, continuous | Setup |
| `profile` | Motion profile |  | trapezoidal | trapezoidal, S-curve (sine acceleration) | Setup |
| `T_basis` | Design torque basis for downstream couplings (T_design): the load's peak (the larger of the accelerating and the braking torque, in magnitude), the motor's typed peak torque, a service factor on the rated torque, or a protective limit such as a torque limiter |  | Load peak (T_pk) | Load peak (T_pk), Motor peak (T_peak), Service factor × rated (SF_c·T_rated), Protective limit (T_lim) | Setup |
| `brake` | A brake or a self-locking drive holds a linear axis during the dwell (no holding torque); unticked, a vertical or inclined axis is held by the motor |  | 0 |  | Setup |
| `J_mech` | Transmission inertia referred to the motor shaft (screw, pulleys, gearbox, coupling) | kg·m² | 0.0001 | 0 to 10 | Geometry |
| `p_screw` | Screw lead (per revolution of the screw) (optional) | mm | 10 | 1 to 100 | Geometry |
| `D_pulley` | Pulley or pinion pitch diameter (optional) | mm |  | 5 to 2000 | Geometry |
| `theta` | Incline from horizontal (+ moving uphill) | ° | 0 | -60 to 90 | Loads |
| `F_p` | Process force opposing the move (linear) | N | 0 | 0 to 1000000 | Loads |
| `T_L` | Constant load torque at the output (rotary; continuous duty needs it, and a zero value is refused) | N·m | 0 | 0 to 100000 | Loads |
| `m` | Moved mass (linear mechanisms) (optional) | kg | 50 | 0 to 100000 | Loads |
| `J_L` | Load inertia at the output (rotary mechanisms) (optional) | kg·m² |  | 0 to 10000 | Loads |
| `i` | Gear ratio (motor : output, 1 = none) |  | 1 | 1 to 1000 | Factors |
| `eta` | Mechanism efficiency (overall) |  | 0.9 | 0.3 to 1 | Factors |
| `mu` | Friction coefficient (guide / bearing) |  | 0.1 | 0 to 1 | Factors |
| `SF` | Safety factor on torque |  | 1.25 | 1 to 3 | Requirements |
| `J_ratio_max` | Inertia ratio limit (load : motor), warning |  | 10 | 1 to 100 | Requirements |
| `s` | Move distance (linear: m; rotary: output revolutions) |  | 0.5 | 0.0001 to 10000 | Motion |
| `t_move` | Move time | s | 0.5 | 0.01 to 3600 | Motion |
| `f_acc` | Acceleration (and deceleration) fraction of the move time |  | 0.333333 | 0.05 to 0.5 | Motion |
| `t_dwell` | Dwell between moves | s | 0.5 | 0 to 3600 | Motion |
| `n_L` | Load speed at the output (continuous duty) (optional) | rpm |  | 1 to 100000 | Motion |
| `t_start` | Run-up time to full speed (continuous duty) (optional) | s |  | 0.1 to 600 | Motion |
| `J_motor` | Candidate motor rotor inertia | kg·m² | 0.0002 | 1e-07 to 10 | Motor |
| `T_rated` | Candidate motor continuous (rated) torque | N·m | 2 | 0.01 to 100000 | Motor |
| `T_peak` | Candidate motor peak torque | N·m | 6 | 0.01 to 100000 | Motor |
| `n_max` | Candidate motor maximum speed | rpm | 6000 | 10 to 100000 | Motor |
| `n_rated` | Candidate motor rated (nameplate) speed, which with the rated torque gives the rated power P_rated = T_rated·ω_rated (optional; blank = not known, so there is no P_rated) (optional) | rpm |  | 10 to 100000 | Motor |
| `SF_c` | Service factor on the rated torque for the design torque (needed for the service-factor basis) (optional) |  |  | 1 to 3 | Motor |
| `T_lim` | Protective torque limit at the motor shaft, e.g. a torque limiter or a drive torque setting (needed for the protective-limit basis) (optional) | N·m |  | 0.01 to 100000 | Motor |

## Assumptions

- The transmission is rigid. Basis: Servo-sizing practice.
- The mechanism efficiency applies to the driving direction and is also used in the deceleration phase. Basis: Servo-sizing practice.
- This is conservative for the peak and slightly pessimistic for the RMS torque. Basis: Servo-sizing practice.
- The friction force is Coulomb friction on the moved mass. Basis: Load model.
- Viscous and breakaway friction are not modelled unless included in F_p. Basis: Load model.
- In continuous duty the load torque is constant at the stated speed. Basis: Run-up idealisation for pumps and fans.
- The motor accelerates the whole inertia linearly to speed in t_start while carrying the full load torque, which is conservative. Basis: Run-up idealisation for pumps and fans.
- Continuous duty ignores the move distance, move time, acceleration fraction, dwell and profile. Basis: Run-up idealisation for pumps and fans.
- The S-curve profile has sine-shaped acceleration over the same acceleration time. Basis: Jerk-limited profile idealisation.
- The peak acceleration of the S-curve profile is π/2 times the trapezoidal value. Basis: Jerk-limited profile idealisation.

## Outputs

| Symbol | Description | Unit |
|---|---|---|
| `k` | Transmission constant | mm |
| `T_load` | Load torque at motor | N·m |
| `J_refl` | Reflected load inertia | kg·m² |
| `J_total` | Total inertia | kg·m² |
| `v_pk` | Peak velocity | m/s |
| `a_pk` | Peak acceleration | m/s² |
| `alpha_pk` | Motor angular acceleration | rad/s² |
| `T_acc` | Acceleration torque | N·m |
| `T_pk` | Peak torque | N·m |
| `T_dec` | Deceleration torque | N·m |
| `T_design` | Design torque for downstream couplings | N·m |
| `T_hold` | Holding torque in the dwell | N·m |
| `T_alt` | Alternating torque amplitude | N·m |
| `F_ax_pk` | Peak axial thrust on a screw's bearings | N |
| `T_rms` | RMS torque | N·m |
| `n_pk` | Peak speed | rpm |
| `P_pk` | Peak power | kW |
| `P_brake` | Peak braking power | kW |
| `P_rms` | RMS power (continuous equivalent) | kW |
| `J_ratio` | Inertia ratio |  |
| `duty` | Thermal duty |  |
| `u_peak` | Peak utilisation |  |
| `u_rms` | Continuous utilisation |  |
| `u_speed` | Speed utilisation |  |
| `n_mean` | Mean motor speed over the cycle | rpm |
| `theta_m` | Motor angle | rad |
| `T_out_m` | Output shaft mean torque | N·m |
| `T_out_a` | Output shaft alternating torque | N·m |
| `T_out_pk` | Output shaft peak torque | N·m |
| `n_out` | Output shaft speed | rpm |
| `P_cont` | Continuous power | kW |
| `P_rated` | Rated (nameplate) power of the candidate motor, T_rated·ω_rated | kW |
| `u_power` | Rated power utilisation P_cont/P_rated |  |
| `u_rated_speed` | Motor speed over the rated speed |  |

## Ports

A port is listed under its public name, `<area>.<quantity>`. The output symbol stays as its first alias, so a saved link, a pack binding or an inputs file that uses the symbol still resolves.

| Public name | Output symbol | Unit | Description |
|---|---|---|---|
| `motor.torque_load` | `T_load` | N*m | Load torque at the motor shaft |
| `motor.inertia_reflected` | `J_refl` | kg*m^2 | Load inertia reflected to the motor shaft |
| `motor.inertia_total` | `J_total` | kg*m^2 | Total inertia at the motor shaft |
| `motor.acceleration_angular_peak` | `alpha_pk` | rad/s^2 | Peak motor angular acceleration |
| `motor.torque_acceleration` | `T_acc` | N*m | Acceleration (or run-up) torque |
| `motor.torque_peak` | `T_pk` | N*m | Peak torque |
| `motor.torque_deceleration` | `T_dec` | N*m | Torque during deceleration (point-to-point) |
| `motor.torque_rms` | `T_rms` | N*m | RMS torque (continuous duty: the load torque) |
| `motor.torque_design` | `T_design` | N*m | Design torque at the motor shaft for a coupling or any part downstream that must carry what the drive can deliver, on the basis chosen in T_basis: the load peak max(\|T_pk\|, \|T_dec\|) (default; T_pk in continuous duty), the motor peak T_peak, SF_c·T_rated or the protective limit T_lim (→ bolt-group M_z) |
| `motor.torque_hold` | `T_hold` | N*m | Holding torque the motor must give during the dwell of a linear axis not held by a brake, m g sin θ·k (0 for a rotary mechanism, a horizontal axis or a held axis) |
| `motor.torque_alternating` | `T_alt` | N*m | Alternating torque amplitude of the cycle about the load torque T_load: (T_pk − T_dec)/2 on a point-to-point cycle, 0 in continuous duty (→ shaft T_a, with T_load → shaft T as the mean). At the motor shaft |
| `motor.force_axial_peak` | `F_ax_pk` | N | Peak axial thrust a lead / ball screw puts on its support bearings, F_load + m·a_pk (→ bearing-life F_a); 0 for every other mechanism (a belt or rack load reaches its bearings as a radial force) |
| `motor.torque_output_mean` | `T_out_m` | N*m | Mean torque on the output shaft behind the gearbox, T_load·i·η (rotary mechanisms; the torque on a shaft after the ratio, → shaft T) |
| `motor.torque_output_alternating` | `T_out_a` | N*m | Alternating torque amplitude on the output shaft, T_alt·i·η (rotary mechanisms; → shaft T_a) |
| `motor.torque_output_peak` | `T_out_pk` | N*m | Peak torque on the output shaft, T_pk·i·η (rotary mechanisms; → shaft T_pk). Conservative: the motor rotor's own inertia torque does not reach the output |
| `motor.speed_output` | `n_out` | rpm | Output shaft speed, n_pk/i (rotary mechanisms) |
| `motor.speed_peak` | `n_pk` | rpm | Peak motor speed |
| `motor.speed_mean` | `n_mean` | rpm | Mean motor speed over the cycle, dwell included (the steady speed in continuous duty; → bearing-life n, the speed a bearing's life in hours is counted at) |
| `motor.power_peak` | `P_pk` | kW | Peak mechanical power |
| `motor.power_brake` | `P_brake` | kW | Peak braking power at the start of the deceleration, max(0, −T_dec·ω_pk): what the drive must absorb or dissipate (point-to-point; 0 in continuous duty) |
| `motor.power_continuous` | `P_cont` | kW | Continuous shaft power at the motor (continuous duty) |
| `motor.power_rated` | `P_rated` | kW | Rated (nameplate) shaft power of the candidate motor, T_rated·ω_rated (needs n_rated; → conductor-sizing P or load-budget P_rated_add to size the supply for the motor, not the load) |
| `motor.power_rms` | `P_rms` | kW | RMS mechanical power over the cycle: the thermal-equivalent continuous power (→ load-budget P_add) |
| `motor.inertia_ratio` | `J_ratio` |  | Inertia ratio (load : motor) |
| `motor.duty_thermal` | `duty` |  | Thermal duty T_rms / T_rated |
| `motor.utilisation_peak` | `u_peak` |  | Peak torque utilisation: SF·max(\|T_pk\|, \|T_dec\|)/T_peak |
| `motor.utilisation_power` | `u_power` |  | Rated power utilisation P_cont/P_rated (continuous duty with n_rated) |
| `motor.utilisation_rms` | `u_rms` |  | Continuous torque utilisation |
| `motor.utilisation_speed` | `u_speed` |  | Speed utilisation |

## Checks

Checks the sheet makes, governing first on the printed sheet:

- Peak torque with the safety factor against the motor peak torque
- RMS torque with the safety factor against the rated torque
- Peak speed against the maximum speed
- Continuous power against the rated power
- Motor speed against the rated speed

Applicability rules:

| Rule | Severity | Effect on the result | Condition |
|---|---|---|---|
| `mechanism-input` | Block | Conservative | The mechanism needs a dimension (screw lead or pulley diameter) or a load value that was not given. |
| `profile` | Warning | Conservative | The acceleration fraction leaves no constant-velocity phase (f_acc = 0.5 is triangular). |
| `inertia-ratio` | Warning | Unconservative | Load inertia exceeds the stated multiple of the motor's: tuning and stability may suffer. |
| `continuous-input` | Block | Conservative | Continuous duty needs a rotary mechanism, the load speed n_L, the run-up time t_start and the load inertia J_L. |
| `continuous-ignored` | Warning | Conservative | In continuous duty the move distance, move time, acceleration fraction, dwell and profile are ignored. |
| `eta` | Warning | Conservative | A direct-coupled rotary load has no transmission: an overall efficiency below 0.95 is probably a leftover default; use 1 or the coupling's efficiency. |
| `rated-speed` | Warning | Unconservative | Continuous duty without the candidate motor's rated speed n_rated: the rated power P_rated is not given. |
| `speed-above-rated` | Warning | Unconservative | Continuous duty: the motor speed n_pk = n_L·i is above the candidate motor's rated (nameplate) speed n_rated; the motor then runs past its nameplate point, where its rated torque and power no longer hold, and the speed check fails. |
| `rated-speed-range` | Warning | Conservative | Continuous duty: the candidate motor's rated speed n_rated is above its maximum speed n_max; a nameplate point the motor cannot reach, so P_rated is not a power it can give. |
| `design-basis` | Block | Conservative | The service-factor basis needs SF_c and the protective-limit basis needs T_lim; chosen without it the sheet is blocked rather than falling back to another basis. |
| `p_screw` | Warning | Conservative | A screw mechanism with the lead left blank: the 10 mm default is used. |
| `return-move` | Warning | Unconservative | An inclined axis or a one-way process force: the cycle holds one move and one dwell, the return stroke is not modelled. |
| `peak-at-speed` | Warning | Unconservative | The peak speed is above 70 % of n_max: T_peak is compared as the stall peak, but a motor's peak torque falls toward its top speed. |
| `overhauling` | Warning | Unconservative | The load drives the motor during deceleration or on the decline (negative load torque); check the drive can regenerate or brake. |

## Limitations

The scope states what the sheet does not cover. No standard text and no licensed table is embedded; each tabulated value is entered by the user.

## References

- Hughes & Drury, Electric Motors and Drives, Ch. on motor and drive selection, 5th ed. (edition to be checked): Load torque, inertia referred to the motor, duty cycles

## Test cases

### Notes on the cases

Ball-screw slide and geared index table, both analytic (Confirmed; P_rms 784.142 W and 101.652 W, the first checked by numerical integration); continuous pump duty through a 4:1 gearbox at 700 rpm with a 2 s run-up (analytic, Confirmed): ω = 293.2153 rad/s, T_load = 0.833333 N·m, T_pk = 1.008448 N·m, T_rms = 0.833333 N·m, P_cont = 244.3461 W, u_peak = 0.210093, u_rms = 0.520833, u_speed = 0.8. Output ports are declared (they were derived before).

- `ka-ball-screw`: Horizontal ball-screw slide, 50 kg, 10 mm lead, 0.5 m in 0.5 s (analytic) (Confirmed; source: Analytic (servo-sizing relations as in Hughes & Drury / manufacturer guides))
- `ka-geared-index-table`: Rotary index table through a 10:1 gearbox, 90° in 0.3 s (analytic) (Confirmed; source: Analytic)
- `ka-continuous-pump`: Continuous rotary duty: a pump at 700 rpm through a 4:1 gearbox, 2 s run-up (analytic) (Confirmed; source: Analytic (steady torque and speed, linear run-up))

## Changes

**1.11.1.** The `T_design` port description names the bolt-group input it feeds by its current symbol `M_z` (the old `T_z` is an alias since C-2 phase B). Text only; no calculated value changes.

**Changes in 1.11.0 (C-2 consistency, phase C).** Layout and wording; no result changes. The checks compare physical values: `T_pk_sf` (the peak torque with the safety factor `SF`) with `T_peak`, `T_rms_sf` with `T_rated`, `n_pk` with `n_max`, and `P_cont` with `P_rated`. `P_cont` and `P_rated` are in kW. The 29 ports are named `motor.*` and their descriptions name templates, not codes. The method is eight lines; the details are in the notes below. The inputs are grouped Setup, Geometry, Loads, Factors, Requirements, Motion and Motor. The messages, assumptions and references follow the sheet standard; the manufacturer guides are no longer a reference.

**Changes in 1.7.0 (elec.motor-sizing-01, -02, -C1, -G1; chain.drive-train-D1).** A vertical or inclined linear axis holds its weight in the dwell: `T_hold = m·g·sin θ·k` (efficiency not credited) enters `T_rms` for the whole dwell, unless the new input `brake` says a brake or a self-locking drive holds the axis (a rotary mechanism has no holding torque). The peak check is `u_peak = SF·max(|T_pk|, |T_dec|)/T_peak`, and `P_brake = max(0, −T_dec·ω_pk)` is reported. Continuous duty with `T_L` blank or zero is refused (rule `continuous-input`). With `n_rated` the sheet checks `P_cont ≤ P_rated` (`u_power`) and warns when `n_rated` is above `n_max` (rule `rated-speed-range`). The design torque a coupling downstream carries is the owner's choice (`T_basis`): the load peak `T_pk` (default, as before), the typed motor peak `T_peak`, `SF_c·T_rated` (needs `SF_c`) or a protective limit `T_lim` (needs `T_lim`); a basis chosen without its input is blocked (rule `design-basis`). The result is the port `T_design`.

**Changes in 1.8.0 (elec.motor-sizing N2).** The default design-torque basis "Load peak (T_pk)" is now `T_design = max(|T_pk|, |T_dec|)` in point-to-point duty (continuous duty has no deceleration and keeps `T_pk`). On an overhauling drive the braking torque is the larger and is what a coupling downstream carries: 200 kg at −20°, `T_pk` 4.019 N·m, `T_dec` −5.740 N·m, `T_design` 4.019 → 5.740 N·m. The choice keeps its stored name so saved sheets still open; where `|T_dec| ≤ T_pk` (an uphill or level move) the value is unchanged.

**Changes in 1.9.0 (elec.motor-sizing-03, -04, -R1, -R2).** With the sine (S-curve) profile `P_pk` and `P_brake` are the maxima of `T(t)·ω(t)` over the acceleration and the deceleration, found by a scan: the torque `T_load + T_acc sin x` peaks at mid-acceleration where the speed is half the top speed, so the product of the two peaks overstated the power (ka-ball-screw with the sine profile: 3771.3 W before, 2457.9 W now; braking 3607.9 W before, 2335.3 W now). The trapezoid keeps `T_pk·ω_pk` (exact). A blank `p_screw` raises the warning `p_screw` (the 10 mm default is used). An inclined axis (`theta` ≠ 0) or a process force (`F_p` ≠ 0) raises `return-move`: the cycle holds one move and one dwell and the return stroke is not modelled. A peak speed above 70 % of `n_max` raises `peak-at-speed`: `T_peak` is compared as the stall peak torque, but a motor's peak torque falls toward its top speed, so check the torque-speed curve at `n_pk`. Chain additions in the same version (chain.drive-train-03, chain.motor-circuit-04): the new output `n_mean`, the mean motor speed over the cycle, `ω_pk·(t_move − t_acc)/t_cycle` for either profile (ka-ball-screw: 3000 rpm against the peak 9000 rpm; the steady speed in continuous duty), is the speed a bearing's life in hours is counted at; and a rated speed `n_rated` on a point-to-point sheet now gives the rated shaft power `P_rated = T_rated·n_rated` (it was ignored, with the notice `rated-speed-ignored`, which is removed), so a supply sized for the motor, not only for the duty, can use it.

**Changes in 1.10.0 (C-2 fix, M1).** `speed-above-rated`: in continuous duty with the candidate motor's rated speed `n_rated` given, the motor speed `n_pk` above `n_rated` now fails the check "Motor speed against the rated speed" (relative tolerance 1·10⁻⁶ so a speed that only rounds above the nameplate does not fail) and shows `u_rated_speed = n_pk/n_rated`; it only warned before. Every other value is unchanged.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Ball-screw axis of a pick-and-place machine ([sheet](../../worked-examples/electrical/elec.motor-sizing/ball-screw-pick-and-place/sheet.txt)).

```text
k = 10 mm / (2 × π × 1) = 1.5915 mm
F_load = 50 kg × 9.80665 m/s^2 × (sin(0 deg) + 0.1 × cos(0 deg)) + 0 N = 49.033 N
T_hold = 50 kg × 9.80665 m/s^2 × sin(0 deg) × 1.5915 mm = 0 N·m
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
… 19 more step(s) on the sheet
Governing: PASS  RMS torque with the safety factor against the rated torque: 1.800 N·m vs 2 N·m  utilisation 0.90
```
<!-- hand-arithmetic:end -->
