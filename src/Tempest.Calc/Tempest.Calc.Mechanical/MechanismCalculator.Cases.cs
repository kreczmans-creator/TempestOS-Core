using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

public sealed partial class MechanismCalculator
{
    private const string AnalyticSource = "Analytic (closed-form; recomputed in python3)";
    private const string FourBarOracle = "Independent python3 recomputation: Freudenstein half-angle quadratic for the position, real 2×2 solves of the differentiated loop (cross-checked by central finite differences), peaks by a 20001-point scan polished by golden section";
    private const string SliderOracle = "Independent python3 recomputation: closed-form derivatives of x = r cosθ ± √(l² − (r sinθ − e)²) (cross-checked by central finite differences), peaks by a 20001-point scan polished by golden section";

    private static CalcInputValue[] FourBarInputs(string circuit, double d, double a, double b, double c, double startDeg, double rangeDeg, double steps, double rpm, double torque, double inertia, string loadDir, double muReq) =>
    [
        CalcInputValue.Choice("mode", FourBar), CalcInputValue.Choice("circuit", circuit),
        Q("L1", d, "mm"), Q("L2", a, "mm"), Q("L3", b, "mm"), Q("L4", c, "mm"),
        Q("theta_start", startDeg, "deg"), Q("theta_range", rangeDeg, "deg"), CalcInputValue.Of("steps", steps), Q("n_in", rpm, "rpm"), Q("mu_req", muReq, "deg"),
        CalcInputValue.Choice("load_dir", loadDir), Q("T_load", torque, "N*m"), Q("J_out", inertia, "kg*m^2"),
    ];

    private static CalcInputValue[] SliderInputs(string circuit, double r, double l, double e, double startDeg, double steps, double rpm, double mass, double load, string loadDir) =>
    [
        CalcInputValue.Choice("mode", SliderCrank), CalcInputValue.Choice("circuit", circuit),
        Q("L2", r, "mm"), Q("L3", l, "mm"), Q("e_off", e, "mm"),
        Q("theta_start", startDeg, "deg"), Q("theta_range", 360, "deg"), CalcInputValue.Of("steps", steps), Q("n_in", rpm, "rpm"), Q("mu_req", 40, "deg"),
        CalcInputValue.Choice("load_dir", loadDir), Q("m_slider", mass, "kg"), Q("F_load", load, "N"),
    ];

    private static CalcExpectedValue X(string symbol, double value, string unit, double tolerance) => new(symbol, value, unit, tolerance);

