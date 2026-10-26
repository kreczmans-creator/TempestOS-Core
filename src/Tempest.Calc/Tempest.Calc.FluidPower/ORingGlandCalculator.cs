using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Analysis;
using Tempest.Calc.Engine.Analysis.ToleranceStack;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.FluidPower;

/// <summary>
/// F3 — O-ring and seal gland. Squeeze, stretch, gland fill and extrusion gap of a radial piston, radial rod or face O-ring
/// seal from the ring and groove dimensions with their tolerances, at the cold and shrunk and the hot and swollen ends of
/// the temperature and swell range. The worst case and the capability indices run through the tolerance-stack engine
/// (<see cref="ToleranceStackEngine"/>, the engine behind M1): the worst case as ranges of linear stacks, the capability on
/// each criterion linearised about the mean dimensions; the seeded Monte Carlo runs the engine's sampler on the exact
/// non-linear criterion. Measured groove distributions (mean, σ, distribution) replace the
/// toleranced dimensions from an optional table, so W2 can feed it later. Every figure the user must supply from a handbook
/// (ring sizes, squeeze and fill bands, the extrusion limit against pressure and hardness) is an input or a record; no AS568
/// or ISO 3601 size table, gland table or extrusion chart is shipped.
/// </summary>
public sealed class ORingGlandCalculator : ICalcCalculator
{
    public const string Id = "fp.oring-gland";

    /// <summary>The record kind of a material from the W3 library (it fills <c>alpha</c> and <c>nu</c>).</summary>
    public const string MaterialKind = "material.properties";

    public const string TypePiston = "Piston (radial)";
    public const string TypeRod = "Rod (radial)";
    public const string TypeFace = "Face (axial)";

    public const string PressureExternal = "External (ring ID seated on the groove)";
    public const string PressureInternal = "Internal (ring OD seated on the groove OD)";

    public const string DutyStatic = "Static";
    public const string DutyDynamic = "Dynamic";

    public const string RelationNone = "None";
    public const string RelationIncompressible = "Incompressible rubber (derived)";
    public const string RelationParker = "Parker ORD 5700 section 3.5 quadratic (as cited)";
    public const string RelationLinear = "Linear in stretch, nu times X (derived)";

    public const string DistributionNormal = "Normal";
    public const string DistributionUniform = "Uniform";
    public const string DistributionTriangular = "Triangular";

    private static readonly string[] Distributions = [DistributionNormal, DistributionUniform, DistributionTriangular];

    /// <summary>The symbols the measured table names its dimensions by.</summary>
    private static readonly string[] MeasuredDims = ["W", "d_r", "D_s", "D_g", "D_c", "h_f", "b", "e_f"];

    private static readonly IReadOnlyList<CalcColumnSpec> MeasuredColumns =
    [
        new("dim", "Dimension", CalcInputKind.Choice, Choices: MeasuredDims),
        new("mean", "Measured mean", CalcInputKind.Quantity, "mm", HardMin: 0.01, HardMax: 2000),
        new("sigma", "Measured standard deviation", CalcInputKind.Quantity, "mm", HardMin: 0, HardMax: 10),
        new("dist", "Distribution", CalcInputKind.Choice, Choices: Distributions, IsOptional: true),
    ];

