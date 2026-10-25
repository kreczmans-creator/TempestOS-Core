using Tempest.Calc.Engine.Analysis;
using Tempest.Calc.Engine.Analysis.ToleranceStack;

namespace Tempest.Calc.FluidPower;

/// <summary>The three gland arrangements of F3.</summary>
public enum SealKind
{
    /// <summary>A ring in a groove on the piston, sealing on the cylinder bore (stretched onto the groove root).</summary>
    Piston,

    /// <summary>A ring in a groove in the housing, sealing on the rod (stretched onto the rod).</summary>
    Rod,

    /// <summary>A ring in an axial groove, sealing against a flat face.</summary>
    Face,
}

/// <summary>The dimensions that vary in a gland. The order is the order of the contributors in every stack.</summary>
public enum GlandDim
{
    /// <summary>Ring cross-section W.</summary>
    Section = 0,

    /// <summary>Ring inside diameter d.</summary>
    RingId = 1,

    /// <summary>Sealing-surface diameter (bore for a piston seal, rod for a rod seal).</summary>
    Surface = 2,

    /// <summary>Groove root diameter (the piston groove; the housing groove of a rod seal; the diameter a face-seal ring seats on).</summary>
    Groove = 3,

    /// <summary>The mating part that sets the extrusion gap (piston outside diameter; housing bore round the rod).</summary>
    Clearance = 4,

    /// <summary>Face-seal groove depth.</summary>
    FaceDepth = 5,

    /// <summary>Groove width.</summary>
    Width = 6,

    /// <summary>Face-seal extrusion gap.</summary>
    FaceGap = 7,
}

/// <summary>How the cross-section shrinks when the ring is stretched.</summary>
public enum ReductionRelation
{
    /// <summary>No reduction.</summary>
    None,

    /// <summary>Constant-volume rubber: the circumference grows by (1 + X), so the cross-section diameter falls by 1/√(1 + X). Derived.</summary>
    Incompressible,

    /// <summary>The quadratic Y = 0.56 + 0.59·X − 0.0046·X² (X, Y in per cent), cited as Parker ORD 5700 §3.5 in Addendum B §B6 F3; not confirmed against the handbook. Used for about 3 to 25 % stretch only: below it the value is blended linearly to zero, above it the 25 % value is held.</summary>
    Parker,

    /// <summary>Linear in the stretch: a reduction of ν·X for the user's Poisson ratio ν. Derived.</summary>
    Linear,
}

/// <summary>The conditions a gland dimension set is evaluated at.</summary>
public enum GlandCase
{
    /// <summary>Installed at the reference temperature, no swell.</summary>
    Installed,

    /// <summary>The cold, shrunk end: the lowest temperature and the least swell. Governs the minimum squeeze and fill.</summary>
    Cold,

    /// <summary>The hot, swollen end: the highest temperature and the most swell. Governs the maximum squeeze and fill.</summary>
    Hot,
}

/// <summary>The quantities F3 judges.</summary>
public enum GlandCriterion
{
    /// <summary>Squeeze at the cold end, with the stretch-reduced cross-section.</summary>
    SqueezeMin,

    /// <summary>Squeeze at the hot end.</summary>
    SqueezeMax,

    /// <summary>Gland fill at the hot end, on the unstretched cross-section and a volume basis.</summary>
    FillMax,

    /// <summary>Gland fill at the cold end, on the stretch-reduced cross-section.</summary>
    FillMin,

    /// <summary>The installed stretch of the ring inside diameter.</summary>
    Stretch,

    /// <summary>The installed compression of the ring outside diameter in a rod-seal groove, or against the groove outer wall of an internal-pressure face seal.</summary>
    OdCompression,

    /// <summary>The largest extrusion gap over the temperature range, with the bore dilation (diametral for radial seals, axial for face seals).</summary>
    Gap,
}

/// <summary>One toleranced dimension: its nominal, its mean (mid-limit, or a measured mean), its half-band and its spread. Lengths are in millimetres.</summary>
public sealed record GlandVar(GlandDim Dim, string Name, double Nominal, double Mid, double Half, double Sigma, ContributorDistribution Distribution)
{
    /// <summary>
    /// The engine contributor of <paramref name="coefficient"/> times this dimension: the sign carries the direction and the
    /// figures are the magnitudes, so Σ sign × mid is coefficient × mid.
    /// </summary>
    public Contributor Scaled(double coefficient, string? name = null)
    {
        var m = Math.Abs(coefficient);
        return new Contributor(name ?? Name, coefficient < 0 ? -1.0 : 1.0, m * Nominal, m * Half, m * Mid, Distribution, m * Sigma);
    }
}

