using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

/// <summary>
/// M7 — cylindrical helical springs of round wire, after EN 13906-1 (compression), -2 (extension) and -3 (torsion). Compression is
/// the first mode: rate, working points, stress with a correction factor, solid length by end type, buckling with the user's
/// end-support coefficient, Goodman fatigue and the surge frequency. Extension adds the initial tension and the hook stresses;
/// torsion the leg-corrected rate and the curvature-corrected bending stress. Wire strength R_m(d) and the permissible stress
/// fractions are the user's inputs (records <c>spring.wire-strength</c> and <c>spring.permissible</c>); no wire-standard table
/// and no permissible-stress diagram is shipped (Addendum B §B5).
/// </summary>
public sealed class SpringCalculator : ICalcCalculator
{
    public const string Id = "mech.spring";

    public const string Compression = "Compression";
    public const string Extension = "Extension";
    public const string Torsion = "Torsion";

    public const string Bergstraesser = "Bergsträsser";
    public const string Wahl = "Wahl";
    public const string Uncorrected = "None (uncorrected)";

    public const string Plain = "Plain";
    public const string PlainGround = "Plain, ground";
    public const string Squared = "Squared (closed)";
    public const string SquaredGround = "Squared and ground";

    public const string Loads = "Loads";
    public const string Deflections = "Deflections";

    /// <summary>The surge ratio f_n/f_op below which the sheet warns.</summary>
    public const double SurgeRatioLimit = 13;

    /// <summary>The record kind of a W3 material (<c>MaterialLibrary.Kind</c>; written out here because the mechanical assembly does not reference the library, and a test keeps the two equal).</summary>
    public const string MaterialKind = "material.properties";

    private const string Part1 = "EN 13906-1";
    private const string Part2 = "EN 13906-2";
    private const string Part3 = "EN 13906-3";

