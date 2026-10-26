using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Electrical;

/// <summary>
/// E1 — motor / actuator sizing: a linear or rotary load (friction,
/// gravity, process force, inertia) reflected to the motor shaft through
/// a gearbox, lead/ball screw, belt and pulley or rack and pinion; a
/// trapezoidal or S-curve (sine-acceleration) move with dwell; peak and
/// RMS torque, peak speed and power, thermal duty, inertia ratio and a
/// candidate-motor check with a safety margin.
/// </summary>
public sealed class MotorSizingCalculator : ICalcCalculator
{
    public const string Id = "elec.motor-sizing";

    public const string MechDirectRotary = "direct rotary";
    public const string MechGearedRotary = "gearbox rotary";
    public const string MechScrew = "lead / ball screw";
    public const string MechBelt = "belt and pulley";
    public const string MechRack = "rack and pinion";

    public const string DutyPointToPoint = "point-to-point";
    public const string DutyContinuous = "continuous";

    public const string BasisLoadPeak = "Load peak (T_pk)";
    public const string BasisMotorPeak = "Motor peak (T_peak)";
    public const string BasisServiceFactor = "Service factor × rated (SF_c·T_rated)";
    public const string BasisProtectiveLimit = "Protective limit (T_lim)";

    public const string ProfileTrapezoidal = "trapezoidal";
    public const string ProfileSCurve = "S-curve (sine acceleration)";