    private static readonly IReadOnlyList<CalcColumnSpec> ExtrusionColumns =
    [
        new("H", "Hardness, Shore A", CalcInputKind.Number, Min: 40, Max: 95, HardMin: 20, HardMax: 100),
        new("p", "Pressure", CalcInputKind.Quantity, "bar", HardMin: 0, HardMax: 2000),
        new("gap", "Largest permitted gap (diametral for a radial seal, axial for a face seal)", CalcInputKind.Quantity, "mm", HardMin: 0, HardMax: 5),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.4.0", CalcDiscipline.FluidPower, "O-ring and seal gland",
        "Squeeze, stretch, gland fill and extrusion gap of a radial piston, radial rod or face O-ring seal, from the ring and groove dimensions with their tolerances, at the cold and shrunk and at the hot and swollen end of the temperature and swell range. The ring's cross-section shrinks with stretch by a relation you choose (none, constant-volume rubber, the quadratic cited from Parker ORD 5700 section 3.5, or linear in stretch). The worst case and the capability indices run through the tolerance-stack engine of M1, and the seeded Monte Carlo of the engine runs the exact non-linear model and reports the fraction outside each limit. A face seal is entered with the pressure on its outside or its inside. Measured groove distributions (from the scan import) can replace the toleranced dimensions. Ring sizes, the squeeze and fill bands and the extrusion limit are your own records: no O-ring size table, gland table or extrusion chart is shipped.",
        "Ring stretch: X = (g·D_ref)/(f·d) − 1, D_ref the diameter the ring ID sits on (groove root for a piston or face seal, rod for a rod seal); f = (1 + α(T − 20 °C))(1 + swell)^(1/3) the ring and g = 1 + α_m(T − 20 °C) the groove metal\n" +
        "Ring cross-section: W_eff = f·W·(1 − r(X))/√(1 − c), r(X) the chosen relation (none; 1 − 1/√(1+X); Y = 0.56 + 0.59X − 0.0046X² per cent as cited, used for about 3 to 25 % stretch only; ν·X) and c the compression of the ring outside diameter where it is held (rod seal in the housing groove; internal-pressure face seal on the groove outer wall), c = 1 − g·D_g/(f·(d + 2W))\n" +
        "Depth and gap: piston h = (D_s − D_g)/2, gap = D_s − D_c; rod h = (D_g − D_s)/2, gap = D_c − D_s; face h and gap entered directly (all times g)\n" +
        "Ring squeeze: q = 1 − g·h/W_eff, minimum at the cold end from the largest depth over the smallest W_eff, maximum at the hot end\n" +
        "Gland fill: ring area over the groove area g·b·g·h − 2(1 − π/4)(g·r_g)²; maximum at the hot end on a volume basis, (π/4)·(f·W)²·(f/g) with f³ = (1 + αΔT)³(1 + s) (an outside diameter held in the groove: (π/4)·(f·W)²/(1 − c)), minimum at the cold end (π/4)·W_eff²\n" +
        "Worst case: each range from the engine's worst-case stack of the dimension (Σ|coefficient|·half-band), combined at the corner that is worst for the quantity\n" +
        "Monte Carlo: the engine's seeded Monte Carlo on the exact criterion, each dimension from its own distribution; the fraction below the lower and above the upper limit. The capability index C_pk uses the criterion linearised about the mean dimensions (secant slope across each band)\n" +
        "Extrusion gap: the largest gap over the installed, cold and hot temperatures (the sealing-surface part and the groove metal each with its own expansion) plus the bore dilation under pressure, against the permitted gap from your record (interpolated in hardness and pressure) or the value entered; with neither the check fails (not verified)",
        [
            new CalcInputSpec("type", "Gland arrangement", CalcInputKind.Choice, Choices: [TypePiston, TypeRod, TypeFace], DefaultChoice: TypePiston, Group: "Seal"),
            new CalcInputSpec("duty", "Static or dynamic duty (selects the squeeze band)", CalcInputKind.Choice, Choices: [DutyStatic, DutyDynamic], DefaultChoice: DutyStatic, Group: "Seal"),
            new CalcInputSpec("face_p", "Face seal only: which side the pressure acts on (external: the ring ID seats on the groove inner wall and is stretched; internal: the ring OD seats on the groove outer wall and is compressed)", CalcInputKind.Choice, Choices: [PressureExternal, PressureInternal], DefaultChoice: PressureExternal, Group: "Seal"),

            new CalcInputSpec("d_r", "Ring inside diameter d (mean of the supplier's tolerance)", CalcInputKind.Quantity, "mm", Default: 50, Min: 3, Max: 500, HardMin: 1, HardMax: 1000, Group: "Ring"),
            new CalcInputSpec("d_r_tol", "Ring inside diameter tolerance, plus or minus", CalcInputKind.Quantity, "mm", Default: 0.4, Min: 0, Max: 2, HardMin: 0, HardMax: 5, Group: "Ring"),
            new CalcInputSpec("W", "Ring cross-section diameter W", CalcInputKind.Quantity, "mm", Default: 3.5, Min: 1, Max: 10, HardMin: 0.5, HardMax: 20, Group: "Ring"),
            new CalcInputSpec("W_tol", "Ring cross-section tolerance, plus or minus", CalcInputKind.Quantity, "mm", Default: 0.1, Min: 0, Max: 0.4, HardMin: 0, HardMax: 2, Group: "Ring"),

            new CalcInputSpec("D_s", "Sealing-surface diameter: the bore of a piston seal, the rod of a rod seal (not used for a face seal)", CalcInputKind.Quantity, "mm", Default: 56.9, Min: 5, Max: 1000, HardMin: 1, HardMax: 2000, Group: "Gland"),
            new CalcInputSpec("D_s_u", "Sealing-surface diameter, upper deviation", CalcInputKind.Quantity, "mm", Default: 0.046, Min: -0.5, Max: 0.5, HardMin: -5, HardMax: 5, Group: "Gland"),
            new CalcInputSpec("D_s_l", "Sealing-surface diameter, lower deviation", CalcInputKind.Quantity, "mm", Default: 0, Min: -0.5, Max: 0.5, HardMin: -5, HardMax: 5, Group: "Gland"),
            new CalcInputSpec("D_g", "Groove root diameter: on the piston, in the housing of a rod seal, or for a face seal the diameter the ring seats on (the groove inner wall for external pressure, the groove outer wall for internal pressure)", CalcInputKind.Quantity, "mm", Default: 51.5, Min: 5, Max: 1000, HardMin: 1, HardMax: 2000, Group: "Gland"),
            new CalcInputSpec("D_g_u", "Groove root diameter, upper deviation", CalcInputKind.Quantity, "mm", Default: 0, Min: -0.5, Max: 0.5, HardMin: -5, HardMax: 5, Group: "Gland"),
            new CalcInputSpec("D_g_l", "Groove root diameter, lower deviation", CalcInputKind.Quantity, "mm", Default: -0.05, Min: -0.5, Max: 0.5, HardMin: -5, HardMax: 5, Group: "Gland"),
            new CalcInputSpec("D_c", "Mating part that sets the extrusion gap: the piston outside diameter, or the housing bore round the rod (not used for a face seal)", CalcInputKind.Quantity, "mm", Default: 56.9, Min: 5, Max: 1000, HardMin: 1, HardMax: 2000, Group: "Gland"),
            new CalcInputSpec("D_c_u", "Mating part diameter, upper deviation", CalcInputKind.Quantity, "mm", Default: -0.03, Min: -0.5, Max: 0.5, HardMin: -5, HardMax: 5, Group: "Gland"),
            new CalcInputSpec("D_c_l", "Mating part diameter, lower deviation", CalcInputKind.Quantity, "mm", Default: -0.06, Min: -0.5, Max: 0.5, HardMin: -5, HardMax: 5, Group: "Gland"),
            new CalcInputSpec("h_f", "Face-seal groove depth (face seal only; the ring is compressed over h_f + e_f)", CalcInputKind.Quantity, "mm", Default: 2.7, Min: 0.3, Max: 20, HardMin: 0.1, HardMax: 50, Group: "Gland"),
            new CalcInputSpec("h_f_u", "Face-seal groove depth, upper deviation", CalcInputKind.Quantity, "mm", Default: 0.05, Min: -0.5, Max: 0.5, HardMin: -5, HardMax: 5, Group: "Gland"),
            new CalcInputSpec("h_f_l", "Face-seal groove depth, lower deviation", CalcInputKind.Quantity, "mm", Default: 0, Min: -0.5, Max: 0.5, HardMin: -5, HardMax: 5, Group: "Gland"),
            new CalcInputSpec("e_f", "Face-seal gap between the flanges (face seal only): the extrusion gap, and it adds to the compression height h_f + e_f", CalcInputKind.Quantity, "mm", Default: 0.05, Min: 0, Max: 2, HardMin: 0, HardMax: 10, Group: "Gland"),
            new CalcInputSpec("e_f_u", "Face-seal gap, upper deviation", CalcInputKind.Quantity, "mm", Default: 0.05, Min: -0.5, Max: 0.5, HardMin: -5, HardMax: 5, Group: "Gland"),
            new CalcInputSpec("e_f_l", "Face-seal gap, lower deviation", CalcInputKind.Quantity, "mm", Default: 0, Min: -0.5, Max: 0.5, HardMin: -5, HardMax: 5, Group: "Gland"),
            new CalcInputSpec("b", "Groove width b", CalcInputKind.Quantity, "mm", Default: 4.8, Min: 1, Max: 20, HardMin: 0.3, HardMax: 50, Group: "Gland"),
            new CalcInputSpec("b_u", "Groove width, upper deviation", CalcInputKind.Quantity, "mm", Default: 0.1, Min: -0.5, Max: 0.5, HardMin: -5, HardMax: 5, Group: "Gland"),
            new CalcInputSpec("b_l", "Groove width, lower deviation", CalcInputKind.Quantity, "mm", Default: 0, Min: -0.5, Max: 0.5, HardMin: -5, HardMax: 5, Group: "Gland"),
            new CalcInputSpec("ecc", "Gland eccentricity or run-out (one-sided offset; it deepens the gland by this much where the squeeze is least and shallows it where the squeeze is most; 0 if none)", CalcInputKind.Quantity, "mm", Default: 0, Min: 0, Max: 0.5, HardMin: 0, HardMax: 5, Group: "Gland"),
            new CalcInputSpec("r_g", "Groove bottom corner radius", CalcInputKind.Quantity, "mm", Default: 0.2, Min: 0, Max: 1, HardMin: 0, HardMax: 5, Group: "Gland"),

            new CalcInputSpec("T_min", "Lowest operating temperature (the ring sits at 20 °C when installed)", CalcInputKind.Quantity, "degC", Default: -20, Min: -60, Max: 100, HardMin: -150, HardMax: 400, Group: "Environment"),
            new CalcInputSpec("T_max", "Highest operating temperature", CalcInputKind.Quantity, "degC", Default: 80, Min: -40, Max: 200, HardMin: -150, HardMax: 400, Group: "Environment"),
            new CalcInputSpec("alpha", "Ring linear thermal expansion coefficient α (an indicative elastomer figure; take yours from the compound or the material library)", CalcInputKind.Quantity, "1/K", Default: 1.5e-4, Min: 5e-5, Max: 3e-4, HardMin: 0, HardMax: 1e-3, Group: "Environment", MaterialProperty: "alpha"),
            new CalcInputSpec("alpha_m", "Groove metal linear thermal expansion coefficient α_m (the part that carries the groove; one material record per sheet, so this one is entered from the metal's data sheet)", CalcInputKind.Quantity, "1/K", Default: 1.2e-5, Min: 5e-6, Max: 3e-5, HardMin: 0, HardMax: 1e-4, Group: "Environment"),
            new CalcInputSpec("alpha_s", "Linear thermal expansion coefficient of the part that carries the sealing surface (the bore of a piston seal, the rod of a rod seal), if it differs from the groove metal's (leave blank to use α_m); it sets the extrusion gap over temperature", CalcInputKind.Quantity, "1/K", Min: 5e-6, Max: 3e-5, HardMin: 0, HardMax: 1e-4, IsOptional: true, Group: "Environment"),
            new CalcInputSpec("s_min", "Smallest volume change of the ring in the fluid (negative for shrinkage; 0 if none)", CalcInputKind.Quantity, "%", Default: 0, Min: -10, Max: 0, HardMin: -20, HardMax: 100, Group: "Environment"),
            new CalcInputSpec("s_max", "Largest volume swell of the ring in the fluid", CalcInputKind.Quantity, "%", Default: 5, Min: 0, Max: 25, HardMin: -20, HardMax: 100, Group: "Environment"),

            new CalcInputSpec("red", "Cross-section reduction with stretch", CalcInputKind.Choice, Choices: [RelationNone, RelationIncompressible, RelationParker, RelationLinear], DefaultChoice: RelationIncompressible, Group: "Stretch"),
            new CalcInputSpec("nu", "Poisson ratio of the ring, for the linear relation", CalcInputKind.Number, Default: 0.49, Min: 0.3, Max: 0.5, HardMin: 0, HardMax: 0.5, Group: "Stretch", MaterialProperty: "nu"),

            new CalcInputSpec("q_lo_s", "Static duty: lowest acceptable squeeze (from your handbook)", CalcInputKind.Quantity, "%", Default: 15, Min: 5, Max: 30, HardMin: 0, HardMax: 60, Group: "Acceptance"),
            new CalcInputSpec("q_hi_s", "Static duty: highest acceptable squeeze", CalcInputKind.Quantity, "%", Default: 30, Min: 10, Max: 45, HardMin: 0, HardMax: 80, Group: "Acceptance"),
            new CalcInputSpec("q_lo_d", "Dynamic duty: lowest acceptable squeeze", CalcInputKind.Quantity, "%", Default: 10, Min: 5, Max: 25, HardMin: 0, HardMax: 60, Group: "Acceptance"),
            new CalcInputSpec("q_hi_d", "Dynamic duty: highest acceptable squeeze", CalcInputKind.Quantity, "%", Default: 25, Min: 10, Max: 35, HardMin: 0, HardMax: 80, Group: "Acceptance"),
            new CalcInputSpec("fill_lo", "Gland fill: lowest acceptable", CalcInputKind.Quantity, "%", Default: 60, Min: 40, Max: 80, HardMin: 0, HardMax: 100, Group: "Acceptance"),
            new CalcInputSpec("fill_hi", "Gland fill: highest acceptable", CalcInputKind.Quantity, "%", Default: 85, Min: 60, Max: 95, HardMin: 0, HardMax: 100, Group: "Acceptance"),
            new CalcInputSpec("x_lo", "Installed stretch: lowest acceptable (negative allows a ring that is compressed on its seat)", CalcInputKind.Quantity, "%", Default: 0, Min: -5, Max: 5, HardMin: -50, HardMax: 50, Group: "Acceptance"),
            new CalcInputSpec("x_hi", "Installed stretch: highest acceptable (the installation rule is up to 6 % on a piston groove); also applied at the cold end", CalcInputKind.Quantity, "%", Default: 6, Min: 1, Max: 10, HardMin: 0.1, HardMax: 100, Group: "Acceptance"),
            new CalcInputSpec("od_hi", "Ring outside-diameter compression: highest acceptable, in a rod-seal groove or against the groove outer wall of an internal-pressure face seal (your record; the default is a placeholder equal to the stretch limit, not a handbook value)", CalcInputKind.Quantity, "%", Default: 6, Min: 1, Max: 10, HardMin: 0.1, HardMax: 100, Group: "Acceptance"),

            new CalcInputSpec("H_A", "Ring hardness, Shore A (for the extrusion record)", CalcInputKind.Number, Default: 70, Min: 40, Max: 95, HardMin: 20, HardMax: 100, Group: "Extrusion"),
            new CalcInputSpec("p_op", "Operating pressure (for the extrusion record, gauge)", CalcInputKind.Quantity, "bar", Default: 160, Min: 0, Max: 700, HardMin: 0, HardMax: 2000, Group: "Extrusion", PressureBasis: CalcInputSpec.BasisGauge),
            new CalcInputSpec("dD_p", "Diametral growth under the operating pressure of the bore (piston seal) or of the housing bore round the rod (rod seal), from your cylinder or housing calculation; added to the extrusion gap (0 if not known)", CalcInputKind.Quantity, "mm", Default: 0, Min: 0, Max: 0.5, HardMin: 0, HardMax: 5, Group: "Extrusion"),
            new CalcInputSpec("gap_allow", "Largest permitted extrusion gap at this pressure and hardness, if you have it as one value: diametral (the clearance D_s − D_c) for a radial seal, axial for a face seal; a radial chart value is half of it", CalcInputKind.Quantity, "mm", Min: 0.01, Max: 1, HardMin: 0, HardMax: 5, IsOptional: true, Group: "Extrusion"),
            new CalcInputSpec("ext", "Permitted extrusion gap against hardness and pressure (your own record)", CalcInputKind.Table, Columns: ExtrusionColumns, MaxRows: 100, IsOptional: true, Group: "Extrusion"),

            new CalcInputSpec("n_mc", "Monte Carlo samples", CalcInputKind.Number, Default: 10_000, Min: 1_000, Max: 100_000, HardMin: 1, HardMax: CalcRunner.MaxCalculatorSamples, Group: "Statistics"),
            new CalcInputSpec("seed", "Monte Carlo seed", CalcInputKind.Number, Default: 1, HardMin: 0, HardMax: 4_294_967_295, Group: "Statistics"),
            new CalcInputSpec("k_sigma", "Standard deviations per half-band (normal tolerances and measured normals)", CalcInputKind.Number, Default: 3, Min: 1, Max: 6, HardMin: 0.1, HardMax: 100, Group: "Statistics"),
            new CalcInputSpec("dist", "Distribution of the toleranced dimensions", CalcInputKind.Choice, Choices: Distributions, DefaultChoice: DistributionNormal, Group: "Statistics"),
            new CalcInputSpec("measured", "Measured groove or ring dimensions (mean, standard deviation, distribution), replacing the toleranced ones", CalcInputKind.Table, Columns: MeasuredColumns, MaxRows: 8, IsOptional: true, Group: "Statistics"),
        ],
        [
            new CalcAssumption("The gland is a plain rectangular groove with a bottom corner radius; the ring is a circle of the stated cross-section. Edge breaks, groove wall angles and surface finish are not modelled.", "Closed-form gland geometry; no gland table."),
            new CalcAssumption("Squeeze is the compressed fraction of the effective cross-section W_eff, which is the nominal section after thermal expansion, swell and the cross-section reduction of the stretch. The reduction relation is chosen by the user; the quadratic of Parker ORD 5700 section 3.5 is implemented as quoted second-hand, used for about 3 to 25 % stretch only, and is not confirmed against the handbook. Where the ring outside diameter is held (a rod seal in its housing groove, an internal-pressure face seal), the compression c thickens the section to W/√(1 − c).", "The relation is an input choice; the default is the constant-volume rubber relation, derived."),
            new CalcAssumption("The cold end is the lowest temperature with the least swell and sets the minimum squeeze and fill; the hot end is the highest temperature with the most swell and sets the maximum. This assumes the ring expands more than the groove metal (alpha above alpha_m).", "Bounding cases of a monotonic model."),
            new CalcAssumption("The worst case combines each dimension at its extreme independently. Where one dimension appears twice (the groove root diameter sets both the depth and the stretch of a piston seal) the worst case is slightly conservative.", "Independent ends of linear stacks."),
            new CalcAssumption("The Monte Carlo samples each dimension from its own distribution about its mean and evaluates each criterion exactly (the non-linear model) on the engine's seeded sampler. It counts the fraction outside each limit at the cold or hot end; it does not model pressure, friction or compression set. The capability index C_pk uses the first-order (secant) linearisation.", "Exact model on the engine's seeded sampler."),
            new CalcAssumption("The gland fill at the hot end is on a volume basis: a ring held circumferentially by the groove takes the whole volume change of thermal expansion and swell in its section, (1 + αΔT)³(1 + s), while the groove circumference grows with the groove metal. This is higher than scaling the section area isotropically.", "Volume conservation of the held ring."),
            new CalcAssumption("The extrusion gap is the largest diametral clearance of a radial seal (the axial gap of a face seal) over the installed, cold and hot temperatures, the sealing-surface part and the groove metal each with its own expansion coefficient, plus the diametral growth of the bore under pressure that the user enters (0 if none). The permitted gap is the user's record; the sheet never states a handbook value. With no permitted gap entered the extrusion check fails (not verified) while the operating pressure is above zero.", "Extrusion limits are licensed data and are not shipped."),
        ],
        [
            new CalcClauseRef("Parker O-Ring Handbook ORD 5700", "Section 3.5", "Cross-section reduction with stretch (relation quoted second-hand; not confirmed)"),
            new CalcClauseRef("Parker PTD 5705", "—", "O-ring installation guidance (stretch rule quoted second-hand; not confirmed)"),
            new CalcClauseRef("ISO 3601-1", "—", "O-rings: inside diameters, cross-sections, tolerances and size codes"),
            new CalcClauseRef("ISO 3601-2", "—", "O-rings: housing dimensions for general applications"),
        ],
        [
            new CalcApplicabilityRule("geometry", "Every dimension stays positive across its band, the deviations are in order, the gland has a positive depth and area and the ring section does not vanish.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("limits", "Each lower limit is below its upper limit and the temperature and swell ranges are in order.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("measured", "A measured row names a dimension the arrangement does not use (a name that is not a dimension at all blocks).", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("extrusion", "No permitted extrusion gap is entered (no record rows and no value): the extrusion is not verified and the check fails while the operating pressure is above zero.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("extrusion-range", "The pressure or hardness lies outside the extrusion record; the check fails because the record cannot confirm it.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("expansion", "The ring expands less than the groove metal: the cold and hot ends are not the bounding cases.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("stretch", "The installed stretch is far beyond installation practice, or the Parker quadratic is used outside about 3 to 25 % stretch.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("installed-state", "The operating temperature range does not include the installation temperature 20 °C: the installed state can have a smaller squeeze than the cold end.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("contact", "The minimum squeeze is zero or negative: the ring can lose contact.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("face", "A face seal is normally a static seal.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("worst-case", "The exact corner is lower than the stack worst case; the stack is not conservative for this geometry.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
        ],
        [
            new CalcTestCase("ka-thermal-swell", "Piston seal with the constant-volume reduction, thermal expansion and swell: the defaults (analytic)",
                new CalcReferenceSource("Analytic worst case, recomputed in python3", null, "cold end f = (1 + 1.5e-4·(−40))·1 = 0.994, g = 1 + 1.2e-5·(−40) = 0.99952, largest stretch 4.40725 %, reduction 1 − 1/√1.0440725 = 2.13336 %; hot end f = (1 + 1.5e-4·60)·1.05^(1/3) = 1.025544, g = 1.00072, smallest stretch −0.38766 %, reduction −0.19439 %; squeeze 16.9560 % to 26.9574 %; fill 63.9511 % at the cold end and, on the volume basis (π/4)·(f·W_max)²·(f/g)/A_g,min, 84.6429 % at the hot end (f/g = 1.024806: the isotropic section area would give 82.5941 %); gap 0.106 mm as dimensioned, 0.106·1.00072 = 0.106076 mm at 80 °C (the bore and the piston expand alike)"),
                [CalcInputValue.Quantity("gap_allow", 0.15, "mm")],
                [
                    new CalcExpectedValue("squeeze_min", 16.9560030299, "%", 1e-9), new CalcExpectedValue("squeeze_max", 26.9574115556, "%", 1e-9),
                    new CalcExpectedValue("fill_max", 84.6429192000, "%", 1e-9), new CalcExpectedValue("fill_min", 63.9511150431, "%", 1e-9),
                    new CalcExpectedValue("squeeze_nom", 21.0318008103, "%", 1e-9), new CalcExpectedValue("red_nom", 1.4431477137, "%", 1e-9),
                    new CalcExpectedValue("stretch", 3.8306451613, "%", 1e-9), new CalcExpectedValue("X_c", 4.4072499513, "%", 1e-9), new CalcExpectedValue("gap_max", 0.10607632, "mm", 1e-9),
                ]),
            new CalcTestCase("ka-worst-case-hand", "Piston seal, hand-calculated worst case: no reduction, no thermal change (analytic)",
                new CalcReferenceSource("Analytic worst case by hand, recomputed in python3", null, "depth h = (D_s − D_g)/2 from 2.700 to 2.748 mm and W from 3.4 to 3.6 mm: squeeze_min = 1 − 2.748/3.4 = 19.17647 %, squeeze_max = 1 − 2.700/3.6 = 25 %; groove 4.8 to 4.9 mm wide, corner term 2(1 − π/4)·0.2² = 0.017168 mm²: fill_max = (π/4)·3.6²/(4.8·2.700 − 0.017168) = 78.6440 %, fill_min = (π/4)·3.4²/(4.9·2.748 − 0.017168) = 67.5132 %; stretch 51.5/49.6 − 1 = 3.83065 % and 51.45/50.4 − 1 = 2.08333 %; gap 56.946 − 56.84 = 0.106 mm"),
                [
                    CalcInputValue.Quantity("T_min", 20, "degC"), CalcInputValue.Quantity("T_max", 20, "degC"),
                    CalcInputValue.Quantity("s_min", 0, "%"), CalcInputValue.Quantity("s_max", 0, "%"),
                    CalcInputValue.Choice("red", RelationNone), CalcInputValue.Quantity("gap_allow", 0.15, "mm"),
                    CalcInputValue.Of("n_mc", 2_000),
                ],
                [
                    new CalcExpectedValue("squeeze_min", 19.1764705882, "%", 1e-9), new CalcExpectedValue("squeeze_max", 25.0, "%", 1e-9),
                    new CalcExpectedValue("fill_max", 78.6439962535, "%", 1e-9), new CalcExpectedValue("fill_min", 67.5132455669, "%", 1e-9),
                    new CalcExpectedValue("stretch", 3.8306451613, "%", 1e-9), new CalcExpectedValue("stretch_min", 2.0833333333, "%", 1e-9),
                    new CalcExpectedValue("squeeze_nom", 22.1714285714, "%", 1e-9), new CalcExpectedValue("gap_max", 0.106, "mm", 1e-9),
                    new CalcExpectedValue("gap_min", 0.03, "mm", 1e-9),
                ]),
            new CalcTestCase("ka-rod-seal", "Rod seal: ring stretched on the rod, ring OD compressed in the housing groove (analytic)",
                new CalcReferenceSource("Analytic worst case, recomputed in python3", null, "rod 40 −0.025/−0.050, groove root 44.8 +0.062/0, ring 39.5 ± 0.35 × 3.0 ± 0.1, housing bore 40 +0.039/0: depth (D_g − D_s)/2, stretch on the rod 39.9625/39.5 − 1 = 1.17 % mean, gap D_c − D_s from 0.025 to 0.089 mm (0.08906 mm at 80 °C), OD compression 2.714 % at the worst corner installed; the section is thickened by 1/√(1 − c) with c = 1 − g·D_g/(f·OD) (hot end c = 5.069 %, the cold end none), so the largest squeeze is 26.820 % and the volume-basis hot fill (π/4)·(f·W)²/(1 − c)/A_g is 84.566 %"),
                [
                    CalcInputValue.Choice("type", TypeRod),
                    CalcInputValue.Quantity("d_r", 39.5, "mm"), CalcInputValue.Quantity("d_r_tol", 0.35, "mm"), CalcInputValue.Quantity("W", 3.0, "mm"), CalcInputValue.Quantity("W_tol", 0.1, "mm"),
                    CalcInputValue.Quantity("D_s", 40, "mm"), CalcInputValue.Quantity("D_s_u", -0.025, "mm"), CalcInputValue.Quantity("D_s_l", -0.05, "mm"),
                    CalcInputValue.Quantity("D_g", 44.8, "mm"), CalcInputValue.Quantity("D_g_u", 0.062, "mm"), CalcInputValue.Quantity("D_g_l", 0, "mm"),
                    CalcInputValue.Quantity("D_c", 40, "mm"), CalcInputValue.Quantity("D_c_u", 0.039, "mm"), CalcInputValue.Quantity("D_c_l", 0, "mm"),
                    CalcInputValue.Quantity("b", 4.1, "mm"), CalcInputValue.Quantity("b_u", 0.1, "mm"), CalcInputValue.Quantity("b_l", 0, "mm"),
                    CalcInputValue.Of("n_mc", 2_000),
                ],
                [
                    new CalcExpectedValue("squeeze_min", 13.7088267470, "%", 1e-9), new CalcExpectedValue("squeeze_max", 26.8202916965, "%", 1e-9),
                    new CalcExpectedValue("fill_max", 84.5655009226, "%", 1e-9), new CalcExpectedValue("fill_min", 61.7816677682, "%", 1e-9),
                    new CalcExpectedValue("stretch", 2.1072796935, "%", 1e-9), new CalcExpectedValue("stretch_nom", 1.1708860759, "%", 1e-9),
                    new CalcExpectedValue("od_comp", 2.7144408252, "%", 1e-9), new CalcExpectedValue("od_comp_nom", 1.4703296703, "%", 1e-9),
                    new CalcExpectedValue("squeeze_nom", 18.9869065887, "%", 1e-9), new CalcExpectedValue("c_h", 5.06929787, "%", 1e-8),
                    new CalcExpectedValue("gap_max", 0.08906408, "mm", 1e-9),
                ]),
            new CalcTestCase("ka-face-seal", "Face seal: ring stretched onto the groove seat, depth and gap entered directly (analytic)",
                new CalcReferenceSource("Analytic worst case, recomputed in python3", null, "ring 60 ± 0.5 × 3.5 ± 0.1 on a 60.8 +0.05/0 seat, groove 2.75 +0.05/0 deep and 4.9 +0.1/0 wide, flange gap 0.05 +0.05/0: stretch 60.85/59.5 − 1 = 2.269 % at most, gap 0.10 mm at most; the ring is compressed over the groove depth plus the flange gap, h_f + e_f = 2.80 to 2.90 mm at the cold end (squeeze_min) and 2.75 + 0.05 = 2.80 mm at the smallest (squeeze_max), so squeeze_min = 1 − (1 − 0.160233)·2.90/2.80 and squeeze_max = 1 − (1 − 0.261855)·2.80/2.75 against the 16.0233 % and 26.1855 % of the depth alone, and the groove area b·(h_f + e_f) − 2(1 − π/4)·r² grows with it"),
                [
                    CalcInputValue.Choice("type", TypeFace),
                    CalcInputValue.Quantity("d_r", 60, "mm"), CalcInputValue.Quantity("d_r_tol", 0.5, "mm"), CalcInputValue.Quantity("W", 3.5, "mm"), CalcInputValue.Quantity("W_tol", 0.1, "mm"),
                    CalcInputValue.Quantity("D_g", 60.8, "mm"), CalcInputValue.Quantity("D_g_u", 0.05, "mm"), CalcInputValue.Quantity("D_g_l", 0, "mm"),
                    CalcInputValue.Quantity("h_f", 2.75, "mm"), CalcInputValue.Quantity("h_f_u", 0.05, "mm"), CalcInputValue.Quantity("h_f_l", 0, "mm"),
                    CalcInputValue.Quantity("e_f", 0.05, "mm"), CalcInputValue.Quantity("e_f_u", 0.05, "mm"), CalcInputValue.Quantity("e_f_l", 0, "mm"),
                    CalcInputValue.Quantity("b", 4.9, "mm"), CalcInputValue.Quantity("b_u", 0.1, "mm"), CalcInputValue.Quantity("b_l", 0, "mm"),
                    CalcInputValue.Of("n_mc", 2_000),
                ],
                [
                    new CalcExpectedValue("squeeze_min", 13.0241772653, "%", 1e-9), new CalcExpectedValue("squeeze_max", 24.8434037408, "%", 1e-9),
                    new CalcExpectedValue("fill_max", 79.9483699797, "%", 1e-9), new CalcExpectedValue("fill_min", 60.2886136783, "%", 1e-9),
                    new CalcExpectedValue("stretch", 2.2689075630, "%", 1e-9), new CalcExpectedValue("gap_max", 0.1, "mm", 1e-9),
                ]),
            new CalcTestCase("ka-face-seal-internal", "Face seal with the pressure inside: the ring OD seats on the groove outer wall and is compressed, not stretched (analytic)",
                new CalcReferenceSource("Analytic worst case, recomputed in python3", null, "ring 60 ± 0.5 × 3.5 ± 0.1 (OD 66.3 to 67.7 mm) on a 66.7 +0.05/0 groove outer wall, groove 2.75 +0.05/0 deep and 4.9 +0.1/0 wide, flange gap 0.05 +0.05/0: the OD stretch D_ref/OD − 1 is 66.75/66.3 − 1 = 0.679 % at most and 66.7/67.7 − 1 = −1.477 % at least, so the OD compression is 1.477 % at most (od_comp = (OD_max − D_g,min)/OD_max); the hot end compresses the ring OD by 3.862 % (f·OD against g·D_g) and thickens the section by 1/√(1 − c); the compression height is the groove depth plus the flange gap, h_f + e_f (2.80 to 2.90 mm), so squeeze_min = 1 − (1 − 0.166788)·2.90/2.80, squeeze_max = 1 − (1 − 0.269137)·2.80/2.75 and the fills follow the groove area b·(h_f + e_f) − 2(1 − π/4)·r², and squeeze_nom = 1 − 2.85/W_eff on the nominal 2.775 + 0.075 mm (20.8772 % with the depth alone); the depth-alone values were 16.6788 %, 26.9137 %, 63.4307 % and 82.6243 %"),
                [
                    CalcInputValue.Choice("type", TypeFace), CalcInputValue.Choice("face_p", PressureInternal),
                    CalcInputValue.Quantity("d_r", 60, "mm"), CalcInputValue.Quantity("d_r_tol", 0.5, "mm"), CalcInputValue.Quantity("W", 3.5, "mm"), CalcInputValue.Quantity("W_tol", 0.1, "mm"),
                    CalcInputValue.Quantity("D_g", 66.7, "mm"), CalcInputValue.Quantity("D_g_u", 0.05, "mm"), CalcInputValue.Quantity("D_g_l", 0, "mm"),
                    CalcInputValue.Quantity("h_f", 2.75, "mm"), CalcInputValue.Quantity("h_f_u", 0.05, "mm"), CalcInputValue.Quantity("h_f_l", 0, "mm"),
                    CalcInputValue.Quantity("e_f", 0.05, "mm"), CalcInputValue.Quantity("e_f_u", 0.05, "mm"), CalcInputValue.Quantity("e_f_l", 0, "mm"),
                    CalcInputValue.Quantity("b", 4.9, "mm"), CalcInputValue.Quantity("b_u", 0.1, "mm"), CalcInputValue.Quantity("b_l", 0, "mm"),
                    CalcInputValue.Of("n_mc", 2_000),
                ],
                [
                    new CalcExpectedValue("stretch", 0.6787330317, "%", 1e-9), new CalcExpectedValue("stretch_min", -1.4771048744, "%", 1e-9), new CalcExpectedValue("stretch_nom", -0.4104477612, "%", 1e-9),
                    new CalcExpectedValue("od_comp", 1.4771048744, "%", 1e-9), new CalcExpectedValue("od_comp_nom", 0.4104477612, "%", 1e-9), new CalcExpectedValue("c_h", 3.8619124, "%", 1e-8),
                    new CalcExpectedValue("squeeze_min", 13.7030179491, "%", 1e-9), new CalcExpectedValue("squeeze_max", 25.5848119507, "%", 1e-9),
                    new CalcExpectedValue("fill_min", 61.2408447515, "%", 1e-9), new CalcExpectedValue("fill_max", 81.1469962162, "%", 1e-9),
                    new CalcExpectedValue("squeeze_nom", 18.7387112741, "%", 1e-9), new CalcExpectedValue("X_c", 1.2378342453, "%", 1e-9),
                ]),
            new CalcTestCase("ka-gap-over-temperature", "Extrusion gap over temperature with a bore of another metal and the bore dilation under pressure (analytic)",
                new CalcReferenceSource("Analytic worst case, recomputed in python3", null, "the defaults with the bore (sealing surface) at α_s = 2.3e-5 and the steel piston at α_m = 1.2e-5, bore dilation 0.02 mm: gap = g_s·D_s − g_m·D_c with the largest at 80 °C, D_s,max = 56.946 and D_c,min = 56.84 mm: 1.00138·56.946 − 1.00072·56.84 = 0.14366 mm, plus 0.02 mm = 0.16366 mm (0.106 mm as dimensioned); smallest gap 0.0049496 mm at −20 °C; the depth is taken with each part at its own temperature, h = (g_s·D_s − g_m·D_g)/2, so the squeeze moves with α_s: 17.335 % to 26.450 %, fill 64.245 % to 84.058 %"),
                [
                    CalcInputValue.Quantity("alpha_s", 2.3e-5, "1/K"), CalcInputValue.Quantity("dD_p", 0.02, "mm"), CalcInputValue.Quantity("gap_allow", 0.2, "mm"),
                    CalcInputValue.Of("n_mc", 1_000),
                ],
                [new CalcExpectedValue("gap_max", 0.16366068, "mm", 1e-9), new CalcExpectedValue("gap_min", 0.0049496, "mm", 1e-9), new CalcExpectedValue("u_gap", 0.8183034, "", 1e-9),
                 new CalcExpectedValue("squeeze_min", 17.3347820634, "%", 1e-9), new CalcExpectedValue("squeeze_max", 26.4498064079, "%", 1e-9),
                 new CalcExpectedValue("fill_min", 64.2445202775, "%", 1e-9), new CalcExpectedValue("fill_max", 84.0579883229, "%", 1e-9)]),
            new CalcTestCase("ka-incompressible-reduction", "Cross-section reduction of constant-volume rubber at 4 % stretch (analytic)",
                new CalcReferenceSource("Analytic: r = 1 − 1/√(1 + X), derived from constant volume", null, "ring 50, seat 52 mm: X = 4 %, r = 1 − 1/√1.04 = 1.94193 %"),
                [
                    CalcInputValue.Quantity("d_r_tol", 0, "mm"),
                    CalcInputValue.Quantity("D_s", 57, "mm"), CalcInputValue.Quantity("D_g", 52, "mm"), CalcInputValue.Quantity("D_g_u", 0, "mm"), CalcInputValue.Quantity("D_g_l", 0, "mm"),
                    CalcInputValue.Choice("red", RelationIncompressible), CalcInputValue.Of("n_mc", 1_000),
                ],
                [new CalcExpectedValue("red_nom", 1.9419324309, "%", 1e-9), new CalcExpectedValue("stretch_nom", 4.0, "%", 1e-9), new CalcExpectedValue("squeeze_nom", 26.8217856664, "%", 1e-9)]),
            new CalcTestCase("ka-linear-reduction", "Cross-section reduction linear in stretch at 4 % stretch (analytic)",
                new CalcReferenceSource("Analytic: r = ν·X, small-strain Poisson contraction", null, "ring 50, seat 52 mm: X = 4 %, ν = 0.49: r = 1.96 %"),
                [
                    CalcInputValue.Quantity("d_r_tol", 0, "mm"),
                    CalcInputValue.Quantity("D_s", 57, "mm"), CalcInputValue.Quantity("D_g", 52, "mm"), CalcInputValue.Quantity("D_g_u", 0, "mm"), CalcInputValue.Quantity("D_g_l", 0, "mm"),
                    CalcInputValue.Choice("red", RelationLinear), CalcInputValue.Of("nu", 0.49), CalcInputValue.Of("n_mc", 1_000),
                ],
                [new CalcExpectedValue("red_nom", 1.96, "%", 1e-9)]),
            new CalcTestCase("ka-monte-carlo-seeded", "Seeded Monte Carlo, normal tolerances, Parker quadratic: the fractions outside each limit (analytic)",
                new CalcReferenceSource("Python3 re-implementation of the engine's xoshiro256** sampler on the exact non-linear criteria, and of the linearised stacks for C_pk", null, "20000 samples, seed 7: bands squeeze 18 to 26 %, fill 65 to 80 %, stretch 2.5 to 3.5 %, gap at most 0.09 mm; the dimensions are sampled in the order W, d, D_s, D_g, D_c, b, each from its own normal; the counts are 416, 23, 3199, 97, 1450 and 154 of 20000, the nearest sample to any limit being more than 3e-7 from it"),
                [
                    CalcInputValue.Choice("red", RelationParker),
                    CalcInputValue.Quantity("q_lo_s", 18, "%"), CalcInputValue.Quantity("q_hi_s", 26, "%"),
                    CalcInputValue.Quantity("fill_lo", 65, "%"), CalcInputValue.Quantity("fill_hi", 80, "%"),
                    CalcInputValue.Quantity("x_lo", 2.5, "%"), CalcInputValue.Quantity("x_hi", 3.5, "%"),
                    CalcInputValue.Quantity("gap_allow", 0.09, "mm"), CalcInputValue.Of("n_mc", 20_000), CalcInputValue.Of("seed", 7),
                ],
                [
                    new CalcExpectedValue("p_sq_min", 2.08, "%", 1e-9), new CalcExpectedValue("p_sq_max", 0.115, "%", 1e-9),
                    new CalcExpectedValue("p_fill_max", 15.995, "%", 1e-9), new CalcExpectedValue("p_fill_min", 0.485, "%", 1e-9),
                    new CalcExpectedValue("p_stretch", 7.25, "%", 1e-9), new CalcExpectedValue("p_gap", 0.77, "%", 1e-9),
                    new CalcExpectedValue("cpk_sq_min", 0.699319526, "", 1e-7), new CalcExpectedValue("cpk_sq_max", 0.979671603, "", 1e-7),
                    new CalcExpectedValue("cpk_fill_max", 0.331310178, "", 1e-7), new CalcExpectedValue("cpk_fill_min", 0.849060611, "", 1e-7),
                    new CalcExpectedValue("cpk_stretch", 0.545342867, "", 1e-7), new CalcExpectedValue("cpk_gap", 0.798834566, "", 1e-7),
                ]),
            new CalcTestCase("ka-monte-carlo-measured", "Seeded Monte Carlo, triangular tolerances with a measured groove and ring section (analytic)",
                new CalcReferenceSource("Python3 re-implementation of the engine's sampler on the exact non-linear criteria, and of the linearised stacks for C_pk", null, "10000 samples, seed 11, triangular tolerances; the groove root measured normal, mean 51.48, σ 0.012 mm (half-band 3σ), the ring section measured uniform, mean 3.505, σ 0.03 mm (half-band σ√3); counts 0, 0, 2476, 0, 1613 and 229 of 10000, the nearest sample to any limit being more than 4e-7 from it"),
                [
                    CalcInputValue.Choice("dist", DistributionTriangular),
                    CalcInputValue.Table("measured",
                    [
                        CalcRow.Of(("dim", new CalcCell(Text: "D_g")), ("mean", new CalcCell(51.48, "mm")), ("sigma", new CalcCell(0.012, "mm")), ("dist", new CalcCell(Text: DistributionNormal))),
                        CalcRow.Of(("dim", new CalcCell(Text: "W")), ("mean", new CalcCell(3.505, "mm")), ("sigma", new CalcCell(0.03, "mm")), ("dist", new CalcCell(Text: DistributionUniform))),
                    ]),
                    CalcInputValue.Quantity("q_lo_s", 18, "%"), CalcInputValue.Quantity("q_hi_s", 26, "%"),
                    CalcInputValue.Quantity("fill_lo", 65, "%"), CalcInputValue.Quantity("fill_hi", 80, "%"),
                    CalcInputValue.Quantity("x_lo", 2.5, "%"), CalcInputValue.Quantity("x_hi", 3.5, "%"),
                    CalcInputValue.Quantity("gap_allow", 0.09, "mm"), CalcInputValue.Of("n_mc", 10_000), CalcInputValue.Of("seed", 11),
                ],
                [
                    new CalcExpectedValue("squeeze_min", 18.1295724925, "%", 1e-9), new CalcExpectedValue("squeeze_max", 26.2969508948, "%", 1e-9),
                    new CalcExpectedValue("fill_max", 82.8770696497, "%", 1e-9), new CalcExpectedValue("fill_min", 65.8694064569, "%", 1e-9),
                    new CalcExpectedValue("stretch", 3.8629032258, "%", 1e-9),
                    new CalcExpectedValue("p_fill_max", 24.76, "%", 1e-9), new CalcExpectedValue("p_stretch", 16.13, "%", 1e-9), new CalcExpectedValue("p_gap", 2.29, "%", 1e-9),
                    new CalcExpectedValue("cpk_sq_min", 1.174984132, "", 1e-7), new CalcExpectedValue("cpk_sq_max", 0.934797809, "", 1e-7),
                    new CalcExpectedValue("cpk_fill_max", 0.289602138, "", 1e-7), new CalcExpectedValue("cpk_stretch", 0.454802341, "", 1e-7),
                    new CalcExpectedValue("cpk_gap", 0.652245692, "", 1e-7),
                ]),
            new CalcTestCase("ka-extrusion-record", "Permitted extrusion gap interpolated in hardness and pressure from a user record (analytic)",
                new CalcReferenceSource("Analytic interpolation of an illustrative record invented for this test (not a handbook chart)", null, "points 70 Shore A: 0 bar 0.30, 100 bar 0.15, 200 bar 0.08 mm; 90 Shore A: 0.45, 0.25, 0.15 mm; at 150 bar: 0.115 mm and 0.20 mm; at 80 Shore A the mean, 0.1575 mm"),
                [
                    CalcInputValue.Table("ext",
                    [
                        CalcRow.Of(("H", new CalcCell(70)), ("p", new CalcCell(0, "bar")), ("gap", new CalcCell(0.30, "mm"))),
                        CalcRow.Of(("H", new CalcCell(70)), ("p", new CalcCell(100, "bar")), ("gap", new CalcCell(0.15, "mm"))),
                        CalcRow.Of(("H", new CalcCell(70)), ("p", new CalcCell(200, "bar")), ("gap", new CalcCell(0.08, "mm"))),
                        CalcRow.Of(("H", new CalcCell(90)), ("p", new CalcCell(0, "bar")), ("gap", new CalcCell(0.45, "mm"))),
                        CalcRow.Of(("H", new CalcCell(90)), ("p", new CalcCell(100, "bar")), ("gap", new CalcCell(0.25, "mm"))),
                        CalcRow.Of(("H", new CalcCell(90)), ("p", new CalcCell(200, "bar")), ("gap", new CalcCell(0.15, "mm"))),
                    ]),
                    CalcInputValue.Of("H_A", 80), CalcInputValue.Quantity("p_op", 150, "bar"), CalcInputValue.Of("n_mc", 1_000),
                ],
                [new CalcExpectedValue("gap_allow", 0.1575, "mm", 1e-9), new CalcExpectedValue("u_gap", 0.10607632 / 0.1575, "", 1e-9)]),
            new CalcTestCase("ka-parker-quadratic", "Parker ORD 5700 section 3.5 quadratic at 4 % stretch, as quoted second-hand",
                new CalcReferenceSource("Parker Hannifin, O-Ring Handbook ORD 5700, section 3.5, relation as quoted second-hand", null, "Y = 0.56 + 0.59·4 − 0.0046·16 = 2.8464 %; arithmetic of the quoted relation, used here for about 3 to 25 % stretch only; to be checked against the owner's copy of the handbook, including what Y measures and the range it covers", Secondary: true),
                [
                    CalcInputValue.Quantity("d_r_tol", 0, "mm"),
                    CalcInputValue.Quantity("D_s", 57, "mm"), CalcInputValue.Quantity("D_g", 52, "mm"), CalcInputValue.Quantity("D_g_u", 0, "mm"), CalcInputValue.Quantity("D_g_l", 0, "mm"),
                    CalcInputValue.Choice("red", RelationParker), CalcInputValue.Of("n_mc", 1_000),
                ],
                [new CalcExpectedValue("red_nom", 2.8464, "%", 1e-9), new CalcExpectedValue("squeeze_nom", 26.1405209306, "%", 1e-9)],
                CalcReferenceStatus.Unconfirmed),
            new CalcTestCase("ka-stretch-rule", "Installation rule: up to 6 % stretch on a piston groove (5.8 % reads 0.967 of the limit)",
                new CalcReferenceSource("Parker Hannifin, PTD 5705, installation guidance, rule as quoted second-hand", null, "piston-groove stretch not above 6 %; the limit is the default of x_hi, a record; to be checked against the owner's copy, including the edition", Secondary: true),
                [
                    CalcInputValue.Quantity("d_r_tol", 0, "mm"),
                    CalcInputValue.Quantity("D_s", 57.6, "mm"), CalcInputValue.Quantity("D_g", 52.9, "mm"), CalcInputValue.Quantity("D_g_u", 0, "mm"), CalcInputValue.Quantity("D_g_l", 0, "mm"),
                    CalcInputValue.Quantity("D_c", 57.5, "mm"), CalcInputValue.Of("n_mc", 1_000),
                ],
                [new CalcExpectedValue("stretch", 5.8, "%", 1e-9), new CalcExpectedValue("u_stretch", 0.9666666667, "", 1e-9)],
                CalcReferenceStatus.Unconfirmed),
        ],
        Category: "Seals",
        OutputPorts:
        [
            new CalcOutputPort("squeeze_min", "%", "Smallest squeeze over tolerance, temperature and swell (cold end)"),
            new CalcOutputPort("squeeze_max", "%", "Largest squeeze over tolerance, temperature and swell (hot end)"),
            new CalcOutputPort("fill_max", "%", "Largest gland fill (hot end, unstretched section)"),
            new CalcOutputPort("stretch", "%", "Largest installed stretch of the ring inside diameter"),
        ],
        RecordKinds:
        [
            new CalcRecordKind(MaterialKind, "Material properties", ["alpha", "nu"], "A material from the library: its properties fill these inputs, each citing the material, its revision and the page or table of the value."),
            new CalcRecordKind("oring.ring", "Ring size", ["d_r", "d_r_tol", "W", "W_tol"], "The ring's inside diameter and cross-section with their tolerances, from the supplier's catalogue or your own size list. No size table is shipped."),
            new CalcRecordKind("oring.criteria", "Squeeze, fill and stretch bands", ["q_lo_s", "q_hi_s", "q_lo_d", "q_hi_d", "fill_lo", "fill_hi", "x_lo", "x_hi", "od_hi"], "The acceptable squeeze for static and dynamic duty, the gland fill band, the installed stretch limits and the outside-diameter compression limit of a rod seal or an internal-pressure face seal, from your handbook or the seal maker, with edition and section."),
            new CalcRecordKind("oring.extrusion", "Permitted extrusion gap", ["gap_allow", "ext"], "The permitted gap against pressure and hardness from the seal maker's extrusion data for the compound, as points (hardness, pressure, gap) or as one value. The chart is not shipped."),
        ]);

    public void Run(CalcRunContext c)
    {
        c.MarkUtilisation("u_gap", "u_stretch");
        var typeText = c.InputText("type");
        var kind = typeText == TypeRod ? SealKind.Rod : typeText == TypeFace ? SealKind.Face : SealKind.Piston;
        var dynamicDuty = c.InputText("duty") == DutyDynamic;
        var radial = kind != SealKind.Face;
        var internalFace = kind == SealKind.Face && c.InputText("face_p") == PressureInternal;
        var odHeld = kind == SealKind.Rod || internalFace;
        var k = c.InputSi("k_sigma");
        var distribution = ToDistribution(c.InputText("dist"));
        var bad = false;
        var vars = new List<GlandVar>();

        bool Block(string symbol, string message)
        {
            c.Block("geometry", $"{symbol}: {message}");
            bad = true;
            return false;
        }

        // Toleranced dimensions, millimetres.
        void Add(GlandDim dim, string name, double nominal, double upper, double lower, string symbol)
        {
            if (lower > upper)
            {
                Block(symbol + "_l", "the lower deviation is above the upper deviation.");
                return;
            }

            var mid = nominal + (upper + lower) / 2.0;
            var half = (upper - lower) / 2.0;
            vars.Add(new GlandVar(dim, name, nominal, mid, half, Contributor.SigmaFor(half, distribution, k), distribution));
        }

        double Mm(string symbol) => c.InputSi(symbol) * 1000.0;

        var wTol = Mm("W_tol");
        var dTol = Mm("d_r_tol");
        Add(GlandDim.Section, "ring cross-section W", Mm("W"), wTol, -wTol, "W");
        Add(GlandDim.RingId, "ring inside diameter d", Mm("d_r"), dTol, -dTol, "d_r");

        if (radial)
        {
            Add(GlandDim.Surface, kind == SealKind.Piston ? "bore D_s" : "rod D_s", Mm("D_s"), Mm("D_s_u"), Mm("D_s_l"), "D_s");
            Add(GlandDim.Groove, kind == SealKind.Piston ? "piston groove root D_g" : "housing groove root D_g", Mm("D_g"), Mm("D_g_u"), Mm("D_g_l"), "D_g");
            Add(GlandDim.Clearance, kind == SealKind.Piston ? "piston outside diameter D_c" : "housing bore D_c", Mm("D_c"), Mm("D_c_u"), Mm("D_c_l"), "D_c");
        }
        else
        {
            Add(GlandDim.Groove, "face groove seat diameter D_g", Mm("D_g"), Mm("D_g_u"), Mm("D_g_l"), "D_g");
            Add(GlandDim.FaceDepth, "face groove depth h_f", Mm("h_f"), Mm("h_f_u"), Mm("h_f_l"), "h_f");
            Add(GlandDim.FaceGap, "face gap e_f", Mm("e_f"), Mm("e_f_u"), Mm("e_f_l"), "e_f");
        }

        Add(GlandDim.Width, "groove width b", Mm("b"), Mm("b_u"), Mm("b_l"), "b");
        var cornerRadius = Mm("r_g");

        // Measured distributions replace the toleranced dimensions.
        var rows = c.Rows("measured");
        var seen = new HashSet<GlandDim>();

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var key = row.Get("dim")?.Text ?? string.Empty;
            var dimOrNull = KeyToDim(key);

            if (dimOrNull is not { } dim)
            {
                Block("measured", $"row {i + 1} names '{key}', which is not a gland dimension; use one of {string.Join(", ", MeasuredDims)}.");
                continue;
            }

            var target = vars.FindIndex(v => v.Dim == dim);

            if (target < 0)
            {
                c.Warn("measured", $"measured: row {i + 1} names {key}, which a {typeText.ToLowerInvariant()} gland does not use; the row is ignored.");
                continue;
            }

            if (!seen.Add(dim))
            {
                Block("measured", $"row {i + 1} repeats {key}; give each dimension once.");
                continue;
            }

            var mean = MeasuredMm(row, "mean");
            var sigma = MeasuredMm(row, "sigma");
            var rowDistribution = ToDistribution(row.Get("dist")?.Text);
            var half = rowDistribution switch
            {
                ContributorDistribution.Uniform => sigma * Math.Sqrt(3.0),
                ContributorDistribution.Triangular => sigma * Math.Sqrt(6.0),
                _ => k * sigma,
            };
            vars[target] = vars[target] with { Mid = mean, Half = half, Sigma = sigma, Distribution = rowDistribution, Name = vars[target].Name + " (measured)" };
        }

        foreach (var v in vars)
        {
            if (!(v.Mid - v.Half > 0.0))
                Block(InputSymbolOf(v.Dim), $"the smallest {v.Name} ({F(v.Mid - v.Half)} mm) is zero or negative; check the nominal, the deviations or the measured mean and spread.");
        }

        // Environment and limits.
        var tMin = c.InputSi("T_min") - 273.15;
        var tMax = c.InputSi("T_max") - 273.15;
        var sMin = c.InputSi("s_min");
        var sMax = c.InputSi("s_max");
        var alpha = c.InputSi("alpha");
        var alphaMetal = c.InputSi("alpha_m");
        var relation = ToRelation(c.InputText("red"));
        var nu = c.InputSi("nu");
        var qLo = c.InputSi(dynamicDuty ? "q_lo_d" : "q_lo_s");
        var qHi = c.InputSi(dynamicDuty ? "q_hi_d" : "q_hi_s");
        var fillLo = c.InputSi("fill_lo");
        var fillHi = c.InputSi("fill_hi");
        var xLo = c.InputSi("x_lo");
        var xHi = c.InputSi("x_hi");
        var odHi = odHeld ? c.InputSi("od_hi") : 0.0;
        var hardness = c.InputSi("H_A");
        var pressureBar = c.InputSi("p_op") / 1e5;
        var samples = (int)Math.Round(c.InputSi("n_mc"));
        var seed = (ulong)Math.Max(0, Math.Round(c.InputSi("seed")));

        if (tMin > tMax)
            Block("T_max", "the highest operating temperature is below the lowest; swap them.");

        if (sMin > sMax)
            Block("s_max", "the largest swell is below the smallest; swap them.");

        if (!(qLo < qHi))
            Block(dynamicDuty ? "q_hi_d" : "q_hi_s", "the highest acceptable squeeze is not above the lowest.");

        if (!(fillLo < fillHi))
            Block("fill_hi", "the highest acceptable gland fill is not above the lowest.");

        if (!internalFace && !(xLo < xHi))
            Block("x_hi", "the highest acceptable stretch is not above the lowest.");

        // The extrusion limit: the record rows, the value, or neither.
        var points = ReadExtrusion(c, out var extrusionProblem);

        if (extrusionProblem is not null)
            Block("ext", extrusionProblem);

        if (bad)
            return;

        double? alphaSurface = radial && c.Has("alpha_s") ? c.InputSi("alpha_s") : null;
        var boreDilation = radial ? Mm("dD_p") : 0.0;
        var eccentricity = Mm("ecc");
        var env = new GlandEnvironment(kind, relation, nu, alpha, alphaMetal, ORingGlandModel.ReferenceTemperature, tMin, tMax, sMin, sMax, cornerRadius, alphaSurface, boreDilation, internalFace, eccentricity);
        var meanVector = MidVector(vars);

        if (!(ORingGlandModel.Depth(kind, meanVector) > 0.0))
        {
            c.Block("geometry", kind switch
            {
                SealKind.Piston => "D_g: the groove root diameter is not below the bore, so the gland has no depth; check D_s and D_g.",
                SealKind.Rod => "D_g: the housing groove root diameter is not above the rod, so the gland has no depth; check D_s and D_g.",
                _ => "h_f: the groove depth is zero or negative.",
            });
            return;
        }

        if (radial && ORingGlandModel.Gap(kind, meanVector) < 0.0)
        {
            c.Block("geometry", "D_c: the mating part is larger than the sealing surface, so the gap is negative; check D_s and D_c.");
            return;
        }

        GlandVar V(GlandDim dim) => vars.Single(v => v.Dim == dim);
        var section = V(GlandDim.Section);
        var ringId = V(GlandDim.RingId);
        var width = V(GlandDim.Width);
        var reference = V(kind == SealKind.Rod ? GlandDim.Surface : GlandDim.Groove);

        // The depth with each part at its own temperature: the bore or rod at the sealing-surface factor gs, the groove root at the groove-metal factor gm.
        Contributor[] DepthStack(double gs, double gm) => kind switch
        {
            SealKind.Piston => [V(GlandDim.Surface).Scaled(gs / 2.0, "D_s / 2"), V(GlandDim.Groove).Scaled(-gm / 2.0, "D_g / 2")],
            SealKind.Rod => [V(GlandDim.Groove).Scaled(gm / 2.0, "D_g / 2"), V(GlandDim.Surface).Scaled(-gs / 2.0, "D_s / 2")],
            _ => [V(GlandDim.FaceDepth).Scaled(gm), V(GlandDim.FaceGap).Scaled(gm, "e_f")],
        };

        // Each contributor, as the sheet lists them.
        var mm = UnitCatalog.Parse("mm");

        foreach (var v in vars)
        {
            c.Record("v_" + SymbolOf(v.Dim), $"mid ± half-band, σ ({v.Distribution.ToString().ToLowerInvariant()})",
                $"{F(v.Nominal)} mm nominal → mid {F(v.Mid)} ± {F(v.Half)} mm, σ {F(v.Sigma)} mm",
                ScalarValue.Of(v.Mid, mm), v.Name);
        }

        // Cases.
        var (fCold, gCold) = ORingGlandModel.Factors(env, GlandCase.Cold);
        var (fHot, gHot) = ORingGlandModel.Factors(env, GlandCase.Hot);
        var (gsCold, _) = ORingGlandModel.GapFactors(env, GlandCase.Cold);
        var (gsHot, _) = ORingGlandModel.GapFactors(env, GlandCase.Hot);
        var refCold = ORingGlandModel.ReferenceFactor(env, GlandCase.Cold);
        var refHot = ORingGlandModel.ReferenceFactor(env, GlandCase.Hot);
        c.Record("f_c", "(1 + α·(T_min − 20 °C))·(1 + s_min)^(1/3)", $"(1 + {F(alpha)}·({F(tMin)} − 20))·(1 + {F(sMin)})^(1/3)", new ScalarValue(fCold), "Ring size factor, cold end");
        c.Record("g_c", "1 + α_m·(T_min − 20 °C)", $"1 + {F(alphaMetal)}·({F(tMin)} − 20)", new ScalarValue(gCold), "Groove metal size factor, cold end");
        c.Record("f_h", "(1 + α·(T_max − 20 °C))·(1 + s_max)^(1/3)", $"(1 + {F(alpha)}·({F(tMax)} − 20))·(1 + {F(sMax)})^(1/3)", new ScalarValue(fHot), "Ring size factor, hot end");
        c.Record("g_h", "1 + α_m·(T_max − 20 °C)", $"1 + {F(alphaMetal)}·({F(tMax)} − 20)", new ScalarValue(gHot), "Groove metal size factor, hot end");

        if (alphaSurface is { } aSurface)
        {
            c.Record("gs_c", "1 + α_s·(T_min − 20 °C)", $"1 + {F(aSurface)}·({F(tMin)} − 20)", new ScalarValue(gsCold), "Sealing-surface part (bore or rod) size factor, cold end");
            c.Record("gs_h", "1 + α_s·(T_max − 20 °C)", $"1 + {F(aSurface)}·({F(tMax)} − 20)", new ScalarValue(gsHot), "Sealing-surface part (bore or rod) size factor, hot end");
        }

        if (alpha < alphaMetal)
            c.Warn("expansion", "alpha: the ring expands less than the groove metal, so the cold and hot ends do not bound the squeeze; the minimum and maximum shown may not be the extremes.");

        // Installed stretch: the ring inside diameter on its seat, or (an internal-pressure face seal) the outside diameter on the groove outer wall.
        Contributor[] RingSeat(double f) => internalFace ? [ringId.Scaled(f), section.Scaled(2.0 * f)] : [ringId.Scaled(f)];
        var refRange = Range(reference.Scaled(1.0));
        var idRange = Range(RingSeat(1.0));
        var stretchMax = refRange.Max / idRange.Min - 1.0;
        var stretchMin = refRange.Min / idRange.Max - 1.0;
        var stretchNom = reference.Mid / (internalFace ? idRange.Mid : ringId.Mid) - 1.0;
        var depthMid = ORingGlandModel.Depth(kind, meanVector);
        var refName = kind == SealKind.Rod ? "rod" : internalFace ? "groove outer wall" : "seat";
        var seatWord = internalFace ? "OD" : "ID";
        var seatSym = internalFace ? "OD" : "d";

        // An internal-pressure face seal is stretched only when its outside diameter is below the seat; a compressed one thickens instead.
        double Red(double x) => ORingGlandModel.Reduction(relation, internalFace ? Math.Max(x, 0.0) : x, nu);

        c.Record("stretch_nom", $"D_ref/{seatSym} − 1 at the mean dimensions", $"{F(reference.Mid)}/{F(internalFace ? idRange.Mid : ringId.Mid)} − 1", ScalarValue.Of(stretchNom * 100, "%"), $"Installed stretch of the ring {seatWord} on the {refName}, mean dimensions");
        c.Record("stretch", $"D_ref,max/{seatSym}_min − 1", $"{F(refRange.Max)}/{F(idRange.Min)} − 1", ScalarValue.Of(stretchMax * 100, "%"), "Largest installed stretch");
        c.Record("stretch_min", $"D_ref,min/{seatSym}_max − 1", $"{F(refRange.Min)}/{F(idRange.Max)} − 1", ScalarValue.Of(stretchMin * 100, "%"), "Smallest installed stretch" + (internalFace ? " (negative: outside-diameter compression)" : string.Empty));
        var redNom = Red(stretchNom);
        c.Record("red_nom", "r(X) at the mean installed stretch", $"r({F(stretchNom * 100)} %)", ScalarValue.Of(redNom * 100, "%"), "Cross-section reduction at the mean installed stretch");
        c.Record("u_stretch", "stretch / x_hi", $"{F(stretchMax * 100)} % / {F(xHi * 100)} %", new ScalarValue(xHi > 0 ? stretchMax / xHi : 0), "Largest installed stretch against its limit");

        if (stretchMax > 0.25)
            c.Warn("stretch", $"d_r: the largest installed stretch is {F(stretchMax * 100)} %, far beyond installation practice; check the ring and groove diameters.");

        // Cold end: minimum squeeze and fill.
        var drefC = Range(reference.Scaled(refCold));
        var idC = Range(RingSeat(fCold));
        var stretchCold = drefC.Max / idC.Min - 1.0;
        var redCold = Red(stretchCold);
        var sectionCold = Range(section.Scaled(fCold));
        var cCold = odHeld ? Math.Clamp(1.0 - Range(V(GlandDim.Groove).Scaled(gCold)).Max / Range(ringId.Scaled(fCold), section.Scaled(2.0 * fCold)).Min, 0.0, ORingGlandModel.MaxCompression) : 0.0;
        var effLow = sectionCold.Min * (1.0 - redCold) * ORingGlandModel.Thickening(cCold);
        var depthCold = Range(DepthStack(gsCold, gCold));
        depthCold = (depthCold.Min, depthCold.Max + eccentricity, depthCold.Mid, depthCold.Sigma);
        var widthCold = Range(width.Scaled(gCold));
        var corner = 2.0 * (1.0 - Math.PI / 4.0);
        var areaCold = widthCold.Max * depthCold.Max - corner * Math.Pow(gCold * cornerRadius, 2);

        // Hot end: maximum squeeze and fill.
        var drefH = Range(reference.Scaled(refHot));
        var idH = Range(RingSeat(fHot));
        var stretchHot = drefH.Min / idH.Max - 1.0;
        var redHot = Red(stretchHot);
        var sectionHot = Range(section.Scaled(fHot));
        var cHot = odHeld ? Math.Clamp(1.0 - Range(V(GlandDim.Groove).Scaled(gHot)).Min / Range(ringId.Scaled(fHot), section.Scaled(2.0 * fHot)).Max, 0.0, ORingGlandModel.MaxCompression) : 0.0;
        var effHigh = sectionHot.Max * (1.0 - redHot) * ORingGlandModel.Thickening(cHot);
        var depthHot = Range(DepthStack(gsHot, gHot));
        depthHot = (depthHot.Min - eccentricity, depthHot.Max, depthHot.Mid, depthHot.Sigma);
        var widthHot = Range(width.Scaled(gHot));
        var areaHot = widthHot.Min * depthHot.Min - corner * Math.Pow(gHot * cornerRadius, 2);

        if (relation == ReductionRelation.Parker)
        {
            var positive = new[] { stretchNom, stretchMin, stretchMax, stretchCold, stretchHot }.Where(x => x > 0.0).Select(x => x * 100).ToList();

            if (positive.Any(x => x < ORingGlandModel.ParkerStretchMin || x > ORingGlandModel.ParkerStretchCap))
                c.Warn("stretch", $"red: the Parker quadratic is used for {F(ORingGlandModel.ParkerStretchMin)} to {F(ORingGlandModel.ParkerStretchCap)} % stretch only; a stretch here is {F(positive.Min())} … {F(positive.Max())} %. Below {F(ORingGlandModel.ParkerStretchMin)} % the reduction is blended linearly to zero, above {F(ORingGlandModel.ParkerStretchCap)} % it is held at its {F(ORingGlandModel.ParkerStretchCap)} % value; take the reduction from your handbook for this range.");
        }

        if (!(effLow > 0.0) || !(effHigh > 0.0))
        {
            c.Block("geometry", "d_r: the stretch leaves the ring no cross-section; check the ring and groove diameters.");
            return;
        }

        if (!(areaCold > 0.0) || !(areaHot > 0.0))
        {
            c.Block("geometry", "r_g: the groove area is zero or negative; check the groove width, depth and corner radius.");
            return;
        }

        var ringAreaHot = Math.PI / 4.0 * Math.Pow(sectionHot.Max, 2) * (odHeld ? Math.Max(fHot / refHot, Math.Pow(ORingGlandModel.Thickening(cHot), 2)) : fHot / refHot);
        var fillMaxValue = ringAreaHot / areaHot;
        var fillMinValue = Math.PI / 4.0 * effLow * effLow / areaCold;

        c.Record("X_c", $"g_c·D_ref,max/(f_c·{seatSym}_min) − 1", $"{F(refCold)}·{F(drefC.Max / refCold)}/({F(fCold)}·{F(idC.Min / fCold)}) − 1", ScalarValue.Of(stretchCold * 100, "%"), "Largest stretch at the cold end");
        c.Record("r_c", "r(X_c)", $"r({F(stretchCold * 100)} %)", ScalarValue.Of(redCold * 100, "%"), "Cross-section reduction at the cold end");
        c.Record("Wc_lo", "f_c·W_min", $"{F(fCold)}·{F(sectionCold.Min / fCold)}", ScalarValue.Of(sectionCold.Min, mm), "Smallest ring section at the cold end");
        if (odHeld)
            c.Record("c_c", "1 − g_c·D_g,max/(f_c·OD_min), not below zero", $"OD compression, smallest, at the cold end", ScalarValue.Of(cCold * 100, "%"), "Smallest outside-diameter compression at the cold end (thickens the section by 1/√(1 − c))");

        c.Record("Weff_lo", odHeld ? "Wc_lo·(1 − r_c)/√(1 − c_c)" : "Wc_lo·(1 − r_c)", odHeld ? $"{F(sectionCold.Min)}·(1 − {F(redCold)})/√(1 − {F(cCold)})" : $"{F(sectionCold.Min)}·(1 − {F(redCold)})", ScalarValue.Of(effLow, mm), "Smallest effective ring section");
        c.Record("h_hi_c", "g_c·(depth stack) maximum", $"mean {F(depthCold.Mid)} mm, worst-case maximum", ScalarValue.Of(depthCold.Max, mm), "Largest gland depth at the cold end");
        c.Record("squeeze_min", "1 − h_hi_c/Weff_lo", $"1 − {F(depthCold.Max)}/{F(effLow)}", ScalarValue.Of((1.0 - depthCold.Max / effLow) * 100, "%"), "Smallest squeeze");
        c.Record("A_g_hi", "b_max·h_max − 2(1 − π/4)·(g_c·r_g)²", $"{F(widthCold.Max)}·{F(depthCold.Max)} − {F(corner * Math.Pow(gCold * cornerRadius, 2))}", ScalarValue.Of(areaCold, "mm^2"), "Largest groove area at the cold end");
        c.Record("fill_min", "(π/4)·Weff_lo²/A_g_hi", $"(π/4)·{F(effLow)}²/{F(areaCold)}", ScalarValue.Of(fillMinValue * 100, "%"), "Smallest gland fill");

        c.Record("X_h", "g_h·D_ref,min/(f_h·d_max) − 1", $"{F(refHot)}·{F(drefH.Min / refHot)}/({F(fHot)}·{F(idH.Max / fHot)}) − 1", ScalarValue.Of(stretchHot * 100, "%"), "Smallest stretch at the hot end");
        c.Record("r_h", "r(X_h)", $"r({F(stretchHot * 100)} %)", ScalarValue.Of(redHot * 100, "%"), "Cross-section reduction at the hot end");
        c.Record("Wh_hi", "f_h·W_max", $"{F(fHot)}·{F(sectionHot.Max / fHot)}", ScalarValue.Of(sectionHot.Max, mm), "Largest ring section at the hot end");
        if (odHeld)
            c.Record("c_h", "1 − g_h·D_g,min/(f_h·OD_max), not below zero", $"OD compression, largest, at the hot end", ScalarValue.Of(cHot * 100, "%"), "Largest outside-diameter compression at the hot end (thickens the section by 1/√(1 − c))");

        c.Record("Weff_hi", odHeld ? "Wh_hi·(1 − r_h)/√(1 − c_h)" : "Wh_hi·(1 − r_h)", odHeld ? $"{F(sectionHot.Max)}·(1 − {F(redHot)})/√(1 − {F(cHot)})" : $"{F(sectionHot.Max)}·(1 − {F(redHot)})", ScalarValue.Of(effHigh, mm), "Largest effective ring section");
        c.Record("h_lo_h", "g_h·(depth stack) minimum", $"mean {F(depthHot.Mid)} mm, worst-case minimum", ScalarValue.Of(depthHot.Min, mm), "Smallest gland depth at the hot end");
        c.Record("squeeze_max", "1 − h_lo_h/Weff_hi", $"1 − {F(depthHot.Min)}/{F(effHigh)}", ScalarValue.Of((1.0 - depthHot.Min / effHigh) * 100, "%"), "Largest squeeze");
        c.Record("A_g_lo", "b_min·h_min − 2(1 − π/4)·(g_h·r_g)²", $"{F(widthHot.Min)}·{F(depthHot.Min)} − {F(corner * Math.Pow(gHot * cornerRadius, 2))}", ScalarValue.Of(areaHot, "mm^2"), "Smallest groove area at the hot end");
        c.Record("fill_max", odHeld ? "(π/4)·Wh_hi²·max(f_h/g_ref,h, 1/(1 − c_h))/A_g_lo" : "(π/4)·Wh_hi²·(f_h/g_ref,h)/A_g_lo", odHeld ? $"(π/4)·{F(sectionHot.Max)}²·max({F(fHot / refHot)}, 1/(1 − {F(cHot)}))/{F(areaHot)}" : $"(π/4)·{F(sectionHot.Max)}²·({F(fHot)}/{F(refHot)})/{F(areaHot)}", ScalarValue.Of(fillMaxValue * 100, "%"), "Largest gland fill (unstretched section, volume basis: the held ring takes its whole volume change in its section)");

        // The mean state at installation.
        var mid = meanVector;
        var squeezeNom = ORingGlandModel.Squeeze(env, GlandCase.Installed, mid);
        c.Record("squeeze_nom", "1 − h/W_eff at the mean dimensions", $"1 − {F(depthMid)}/{F(ORingGlandModel.EffectiveSection(env, GlandCase.Installed, mid))}", ScalarValue.Of(squeezeNom * 100, "%"), "Squeeze at the mean dimensions, installed");

        // Cross-check: the exact corner of the non-linear model must not be lower than the stack worst case.
        var exactLow = ORingGlandModel.Corner(env, GlandCriterion.SqueezeMin, vars, false);
        var exactHigh = ORingGlandModel.Corner(env, GlandCriterion.SqueezeMax, vars, true);
        var squeezeMin = 1.0 - depthCold.Max / effLow;
        var squeezeMax = 1.0 - depthHot.Min / effHigh;
        c.Record("sq_min_exact", "exact non-linear corner (check)", "corner chosen by the sensitivities", ScalarValue.Of(exactLow * 100, "%"), "Check: smallest squeeze at the exact corner");
        c.Record("sq_max_exact", "exact non-linear corner (check)", "corner chosen by the sensitivities", ScalarValue.Of(exactHigh * 100, "%"), "Check: largest squeeze at the exact corner");

        if (exactLow < squeezeMin - 0.002 || exactHigh > squeezeMax + 0.002)
            c.Warn("worst-case", $"squeeze: the exact corner ({F(exactLow * 100)} … {F(exactHigh * 100)} %) lies outside the stack worst case ({F(squeezeMin * 100)} … {F(squeezeMax * 100)} %) by more than 0.2 points; the stack is not conservative for this geometry.");

        // fp.oring-gland-R1: the extremes are the cold and hot ends; the ring is installed at 20 °C with no swell, which is a third state. When the operating range leaves 20 °C out, that state can have the smallest squeeze.
        if (ORingGlandModel.ReferenceTemperature < tMin - 1e-9 || ORingGlandModel.ReferenceTemperature > tMax + 1e-9)
            c.Warn("installed-state", $"T_min: the operating range {F(tMin)} … {F(tMax)} °C does not include the installation temperature 20 °C, so the minimum and maximum squeeze and fill shown are the ends of the range, not the installed state (no swell, 20 °C), which can have a smaller squeeze (the finding's example: 17.64 % installed against 18.32 % at the cold end); check the installed squeeze_nom or widen the range to include 20 °C.");

        if (squeezeMin <= 0.0)
            c.Warn("contact", $"squeeze: the smallest squeeze is {F(squeezeMin * 100)} %; the ring can lose contact.");

        // Extrusion gap (over the temperature range, with the bore dilation) and the outside diameter held in the groove.
        (double Min, double Max, double Mid, double Sigma) GapRange(GlandCase gc)
        {
            var (gs, gm) = ORingGlandModel.GapFactors(env, gc);

            return kind switch
            {
                SealKind.Piston => Range(V(GlandDim.Surface).Scaled(gs), V(GlandDim.Clearance).Scaled(-gm)),
                SealKind.Rod => Range(V(GlandDim.Clearance).Scaled(gm), V(GlandDim.Surface).Scaled(-gs)),
                _ => Range(V(GlandDim.FaceGap).Scaled(1.0)),
            };
        }

        var gapRanges = new[] { GapRange(GlandCase.Installed), GapRange(GlandCase.Cold), GapRange(GlandCase.Hot) };
        var gapMax = gapRanges.Max(r => r.Max) + boreDilation;
        var gapMin = gapRanges.Min(r => r.Min);
        var gapMid = gapRanges[0].Mid;
        var gapBasisText = radial ? "largest gap over the installed, cold and hot temperatures (diametral clearance, each part with its own expansion) plus the bore dilation under pressure" : "largest gap (the axial gap of a face seal)";
        c.Record("gap_max", gapBasisText, $"mean {F(gapMid)} mm, worst-case maximum" + (boreDilation > 0 ? $", dilation {F(boreDilation)} mm" : string.Empty), ScalarValue.Of(gapMax, mm), "Largest extrusion gap, diametral for a radial seal and axial for a face seal");
        c.Record("gap_min", "smallest gap over the temperatures", $"mean {F(gapMid)} mm, worst-case minimum", ScalarValue.Of(gapMin, mm), "Smallest extrusion gap");

        var gapLimit = ResolveGapLimit(c, points, hardness, pressureBar, out var gapBasis, out var gapOutOfRange);
        var odMax = 0.0;

        if (odHeld)
        {
            var od = Range(ringId.Scaled(1.0, "d"), section.Scaled(2.0, "2W"));
            var groove = Range(V(GlandDim.Groove).Scaled(1.0));
            odMax = (od.Max - groove.Min) / od.Max;
            var odNom = (od.Mid - V(GlandDim.Groove).Mid) / od.Mid;
            c.Record("od_comp_nom", "(OD − D_g)/OD at the mean dimensions", $"({F(od.Mid)} − {F(V(GlandDim.Groove).Mid)})/{F(od.Mid)}", ScalarValue.Of(odNom * 100, "%"), "Compression of the ring outside diameter against the groove, mean dimensions");
            c.Record("od_comp", "(OD_max − D_g,min)/OD_max", $"({F(od.Max)} − {F(groove.Min)})/{F(od.Max)}", ScalarValue.Of(odMax * 100, "%"), "Largest compression of the ring outside diameter against the groove");
        }

        // PASS/FAIL per criterion.
        const double Slack = 1e-9;

        c.CheckCondition("Squeeze minimum within the band", squeezeMin >= qLo - Slack, $"squeeze_min {F(squeezeMin * 100)} %", $"≥ {F(qLo * 100)} %", "squeeze_min", "oring.criteria record");
        c.CheckCondition("Squeeze maximum within the band", squeezeMax <= qHi + Slack, $"squeeze_max {F(squeezeMax * 100)} %", $"≤ {F(qHi * 100)} %", "squeeze_max", "oring.criteria record");
        c.CheckCondition("Gland fill maximum within the band", fillMaxValue <= fillHi + Slack, $"fill_max {F(fillMaxValue * 100)} %", $"≤ {F(fillHi * 100)} %", "fill_max", "oring.criteria record");
        c.CheckCondition("Gland fill minimum within the band", fillMinValue >= fillLo - Slack, $"fill_min {F(fillMinValue * 100)} %", $"≥ {F(fillLo * 100)} %", "fill_min", "oring.criteria record");
        c.CheckCondition("Installed stretch not above the limit", stretchMax <= xHi + Slack, $"stretch {F(stretchMax * 100)} %", $"≤ {F(xHi * 100)} %", "stretch", "oring.criteria record; PTD 5705 installation rule");
        c.CheckCondition("Installed stretch not above the limit at the cold end", stretchCold <= xHi + Slack, $"X_c {F(stretchCold * 100)} %", $"≤ {F(xHi * 100)} %", "X_c", "oring.criteria record");

        if (!internalFace)
            c.CheckCondition("Installed stretch not below the limit", stretchMin >= xLo - Slack, $"stretch_min {F(stretchMin * 100)} %", $"≥ {F(xLo * 100)} %", "stretch_min", "oring.criteria record");

        if (odHeld)
        {
            c.CheckCondition("Ring OD compression not above the limit", odMax <= odHi + Slack, $"od_comp {F(odMax * 100)} %", $"≤ {F(odHi * 100)} %", "od_comp", "oring.criteria record");
            c.CheckCondition("Ring OD compression at the hot end not above the limit", cHot <= odHi + Slack, $"c_h {F(cHot * 100)} %", $"≤ {F(odHi * 100)} %", "c_h", "oring.criteria record");
        }

        if (gapLimit is { } allowed)
        {
            c.Record("gap_allow", "permitted extrusion gap", gapBasis, ScalarValue.Of(allowed, mm), "Permitted extrusion gap, diametral for a radial seal and axial for a face seal (your record or value)");
            c.Record("u_gap", "gap_max / gap_allow", $"{F(gapMax)} / {F(allowed)}", new ScalarValue(allowed > 0 ? gapMax / allowed : 0), "Largest gap against the permitted gap");
            c.CheckCondition("Extrusion gap within the permitted gap", !gapOutOfRange && gapMax <= allowed + Slack, $"gap_max {F(gapMax)} mm", gapOutOfRange ? $"{gapBasis}" : $"≤ {F(allowed)} mm ({gapBasis})", "gap_max", "oring.extrusion record");
        }
        else if (gapOutOfRange)
        {
            c.CheckCondition("Extrusion gap within the permitted gap", false, $"gap_max {F(gapMax)} mm", gapBasis, "gap_max", "oring.extrusion record");
        }
        else
        {
            c.Warn("extrusion", "gap_allow: no permitted extrusion gap is entered (no rows in the extrusion record and no value), so the extrusion is not verified. Enter the seal maker's limit for this pressure and hardness.");

            // Not verified is not passed: at a pressure the ring can extrude, the sheet fails until a limit is entered.
            if (pressureBar > 0.0)
                c.CheckCondition("Extrusion verified against a permitted gap", false, $"gap_max {F(gapMax)} mm at {F(pressureBar)} bar", "no permitted gap entered (gap_allow or the oring.extrusion record)", "gap_max", "oring.extrusion record");
        }

        if (kind == SealKind.Face && dynamicDuty)
            c.Warn("face", "duty: a face seal is normally a static seal; check that a dynamic squeeze band is what you want.");

        // Outputs.
        c.Output("squeeze_nom", "Squeeze at the mean dimensions");
        c.Output("squeeze_min", "Smallest squeeze");
        c.Output("squeeze_max", "Largest squeeze");
        c.Output("fill_min", "Smallest gland fill");
        c.Output("fill_max", "Largest gland fill");
        c.Output("stretch_nom", "Installed stretch, mean dimensions");
        c.Output("stretch", "Largest installed stretch");
        c.Output("stretch_min", internalFace ? "Smallest installed stretch (negative: outside-diameter compression)" : "Smallest installed stretch");
        c.Output("X_c", "Largest stretch at the cold end");
        c.Output("u_stretch", "Largest stretch against its limit");
        c.Output("red_nom", "Cross-section reduction at the mean stretch");

        if (odHeld)
        {
            c.Output("od_comp_nom", "Ring OD compression against the groove, mean dimensions");
            c.Output("od_comp", "Largest ring OD compression against the groove");
            c.Output("c_c", "Smallest OD compression at the cold end (thickens the section)");
            c.Output("c_h", "Largest OD compression at the hot end (thickens the section)");
        }

        c.Output("gap_min", "Smallest extrusion gap");
        c.Output("gap_max", "Largest extrusion gap, diametral for a radial seal (over the temperature range, with the bore dilation)");

        if (gapLimit is not null)
        {
            c.Output("gap_allow", "Permitted extrusion gap, diametral for a radial seal");
            c.Output("u_gap", "Largest gap against the permitted gap");
        }

        // Monte Carlo through the engine.
        var plans = new List<(GlandCriterion Criterion, string Tag, string Label, double? Lower, double? Upper)>
        {
            (GlandCriterion.SqueezeMin, "sq_min", "squeeze below the lower limit (cold end)", qLo, null),
            (GlandCriterion.SqueezeMax, "sq_max", "squeeze above the upper limit (hot end)", null, qHi),
            (GlandCriterion.FillMax, "fill_max", "gland fill above the upper limit (hot end)", null, fillHi),
            (GlandCriterion.FillMin, "fill_min", "gland fill below the lower limit (cold end)", fillLo, null),
            (GlandCriterion.Stretch, "stretch", "installed stretch outside its limits", internalFace ? null : xLo, xHi),
        };

        if (odHeld)
            plans.Add((GlandCriterion.OdCompression, "od", "ring OD compression above the limit", null, odHi));

        if (gapLimit is { } gapMm)
            plans.Add((GlandCriterion.Gap, "gap", "extrusion gap above the permitted gap", null, gapMm));

        foreach (var plan in plans)
        {
            var analysis = ORingGlandModel.Analyse(env, plan.Criterion, vars, plan.Lower, plan.Upper, samples, seed, c.CancellationToken);
            var simulation = analysis.Simulation!;
            var fraction = (simulation.FractionBelowLower ?? 0.0) + (simulation.FractionAboveUpper ?? 0.0);
            c.Record("p_" + plan.Tag, $"fraction of {samples} samples with {plan.Label}", $"Monte Carlo of the exact model, seed {seed}", ScalarValue.Of(fraction * 100, "%"), "Monte Carlo probability: " + plan.Label);
            c.Output("p_" + plan.Tag, "Fraction outside the limit: " + plan.Label);

            if (analysis.Stack.Capability is { } capability)
            {
                c.Record("cpk_" + plan.Tag, "limit distance over three σ of the linearised model (the Monte Carlo itself runs the exact model)", $"σ = {F(analysis.Stack.Sigma)}", new ScalarValue(capability.Cpk), "Capability index C_pk: " + plan.Label);
                c.Output("cpk_" + plan.Tag, "C_pk: " + plan.Label);
            }
        }

        // Where the squeeze spread comes from.
        var squeezeLinear = ORingGlandModel.Linearise(env, GlandCriterion.SqueezeMin, vars);
        var total = squeezeLinear.HalfBand;

        if (total > 0.0)
        {
            var shares = squeezeLinear.Terms.Select((t, i) => (X: (double)(i + 1), Y: 100.0 * Math.Abs(t.Slope) * t.Var.Half / total)).ToList();
            c.Series(new CalcSeries("Contribution to the minimum squeeze spread, worst case", "dimension", "", "share", "%", shares));
            var largest = squeezeLinear.Terms.OrderByDescending(t => Math.Abs(t.Slope) * t.Var.Half).First().Var;
            c.Record("pareto", "largest contributor to the squeeze spread (worst case)", string.Join("; ", squeezeLinear.Terms.Select((t, i) => $"{t.Var.Name} {F(shares[i].Y)} %")), new TextValue(largest.Name), "Contribution Pareto");
        }

        c.Assume(dynamicDuty
            ? "Dynamic duty: the squeeze band for dynamic seals from the oring.criteria record is used; friction, wear and the dynamic groove finish are not checked."
            : "Static duty: the squeeze band for static seals from the oring.criteria record is used.", "The bands are the user's record.");

        if (relation == ReductionRelation.Parker)
            c.Assume("The cross-section reduction uses the quadratic as quoted second-hand from Parker ORD 5700 section 3.5 (Y = 0.56 + 0.59X − 0.0046X², X and Y in per cent, applied for X above zero, used for about 3 to 25 % stretch only: below 3 % it is blended linearly to zero, above 25 % it is held at its 25 % value); it has not been confirmed against the handbook.", "Cited relation, unconfirmed.");

    }

    /// <summary>The permitted gap in millimetres from the extrusion rows (interpolated) and the entered value (the smaller of the two when both are given).</summary>
    private static double? ResolveGapLimit(CalcRunContext c, IReadOnlyList<ExtrusionPoint> points, double hardness, double pressureBar, out string basis, out bool outOfRange)
    {
        double? limit = null;
        var parts = new List<string>();
        outOfRange = false;

        if (points.Count > 0)
        {
            var found = ExtrusionRecord.Lookup(points, hardness, pressureBar);
            outOfRange = found.OutOfRange;

            if (found.GapMm is { } g)
            {
                limit = g;
                parts.Add(found.Basis);
            }

            if (found.OutOfRange)
                c.Warn("extrusion-range", $"ext: {found.Basis}");
        }

        if (c.Has("gap_allow"))
        {
            var entered = c.InputSi("gap_allow") * 1000.0;
            parts.Add($"entered {CalcValue.FormatNumber(entered, 5)} mm");
            limit = limit is { } existing ? Math.Min(existing, entered) : entered;
        }

        basis = string.Join("; the smaller of ", parts);
        return limit;
    }

    private static IReadOnlyList<ExtrusionPoint> ReadExtrusion(CalcRunContext c, out string? problem)
    {
        problem = null;
        var points = new List<ExtrusionPoint>();
        var rows = c.Rows("ext");

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];

            if (row.Get("H")?.Number is not { } h || row.Get("p")?.Number is not { } p || row.Get("gap")?.Number is not { } gap)
            {
                problem = $"row {i + 1} needs a hardness, a pressure and a gap.";
                return [];
            }

            var pressureBar = UnitCatalog.Parse(row.Get("p")!.Unit ?? "bar").ToSi(p) / 1e5;
            var gapMm = UnitCatalog.Parse(row.Get("gap")!.Unit ?? "mm").ToSi(gap) * 1000.0;
            points.Add(new ExtrusionPoint(h, pressureBar, gapMm));
        }

        return points;
    }

    private static double MeasuredMm(CalcRow row, string key) =>
        row.Get(key) is { Number: { } n } cell ? UnitCatalog.Parse(cell.Unit ?? "mm").ToSi(n) * 1000.0 : 0.0;

    private static double[] MidVector(IEnumerable<GlandVar> vars)
    {
        var x = new double[Enum.GetValues<GlandDim>().Length];

        foreach (var v in vars)
            x[(int)v.Dim] = v.Mid;

        return x;
    }

    /// <summary>The range of a linear stack from the engine: worst-case minimum and maximum, mean and σ.</summary>
    private static (double Min, double Max, double Mid, double Sigma) Range(params Contributor[] contributors)
    {
        var r = ToleranceStackEngine.Analyse(contributors);
        return (r.WorstCaseMinimum, r.WorstCaseMaximum, r.Mean, r.Sigma);
    }

    private static string SymbolOf(GlandDim dim) => dim switch
    {
        GlandDim.Section => "W",
        GlandDim.RingId => "d",
        GlandDim.Surface => "Ds",
        GlandDim.Groove => "Dg",
        GlandDim.Clearance => "Dc",
        GlandDim.FaceDepth => "hf",
        GlandDim.Width => "b",
        _ => "ef",
    };

    /// <summary>The input symbol a dimension is entered under (what a block message names).</summary>
    private static string InputSymbolOf(GlandDim dim) => dim switch
    {
        GlandDim.Section => "W",
        GlandDim.RingId => "d_r",
        GlandDim.Surface => "D_s",
        GlandDim.Groove => "D_g",
        GlandDim.Clearance => "D_c",
        GlandDim.FaceDepth => "h_f",
        GlandDim.Width => "b",
        _ => "e_f",
    };

    private static GlandDim? KeyToDim(string key) => key switch
    {
        "W" => GlandDim.Section,
        "d_r" => GlandDim.RingId,
        "D_s" => GlandDim.Surface,
        "D_g" => GlandDim.Groove,
        "D_c" => GlandDim.Clearance,
        "h_f" => GlandDim.FaceDepth,
        "b" => GlandDim.Width,
        "e_f" => GlandDim.FaceGap,
        _ => null,
    };

    private static ContributorDistribution ToDistribution(string? text) => text switch
    {
        DistributionUniform => ContributorDistribution.Uniform,
        DistributionTriangular => ContributorDistribution.Triangular,
        _ => ContributorDistribution.Normal,
    };

    private static ReductionRelation ToRelation(string text) => text switch
    {
        RelationNone => ReductionRelation.None,
        RelationParker => ReductionRelation.Parker,
        RelationLinear => ReductionRelation.Linear,
        _ => ReductionRelation.Incompressible,
    };

    private static string F(double v) => double.IsFinite(v) ? CalcValue.FormatNumber(v, 5) : "—";
}

/// <summary>One point of a user's extrusion record: the largest permitted gap at a hardness and a pressure.</summary>
/// <param name="Hardness">Shore A.</param>
/// <param name="PressureBar">The pressure, bar.</param>
/// <param name="GapMm">The largest permitted gap, mm.</param>
public sealed record ExtrusionPoint(double Hardness, double PressureBar, double GapMm);

/// <summary>What the extrusion record says at a hardness and a pressure.</summary>
/// <param name="GapMm">The permitted gap, mm, or <see langword="null"/> without points.</param>
/// <param name="Basis">How it was got, in words.</param>
/// <param name="OutOfRange">The record does not cover the pressure or the hardness; the gap shown is the nearest listed value and the check must fail.</param>
public sealed record ExtrusionLimit(double? GapMm, string Basis, bool OutOfRange);

/// <summary>
/// The user's extrusion record read at a hardness and a pressure: points grouped by hardness, each group a curve of permitted
/// gap against pressure. Between listed pressures the gap is linear in pressure; between listed hardnesses linear in hardness.
/// Pressure above a curve's last point, or hardness below the softest listed, is outside the record (the nearest value is
/// returned and flagged, since the record cannot confirm it); hardness above the hardest listed uses the hardest curve,
/// which is the conservative reading.
/// </summary>
public static class ExtrusionRecord
{
    public static ExtrusionLimit Lookup(IReadOnlyList<ExtrusionPoint> points, double hardness, double pressureBar)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count == 0)
            return new ExtrusionLimit(null, "no extrusion record rows", false);

        var curves = points.GroupBy(p => p.Hardness).OrderBy(g => g.Key).Select(g => (Hardness: g.Key, Points: g.OrderBy(p => p.PressureBar).ThenBy(p => p.GapMm).ToList())).ToList();
        var outside = false;

        double At((double Hardness, List<ExtrusionPoint> Points) curve)
        {
            var list = curve.Points;

            if (pressureBar <= list[0].PressureBar)
                return list[0].GapMm;

            if (pressureBar > list[^1].PressureBar)
            {
                outside = true;
                return list[^1].GapMm;
            }

            for (var i = 1; i < list.Count; i++)
            {
                if (pressureBar <= list[i].PressureBar)
                {
                    var span = list[i].PressureBar - list[i - 1].PressureBar;
                    return span <= 0 ? Math.Min(list[i - 1].GapMm, list[i].GapMm) : list[i - 1].GapMm + (list[i].GapMm - list[i - 1].GapMm) * (pressureBar - list[i - 1].PressureBar) / span;
                }
            }

            return list[^1].GapMm;
        }

        var pressureText = CalcValueText(pressureBar);
        var hardnessText = CalcValueText(hardness);

        if (hardness < curves[0].Hardness)
        {
            var value = At(curves[0]);
            return new ExtrusionLimit(value, $"hardness {hardnessText} Shore A is below the softest listed ({CalcValueText(curves[0].Hardness)}); the record cannot confirm it (value shown: the softest curve at {pressureText} bar)", true);
        }

        if (hardness >= curves[^1].Hardness)
        {
            var value = At(curves[^1]);
            var text = hardness == curves[^1].Hardness ? $"{hardnessText} Shore A curve at {pressureText} bar" : $"hardest listed curve ({CalcValueText(curves[^1].Hardness)} Shore A) at {pressureText} bar, conservative for {hardnessText} Shore A";
            return new ExtrusionLimit(value, outside ? $"pressure {pressureText} bar is above the last listed point of the {text}; the record cannot confirm it (value shown: the last point)" : text, outside);
        }

        var upperIndex = curves.FindIndex(cv => cv.Hardness >= hardness);
        var upper = curves[upperIndex];

        if (upper.Hardness == hardness)
        {
            var exact = At(upper);
            return new ExtrusionLimit(exact, outside ? $"pressure {pressureText} bar is above the last listed point of the {hardnessText} Shore A curve; the record cannot confirm it (value shown: the last point)" : $"{hardnessText} Shore A curve at {pressureText} bar", outside);
        }

        var lower = curves[upperIndex - 1];
        var gLow = At(lower);
        var gHigh = At(upper);
        var gap = gLow + (gHigh - gLow) * (hardness - lower.Hardness) / (upper.Hardness - lower.Hardness);
        return new ExtrusionLimit(gap, outside
            ? $"pressure {pressureText} bar is above the last listed point of a curve between {CalcValueText(lower.Hardness)} and {CalcValueText(upper.Hardness)} Shore A; the record cannot confirm it (value shown: the last points)"
            : $"interpolated between the {CalcValueText(lower.Hardness)} and {CalcValueText(upper.Hardness)} Shore A curves at {pressureText} bar", outside);
    }

    private static string CalcValueText(double v) => CalcValue.FormatNumber(v, 5);
}
