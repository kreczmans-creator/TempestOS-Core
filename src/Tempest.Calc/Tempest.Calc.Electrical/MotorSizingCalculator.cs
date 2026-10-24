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

    public const string ProfileTrapezoidal = "trapezoidal";
    public const string ProfileSCurve = "S-curve (sine acceleration)";

    public CalcTemplate Template { get; } = new(
        Id, "1.2.0", CalcDiscipline.Electrical, "Motor and actuator sizing",
        "Torque, speed and power a motor needs to move a load through a mechanism on a point-to-point cycle: the load reflected to the motor shaft, the acceleration from the motion profile, peak torque during acceleration, RMS torque over the cycle (thermal duty), peak speed, inertia ratio, and a check of a candidate motor's rated and peak torque and speed with a safety margin. A rotary load that runs continuously (a pump, a fan) is sized instead from its steady torque and speed and a run-up time.",
        "Linear mechanisms: k = metres per motor radian (screw p/(2π i); belt D/(2 i); rack D_p/(2 i)); J_refl = m k²/η; T_load = F k/η with F = m g (sin θ + μ cos θ) + F_p. Rotary: J_refl = J_L/(i² η), T_load = T_L/(i η). Trapezoidal 1/3–1/3–1/3 (adjustable): v_pk = s/(t_move − t_acc), a = v_pk/t_acc; S-curve: sine acceleration with a_pk = (π/2) a_trap and the same v_pk. T_acc = J_total α; T_pk = T_acc + T_load; T_rms = √(Σ T_i² t_i / t_cycle) including the dwell. RMS mechanical power (the thermal-equivalent continuous power a supply sees; regeneration counts as power): P_rms = ω_pk √{[t_acc·(2/3)(T_load² + T_acc²) + T_load² t_const]/t_cycle} (trapezoid), with t_acc·[(3/4) T_load² + (5/16) T_acc²] for the S-curve; the instantaneous peak P_pk is not a continuous demand.\nContinuous rotary duty: ω = 2π n_L i/60; T_load = T_L/(i η); T_rms = T_load; T_pk = J_total ω/t_start + T_load (full load during the run-up, conservative); n_pk = n_L i; P_cont = T_load ω; P_rms = P_cont.",
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
            new CalcInputSpec("T_L", "Constant load torque at the output (rotary)", CalcInputKind.Quantity, "N*m", Default: 0, Min: 0, Max: 1e5, HardMin: -1e7, HardMax: 1e7, Group: "Load"),
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
            new CalcApplicabilityRule("mechanism-input", "The mechanism needs a dimension (screw lead or pulley diameter) or a load value that was not given.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("profile", "The acceleration fraction leaves no constant-velocity phase (f_acc = 0.5 is triangular).", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("inertia-ratio", "Load inertia exceeds the stated multiple of the motor's: tuning and stability may suffer.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("continuous-input", "Continuous duty needs a rotary mechanism, the load speed n_L, the run-up time t_start and the load inertia J_L.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("continuous-ignored", "In continuous duty the move distance, move time, acceleration fraction, dwell and profile are ignored.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("eta", "A direct-coupled rotary load has no transmission: an overall efficiency below 0.95 is probably a leftover default; use 1 or the coupling's efficiency.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("overhauling", "The load drives the motor during deceleration or on the decline (negative load torque); check the drive can regenerate or brake.", CalcNoticeSeverity.Warning),
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
                new CalcReferenceSource("Analytic (steady torque and speed, linear run-up)", null, "ω = 2π × 700 × 4/60 = 293.2153 rad/s (2800 rpm); T_load = 3/(4 × 0.9) = 0.83333 N·m; J_refl = 0.01/(16 × 0.9) = 6.9444e-4, J_total = 1.19444e-3 kg·m²; α = ω/2 = 146.6077 rad/s²; T_acc = 0.175115, T_pk = 1.008448 N·m; T_rms = T_load; P_cont = 0.83333 × 293.2153 = 244.346 W; u_peak = 1.25 × 1.008448/6 = 0.210093; u_rms = 1.25 × 0.83333/2 = 0.520833; u_speed = 2800/3500 = 0.8; J_ratio = 6.9444e-4/5e-4 = 1.38889"),
                [CalcInputValue.Choice("mechanism", MechGearedRotary), CalcInputValue.Choice("duty_mode", DutyContinuous), CalcInputValue.Of("i", 4), CalcInputValue.Of("eta", 0.9), CalcInputValue.Quantity("J_mech", 0, "kg*m^2"), CalcInputValue.Quantity("J_L", 0.01, "kg*m^2"), CalcInputValue.Quantity("T_L", 3, "N*m"), CalcInputValue.Quantity("n_L", 700, "rpm"), CalcInputValue.Quantity("t_start", 2, "s"), CalcInputValue.Quantity("J_motor", 5e-4, "kg*m^2"), CalcInputValue.Quantity("T_rated", 2.0, "N*m"), CalcInputValue.Quantity("T_peak", 6.0, "N*m"), CalcInputValue.Quantity("n_max", 3500, "rpm"), CalcInputValue.Of("SF", 1.25), CalcInputValue.Of("J_ratio_max", 10)],
                [new CalcExpectedValue("n_pk", 2800, "rpm", 1e-9), new CalcExpectedValue("T_load", 0.8333333, "N*m", 1e-6), new CalcExpectedValue("J_refl", 6.944444e-4, "kg*m^2", 1e-6), new CalcExpectedValue("J_total", 1.194444e-3, "kg*m^2", 1e-6), new CalcExpectedValue("T_acc", 0.1751147, "N*m", 1e-6), new CalcExpectedValue("T_pk", 1.008448, "N*m", 1e-6), new CalcExpectedValue("T_rms", 0.8333333, "N*m", 1e-6), new CalcExpectedValue("P_cont", 244.3461, "W", 1e-6), new CalcExpectedValue("P_rms", 0.2443461, "kW", 1e-6), new CalcExpectedValue("u_peak", 0.2100933, "", 1e-6), new CalcExpectedValue("u_rms", 0.5208333, "", 1e-6), new CalcExpectedValue("u_speed", 0.8, "", 1e-9), new CalcExpectedValue("J_ratio", 1.388889, "", 1e-6)]),
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
            new CalcOutputPort("n_pk", "rpm", "Peak motor speed"),
            new CalcOutputPort("P_pk", "kW", "Peak mechanical power"),
            new CalcOutputPort("P_cont", "W", "Continuous shaft power at the motor (continuous duty)"),
            new CalcOutputPort("P_rms", "kW", "RMS mechanical power over the cycle: the thermal-equivalent continuous power (→ E6 P_add)"),
            new CalcOutputPort("J_ratio", "", "Inertia ratio (load : motor)"),
            new CalcOutputPort("duty", "", "Thermal duty T_rms / T_rated"),
            new CalcOutputPort("u_peak", "", "Peak torque utilisation"),
            new CalcOutputPort("u_rms", "", "Continuous torque utilisation"),
            new CalcOutputPort("u_speed", "", "Speed utilisation"),
        ]);

    public void Run(CalcRunContext c)
    {
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
                    c.Assign("k", "p_screw / (2 * pi * i)", "mm", "Travel per motor radian");
                    break;
                default:
                    if (!c.Has("D_pulley")) { c.Block("mechanism-input", "D_pulley: the pulley or pinion pitch diameter is needed."); return; }
                    c.Input("D_pulley");
                    c.Assign("k", "D_pulley / (2 * i)", "mm", "Travel per motor radian");
                    break;
            }

            c.Assign("F_load", "m * 9.80665 m/s^2 * (sin(theta) + mu * cos(theta)) + F_p", "N", "Load force along the motion (gravity, friction, process)");
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
        c.Assign("n_pk", "omega_pk", "rpm", "Peak motor speed");
        c.Assign("P_pk", "T_pk * omega_pk", "kW", "Peak mechanical power");
        c.Assign("t_acc", "f_acc * t_move", "s", "Acceleration time");
        c.Assign("t_const", "t_move - 2 * t_acc", "s", "Constant-velocity time");
        c.Assign("t_cycle", "t_move + t_dwell", "s", "Cycle time");

        // RMS power: P = T·ω with ω rising linearly (trapezoid) or as (1 − cos)/2 (sine acceleration) through the acceleration phase and
        // mirrored through the deceleration; the closed forms follow from integrating (T·ω)² over the phases (Green G-B-11).
        c.Assign("P_rms", sCurve
            ? "omega_pk * sqrt((t_acc * (3 / 4 * T_load^2 + 5 / 16 * T_acc^2) + T_load^2 * t_const) / t_cycle)"
            : "omega_pk * sqrt((t_acc * (2 / 3) * (T_load^2 + T_acc^2) + T_load^2 * t_const) / t_cycle)", "kW", "RMS mechanical power over the cycle (thermal-equivalent continuous power)");

        // RMS: a sine acceleration has RMS a_pk/√2 over the acceleration time; the torque in that phase is J α(t) + T_load.
        c.Assign("T_rms", sCurve
            ? "sqrt(((T_acc^2 / 2 + T_load^2) * 2 * t_acc + T_load^2 * t_const) / t_cycle)"
            : "sqrt((T_pk^2 * t_acc + T_load^2 * t_const + T_dec^2 * t_acc) / t_cycle)", "N*m", "RMS torque over the cycle (thermal)");
        c.Assign("J_ratio", "(J_refl + J_mech) / J_motor", null, "Inertia ratio (load : motor)");
        c.Assign("u_peak", "SF * T_pk / T_peak", null, "Peak torque utilisation with safety margin");
        c.Assign("u_rms", "SF * T_rms / T_rated", null, "Continuous torque utilisation with safety margin");
        c.Assign("u_speed", "n_pk / n_max", null, "Speed utilisation");
        c.Assign("duty", "T_rms / T_rated", null, "Thermal duty (without margin)");

        if (c.Si("J_ratio") > c.Si("J_ratio_max"))
            c.Warn("inertia-ratio", $"J_motor: the inertia ratio {CalcValue.FormatNumber(c.Si("J_ratio"), 3)} exceeds {CalcValue.FormatNumber(c.Si("J_ratio_max"), 3)}.");

        if (c.Si("T_load") < 0 || c.Si("T_dec") < 0)
            c.Warn("overhauling", "T_dec: the motor brakes during deceleration (negative torque); the drive must absorb the regenerated energy.");

        foreach (var (symbol, text) in new[] { ("k", "Transmission constant"), ("T_load", "Load torque at motor"), ("J_refl", "Reflected load inertia"), ("J_total", "Total inertia"), (linear ? "v_pk" : "theta_m", linear ? "Peak velocity" : "Motor angle"), ("a_pk", "Peak acceleration"), ("alpha_pk", "Motor angular acceleration"), ("T_acc", "Acceleration torque"), ("T_pk", "Peak torque"), ("T_dec", "Deceleration torque"), ("T_rms", "RMS torque"), ("n_pk", "Peak speed"), ("P_pk", "Peak power"), ("P_rms", "RMS power (continuous equivalent)"), ("J_ratio", "Inertia ratio"), ("duty", "Thermal duty"), ("u_peak", "Peak utilisation"), ("u_rms", "Continuous utilisation"), ("u_speed", "Speed utilisation") })
            c.Output(symbol, text);

        c.CheckUtilisation("Peak torque SF·T_pk ≤ T_peak", "u_peak");
        c.CheckUtilisation("RMS torque SF·T_rms ≤ T_rated", "u_rms");
        c.CheckUtilisation("Speed n_pk ≤ n_max", "u_speed");
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
        c.Assign("n_pk", "omega_pk", "rpm", "Motor speed");
        c.Assign("P_cont", "T_load * omega_pk", "W", "Continuous shaft power at the motor");
        c.Assign("P_pk", "T_pk * omega_pk", "kW", "Peak mechanical power (end of the run-up)");
        c.Assign("P_rms", "P_cont", "kW", "RMS mechanical power: the steady running power (thermal-equivalent continuous power)");
        c.Assign("J_ratio", "(J_refl + J_mech) / J_motor", null, "Inertia ratio (load : motor)");
        c.Assign("u_peak", "SF * T_pk / T_peak", null, "Peak torque utilisation with safety margin");
        c.Assign("u_rms", "SF * T_rms / T_rated", null, "Continuous torque utilisation with safety margin");
        c.Assign("u_speed", "n_pk / n_max", null, "Speed utilisation");
        c.Assign("duty", "T_rms / T_rated", null, "Thermal duty (without margin)");

        if (c.Si("J_ratio") > c.Si("J_ratio_max"))
            c.Warn("inertia-ratio", $"J_motor: the inertia ratio {CalcValue.FormatNumber(c.Si("J_ratio"), 3)} exceeds {CalcValue.FormatNumber(c.Si("J_ratio_max"), 3)}.");

        if (c.Si("T_load") < 0)
            c.Warn("overhauling", "T_L: the load drives the motor (negative load torque); check the drive can regenerate or brake.");

        foreach (var (symbol, text) in new[] { ("k", "Transmission constant"), ("T_load", "Load torque at motor"), ("J_refl", "Reflected load inertia"), ("J_total", "Total inertia"), ("alpha_pk", "Run-up angular acceleration"), ("T_acc", "Run-up torque"), ("T_pk", "Peak torque"), ("T_rms", "RMS torque"), ("n_pk", "Motor speed"), ("P_cont", "Continuous power"), ("P_pk", "Peak power"), ("P_rms", "RMS power (continuous equivalent)"), ("J_ratio", "Inertia ratio"), ("duty", "Thermal duty"), ("u_peak", "Peak utilisation"), ("u_rms", "Continuous utilisation"), ("u_speed", "Speed utilisation") })
            c.Output(symbol, text);

        c.CheckUtilisation("Peak torque SF·T_pk ≤ T_peak", "u_peak");
        c.CheckUtilisation("RMS torque SF·T_rms ≤ T_rated", "u_rms");
        c.CheckUtilisation("Speed n_pk ≤ n_max", "u_speed");
    }
}
