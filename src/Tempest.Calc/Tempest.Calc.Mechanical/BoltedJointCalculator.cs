using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

/// <summary>M3a — single bolted joint by a simplified VDI 2230 approach: preload from torque (nut factor or friction), scatter, bolt and clamped-member stiffness (frusta), load factor, embedding, residual clamp and separation, thread stripping length, yield under preload + working load, fatigue amplitude.</summary>
public sealed class BoltedJointCalculator : ICalcCalculator
{
    public const string Id = "mech.bolted-joint";

    public CalcTemplate Template { get; } = new(
        Id, "1.2.1", CalcDiscipline.Mechanical, "Bolted joint (simplified VDI 2230)",
        "One bolt clamping members under an axial working load: the preload from the tightening torque with its scatter (or as measured), the stiffness of bolt and members, how much of the working load the bolt sees, the residual clamp after embedding, separation, yield under preload plus working load, the thread engagement needed against stripping, and the fatigue amplitude.",
        "Tensile stress area of the thread: A_s = (π/4)·((d_2 + d_3)/2)², with d_2 = d − 0.6495 P and d_3 = d − 1.2269 P.\nAssembly preload from the tightening torque, nut-factor method: F_M = M_A/(K d).\nAssembly preload from the tightening torque, thread and head friction method: F_M = M_A/(0.16 P + 0.58 d_2 μ_G + 0.5 D_Km μ_K).\nAssembly preload stated directly (measured) instead of derived: F_M = F_V.\nThread torsion under preload: τ_M = M_G/W_p with M_G = F_M (0.16 P + 0.58 d_2 μ_G), W_p = π/16·d_3³ at the minor diameter (conservative: the VDI 2230 mean diameter d_S = (d_2 + d_3)/2 gives a lower τ_M).\nBolt and member stiffness and the stress area may also be given (measured, FE or a fastener record) instead of derived.\nMinimum preload after the tightening scatter: F_M,min = F_M/α_A.\nBolt resilience, the head, free shank and engaged thread as springs in series: δ_S = (0.4 d + 0.4 d)/(E_S A_N) + l_k/(E_S A_s) + 0.5 d/(E_S A_d3).\nMember stiffness, the clamped parts as two pressure frusta: k = 0.5774 π E d / ln[((1.155 t + d_w − d_h)(d_w + d_h))/((1.155 t + d_w + d_h)(d_w − d_h))].\nLoad factor, the share of the working load the bolt sees: Φ = n k_S/(k_S + k_P).\nPreload lost to embedding (settling of the interfaces): F_Z = f_Z (k_S k_P)/(k_S + k_P).\nResidual clamp at the interface under the working load: F_KR = max(F_M,min − (1 − Φ) F_A − F_Z, 0).\nMaximum bolt load, the whole working load once the joint opens: F_S,max = max(F_M + Φ F_A, F_A).\nWorking equivalent stress in the bolt, tension with half the tightening torsion: σ_red = √(σ_z² + 3 (0.5 τ)²).\nFatigue stress amplitude in the thread: σ_a = (F_S,max − F_M)/(2 A_s).\nThread engagement length needed against stripping: m_min = S F_S,max/(0.5 π d τ_B).",
        [
            new CalcInputSpec("d", "Nominal thread diameter", CalcInputKind.Quantity, "mm", Default: 12, Min: 3, Max: 64, HardMin: 0.5, HardMax: 500, Group: "Bolt"),
            new CalcInputSpec("P", "Thread pitch", CalcInputKind.Quantity, "mm", Default: 1.75, Min: 0.35, Max: 6, HardMin: 0.05, HardMax: 50, Group: "Bolt"),
            new CalcInputSpec("R_p02", "Bolt 0.2 % proof (yield) strength", CalcInputKind.Quantity, "MPa", ReferenceLibrary: "Fasteners", ReferenceProperty: "YieldStrength", ReferenceDimension: "Pressure", Default: 640, Min: 200, Max: 1200, HardMin: 1, HardMax: 1e4, Group: "Bolt"),
            new CalcInputSpec("E_S", "Bolt Young's modulus", CalcInputKind.Quantity, "GPa", Default: 205, Min: 60, Max: 250, HardMin: 1, HardMax: 2000, Group: "Bolt"),
            new CalcInputSpec("sigma_A", "Bolt endurance limit (stress amplitude)", CalcInputKind.Quantity, "MPa", Default: 50, Min: 10, Max: 200, HardMin: 0.1, HardMax: 1e4, Group: "Bolt"),
            new CalcInputSpec("l_k", "Clamp length", CalcInputKind.Quantity, "mm", Default: 40, Min: 1, Max: 2000, HardMin: 0.1, HardMax: 1e5, Group: "Joint"),
            new CalcInputSpec("d_h", "Hole diameter", CalcInputKind.Quantity, "mm", Default: 13, Min: 1, Max: 100, HardMin: 0.1, HardMax: 1000, Group: "Joint"),
            new CalcInputSpec("d_w", "Head / washer bearing diameter", CalcInputKind.Quantity, "mm", Default: 18, Min: 2, Max: 200, HardMin: 0.1, HardMax: 1000, Group: "Joint"),
            new CalcInputSpec("E_P", "Clamped members' Young's modulus", CalcInputKind.Quantity, "GPa", ReferenceLibrary: "Materials", ReferenceProperty: "YoungsModulus", ReferenceDimension: "Pressure", Default: 205, Min: 5, Max: 250, HardMin: 1, HardMax: 2000, Group: "Joint"),
            new CalcInputSpec("n", "Load introduction factor (0 = at the clamped faces, 1 = under the head)", CalcInputKind.Number, Default: 0.5, Min: 0, Max: 1, HardMin: 0, HardMax: 1, Group: "Joint"),
            new CalcInputSpec("f_Z", "Embedding (settling) loss", CalcInputKind.Quantity, "µm", Default: 11, Min: 0, Max: 100, HardMin: 0, HardMax: 1e4, Group: "Joint"),
            new CalcInputSpec("method", "Preload method", CalcInputKind.Choice, Choices: ["nut factor", "friction", GivenPreload], DefaultChoice: "friction", Group: "Tightening"),
            new CalcInputSpec("F_V", "Preload (given-preload method: measured, or from a tightening specification)", CalcInputKind.Quantity, "kN", Min: 0.1, Max: 5000, HardMin: 1e-6, HardMax: 1e6, IsOptional: true, Group: "Tightening"),
            new CalcInputSpec("M_A", "Tightening torque", CalcInputKind.Quantity, "N*m", Default: 80, Min: 0.1, Max: 10000, HardMin: 0, HardMax: 1e6, Group: "Tightening"),
            new CalcInputSpec("K", "Nut factor (nut-factor method); F_M is the maximum preload, so enter the smallest value the joint can have", CalcInputKind.Number, Default: 0.2, Min: 0.1, Max: 0.35, HardMin: 0.01, HardMax: 2, Group: "Tightening"),
            new CalcInputSpec("mu_G", "Thread friction coefficient; F_M is the maximum preload, so enter the smallest value the joint can have", CalcInputKind.Number, Default: 0.12, Min: 0.06, Max: 0.25, HardMin: 0.001, HardMax: 1, Group: "Tightening"),
            new CalcInputSpec("mu_K", "Head / nut bearing friction coefficient; F_M is the maximum preload, so enter the smallest value the joint can have", CalcInputKind.Number, Default: 0.12, Min: 0.06, Max: 0.25, HardMin: 0.001, HardMax: 1, Group: "Tightening"),
            new CalcInputSpec("nu_perm", "Permitted assembly utilisation ν: the equivalent stress at assembly over the proof strength (VDI 2230 R7; 0.9 is the usual target, 1.0 is the limit at which the bolt yields in tightening)", CalcInputKind.Number, Default: 1, Min: 0.5, Max: 1, HardMin: 0.1, HardMax: 1, Group: "Tightening"),
            new CalcInputSpec("alpha_A", "Tightening scatter factor (F_M,max / F_M,min)", CalcInputKind.Number, Default: 1.6, Min: 1, Max: 4, HardMin: 1, HardMax: 10, Group: "Tightening"),
            new CalcInputSpec("F_A", "Axial working load (per bolt)", CalcInputKind.Quantity, "kN", Default: 10, Min: 0, Max: 5000, HardMin: 0, HardMax: 1e6, Group: "Loads"),
            new CalcInputSpec("F_KR_req", "Required residual clamp force", CalcInputKind.Quantity, "kN", Default: 5, Min: 0, Max: 5000, HardMin: 0, HardMax: 1e6, Group: "Loads"),
            new CalcInputSpec("tau_B", "Nut / internal-thread material shear strength", CalcInputKind.Quantity, "MPa", Default: 400, Min: 50, Max: 1200, HardMin: 1, HardMax: 1e4, Group: "Thread"),
            new CalcInputSpec("m_eff", "Thread engagement length provided", CalcInputKind.Quantity, "mm", Default: 12, Min: 1, Max: 200, HardMin: 0.1, HardMax: 1e4, Group: "Thread"),
            new CalcInputSpec("S_strip", "Safety factor on stripping", CalcInputKind.Number, Default: 1.25, Min: 1, Max: 3, HardMin: 0.5, HardMax: 10, Group: "Thread"),
            new CalcInputSpec("A_s_given", "Tensile stress area, from a fastener record (blank = from d and P)", CalcInputKind.Quantity, "mm^2", ReferenceLibrary: "Fasteners", ReferenceProperty: "StressArea", ReferenceDimension: "Area", Min: 1, Max: 5000, HardMin: 1e-3, HardMax: 1e6, IsOptional: true, Group: "Given properties"),
            new CalcInputSpec("k_S_given", "Bolt stiffness, measured or from an FE model (blank = from the geometry)", CalcInputKind.Quantity, "kN/mm", Min: 1, Max: 1e5, HardMin: 1e-6, HardMax: 1e8, IsOptional: true, Group: "Given properties"),
            new CalcInputSpec("k_P_given", "Clamped members' stiffness, measured or from an FE model (blank = two frusta)", CalcInputKind.Quantity, "kN/mm", Min: 1, Max: 1e6, HardMin: 1e-6, HardMax: 1e8, IsOptional: true, Group: "Given properties"),
        ],
        [
            new CalcAssumption("The joint is concentrically clamped and concentrically loaded; the members behave as two 30° frusta in series.", "Eccentric clamping and loading (VDI 2230 full procedure) are outside this sheet."),
            new CalcAssumption("Thread torsion is taken at 50 % in the equivalent stress for the working state.", "Common VDI 2230 practice (k_τ = 0.5)."),
            new CalcAssumption("The thread torsion modulus is taken at the minor diameter d_3, W_p = π/16·d_3³, which is conservative: with the VDI 2230 mean diameter d_S the torsion stress is about 14 % lower and ν_M about 5 % lower at the defaults.", "Stated choice; the equivalent stress at assembly is the more conservative for it."),
            new CalcAssumption("F_M is the maximum preload (the one the tightening torque gives with the smallest friction and nut factor); F_M,min = F_M/α_A is the minimum after the scatter.", "VDI 2230 R2: the maximum preload comes from the minimum friction coefficients."),
            new CalcAssumption("The working load is a pulsating 0…F_A load; the fatigue amplitude is Φ F_A / (2 A_s).", "Compare with the bolt's endurance limit as entered."),
            new CalcAssumption("Thread stripping uses a shear area of half the pitch per unit length on the nominal diameter.", "A conservative simplification of ISO 898-2 / VDI 2230 §5.5.5."),
        ],
        [
            new CalcClauseRef("VDI 2230 Part 1:2015", "§5.1–§5.5", "Systematic calculation of highly stressed bolted joints — calculation steps R0–R13"),
            new CalcClauseRef("ISO 898-1:2013", "§9.1.6", "Tensile stress area of the thread"),
            new CalcClauseRef("Shigley's Mechanical Engineering Design", "§8-5, §8-7", "Joint stiffness (frusta); preload and torque"),
        ],
        [
            new CalcApplicabilityRule("separation", "The residual clamp force is below zero: the joint opens under the working load.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("geometry", "The hole must be larger than the thread and smaller than the bearing diameter.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("no-preload", "The given-preload method needs F_V.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("preload-yield", "Preload alone takes the bolt above 90 % of its proof strength.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
        ],
        [
            new CalcTestCase("ka-m12-88-nut-factor", "M12×1.75 class 8.8, 93 N·m, K = 0.2",
                new CalcReferenceSource("Analytic (ISO 898-1 §9.1.6 stress area; nut-factor relation F = T/(K d))", null, "A_s = 84.27 mm²; F_M = 93/(0.2 × 0.012) = 38.75 kN; σ_M = 459.8 MPa"),
                [CalcInputValue.Quantity("d", 12, "mm"), CalcInputValue.Quantity("P", 1.75, "mm"), CalcInputValue.Quantity("R_p02", 640, "MPa"), CalcInputValue.Quantity("E_S", 205, "GPa"), CalcInputValue.Quantity("sigma_A", 50, "MPa"), CalcInputValue.Quantity("l_k", 40, "mm"), CalcInputValue.Quantity("d_h", 13, "mm"), CalcInputValue.Quantity("d_w", 18, "mm"), CalcInputValue.Quantity("E_P", 205, "GPa"), CalcInputValue.Of("n", 0.5), CalcInputValue.Quantity("f_Z", 11, "µm"), CalcInputValue.Choice("method", "nut factor"), CalcInputValue.Quantity("M_A", 93, "N*m"), CalcInputValue.Of("K", 0.2), CalcInputValue.Of("mu_G", 0.12), CalcInputValue.Of("mu_K", 0.12), CalcInputValue.Of("alpha_A", 1.6), CalcInputValue.Quantity("F_A", 10, "kN"), CalcInputValue.Quantity("F_KR_req", 5, "kN"), CalcInputValue.Quantity("tau_B", 400, "MPa"), CalcInputValue.Quantity("m_eff", 12, "mm"), CalcInputValue.Of("S_strip", 1.25)],
                [new CalcExpectedValue("A_s", 84.27, "mm^2", 1e-3), new CalcExpectedValue("F_M", 38.75, "kN", 1e-4), new CalcExpectedValue("sigma_M", 459.8, "MPa", 2e-3)]),
            new CalcTestCase("ka-shigley-stiffness", "Shigley-style joint stiffness: M12 in 40 mm steel, d_w = 18 mm",
                new CalcReferenceSource("Shigley's Mechanical Engineering Design", "9th ed.", "Eq. 8-20 (frustum stiffness) and eq. 8-17 (bolt resilience), re-derived analytically for this joint: k_f = 0.5774π·205000·12/ln(3.220) = 3.816e6 N/mm, k_P = 1.908e6 kN/m; k_S = 3.212e5 kN/m; Φ = 0.5·k_S/(k_S + k_P) = 0.0720; F_M = 80/(0.28 + 0.756 + 0.93) mm = 40.69 kN"),
                [CalcInputValue.Quantity("d", 12, "mm"), CalcInputValue.Quantity("P", 1.75, "mm"), CalcInputValue.Quantity("R_p02", 640, "MPa"), CalcInputValue.Quantity("E_S", 205, "GPa"), CalcInputValue.Quantity("sigma_A", 50, "MPa"), CalcInputValue.Quantity("l_k", 40, "mm"), CalcInputValue.Quantity("d_h", 13, "mm"), CalcInputValue.Quantity("d_w", 18, "mm"), CalcInputValue.Quantity("E_P", 205, "GPa"), CalcInputValue.Of("n", 0.5), CalcInputValue.Quantity("f_Z", 11, "µm"), CalcInputValue.Choice("method", "friction"), CalcInputValue.Quantity("M_A", 80, "N*m"), CalcInputValue.Of("K", 0.2), CalcInputValue.Of("mu_G", 0.12), CalcInputValue.Of("mu_K", 0.12), CalcInputValue.Of("alpha_A", 1.6), CalcInputValue.Quantity("F_A", 10, "kN"), CalcInputValue.Quantity("F_KR_req", 5, "kN"), CalcInputValue.Quantity("tau_B", 400, "MPa"), CalcInputValue.Quantity("m_eff", 12, "mm"), CalcInputValue.Of("S_strip", 1.25)],
                [new CalcExpectedValue("k_P", 1.9078e6, "kN/m", 2e-3), new CalcExpectedValue("k_S", 3.2119e5, "kN/m", 2e-3), new CalcExpectedValue("Phi", 0.07205, "", 3e-3), new CalcExpectedValue("F_M", 40.69, "kN", 2e-3)]),
            new CalcTestCase("ka-legacy-given-1", "Legacy vector 1: given preload 37.935 kN, k_b 200 / k_m 600 kN/mm, 20 kN working load", Legacy(1), Given(37.935, 20, 200, 600, 84.3, 600),
                [new CalcExpectedValue("Phi", 0.25, "", 5e-5), new CalcExpectedValue("F_S_max", 42.935, "kN", 5e-5), new CalcExpectedValue("F_KR", 22.935, "kN", 5e-5), new CalcExpectedValue("F_A_sep", 50.580, "kN", 5e-5), new CalcExpectedValue("n_0", 2.5290, "", 5e-5), new CalcExpectedValue("n_L", 2.5290, "", 5e-5), new CalcExpectedValue("sigma_z_max", 509.31, "MPa", 5e-5)]),
            new CalcTestCase("ka-legacy-given-2", "Legacy vector 2: given preload 90 kN, k_b 500 / k_m 2000 kN/mm, 40 kN working load", Legacy(2), Given(90, 40, 500, 2000, 157, 830),
                [new CalcExpectedValue("Phi", 0.20, "", 5e-5), new CalcExpectedValue("F_S_max", 98.000, "kN", 5e-5), new CalcExpectedValue("F_KR", 58.000, "kN", 5e-5), new CalcExpectedValue("F_A_sep", 112.50, "kN", 5e-5), new CalcExpectedValue("n_0", 2.8125, "", 5e-5), new CalcExpectedValue("n_L", 3.2578, "", 5e-5), new CalcExpectedValue("sigma_z_max", 624.20, "MPa", 5e-5)]),
            new CalcTestCase("ka-legacy-given-separates", "Legacy vector 3: the joint separates; the bolt carries the whole 20 kN", Legacy(3), Given(10, 20, 3, 7, 84.3, 600),
                [new CalcExpectedValue("Phi", 0.30, "", 5e-5), new CalcExpectedValue("F_S_max", 20.000, "kN", 5e-5), new CalcExpectedValue("F_KR", 0, "kN", 1e-9), new CalcExpectedValue("F_A_sep", 14.286, "kN", 5e-5), new CalcExpectedValue("n_0", 0.71429, "", 5e-5), new CalcExpectedValue("sigma_z_max", 237.25, "MPa", 5e-5)]),
        ],
        DiagramKey: "bolted-joint-preload",
        Category: "Connections");

    private static CalcReferenceSource Legacy(int n) =>
        new("TempestOS calc.bolted-joint-preload (v0.21.0) hand calculation and test vector", null, $"docs/engineering/calculations/calc.bolted-joint-preload.md, Example {n}");

    /// <summary>A legacy vector: preload, stiffnesses and stress area given; load introduced under the head (n = 1), no scatter or embedding.</summary>
    private static CalcInputValue[] Given(double preload, double load, double kS, double kP, double area, double proof) =>
    [
        CalcInputValue.Quantity("d", 12, "mm"), CalcInputValue.Quantity("P", 1.75, "mm"), CalcInputValue.Quantity("R_p02", proof, "MPa"), CalcInputValue.Quantity("E_S", 205, "GPa"), CalcInputValue.Quantity("sigma_A", 50, "MPa"),
        CalcInputValue.Quantity("l_k", 40, "mm"), CalcInputValue.Quantity("d_h", 13, "mm"), CalcInputValue.Quantity("d_w", 18, "mm"), CalcInputValue.Quantity("E_P", 205, "GPa"), CalcInputValue.Of("n", 1), CalcInputValue.Quantity("f_Z", 0, "µm"),
        CalcInputValue.Choice("method", GivenPreload), CalcInputValue.Quantity("F_V", preload, "kN"), CalcInputValue.Quantity("M_A", 80, "N*m"), CalcInputValue.Of("K", 0.2), CalcInputValue.Of("mu_G", 0.12), CalcInputValue.Of("mu_K", 0.12), CalcInputValue.Of("alpha_A", 1),
        CalcInputValue.Quantity("F_A", load, "kN"), CalcInputValue.Quantity("F_KR_req", 0, "kN"), CalcInputValue.Quantity("tau_B", 400, "MPa"), CalcInputValue.Quantity("m_eff", 12, "mm"), CalcInputValue.Of("S_strip", 1.25),
        CalcInputValue.Quantity("A_s_given", area, "mm^2"), CalcInputValue.Quantity("k_S_given", kS, "kN/mm"), CalcInputValue.Quantity("k_P_given", kP, "kN/mm"),
    ];

    /// <summary>The method that takes the preload as given (measured, or from a tightening specification) rather than from a torque.</summary>
    public const string GivenPreload = "given preload";

    public void Run(CalcRunContext c)
    {
        c.MarkUtilisation("nu_M", "u_assembly", "u_clamp", "u_fatigue", "u_strip", "u_yield");
        c.MarkAtLeast("n_0", "n_L");
        foreach (var s in new[] { "d", "P", "R_p02", "E_S", "sigma_A", "l_k", "d_h", "d_w", "E_P", "n", "f_Z", "M_A", "K", "mu_G", "mu_K", "nu_perm", "alpha_A", "F_A", "F_KR_req", "tau_B", "m_eff", "S_strip" })
            c.Input(s);

        var method = c.InputText("method");

        if (!(c.Si("d") < c.Si("d_h")) || !(c.Si("d_h") < c.Si("d_w")))
        {
            c.Block("geometry", "d_h: the hole must be larger than the thread diameter and smaller than the bearing diameter.");
            return;
        }

        c.Assign("d_2", "d - 0.6495 * P", "mm", "Pitch diameter", "ISO 898-1 §9.1.6");
        c.Assign("d_3", "d - 1.2269 * P", "mm", "Minor diameter", "ISO 898-1 §9.1.6");
        if (c.Has("A_s_given"))
        {
            c.Input("A_s_given");
            c.Assign("A_s", "A_s_given", "mm^2", "Tensile stress area (given)");
        }
        else
            c.Assign("A_s", "pi / 4 * ((d_2 + d_3) / 2)^2", "mm^2", "Tensile stress area", "ISO 898-1 §9.1.6");
        c.Assign("A_N", "pi / 4 * d^2", "mm^2", "Nominal shank area");
        c.Assign("A_d3", "pi / 4 * d_3^2", "mm^2", "Minor-diameter area");
        c.Assign("D_Km", "(d_w + d_h) / 2", "mm", "Mean bearing diameter under the head");

        if (method == GivenPreload)
        {
            if (!c.Has("F_V"))
            {
                c.Block("no-preload", "F_V: the given-preload method needs the preload.");
                return;
            }

            c.Input("F_V");
            c.Assign("F_M", "F_V", "kN", "Assembly preload (given)");
        }
        else if (method == "nut factor")
            c.Assign("F_M", "M_A / (K * d)", "kN", "Assembly preload (nut factor)", "Shigley eq. 8-27");
        else
            c.Assign("F_M", "M_A / (0.16 * P + 0.58 * d_2 * mu_G + 0.5 * D_Km * mu_K)", "kN", "Assembly preload (friction)", "VDI 2230 §5.4.3");

        c.Assign("F_M_min", "F_M / alpha_A", "kN", "Minimum preload after scatter", "VDI 2230 R2");
        c.Assign("sigma_M", "F_M / A_s", "MPa", "Preload stress");
        c.Assign("M_G", "F_M * (0.16 * P + 0.58 * d_2 * mu_G)", "N*m", "Thread torque under preload", "VDI 2230 §5.4.3");
        c.Assign("W_p", "pi / 16 * d_3^3", "mm^3", "Polar section modulus at the minor diameter");
        c.Assign("tau_M", "M_G / W_p", "MPa", "Thread torsional stress under preload");
        c.Assign("sigma_red_M", "sqrt(sigma_M^2 + 3 * tau_M^2)", "MPa", "Equivalent stress at assembly (full torsion)", "VDI 2230 R8");
        c.Assign("nu_M", "sigma_red_M / R_p02", null, "Utilisation of proof strength at assembly");

        c.Assign("u_assembly", "nu_M / nu_perm", null, "Assembly utilisation over the permitted ν");

        if (c.Si("nu_M") > 0.9)
            c.Warn("preload-yield", c.Si("nu_M") > 1.0
                ? $"M_A: the equivalent stress at assembly is {CalcValue.FormatNumber(c.Si("nu_M"), 4)} of the proof strength: the bolt yields in tightening."
                : "M_A: the preload alone takes the bolt above 90 % of its proof strength.");

        // Stiffness.
        if (c.Has("k_S_given"))
        {
            c.Input("k_S_given");
            c.Assign("k_S", "k_S_given", "kN/m", "Bolt stiffness (given)");
        }
        else
        {
            c.Assign("delta_S", "0.4 * d / (E_S * A_N) + l_k / (E_S * A_s) + 0.5 * d / (E_S * A_d3) + 0.4 * d / (E_S * A_N)", "m/kN", "Bolt resilience (head, shank, engaged thread, nut)", "VDI 2230 §5.1.1");
            c.Assign("k_S", "1 / delta_S", "kN/m", "Bolt stiffness");
        }

        if (c.Has("k_P_given"))
        {
            c.Input("k_P_given");
            c.Assign("k_P", "k_P_given", "kN/m", "Clamped members' stiffness (given)");
        }
        else
        {
            c.Assign("t_f", "l_k / 2", "mm", "Frustum height (two equal frusta)");
            c.Assign("k_f", "0.5774 * pi * E_P * d / ln(((1.155 * t_f + d_w - d_h) * (d_w + d_h)) / ((1.155 * t_f + d_w + d_h) * (d_w - d_h)))", "kN/m", "Stiffness of one frustum", "Shigley eq. 8-20");
            c.Assign("k_P", "k_f / 2", "kN/m", "Clamped members' stiffness (two frusta in series)", "Shigley eq. 8-21");
        }
        c.Assign("Phi", "n * k_S / (k_S + k_P)", null, "Load factor", "VDI 2230 R3");

        // Working state.
        c.Assign("F_Z", "f_Z * (k_S * k_P) / (k_S + k_P)", "kN", "Preload loss from embedding", "VDI 2230 R4");
        c.Assign("F_SA", "Phi * F_A", "kN", "Additional bolt load from the working load");
        c.Assign("F_PA", "(1 - Phi) * F_A", "kN", "Load relieved from the members");
        // Separation (Black/Green G-05, the legacy module's own branch): once the working load relieves the members of all
        // the preload, the clamp is zero, not negative, and the bolt carries the whole working load.
        var separated = c.Si("F_M_min") - c.Si("F_PA") - c.Si("F_Z") <= 0;
        c.Assign("F_KR", "max(F_M_min - F_PA - F_Z, 0 kN)", "kN", "Residual clamp force (minimum; zero once the joint opens)", "VDI 2230 R7");
        c.Assign("F_S_max", "max(F_M + F_SA, F_A)", "kN", "Maximum bolt load (maximum preload + additional load; the whole working load once the joint opens)");
        c.Assign("sigma_z_max", "F_S_max / A_s", "MPa", "Maximum bolt tensile stress");
        c.Assign("sigma_red_B", "sqrt(sigma_z_max^2 + 3 * (0.5 * tau_M)^2)", "MPa", "Equivalent stress in the working state (50 % torsion)", "VDI 2230 R8");
        c.Assign("u_yield", "sigma_red_B / R_p02", null, "Yield utilisation in the working state");
        c.Assign("sigma_a", "(F_S_max - F_M) / (2 * A_s)", "MPa", "Fatigue stress amplitude (pulsating 0…F_A load)", "VDI 2230 R9");
        c.Assign("u_fatigue", "sigma_a / sigma_A", null, "Fatigue utilisation");
        c.Assign("m_min", "S_strip * F_S_max / (0.5 * pi * d * tau_B)", "mm", "Thread engagement needed against stripping", "VDI 2230 §5.5.5");
        c.Assign("u_strip", "m_min / m_eff", null, "Engagement utilisation");

        // Joint-diagram load factors (carried from the legacy calc.bolted-joint-preload module, ADR-0167): how far the working
        // load can grow before the members separate, and before the bolt's axial stress reaches the proof strength.
        c.Assign("F_A_sep", "(F_M_min - F_Z) / (1 - Phi)", "kN", "Working load at which the members separate (minimum preload, after embedding)", "VDI 2230 R7");
        c.Assign("F_proof", "R_p02 * A_s", "kN", "Bolt load at the proof (yield) strength");

        if (c.Si("F_A") > 0)
        {
            c.Assign("n_0", "F_A_sep / F_A", null, "Load factor against separation (F_A,sep / F_A)");
            c.Output("n_0", "Load factor against separation");

            if (c.Si("F_proof") > c.Si("F_M"))
            {
                // The bolt load is max(F_M + Φ·F_A, F_A): it reaches the proof load on the linear line, or, once the joint has
                // opened, when the working load itself does — whichever comes first (D-42: the legacy n_L ignored separation).
                c.Assign("F_A_yield", c.Si("Phi") > 0 ? "min((F_proof - F_M) / Phi, F_proof)" : "F_proof", "kN", "Working load at which the bolt reaches its proof load (separation included)");
                c.Assign("n_L", "F_A_yield / F_A", null, "Load factor against axial yield of the bolt, torsion excluded");
                c.Output("n_L", "Load factor against axial yield");
            }
        }

        foreach (var (s, t) in new[] { ("A_s", "Tensile stress area"), ("F_M", "Assembly preload (maximum)"), ("F_M_min", "Minimum preload"), ("sigma_M", "Preload stress"), ("nu_M", "Assembly utilisation of proof strength"), ("u_assembly", "Assembly utilisation over the permitted ν"), ("k_S", "Bolt stiffness"), ("k_P", "Members' stiffness"), ("Phi", "Load factor"), ("F_Z", "Embedding loss"), ("F_SA", "Additional bolt load"), ("F_KR", "Residual clamp"), ("F_A_sep", "Separation load"), ("F_S_max", "Maximum bolt load"), ("sigma_z_max", "Maximum bolt tensile stress"), ("sigma_red_B", "Working equivalent stress"), ("sigma_a", "Fatigue amplitude"), ("m_min", "Engagement needed") })
            c.Output(s, t);

        if (separated)
        {
            c.Warn("separation", "F_KR: the residual clamp force is not positive; the joint separates under the working load and the bolt carries all of it.");
            c.CheckCondition("No separation", false, c.Scalar("F_KR").Format(), "> 0 kN", "F_KR", "VDI 2230 R7");
            c.CheckCondition("Residual clamp against requirement", !(c.Si("F_KR_req") > 0), c.Scalar("F_KR").Format(), $"≥ {c.Scalar("F_KR_req").Format()}", "F_KR", "VDI 2230 R7");
        }
        else
        {
            c.Assign("u_clamp", "F_KR_req / F_KR", null, "Required over available residual clamp");
            c.CheckCondition("No separation", true, c.Scalar("F_KR").Format(), "> 0 kN", "F_KR", "VDI 2230 R7");
            c.CheckUtilisation("Residual clamp against requirement", "u_clamp", "VDI 2230 R7");
        }

        c.CheckUtilisation("Equivalent stress at assembly within the permitted ν of the proof strength", "u_assembly", "VDI 2230 R7");
        c.CheckUtilisation("Yield in the working state", "u_yield", "VDI 2230 R8");
        c.CheckUtilisation("Fatigue amplitude", "u_fatigue", "VDI 2230 R9");
        c.CheckUtilisation("Thread stripping", "u_strip", "VDI 2230 §5.5.5");
    }
}