    public CalcTemplate Template { get; } = new(
        Id, "1.0.0", CalcDiscipline.Mechanical, "Helical springs: compression, extension, torsion",
        "A cylindrical helical spring of round wire, cold-coiled, with a linear characteristic: compression (rate, working points, solid length by end type, stress at the working points and at solid, buckling, Goodman fatigue, surge frequency), extension (the same with the initial tension and the hook stresses) or torsion (rate with the legs, curvature-corrected bending stress, wound diameter and body length).",
        "Index and rate: C = D/d; k = G·d⁴/(8·D³·n) (compression, extension); k_φ = E·d⁴/(64·D·n_a), n_a = n + (l₁ + l₂)/(3π·D) (torsion)\n" +
        "Stress: τ_k = k_c·8·F·D/(π·d³), k_c = (C + 0.5)/(C − 0.75) (Bergsträsser) or (4C − 1)/(4C − 4) + 0.615/C (Wahl); torsion σ = K_i·32·M/(π·d³), K_i = (4C² − C − 1)/(4C·(C − 1))\n" +
        "Solid length (compression, derived from the geometry of the end): n_t = n + e_n, L_s = d·(n_t + e_s) with (e_n, e_s) = (0, 1) plain, (1, 0) plain and ground, (2, 1) squared, (2, 0) squared and ground; pitch p = (L₀ − a·d)/(n + b)\n" +
        "Extension: F = F₀ + k·s; hook bending σ_A = F·[K_A·16·D/(π·d³) + 4/(π·d²)], K_A = (4C₁² − C₁ − 1)/(4C₁·(C₁ − 1)), C₁ = 2r₁/d; hook torsion τ_B = K_B·8·F·D/(π·d³), K_B = (4C₂ − 1)/(4C₂ − 4), C₂ = 2r₂/d\n" +
        "Buckling (compression): λ_eff = ν·L₀/D; y_cr = L₀·C₁′·[1 − √(1 − C₂′/λ_eff²)], C₁′ = E/(2(E − G)), C₂′ = 2π²(E − G)/(2G + E); stable at any deflection when λ_eff² < C₂′\n" +
        "Fatigue (Goodman, as M4): 1/n_f = a/S_e + m/S_u with a = (x₂ − x₁)/2, m = (x₂ + x₁)/2 of the stresses at the two working points; S_u = f_su·R_m in shear, R_m in bending\n" +
        "Surge: f_n = (d/(2π·n·D²))·√(G/(2ρ)); warning when f_n/f_op < 13\n" +
        "Torsion body: D_w = D·n/(n + φ/2π); L_w = d·(n + 1 + φ/2π)",
        [
            new CalcInputSpec("mode", "Kind of spring", CalcInputKind.Choice, Choices: [Compression, Extension, Torsion], DefaultChoice: Compression, Group: "Spring"),
            new CalcInputSpec("corr", "Stress correction factor k_c for the shear stress", CalcInputKind.Choice, Choices: [Bergstraesser, Wahl, Uncorrected], DefaultChoice: Bergstraesser, Group: "Spring"),
            new CalcInputSpec("d", "Wire diameter", CalcInputKind.Quantity, "mm", Default: 2, Min: 0.1, Max: 20, HardMin: 0.05, HardMax: 50, Group: "Spring"),
            new CalcInputSpec("D", "Mean coil diameter", CalcInputKind.Quantity, "mm", Default: 16, Min: 0.5, Max: 400, HardMin: 0.1, HardMax: 1000, Group: "Spring"),
            new CalcInputSpec("n", "Active coils (torsion: coils of the body)", CalcInputKind.Number, Default: 8, Min: 2, Max: 200, HardMin: 0.25, HardMax: 1000, Group: "Spring"),
            new CalcInputSpec("ends", "End type (compression)", CalcInputKind.Choice, Choices: [Plain, PlainGround, Squared, SquaredGround], DefaultChoice: SquaredGround, Group: "Spring"),
            new CalcInputSpec("L0", "Free length (compression)", CalcInputKind.Quantity, "mm", Default: 45, Min: 1, Max: 1000, HardMin: 0.1, HardMax: 10000, IsOptional: true, Group: "Spring"),
            new CalcInputSpec("r_h1", "Hook mean radius, bending (extension)", CalcInputKind.Quantity, "mm", Default: 8, Min: 0.5, Max: 200, HardMin: 0.05, HardMax: 1000, IsOptional: true, Group: "Spring"),
            new CalcInputSpec("r_h2", "Hook side-bend mean radius, torsion (extension)", CalcInputKind.Quantity, "mm", Default: 4, Min: 0.5, Max: 200, HardMin: 0.05, HardMax: 1000, IsOptional: true, Group: "Spring"),
            new CalcInputSpec("l_1", "Leg length 1 (torsion)", CalcInputKind.Quantity, "mm", Default: 0, Min: 0, Max: 500, HardMin: 0, HardMax: 10000, IsOptional: true, Group: "Spring"),
            new CalcInputSpec("l_2", "Leg length 2 (torsion)", CalcInputKind.Quantity, "mm", Default: 0, Min: 0, Max: 500, HardMin: 0, HardMax: 10000, IsOptional: true, Group: "Spring"),
            new CalcInputSpec("work", "Working points given as", CalcInputKind.Choice, Choices: [Loads, Deflections], DefaultChoice: Loads, Group: "Working points"),
            new CalcInputSpec("F_1", "Force at working point 1 (loads)", CalcInputKind.Quantity, "N", Default: 10, Min: 0, Max: 20000, HardMin: 0, HardMax: 1e6, IsOptional: true, Group: "Working points"),
            new CalcInputSpec("F_2", "Force at working point 2 (loads)", CalcInputKind.Quantity, "N", Default: 40, Min: 0, Max: 20000, HardMin: 0, HardMax: 1e6, IsOptional: true, Group: "Working points"),
            new CalcInputSpec("s_1", "Deflection at working point 1 (deflections)", CalcInputKind.Quantity, "mm", Default: 2, Min: 0, Max: 1000, HardMin: 0, HardMax: 1e5, IsOptional: true, Group: "Working points"),
            new CalcInputSpec("s_2", "Deflection at working point 2 (deflections)", CalcInputKind.Quantity, "mm", Default: 8, Min: 0, Max: 1000, HardMin: 0, HardMax: 1e5, IsOptional: true, Group: "Working points"),
            new CalcInputSpec("F_0", "Initial tension (extension)", CalcInputKind.Quantity, "N", Default: 10, Min: 0, Max: 5000, HardMin: 0, HardMax: 1e6, IsOptional: true, Group: "Working points"),
            new CalcInputSpec("M_1", "Torque at working point 1 (torsion, loads)", CalcInputKind.Quantity, "N*m", Default: 0.05, Min: 0, Max: 100, HardMin: 0, HardMax: 1e5, IsOptional: true, Group: "Working points"),
            new CalcInputSpec("M_2", "Torque at working point 2 (torsion, loads)", CalcInputKind.Quantity, "N*m", Default: 0.15, Min: 0, Max: 100, HardMin: 0, HardMax: 1e5, IsOptional: true, Group: "Working points"),
            new CalcInputSpec("phi_1", "Angle at working point 1 (torsion, deflections)", CalcInputKind.Quantity, "deg", Default: 5, Min: 0, Max: 1080, HardMin: 0, HardMax: 1e5, IsOptional: true, Group: "Working points"),
            new CalcInputSpec("phi_2", "Angle at working point 2 (torsion, deflections)", CalcInputKind.Quantity, "deg", Default: 15, Min: 0, Max: 1080, HardMin: 0, HardMax: 1e5, IsOptional: true, Group: "Working points"),
            new CalcInputSpec("G", "Shear modulus", CalcInputKind.Quantity, "GPa", Default: 80, Min: 20, Max: 100, HardMin: 1, HardMax: 200, Group: "Material", MaterialProperty: "G"),
            new CalcInputSpec("E", "Young's modulus", CalcInputKind.Quantity, "GPa", Default: 206, Min: 50, Max: 250, HardMin: 5, HardMax: 500, Group: "Material", MaterialProperty: "E"),
            new CalcInputSpec("rho", "Density", CalcInputKind.Quantity, "kg/m^3", Default: 7850, Min: 1000, Max: 20000, HardMin: 100, HardMax: 25000, Group: "Material", MaterialProperty: "rho"),
            new CalcInputSpec("Rm", "Tensile strength of the wire at this diameter R_m(d)", CalcInputKind.Quantity, "MPa", Default: 1800, Min: 300, Max: 3500, HardMin: 50, HardMax: 5000, Group: "Strength"),
            new CalcInputSpec("f_tau", "Permissible shear stress as a fraction of R_m", CalcInputKind.Number, Default: 0.45, Min: 0.2, Max: 0.8, HardMin: 0.01, HardMax: 1, Group: "Strength"),
            new CalcInputSpec("f_sigma", "Permissible bending stress as a fraction of R_m (torsion, hooks)", CalcInputKind.Number, Default: 0.75, Min: 0.3, Max: 1, HardMin: 0.01, HardMax: 1.5, IsOptional: true, Group: "Strength"),
            new CalcInputSpec("f_su", "Ultimate shear strength as a fraction of R_m (Goodman, shear)", CalcInputKind.Number, Default: 0.67, Min: 0.4, Max: 0.9, HardMin: 0.05, HardMax: 1, Group: "Strength"),
            new CalcInputSpec("nu_seat", "End-support coefficient ν (0.5 both ends fixed, 0.7 one fixed one pinned, 1 both pinned, 2 one free)", CalcInputKind.Number, Default: 0.5, Min: 0.5, Max: 2, HardMin: 0.25, HardMax: 5, Group: "Buckling"),
            new CalcInputSpec("S_e", "Endurance strength for the stress kind (shear; bending in torsion) at N_c cycles, alternating", CalcInputKind.Quantity, "MPa", Default: 400, Min: 50, Max: 2000, HardMin: 1, HardMax: 1e4, IsOptional: true, Group: "Fatigue and surge"),
            new CalcInputSpec("N_c", "Required life the endurance strength belongs to", CalcInputKind.Number, Default: 1e7, Min: 1e4, Max: 1e10, HardMin: 1, HardMax: 1e12, Group: "Fatigue and surge"),
            new CalcInputSpec("n_f_req", "Required Goodman safety factor", CalcInputKind.Number, Default: 1.2, Min: 1, Max: 5, HardMin: 0.5, HardMax: 20, Group: "Fatigue and surge"),
            new CalcInputSpec("f_op", "Operating (excitation) frequency", CalcInputKind.Quantity, "Hz", Default: 10, Min: 0, Max: 1000, HardMin: 0, HardMax: 1e6, IsOptional: true, Group: "Fatigue and surge"),
        ],
        [
            new CalcAssumption("Linear characteristic: the rate is constant between the working points; no progressive or conical spring, no variable pitch.", "Cold-coiled round wire up to 20 mm."),
            new CalcAssumption("Active coils n are as entered; end coils are accounted for only in the solid length (and the pitch) by the end type; the hook and leg flexibility is in the rate only for the torsion legs.", "Shigley 10th ed. Ch. 10, geometry of the ends."),
            new CalcAssumption("R_m(d), the permissible stress fractions and the endurance strength are the user's inputs from the user's own copy of the wire standard and design reference; no wire-strength table and no permissible-stress diagram is shipped.", "Addendum B §B5."),
            new CalcAssumption("The stress correction factor k_c is applied to the working and the solid stress alike (conservative for the static check).", "Choose 'None (uncorrected)' for the uncorrected static stress."),
            new CalcAssumption("Extension: hook fatigue and the hook shape beyond its two radii are not assessed; the initial tension is a force the user enters.", "Shigley 10th ed. Ch. 10, extension springs."),
            new CalcAssumption("Surge is the first natural frequency of a spring with both ends fixed; a spring with one end free has half of it.", "Shigley 10th ed. Ch. 10, critical frequency."),
        ],
        [
            new CalcClauseRef(Part1, "Calculation of compression springs", "Rate, stress correction, solid length, buckling, fatigue, surge"),
            new CalcClauseRef(Part2, "Calculation of extension springs", "Rate, initial tension, hook stress"),
            new CalcClauseRef(Part3, "Calculation of torsion springs", "Rate, bending stress with curvature correction"),
            new CalcClauseRef("Shigley's Mechanical Engineering Design", "10th ed., Ch. 10", "Helical springs: end geometry, buckling, critical frequency, extension and torsion springs"),
        ],
        [
            new CalcApplicabilityRule("geometry", "The mean coil diameter must exceed the wire diameter; the elastic constants must satisfy E > G.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("solid", "The free length must exceed the solid length of the end type.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("sequence", "Working point 2 must not be below working point 1; an extension spring's first force must reach the initial tension.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("hook", "The hook radii must exceed half the wire diameter.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("index", "The spring index C = D/d is outside 4 … 20.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("buckling", "The deflection at working point 2 reaches the buckling deflection of the spring.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("surge", "The surge frequency is less than 13 times the operating frequency.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("fatigue-cycles", "The required life is below 10⁴ cycles: the Goodman relation is for high-cycle fatigue.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("fatigue-skipped", "No endurance strength entered: the fatigue check is not made.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-compression", "Compression spring, squared and ground ends: rate, stress, solid length, surge, Goodman",
                new CalcReferenceSource("Analytic (closed-form; recomputed in python3)", null, "d 2 mm, D 16 mm, n 8, G 80 GPa: k = G d⁴/(8 D³ n) = 4.8828125 N/mm exactly; C = 8, k_c = 8.5/7.25 = 1.1724138; n_t = 10, L_s = 20 mm; F_2 = 40 N: s_2 = 8.192 mm, τ_2 = k_c·8·F·D/(π d³) = 238.84218 MPa; F_c = k·(45 − 20) = 122.07031 N, τ_c = 728.88848 MPa; f_n = (d/(2π n D²))√(G/2ρ) = 350.84504 Hz; Goodman 1/n_f = τ_a/S_e + τ_m/(0.67·R_m) = 0.3476926"),
                [Q("d", 2, "mm"), Q("D", 16, "mm"), CalcInputValue.Of("n", 8), CalcInputValue.Choice("mode", Compression), CalcInputValue.Choice("corr", Bergstraesser), CalcInputValue.Choice("ends", SquaredGround), Q("L0", 45, "mm"), CalcInputValue.Choice("work", Loads), Q("F_1", 10, "N"), Q("F_2", 40, "N"), Q("G", 80, "GPa"), Q("E", 206, "GPa"), Q("rho", 7850, "kg/m^3"), Q("Rm", 1800, "MPa"), CalcInputValue.Of("f_tau", 0.45), CalcInputValue.Of("f_su", 0.67), CalcInputValue.Of("nu_seat", 0.5), Q("S_e", 400, "MPa"), CalcInputValue.Of("N_c", 1e7), CalcInputValue.Of("n_f_req", 1.2), Q("f_op", 10, "Hz")],
                [new CalcExpectedValue("C", 8, "", 1e-9), new CalcExpectedValue("k", 4.8828125, "N/mm", 1e-9), new CalcExpectedValue("k_c", 1.17241379, "", 1e-7), new CalcExpectedValue("L_s", 20, "mm", 1e-9), new CalcExpectedValue("s_2", 8.192, "mm", 1e-9), new CalcExpectedValue("tau_2", 238.842177, "MPa", 1e-7), new CalcExpectedValue("F_c", 122.070312, "N", 1e-7), new CalcExpectedValue("tau_c", 728.888479, "MPa", 1e-7), new CalcExpectedValue("u_c", 0.899862319, "", 1e-7), new CalcExpectedValue("f_n", 350.845041, "Hz", 1e-7), new CalcExpectedValue("r_f", 35.0845041, "", 1e-7), new CalcExpectedValue("u_f", 0.347692617, "", 1e-7)]),
            new CalcTestCase("ka-wahl-slender", "Slender compression spring from deflections, Wahl factor, pinned ends: buckling deflection",
                new CalcReferenceSource("Analytic (closed-form; recomputed in python3)", null, "d 2 mm, D 20 mm, n 10, G 80 GPa, E 206 GPa, plain and ground ends, L₀ 80 mm, ν 1: k = 2 N/mm, s₁ 4 and s₂ 12 mm give F₁ 8 and F₂ 24 N; Wahl k_c = 39/36 + 0.0615 = 1.1448333; L_s = 22 mm; C₁′ = 206/252 = 0.8174603, C₂′ = 2π²·126/366 = 6.7954653, λ_eff = 4, y_cr = 80·C₁′·(1 − √(1 − C₂′/16)) = 15.795007 mm, s₂/y_cr = 0.7597337; f_n = 179.63266 Hz, f_n/25 Hz = 7.185306"),
                [Q("d", 2, "mm"), Q("D", 20, "mm"), CalcInputValue.Of("n", 10), CalcInputValue.Choice("mode", Compression), CalcInputValue.Choice("corr", Wahl), CalcInputValue.Choice("ends", PlainGround), Q("L0", 80, "mm"), CalcInputValue.Choice("work", Deflections), Q("s_1", 4, "mm"), Q("s_2", 12, "mm"), Q("G", 80, "GPa"), Q("E", 206, "GPa"), Q("rho", 7850, "kg/m^3"), Q("Rm", 1800, "MPa"), CalcInputValue.Of("f_tau", 0.45), CalcInputValue.Of("f_su", 0.67), CalcInputValue.Of("nu_seat", 1), Q("S_e", 350, "MPa"), CalcInputValue.Of("N_c", 1e7), CalcInputValue.Of("n_f_req", 1.2), Q("f_op", 25, "Hz")],
                [new CalcExpectedValue("k", 2, "N/mm", 1e-9), new CalcExpectedValue("F_1", 8, "N", 1e-9), new CalcExpectedValue("F_2", 24, "N", 1e-9), new CalcExpectedValue("k_c", 1.14483333, "", 1e-7), new CalcExpectedValue("L_s", 22, "mm", 1e-9), new CalcExpectedValue("y_cr", 15.7950074, "mm", 1e-7), new CalcExpectedValue("u_b", 0.759733736, "", 1e-7), new CalcExpectedValue("f_n", 179.632661, "Hz", 1e-7), new CalcExpectedValue("r_f", 7.18530644, "", 1e-7), new CalcExpectedValue("n_f", 3.79821968, "", 1e-7)]),
            new CalcTestCase("ka-extension", "Extension spring with initial tension and hooks: rate, stresses, hook stresses",
                new CalcReferenceSource("Analytic (closed-form; recomputed in python3)", null, "d 2 mm, D 16 mm, n 20, G 80 GPa: k = 1.953125 N/mm exactly; F₀ 10 N, F₁ 30 N, F₂ 60 N: s₁ 10.24 mm, s₂ 25.6 mm; k_c = 1.1724138, τ₀ = 59.710544, τ₂ = 358.26327 MPa; hooks r₁ 8 mm (C₁ = 8, K_A = 247/224 = 1.1026786), r₂ 4 mm (C₂ = 4, K_B = 15/12 = 1.25): σ_A = F[K_A·16D/(πd³) + 4/(πd²)] = 693.00610 MPa, τ_B = 381.97186 MPa; body length d(n + 1) = 42 mm; f_n = 140.33802 Hz"),
                [Q("d", 2, "mm"), Q("D", 16, "mm"), CalcInputValue.Of("n", 20), CalcInputValue.Choice("mode", Extension), CalcInputValue.Choice("corr", Bergstraesser), CalcInputValue.Choice("work", Loads), Q("F_0", 10, "N"), Q("F_1", 30, "N"), Q("F_2", 60, "N"), Q("r_h1", 8, "mm"), Q("r_h2", 4, "mm"), Q("G", 80, "GPa"), Q("rho", 7850, "kg/m^3"), Q("Rm", 1800, "MPa"), CalcInputValue.Of("f_tau", 0.45), CalcInputValue.Of("f_sigma", 0.75), CalcInputValue.Of("f_su", 0.67), Q("S_e", 400, "MPa"), CalcInputValue.Of("N_c", 1e7), CalcInputValue.Of("n_f_req", 1.2), Q("f_op", 10, "Hz")],
                [new CalcExpectedValue("k", 1.953125, "N/mm", 1e-9), new CalcExpectedValue("s_1", 10.24, "mm", 1e-9), new CalcExpectedValue("s_2", 25.6, "mm", 1e-9), new CalcExpectedValue("L_s", 42, "mm", 1e-9), new CalcExpectedValue("tau_0", 59.7105442, "MPa", 1e-7), new CalcExpectedValue("tau_2", 358.263265, "MPa", 1e-7), new CalcExpectedValue("sigma_A", 693.006095, "MPa", 1e-7), new CalcExpectedValue("tau_B", 381.971863, "MPa", 1e-7), new CalcExpectedValue("u_A", 0.513337848, "", 1e-7), new CalcExpectedValue("f_n", 140.338016, "Hz", 1e-7), new CalcExpectedValue("u_f", 0.446715079, "", 1e-7)]),
            new CalcTestCase("ka-torsion", "Torsion spring with 30 mm legs: rate, bending stress, wound diameter",
                new CalcReferenceSource("Analytic (closed-form; recomputed in python3)", null, "d 2 mm, D 16 mm, body n = 6, legs 30 + 30 mm, E 206 GPa: n_a = 6 + 60/(3π·16) = 6.3978874; k_φ = E d⁴/(64 D n_a) = 0.50309576 N·m/rad; C = 8, K_i = 247/224 = 1.1026786; M₁ 0.05 and M₂ 0.15 N·m: φ₁ 5.694321°, φ₂ 17.082964°; σ₂ = K_i·32·M/(π d³) = 210.59609 MPa; D_w = D·n/(n + φ/2π) = 15.874452 mm; body length d(n + 1) = 14 mm, wound 14.094905 mm; Goodman (S_e 500 MPa, bending, S_u = R_m): 1/n_f = 0.2183960"),
                [Q("d", 2, "mm"), Q("D", 16, "mm"), CalcInputValue.Of("n", 6), CalcInputValue.Choice("mode", Torsion), CalcInputValue.Choice("work", Loads), Q("l_1", 30, "mm"), Q("l_2", 30, "mm"), Q("M_1", 0.05, "N*m"), Q("M_2", 0.15, "N*m"), Q("E", 206, "GPa"), Q("Rm", 1800, "MPa"), CalcInputValue.Of("f_sigma", 0.75), Q("S_e", 500, "MPa"), CalcInputValue.Of("N_c", 1e7), CalcInputValue.Of("n_f_req", 1.2)],
                [new CalcExpectedValue("n_a", 6.39788736, "", 1e-7), new CalcExpectedValue("k_phi", 0.50309576, "N*m/rad", 1e-7), new CalcExpectedValue("K_i", 1.10267857, "", 1e-7), new CalcExpectedValue("phi_1", 5.69432145, "deg", 1e-7), new CalcExpectedValue("phi_2", 17.0829644, "deg", 1e-7), new CalcExpectedValue("sigma_2", 210.596094, "MPa", 1e-7), new CalcExpectedValue("D_w", 15.8744525, "mm", 1e-7), new CalcExpectedValue("L_w", 14.0949054, "mm", 1e-7), new CalcExpectedValue("u_2", 0.155997107, "", 1e-7), new CalcExpectedValue("u_f", 0.21839595, "", 1e-7)]),
        ],
        Category: "Springs",
        OutputPorts:
        [
            new CalcOutputPort("k", "N/mm", "Spring rate, compression and extension (spring.rate → M9)"),
            new CalcOutputPort("F_1", "N", "Force at working point 1 (spring.force_at → M9)"),
            new CalcOutputPort("F_2", "N", "Force at working point 2 (spring.force_at → M9)"),
            new CalcOutputPort("L_s", "mm", "Solid length (compression), closed body length (extension, torsion) (spring.solid_length)"),
            new CalcOutputPort("f_n", "Hz", "Surge (natural) frequency (spring.surge_freq)"),
        ],
        RecordKinds:
        [
            new CalcRecordKind(MaterialKind, "Material properties", ["G", "E", "rho"], "A material from the library: its properties fill these inputs, each citing the material, its revision and the page or table of the value."),
            new CalcRecordKind("spring.wire-strength", "Wire tensile strength R_m(d)", ["Rm"], "R_m at this wire diameter, from your own copy of the wire standard (for example the EN 10270 table) or the wire certificate. The table is not shipped."),
            new CalcRecordKind("spring.permissible", "Permissible and ultimate stress fractions", ["f_tau", "f_sigma", "f_su"], "Fractions of R_m from your design reference or the spring standard's permissible-stress diagrams (not shipped), with their source and edition."),
            new CalcRecordKind("spring.seating", "End-support coefficient", ["nu_seat"], "ν for the way the ends are held (0.5 both ends fixed and parallel, 0.7 one fixed and one pinned, 1 both pinned, 2 one end free), from your reference."),
            new CalcRecordKind("spring.endurance", "Endurance strength", ["S_e"], "Alternating endurance strength of this wire at the required life, from the wire maker or your fatigue reference (shear for compression and extension, bending for torsion)."),
        ]);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    public void Run(CalcRunContext c)
    {
        var mode = c.InputText("mode");

        foreach (var s in new[] { "d", "D", "n", "Rm" })
            c.Input(s);

        if (!(c.Si("D") > c.Si("d")))
        {
            c.Block("geometry", "D: the mean coil diameter must exceed the wire diameter d.");
            return;
        }

        c.Assign("C", "D / d", null, "Spring index", $"{Part1} / {Part2} / {Part3}");
        var index = c.Si("C");

        if (index < 4 || index > 20)
            c.Warn("index", $"D: the spring index C = {F(index)} is outside 4 … 20.");

        switch (mode)
        {
            case Extension:
                Extend(c);
                break;
            case Torsion:
                Twist(c);
                break;
            default:
                Compress(c);
                break;
        }
    }

    // ------------------------------------------------------------------
    // Compression
    // ------------------------------------------------------------------

    /// <summary>The shear stress correction factor k_c of the chosen kind, and the permissible shear stress (compression and extension).</summary>
    private static void ShearFactors(CalcRunContext c)
    {
        c.Input("f_tau");
        var corr = c.InputText("corr");

        var kc = corr switch
        {
            Wahl => "(4 * C - 1) / (4 * C - 4) + 0.615 / C",
            Uncorrected => "1",
            _ => "(C + 0.5) / (C - 0.75)",
        };

        c.Assign("k_c", kc, null, $"Shear stress correction factor ({corr})");
        c.Assign("tau_perm", "f_tau * Rm", "MPa", "Permissible shear stress (fraction of R_m, user input)");
    }

    /// <summary>Blocks, in the sheet's own words, when an input the chosen mode needs has been cleared (these inputs are optional on the template because a mode does not need them all). False when blocked.</summary>
    private static bool Require(CalcRunContext c, params string[] symbols)
    {
        foreach (var symbol in symbols)
        {
            if (c.Has(symbol))
                continue;

            c.Block("missing", $"{symbol}: enter '{c.Template.FindInput(symbol)?.Description ?? symbol}'; the {c.InputText("mode").ToLowerInvariant()} calculation needs it.");
            return false;
        }

        return true;
    }

    private static void Compress(CalcRunContext c)
    {
        if (!Require(c, "L0"))
            return;

        foreach (var s in new[] { "G", "E", "rho", "L0", "nu_seat" })
            c.Input(s);

        ShearFactors(c);

        if (!(c.Si("E") > c.Si("G")))
        {
            c.Block("geometry", "E: Young's modulus must exceed the shear modulus G.");
            return;
        }

        var (en, es, pa, pb) = EndFactors(c.InputText("ends"));
        c.Assign("k", "G * d^4 / (8 * D^3 * n)", "N/mm", "Spring rate", $"{Part1}");
        c.Assign("n_t", $"n + {en}", null, "Total coils (derived: end coils added to the active coils)");
        c.Assign("L_s", $"d * (n_t + {es})", "mm", "Solid length (derived from the end type)", $"{Part1}");

        if (!(c.Si("L0") > c.Si("L_s")))
        {
            c.Block("solid", $"L0: the free length must exceed the solid length L_s = {F(c.Si("L_s") * 1e3)} mm.");
            return;
        }

        c.Assign("p", $"(L0 - {pa} * d) / (n + {pb})", "mm", "Pitch of the free spring (derived)");
        c.Assign("s_c", "L0 - L_s", "mm", "Travel to solid");
        c.Assign("F_c", "k * s_c", "N", "Force at solid length");

        if (!WorkingPoints(c, initialTension: false))
            return;

        c.Assign("tau_1", "k_c * 8 * F_1 * D / (pi * d^3)", "MPa", "Shear stress at working point 1", Part1);
        c.Assign("tau_2", "k_c * 8 * F_2 * D / (pi * d^3)", "MPa", "Shear stress at working point 2", Part1);
        c.Assign("tau_c", "k_c * 8 * F_c * D / (pi * d^3)", "MPa", "Shear stress at solid length", Part1);
        c.Assign("u_2", "tau_2 / tau_perm", null, "Utilisation at working point 2");
        c.Assign("u_c", "tau_c / tau_perm", null, "Utilisation at solid length");
        c.Assign("L_1", "L0 - s_1", "mm", "Spring length at working point 1");
        c.Assign("L_2", "L0 - s_2", "mm", "Spring length at working point 2");

        Buckling(c);

        foreach (var (symbol, text) in new[] { ("C", "Spring index"), ("k", "Rate"), ("k_c", "Stress correction factor"), ("n_t", "Total coils"), ("L_s", "Solid length"), ("p", "Pitch"), ("F_1", "Force at point 1"), ("F_2", "Force at point 2"), ("s_1", "Deflection at point 1"), ("s_2", "Deflection at point 2"), ("L_1", "Length at point 1"), ("L_2", "Length at point 2"), ("s_c", "Travel to solid"), ("F_c", "Force at solid"), ("tau_1", "Stress at point 1"), ("tau_2", "Stress at point 2"), ("tau_c", "Stress at solid"), ("u_2", "Utilisation at point 2"), ("u_c", "Utilisation at solid") })
            c.Output(symbol, text);

        BucklingOutputs(c);
        Surge(c);
        Fatigue(c, "tau_1", "tau_2", "S_su", shear: true);

        c.CheckUtilisation("Static shear stress at solid length", "u_c", Part1);
        c.CheckCondition("Working deflection within the travel to solid", c.Si("s_2") <= c.Si("s_c") * (1 + 1e-9), $"s_2 = {F(c.Si("s_2") * 1e3)} mm", $"s_c = {F(c.Si("s_c") * 1e3)} mm", "s_2", Part1);
    }

    /// <summary>The end-type factors: coils added to n, the solid-length term, and the free-length terms of the pitch (derived from the geometry of the end).</summary>
    public static (int En, int Es, int Pa, int Pb) EndFactors(string ends) => ends switch
    {
        Plain => (0, 1, 1, 0),
        PlainGround => (1, 0, 0, 1),
        Squared => (2, 1, 3, 0),
        _ => (2, 0, 2, 0),
    };

    private static void Buckling(CalcRunContext c)
    {
        var e = c.Si("E");
        var g = c.Si("G");
        var lambda = c.Si("nu_seat") * c.Si("L0") / c.Si("D");
        c.Record("lambda_eff", "ν·L₀/D", $"{F(c.Si("nu_seat"))} × {F(c.Si("L0") * 1e3)} / {F(c.Si("D") * 1e3)}", new ScalarValue(lambda), "Effective slenderness", Part1);
        var c1 = e / (2 * (e - g));
        var c2 = 2 * Math.PI * Math.PI * (e - g) / (2 * g + e);
        c.Record("C1p", "E/(2(E − G))", $"{F(e / 1e9)} / (2 × ({F(e / 1e9)} − {F(g / 1e9)}))", new ScalarValue(c1), "Buckling constant C₁′", "Shigley 10th ed. Ch. 10, buckling");
        c.Record("C2p", "2π²(E − G)/(2G + E)", $"2π² × ({F(e / 1e9)} − {F(g / 1e9)}) / (2 × {F(g / 1e9)} + {F(e / 1e9)})", new ScalarValue(c2), "Buckling constant C₂′", "Shigley 10th ed. Ch. 10, buckling");

        if (c2 / (lambda * lambda) <= 1)
        {
            var ycr = c.Si("L0") * c1 * (1 - Math.Sqrt(1 - c2 / (lambda * lambda)));
            c.Record("y_cr", "L₀·C₁′·[1 − √(1 − C₂′/λ_eff²)]", $"{F(c.Si("L0") * 1e3)} × {F(c1)} × [1 − √(1 − {F(c2)}/{F(lambda * lambda)})]", ScalarValue.Of(ycr * 1e3, "mm"), "Buckling deflection", "Shigley 10th ed. Ch. 10, buckling");
            c.Assign("u_b", "s_2 / y_cr", null, "Deflection at point 2 against the buckling deflection");
        }
        else
        {
            c.Record("u_b", "stable at any deflection (λ_eff² < C₂′)", $"{F(lambda * lambda)} < {F(c2)}", new ScalarValue(0), "Deflection at point 2 against the buckling deflection (none: stable)");
        }
    }

    private static void BucklingOutputs(CalcRunContext c)
    {
        var stable = c.Session.Find("y_cr") is null;
        var ub = c.Si("u_b");

        if (!stable)
            c.Output("y_cr", "Buckling deflection");
        else
            c.Output("y_cr", "Buckling deflection", new TextValue("— (stable at any deflection)"));

        c.Output("u_b", "Deflection at point 2 / buckling deflection");

        if (ub < 1)
        {
            c.Output("buckling", "Buckling", new TextValue("PASS"));
            return;
        }

        c.Output("buckling", "Buckling", new TextValue("WARN"));
        c.Warn("buckling", $"nu_seat: the deflection at working point 2 ({F(c.Si("s_2") * 1e3)} mm) reaches the buckling deflection ({F(c.Si("y_cr") * 1e3)} mm); guide the spring or change the end support.");
    }

    // ------------------------------------------------------------------
    // Extension
    // ------------------------------------------------------------------

    private static void Extend(CalcRunContext c)
    {
        if (!Require(c, "F_0", "r_h1", "r_h2", "f_sigma"))
            return;

        foreach (var s in new[] { "G", "rho", "F_0", "r_h1", "r_h2", "f_sigma" })
            c.Input(s);

        ShearFactors(c);

        c.Assign("k", "G * d^4 / (8 * D^3 * n)", "N/mm", "Spring rate", Part2);
        c.Assign("L_s", "d * (n + 1)", "mm", "Body length, coils closed (derived)", Part2);

        if (c.Si("r_h1") <= c.Si("d") / 2 || c.Si("r_h2") <= c.Si("d") / 2)
        {
            c.Block("hook", "r_h1: the hook radii r_h1 and r_h2 must exceed half the wire diameter.");
            return;
        }

        if (!WorkingPoints(c, initialTension: true))
            return;

        c.Assign("tau_0", "k_c * 8 * F_0 * D / (pi * d^3)", "MPa", "Shear stress from the initial tension", Part2);
        c.Assign("tau_1", "k_c * 8 * F_1 * D / (pi * d^3)", "MPa", "Shear stress at working point 1", Part2);
        c.Assign("tau_2", "k_c * 8 * F_2 * D / (pi * d^3)", "MPa", "Shear stress at working point 2", Part2);
        c.Assign("u_2", "tau_2 / tau_perm", null, "Body utilisation at working point 2");
        c.Assign("L_1", "L_s + s_1", "mm", "Body length at working point 1 (hooks excluded)");
        c.Assign("L_2", "L_s + s_2", "mm", "Body length at working point 2 (hooks excluded)");
        c.Assign("C_1", "2 * r_h1 / d", null, "Hook index C₁");
        c.Assign("C_2", "2 * r_h2 / d", null, "Side-bend index C₂");
        c.Assign("K_A", "(4 * C_1^2 - C_1 - 1) / (4 * C_1 * (C_1 - 1))", null, "Hook bending curvature factor", "Shigley 10th ed. Ch. 10, hooks");
        c.Assign("K_B", "(4 * C_2 - 1) / (4 * C_2 - 4)", null, "Side-bend torsion curvature factor", "Shigley 10th ed. Ch. 10, hooks");
        c.Assign("sigma_A", "F_2 * (K_A * 16 * D / (pi * d^3) + 4 / (pi * d^2))", "MPa", "Hook bending stress at working point 2", Part2);
        c.Assign("tau_B", "K_B * 8 * F_2 * D / (pi * d^3)", "MPa", "Hook side-bend torsion stress at working point 2", Part2);
        c.Assign("sigma_perm", "f_sigma * Rm", "MPa", "Permissible bending stress (fraction of R_m, user input)");
        c.Assign("u_A", "sigma_A / sigma_perm", null, "Hook bending utilisation");
        c.Assign("u_B", "tau_B / tau_perm", null, "Side-bend torsion utilisation");

        foreach (var (symbol, text) in new[] { ("C", "Spring index"), ("k", "Rate"), ("k_c", "Stress correction factor"), ("L_s", "Body length, coils closed"), ("F_1", "Force at point 1"), ("F_2", "Force at point 2"), ("s_1", "Extension at point 1"), ("s_2", "Extension at point 2"), ("L_1", "Body length at point 1"), ("L_2", "Body length at point 2"), ("tau_0", "Stress from initial tension"), ("tau_1", "Stress at point 1"), ("tau_2", "Stress at point 2"), ("u_2", "Body utilisation at point 2"), ("K_A", "Hook bending factor"), ("K_B", "Side-bend factor"), ("sigma_A", "Hook bending stress"), ("tau_B", "Side-bend torsion stress"), ("u_A", "Hook bending utilisation"), ("u_B", "Side-bend utilisation") })
            c.Output(symbol, text);

        Surge(c);
        Fatigue(c, "tau_1", "tau_2", "S_su", shear: true);

        c.CheckUtilisation("Static shear stress at working point 2", "u_2", Part2);
        c.CheckUtilisation("Hook bending stress", "u_A", Part2);
        c.CheckUtilisation("Hook side-bend torsion stress", "u_B", Part2);
    }

    // ------------------------------------------------------------------
    // Torsion
    // ------------------------------------------------------------------

    private static void Twist(CalcRunContext c)
    {
        var angles = c.InputText("work") == Deflections;

        if (!Require(c, "f_sigma", "l_1", "l_2", angles ? "phi_1" : "M_1", angles ? "phi_2" : "M_2"))
            return;

        foreach (var s in new[] { "E", "l_1", "l_2", "f_sigma" })
            c.Input(s);

        c.Assign("n_a", "n + (l_1 + l_2) / (3 * pi * D)", null, "Active coils including the legs", Part3);
        c.Assign("k_phi", "E * d^4 / (64 * D * n_a)", "N*m/rad", "Torsional rate", Part3);
        c.Assign("L_s", "d * (n + 1)", "mm", "Body length, unloaded, coils closed (derived)", Part3);
        c.Assign("K_i", "(4 * C^2 - C - 1) / (4 * C * (C - 1))", null, "Inner-fibre curvature factor", "Shigley 10th ed. Ch. 10, torsion springs");
        c.Assign("sigma_perm", "f_sigma * Rm", "MPa", "Permissible bending stress (fraction of R_m, user input)");

        var work = c.InputText("work");

        if (work == Deflections)
        {
            c.Input("phi_1");
            c.Input("phi_2");

            if (c.Si("phi_2") < c.Si("phi_1"))
            {
                c.Block("sequence", "phi_2: working point 2 must not be below working point 1.");
                return;
            }

            c.Assign("M_1", "k_phi * phi_1", "N*m", "Torque at working point 1", Part3);
            c.Assign("M_2", "k_phi * phi_2", "N*m", "Torque at working point 2", Part3);
        }
        else
        {
            c.Input("M_1");
            c.Input("M_2");

            if (c.Si("M_2") < c.Si("M_1"))
            {
                c.Block("sequence", "M_2: working point 2 must not be below working point 1.");
                return;
            }

            c.Assign("phi_1", "M_1 / k_phi", "deg", "Angle at working point 1", Part3);
            c.Assign("phi_2", "M_2 / k_phi", "deg", "Angle at working point 2", Part3);
        }

        c.Assign("sigma_1", "K_i * 32 * M_1 / (pi * d^3)", "MPa", "Bending stress at working point 1", Part3);
        c.Assign("sigma_2", "K_i * 32 * M_2 / (pi * d^3)", "MPa", "Bending stress at working point 2", Part3);
        c.Assign("u_2", "sigma_2 / sigma_perm", null, "Utilisation at working point 2");
        c.Assign("D_w", "D * n / (n + phi_2 / (2 * pi))", "mm", "Mean coil diameter wound to point 2 (wire length constant)");
        c.Assign("L_w", "d * (n + 1 + phi_2 / (2 * pi))", "mm", "Body length wound to point 2 (coils closed)");

        foreach (var (symbol, text) in new[] { ("C", "Spring index"), ("n_a", "Active coils with legs"), ("k_phi", "Torsional rate"), ("K_i", "Curvature factor"), ("L_s", "Body length, unloaded"), ("phi_1", "Angle at point 1"), ("phi_2", "Angle at point 2"), ("M_1", "Torque at point 1"), ("M_2", "Torque at point 2"), ("sigma_1", "Stress at point 1"), ("sigma_2", "Stress at point 2"), ("u_2", "Utilisation at point 2"), ("D_w", "Wound coil diameter"), ("L_w", "Wound body length") })
            c.Output(symbol, text);

        Fatigue(c, "sigma_1", "sigma_2", "Rm", shear: false);
        c.CheckUtilisation("Static bending stress at working point 2", "u_2", Part3);
    }

    // ------------------------------------------------------------------
    // Shared
    // ------------------------------------------------------------------

    /// <summary>The working points as forces (loads) or deflections; compression and extension. False when the input is blocked.</summary>
    private static bool WorkingPoints(CalcRunContext c, bool initialTension)
    {
        var lead = initialTension ? "F_0 + " : string.Empty;
        var byDeflection = c.InputText("work") == Deflections;

        if (!(byDeflection ? Require(c, "s_1", "s_2") : Require(c, "F_1", "F_2")))
            return false;

        if (byDeflection)
        {
            c.Input("s_1");
            c.Input("s_2");

            if (c.Si("s_2") < c.Si("s_1"))
            {
                c.Block("sequence", "s_2: working point 2 must not be below working point 1.");
                return false;
            }

            c.Assign("F_1", $"{lead}k * s_1", "N", "Force at working point 1");
            c.Assign("F_2", $"{lead}k * s_2", "N", "Force at working point 2");
            return true;
        }

        c.Input("F_1");
        c.Input("F_2");

        if (c.Si("F_2") < c.Si("F_1"))
        {
            c.Block("sequence", "F_2: working point 2 must not be below working point 1.");
            return false;
        }

        if (initialTension && c.Si("F_1") < c.Si("F_0"))
        {
            c.Block("sequence", "F_1: the first force must reach the initial tension F_0; below it the spring does not extend.");
            return false;
        }

        c.Assign("s_1", initialTension ? "(F_1 - F_0) / k" : "F_1 / k", "mm", "Deflection at working point 1");
        c.Assign("s_2", initialTension ? "(F_2 - F_0) / k" : "F_2 / k", "mm", "Deflection at working point 2");
        return true;
    }

    private static void Surge(CalcRunContext c)
    {
        c.Assign("f_n", "d / (2 * pi * n * D^2) * sqrt(G / (2 * rho))", "Hz", "Surge (natural) frequency, both ends fixed", $"{Part1} / {Part2}");
        c.Output("f_n", "Surge frequency");

        if (!c.Has("f_op") || c.InputSi("f_op") <= 0)
            return;

        c.Assign("r_f", "f_n / f_op", null, "Surge frequency / operating frequency");
        c.Output("r_f", "Surge ratio f_n/f_op");

        if (c.Si("r_f") < SurgeRatioLimit)
            c.Warn("surge", $"f_op: the surge frequency is {F(c.Si("r_f"))} times the operating frequency, below {SurgeRatioLimit:0}; the spring may resonate.");
    }

    /// <summary>The Goodman check on the stresses at the two working points; skipped, with a warning, when no endurance strength is entered.</summary>
    private static void Fatigue(CalcRunContext c, string low, string high, string ultimate, bool shear)
    {
        if (!c.Has("S_e"))
        {
            c.Warn("fatigue-skipped", "S_e: no endurance strength entered; the Goodman fatigue check is not made.");
            return;
        }

        foreach (var s in shear ? new[] { "S_e", "N_c", "n_f_req", "f_su" } : ["S_e", "N_c", "n_f_req"])
            c.Input(s);

        if (c.Si("N_c") < 1e4)
            c.Warn("fatigue-cycles", $"N_c: the required life is {F(c.Si("N_c"))} cycles; the Goodman relation is for high-cycle fatigue, so the static check governs.");

        var (amplitude, mean, kind) = shear ? ("tau_a", "tau_m", "shear") : ("sigma_a", "sigma_m", "bending");
        c.Assign(amplitude, $"({high} - {low}) / 2", "MPa", $"Alternating {kind} stress");
        c.Assign(mean, $"({high} + {low}) / 2", "MPa", $"Mean {kind} stress");

        if (shear)
            c.Assign("S_su", "f_su * Rm", "MPa", "Ultimate shear strength (fraction of R_m, user input)");

        var ultimateSi = c.Si(ultimate);
        var u = FatigueRelations.GoodmanUtilisation(c.Si(amplitude), c.Si(mean), c.Si("S_e"), ultimateSi);
        var nf = u > 1e-12 ? Math.Min(1 / u, 1e9) : 1e9;
        c.Record("u_f", $"{amplitude}/S_e + {mean}/{ultimate}", $"{F(c.Si(amplitude) / 1e6)} / {F(c.Si("S_e") / 1e6)} + {F(c.Si(mean) / 1e6)} / {F(ultimateSi / 1e6)}", new ScalarValue(u), "Goodman utilisation (1/n_f)", "Goodman, as M4 (Shigley 10th ed. Ch. 6)");
        c.Record("n_f", "1/u_f", $"1 / {F(u)}", new ScalarValue(nf), "Goodman safety factor");
        c.Assign("u_fat", "n_f_req * u_f", null, "Fatigue utilisation against the required factor");
        c.Output("u_f", "Goodman utilisation");
        c.Output("n_f", "Goodman safety factor");
        c.CheckUtilisation("Goodman fatigue against the required safety factor", "u_fat", "Goodman, as M4");
    }

    private static string F(double v) => double.IsFinite(v) ? CalcValue.FormatNumber(v, 5) : "—";
}