/// <summary>The fixed part of a gland analysis: the arrangement, the reduction relation and the thermal and swell cases. Lengths in millimetres, strains and swell as fractions.</summary>
/// <param name="Kind">The gland arrangement.</param>
/// <param name="Relation">How the cross-section shrinks with stretch.</param>
/// <param name="Nu">The Poisson ratio of the linear relation.</param>
/// <param name="Alpha">The ring's linear thermal expansion coefficient, 1/K.</param>
/// <param name="AlphaMetal">The groove material's linear thermal expansion coefficient, 1/K.</param>
/// <param name="TRef">The reference (installation) temperature, °C.</param>
/// <param name="TMin">The lowest operating temperature, °C.</param>
/// <param name="TMax">The highest operating temperature, °C.</param>
/// <param name="SwellMin">The smallest volume change of the ring in service (negative: shrinkage), as a fraction.</param>
/// <param name="SwellMax">The largest volume swell, as a fraction.</param>
/// <param name="CornerRadius">The groove bottom corner radius, mm.</param>
/// <param name="AlphaSurface">The linear thermal expansion coefficient of the part that carries the sealing surface (the bore of a piston seal, the rod of a rod seal), 1/K; <see langword="null"/> takes <paramref name="AlphaMetal"/>.</param>
/// <param name="BoreDilation">The diametral growth of the bore (piston seal) or of the housing bore round the rod (rod seal) under the operating pressure, mm, added to the extrusion gap; 0 if not entered.</param>
/// <param name="InternalPressure">A face seal with the pressure on the inside: the ring outside diameter seats on the groove outer wall (<see cref="GlandDim.Groove"/>) instead of the inside diameter on the groove inner wall. Ignored for radial seals.</param>
public sealed record GlandEnvironment(SealKind Kind, ReductionRelation Relation, double Nu, double Alpha, double AlphaMetal, double TRef, double TMin, double TMax, double SwellMin, double SwellMax, double CornerRadius, double? AlphaSurface = null, double BoreDilation = 0.0, bool InternalPressure = false)
{
    /// <summary>The ring outside diameter seats on the reference diameter (an internal-pressure face seal).</summary>
    public bool HeldByOutsideDiameter => Kind == SealKind.Face && InternalPressure;
}

/// <summary>A criterion linearised about the mean dimensions: its value there and one sensitivity per varying dimension (secant across the band).</summary>
/// <param name="Value">The criterion at the mean dimensions.</param>
/// <param name="Terms">Sensitivity a_i = (F(mid + half) − F(mid − half)) / (2·half) of each dimension that varies and matters.</param>
/// <param name="Offset">F(mid) − Σ a_i·mid_i, so that F ≈ Offset + Σ a_i·x_i.</param>
public sealed record GlandLinearisation(double Value, IReadOnlyList<(GlandVar Var, double Slope)> Terms, double Offset)
{
    /// <summary>The linear model as engine contributors: one per term, then the constant (zero width) last.</summary>
    public IReadOnlyList<Contributor> Contributors()
    {
        var list = new List<Contributor>(Terms.Count + 1);

        foreach (var (v, a) in Terms)
            list.Add(v.Scaled(a));

        var m = Math.Abs(Offset);
        list.Add(new Contributor("constant (linearisation offset)", Offset < 0 ? -1.0 : 1.0, m, 0.0, m, ContributorDistribution.Normal, 0.0));
        return list;
    }

    /// <summary>The sum of |a_i|·half_i: the worst-case half-band of the linear model.</summary>
    public double HalfBand => Terms.Sum(t => Math.Abs(t.Slope) * t.Var.Half);
}

/// <summary>What the engine says about one linearised criterion.</summary>
/// <param name="Criterion">Which quantity.</param>
/// <param name="Linear">The linear model.</param>
/// <param name="Stack">Worst-case, RSS and capability analysis of the linear model.</param>
/// <param name="Simulation">The seeded Monte Carlo of the linear model, or <see langword="null"/> when no limit was stated.</param>
public sealed record GlandAnalysis(GlandCriterion Criterion, GlandLinearisation Linear, StackResult Stack, MonteCarloResult? Simulation);

