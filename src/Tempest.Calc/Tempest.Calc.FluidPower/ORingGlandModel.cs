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

    /// <summary>The quadratic Y = 0.56 + 0.59·X − 0.0046·X² (X, Y in per cent), cited as Parker ORD 5700 §3.5 in Addendum B §B6 F3; not confirmed against the handbook.</summary>
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

    /// <summary>Gland fill at the hot end, on the unstretched cross-section.</summary>
    FillMax,

    /// <summary>Gland fill at the cold end, on the stretch-reduced cross-section.</summary>
    FillMin,

    /// <summary>The installed stretch of the ring inside diameter.</summary>
    Stretch,

    /// <summary>The installed compression of the ring outside diameter in a rod-seal groove.</summary>
    OdCompression,

    /// <summary>The extrusion gap (diametral for radial seals, axial for face seals).</summary>
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
public sealed record GlandEnvironment(SealKind Kind, ReductionRelation Relation, double Nu, double Alpha, double AlphaMetal, double TRef, double TMin, double TMax, double SwellMin, double SwellMax, double CornerRadius);

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

    /// <summary>The Parker quadratic is held at its vertex (64 % stretch): beyond it the fit turns downward and means nothing.</summary>
    public const double ParkerStretchCap = 64.0;

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
            ReductionRelation.Parker => x > 0.0 ? Math.Max(Parker(Math.Min(x * 100.0, ParkerStretchCap)), 0.0) / 100.0 : 0.0,
            ReductionRelation.Linear => Math.Min(nu * x, MaxReduction),
            _ => 0.0,
        };
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

    /// <summary>The ring stretch in a case: (g·D_ref)/(f·d) − 1, a fraction.</summary>
    public static double Stretch(GlandEnvironment env, GlandCase c, double[] x)
    {
        var (f, g) = Factors(env, c);
        return g * ReferenceDiameter(env.Kind, x) / (f * x[(int)GlandDim.RingId]) - 1.0;
    }

    /// <summary>The effective cross-section in a case: f·W·(1 − reduction(stretch)), mm.</summary>
    public static double EffectiveSection(GlandEnvironment env, GlandCase c, double[] x)
    {
        var (f, _) = Factors(env, c);
        return f * x[(int)GlandDim.Section] * (1.0 - Reduction(env.Relation, Stretch(env, c, x), env.Nu));
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
            {
                var (f, _) = Factors(env, GlandCase.Hot);
                return Math.PI / 4.0 * Math.Pow(f * x[(int)GlandDim.Section], 2) / GrooveArea(env, GlandCase.Hot, x);
            }

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
                return Gap(env.Kind, x);

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
    /// Runs the engine on the linearised criterion: worst case, RSS and capability against the limits, and a seeded Monte
    /// Carlo counting the fraction below the lower and above the upper limit. With no limit the simulation is skipped.
    /// </summary>
    public static GlandAnalysis Analyse(GlandEnvironment env, GlandCriterion criterion, IReadOnlyList<GlandVar> vars, double? lower, double? upper, int samples, ulong seed, CancellationToken cancellationToken = default)
    {
        var linear = Linearise(env, criterion, vars);
        var contributors = linear.Contributors();
        var requirement = lower is null && upper is null ? null : new StackRequirement(lower, upper);
        var stack = ToleranceStackEngine.Analyse(contributors, requirement);
        var simulation = requirement is null ? null : ToleranceStackEngine.Simulate(contributors, samples, seed, requirement, cancellationToken: cancellationToken);
        return new GlandAnalysis(criterion, linear, stack, simulation);
    }
}