    private static IReadOnlyList<CalcTestCase> BuildCases()
    {
        var cases = new List<CalcTestCase>
        {
            new("ka-fourbar-crank-rocker-open", "Crank-rocker 120/40/110/90 mm, open circuit, crank at 90°: positions, velocities, accelerations, peaks, input torque",
                new CalcReferenceSource(FourBarOracle, null, "d 120, a 40, b 110, c 90 mm (Grashof: 40 + 120 < 110 + 90), crank 90° to 450° at 120 rpm, J = 0.004 kg·m², T_L = 2.5 N·m opposing ω₄: at θ₂ = 90°, open circuit, θ₃ = 25.617841°, θ₄ = 103.371151°, ω₃ = −1.0813626 and ω₄ = 5.1533032 rad/s, α₃ = 35.187265 and α₄ = 26.749883 rad/s², μ = 77.753310°, T_in = (T_L + J α₄)ω₄/ω₂ = 1.0690962 N·m; the crossed circuit gives θ₄ = 219.758951°. Sweep peaks: |ω₄| 7.4436294 rad/s, |α₄| 134.28075 rad/s², μ_min 45.815615°, peak T_in 1.5134299 N·m."),
                FourBarInputs(Open, 120, 40, 110, 90, 90, 360, 72, 120, 2.5, 0.004, Opposes, 40),
                [X("S_plus_L", 160, "mm", 1e-9), X("P_plus_Q", 200, "mm", 1e-9), X("theta3_0", 25.617840923767837, "deg", 1e-9), X("theta4_0", 103.37115119645648, "deg", 1e-9), X("omega3_0", -1.0813626044165845, "rad/s", 1e-9), X("omega4_0", 5.153303171190088, "rad/s", 1e-9), X("alpha3_0", 35.18726518042769, "rad/s^2", 1e-9), X("alpha4_0", 26.749882941188588, "rad/s^2", 1e-9), X("mu_0", 77.75331027268864, "deg", 1e-9), X("T_in_0", 1.0690961906680558, "N*m", 1e-9), X("theta4_alt", 219.7589511576995, "deg", 1e-9), X("omega_out_pk", 7.443629390207442, "rad/s", 1e-7), X("alpha_out_pk", 134.2807515703869, "rad/s^2", 1e-7), X("omega3_pk", 6.351675266270291, "rad/s", 1e-7), X("alpha3_pk", 109.60057795927224, "rad/s^2", 1e-7), X("mu_min", 45.815614846695546, "deg", 1e-7), X("input_torque", 1.5134299396600364, "N*m", 1e-7), X("follower_force", 33.09473637769108, "N", 1e-7), X("output_motion", 0.6699266451186697, "m/s", 1e-7), X("peak_accel", 12.102330098518621, "m/s^2", 1e-7), X("res_max", 0, "mm", 1e-9)]),
            new("ka-fourbar-parallelogram", "Parallelogram four-bar: the rocker copies the crank, the coupler translates",
                new CalcReferenceSource(AnalyticSource, null, "d = b = 100 mm, a = c = 40 mm, crank 5° to 175° at 120 rpm (ω₂ = 4π rad/s), T_L = 3 N·m constant, J = 0.01 kg·m²: θ₄ = θ₂, θ₃ = 0, ω₄ = ω₂, ω₃ = 0, α = 0, so T_in = T_L·ω₄/ω₂ = 3 N·m exactly, the tangential pin force is T_L/c = 75 N, the pin moves at c·ω₂ = 0.5026548 m/s and accelerates at c·ω₂² = 6.3165468 m/s²; μ = min(θ₂, 180° − θ₂) so μ_min = 5°; s + l = p + q = 140 mm (change point). The Freudenstein quadratic gives θ₄ = 5° on the open circuit and 348.366061° on the crossed one."),
                FourBarInputs(Open, 100, 40, 100, 40, 5, 170, 34, 120, 3, 0.01, Constant, 4),
                [X("S_plus_L", 140, "mm", 1e-9), X("P_plus_Q", 140, "mm", 1e-9), X("theta3_0", 0, "deg", 1e-9), X("theta4_0", 5, "deg", 1e-9), X("omega3_0", 0, "rad/s", 1e-9), X("omega4_0", 12.566370614359172, "rad/s", 1e-9), X("alpha3_0", 0, "rad/s^2", 1e-7), X("alpha4_0", 0, "rad/s^2", 1e-7), X("mu_0", 5, "deg", 1e-9), X("T_in_0", 3, "N*m", 1e-9), X("theta4_alt", 348.366061098273, "deg", 1e-9), X("omega_out_pk", 12.566370614359172, "rad/s", 1e-9), X("mu_min", 5, "deg", 1e-9), X("input_torque", 3, "N*m", 1e-9), X("follower_force", 75, "N", 1e-9), X("output_motion", 0.502654824574367, "m/s", 1e-9), X("peak_accel", 6.31654681669719, "m/s^2", 1e-9), X("res_max", 0, "mm", 1e-9)]),
            new("ka-fourbar-crank-rocker-crossed", "The same crank-rocker on the crossed circuit",
                new CalcReferenceSource(FourBarOracle, null, "As the open-circuit case with the crossed circuit: at θ₂ = 90° θ₃ = −62.487739°, θ₄ = 219.758951°, ω₃ = 3.5946367 and ω₄ = −2.6400290 rad/s, α₃ = 40.611297 and α₄ = 49.048679 rad/s², μ = 77.753310° (the transmission angle is the same on both circuits), T_in = 0.48399916 N·m; the open circuit gives θ₄ = 103.371151°. Peaks: |ω₄| 7.4436294 rad/s, |α₄| 134.28075 rad/s², peak T_in 1.5240251 N·m."),
                FourBarInputs(Crossed, 120, 40, 110, 90, 90, 360, 72, 120, 2.5, 0.004, Opposes, 40),
                [X("theta3_0", -62.487738569611864, "deg", 1e-9), X("theta4_0", 219.7589511576995, "deg", 1e-9), X("omega3_0", 3.5946367272884188, "rad/s", 1e-9), X("omega4_0", -2.6400290483182536, "rad/s", 1e-9), X("alpha3_0", 40.611296619938564, "rad/s^2", 1e-9), X("alpha4_0", 49.04867885917766, "rad/s^2", 1e-9), X("mu_0", 77.75331027268864, "deg", 1e-9), X("T_in_0", 0.4839991640837298, "N*m", 1e-9), X("theta4_alt", 103.37115119645648, "deg", 1e-9), X("omega_out_pk", 7.4436293902074375, "rad/s", 1e-7), X("alpha_out_pk", 134.280751570387, "rad/s^2", 1e-7), X("mu_min", 45.815614846695546, "deg", 1e-7), X("input_torque", 1.5240251496269444, "N*m", 1e-7), X("follower_force", 33.74581118090609, "N", 1e-7)]),
            new("ka-slider-inline", "In-line slider-crank r = 50, l = 250 mm at 600 rpm, crank at 90°: closed-form position, velocity, acceleration, stroke, peak acceleration",
                new CalcReferenceSource(AnalyticSource, null, "r 50 mm, l 250 mm, e = 0, 600 rpm (ω = 20π rad/s), slider 0.8 kg, constant load 150 N, crank 90° to 450°: x(90°) = √(l² − r²) = 244.94897 mm, v = −r·ω = −3.1415927 m/s, a = ω²·r²/√(l² − r²) = 40.292491 m/s², rod angle −11.536959°, ω₃ = 0, μ = arccos(r/l) = 78.463041°; stroke = 2r = 100 mm (x from 200 to 300 mm, closed form √((l+r)² − e²) − √((l−r)² − e²)); peak |a| at θ = 0 is r·ω²·(1 + r/l) = 236.87051 m/s²; T_in(90°) = (m·a + F)·(−r) = −9.1117 N·m. The crossed circuit puts the slider at −244.94897 mm."),
                SliderInputs(Open, 50, 250, 0, 90, 72, 600, 0.8, 150, Constant),
                [X("x_0", 244.9489742783178, "mm", 1e-9), X("v_0", -3.141592653589793, "m/s", 1e-9), X("a_0", 40.292491242993485, "m/s^2", 1e-9), X("theta3_0", -11.53695903281549, "deg", 1e-9), X("omega3_0", 0, "rad/s", 1e-9), X("mu_0", 78.46304096718453, "deg", 1e-9), X("T_in_0", -9.11169964971974, "N*m", 1e-9), X("x_alt", -244.9489742783178, "mm", 1e-9), X("x_max", 300, "mm", 1e-9), X("x_min", 200, "mm", 1e-9), X("stroke", 100, "mm", 1e-9), X("stroke_closed", 100, "mm", 1e-9), X("peak_accel", 236.87050562614462, "m/s^2", 1e-9), X("output_motion", 3.2039001123142823, "m/s", 1e-7), X("input_torque", 10.015914947654077, "N*m", 1e-7), X("follower_force", 276.3309363339438, "N", 1e-7), X("res_max", 0, "mm", 1e-9)]),
            new("ka-slider-offset", "Offset slider-crank (e = 20 mm): position at 0°, stroke between the dead centres, peaks",
                new CalcReferenceSource(AnalyticSource, null, "r 50 mm, l 250 mm, e = +20 mm, 600 rpm, slider 0.8 kg, load 150 N opposing the motion, crank from 0°: x(0) = r + √(l² − e²) = 299.19872 mm, v(0) = 0.25213554 m/s, a(0) = −237.25255 m/s², θ₃ = 4.588566°, ω₃ = −12.606777 rad/s, α₃ = 12.755349 rad/s², μ = 85.411434°; stroke = √((l + r)² − e²) − √((l − r)² − e²) = 100.335104 mm (swept 299.332591 − 198.997487); the crossed circuit puts the slider at −199.19872 mm. Peak |v| 3.2654572 m/s, peak |a| 237.52786 m/s², μ_min 73.739795°."),
                SliderInputs(Open, 50, 250, 20, 0, 72, 600, 0.8, 150, Opposes),
                [X("x_0", 299.1987158875423, "mm", 1e-9), X("v_0", 0.25213554110026176, "m/s", 1e-9), X("a_0", -237.25255318027496, "m/s^2", 1e-9), X("theta3_0", 4.588565735785835, "deg", 1e-9), X("omega3_0", -12.606777055013088, "rad/s", 1e-9), X("alpha3_0", 12.755348850716102, "rad/s^2", 1e-9), X("mu_0", 85.41143426421417, "deg", 1e-9), X("T_in_0", -0.15972009487473335, "N*m", 1e-9), X("x_alt", -199.19871588754222, "mm", 1e-9), X("x_max", 299.3325909419153, "mm", 1e-9), X("x_min", 198.99748742132397, "mm", 1e-9), X("stroke", 100.33510352059133, "mm", 1e-9), X("stroke_closed", 100.33510352059133, "mm", 1e-9), X("output_motion", 3.265457220988406, "m/s", 1e-7), X("peak_accel", 237.5278558879714, "m/s^2", 1e-7), X("omega3_pk", 12.608480964698442, "rad/s", 1e-7), X("alpha3_pk", 822.4670334241132, "rad/s^2", 1e-7), X("mu_min", 73.73979529168804, "deg", 1e-7), X("input_torque", 10.436357950518868, "N*m", 1e-7)]),
            new("ka-slider-crossed", "The offset slider-crank on the crossed circuit (slider on the −x side), crank at 37°",
                new CalcReferenceSource(SliderOracle, null, "r 50 mm, l 250 mm, e = +20 mm, 600 rpm, slider 0.8 kg, constant load 150 N, crossed circuit (x = r cosθ − √(…)), crank from 37°: x = −209.86449 mm, v = −1.7893048 m/s, a = −137.20137 m/s², θ₃ = −177.68674°, ω₃ = 10.044135 rad/s, α₃ = −471.48631 rad/s², μ = 87.686742°, T_in = −1.1459102 N·m; x ranges from −299.332591 to −198.997487 mm (stroke 100.335104 mm); the open circuit puts the slider at 289.72805 mm."),
                SliderInputs(Crossed, 50, 250, 20, 37, 72, 600, 0.8, 150, Constant),
                [X("x_0", -209.86449496834385, "mm", 1e-9), X("v_0", -1.7893047891545226, "m/s", 1e-9), X("a_0", -137.20137338389128, "m/s^2", 1e-9), X("theta3_0", -177.68674178005475, "deg", 1e-9), X("omega3_0", 10.044134952586175, "rad/s", 1e-9), X("alpha3_0", -471.48631135676334, "rad/s^2", 1e-9), X("mu_0", 87.68674178005476, "deg", 1e-9), X("T_in_0", -1.1459101597943824, "N*m", 1e-9), X("x_alt", 289.7280459730731, "mm", 1e-9), X("x_max", -198.997487421324, "mm", 1e-9), X("x_min", -299.33259094191527, "mm", 1e-9), X("stroke", 100.33510352059128, "mm", 1e-9), X("input_torque", 10.991134024781129, "N*m", 1e-7), X("follower_force", 340.0222847103771, "N", 1e-7)]),
        };

        AddCamCases(cases);
        return cases;
    }
}
