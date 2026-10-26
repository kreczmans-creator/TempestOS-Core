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

    public const string BarOne = "Held at one end";
    public const string BarBoth = "Held at both ends, lever in the middle";

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

    /// <summary>The deflection over the length y/l above which the linear beam theory under-predicts the strain by about 4 % and the force by about 10 % (elastica of a fixed-direction tip load).</summary>
    public const double LargeDeflectionLimit = 0.3;

    /// <summary>The bar twist above which the small-angle relation φ = y/a is no longer good (about 20°).</summary>
    public const double SmallAngleLimit = 0.35;

    private const string Ref = "Covestro, Snap-Fit Joints for Plastics: A Design Guide";
    private const string Derived = "Euler–Bernoulli beam theory (derived)";
    private const string DerivedRing = "Derived: thin-ring hoop force";
    private const string DerivedBar = "Derived: torsion of a round bar";

    private static readonly IReadOnlyList<CalcColumnSpec> SecantColumns =
    [
        new("eps", "Strain", CalcInputKind.Number, string.Empty, Min: 0, Max: 0.5, HardMin: 0, HardMax: 1),
        new("Es", "Secant modulus", CalcInputKind.Quantity, "MPa", Min: 1, Max: 20000, HardMin: 0.001, HardMax: 1e6),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.3.2", CalcDiscipline.Mechanical, "Snap-fits",
        "A plastic snap-fit joint of one of five forms: a cantilever lug of constant section, of tapered thickness or of tapered width, an annular (closed ring) snap, or a torsion bar. The strain at the critical section against the permissible strain of the plastic (one-time or repeated joining), the deflection force with the secant modulus, the assembly force (inclined entry face) and the separation force (return face) with Coulomb friction, and whether the joint is self-locking.",
        "Cantilever geometry (Euler–Bernoulli): y = K_y·ε·l²/h with K_y = 2/3 (constant section; the design guides print the rounded 0.67); a linear taper of the thickness to r_h = h_tip/h gives K_y = 2·J/φ_max with J = [2r_h − r_h²/2 − ln r_h − 3/2]/(1 − r_h)³ and φ_max = 1 for r_h ≥ 1/2 (otherwise the strain peaks inside the beam at t = (2a − 1)/a, a = 1 − r_h, with φ_max = (1 − t)/(1 − a·t)²); a linear taper of the width to r_b gives K_y = 2·J with J = [1/2 − 2r_b + 3r_b²/2 − r_b²·ln r_b]/(1 − r_b)³\n" +
        "Strain and force of the cantilever: ε = y·h/(K_y·l²); P = b·h²·Es·ε/(6·l·φ_max) = (b·h²/6)·(Es·ε/l) at the root of a constant section; Es is the secant modulus at ε (typed, or interpolated from the W3 points Es(ε))\n" +
        "Annular (derived, free thin ring): nominal hoop strain ε_nom = ΔD/D (the mean diameter D widened by the diametral interference ΔD); the strain checked is the larger of ε = K_ann·ε_nom (K_ann the user's strain factor) and the inner-surface strain ΔD/(D − h); the radial contact force summed round the ring is P = K_F·2π·Es·ε_nom·h·b (hoop force Es·ε_nom·h·b over the radius, K_F the user's optional force factor, 1 if none), b being the effective length of a free ring\n" +
        "Torsion bar of radius r with a lever of length a deflected y: φ = y/a; the bar is held at one end (free length l_eff = l) or at both ends with the lever in the middle (l_eff = l/2, two halves in parallel); γ = φ·r/l_eff, equivalent tensile strain ε = γ/(1 + ν), γ_perm = ε_perm·(1 + ν); G = Es/(2(1 + ν)), T = n·G·(π r⁴/2)·φ/l_eff with n = 1 or 2, P = T/a (both ends: P = 4·G·J·φ/(l·a))\n" +
        "Joining forces: W = P·(μ + tan α)/(1 − μ·tan α) on the entry face (angle α to the joining direction) and W′ = P·(μ + tan α′)/(1 − μ·tan α′) on the return face (α′); the joint is self-locking, with an infinite separation force, when α′ ≥ arctan(1/μ); assembly jams when μ·tan α ≥ 1\n" +
        "Utilisation u = ε/ε_perm (FAIL above 1); permissible deflection y_perm = K_y·ε_perm·l²/h (annular ΔD_perm = ε_perm·min(D/K_ann, D − h); torsion y_perm = γ_perm·l_eff·a/r)",
        [
            new CalcInputSpec("mode", "Form of snap-fit", CalcInputKind.Choice, Choices: [Constant, TaperedThickness, TaperedWidth, Annular, Torsion], DefaultChoice: Constant, Group: "Joint"),
            new CalcInputSpec("l", "Length of the beam (cantilever); of the torsion bar: its free length when held at one end, or its whole length between the two supports when held at both ends with the lever in the middle", CalcInputKind.Quantity, "mm", Default: 15, Min: 2, Max: 100, HardMin: 0.1, HardMax: 1000, Group: "Geometry"),
            new CalcInputSpec("h", "Thickness at the root (cantilever); wall thickness of the ring (annular)", CalcInputKind.Quantity, "mm", Default: 2, Min: 0.3, Max: 10, HardMin: 0.01, HardMax: 100, Group: "Geometry"),
            new CalcInputSpec("b", "Width at the root (cantilever); effective axial length of the ring (annular: that of a free ring, not the lip of a longer tube)", CalcInputKind.Quantity, "mm", Default: 6, Min: 1, Max: 50, HardMin: 0.01, HardMax: 1000, Group: "Geometry"),
            new CalcInputSpec("h_ratio", "Tip thickness / root thickness (tapered thickness)", CalcInputKind.Number, Default: 0.5, Min: 0.25, Max: 1, HardMin: 0.05, HardMax: 1, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("b_ratio", "Tip width / root width (tapered width)", CalcInputKind.Number, Default: 0.5, Min: 0.1, Max: 1, HardMin: 0.02, HardMax: 1, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("y", "Deflection at the tip (cantilever) or at the end of the lever (torsion): the undercut to be cleared", CalcInputKind.Quantity, "mm", Default: 1, Min: 0.05, Max: 20, HardMin: 0, HardMax: 1000, Group: "Geometry"),
            new CalcInputSpec("D_ring", "Mean diameter of the ring (annular)", CalcInputKind.Quantity, "mm", Default: 30, Min: 5, Max: 500, HardMin: 0.5, HardMax: 5000, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("dD", "Interference on the diameter: how much the ring is widened to pass the undercut (annular)", CalcInputKind.Quantity, "mm", Default: 0.4, Min: 0, Max: 5, HardMin: 0, HardMax: 100, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("K_ann", "Strain factor of the annular snap on the nominal hoop strain ΔD/D (record snapfit.annular-factor; 1 for a plain free ring; no default: from your design reference)", CalcInputKind.Number, Min: 0.5, Max: 3, HardMin: 0.1, HardMax: 10, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("K_F", "Force factor of the annular snap on the hoop force (record snapfit.annular-force-factor; e.g. the stiffening of a tube behind the ring; blank: 1, the free ring; no default)", CalcInputKind.Number, Min: 0.5, Max: 5, HardMin: 0.01, HardMax: 20, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("bar_support", "How the torsion bar is held: at one end, or at both ends with the lever in the middle (twice the strain and four times the stiffness of one end for the same total length)", CalcInputKind.Choice, Choices: [BarOne, BarBoth], DefaultChoice: BarOne, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("r_bar", "Radius of the torsion bar", CalcInputKind.Quantity, "mm", Default: 1.5, Min: 0.3, Max: 10, HardMin: 0.01, HardMax: 100, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("a_arm", "Length of the lever on the torsion bar", CalcInputKind.Quantity, "mm", Default: 8, Min: 2, Max: 100, HardMin: 0.1, HardMax: 1000, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("alpha", "Entry (assembly) angle α of the inclined face to the joining direction", CalcInputKind.Quantity, "deg", Default: 30, Min: 5, Max: 60, HardMin: 0, HardMax: 89, Group: "Joining"),
            new CalcInputSpec("alpha_r", "Return (separation) angle α′ of the retaining face (90° is a face square to the joining direction)", CalcInputKind.Quantity, "deg", Default: 60, Min: 10, Max: 90, HardMin: 0, HardMax: 90, Group: "Joining"),
            new CalcInputSpec("mu", "Coefficient of friction between the two parts (record snapfit.friction; no default: the pair of materials, from your reference or a W3 friction pair)", CalcInputKind.Number, Min: 0.05, Max: 1, HardMin: 0, HardMax: 2, Group: "Joining"),
            new CalcInputSpec("Es_source", "Where the secant modulus comes from: typed at the design strain, or interpolated from the Es(ε) points of a W3 material", CalcInputKind.Choice, Choices: [EsTyped, EsCurve], DefaultChoice: EsTyped, Group: "Material"),
            new CalcInputSpec("Es", "Secant modulus Es at the design strain and temperature (annular: at the nominal hoop strain ΔD/D) (typed; no default: the plastic's stress–strain data)", CalcInputKind.Quantity, "MPa", Min: 10, Max: 20000, HardMin: 0.01, HardMax: 1e6, IsOptional: true, Group: "Material"),
            new CalcInputSpec("Es_pts", "Secant modulus against strain, Es(ε), of the plastic at the temperature of use (from the W3 material)", CalcInputKind.Table, Columns: SecantColumns, MaxRows: 50, IsOptional: true, Group: "Material", MaterialProperty: SecantCurveKey),
            new CalcInputSpec("loading", "How often the joint is made: selects the one-time or the repeated permissible strain", CalcInputKind.Choice, Choices: [Once, Repeated], DefaultChoice: Once, Group: "Material"),
            new CalcInputSpec("eps_perm_once", "Permissible strain for one-time joining, as a fraction (0.04 is 4 %) at the temperature of use (no default: from the plastic's data)", CalcInputKind.Number, Min: 0.001, Max: 0.3, HardMin: 1e-6, HardMax: 1, IsOptional: true, Group: "Material", MaterialProperty: "eps_perm_once"),
            new CalcInputSpec("eps_perm_repeated", "Permissible strain for repeated joining, as a fraction (no default)", CalcInputKind.Number, Min: 0.001, Max: 0.3, HardMin: 1e-6, HardMax: 1, IsOptional: true, Group: "Material", MaterialProperty: "eps_perm_repeated"),
            new CalcInputSpec("nu", "Poisson's ratio ν (torsion bar)", CalcInputKind.Number, Min: 0.2, Max: 0.49, HardMin: -0.99, HardMax: 0.499, IsOptional: true, Group: "Material", MaterialProperty: "nu"),
            new CalcInputSpec("n_latch", "Number of identical latches that share a holding load (a whole number; blank = the holding force is not worked out)", CalcInputKind.Number, Min: 1, Max: 100, HardMin: 1, HardMax: 1000, IsOptional: true, Group: "Holding"),
            new CalcInputSpec("F_req", "Force the latches must hold against, for example the pressure on a cover (blank = no check; needs n_latch; may be linked from E2 F_seal)", CalcInputKind.Quantity, "N", Min: 0, Max: 100000, HardMin: 0, HardMax: 1e8, IsOptional: true, Group: "Holding"),
            new CalcInputSpec("T_use", "Temperature of use. Recorded: Es and the permissible strain must be the values at this temperature", CalcInputKind.Quantity, "degC", Default: 23, Min: -40, Max: 120, HardMin: -150, HardMax: 400, Group: "Material"),
        ],
        [
            new CalcAssumption("Beam theory (Euler–Bernoulli) for the cantilever: a rigid root, a linear taper, a constant secant modulus Es at the design strain over the whole beam, small deflection. The strain is the nominal surface strain at the critical section, with no local concentration from the root fillet; the geometry factors are derived by integration (the 0.67 of the design guides is the constant-section 2/3 rounded).", "Euler–Bernoulli beam theory (derived); the guide's closed forms for the constant section are to be checked against the owner's copy."),
            new CalcAssumption("Es and the permissible strain are properties of the plastic at the temperature of use, with the loading (one-time or repeated) and the moisture state of the part. No values are supplied: they come from the plastic maker's data or a W3 material, and the temperature entered records what they were taken at.", "No plastics data are shipped."),
            new CalcAssumption("Annular snap: a free, thin, closed ring of effective axial length b. The nominal hoop strain is the diametral interference over the mean diameter, ΔD/D; the strain checked is the larger of that times the user's strain factor K_ann (record snapfit.annular-factor, no default) and the inner-surface strain. The contact force is the hoop force Es·ε_nom·h·b taken round the ring, from the nominal strain only, times the user's optional force factor K_F (record snapfit.annular-force-factor; blank is 1); a tube behind the ring stiffens the joint and raises the force, which only K_F can represent. The strain on the inner (contact) surface is ΔD/(D − h), slightly above the mean-diameter value, so the check takes the larger of the two; a wall above a tenth of the diameter warns that the thin-ring model has run out.", "Derived from ring geometry (not a relation of the design guide); compare with the owner's copy of the guide."),
            new CalcAssumption("Torsion bar: a solid round bar of radius r loaded at the end of a lever, the angle small (φ = y/a), held at one end or at both ends with the lever in the middle (twice the strain and four times the stiffness of one end for the same total length). The permissible shear strain is ε_perm·(1 + ν) and the equivalent tensile strain γ/(1 + ν); G = Es/(2(1 + ν)) with the secant modulus at that strain.", "γ = φ·r/l and γ_perm ≈ ε_perm·(1 + ν) are the brief's design-guide relations, to be checked against the owner's copy; the torque, the force and the two-ended case are derived."),
            new CalcAssumption("Assembly and separation: a rigid mating part, Coulomb friction μ on the inclined face (α for assembly, α′ for separation), the deflection force P taken at the design deflection; no creep or stress relaxation of the joint in service, no impact (speed) effect.", "Friction on an inclined plane; the friction coefficient is the user's record."),
        ],
        [
            new CalcClauseRef(Ref, "Section B", "Cantilever, annular and torsion snap-fit relations"),
            new CalcClauseRef("Euler–Bernoulli beam theory", "—", "Tip deflection and surface strain of a tapered cantilever, by integration"),
        ],
        [
            new CalcApplicabilityRule("missing", "An input the chosen form, or the chosen source of the secant modulus or the permissible strain, needs is blank.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("geometry", "The mean diameter of the ring does not exceed its wall thickness.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("es-curve", "The W3 secant-modulus points hold no usable row (a strain of zero or more with a positive modulus).", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("slender", "The beam is shorter than 5 times its thickness (l/h < 5): beam theory underestimates the stiffness and the strain is not reliable.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("large-deflection", "The cantilever deflection is more than 0.3 of its length: linear beam theory under-predicts the strain (about 4 % at 0.3, 8.5 % at 0.41) and the force (10 %, 22 %).", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("es-range", "The design strain is outside the strains of the secant-modulus points: the end value is held, with no extrapolation.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("assembly-jam", "μ·tan α is 1 or more: the entry face jams and the part cannot be pushed on.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("n-latch", "The number of latches must be a whole number.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("hold-unused", "A force to hold is given without a number of latches: the holding check is not made.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("self-locking", "The return angle α′ is at or above arctan(1/μ): the joint cannot be separated by pulling, however large the force.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("thick-ring", "The ring's wall is more than a tenth of its mean diameter (h/D > 0.1): the thin-ring model no longer holds.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("small-angle", "The torsion-bar twist is above about 20°: the small-angle relation φ = y/a is no longer good.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
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
            new CalcTestCase("ka-annular", "Annular snap: hoop strain from the interference, radial force round the ring from the nominal strain",
                new CalcReferenceSource("Analytic (thin-ring geometry; recomputed in python3, the force also by strain energy)", null, "D 30 mm, ΔD 0.4 mm, K_ann 1.2, h 1.5 mm, b 5 mm, Es 2400 MPa, ε_perm 0.04, μ 0.3, α 30°, α′ 60°: ε_nom = ΔD/D = 1.33333 %; K_ann·ε_nom = 1.6 % against the inner surface ΔD/(D − h) = 1.403509 %, so ε = 1.6 %; P = 2π·Es·ε_nom·h·b = 1507.964 N (K_ann does not enter); u = 0.4; ΔD_perm = ε_perm·D/K_ann = 1 mm; W = 1600.171 N; W′ = 6378.763 N"),
                [CalcInputValue.Choice("mode", Annular), Q("h", 1.5, "mm"), Q("b", 5, "mm"), Q("D_ring", 30, "mm"), Q("dD", 0.4, "mm"), CalcInputValue.Of("K_ann", 1.2), Q("Es", 2400, "MPa"), CalcInputValue.Of("eps_perm_once", 0.04), CalcInputValue.Of("mu", 0.3), Q("alpha", 30, "deg"), Q("alpha_r", 60, "deg")],
                [new CalcExpectedValue("eps_nom", 1.33333333333, "%", 1e-9), new CalcExpectedValue("eps", 1.6, "%", 1e-9), new CalcExpectedValue("eps_inner", 1.40350877193, "%", 1e-9), new CalcExpectedValue("K_F", 1, "", 1e-12), new CalcExpectedValue("P", 1507.96447372, "N", 1e-9), new CalcExpectedValue("strain_util", 0.4, "", 1e-9), new CalcExpectedValue("y_perm", 1, "mm", 1e-9), new CalcExpectedValue("assembly_force", 1600.17073903, "N", 1e-9), new CalcExpectedValue("retention_force", 6378.76280900, "N", 1e-9)]),
            new CalcTestCase("ka-annular-inner-surface", "Annular snap with K_ann = 1: the inner-surface strain governs the check",
                new CalcReferenceSource("Analytic (thin-ring geometry; recomputed in python3)", null, "D 30 mm, ΔD 0.4 mm, K_ann 1, h 3 mm, b 5 mm, Es 2400 MPa, ε_perm 0.04, μ 0.3, α 30°, α′ 60° (h/D = 0.1, no warning): ε_nom = 1.33333 %, inner surface ΔD/(D − h) = 0.4/27 = 1.481481 % > K_ann·ε_nom, so ε = 1.481481 %; u = 0.0148148/0.04 = 0.370370; ΔD_perm = ε_perm·min(D/K_ann, D − h) = 1.08 mm; P = 2π·Es·ε_nom·h·b = 3015.929 N (the force uses the nominal strain); W = 3200.342 N; W′ = 12757.526 N"),
                [CalcInputValue.Choice("mode", Annular), Q("h", 3, "mm"), Q("b", 5, "mm"), Q("D_ring", 30, "mm"), Q("dD", 0.4, "mm"), CalcInputValue.Of("K_ann", 1), Q("Es", 2400, "MPa"), CalcInputValue.Of("eps_perm_once", 0.04), CalcInputValue.Of("mu", 0.3), Q("alpha", 30, "deg"), Q("alpha_r", 60, "deg")],
                [new CalcExpectedValue("eps_nom", 1.33333333333, "%", 1e-9), new CalcExpectedValue("eps_inner", 1.48148148148, "%", 1e-9), new CalcExpectedValue("eps", 1.48148148148, "%", 1e-9), new CalcExpectedValue("strain_util", 0.370370370370, "", 1e-9), new CalcExpectedValue("y_perm", 1.08, "mm", 1e-9), new CalcExpectedValue("P", 3015.92894745, "N", 1e-9), new CalcExpectedValue("assembly_force", 3200.34147805, "N", 1e-9), new CalcExpectedValue("retention_force", 12757.5256180, "N", 1e-9)]),
            new CalcTestCase("ka-annular-force-factor", "Annular snap with a force factor K_F = 1.25 (a tube behind the ring): the force scales, the strain does not",
                new CalcReferenceSource("Analytic (thin-ring geometry; recomputed in python3, the force also by strain energy)", null, "The ring of ka-annular with K_F = 1.25: ε and the utilisation are unchanged (1.6 %, 0.4); P = K_F·2π·Es·ε_nom·h·b = 1884.956 N; W = 2000.213 N; W′ = 7973.454 N"),
                [CalcInputValue.Choice("mode", Annular), Q("h", 1.5, "mm"), Q("b", 5, "mm"), Q("D_ring", 30, "mm"), Q("dD", 0.4, "mm"), CalcInputValue.Of("K_ann", 1.2), CalcInputValue.Of("K_F", 1.25), Q("Es", 2400, "MPa"), CalcInputValue.Of("eps_perm_once", 0.04), CalcInputValue.Of("mu", 0.3), Q("alpha", 30, "deg"), Q("alpha_r", 60, "deg")],
                [new CalcExpectedValue("eps", 1.6, "%", 1e-9), new CalcExpectedValue("K_F", 1.25, "", 1e-12), new CalcExpectedValue("P", 1884.95559215, "N", 1e-9), new CalcExpectedValue("strain_util", 0.4, "", 1e-9), new CalcExpectedValue("assembly_force", 2000.21342378, "N", 1e-9), new CalcExpectedValue("retention_force", 7973.45351125, "N", 1e-9)]),
            new CalcTestCase("ka-torsion", "Torsion bar: shear strain, permissible shear strain, torque and lever force",
                new CalcReferenceSource("Analytic (closed-form; recomputed in python3)", null, "l 20 mm, r 2 mm, lever a 10 mm, y 1.5 mm, ν 0.38, Es 2000 MPa, ε_perm 0.03, μ 0.3, α 30°, α′ 60°: φ = y/a = 0.15 rad = 8.594367°; γ = φ·r/l = 0.015; ε = γ/(1 + ν) = 1.086957 %; γ_perm = ε_perm·(1 + ν) = 0.0414; u = 0.362319; G = Es/(2(1 + ν)) = 724.6377 MPa; T = G·(π r⁴/2)·φ/l = 0.136591 N·m; P = T/a = 13.659098 N; y_perm = γ_perm·l·a/r = 4.14 mm; W = 14.494300 N; W′ = 57.778649 N"),
                [CalcInputValue.Choice("mode", Torsion), Q("l", 20, "mm"), Q("r_bar", 2, "mm"), Q("a_arm", 10, "mm"), Q("y", 1.5, "mm"), CalcInputValue.Of("nu", 0.38), Q("Es", 2000, "MPa"), CalcInputValue.Of("eps_perm_once", 0.03), CalcInputValue.Of("mu", 0.3), Q("alpha", 30, "deg"), Q("alpha_r", 60, "deg")],
                [new CalcExpectedValue("phi", 8.59436692696, "deg", 1e-9), new CalcExpectedValue("gamma", 0.015, "", 1e-9), new CalcExpectedValue("eps", 1.08695652174, "%", 1e-9), new CalcExpectedValue("gamma_perm", 0.0414, "", 1e-9), new CalcExpectedValue("strain_util", 0.362318840580, "", 1e-9), new CalcExpectedValue("G_sec", 724.637681159, "MPa", 1e-9), new CalcExpectedValue("T_bar", 0.136590984939, "N*m", 1e-9), new CalcExpectedValue("P", 13.6590984939, "N", 1e-9), new CalcExpectedValue("y_perm", 4.14, "mm", 1e-9), new CalcExpectedValue("assembly_force", 14.4943001723, "N", 1e-9), new CalcExpectedValue("retention_force", 57.7786486322, "N", 1e-9)]),
            new CalcTestCase("ka-torsion-both-ends", "Torsion bar held at both ends with the lever in the middle: twice the strain, four times the stiffness",
                new CalcReferenceSource("Analytic (closed-form; recomputed in python3, the force also by strain energy U = G·γ²·π·r²·L/4, P = 2U/y)", null, "The bar of ka-torsion (whole length L = 20 mm between the supports, r 2 mm, lever 10 mm, y 1.5 mm, ν 0.38, Es 2000 MPa, ε_perm 0.03, μ 0.3, α 30°, α′ 60°) held at both ends: l_eff = L/2 = 10 mm; γ = φ·r/l_eff = 0.03; ε = 2.173913 %; u = 0.724638; T = 2·G·(π r⁴/2)·φ/l_eff = 4·G·J·φ/L = 0.546364 N·m; P = T/a = 54.636394 N; y_perm = γ_perm·l_eff·a/r = 2.07 mm; W = 57.977201 N; W′ = 231.114595 N"),
                [CalcInputValue.Choice("mode", Torsion), CalcInputValue.Choice("bar_support", BarBoth), Q("l", 20, "mm"), Q("r_bar", 2, "mm"), Q("a_arm", 10, "mm"), Q("y", 1.5, "mm"), CalcInputValue.Of("nu", 0.38), Q("Es", 2000, "MPa"), CalcInputValue.Of("eps_perm_once", 0.03), CalcInputValue.Of("mu", 0.3), Q("alpha", 30, "deg"), Q("alpha_r", 60, "deg")],
                [new CalcExpectedValue("l_eff", 10, "mm", 1e-9), new CalcExpectedValue("gamma", 0.03, "", 1e-9), new CalcExpectedValue("eps", 2.17391304348, "%", 1e-9), new CalcExpectedValue("strain_util", 0.724637681159, "", 1e-9), new CalcExpectedValue("T_bar", 0.546363939755, "N*m", 1e-9), new CalcExpectedValue("P", 54.6363939755, "N", 1e-9), new CalcExpectedValue("y_perm", 2.07, "mm", 1e-9), new CalcExpectedValue("assembly_force", 57.9772006894, "N", 1e-9), new CalcExpectedValue("retention_force", 231.114594529, "N", 1e-9)]),
            new CalcTestCase("ka-curve", "Cantilever with the secant modulus interpolated from W3 points Es(ε)",
                new CalcReferenceSource("Analytic (closed-form; recomputed in python3)", null, "l 15 mm, h 2 mm, b 6 mm, y 1.5 mm: ε = 2 %; points (0.5 %, 2600 MPa), (1 %, 2400 MPa), (3 %, 1800 MPa) give Es(2 %) = 2400 − 600·(0.01/0.02) = 2100 MPa; P = b·h²·Es·ε/(6·l) = 11.2 N; ε_perm 0.04: u = 0.5"),
                [Q("l", 15, "mm"), Q("h", 2, "mm"), Q("b", 6, "mm"), Q("y", 1.5, "mm"), CalcInputValue.Choice("Es_source", EsCurve), SecantPoints((0.005, 2600), (0.01, 2400), (0.03, 1800)), CalcInputValue.Of("eps_perm_once", 0.04), CalcInputValue.Of("mu", 0.3), Q("alpha", 30, "deg"), Q("alpha_r", 60, "deg")],
                [new CalcExpectedValue("eps", 2, "%", 1e-9), new CalcExpectedValue("Es", 2100, "MPa", 1e-9), new CalcExpectedValue("P", 11.2, "N", 1e-9), new CalcExpectedValue("strain_util", 0.5, "", 1e-9)]),
            new CalcTestCase("ka-holding", "Four of the constant-section latches hold a 100 N load: F_hold = n·W′",
                new CalcReferenceSource("Analytic (n identical latches sharing the load equally; recomputed in python3)", null, "The cantilever of ka-constant (W′ = 36.096414 N) with n_latch = 4 and a load F_req = 100 N: F_hold = 4 × 36.096414 = 144.385654 N, utilisation 100/144.385654 = 0.692590 (PASS)"),
                [Q("l", 15, "mm"), Q("h", 2, "mm"), Q("b", 6, "mm"), Q("y", 1, "mm"), Q("Es", 2400, "MPa"), CalcInputValue.Of("eps_perm_once", 0.04), CalcInputValue.Of("mu", 0.3), Q("alpha", 30, "deg"), Q("alpha_r", 60, "deg"), CalcInputValue.Of("n_latch", 4), Q("F_req", 100, "N")],
                [new CalcExpectedValue("retention_force", 36.096413577352465, "N", 1e-9), new CalcExpectedValue("F_hold", 144.38565430940986, "N", 1e-9), new CalcExpectedValue("u_hold", 0.692589582242748, "", 1e-9)]),
        ],
        Category: "Connections",
        OutputPorts:
        [
            new CalcOutputPort("assembly_force", "N", "Force to push the parts together over the entry face (snap.assembly_force)"),
            new CalcOutputPort("retention_force", "N", "Force to separate the parts over the return face (snap.retention_force); reads \"self-locking\" and has no number when the return angle locks"),
            new CalcOutputPort("strain_util", "", "Strain at the critical section / permissible strain (snap.strain_util)"),
            new CalcOutputPort("F_hold", "N", "Force n_latch latches hold, n × the separation force (only when n_latch is entered and the joint is not self-locking)"),
        ],
        RecordKinds:
        [
            new CalcRecordKind(MaterialKind, "Material properties", ["Es_pts", "eps_perm_once", "eps_perm_repeated", "nu"], "A plastic from the library: its permissible strains, its Poisson ratio and its secant-modulus points Es(ε) fill these inputs, each citing the material, its revision and the page or table of the value. Use the values for the temperature of use."),
            new CalcRecordKind("snapfit.friction", "Coefficient of friction", ["mu"], "μ for the pair of materials and surfaces of this joint (static or kinetic as the use needs), from your reference, the plastic maker or a test, with its source. A W3 material can hold friction coefficients by pair; read μ from there and enter it here."),
            new CalcRecordKind("snapfit.annular-factor", "Annular snap strain factor", ["K_ann"], "The factor on the nominal hoop strain ΔD/D of the annular snap-fit (1 for a plain free ring) that gives the strain to check, from your design reference for the ring shape, with its source."),
            new CalcRecordKind("snapfit.annular-force-factor", "Annular snap force factor", ["K_F"], "The factor on the hoop force of the free ring (for example the stiffening of a tube behind the ring), from your design reference, with its source. Blank: 1."),
            new CalcRecordKind("snapfit.secant-modulus", "Secant modulus at the design strain", ["Es"], "Es read from the plastic's stress–strain curve at the design strain (annular: at the nominal hoop strain ΔD/D) and the temperature of use (the temperature of use is the input T_use), with the source of the curve."),
        ]);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    /// <summary>The secant-modulus table from (strain, Es in MPa) points.</summary>
    public static CalcInputValue SecantPoints(params (double Strain, double EsMpa)[] points) =>
        CalcInputValue.Table("Es_pts", points.Select(p => CalcRow.Of(("eps", new CalcCell(p.Strain)), ("Es", new CalcCell(p.EsMpa, "MPa")))).ToList());

    public void Run(CalcRunContext c)
    {
        c.MarkUtilisation("strain_util", "u_hold");
        var mode = c.InputText("mode");

        foreach (var s in new[] { "mu", "alpha", "alpha_r", "T_use" })
            c.Input(s);

        c.Assume($"Es and the permissible strain are the values of the plastic at the temperature of use, {F(c.Si("T_use") - 273.15)} °C, as entered, in the moisture state of the part; the sheet cannot check this.", "Recorded temperature of use.");

        if (!Strain(c, mode))
            return;

        if (!Material(c, mode))
            return;

        Force(c, mode);
        Joining(c);

        var peak = c.Session.Find("t_crit") is not null
            ? $" The taper is steep enough that the strain peaks inside the beam, {F(c.Si("t_crit") * c.Si("l") * 1e3)} mm from the root, so a section change or notch near that point matters as much as the root."
            : string.Empty;

        c.Output("fillet_advisory", "Root-fillet advisory", new TextValue(
            "The strain above is the nominal strain, with no local concentration. A sharp internal corner at the root (or at the end of the bar or ring) concentrates the strain and is the usual place for a snap-fit to break: give the root a generous fillet radius, of the order of the thickness, taper the section away from it, and apply a concentration factor from your own reference if the radius is small." + peak));
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

        // mech.snap-fit-01: beam theory is linear, so a deflection that is a large share of the length is under-predicted in strain and force.
        if (c.Si("y") / c.Si("l") > LargeDeflectionLimit)
            c.Warn("large-deflection", $"y: the deflection is {F(c.Si("y") / c.Si("l"))} of the length l; beam theory is linear and under-predicts the strain and the force at this size (a large-deflection solution gives about +4 % strain and +10 % force at y/l 0.3, +8.5 % and +22 % at 0.41), so the strain check is unconservative; check by a large-deflection model or a test.");

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
            c.Record("J", "[2r − r²/2 − ln r − 3/2]/(1 − r)³", $"r = {F(r)}", new ScalarValue(j), "Deflection integral J of the thickness taper (derived: Euler–Bernoulli integration)", Derived);
            c.Record("phi_max", "(1 − t*)/(1 − a·t*)², t* = (2a − 1)/a for a > 1/2, else 1", $"a = {F(1 - r)}", new ScalarValue(phiMax), "Largest value of the strain shape along the beam (1 at the root)", Derived);

            if (tCrit > 0)
                c.Record("t_crit", "(2a − 1)/a", $"a = {F(1 - r)}", new ScalarValue(tCrit), "Where the strain peaks, as a fraction of the length from the root (the taper is steep enough for the maximum to lie inside the beam)", Derived);

            c.Record("K_y", "2·J/φ_max", $"2 × {F(j)} / {F(phiMax)}", new ScalarValue(ky), "Deflection factor: y = K_y·ε·l²/h", Derived);
        }
        else if (mode == TaperedWidth)
        {
            c.Input("b_ratio");
            var r = c.InputSi("b_ratio");
            var j = SnapFitGeometry.WidthJ(r);
            ky = 2 * j;
            c.Record("J", "[1/2 − 2r + 3r²/2 − r²·ln r]/(1 − r)³", $"r = {F(r)}", new ScalarValue(j), "Deflection integral J of the width taper (derived: Euler–Bernoulli integration)", Derived);
            c.Record("phi_max", "1 (the strain is largest at the root for a width taper)", "—", new ScalarValue(1), "Largest value of the strain shape along the beam", Derived);
            c.Record("K_y", "2·J", $"2 × {F(j)}", new ScalarValue(ky), "Deflection factor: y = K_y·ε·l²/h", Derived);
        }
        else
        {
            ky = SnapFitGeometry.ConstantKy;
            c.Record("phi_max", "1", "—", new ScalarValue(1), "Largest value of the strain shape along the beam (the root)", Derived);
            c.Record("K_y", "2/3", "constant section", new ScalarValue(ky), "Deflection factor: y = K_y·ε·l²/h (derived; the design guides print the rounded 0.67)", Derived);
        }

        c.Assign("eps", "y * h / (K_y * l^2)", "%", "Maximum surface strain at the critical section (nominal)", mode == Constant ? Ref : Derived);
        return true;
    }

    private static bool Ring(CalcRunContext c)
    {
        if (!Require(c, "D_ring", "dD", "K_ann"))
            return false;

        foreach (var s in new[] { "h", "b", "D_ring", "dD", "K_ann" })
            c.Input(s);

        if (!(c.Si("D_ring") > c.Si("h")))
        {
            c.Block("geometry", "D_ring: the mean diameter of the ring must exceed its wall thickness h.");
            return false;
        }

        c.Assign("eps_nom", "dD / D_ring", "%", "Nominal hoop strain ΔD/D of the mean diameter (drives the force)", DerivedRing);
        c.Assign("eps_inner", "dD / (D_ring - h)", "%", "Nominal strain at the inner (contact) surface ΔD/(D − h)", DerivedRing);
        c.Assign("eps", "max(K_ann * eps_nom, eps_inner)", "%", "Hoop strain checked: the larger of the nominal strain times the user's strain factor K_ann and the inner-surface strain", DerivedRing);

        if (c.Si("h") / c.Si("D_ring") > 0.1 + 1e-12) // tolerance: a ring exactly at 10 % does not warn
            c.Warn("thick-ring", $"h: the wall is {F(c.Si("h") / c.Si("D_ring") * 100)} % of the mean diameter (h/D > 10 %); the thin-ring model of the hoop strain and force is no longer good, so check the ring with a finite-element model or a test.");
        return true;
    }

    private static bool Bar(CalcRunContext c)
    {
        if (!Require(c, "r_bar", "a_arm", "nu"))
            return false;

        foreach (var s in new[] { "l", "y", "r_bar", "a_arm", "nu" })
            c.Input(s);

        var both = c.Has("bar_support") && c.InputText("bar_support") == BarBoth;

        if (c.Has("bar_support"))
            c.Input("bar_support");

        if (both)
            c.Assign("l_eff", "l / 2", "mm", "Free length of each half of the bar (held at both ends, lever in the middle)", DerivedBar);
        else
            c.Assign("l_eff", "l", "mm", "Free length of the bar (held at one end)", DerivedBar);

        c.Record("n_bar", both ? "2 (two halves in parallel)" : "1", both ? "held at both ends" : "held at one end", new ScalarValue(both ? 2 : 1), "Number of bar lengths twisting", DerivedBar);
        c.Assign("phi", "y / a_arm", "deg", "Twist of the bar (small angle: lever deflection / lever length)", DerivedBar);

        if (c.Si("phi") > SmallAngleLimit)
            c.Warn("small-angle", $"y: the bar twists {F(c.Si("phi") * 180 / Math.PI)}°; the small-angle relation φ = y/a is no longer good above about 20°.");

        c.Assign("gamma", "phi * r_bar / l_eff", null, "Shear strain at the surface of the bar", both ? DerivedBar : Ref);
        c.Assign("eps", "gamma / (1 + nu)", "%", "Equivalent tensile strain γ/(1 + ν)", DerivedBar);
        return true;
    }

    // ------------------------------------------------------------------
    // Material: permissible strain and secant modulus
    // ------------------------------------------------------------------

    private static bool Material(CalcRunContext c, string mode)
    {
        var repeated = c.InputText("loading") == Repeated;
        var epsSymbol = repeated ? "eps_perm_repeated" : "eps_perm_once";

        if (!Require(c, epsSymbol))
            return false;

        c.Input(epsSymbol);
        c.Assign("eps_perm", epsSymbol, "%", $"Permissible strain ({(repeated ? "repeated" : "one-time")} joining, as entered)", Ref);

        if (c.InputText("Es_source") == EsCurve)
            return SecantFromCurve(c, mode == Annular ? "eps_nom" : "eps");

        if (!Require(c, "Es"))
            return false;

        c.Input("Es");
        return true;
    }

    /// <summary>The secant modulus at the strain that drives the force (<paramref name="strainSymbol"/>), from the W3 points.</summary>
    private static bool SecantFromCurve(CalcRunContext c, string strainSymbol)
    {
        var points = new List<(double Strain, double Es)>();

        foreach (var row in c.Rows("Es_pts"))
        {
            if (row.Get("eps") is not { Number: { } strain } || row.Get("Es") is not { Number: { } modulus } cell)
                continue;

            var si = (cell.Unit is { Length: > 0 } unit ? UnitCatalog.Parse(unit) : UnitCatalog.Parse("MPa")).ToSi(modulus);

            if (double.IsFinite(strain) && double.IsFinite(si) && strain >= 0 && si > 0)
                points.Add((strain, si));
        }

        if (points.Count == 0)
        {
            c.Block("es-curve", c.EnterRows("Es_pts").TrimEnd('.') + "; a row needs a strain of zero or more and a positive secant modulus.");
            return false;
        }

        points.Sort((a, b) => a.Strain.CompareTo(b.Strain));
        var eps = c.Si(strainSymbol);
        var es = Interpolate(points, eps, out var outside);

        if (outside)
            c.Warn("es-range", $"Es_pts: the design strain {F(eps * 100)} % is outside the strains of the points ({F(points[0].Strain * 100)} … {F(points[^1].Strain * 100)} %); the end value of the secant modulus is held, with no extrapolation.");

        c.Record("Es", "Es(ε) by linear interpolation of the W3 points", $"ε = {F(eps * 100)} % in {points.Count} point(s)", ScalarValue.Of(es / 1e6, "MPa"), "Secant modulus at the design strain (from the W3 points)", "W3 material points (linear interpolation)");
        return true;
    }

    /// <summary>Es at <paramref name="strain"/> by linear interpolation of ascending points; the end value is held outside the points.</summary>
    private static double Interpolate(IReadOnlyList<(double Strain, double Es)> points, double strain, out bool outside)
    {
        const double slack = 1e-9;
        outside = strain < points[0].Strain * (1 - slack) || strain > points[^1].Strain * (1 + slack);

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
                if (c.Has("K_F"))
                    c.Input("K_F");
                else
                    c.Record("K_F", "1 (no force factor entered)", "free ring", new ScalarValue(1), "Force factor K_F on the hoop force (none entered: the free ring)", DerivedRing);

                c.Assign("P", "K_F * 2 * pi * Es * eps_nom * h * b", "N", "Radial contact force summed round the ring, from the nominal strain (hoop force Es·ε_nom·h·b over the radius)", DerivedRing);
                c.Assign("y_perm", "eps_perm * min(D_ring / K_ann, D_ring - h)", "mm", "Permissible interference on the diameter ΔD_perm (the smaller of the mean-diameter and the inner-surface limits)", DerivedRing);
                break;
            case Torsion:
                c.Assign("gamma_perm", "eps_perm * (1 + nu)", null, "Permissible shear strain γ_perm ≈ ε_perm·(1 + ν)", Ref);
                c.Assign("G_sec", "Es / (2 * (1 + nu))", "MPa", "Secant shear modulus Es/(2(1 + ν))");
                c.Assign("J_bar", "pi * r_bar^4 / 2", "mm^4", "Polar second moment of the bar");
                c.Assign("T_bar", "n_bar * G_sec * J_bar * phi / l_eff", "N*m", "Torque in the bar", DerivedBar);
                c.Assign("P", "T_bar / a_arm", "N", "Force at the end of the lever", DerivedBar);
                c.Assign("y_perm", "gamma_perm * l_eff * a_arm / r_bar", "mm", "Permissible deflection at the end of the lever", DerivedBar);
                break;
            default:
                c.Assign("P", "b * h^2 * Es * eps / (6 * l * phi_max)", "N", "Deflection force at the tip: P = (b·h²/6)·(Es·ε/l) at the root of a constant section", mode == Constant ? Ref : Derived);
                c.Assign("y_perm", "K_y * eps_perm * l^2 / h", "mm", "Permissible tip deflection", mode == Constant ? Ref : Derived);
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
        else if (mode == Annular)
        {
            c.Output("eps_nom", "Nominal hoop strain ΔD/D");
            c.Output("eps_inner", "Nominal strain at the inner surface");
            c.Output("K_F", "Force factor used");
        }
        else if (mode == Torsion)
        {
            c.Output("l_eff", "Free length twisting");
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
            c.Output("self_locking", "Self-locking", new TextValue("Yes: α′ ≥ arctan(1/μ); the parts cannot be separated by pulling"));
            c.Warn("self-locking", $"alpha_r: the return angle {F(alphaR * 180 / Math.PI)}° is at or above arctan(1/μ) = {F(limit * 180 / Math.PI)}°; the separation force is infinite and the joint cannot be undone by pulling. That is right for a permanent joint and wrong for a serviceable one.");
        }
        else
        {
            c.Assign("tan_ar", "tan(alpha_r)", null, "tan α′ of the return face");
            c.Assign("retention_force", "P * (mu + tan_ar) / (1 - mu * tan_ar)", "N", "Separation force W′ = P·(μ + tan α′)/(1 − μ·tan α′)", Ref);
            c.Output("retention_force", "Separation force");
            c.Output("self_locking", "Self-locking", new TextValue("No"));

            if (c.Has("n_latch"))
            {
                c.Input("n_latch");
                var n = c.Si("n_latch");

                if (Math.Abs(n - Math.Round(n)) > 1e-9)
                {
                    c.Block("n_latch", "n_latch: the number of latches must be a whole number.");
                    return;
                }

                c.Assign("F_hold", "n_latch * retention_force", "N", "Force the latches hold together: n × the separation force W′ of one (equal sharing assumed)", Ref);
                c.Output("F_hold", "Holding force of the latches");

                if (c.Has("F_req"))
                {
                    c.Input("F_req");
                    c.Assign("u_hold", "F_req / F_hold", null, "Utilisation of the latches' holding force");
                    c.Output("u_hold", "Holding utilisation");
                    c.CheckUtilisation("Latches hold the load: F_req ≤ n·W′", "u_hold", Ref);
                }
            }
            else if (c.Has("F_req"))
            {
                c.Warn("hold-unused", "F_req: a force to hold was given without n_latch, so the holding check is not made.");
            }
        }
    }

    private static string F(double v) => double.IsFinite(v) ? CalcValue.FormatNumber(v, 5) : "—";
}