/// <summary>
/// F3 — the pure geometry of an O-ring gland, in one place so the sheet, the tests and the Python cross-check share one
/// definition. Lengths in millimetres, strains, squeeze and fill as fractions. Squeeze is (W_eff − depth)/W_eff where W_eff
/// is the cross-section after thermal expansion, swell and the stretch reduction; the stretch is (D_ref/d − 1) with D_ref
/// the diameter the ring ID sits on; the gland fill is the ring area π/4·W² over the groove area width × depth less the
/// bottom corner radii. Nothing here is a size or gland table: every dimension is an input.
/// </summary>
public static class ORingGlandModel
{
    /// <summary>The fixed reference temperature of the installed state, °C.</summary>
    public const double ReferenceTemperature = 20.0;

    /// <summary>The lowest stretch (per cent) the Parker quadratic is used for. Below it the reduction is blended linearly to zero at no stretch, so that it does not jump from nothing to 0.56 % at the first trace of stretch (the handbook's range is an owner check).</summary>
    public const double ParkerStretchMin = 3.0;

    /// <summary>The highest stretch (per cent) the Parker quadratic is used for: above it the value at this stretch is held.</summary>
    public const double ParkerStretchCap = 25.0;

    /// <summary>A compressed outside diameter never thickens the section beyond this fraction of compression.</summary>
    public const double MaxCompression = 0.9;

    /// <summary>The linear relation never leaves less than a tenth of the cross-section.</summary>
    public const double MaxReduction = 0.9;

    /// <summary>The cross-section reduction (a fraction of W) for a stretch <paramref name="x"/> (a fraction; negative for compression).</summary>
    public static double Reduction(ReductionRelation relation, double x, double nu)
    {
        if (!double.IsFinite(x))
            return 0.0;

        return relation switch
        {
            ReductionRelation.None => 0.0,
            ReductionRelation.Incompressible => x > -0.99 ? 1.0 - 1.0 / Math.Sqrt(1.0 + x) : 0.0,
            ReductionRelation.Parker => x > 0.0 ? ParkerBranch(x * 100.0) / 100.0 : 0.0,
            ReductionRelation.Linear => Math.Min(nu * x, MaxReduction),
            _ => 0.0,
        };
    }

    /// <summary>The quadratic inside its range (about 3 to 25 % stretch, held at the top), blended linearly to zero below it.</summary>
    private static double ParkerBranch(double stretchPercent)
    {
        if (stretchPercent >= ParkerStretchMin)
            return Math.Max(Parker(Math.Min(stretchPercent, ParkerStretchCap)), 0.0);

        return Parker(ParkerStretchMin) * stretchPercent / ParkerStretchMin;
    }

    /// <summary>Y = 0.56 + 0.59·X − 0.0046·X² with X the stretch and Y the cross-section reduction, both in per cent (the form Addendum B §B6 F3 quotes from Parker ORD 5700 §3.5).</summary>
    public static double Parker(double stretchPercent) => 0.56 + 0.59 * stretchPercent - 0.0046 * stretchPercent * stretchPercent;

    /// <summary>The linear size factors of a case: ring (thermal × cube root of the volume change) and groove metal (thermal).</summary>
    public static (double Ring, double Metal) Factors(GlandEnvironment env, GlandCase c)
    {
        ArgumentNullException.ThrowIfNull(env);

        if (c == GlandCase.Installed)
            return (1.0, 1.0);

        var (t, s) = c == GlandCase.Cold ? (env.TMin, env.SwellMin) : (env.TMax, env.SwellMax);
        return ((1.0 + env.Alpha * (t - env.TRef)) * Math.Cbrt(1.0 + s), 1.0 + env.AlphaMetal * (t - env.TRef));
    }

    /// <summary>The diameter the ring ID sits on: the groove root (piston and face) or the rod (rod seal).</summary>
    public static double ReferenceDiameter(SealKind kind, double[] x)
    {
        ArgumentNullException.ThrowIfNull(x);
        return kind == SealKind.Rod ? x[(int)GlandDim.Surface] : x[(int)GlandDim.Groove];
    }

    /// <summary>The gland depth (before any thermal factor): half the diameter difference, or the face-seal depth.</summary>
    public static double Depth(SealKind kind, double[] x)
    {
        ArgumentNullException.ThrowIfNull(x);

        return kind switch
        {
            SealKind.Piston => (x[(int)GlandDim.Surface] - x[(int)GlandDim.Groove]) / 2.0,
            SealKind.Rod => (x[(int)GlandDim.Groove] - x[(int)GlandDim.Surface]) / 2.0,
            _ => x[(int)GlandDim.FaceDepth],
        };
    }

