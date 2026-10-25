using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

/// <summary>
/// M8 — plastic snap-fits: the cantilever lug (constant section, tapered thickness, tapered width), the annular (closed ring) snap
/// and the torsion bar. The strain at the critical section against the permissible strain of the plastic, the deflection force by beam
/// theory with the secant modulus, and the assembly and separation forces of the inclined faces with Coulomb friction. The geometry
/// factors of the tapers are derived from Euler–Bernoulli integration (<see cref="SnapFitGeometry"/>). Es(ε), the permissible strains
/// and ν are properties of a W3 material (revision-pinned); the friction coefficient and the annular geometry factor are the user's
/// records with provenance. No plastics data are shipped (Addendum B §B5), and none has a default.
/// </summary>
public sealed class SnapFitCalculator : ICalcCalculator
{
    public const string Id = "mech.snap-fit";

    public const string Constant = "Cantilever, constant section";
    public const string TaperedThickness = "Cantilever, tapered thickness";
    public const string TaperedWidth = "Cantilever, tapered width";
    public const string Annular = "Annular (closed ring)";
    public const string Torsion = "Torsion bar";

    public const string EsTyped = "Typed secant modulus";
    public const string EsCurve = "W3 secant curve Es(ε)";

    public const string Once = "One-time (assembled once or rarely)";
    public const string Repeated = "Repeated (assembled and separated often)";

    /// <summary>The record kind of a W3 material (<c>MaterialLibrary.Kind</c>; written out here because the mechanical assembly does not reference the library, and a test keeps the two equal).</summary>
    public const string MaterialKind = "material.properties";

    /// <summary>The property key of the secant-modulus points in a W3 material (<c>MaterialProperties.SecantCurve</c>; written out here, and a test keeps the two equal).</summary>
    public const string SecantCurveKey = "Es_curve";

    /// <summary>The slenderness l/h below which beam theory is no longer a good model and the sheet warns.</summary>
    public const double SlendernessLimit = 5;

    /// <summary>The bar twist above which the small-angle relation φ = y/a is no longer good (about 20°).</summary>
    public const double SmallAngleLimit = 0.35;

    private const string Ref = "Covestro, Snap-Fit Joints for Plastics: A Design Guide";