    public CalcTemplate Template { get; } = new(
        Id, "1.10.0", CalcDiscipline.Electrical, "Motor and actuator sizing",
        "Torque, speed and power a motor needs to move a load through a mechanism on a point-to-point cycle: the load reflected to the motor shaft, the acceleration from the motion profile, peak torque during acceleration, RMS torque over the cycle (thermal duty), peak speed, inertia ratio, and a check of a candidate motor's rated and peak torque and speed with a safety margin. A rotary load that runs continuously (a pump, a fan) is sized instead from its steady torque and speed and a run-up time.",
        "Motor-to-load ratio of a linear mechanism, in metres per motor radian: k = p/(2π i) (screw); D/(2 i) (belt); D_p/(2 i) (rack).\nLoad inertia reflected to the motor through a linear mechanism: J_refl = m k²/η.\nLoad torque at the motor through a linear mechanism: T_load = F k/η, with F = m g (sin θ + μ cos θ) + F_p.\nReflected inertia and torque of a rotary load through a gearbox: J_refl = J_L/(i² η); T_load = T_L/(i η).\nMove profile, trapezoidal 1/3–1/3–1/3 (adjustable): v_pk = s/(t_move − t_acc); a = v_pk/t_acc.\nMove profile, S-curve (sine acceleration, same peak speed): a_pk = (π/2) a_trap.\nTorque to accelerate the total inertia, and the peak torque: T_acc = J_total α; T_pk = T_acc + T_load; deceleration T_dec = −T_acc + T_load; the peak check is on the larger magnitude, SF·max(|T_pk|, |T_dec|) ≤ T_peak, and the braking power is P_brake = max(0, −T_dec·ω_pk).\nDesign torque for a coupling or any part downstream that must carry what the drive can deliver, on the basis the owner chooses (T_basis): the load peak T_design = max(|T_pk|, |T_dec|) (default; T_pk alone in continuous duty, where there is no deceleration), the motor peak T_design = T_peak, a service factor on the rated torque T_design = SF_c·T_rated, or a protective limit T_design = T_lim (a torque limiter or a drive setting).\nHolding torque in the dwell of a linear mechanism that is not held by a brake: T_hold = m g sin θ·k (efficiency not credited: it is a torque to stand still); it is part of the RMS torque for the whole dwell.\nAlternating torque amplitude of a point-to-point cycle about T_load, for a shaft's fatigue: T_alt = (T_pk − T_dec)/2 (0 in continuous duty). Torque behind the ratio on the output shaft of a rotary mechanism: T_out = T·i·η (mean, alternating and peak), n_out = n_pk/i. Peak axial thrust of a ball screw on its support bearings: F_ax_pk = F_load + m a_pk (0 for other mechanisms).\nRMS (thermal) torque over the whole cycle, dwell included: T_rms = √(Σ T_i² t_i / t_cycle), the dwell at T_hold.\nRMS mechanical power over the cycle, the thermal-equivalent continuous power a supply sees (regeneration counts as power; the instantaneous peak P_pk is not a continuous demand), trapezoidal profile: P_rms = ω_pk √{[t_acc·(2/3)(T_load² + T_acc²) + T_load² t_const]/t_cycle}; for the S-curve the acceleration term becomes t_acc·[(3/4) T_load² + (5/16) T_acc²].\nContinuous rotary duty (a pump or fan), speed at the motor: ω = 2π n_L i/60; n_pk = n_L i.\nContinuous rotary duty, load torque and RMS torque: T_load = T_L/(i η); T_rms = T_load.\nPeak torque during the run-up, with full load throughout (conservative): T_pk = J_total ω/t_start + T_load.\nContinuous power: P_cont = T_load ω; P_rms = P_cont; continuous duty needs the steady load torque T_L (blank or zero is refused: it would size the motor for no load).\nRated shaft power of the candidate motor, from its rated torque and its rated speed n_rated (not the running speed), the shaft power its supply must be sized for; without n_rated there is no P_rated and the sheet warns: P_rated = T_rated·2π n_rated/60; check P_cont ≤ P_rated, u_power = P_cont/P_rated.",
        [
            new CalcInputSpec("mechanism", "Mechanism", CalcInputKind.Choice, Choices: [MechScrew, MechBelt, MechRack, MechGearedRotary, MechDirectRotary], DefaultChoice: MechScrew, Group: "Mechanism"),
            new CalcInputSpec("i", "Gear ratio i (motor : output, 1 = none)", CalcInputKind.Number, Default: 1, Min: 1, Max: 1000, HardMin: 0.01, HardMax: 1e5, Group: "Mechanism"),
            new CalcInputSpec("eta", "Mechanism efficiency (overall)", CalcInputKind.Number, Default: 0.9, Min: 0.3, Max: 1, HardMin: 0.01, HardMax: 1, Group: "Mechanism"),
            new CalcInputSpec("p_screw", "Screw lead (per revolution of the screw)", CalcInputKind.Quantity, "mm", Default: 10, Min: 1, Max: 100, HardMin: 1e-3, HardMax: 1e4, IsOptional: true, Group: "Mechanism"),
            new CalcInputSpec("D_pulley", "Pulley or pinion pitch diameter", CalcInputKind.Quantity, "mm", Min: 5, Max: 2000, HardMin: 1e-3, HardMax: 1e5, IsOptional: true, Group: "Mechanism"),
            new CalcInputSpec("J_mech", "Transmission inertia referred to the motor shaft (screw, pulleys, gearbox, coupling)", CalcInputKind.Quantity, "kg*m^2", Default: 1e-4, Min: 0, Max: 10, HardMin: 0, HardMax: 1e4, Group: "Mechanism"),
            new CalcInputSpec("m", "Moved mass (linear mechanisms)", CalcInputKind.Quantity, "kg", Default: 50, Min: 0, Max: 1e5, HardMin: 0, HardMax: 1e7, IsOptional: true, Group: "Load"),
            new CalcInputSpec("mu", "Friction coefficient (guide / bearing)", CalcInputKind.Number, Default: 0.1, Min: 0, Max: 1, HardMin: 0, HardMax: 5, Group: "Load"),
            new CalcInputSpec("theta", "Incline from horizontal (+ moving uphill)", CalcInputKind.Quantity, "deg", Default: 0, Min: -90, Max: 90, HardMin: -90, HardMax: 90, Group: "Load"),
            new CalcInputSpec("F_p", "Process force opposing the move (linear)", CalcInputKind.Quantity, "N", Default: 0, Min: 0, Max: 1e6, HardMin: -1e8, HardMax: 1e8, Group: "Load"),
            new CalcInputSpec("J_L", "Load inertia at the output (rotary mechanisms)", CalcInputKind.Quantity, "kg*m^2", Min: 0, Max: 1e4, HardMin: 0, HardMax: 1e7, IsOptional: true, Group: "Load"),
            new CalcInputSpec("brake", "A brake or a self-locking drive holds a linear axis during the dwell (no holding torque); unticked, a vertical or inclined axis is held by the motor", CalcInputKind.Boolean, Default: 0, Group: "Load"),
            new CalcInputSpec("T_L", "Constant load torque at the output (rotary; continuous duty needs it: blank or zero is refused)", CalcInputKind.Quantity, "N*m", Default: 0, Min: 0, Max: 1e5, HardMin: -1e7, HardMax: 1e7, Group: "Load"),
            new CalcInputSpec("duty_mode", "Duty (point-to-point move cycle, or continuous rotary running with a run-up)", CalcInputKind.Choice, Choices: [DutyPointToPoint, DutyContinuous], DefaultChoice: DutyPointToPoint, Group: "Motion"),
            new CalcInputSpec("n_L", "Load speed at the output (continuous duty)", CalcInputKind.Quantity, "rpm", Min: 1, Max: 1e5, HardMin: 1e-3, HardMax: 1e6, IsOptional: true, Group: "Motion"),
            new CalcInputSpec("t_start", "Run-up time to full speed (continuous duty)", CalcInputKind.Quantity, "s", Min: 0.1, Max: 600, HardMin: 1e-3, HardMax: 1e6, IsOptional: true, Group: "Motion"),
            new CalcInputSpec("profile", "Motion profile", CalcInputKind.Choice, Choices: [ProfileTrapezoidal, ProfileSCurve], DefaultChoice: ProfileTrapezoidal, Group: "Motion"),
            new CalcInputSpec("s", "Move distance (linear: m; rotary: output revolutions)", CalcInputKind.Number, Default: 0.5, Min: 1e-4, Max: 1e4, HardMin: 1e-9, HardMax: 1e7, Group: "Motion"),
            new CalcInputSpec("t_move", "Move time", CalcInputKind.Quantity, "s", Default: 0.5, Min: 0.01, Max: 3600, HardMin: 1e-4, HardMax: 1e6, Group: "Motion"),
            new CalcInputSpec("f_acc", "Acceleration (and deceleration) fraction of the move time", CalcInputKind.Number, Default: 1.0 / 3, Min: 0.05, Max: 0.5, HardMin: 0.01, HardMax: 0.5, Group: "Motion"),
            new CalcInputSpec("t_dwell", "Dwell between moves", CalcInputKind.Quantity, "s", Default: 0.5, Min: 0, Max: 3600, HardMin: 0, HardMax: 1e6, Group: "Motion"),
            new CalcInputSpec("J_motor", "Candidate motor rotor inertia", CalcInputKind.Quantity, "kg*m^2", Default: 2e-4, Min: 1e-7, Max: 10, HardMin: 1e-9, HardMax: 1e4, Group: "Motor"),
            new CalcInputSpec("T_rated", "Candidate motor continuous (rated) torque", CalcInputKind.Quantity, "N*m", Default: 2.0, Min: 0.01, Max: 1e5, HardMin: 1e-4, HardMax: 1e7, Group: "Motor"),
            new CalcInputSpec("T_peak", "Candidate motor peak torque", CalcInputKind.Quantity, "N*m", Default: 6.0, Min: 0.01, Max: 1e5, HardMin: 1e-4, HardMax: 1e7, Group: "Motor"),
            new CalcInputSpec("n_rated", "Candidate motor rated (nameplate) speed, with the rated torque the rated power P_rated = T_rated·ω_rated (leave blank if not known: there is then no P_rated)", CalcInputKind.Quantity, "rpm", Min: 10, Max: 1e5, HardMin: 1, HardMax: 1e6, IsOptional: true, Group: "Motor"),
            new CalcInputSpec("T_basis", "Design torque basis for downstream couplings (T_design): the load's peak (the larger of the accelerating and the braking torque, in magnitude), the motor's typed peak torque, a service factor on the rated torque, or a protective limit such as a torque limiter", CalcInputKind.Choice, Choices: [BasisLoadPeak, BasisMotorPeak, BasisServiceFactor, BasisProtectiveLimit], DefaultChoice: BasisLoadPeak, Group: "Motor"),
            new CalcInputSpec("SF_c", "Service factor on the rated torque for the design torque (needed for the service-factor basis)", CalcInputKind.Number, Min: 1, Max: 10, HardMin: 1, HardMax: 10, IsOptional: true, Group: "Motor"),
            new CalcInputSpec("T_lim", "Protective torque limit at the motor shaft, e.g. a torque limiter or a drive torque setting (needed for the protective-limit basis)", CalcInputKind.Quantity, "N*m", Min: 0.01, Max: 1e5, HardMin: 1e-4, HardMax: 1e7, IsOptional: true, Group: "Motor"),
            new CalcInputSpec("n_max", "Candidate motor maximum speed", CalcInputKind.Quantity, "rpm", Default: 6000, Min: 10, Max: 1e5, HardMin: 1, HardMax: 1e6, Group: "Motor"),
            new CalcInputSpec("SF", "Safety margin on torque", CalcInputKind.Number, Default: 1.25, Min: 1, Max: 3, HardMin: 1, HardMax: 10, Group: "Motor"),
            new CalcInputSpec("J_ratio_max", "Inertia ratio limit (load : motor), warning", CalcInputKind.Number, Default: 10, Min: 1, Max: 100, HardMin: 0.1, HardMax: 1e4, Group: "Motor"),
        ],
        [
            new CalcAssumption("Rigid transmission; the mechanism efficiency applies to the driving direction and is also used in the deceleration phase (conservative for peak, slightly pessimistic for RMS).", "Servo-sizing practice."),
            new CalcAssumption("The friction force is Coulomb friction on the moved mass; viscous and breakaway friction are not modelled unless included in F_p.", "Load model."),
            new CalcAssumption("Continuous duty: the load torque is constant at the stated speed and the motor accelerates the whole inertia linearly to speed in t_start while carrying the full load torque (conservative); the move distance, move time, acceleration fraction, dwell and profile are ignored.", "Run-up idealisation for pumps and fans."),
            new CalcAssumption("The S-curve profile has sine-shaped acceleration over the same acceleration time, so its peak acceleration is π/2 times the trapezoidal value.", "Jerk-limited profile idealisation."),
        ],
        [
            new CalcClauseRef("Hughes & Drury, Electric Motors and Drives", "Ch. on motor and drive selection", "Load torque, inertia referred to the motor, duty cycles"),
            new CalcClauseRef("Manufacturer servo-sizing guides (Kollmorgen, Parker)", "—", "Reflected inertia, RMS torque, inertia ratio practice"),
        ],
        [
            new CalcApplicabilityRule("mechanism-input", "The mechanism needs a dimension (screw lead or pulley diameter) or a load value that was not given.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("profile", "The acceleration fraction leaves no constant-velocity phase (f_acc = 0.5 is triangular).", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("inertia-ratio", "Load inertia exceeds the stated multiple of the motor's: tuning and stability may suffer.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("continuous-input", "Continuous duty needs a rotary mechanism, the load speed n_L, the run-up time t_start and the load inertia J_L.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("continuous-ignored", "In continuous duty the move distance, move time, acceleration fraction, dwell and profile are ignored.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("eta", "A direct-coupled rotary load has no transmission: an overall efficiency below 0.95 is probably a leftover default; use 1 or the coupling's efficiency.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("rated-speed", "Continuous duty without the candidate motor's rated speed n_rated: the rated power P_rated is not given.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
                        new CalcApplicabilityRule("speed-above-rated", "Continuous duty: the motor speed n_pk = n_L·i is above the candidate motor's rated (nameplate) speed n_rated; the motor then runs past its nameplate point, where its rated torque and power no longer hold, and the speed check fails.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("rated-speed-range", "Continuous duty: the candidate motor's rated speed n_rated is above its maximum speed n_max; a nameplate point the motor cannot reach, so P_rated is not a power it can give.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("design-basis", "The service-factor basis needs SF_c and the protective-limit basis needs T_lim; chosen without it the sheet is blocked rather than falling back to another basis.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("p_screw", "A screw mechanism with the lead left blank: the 10 mm default is used.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("return-move", "An inclined axis or a one-way process force: the cycle holds one move and one dwell, the return stroke is not modelled.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("peak-at-speed", "The peak speed is above 70 % of n_max: T_peak is compared as the stall peak, but a motor's peak torque falls toward its top speed.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("overhauling", "The load drives the motor during deceleration or on the decline (negative load torque); check the drive can regenerate or brake.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
        ],
        [
            new CalcTestCase("ka-ball-screw", "Horizontal ball-screw slide, 50 kg, 10 mm lead, 0.5 m in 0.5 s (analytic)",
                new CalcReferenceSource("Analytic (servo-sizing relations as in Hughes & Drury / manufacturer guides)", null, "k = 10 mm/(2π) = 1.5915 mm/rad; v_pk = 0.5/(0.5 − 0.1667) = 1.5 m/s (9000 rpm); a = 9 m/s²; J_refl = 50 k²/0.9 = 1.4072e-4; J_total = 4.4072e-4 kg·m²; α = 5655 rad/s²; T_acc = 2.4923 N·m; T_load = 49.03 N × k / 0.9 = 0.0867 N·m; T_pk = 2.579 N·m; T_rms (with 0.5 s dwell) = 1.440 N·m; ω_pk = 942.478 rad/s, T_dec = −2.40552 N·m; P_rms = ω_pk·√{[(1/6)(2/3)(0.0867098² + 2.492235²) + 0.0867098²/6]/1.0} = 784.142 W"),
                [CalcInputValue.Choice("mechanism", MechScrew), CalcInputValue.Of("i", 1), CalcInputValue.Of("eta", 0.9), CalcInputValue.Quantity("p_screw", 10, "mm"), CalcInputValue.Quantity("J_mech", 1e-4, "kg*m^2"), CalcInputValue.Quantity("m", 50, "kg"), CalcInputValue.Of("mu", 0.1), CalcInputValue.Quantity("theta", 0, "deg"), CalcInputValue.Quantity("F_p", 0, "N"), CalcInputValue.Choice("profile", ProfileTrapezoidal), CalcInputValue.Of("s", 0.5), CalcInputValue.Quantity("t_move", 0.5, "s"), CalcInputValue.Of("f_acc", 1.0 / 3), CalcInputValue.Quantity("t_dwell", 0.5, "s"), CalcInputValue.Quantity("J_motor", 2e-4, "kg*m^2"), CalcInputValue.Quantity("T_rated", 2.0, "N*m"), CalcInputValue.Quantity("T_peak", 6.0, "N*m"), CalcInputValue.Quantity("n_max", 10000, "rpm"), CalcInputValue.Of("SF", 1.25), CalcInputValue.Of("J_ratio_max", 10)],
                [new CalcExpectedValue("v_pk", 1.5, "m/s", 1e-9), new CalcExpectedValue("n_pk", 9000, "rpm", 1e-9), new CalcExpectedValue("J_refl", 1.4072e-4, "kg*m^2", 1e-4), new CalcExpectedValue("T_acc", 2.4923, "N*m", 1e-4), new CalcExpectedValue("T_load", 0.08670, "N*m", 1e-3), new CalcExpectedValue("T_pk", 2.579, "N*m", 1e-3), new CalcExpectedValue("T_rms", 1.4402, "N*m", 1e-3), new CalcExpectedValue("J_ratio", 1.2036, "", 1e-3), new CalcExpectedValue("P_rms", 0.7841424, "kW", 1e-6)]),
            new CalcTestCase("ka-geared-index-table", "Rotary index table through a 10:1 gearbox, 90° in 0.3 s (analytic)",
                new CalcReferenceSource("Analytic", null, "Motor angle 15.708 rad; t_acc = 0.1 s; ω_pk = 78.54 rad/s = 750 rpm; α = 785.4 rad/s²; J_refl = 0.5/(100 × 0.85) = 5.882e-3; J_total = 6.382e-3; T_pk = 5.013 N·m; T_rms over 1.0 s cycle = 2.2417 N·m; inertia ratio 11.8 (warning); P_rms = 78.5398·√{[0.1·(2/3)·5.01269²]/1.0} (no load torque, 0.1 s constant-velocity phase) = 101.652 W"),
                [CalcInputValue.Choice("mechanism", MechGearedRotary), CalcInputValue.Of("i", 10), CalcInputValue.Of("eta", 0.85), CalcInputValue.Quantity("J_mech", 0, "kg*m^2"), CalcInputValue.Quantity("J_L", 0.5, "kg*m^2"), CalcInputValue.Quantity("T_L", 0, "N*m"), CalcInputValue.Choice("profile", ProfileTrapezoidal), CalcInputValue.Of("s", 0.25), CalcInputValue.Quantity("t_move", 0.3, "s"), CalcInputValue.Of("f_acc", 1.0 / 3), CalcInputValue.Quantity("t_dwell", 0.7, "s"), CalcInputValue.Quantity("J_motor", 5e-4, "kg*m^2"), CalcInputValue.Quantity("T_rated", 3.0, "N*m"), CalcInputValue.Quantity("T_peak", 9.0, "N*m"), CalcInputValue.Quantity("n_max", 3000, "rpm"), CalcInputValue.Of("SF", 1.25), CalcInputValue.Of("J_ratio_max", 10)],
                [new CalcExpectedValue("n_pk", 750, "rpm", 1e-6), new CalcExpectedValue("J_refl", 5.882e-3, "kg*m^2", 1e-3), new CalcExpectedValue("T_pk", 5.0126, "N*m", 1e-3), new CalcExpectedValue("T_rms", 2.2417, "N*m", 1e-3), new CalcExpectedValue("J_ratio", 11.76, "", 2e-3), new CalcExpectedValue("P_rms", 0.1016518, "kW", 1e-6)]),
            new CalcTestCase("ka-continuous-pump", "Continuous rotary duty: a pump at 700 rpm through a 4:1 gearbox, 2 s run-up (analytic)",
                new CalcReferenceSource("Analytic (steady torque and speed, linear run-up)", null, "ω = 2π × 700 × 4/60 = 293.2153 rad/s (2800 rpm); T_load = 3/(4 × 0.9) = 0.83333 N·m; J_refl = 0.01/(16 × 0.9) = 6.9444e-4, J_total = 1.19444e-3 kg·m²; α = ω/2 = 146.6077 rad/s²; T_acc = 0.175115, T_pk = 1.008448 N·m; T_rms = T_load; P_cont = 0.83333 × 293.2153 = 244.346 W; P_rated = 2.0 N·m × 2π × 2850/60 = 596.903 W (rated speed 2850 rpm, an invented nameplate); u_peak = 1.25 × 1.008448/6 = 0.210093; u_rms = 1.25 × 0.83333/2 = 0.520833; u_speed = 2800/3500 = 0.8; J_ratio = 6.9444e-4/5e-4 = 1.38889"),
                [CalcInputValue.Choice("mechanism", MechGearedRotary), CalcInputValue.Choice("duty_mode", DutyContinuous), CalcInputValue.Of("i", 4), CalcInputValue.Of("eta", 0.9), CalcInputValue.Quantity("J_mech", 0, "kg*m^2"), CalcInputValue.Quantity("J_L", 0.01, "kg*m^2"), CalcInputValue.Quantity("T_L", 3, "N*m"), CalcInputValue.Quantity("n_L", 700, "rpm"), CalcInputValue.Quantity("t_start", 2, "s"), CalcInputValue.Quantity("J_motor", 5e-4, "kg*m^2"), CalcInputValue.Quantity("T_rated", 2.0, "N*m"), CalcInputValue.Quantity("T_peak", 6.0, "N*m"), CalcInputValue.Quantity("n_rated", 2850, "rpm"), CalcInputValue.Quantity("n_max", 3500, "rpm"), CalcInputValue.Of("SF", 1.25), CalcInputValue.Of("J_ratio_max", 10)],
                [new CalcExpectedValue("n_pk", 2800, "rpm", 1e-9), new CalcExpectedValue("T_load", 0.8333333, "N*m", 1e-6), new CalcExpectedValue("J_refl", 6.944444e-4, "kg*m^2", 1e-6), new CalcExpectedValue("J_total", 1.194444e-3, "kg*m^2", 1e-6), new CalcExpectedValue("T_acc", 0.1751147, "N*m", 1e-6), new CalcExpectedValue("T_pk", 1.008448, "N*m", 1e-6), new CalcExpectedValue("T_rms", 0.8333333, "N*m", 1e-6), new CalcExpectedValue("P_cont", 244.3461, "W", 1e-6), new CalcExpectedValue("P_rated", 596.9026042, "W", 1e-6), new CalcExpectedValue("P_rms", 0.2443461, "kW", 1e-6), new CalcExpectedValue("u_peak", 0.2100933, "", 1e-6), new CalcExpectedValue("u_rms", 0.5208333, "", 1e-6), new CalcExpectedValue("u_speed", 0.8, "", 1e-9), new CalcExpectedValue("J_ratio", 1.388889, "", 1e-6)]),
        ],
        DiagramKey: "motor-sizing",
        Category: "Drives",
        OutputPorts:
        [
            new CalcOutputPort("T_load", "N*m", "Load torque at the motor shaft"),
            new CalcOutputPort("J_refl", "kg*m^2", "Load inertia reflected to the motor shaft"),
            new CalcOutputPort("J_total", "kg*m^2", "Total inertia at the motor shaft"),
            new CalcOutputPort("alpha_pk", "rad/s^2", "Peak motor angular acceleration"),
            new CalcOutputPort("T_acc", "N*m", "Acceleration (or run-up) torque"),
            new CalcOutputPort("T_pk", "N*m", "Peak torque"),
            new CalcOutputPort("T_dec", "N*m", "Torque during deceleration (point-to-point)"),
            new CalcOutputPort("T_rms", "N*m", "RMS torque (continuous duty: the load torque)"),
            new CalcOutputPort("T_design", "N*m", "Design torque at the motor shaft for a coupling or any part downstream that must carry what the drive can deliver, on the basis chosen in T_basis: the load peak max(|T_pk|, |T_dec|) (default; T_pk in continuous duty), the motor peak T_peak, SF_c·T_rated or the protective limit T_lim (→ M3 T_z)"),
            new CalcOutputPort("T_hold", "N*m", "Holding torque the motor must give during the dwell of a linear axis not held by a brake, m g sin θ·k (0 for a rotary mechanism, a horizontal axis or a held axis)"),
            new CalcOutputPort("T_alt", "N*m", "Alternating torque amplitude of the cycle about the load torque T_load: (T_pk − T_dec)/2 on a point-to-point cycle, 0 in continuous duty (→ M4 T_a, with T_load → M4 T as the mean). At the motor shaft"),
            new CalcOutputPort("F_ax_pk", "N", "Peak axial thrust a lead / ball screw puts on its support bearings, F_load + m·a_pk (→ M5 F_a); 0 for every other mechanism (a belt or rack load reaches its bearings as a radial force)"),
            new CalcOutputPort("T_out_m", "N*m", "Mean torque on the output shaft behind the gearbox, T_load·i·η (rotary mechanisms; the torque on a shaft after the ratio, → M4 T)"),
            new CalcOutputPort("T_out_a", "N*m", "Alternating torque amplitude on the output shaft, T_alt·i·η (rotary mechanisms; → M4 T_a)"),
            new CalcOutputPort("T_out_pk", "N*m", "Peak torque on the output shaft, T_pk·i·η (rotary mechanisms; → M4 T_pk). Conservative: the motor rotor's own inertia torque does not reach the output"),
            new CalcOutputPort("n_out", "rpm", "Output shaft speed, n_pk/i (rotary mechanisms)"),
            new CalcOutputPort("n_pk", "rpm", "Peak motor speed"),
            new CalcOutputPort("n_mean", "rpm", "Mean motor speed over the cycle, dwell included (the steady speed in continuous duty; → M5 n, the speed a bearing's life in hours is counted at)"),
            new CalcOutputPort("P_pk", "kW", "Peak mechanical power"),
            new CalcOutputPort("P_brake", "kW", "Peak braking power at the start of the deceleration, max(0, −T_dec·ω_pk): what the drive must absorb or dissipate (point-to-point; 0 in continuous duty)"),
            new CalcOutputPort("P_cont", "W", "Continuous shaft power at the motor (continuous duty)"),
            new CalcOutputPort("P_rated", "W", "Rated (nameplate) shaft power of the candidate motor, T_rated·ω_rated (needs n_rated; → E4 P or E6 P_rated_add to size the supply for the motor, not the load)"),
            new CalcOutputPort("P_rms", "kW", "RMS mechanical power over the cycle: the thermal-equivalent continuous power (→ E6 P_add)"),
            new CalcOutputPort("J_ratio", "", "Inertia ratio (load : motor)"),
            new CalcOutputPort("duty", "", "Thermal duty T_rms / T_rated"),
            new CalcOutputPort("u_peak", "", "Peak torque utilisation: SF·max(|T_pk|, |T_dec|)/T_peak"),
            new CalcOutputPort("u_power", "", "Rated power utilisation P_cont/P_rated (continuous duty with n_rated)"),
            new CalcOutputPort("u_rms", "", "Continuous torque utilisation"),
            new CalcOutputPort("u_speed", "", "Speed utilisation"),
        ],
        RecordKinds:
        [
            new CalcRecordKind("motor.rating", "Candidate motor rating", ["J_motor", "T_rated", "T_peak", "n_rated", "n_max"], "The candidate motor's rotor inertia, rated and peak torque, rated (nameplate) speed and maximum speed from its datasheet or nameplate, with the source and edition. The rated power P_rated is the rated torque times the rated speed."),
        ]);

    public void Run(CalcRunContext c)
    {
        c.MarkUtilisation("u_peak", "u_power", "u_rms", "u_speed");
        var mech = c.InputText("mechanism");
        var continuous = c.InputText("duty_mode") == DutyContinuous;

        foreach (var s in new[] { "i", "eta", "J_mech", "mu", "theta", "F_p", "T_L", "s", "t_move", "f_acc", "t_dwell", "J_motor", "T_rated", "T_peak", "n_max", "SF", "J_ratio_max" })
        {
            if (continuous && s is "s" or "t_move" or "f_acc" or "t_dwell")
                continue;

            c.Input(s);
        }

        if (mech == MechDirectRotary && c.Si("eta") < 0.95)
            c.Warn("eta", $"eta: a direct-coupled rotary load has no transmission, but the overall efficiency is {CalcValue.FormatNumber(c.Si("eta"), 3)}; use 1 (or the coupling's efficiency) unless the loss is intended.");

        if (continuous)
        {
            RunContinuous(c, mech);
            return;
        }

        var linear = mech is MechScrew or MechBelt or MechRack;

        if (linear)
        {
            if (!c.Has("m")) { c.Block("mechanism-input", "m: a linear mechanism needs the moved mass."); return; }
            c.Input("m");

            switch (mech)
            {
                case MechScrew:
                    if (!c.Has("p_screw") && c.Template.FindInput("p_screw")!.Default is null) { c.Block("mechanism-input", "p_screw: the screw lead is needed."); return; }
                    c.Input("p_screw");

                    // elec.motor-sizing-04: a blank lead is replaced by the 10 mm default; say so.
                    if (!c.Has("p_screw"))
                        c.Warn("p_screw", "p_screw: the screw lead is blank, so the default 10 mm is used; enter the lead of the screw.");

                    c.Assign("k", "p_screw / (2 * pi * i)", "mm", "Travel per motor radian");
                    break;
                default:
                    if (!c.Has("D_pulley")) { c.Block("mechanism-input", "D_pulley: the pulley or pinion pitch diameter is needed."); return; }
                    c.Input("D_pulley");
                    c.Assign("k", "D_pulley / (2 * i)", "mm", "Travel per motor radian");
                    break;
            }

            // elec.motor-sizing-R1: the cycle holds one move and one dwell. With gravity along the axis or a one-way process force the return stroke has other torques.
            if (Math.Abs(Math.Sin(c.Si("theta"))) > 1e-9 || Math.Abs(c.Si("F_p")) > 1e-9)
                c.Warn("return-move", "theta: the cycle holds one move and one dwell, and the return stroke is not modelled; with an inclined axis or a one-way process force the return move carries other torques (a vertical axis up is about the reverse of down), so check the return stroke and the thermal cycle with it.");

            c.Assign("F_load", "m * 9.80665 m/s^2 * (sin(theta) + mu * cos(theta)) + F_p", "N", "Load force along the motion (gravity, friction, process)");
            c.Assign("T_hold", c.InputFlag("brake") ? "0 N*m" : "m * 9.80665 m/s^2 * sin(theta) * k", "N*m", c.InputFlag("brake") ? "Holding torque in the dwell: none, a brake or a self-locking drive holds the axis" : "Holding torque in the dwell: the motor holds the weight of the axis (efficiency not credited)");
            c.Assign("T_load", "F_load * k / eta", "N*m", "Load torque at the motor shaft");
            c.Assign("J_refl", "m * k^2 / eta", "kg*m^2", "Load inertia reflected to the motor shaft");
            c.Assign("v_pk", "s * 1 m / (t_move * (1 - f_acc))", "m/s", "Peak linear velocity (trapezoid)");
            c.Assign("a_trap", "v_pk / (f_acc * t_move)", "m/s^2", "Trapezoidal acceleration");
            c.Assign("omega_pk", "v_pk / k", "rad/s", "Peak motor speed");
        }
        else
        {
            if (!c.Has("J_L")) { c.Block("mechanism-input", "J_L: a rotary mechanism needs the load inertia."); return; }
            c.Input("J_L");
            c.Assign("k", "1 / i", null, "Output radians per motor radian");
            c.Assign("T_hold", "0 N*m", "N*m", "Holding torque in the dwell: none for a rotary mechanism (a constant load torque that holds against gravity is not distinguished from friction)");
            c.Assign("T_load", "T_L / (i * eta)", "N*m", "Load torque at the motor shaft");
            c.Assign("J_refl", "J_L / (i^2 * eta)", "kg*m^2", "Load inertia reflected to the motor shaft");
            c.Assign("theta_m", "s * 2 * pi * i", "rad", "Motor angle for the move");
            c.Assign("omega_pk", "theta_m / (t_move * (1 - f_acc))", "rad/s", "Peak motor speed (trapezoid)");
            c.Assign("a_trap", "omega_pk / (f_acc * t_move)", "rad/s^2", "Trapezoidal angular acceleration at the motor");
        }

        if (c.Si("f_acc") >= 0.5 - 1e-9)
            c.Warn("profile", "f_acc: no constant-velocity phase; the profile is triangular.");

        var sCurve = c.InputText("profile") == ProfileSCurve;
        c.Assign("a_pk", sCurve ? "pi / 2 * a_trap" : "a_trap", linear ? "m/s^2" : "rad/s^2", sCurve ? "Peak acceleration (sine profile)" : "Peak acceleration");
        c.Assign("alpha_pk", linear ? "a_pk / k" : "a_pk", "rad/s^2", "Peak motor angular acceleration");
        c.Assign("J_total", "J_motor + J_mech + J_refl", "kg*m^2", "Total inertia at the motor shaft");
        c.Assign("T_acc", "J_total * alpha_pk", "N*m", "Acceleration torque");
        c.Assign("T_pk", "T_acc + T_load", "N*m", "Peak torque (accelerating against the load)");
        c.Assign("T_dec", "-T_acc + T_load", "N*m", "Torque during deceleration");
        c.Assign("T_alt", "(T_pk - T_dec) / 2", "N*m", "Alternating torque amplitude of the cycle about T_load");

        if (!AssignDesignTorque(c, true))
            return;

        if (mech == MechScrew)
            c.Assign("F_ax_pk", "F_load + m * a_pk", "N", "Peak axial thrust on the screw's support bearings (load force plus the moved mass accelerated)");
        else
            c.Assign("F_ax_pk", "0 N", "N", "Peak axial thrust on the bearings (none for this mechanism)");
        c.Assign("n_pk", "omega_pk", "rpm", "Peak motor speed");
        AssignOutputSide(c, linear);

        // The trapezoid reaches T_pk at the top speed, so T_pk·ω_pk is exact; the sine profile has its peak torque at mid-acceleration, where the speed is half the top, so the peak power is
        // the maximum of T(t)·ω(t) over the phase (elec.motor-sizing-03).
        if (sCurve)
        {
            var (pPk, pBrake) = SineProfilePeakPowers(c.Si("T_load"), c.Si("T_acc"), c.Si("omega_pk"));
            var kWUnit = UnitCatalog.Parse("kW");
            c.Record("P_pk", "max over the acceleration of T(t)·ω(t), T = T_load + T_acc·sin x, ω = ω_pk·(1 − cos x)/2, x = 0 … π", $"scan of x over 0 … π: {CalcValue.FormatNumber(pPk, 5)} W", ScalarValue.Of(pPk / 1e3, kWUnit), "Peak mechanical power");
            c.Record("P_brake", "max over the deceleration of (T_acc·sin y − T_load)·ω_pk·(1 + cos y)/2, y = 0 … π", $"scan of y over 0 … π: {CalcValue.FormatNumber(pBrake, 5)} W", ScalarValue.Of(pBrake / 1e3, kWUnit), "Peak braking power");
        }
        else
        {
            c.Assign("P_pk", "T_pk * omega_pk", "kW", "Peak mechanical power");
            c.Assign("P_brake", "max(0 N*m, -T_dec) * omega_pk", "kW", "Peak braking power at the start of the deceleration");
        }

        c.Assign("t_acc", "f_acc * t_move", "s", "Acceleration time");
        c.Assign("t_const", "t_move - 2 * t_acc", "s", "Constant-velocity time");
        c.Assign("t_cycle", "t_move + t_dwell", "s", "Cycle time");

        // The mean motor speed over the cycle (chain.drive-train-03): the travel is ω_pk·(t_move − t_acc) for either profile, turned once per cycle including the dwell. A bearing's life in hours needs this, not the peak speed.
        c.Assign("n_mean", "omega_pk * (t_move - t_acc) / t_cycle", "rpm", "Mean motor speed over the cycle, dwell included (the travel's motor turns per cycle time)");

        // The candidate motor's rated shaft power, T_rated·ω_rated, when the nameplate speed is given (chain.motor-circuit-04): a supply or cable sized for the motor, not only for the duty, uses it.
        var hasRatedPointToPoint = c.Has("n_rated");

        if (hasRatedPointToPoint)
        {
            c.Input("n_rated");
            c.Assign("P_rated", "T_rated * n_rated", "W", "Rated (nameplate) shaft power of the candidate motor: its rated torque times its rated speed");

            if (c.Si("n_rated") > c.Si("n_max") * (1 + 1e-6))
                c.Warn("rated-speed-range", $"n_rated: the rated speed {CalcValue.FormatNumber(c.Si("n_rated") * 60 / (2 * Math.PI), 4)} rpm is above the candidate motor's maximum speed {CalcValue.FormatNumber(c.Si("n_max") * 60 / (2 * Math.PI), 4)} rpm, a nameplate point the motor cannot reach, so P_rated is not a power it can give.");
        }

        // RMS power: P = T·ω with ω rising linearly (trapezoid) or as (1 − cos)/2 (sine acceleration) through the acceleration phase and
        // mirrored through the deceleration; the closed forms follow from integrating (T·ω)² over the phases (Green G-B-11).
        c.Assign("P_rms", sCurve
            ? "omega_pk * sqrt((t_acc * (3 / 4 * T_load^2 + 5 / 16 * T_acc^2) + T_load^2 * t_const) / t_cycle)"
            : "omega_pk * sqrt((t_acc * (2 / 3) * (T_load^2 + T_acc^2) + T_load^2 * t_const) / t_cycle)", "kW", "RMS mechanical power over the cycle (thermal-equivalent continuous power)");

        // RMS: a sine acceleration has RMS a_pk/√2 over the acceleration time; the torque in that phase is J α(t) + T_load.
        c.Assign("T_rms", sCurve
            ? "sqrt(((T_acc^2 / 2 + T_load^2) * 2 * t_acc + T_load^2 * t_const + T_hold^2 * t_dwell) / t_cycle)"
            : "sqrt((T_pk^2 * t_acc + T_load^2 * t_const + T_dec^2 * t_acc + T_hold^2 * t_dwell) / t_cycle)", "N*m", "RMS torque over the cycle (thermal), the dwell at the holding torque");
        c.Assign("J_ratio", "(J_refl + J_mech) / J_motor", null, "Inertia ratio (load : motor)");
        c.Assign("u_peak", "SF * max(abs(T_pk), abs(T_dec)) / T_peak", null, "Peak torque utilisation with safety margin, on the larger of the accelerating and the braking torque");
        c.Assign("u_rms", "SF * T_rms / T_rated", null, "Continuous torque utilisation with safety margin");
        c.Assign("u_speed", "n_pk / n_max", null, "Speed utilisation");
        c.Assign("duty", "T_rms / T_rated", null, "Thermal duty (without margin)");

        // elec.motor-sizing-R2: the peak torque of a motor falls toward its top speed, but T_peak is one number (the stall peak); near the speed limit the comparison is optimistic.
        if (c.Si("u_speed") > 0.7)
            c.Warn("peak-at-speed", $"n_pk: the peak speed is {CalcValue.FormatNumber(c.Si("u_speed") * 100, 3)} % of n_max and T_peak is compared as the stall peak torque; a servo's peak torque falls toward its top speed, so check the motor's torque-speed curve at n_pk for T_pk.");

        if (c.Si("J_ratio") > c.Si("J_ratio_max"))
            c.Warn("inertia-ratio", $"J_motor: the inertia ratio {CalcValue.FormatNumber(c.Si("J_ratio"), 3)} exceeds {CalcValue.FormatNumber(c.Si("J_ratio_max"), 3)}.");

        if (c.Si("T_load") < 0 || c.Si("T_dec") < 0)
            c.Warn("overhauling", "T_dec: the motor brakes during deceleration (negative torque); the drive must absorb the regenerated energy.");

        foreach (var (symbol, text) in new[] { ("k", "Transmission constant"), ("T_load", "Load torque at motor"), ("J_refl", "Reflected load inertia"), ("J_total", "Total inertia"), (linear ? "v_pk" : "theta_m", linear ? "Peak velocity" : "Motor angle"), ("a_pk", "Peak acceleration"), ("alpha_pk", "Motor angular acceleration"), ("T_acc", "Acceleration torque"), ("T_pk", "Peak torque"), ("T_dec", "Deceleration torque"), ("T_design", "Design torque for downstream couplings"), ("T_hold", "Holding torque in the dwell"), ("T_alt", "Alternating torque amplitude"), ("F_ax_pk", "Peak axial thrust on a screw's bearings"), ("T_rms", "RMS torque"), ("n_pk", "Peak speed"), ("P_pk", "Peak power"), ("P_brake", "Peak braking power"), ("P_rms", "RMS power (continuous equivalent)"), ("J_ratio", "Inertia ratio"), ("duty", "Thermal duty"), ("u_peak", "Peak utilisation"), ("u_rms", "Continuous utilisation"), ("u_speed", "Speed utilisation") })
            c.Output(symbol, text);

        if (!linear)
            OutputOutputSide(c);

        c.Output("n_mean", "Mean motor speed over the cycle");

        if (hasRatedPointToPoint)
            c.Output("P_rated", "Rated (nameplate) power of the candidate motor, T_rated·ω_rated");

        c.CheckUtilisation("Peak torque SF·max(|T_pk|, |T_dec|) ≤ T_peak", "u_peak");
        c.CheckUtilisation("RMS torque SF·T_rms ≤ T_rated", "u_rms");
        c.CheckUtilisation("Speed n_pk ≤ n_max", "u_speed");
    }

    /// <summary>
    /// The peak mechanical and braking power of the sine-acceleration profile: with a(t) ∝ sin(πt/t_acc) the speed is ω_pk(1 − cos x)/2 and the torque T_load + T_acc·sin x (x = πt/t_acc) on the
    /// way up, ω_pk(1 + cos y)/2 and T_load − T_acc·sin y on the way down. The maximum of each product is found by a fine scan and a golden-section refinement.
    /// </summary>
    internal static (double Peak, double Brake) SineProfilePeakPowers(double tLoad, double tAcc, double omegaPk)
    {
        double Up(double x) => omegaPk * (1 - Math.Cos(x)) / 2 * (tLoad + tAcc * Math.Sin(x));
        double Down(double y) => omegaPk * (1 + Math.Cos(y)) / 2 * (tAcc * Math.Sin(y) - tLoad);

        return (Math.Max(0, Maximise(Up)), Math.Max(0, Maximise(Down)));
    }

    private static double Maximise(Func<double, double> f)
    {
        const int steps = 4000;
        var best = double.NegativeInfinity;
        var at = 0.0;

        for (var i = 0; i <= steps; i++)
        {
            var x = Math.PI * i / steps;
            var v = f(x);

            if (v > best)
            {
                best = v;
                at = x;
            }
        }

        // Golden-section refinement inside the bracket around the best sample.
        var lo = Math.Max(0, at - Math.PI / steps);
        var hi = Math.Min(Math.PI, at + Math.PI / steps);
        var g = (Math.Sqrt(5) - 1) / 2;

        for (var k = 0; k < 60; k++)
        {
            var a = hi - g * (hi - lo);
            var b = lo + g * (hi - lo);

            if (f(a) > f(b))
                hi = b;
            else
                lo = a;
        }

        return Math.Max(best, f((lo + hi) / 2));
    }

    /// <summary>
    /// The design torque on the basis the owner chose (chain.drive-train-D1, elec.motor-sizing-N2): the load peak max(|T_pk|, |T_dec|) (T_pk in continuous duty), the typed motor peak T_peak, SF_c·T_rated or the protective
    /// limit T_lim. A basis chosen without its input is blocked, never replaced by another. Returns false when blocked.
    /// </summary>
    private static bool AssignDesignTorque(CalcRunContext c, bool pointToPoint)
    {
        var basis = c.InputText("T_basis");

        switch (basis)
        {
            case BasisMotorPeak:
                c.Assign("T_design", "T_peak", "N*m", "Design torque: the motor's peak torque (the torque the drive can deliver)");
                break;

            case BasisServiceFactor:
                if (!c.Has("SF_c")) { c.Block("design-basis", "SF_c: the service-factor basis needs the service factor on the rated torque; enter it or choose another basis."); return false; }
                c.Input("SF_c");
                c.Assign("T_design", "SF_c * T_rated", "N*m", "Design torque: the service factor times the rated torque");
                break;

            case BasisProtectiveLimit:
                if (!c.Has("T_lim")) { c.Block("design-basis", "T_lim: the protective-limit basis needs the torque limit (a torque limiter or a drive setting); enter it or choose another basis."); return false; }
                c.Input("T_lim");
                c.Assign("T_design", "T_lim", "N*m", "Design torque: the protective torque limit");
                break;

            default:
                if (pointToPoint)
                    c.Assign("T_design", "max(abs(T_pk), abs(T_dec))", "N*m", "Design torque: the load's peak torque, the larger in magnitude of the accelerating T_pk and the braking T_dec (an overhauling drive brakes harder than it accelerates)");
                else
                    c.Assign("T_design", "T_pk", "N*m", "Design torque: the load's peak torque (run-up included; continuous duty has no deceleration)");
                break;
        }

        return true;
    }

    /// <summary>The torques and speed on the output shaft behind the ratio (rotary mechanisms only): the motor-shaft torque times i·η, and the speed over i.</summary>
    private static void AssignOutputSide(CalcRunContext c, bool linear)
    {
        if (linear)
            return;

        c.Assign("T_out_m", "T_load * i * eta", "N*m", "Mean torque on the output shaft (the constant load torque)");
        c.Assign("T_out_a", "T_alt * i * eta", "N*m", "Alternating torque amplitude on the output shaft");
        c.Assign("T_out_pk", "T_pk * i * eta", "N*m", "Peak torque on the output shaft (the motor rotor's inertia torque is counted, so conservative)");
        c.Assign("n_out", "n_pk / i", "rpm", "Output shaft speed");
    }

    private static void OutputOutputSide(CalcRunContext c)
    {
        foreach (var (symbol, text) in new[] { ("T_out_m", "Output shaft mean torque"), ("T_out_a", "Output shaft alternating torque"), ("T_out_pk", "Output shaft peak torque"), ("n_out", "Output shaft speed") })
            c.Output(symbol, text);
    }

    /// <summary>Continuous rotary duty: constant torque at a steady speed, with a linear run-up carrying the full load.</summary>
    private static void RunContinuous(CalcRunContext c, string mech)
    {
        if (mech is MechScrew or MechBelt or MechRack)
        {
            c.Block("continuous-input", "duty_mode: continuous duty is for a rotary mechanism (direct or gearbox rotary); a linear mechanism has no steady speed.");
            return;
        }

        if (!c.Has("n_L")) { c.Block("continuous-input", "n_L: continuous duty needs the load speed."); return; }
        if (!c.Has("t_start")) { c.Block("continuous-input", "t_start: continuous duty needs the run-up time."); return; }
        if (!c.Has("J_L")) { c.Block("continuous-input", "J_L: a rotary mechanism needs the load inertia."); return; }

        // A continuous load is sized from its steady torque: a blank T_L, or a zero one, would size the motor for no load and pass (elec.motor-sizing-C1).
        if (!c.Has("T_L") || c.InputSi("T_L") == 0)
        {
            c.Block("continuous-input", "T_L: continuous duty needs the steady load torque at the output (a pump or fan torque at its speed); blank or zero sizes the motor for no load.");
            return;
        }

        c.Input("n_L");
        c.Input("t_start");
        c.Input("J_L");
        c.Warn("continuous-ignored", "duty_mode: continuous duty ignores the move distance, move time, acceleration fraction, dwell and profile.");

        c.Assign("k", "1 / i", null, "Output radians per motor radian");
        c.Assign("T_load", "T_L / (i * eta)", "N*m", "Load torque at the motor shaft (constant)");
        c.Assign("J_refl", "J_L / (i^2 * eta)", "kg*m^2", "Load inertia reflected to the motor shaft");
        c.Assign("omega_pk", "n_L * i", "rad/s", "Motor speed, n_L · i");
        c.Assign("J_total", "J_motor + J_mech + J_refl", "kg*m^2", "Total inertia at the motor shaft");
        c.Assign("alpha_pk", "omega_pk / t_start", "rad/s^2", "Run-up angular acceleration (to speed in t_start)");
        c.Assign("T_acc", "J_total * alpha_pk", "N*m", "Run-up (acceleration) torque");
        c.Assign("T_pk", "T_acc + T_load", "N*m", "Peak torque: run-up carrying the full load (conservative)");
        c.Assign("T_rms", "T_load", "N*m", "RMS torque: continuous running at the load torque");
        c.Assign("T_alt", "0 N*m", "N*m", "Alternating torque amplitude: none, continuous running is steady (the run-up is a rare peak, T_pk)");

        if (!AssignDesignTorque(c, false))
            return;

        c.Assign("F_ax_pk", "0 N", "N", "Peak axial thrust on the bearings (none for a rotary mechanism)");
        c.Assign("T_hold", "0 N*m", "N*m", "Holding torque in the dwell: none, continuous running has no dwell");
        c.Assign("P_brake", "0 kW", "kW", "Peak braking power: none in continuous duty");
        c.Assign("n_pk", "omega_pk", "rpm", "Motor speed");
        AssignOutputSide(c, false);
        c.Assign("P_cont", "T_load * omega_pk", "W", "Continuous shaft power at the motor");

        // The nameplate rated power needs the rated speed; the running speed is not a substitute (a 4-pole motor runs near, not at, its rated speed).
        var hasRated = c.Has("n_rated");

        if (hasRated)
        {
            c.Input("n_rated");
            c.Assign("P_rated", "T_rated * n_rated", "W", "Rated (nameplate) shaft power of the candidate motor: its rated torque times its rated speed");
            c.Assign("u_power", "P_cont / P_rated", null, "Continuous shaft power against the rated power of the candidate motor");

            if (c.Si("n_rated") > c.Si("n_max") * (1 + 1e-6))
                c.Warn("rated-speed-range", $"n_rated: the rated speed {CalcValue.FormatNumber(c.Si("n_rated") * 60 / (2 * Math.PI), 4)} rpm is above the candidate motor's maximum speed {CalcValue.FormatNumber(c.Si("n_max") * 60 / (2 * Math.PI), 4)} rpm, a nameplate point the motor cannot reach; P_rated = T_rated·ω_rated is then not a power it can give. Check the nameplate speed.");

            // A relative tolerance of 1e-6 keeps a speed that only rounds above the nameplate (n_L * i against n_rated) from warning.
            if (c.Si("n_pk") > c.Si("n_rated") * (1 + 1e-6))
                c.Warn("speed-above-rated", $"n_L: the motor speed n_pk = {CalcValue.FormatNumber(c.Si("n_pk") * 60 / (2 * Math.PI), 4)} rpm is above the candidate motor's rated speed {CalcValue.FormatNumber(c.Si("n_rated") * 60 / (2 * Math.PI), 4)} rpm; its rated torque and power hold at the nameplate speed, so check the motor's torque and power at this speed (field weakening or a different ratio i).");
        }
        else
        {
            c.Warn("rated-speed", "n_rated: the candidate motor's rated speed is not entered, so its rated power P_rated (T_rated·ω_rated, which a supply or cable for the motor is sized on) is not given; enter the nameplate speed.");
        }

        c.Assign("P_pk", "T_pk * omega_pk", "kW", "Peak mechanical power (end of the run-up)");
        c.Assign("n_mean", "omega_pk", "rpm", "Mean motor speed: the steady running speed");
        c.Assign("P_rms", "P_cont", "kW", "RMS mechanical power: the steady running power (thermal-equivalent continuous power)");
        c.Assign("J_ratio", "(J_refl + J_mech) / J_motor", null, "Inertia ratio (load : motor)");
        c.Assign("u_peak", "SF * T_pk / T_peak", null, "Peak torque utilisation with safety margin");
        c.Assign("u_rms", "SF * T_rms / T_rated", null, "Continuous torque utilisation with safety margin");
        c.Assign("u_speed", "n_pk / n_max", null, "Speed utilisation");

        // The motor speed above the nameplate (rated) speed is a fail, not only a warning: the rated torque and power hold at the nameplate speed.
        if (hasRated)
            c.Assign("u_rated_speed", "n_pk / n_rated", null, "Motor speed over the rated (nameplate) speed");
        c.Assign("duty", "T_rms / T_rated", null, "Thermal duty (without margin)");

        if (c.Si("J_ratio") > c.Si("J_ratio_max"))
            c.Warn("inertia-ratio", $"J_motor: the inertia ratio {CalcValue.FormatNumber(c.Si("J_ratio"), 3)} exceeds {CalcValue.FormatNumber(c.Si("J_ratio_max"), 3)}.");

        if (c.Si("T_load") < 0)
            c.Warn("overhauling", "T_L: the load drives the motor (negative load torque); check the drive can regenerate or brake.");

        foreach (var (symbol, text) in new[] { ("k", "Transmission constant"), ("T_load", "Load torque at motor"), ("J_refl", "Reflected load inertia"), ("J_total", "Total inertia"), ("alpha_pk", "Run-up angular acceleration"), ("T_acc", "Run-up torque"), ("T_pk", "Peak torque"), ("T_design", "Design torque for downstream couplings"), ("T_hold", "Holding torque in the dwell"), ("T_alt", "Alternating torque amplitude"), ("F_ax_pk", "Peak axial thrust on a screw's bearings"), ("T_rms", "RMS torque"), ("n_pk", "Motor speed"), ("P_cont", "Continuous power"), ("P_brake", "Peak braking power"), ("P_pk", "Peak power"), ("P_rms", "RMS power (continuous equivalent)"), ("J_ratio", "Inertia ratio"), ("duty", "Thermal duty"), ("u_peak", "Peak utilisation"), ("u_rms", "Continuous utilisation"), ("u_speed", "Speed utilisation") })
            c.Output(symbol, text);

        c.CheckUtilisation("Peak torque SF·T_pk ≤ T_peak", "u_peak");
        c.CheckUtilisation("RMS torque SF·T_rms ≤ T_rated", "u_rms");
        c.CheckUtilisation("Speed n_pk ≤ n_max", "u_speed");

        if (hasRated)
        {
            c.CheckUtilisation("Rated power P_cont ≤ P_rated", "u_power");

            // A relative tolerance of 1e-6 keeps a speed that only rounds above the nameplate (n_L * i against n_rated) from failing.
            c.Check("Motor speed against the rated speed", "n_pk", "n_rated", tolerance: 1e-6);
        }

        OutputOutputSide(c);
        c.Output("n_mean", "Mean motor speed (the steady speed)");

        if (hasRated)
        {
            c.Output("P_rated", "Rated (nameplate) power of the candidate motor, T_rated·ω_rated");
            c.Output("u_power", "Rated power utilisation P_cont/P_rated");
            c.Output("u_rated_speed", "Motor speed over the rated speed");
        }
    }
}