    /// <summary>The extrusion gap: the diametral clearance of a radial seal, the axial gap of a face seal.</summary>
    public static double Gap(SealKind kind, double[] x)
    {
        ArgumentNullException.ThrowIfNull(x);

        return kind switch
        {
            SealKind.Piston => x[(int)GlandDim.Surface] - x[(int)GlandDim.Clearance],
            SealKind.Rod => x[(int)GlandDim.Clearance] - x[(int)GlandDim.Surface],
            _ => x[(int)GlandDim.FaceGap],
        };
    }

    /// <summary>The ring diameter that sits on the reference diameter: the inside diameter, or the outside diameter d + 2W of an internal-pressure face seal.</summary>
    public static double RingSeatDiameter(GlandEnvironment env, double[] x)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(x);
        return env.HeldByOutsideDiameter ? RingOd(x) : x[(int)GlandDim.RingId];
    }

    /// <summary>The ring stretch in a case: (g·D_ref)/(f·d) − 1, a fraction (d is the outside diameter d + 2W for an internal-pressure face seal, where a negative value is the outside-diameter compression).</summary>
    public static double Stretch(GlandEnvironment env, GlandCase c, double[] x)
    {
        var (f, g) = Factors(env, c);
        return g * ReferenceDiameter(env.Kind, x) / (f * RingSeatDiameter(env, x)) - 1.0;
    }

    /// <summary>
    /// The compression of the ring outside diameter in a case, a fraction, where the outside diameter is held in the groove: a rod
    /// seal (against the housing groove root) and an internal-pressure face seal (against the groove outer wall),
    /// 1 − g·D_g/(f·(d + 2W)) and not below zero. Zero for every other seal.
    /// </summary>
    public static double OdCompression(GlandEnvironment env, GlandCase c, double[] x)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(x);

        if (env.Kind != SealKind.Rod && !env.HeldByOutsideDiameter)
            return 0.0;

        var (f, g) = Factors(env, c);
        return Math.Clamp(1.0 - g * x[(int)GlandDim.Groove] / (f * RingOd(x)), 0.0, MaxCompression);
    }

    /// <summary>The thickening of the section by a compressed outside diameter, 1/√(1 − c) (constant volume); 1 without compression.</summary>
    public static double Thickening(double compression) => 1.0 / Math.Sqrt(1.0 - Math.Clamp(compression, 0.0, MaxCompression));

    /// <summary>The effective cross-section in a case: f·W·(1 − reduction(stretch))/√(1 − c), mm (c the outside-diameter compression, zero unless the outside diameter is held).</summary>
    public static double EffectiveSection(GlandEnvironment env, GlandCase c, double[] x)
    {
        var (f, _) = Factors(env, c);
        var stretch = Stretch(env, c, x);

        // An internal-pressure face seal is stretched only when its outside diameter is smaller than the seat; a compressed one thickens instead.
        var reduction = Reduction(env.Relation, env.HeldByOutsideDiameter ? Math.Max(stretch, 0.0) : stretch, env.Nu);
        return f * x[(int)GlandDim.Section] * (1.0 - reduction) * Thickening(OdCompression(env, c, x));
    }

    /// <summary>The size factors of the sealing-surface part and of the groove metal in a case, for the gap: 1 + α(T − 20 °C) each.</summary>
    public static (double Surface, double Metal) GapFactors(GlandEnvironment env, GlandCase c)
    {
        ArgumentNullException.ThrowIfNull(env);

        if (c == GlandCase.Installed)
            return (1.0, 1.0);

        var t = c == GlandCase.Cold ? env.TMin : env.TMax;
        return (1.0 + (env.AlphaSurface ?? env.AlphaMetal) * (t - env.TRef), 1.0 + env.AlphaMetal * (t - env.TRef));
    }

    /// <summary>The diametral gap of a radial seal in a case, mm: the sealing-surface part (bore or rod) scaled by its own expansion, the groove-metal part by the groove metal's. A face seal's axial gap is as dimensioned.</summary>
    public static double GapAt(GlandEnvironment env, GlandCase c, double[] x)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(x);
        var (gs, gm) = GapFactors(env, c);

        return env.Kind switch
        {
            SealKind.Piston => gs * x[(int)GlandDim.Surface] - gm * x[(int)GlandDim.Clearance],
            SealKind.Rod => gm * x[(int)GlandDim.Clearance] - gs * x[(int)GlandDim.Surface],
            _ => x[(int)GlandDim.FaceGap],
        };
    }

    /// <summary>The largest gap over the reference, cold and hot temperatures, plus the bore dilation under pressure (radial seals), mm.</summary>
    public static double GapWorst(GlandEnvironment env, double[] x)
    {
        ArgumentNullException.ThrowIfNull(env);
        var worst = Math.Max(GapAt(env, GlandCase.Installed, x), Math.Max(GapAt(env, GlandCase.Cold, x), GapAt(env, GlandCase.Hot, x)));
        return env.Kind == SealKind.Face ? worst : worst + env.BoreDilation;
    }

    /// <summary>
    /// The ring area of the hot-end gland fill, mm², on a volume basis. A ring held circumferentially by the groove takes the whole
    /// volume change in its section: (π/4)·W²·f³/g with f³ = (1 + αΔT)³(1 + s) and the held circumference growing by g. Where the
    /// outside diameter is held (rod seal, internal-pressure face seal) the compression c = 1 − g·D_g/(f·OD), thermal growth
    /// included, is that same volume change: (π/4)·(f·W)²/(1 − c).
    /// </summary>
    public static double HotRingArea(GlandEnvironment env, double[] x)
    {
        var (f, g) = Factors(env, GlandCase.Hot);
        var isotropic = Math.PI / 4.0 * Math.Pow(f * x[(int)GlandDim.Section], 2);
        return env.Kind == SealKind.Rod || env.HeldByOutsideDiameter ? isotropic * Math.Pow(Thickening(OdCompression(env, GlandCase.Hot, x)), 2) : isotropic * (f / g);
    }

    /// <summary>The groove area in a case, mm²: (g·width)(g·depth) less the two bottom corner fillets.</summary>
    public static double GrooveArea(GlandEnvironment env, GlandCase c, double[] x)
    {
        var (_, g) = Factors(env, c);
        return g * x[(int)GlandDim.Width] * g * Depth(env.Kind, x) - 2.0 * (1.0 - Math.PI / 4.0) * Math.Pow(g * env.CornerRadius, 2);
    }

    /// <summary>The squeeze in a case: 1 − (g·depth)/W_eff.</summary>
    public static double Squeeze(GlandEnvironment env, GlandCase c, double[] x)
    {
        var (_, g) = Factors(env, c);
        return 1.0 - g * Depth(env.Kind, x) / EffectiveSection(env, c, x);
    }

    /// <summary>The ring outside diameter d + 2W, mm.</summary>
    public static double RingOd(double[] x)
    {
        ArgumentNullException.ThrowIfNull(x);
        return x[(int)GlandDim.RingId] + 2.0 * x[(int)GlandDim.Section];
    }

    /// <summary>The criterion as a function of the dimension vector <paramref name="x"/> (indexed by <see cref="GlandDim"/>).</summary>
    public static double Evaluate(GlandEnvironment env, GlandCriterion criterion, double[] x)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(x);

        switch (criterion)
        {
            case GlandCriterion.SqueezeMin:
                return Squeeze(env, GlandCase.Cold, x);

            case GlandCriterion.SqueezeMax:
                return Squeeze(env, GlandCase.Hot, x);

            case GlandCriterion.FillMax:
                return HotRingArea(env, x) / GrooveArea(env, GlandCase.Hot, x);

            case GlandCriterion.FillMin:
                return Math.PI / 4.0 * Math.Pow(EffectiveSection(env, GlandCase.Cold, x), 2) / GrooveArea(env, GlandCase.Cold, x);

            case GlandCriterion.Stretch:
                return Stretch(env, GlandCase.Installed, x);

            case GlandCriterion.OdCompression:
            {
                var od = RingOd(x);
                return (od - x[(int)GlandDim.Groove]) / od;
            }

            case GlandCriterion.Gap:
                return GapWorst(env, x);

            default:
                throw new ArgumentOutOfRangeException(nameof(criterion));
        }
    }

    /// <summary>Linearises a criterion about the mean dimensions with a secant sensitivity across each dimension's band. Dimensions with no spread, or that the criterion does not use, drop out.</summary>
    public static GlandLinearisation Linearise(GlandEnvironment env, GlandCriterion criterion, IReadOnlyList<GlandVar> vars)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(vars);

        var mid = new double[Enum.GetValues<GlandDim>().Length];

        foreach (var v in vars)
            mid[(int)v.Dim] = v.Mid;

        var value = Evaluate(env, criterion, mid);
        var terms = new List<(GlandVar, double)>();

        foreach (var v in vars.OrderBy(v => (int)v.Dim))
        {
            if (!(v.Half > 0.0))
                continue;

            var x = (double[])mid.Clone();
            var i = (int)v.Dim;
            x[i] = v.Mid + v.Half;
            var up = Evaluate(env, criterion, x);
            x[i] = v.Mid - v.Half;
            var down = Evaluate(env, criterion, x);
            var a = (up - down) / (2.0 * v.Half);

            if (a != 0.0 && double.IsFinite(a))
                terms.Add((v, a));
        }

        var offset = value - terms.Sum(t => t.Item2 * t.Item1.Mid);
        return new GlandLinearisation(value, terms, offset);
    }

    /// <summary>The exact (non-linear) value of the criterion at the corner of the box that its sensitivities say is the lowest or highest.</summary>
    public static double Corner(GlandEnvironment env, GlandCriterion criterion, IReadOnlyList<GlandVar> vars, bool highest)
    {
        var linear = Linearise(env, criterion, vars);
        var x = new double[Enum.GetValues<GlandDim>().Length];

        foreach (var v in vars)
            x[(int)v.Dim] = v.Mid;

        foreach (var (v, a) in linear.Terms)
            x[(int)v.Dim] = v.Mid + ((a > 0) == highest ? v.Half : -v.Half);

        return Evaluate(env, criterion, x);
    }

    /// <summary>
    /// Runs the engine on the criterion: worst case, RSS and capability index on the criterion linearised about the mean dimensions
    /// (the engine's tolerance-stack analysis), and a seeded Monte Carlo of the exact non-linear criterion counting the fraction
    /// below the lower and above the upper limit (the engine's Monte Carlo, with the model as the function). With no limit the
    /// simulation is skipped.
    /// </summary>
    public static GlandAnalysis Analyse(GlandEnvironment env, GlandCriterion criterion, IReadOnlyList<GlandVar> vars, double? lower, double? upper, int samples, ulong seed, CancellationToken cancellationToken = default)
    {
        var linear = Linearise(env, criterion, vars);
        var contributors = linear.Contributors();
        var requirement = lower is null && upper is null ? null : new StackRequirement(lower, upper);
        var stack = ToleranceStackEngine.Analyse(contributors, requirement);
        var simulation = requirement is null ? null : Simulate(env, criterion, vars, lower, upper, samples, seed, cancellationToken);
        return new GlandAnalysis(criterion, linear, stack, simulation);
    }

    /// <summary>
    /// The seeded Monte Carlo of the exact criterion: each dimension that varies is sampled in <see cref="GlandDim"/> order from its
    /// own distribution about its mean (normal about its σ, uniform or triangular over its half-band), and the engine's
    /// <see cref="MonteCarlo"/> evaluates <see cref="Evaluate"/> on every sample.
    /// </summary>
    public static MonteCarloResult Simulate(GlandEnvironment env, GlandCriterion criterion, IReadOnlyList<GlandVar> vars, double? lower, double? upper, int samples, ulong seed, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(vars);
        var mid = new double[Enum.GetValues<GlandDim>().Length];

        foreach (var v in vars)
            mid[(int)v.Dim] = v.Mid;

        var nominal = new Dictionary<string, double>(StringComparer.Ordinal);
        var distributions = new Dictionary<string, Distribution>(StringComparer.Ordinal);
        var keys = new List<(string Key, int Index)>();

        foreach (var v in vars.Where(v => v.Half > 0.0 && v.Sigma > 0.0).OrderBy(v => (int)v.Dim))
        {
            var key = $"x{(int)v.Dim}";
            keys.Add((key, (int)v.Dim));
            nominal[key] = v.Mid;
            distributions[key] = v.Distribution switch
            {
                ContributorDistribution.Uniform => Distribution.Uniform(v.Mid - v.Half, v.Mid + v.Half),
                ContributorDistribution.Triangular => Distribution.Triangular(v.Mid - v.Half, v.Mid, v.Mid + v.Half),
                _ => Distribution.Normal(v.Mid, v.Sigma),
            };
        }

        var x = (double[])mid.Clone();

        return MonteCarlo.Run(sample =>
        {
            foreach (var (key, index) in keys)
                x[index] = sample[key];

            var y = Evaluate(env, criterion, x);
            return double.IsFinite(y) ? y : (y < 0.0 ? -1e9 : 1e9);
        }, nominal, distributions, samples, seed, lower, upper, cancellationToken: cancellationToken);
    }
}