    private static readonly IReadOnlyList<CalcColumnSpec> SecantColumns =
    [
        new("eps", "Strain", CalcInputKind.Number, string.Empty, Min: 0, Max: 0.5, HardMin: 0, HardMax: 1),
        new("Es", "Secant modulus", CalcInputKind.Quantity, "MPa", Min: 1, Max: 20000, HardMin: 0.001, HardMax: 1e6),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.0.0", CalcDiscipline.Mechanical, "Snap-fits: cantilever, annular, torsion",
        "A plastic snap-fit joint of one of five forms: a cantilever lug of constant section, of tapered thickness or of tapered width, an annular (closed ring) snap, or a torsion bar. The strain at the critical section against the permissible strain of the plastic (one-time or repeated joining), the deflection force with the secant modulus, the assembly force (inclined entry face) and the separation force (return face) with Coulomb friction, and whether the joint is self-locking.",
        "Cantilever geometry (Euler–Bernoulli): y = K_y·ε·l²/h with K_y = 2/3 (constant section; the design guides print the rounded 0.67); a linear taper of the thickness to r_h = h_tip/h gives K_y = 2·J/φ_max with J = [2r_h − r_h²/2 − ln r_h − 3/2]/(1 − r_h)³ and φ_max = 1 for r_h ≥ 1/2 (otherwise the strain peaks inside the beam at t = (2a − 1)/a, a = 1 − r_h, with φ_max = (1 − t)/(1 − a·t)²); a linear taper of the width to r_b gives K_y = 2·J with J = [1/2 − 2r_b + 3r_b²/2 − r_b²·ln r_b]/(1 − r_b)³\n" +
        "Strain and force of the cantilever: ε = y·h/(K_y·l²); P = b·h²·Es·ε/(6·l·φ_max) = (b·h²/6)·(Es·ε/l) at the root of a constant section; Es is the secant modulus at ε (typed, or interpolated from the W3 points Es(ε))\n" +
        "Annular: ε = K_ann·ΔD/D (the ring's mean diameter D widened by the diametral interference ΔD, K_ann the user's geometry factor); the radial contact force summed round the ring is P = 2π·Es·ε·h·b (hoop force Es·ε·h·b over the radius)\n" +
        "Torsion bar of radius r and length l with a lever of length a deflected y: φ = y/a, γ = φ·r/l, equivalent tensile strain ε = γ/(1 + ν), γ_perm = ε_perm·(1 + ν); G = Es/(2(1 + ν)), T = G·(π r⁴/2)·φ/l, P = T/a\n" +
        "Joining forces: W = P·(μ + tan α)/(1 − μ·tan α) on the entry face (angle α to the joining direction) and W′ = P·(μ + tan α′)/(1 − μ·tan α′) on the return face (α′); the joint is self-locking, with an infinite separation force, when α′ ≥ arctan(1/μ); assembly jams when μ·tan α ≥ 1\n" +
        "Utilisation u = ε/ε_perm (FAIL above 1); permissible deflection y_perm = K_y·ε_perm·l²/h (annular ΔD_perm = ε_perm·D/K_ann; torsion y_perm = γ_perm·l·a/r)",
        [
            new CalcInputSpec("mode", "Form of snap-fit", CalcInputKind.Choice, Choices: [Constant, TaperedThickness, TaperedWidth, Annular, Torsion], DefaultChoice: Constant, Group: "Joint"),
            new CalcInputSpec("l", "Length of the beam (cantilever) or of the torsion bar", CalcInputKind.Quantity, "mm", Default: 15, Min: 2, Max: 100, HardMin: 0.1, HardMax: 1000, Group: "Geometry"),
            new CalcInputSpec("h", "Thickness at the root (cantilever); wall thickness of the ring (annular)", CalcInputKind.Quantity, "mm", Default: 2, Min: 0.3, Max: 10, HardMin: 0.01, HardMax: 100, Group: "Geometry"),
            new CalcInputSpec("b", "Width at the root (cantilever); axial length of the ring (annular)", CalcInputKind.Quantity, "mm", Default: 6, Min: 1, Max: 50, HardMin: 0.01, HardMax: 1000, Group: "Geometry"),
            new CalcInputSpec("h_ratio", "Tip thickness / root thickness (tapered thickness)", CalcInputKind.Number, Default: 0.5, Min: 0.25, Max: 1, HardMin: 0.05, HardMax: 1, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("b_ratio", "Tip width / root width (tapered width)", CalcInputKind.Number, Default: 0.5, Min: 0.1, Max: 1, HardMin: 0.02, HardMax: 1, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("y", "Deflection at the tip (cantilever) or at the end of the lever (torsion): the undercut to be cleared", CalcInputKind.Quantity, "mm", Default: 1, Min: 0.05, Max: 20, HardMin: 0, HardMax: 1000, Group: "Geometry"),
            new CalcInputSpec("D_ring", "Mean diameter of the ring (annular)", CalcInputKind.Quantity, "mm", Default: 30, Min: 5, Max: 500, HardMin: 0.5, HardMax: 5000, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("dD", "Interference on the diameter: how much the ring is widened to pass the undercut (annular)", CalcInputKind.Quantity, "mm", Default: 0.4, Min: 0, Max: 5, HardMin: 0, HardMax: 100, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("K_ann", "Geometry factor of the annular snap on the hoop strain (record snapfit.annular-factor; 1 for the thin closed ring)", CalcInputKind.Number, Default: 1, Min: 0.5, Max: 3, HardMin: 0.1, HardMax: 10, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("r_bar", "Radius of the torsion bar", CalcInputKind.Quantity, "mm", Default: 1.5, Min: 0.3, Max: 10, HardMin: 0.01, HardMax: 100, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("a_arm", "Length of the lever on the torsion bar", CalcInputKind.Quantity, "mm", Default: 8, Min: 2, Max: 100, HardMin: 0.1, HardMax: 1000, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("alpha", "Entry (assembly) angle α of the inclined face to the joining direction", CalcInputKind.Quantity, "deg", Default: 30, Min: 5, Max: 60, HardMin: 0, HardMax: 89, Group: "Joining"),
            new CalcInputSpec("alpha_r", "Return (separation) angle α′ of the retaining face (90° is a face square to the joining direction)", CalcInputKind.Quantity, "deg", Default: 60, Min: 10, Max: 90, HardMin: 0, HardMax: 90, Group: "Joining"),
            new CalcInputSpec("mu", "Coefficient of friction between the two parts (record snapfit.friction; no default: the pair of materials, from your reference or a W3 friction pair)", CalcInputKind.Number, Min: 0.05, Max: 1, HardMin: 0, HardMax: 2, Group: "Joining"),
            new CalcInputSpec("Es_source", "Where the secant modulus comes from: typed at the design strain, or interpolated from the Es(ε) points of a W3 material", CalcInputKind.Choice, Choices: [EsTyped, EsCurve], DefaultChoice: EsTyped, Group: "Material"),
            new CalcInputSpec("Es", "Secant modulus Es at the design strain and temperature (typed; no default: the plastic's stress–strain data)", CalcInputKind.Quantity, "MPa", Min: 10, Max: 20000, HardMin: 0.01, HardMax: 1e6, IsOptional: true, Group: "Material"),
            new CalcInputSpec("Es_pts", "Secant modulus against strain, Es(ε), of the plastic at the temperature of use (from the W3 material)", CalcInputKind.Table, Columns: SecantColumns, MaxRows: 50, IsOptional: true, Group: "Material", MaterialProperty: SecantCurveKey),
            new CalcInputSpec("loading", "How often the joint is made: selects the one-time or the repeated permissible strain", CalcInputKind.Choice, Choices: [Once, Repeated], DefaultChoice: Once, Group: "Material"),
            new CalcInputSpec("eps_perm_once", "Permissible strain for one-time joining, as a fraction (0.04 is 4 %) at the temperature of use (no default: from the plastic's data)", CalcInputKind.Number, Min: 0.001, Max: 0.3, HardMin: 1e-6, HardMax: 1, IsOptional: true, Group: "Material", MaterialProperty: "eps_perm_once"),
            new CalcInputSpec("eps_perm_repeated", "Permissible strain for repeated joining, as a fraction (no default)", CalcInputKind.Number, Min: 0.001, Max: 0.3, HardMin: 1e-6, HardMax: 1, IsOptional: true, Group: "Material", MaterialProperty: "eps_perm_repeated"),
            new CalcInputSpec("nu", "Poisson's ratio ν (torsion bar)", CalcInputKind.Number, Min: 0.2, Max: 0.49, HardMin: -0.99, HardMax: 0.499, IsOptional: true, Group: "Material", MaterialProperty: "nu"),
            new CalcInputSpec("T_use", "Temperature of use. Recorded: Es and the permissible strain must be the values at this temperature", CalcInputKind.Quantity, "degC", Default: 23, Min: -40, Max: 120, HardMin: -150, HardMax: 400, Group: "Material"),
        ],
        [
            new CalcAssumption("Beam theory (Euler–Bernoulli) for the cantilever: a rigid root, a linear taper, a constant secant modulus Es at the design strain over the whole beam, small deflection. The strain is the nominal surface strain at the critical section, with no local concentration from the root fillet; the geometry factors are derived by integration (the 0.67 of the design guides is the constant-section 2/3 rounded).", "Euler–Bernoulli beam theory; the guide's closed forms to be checked against the owner's copy."),
            new CalcAssumption("Es and the permissible strain are properties of the plastic at the temperature of use, with the loading (one-time or repeated) and the moisture state of the part. No values are supplied: they come from the plastic maker's data or a W3 material, and the temperature entered records what they were taken at.", "Addendum B §B5: no plastics data are shipped."),
            new CalcAssumption("Annular snap: a thin closed ring whose mean-diameter strain is the diametral interference over the diameter, times the user's geometry factor (record snapfit.annular-factor; 1 for the plain thin ring); the contact force is the hoop force Es·ε·h·b taken round the ring. The factor, if the ring is stiffened or slotted, is the user's.", "Derived from ring geometry; to be checked against the owner's copy of the design guide."),
            new CalcAssumption("Torsion bar: a round bar of radius r loaded at the end of a lever, the angle small (φ = y/a); the permissible shear strain is ε_perm·(1 + ν) and the equivalent tensile strain γ/(1 + ν); G = Es/(2(1 + ν)) with the secant modulus at that strain.", "Design-guide relation γ_perm ≈ ε_perm·(1 + ν); to be checked against the owner's copy."),
            new CalcAssumption("Assembly and separation: a rigid mating part, Coulomb friction μ on the inclined face (α for assembly, α′ for separation), the deflection force P taken at the design deflection; no creep or stress relaxation of the joint in service, no impact (speed) effect.", "Friction on an inclined plane; the friction coefficient is the user's record."),
        ],
        [
            new CalcClauseRef(Ref, "Section B", "Cantilever, annular and torsion snap-fit relations"),
            new CalcClauseRef("Euler–Bernoulli beam theory", "—", "Tip deflection and surface strain of a tapered cantilever, by integration"),
        ],
        [
            new CalcApplicabilityRule("missing", "An input the chosen form, or the chosen source of the secant modulus or the permissible strain, needs is blank.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("es-curve", "The W3 secant-modulus points hold no usable row (a strain above zero with a positive modulus).", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("slender", "The beam is shorter than 5 times its thickness (l/h < 5): beam theory underestimates the stiffness and the strain is not reliable.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("es-range", "The design strain is outside the strains of the secant-modulus points: the end value is held, with no extrapolation.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("assembly-jam", "μ·tan α is 1 or more: the entry face jams and the part cannot be pushed on.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("self-locking", "The return angle α′ is at or above arctan(1/μ): the joint cannot be separated by pulling, however large the force.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("small-angle", "The torsion-bar twist is above about 20°: the small-angle relation φ = y/a is no longer good.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-constant", "Cantilever of constant section: strain, deflection force, assembly and separation forces",
                new CalcReferenceSource("Analytic (closed-form beam theory; recomputed in python3)", null, "l 15 mm, h 2 mm, b 6 mm, y 1 mm, Es 2400 MPa, ε_perm 0.04, μ 0.3, α 30°, α′ 60°: K_y = 2/3; ε = 3·h·y/(2·l²) = 1.33333 %; P = b·h²·Es·ε/(6·l) = 8.53333 N; u = 0.333333; y_perm = (2/3)·0.04·l²/h = 3 mm; W = P(μ + tan 30°)/(1 − μ·tan 30°) = 9.055114 N; arctan(1/μ) = 73.300756°; W′ = P(μ + tan 60°)/(1 − μ·tan 60°) = 36.096414 N"),
                [Q("l", 15, "mm"), Q("h", 2, "mm"), Q("b", 6, "mm"), Q("y", 1, "mm"), Q("Es", 2400, "MPa"), CalcInputValue.Of("eps_perm_once", 0.04), CalcInputValue.Of("mu", 0.3), Q("alpha", 30, "deg"), Q("alpha_r", 60, "deg")],
                [new CalcExpectedValue("K_y", 2.0 / 3.0, "", 1e-12), new CalcExpectedValue("eps", 1.33333333333, "%", 1e-9), new CalcExpectedValue("P", 8.53333333333, "N", 1e-9), new CalcExpectedValue("strain_util", 0.333333333333, "", 1e-9), new CalcExpectedValue("y_perm", 3, "mm", 1e-9), new CalcExpectedValue("assembly_force", 9.05511405892, "N", 1e-9), new CalcExpectedValue("alpha_lock", 73.3007557660, "deg", 1e-9), new CalcExpectedValue("retention_force", 36.0964135774, "N", 1e-9)]),
            new CalcTestCase("ka-taper-thickness", "Cantilever with the thickness tapered to half: the deflection factor K_y = 1.0904",
                new CalcReferenceSource("Analytic (Euler–Bernoulli integration; recomputed in python3 by closed form and by numerical double integration of the curvature)", null, "The geometry and loads of ka-constant with the tip thickness h/2: J = [2r − r²/2 − ln r − 3/2]/(1 − r)³ = 0.545177 at r = 0.5, the strain is largest at the root (φ_max = 1), K_y = 2J = 1.0903549; ε = y·h/(K_y·l²) = 0.815229 %; P = 5.217465 N; u = 0.203807; y_perm = 4.906597 mm; W = 5.536493 N; W′ = 22.070131 N"),
                [Q("l", 15, "mm"), Q("h", 2, "mm"), Q("b", 6, "mm"), CalcInputValue.Choice("mode", TaperedThickness), CalcInputValue.Of("h_ratio", 0.5), Q("y", 1, "mm"), Q("Es", 2400, "MPa"), CalcInputValue.Of("eps_perm_once", 0.04), CalcInputValue.Of("mu", 0.3), Q("alpha", 30, "deg"), Q("alpha_r", 60, "deg")],
                [new CalcExpectedValue("K_y", 1.09035488896, "", 1e-9), new CalcExpectedValue("phi_max", 1, "", 1e-12), new CalcExpectedValue("eps", 0.815228966174, "%", 1e-9), new CalcExpectedValue("P", 5.21746538331, "N", 1e-9), new CalcExpectedValue("strain_util", 0.203807241544, "", 1e-9), new CalcExpectedValue("y_perm", 4.90659700032, "mm", 1e-9), new CalcExpectedValue("assembly_force", 5.53649345463, "N", 1e-9), new CalcExpectedValue("retention_force", 22.0701314424, "N", 1e-9)]),
            new CalcTestCase("ka-taper-thickness-deep", "Cantilever with the thickness tapered to 0.3: the strain peaks inside the beam",
                new CalcReferenceSource("Analytic (Euler–Bernoulli integration; recomputed in python3 by closed form and by numerical double integration of the curvature)", null, "l 20 mm, h 2.5 mm, b 8 mm, tip thickness 0.3·h, y 1.2 mm, Es 2200 MPa, ε_perm 0.05, μ 0.25, α 25°, α′ 70°: a = 0.7, the strain shape (1 − t)/(1 − a·t)² peaks at t = (2a − 1)/a = 0.571429 with φ_max = 1.190476; J = 0.755023, K_y = 2J/φ_max = 1.2684382; ε = 0.591278 %; P = b·h²·Es·ε/(6·l·φ_max) = 4.552843 N; u = 0.118256; y_perm = 10.147506 mm; W = 3.691590 N; W′ = 43.582588 N"),
                [Q("l", 20, "mm"), Q("h", 2.5, "mm"), Q("b", 8, "mm"), CalcInputValue.Choice("mode", TaperedThickness), CalcInputValue.Of("h_ratio", 0.3), Q("y", 1.2, "mm"), Q("Es", 2200, "MPa"), CalcInputValue.Of("eps_perm_once", 0.05), CalcInputValue.Of("mu", 0.25), Q("alpha", 25, "deg"), Q("alpha_r", 70, "deg")],
                [new CalcExpectedValue("t_crit", 0.571428571429, "", 1e-9), new CalcExpectedValue("phi_max", 1.19047619048, "", 1e-9), new CalcExpectedValue("K_y", 1.26843822527, "", 1e-9), new CalcExpectedValue("eps", 0.591278301977, "%", 1e-9), new CalcExpectedValue("P", 4.55284292522, "N", 1e-9), new CalcExpectedValue("strain_util", 0.118255660395, "", 1e-9), new CalcExpectedValue("y_perm", 10.1475058022, "mm", 1e-9), new CalcExpectedValue("assembly_force", 3.69159048182, "N", 1e-9), new CalcExpectedValue("retention_force", 43.5825878892, "N", 1e-9)]),
            new CalcTestCase("ka-taper-width", "Cantilever with the width tapered to a quarter: K_y = 0.8552",
                new CalcReferenceSource("Analytic (Euler–Bernoulli integration; recomputed in python3 by closed form and by numerical double integration of the curvature)", null, "l 12 mm, h 1.5 mm, b 8 mm, tip width b/4, y 0.8 mm, Es 3000 MPa, ε_perm 0.03, μ 0.35, α 35°, α′ 45°: J = [1/2 − 2r + 3r²/2 − r²·ln r]/(1 − r)³ = 0.427599 at r = 0.25, K_y = 2J = 0.8551983; ε = 0.974433 %; P = b·h²·Es·ε/(6·l) = 7.308246 N; u = 0.324811; y_perm = 2.462971 mm; W = 10.166773 N; W′ = 15.178665 N"),
                [Q("l", 12, "mm"), Q("h", 1.5, "mm"), Q("b", 8, "mm"), CalcInputValue.Choice("mode", TaperedWidth), CalcInputValue.Of("b_ratio", 0.25), Q("y", 0.8, "mm"), Q("Es", 3000, "MPa"), CalcInputValue.Of("eps_perm_once", 0.03), CalcInputValue.Of("mu", 0.35), Q("alpha", 35, "deg"), Q("alpha_r", 45, "deg")],
                [new CalcExpectedValue("K_y", 0.855198329221, "", 1e-9), new CalcExpectedValue("eps", 0.974432836057, "%", 1e-9), new CalcExpectedValue("P", 7.30824627042, "N", 1e-9), new CalcExpectedValue("strain_util", 0.324810945350, "", 1e-9), new CalcExpectedValue("y_perm", 2.46297118816, "mm", 1e-9), new CalcExpectedValue("assembly_force", 10.1667732745, "N", 1e-9), new CalcExpectedValue("retention_force", 15.1786653309, "N", 1e-9)]),
            new CalcTestCase("ka-annular", "Annular snap: hoop strain from the interference, radial force round the ring",
                new CalcReferenceSource("Analytic (thin-ring geometry; recomputed in python3)", null, "D 30 mm, ΔD 0.4 mm, K_ann 1.2, h 1.5 mm, b 5 mm, Es 2400 MPa, ε_perm 0.04, μ 0.3, α 30°, α′ 60°: ε = K_ann·ΔD/D = 1.6 %; P = 2π·Es·ε·h·b = 1809.557 N; u = 0.4; ΔD_perm = ε_perm·D/K_ann = 1 mm; W = 1920.205 N; W′ = 7654.515 N"),
                [CalcInputValue.Choice("mode", Annular), Q("h", 1.5, "mm"), Q("b", 5, "mm"), Q("D_ring", 30, "mm"), Q("dD", 0.4, "mm"), CalcInputValue.Of("K_ann", 1.2), Q("Es", 2400, "MPa"), CalcInputValue.Of("eps_perm_once", 0.04), CalcInputValue.Of("mu", 0.3), Q("alpha", 30, "deg"), Q("alpha_r", 60, "deg")],
                [new CalcExpectedValue("eps", 1.6, "%", 1e-9), new CalcExpectedValue("P", 1809.55736847, "N", 1e-9), new CalcExpectedValue("strain_util", 0.4, "", 1e-9), new CalcExpectedValue("y_perm", 1, "mm", 1e-9), new CalcExpectedValue("assembly_force", 1920.20488683, "N", 1e-9), new CalcExpectedValue("retention_force", 7654.51537080, "N", 1e-9)]),
            new CalcTestCase("ka-torsion", "Torsion bar: shear strain, permissible shear strain, torque and lever force",
                new CalcReferenceSource("Analytic (closed-form; recomputed in python3)", null, "l 20 mm, r 2 mm, lever a 10 mm, y 1.5 mm, ν 0.38, Es 2000 MPa, ε_perm 0.03, μ 0.3, α 30°, α′ 60°: φ = y/a = 0.15 rad = 8.594367°; γ = φ·r/l = 0.015; ε = γ/(1 + ν) = 1.086957 %; γ_perm = ε_perm·(1 + ν) = 0.0414; u = 0.362319; G = Es/(2(1 + ν)) = 724.6377 MPa; T = G·(π r⁴/2)·φ/l = 0.136591 N·m; P = T/a = 13.659098 N; y_perm = γ_perm·l·a/r = 4.14 mm; W = 14.494300 N; W′ = 57.778649 N"),
                [CalcInputValue.Choice("mode", Torsion), Q("l", 20, "mm"), Q("r_bar", 2, "mm"), Q("a_arm", 10, "mm"), Q("y", 1.5, "mm"), CalcInputValue.Of("nu", 0.38), Q("Es", 2000, "MPa"), CalcInputValue.Of("eps_perm_once", 0.03), CalcInputValue.Of("mu", 0.3), Q("alpha", 30, "deg"), Q("alpha_r", 60, "deg")],
                [new CalcExpectedValue("phi", 8.59436692696, "deg", 1e-9), new CalcExpectedValue("gamma", 0.015, "", 1e-9), new CalcExpectedValue("eps", 1.08695652174, "%", 1e-9), new CalcExpectedValue("gamma_perm", 0.0414, "", 1e-9), new CalcExpectedValue("strain_util", 0.362318840580, "", 1e-9), new CalcExpectedValue("G_sec", 724.637681159, "MPa", 1e-9), new CalcExpectedValue("T_bar", 0.136590984939, "N*m", 1e-9), new CalcExpectedValue("P", 13.6590984939, "N", 1e-9), new CalcExpectedValue("y_perm", 4.14, "mm", 1e-9), new CalcExpectedValue("assembly_force", 14.4943001723, "N", 1e-9), new CalcExpectedValue("retention_force", 57.7786486322, "N", 1e-9)]),
            new CalcTestCase("ka-curve", "Cantilever with the secant modulus interpolated from W3 points Es(ε)",
                new CalcReferenceSource("Analytic (closed-form; recomputed in python3)", null, "l 15 mm, h 2 mm, b 6 mm, y 1.5 mm: ε = 2 %; points (0.5 %, 2600 MPa), (1 %, 2400 MPa), (3 %, 1800 MPa) give Es(2 %) = 2400 − 600·(0.01/0.02) = 2100 MPa; P = b·h²·Es·ε/(6·l) = 11.2 N; ε_perm 0.04: u = 0.5"),
                [Q("l", 15, "mm"), Q("h", 2, "mm"), Q("b", 6, "mm"), Q("y", 1.5, "mm"), CalcInputValue.Choice("Es_source", EsCurve), SecantPoints((0.005, 2600), (0.01, 2400), (0.03, 1800)), CalcInputValue.Of("eps_perm_once", 0.04), CalcInputValue.Of("mu", 0.3), Q("alpha", 30, "deg"), Q("alpha_r", 60, "deg")],
                [new CalcExpectedValue("eps", 2, "%", 1e-9), new CalcExpectedValue("Es", 2100, "MPa", 1e-9), new CalcExpectedValue("P", 11.2, "N", 1e-9), new CalcExpectedValue("strain_util", 0.5, "", 1e-9)]),
        ],
        Category: "Joining",
        OutputPorts:
        [
            new CalcOutputPort("assembly_force", "N", "Force to push the parts together over the entry face (snap.assembly_force)"),
            new CalcOutputPort("retention_force", "N", "Force to separate the parts over the return face (snap.retention_force); reads \"self-locking\" and has no number when the return angle locks"),
            new CalcOutputPort("strain_util", "", "Strain at the critical section / permissible strain (snap.strain_util)"),
        ],
        RecordKinds:
        [
            new CalcRecordKind(MaterialKind, "Material properties", ["Es_pts", "eps_perm_once", "eps_perm_repeated", "nu"], "A plastic from the library: its permissible strains, its Poisson ratio and its secant-modulus points Es(ε) fill these inputs, each citing the material, its revision and the page or table of the value. Use the values for the temperature of use."),
            new CalcRecordKind("snapfit.friction", "Coefficient of friction", ["mu"], "μ for the pair of materials and surfaces of this joint (static or kinetic as the use needs), from your reference, the plastic maker or a test, with its source. A W3 material can hold friction coefficients by pair; read μ from there and enter it here."),
            new CalcRecordKind("snapfit.annular-factor", "Annular snap geometry factor", ["K_ann"], "The factor on the hoop strain ΔD/D of the annular snap-fit (1 for a plain thin closed ring), from your design reference for the ring shape, with its source."),
            new CalcRecordKind("snapfit.secant-modulus", "Secant modulus at the design strain", ["Es"], "Es read from the plastic's stress–strain curve at the design strain and the temperature of use (the temperature of use is the input T_use), with the source of the curve."),
        ]);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    /// <summary>The secant-modulus table from (strain, Es in MPa) points.</summary>
    public static CalcInputValue SecantPoints(params (double Strain, double EsMpa)[] points) =>
        CalcInputValue.Table("Es_pts", points.Select(p => CalcRow.Of(("eps", new CalcCell(p.Strain)), ("Es", new CalcCell(p.EsMpa, "MPa")))).ToList());

    public void Run(CalcRunContext c)
    {
        var mode = c.InputText("mode");

        foreach (var s in new[] { "mu", "alpha", "alpha_r", "T_use" })
            c.Input(s);

        c.Assume($"Es and the permissible strain are the values of the plastic at the temperature of use, {F(c.Si("T_use") - 273.15)} °C, as entered, in the moisture state of the part; the sheet cannot check this.", "Recorded temperature of use.");

        if (!Strain(c, mode))
            return;

        if (!Material(c))
            return;

        Force(c, mode);
        Joining(c);

        c.Output("fillet_advisory", "Root-fillet advisory", new TextValue(
            "The strain above is the nominal strain, with no local concentration. A sharp internal corner at the root (or at the end of the bar or ring) concentrates the strain and is the usual place for a snap-fit to break: give the root a generous fillet radius, of the order of the thickness, taper the section away from it, and apply a concentration factor from your own reference if the radius is small."));
    }

    // ------------------------------------------------------------------
    // Strain at the critical section
    // ------------------------------------------------------------------

    /// <summary>Blocks, in the sheet's own words, when an input the chosen form needs has been cleared. False when blocked.</summary>
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

    private static bool Strain(CalcRunContext c, string mode)
    {
        switch (mode)
        {
            case Annular:
                return Ring(c);
            case Torsion:
                return Bar(c);
            default:
                return Beam(c, mode);
        }
    }

    private static bool Beam(CalcRunContext c, string mode)
    {
        var ratioSymbol = mode == TaperedThickness ? "h_ratio" : mode == TaperedWidth ? "b_ratio" : null;

        if (ratioSymbol is not null && !Require(c, ratioSymbol))
            return false;

        foreach (var s in new[] { "l", "h", "b", "y" })
            c.Input(s);

        c.Assign("l_h", "l / h", null, "Slenderness of the beam l/h");

        if (c.Si("l_h") < SlendernessLimit)
            c.Warn("slender", $"l: the beam is {F(c.Si("l_h"))} times as long as it is thick (l/h < {SlendernessLimit:0}); beam theory is no longer reliable, so check the strain with a finite-element model or a test.");

        double ky;
        double phiMax = 1;
        double tCrit = 0;

        if (mode == TaperedThickness)
        {
            c.Input("h_ratio");
            var r = c.InputSi("h_ratio");
            var j = SnapFitGeometry.ThicknessJ(r);
            (phiMax, tCrit) = SnapFitGeometry.ThicknessPhiMax(r);
            ky = 2 * j / phiMax;
            c.Record("J", "[2r − r²/2 − ln r − 3/2]/(1 − r)³", $"r = {F(r)}", new ScalarValue(j), "Deflection integral J of the thickness taper (derived: Euler–Bernoulli integration)", "Euler–Bernoulli beam theory");
            c.Record("phi_max", "(1 − t*)/(1 − a·t*)², t* = (2a − 1)/a for a > 1/2, else 1", $"a = {F(1 - r)}", new ScalarValue(phiMax), "Largest value of the strain shape along the beam (1 at the root)", "Euler–Bernoulli beam theory");

            if (tCrit > 0)
                c.Record("t_crit", "(2a − 1)/a", $"a = {F(1 - r)}", new ScalarValue(tCrit), "Where the strain peaks, as a fraction of the length from the root (the taper is steep enough for the maximum to lie inside the beam)", "Euler–Bernoulli beam theory");

            c.Record("K_y", "2·J/φ_max", $"2 × {F(j)} / {F(phiMax)}", new ScalarValue(ky), "Deflection factor: y = K_y·ε·l²/h", Ref);
        }
        else if (mode == TaperedWidth)
        {
            c.Input("b_ratio");
            var r = c.InputSi("b_ratio");
            var j = SnapFitGeometry.WidthJ(r);
            ky = 2 * j;
            c.Record("J", "[1/2 − 2r + 3r²/2 − r²·ln r]/(1 − r)³", $"r = {F(r)}", new ScalarValue(j), "Deflection integral J of the width taper (derived: Euler–Bernoulli integration)", "Euler–Bernoulli beam theory");
            c.Record("phi_max", "1 (the strain is largest at the root for a width taper)", "—", new ScalarValue(1), "Largest value of the strain shape along the beam", "Euler–Bernoulli beam theory");
            c.Record("K_y", "2·J", $"2 × {F(j)}", new ScalarValue(ky), "Deflection factor: y = K_y·ε·l²/h", Ref);
        }
        else
        {
            ky = SnapFitGeometry.ConstantKy;
            c.Record("phi_max", "1", "—", new ScalarValue(1), "Largest value of the strain shape along the beam (the root)", "Euler–Bernoulli beam theory");
            c.Record("K_y", "2/3", "constant section", new ScalarValue(ky), "Deflection factor: y = K_y·ε·l²/h (the design guides print the rounded 0.67)", Ref);
        }

        c.Assign("eps", "y * h / (K_y * l^2)", "%", "Maximum surface strain at the critical section (nominal)", Ref);
        return true;
    }

    private static bool Ring(CalcRunContext c)
    {
        if (!Require(c, "D_ring", "dD", "K_ann"))
            return false;

        foreach (var s in new[] { "h", "b", "D_ring", "dD", "K_ann" })
            c.Input(s);

        c.Assign("eps", "K_ann * dD / D_ring", "%", "Hoop strain of the ring (nominal)", Ref);
        return true;
    }

    private static bool Bar(CalcRunContext c)
    {
        if (!Require(c, "r_bar", "a_arm", "nu"))
            return false;

        foreach (var s in new[] { "l", "y", "r_bar", "a_arm", "nu" })
            c.Input(s);

        c.Assign("phi", "y / a_arm", "deg", "Twist of the bar (small angle: lever deflection / lever length)", Ref);

        if (c.Si("phi") > SmallAngleLimit)
            c.Warn("small-angle", $"y: the bar twists {F(c.Si("phi") * 180 / Math.PI)}°; the small-angle relation φ = y/a is no longer good above about 20°.");

        c.Assign("gamma", "phi * r_bar / l", null, "Shear strain at the surface of the bar", Ref);
        c.Assign("eps", "gamma / (1 + nu)", "%", "Equivalent tensile strain γ/(1 + ν)", Ref);
        return true;
    }

    // ------------------------------------------------------------------
    // Material: permissible strain and secant modulus
    // ------------------------------------------------------------------

    private static bool Material(CalcRunContext c)
    {
        var repeated = c.InputText("loading") == Repeated;
        var epsSymbol = repeated ? "eps_perm_repeated" : "eps_perm_once";

        if (!Require(c, epsSymbol))
            return false;

        c.Input(epsSymbol);
        c.Assign("eps_perm", epsSymbol, "%", $"Permissible strain ({(repeated ? "repeated" : "one-time")} joining, as entered)", Ref);

        if (c.InputText("Es_source") == EsCurve)
            return SecantFromCurve(c);

        if (!Require(c, "Es"))
            return false;

        c.Input("Es");
        return true;
    }

    private static bool SecantFromCurve(CalcRunContext c)
    {
        var points = new List<(double Strain, double Es)>();

        foreach (var row in c.Rows("Es_pts"))
        {
            if (row.Get("eps") is not { Number: { } strain } || row.Get("Es") is not { Number: { } modulus } cell)
                continue;

            var si = (cell.Unit is { Length: > 0 } unit ? UnitCatalog.Parse(unit) : UnitCatalog.Parse("MPa")).ToSi(modulus);

            if (double.IsFinite(strain) && double.IsFinite(si) && strain > 0 && si > 0)
                points.Add((strain, si));
        }

        if (points.Count == 0)
        {
            c.Block("es-curve", c.EnterRows("Es_pts").TrimEnd('.') + "; a row needs a strain above zero and a positive secant modulus.");
            return false;
        }

        points.Sort((a, b) => a.Strain.CompareTo(b.Strain));
        var eps = c.Si("eps");
        var es = Interpolate(points, eps, out var outside);

        if (outside)
            c.Warn("es-range", $"Es_pts: the design strain {F(eps * 100)} % is outside the strains of the points ({F(points[0].Strain * 100)} … {F(points[^1].Strain * 100)} %); the end value of the secant modulus is held, with no extrapolation.");

        c.Record("Es", "Es(ε) by linear interpolation of the W3 points", $"ε = {F(eps * 100)} % in {points.Count} point(s)", ScalarValue.Of(es / 1e6, "MPa"), "Secant modulus at the design strain (from the W3 points)", Ref);
        return true;
    }

    /// <summary>Es at <paramref name="strain"/> by linear interpolation of ascending points; the end value is held outside the points.</summary>
    public static double Interpolate(IReadOnlyList<(double Strain, double Es)> points, double strain, out bool outside)
    {
        outside = false;
        const double slack = 1e-9;

        if (strain < points[0].Strain * (1 - slack))
        {
            outside = true;
            return points[0].Es;
        }

        if (strain > points[^1].Strain * (1 + slack))
        {
            outside = true;
            return points[^1].Es;
        }

        if (strain <= points[0].Strain)
            return points[0].Es;

        if (strain >= points[^1].Strain)
            return points[^1].Es;

        for (var i = 1; i < points.Count; i++)
        {
            var (x0, y0) = points[i - 1];
            var (x1, y1) = points[i];

            if (strain <= x1 && x1 > x0)
                return y0 + (y1 - y0) * (strain - x0) / (x1 - x0);
        }

        return points[^1].Es;
    }

    // ------------------------------------------------------------------
    // Deflection force and strain check
    // ------------------------------------------------------------------

    private static void Force(CalcRunContext c, string mode)
    {
        switch (mode)
        {
            case Annular:
                c.Assign("P", "2 * pi * Es * eps * h * b", "N", "Radial contact force summed round the ring (hoop force Es·ε·h·b over the radius)", Ref);
                c.Assign("y_perm", "eps_perm * D_ring / K_ann", "mm", "Permissible interference on the diameter ΔD_perm", Ref);
                break;
            case Torsion:
                c.Assign("gamma_perm", "eps_perm * (1 + nu)", null, "Permissible shear strain γ_perm ≈ ε_perm·(1 + ν)", Ref);
                c.Assign("G_sec", "Es / (2 * (1 + nu))", "MPa", "Secant shear modulus Es/(2(1 + ν))");
                c.Assign("J_bar", "pi * r_bar^4 / 2", "mm^4", "Polar second moment of the bar");
                c.Assign("T_bar", "G_sec * J_bar * phi / l", "N*m", "Torque in the bar", Ref);
                c.Assign("P", "T_bar / a_arm", "N", "Force at the end of the lever", Ref);
                c.Assign("y_perm", "gamma_perm * l * a_arm / r_bar", "mm", "Permissible deflection at the end of the lever", Ref);
                break;
            default:
                c.Assign("P", "b * h^2 * Es * eps / (6 * l * phi_max)", "N", "Deflection force at the tip: P = (b·h²/6)·(Es·ε/l) at the root of a constant section", Ref);
                c.Assign("y_perm", "K_y * eps_perm * l^2 / h", "mm", "Permissible tip deflection", Ref);
                break;
        }

        c.Assign("strain_util", "eps / eps_perm", null, "Strain / permissible strain");
        c.Output("eps", "Maximum strain");
        c.Output("eps_perm", "Permissible strain");
        c.Output("strain_util", "Strain / permissible strain");
        c.Output("Es", "Secant modulus used");
        c.Output("P", "Deflection force");
        c.Output("y_perm", mode == Annular ? "Permissible interference on the diameter" : "Permissible deflection");

        if (mode is Constant or TaperedThickness or TaperedWidth)
        {
            c.Output("l_h", "Slenderness l/h");
            c.Output("K_y", "Deflection factor K_y");
            c.Output("phi_max", "Strain shape maximum");

            if (c.Session.Find("t_crit") is not null)
                c.Output("t_crit", "Strain peaks at (fraction of the length)");
        }
        else if (mode == Torsion)
        {
            c.Output("phi", "Twist of the bar");
            c.Output("gamma", "Shear strain");
            c.Output("gamma_perm", "Permissible shear strain");
            c.Output("G_sec", "Secant shear modulus");
            c.Output("T_bar", "Torque in the bar");
        }

        c.CheckUtilisation("Strain at the critical section against the permissible strain", "strain_util", Ref);
    }

    // ------------------------------------------------------------------
    // Assembly and separation
    // ------------------------------------------------------------------

    private static void Joining(CalcRunContext c)
    {
        var mu = c.Si("mu");
        var alpha = c.Si("alpha");
        var alphaR = c.Si("alpha_r");

        c.Assign("tan_a", "tan(alpha)", null, "tan α of the entry face");
        c.Assign("mu_tan_a", "mu * tan_a", null, "μ·tan α of the entry face");
        var jams = c.Si("mu_tan_a") >= 1 - 1e-9;
        c.CheckCondition("Assembly: the entry face does not jam (μ·tan α < 1)", !jams, $"μ·tan α = {F(c.Si("mu_tan_a"))}", "< 1", "mu_tan_a", Ref);

        if (jams)
        {
            c.Output("assembly_force", "Assembly force", new TextValue("jams (μ·tan α ≥ 1)"));
            c.Warn("assembly-jam", $"alpha: μ·tan α = {F(c.Si("mu_tan_a"))} is 1 or more; the entry face jams and the part cannot be pushed on. Reduce the entry angle α or the friction.");
        }
        else
        {
            c.Assign("assembly_force", "P * (mu + tan_a) / (1 - mu_tan_a)", "N", "Assembly force W = P·(μ + tan α)/(1 − μ·tan α)", Ref);
            c.Output("assembly_force", "Assembly force");
        }

        var limit = mu > 0 ? Math.Atan(1 / mu) : Math.PI / 2;
        c.Record("alpha_lock", "arctan(1/μ)", $"arctan(1/{F(mu)})", ScalarValue.Of(limit * 180 / Math.PI, "deg"), "Return angle above which the joint is self-locking (π/2 when μ = 0)", Ref);
        c.Output("alpha_lock", "Self-locking return angle");
        var locks = alphaR >= limit - 1e-12;

        if (locks)
        {
            c.Output("retention_force", "Separation force", new TextValue("self-locking (infinite)"));
            c.Output("self_locking", "Self-locking", new TextValue("YES: α′ ≥ arctan(1/μ); the parts cannot be separated by pulling"));
            c.Warn("self-locking", $"alpha_r: the return angle {F(alphaR * 180 / Math.PI)}° is at or above arctan(1/μ) = {F(limit * 180 / Math.PI)}°; the separation force is infinite and the joint cannot be undone by pulling. That is right for a permanent joint and wrong for a serviceable one.");
        }
        else
        {
            c.Assign("tan_ar", "tan(alpha_r)", null, "tan α′ of the return face");
            c.Assign("retention_force", "P * (mu + tan_ar) / (1 - mu * tan_ar)", "N", "Separation force W′ = P·(μ + tan α′)/(1 − μ·tan α′)", Ref);
            c.Output("retention_force", "Separation force");
            c.Output("self_locking", "Self-locking", new TextValue("no"));
        }
    }

    private static string F(double v) => double.IsFinite(v) ? CalcValue.FormatNumber(v, 5) : "—";
}
