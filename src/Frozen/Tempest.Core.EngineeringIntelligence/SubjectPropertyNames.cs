using Tempest.Core.Materials;

namespace Tempest.Core.EngineeringIntelligence;

/// <summary>
/// The property names the subject adapters expose, so a rule can be
/// written against a name that will actually resolve.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this matters more than it looks.</b> A rule reading a
/// misspelled property name does not fail loudly — it reports
/// <see cref="AssessmentOutcome.NotRecorded"/>, forever, for every
/// subject. It looks like a data gap and is actually a broken rule.
/// Validation warns against a name nothing recognises, which is the only
/// point at which the mistake is visible.
/// </para>
/// <para>
/// <b>Open, not closed.</b> A name absent from here is legitimate: a rule
/// may read a domain-specific property a subject adapter exposes and this
/// list has not caught up with. The vocabulary is a check, not a gate —
/// the same middle ground `A1` took with
/// <see cref="MaterialPropertyNames"/>, and for the same reason.
/// </para>
/// <para>
/// Material property names are <b>not</b> restated here: they are
/// <see cref="MaterialPropertyNames"/>'s, and `A1` owns them. One concept,
/// one owner.
/// </para>
/// </remarks>
public static class SubjectPropertyNames
{
    // ----------------------------------------------------------------
    // Fasteners (A3)
    // ----------------------------------------------------------------

    /// <summary>The nominal thread diameter.</summary>
    public const string NominalDiameter = "NominalDiameter";

    /// <summary>The thread pitch.</summary>
    public const string ThreadPitch = "ThreadPitch";

    /// <summary>The nominal length.</summary>
    public const string NominalLength = "NominalLength";

    /// <summary>The spanner size across the flats.</summary>
    public const string WidthAcrossFlats = "WidthAcrossFlats";

    /// <summary>The published proof strength.</summary>
    public const string ProofStrength = "ProofStrength";

    /// <summary>The published minimum tensile strength of a fastener.</summary>
    public const string FastenerTensileStrength = "FastenerTensileStrength";

    /// <summary>The published proof load — a force, not a stress.</summary>
    public const string ProofLoad = "ProofLoad";

    /// <summary>The published minimum breaking load.</summary>
    public const string MinimumBreakingLoad = "MinimumBreakingLoad";

    // ----------------------------------------------------------------
    // Bearings (A4) and components (A5)
    // ----------------------------------------------------------------

    /// <summary>The bore fitted to a shaft.</summary>
    public const string BoreDiameter = "BoreDiameter";

    /// <summary>The overall outside diameter.</summary>
    public const string OutsideDiameter = "OutsideDiameter";

    /// <summary>The greatest rotational speed a source states.</summary>
    public const string MaximumSpeed = "MaximumSpeed";

    /// <summary>The torque a source rates the component at.</summary>
    public const string RatedTorque = "RatedTorque";

    /// <summary>A spring's own force-per-deflection rate.</summary>
    public const string SpringRate = "SpringRate";

    /// <summary>A gear's own module.</summary>
    public const string Module = "Module";

    /// <summary>The component's own mass.</summary>
    public const string Mass = "Mass";

    // ----------------------------------------------------------------
    // Manufacturing processes (A7) — the ends of a published capability band
    // ----------------------------------------------------------------

    /// <summary>The tightest tolerance a source states the process holds — the lower end of its published band.</summary>
    public const string FinestAchievableTolerance = "FinestAchievableTolerance";

    /// <summary>The loosest tolerance the published band reaches.</summary>
    public const string CoarsestAchievableTolerance = "CoarsestAchievableTolerance";

    /// <summary>The smoothest surface a source states the process leaves.</summary>
    public const string FinestSurfaceRoughness = "FinestSurfaceRoughness";

    /// <summary>The thinnest wall a source states the process can produce.</summary>
    public const string MinimumWallThickness = "MinimumWallThickness";

    /// <summary>The largest part dimension a source states the process handles.</summary>
    public const string MaximumPartSize = "MaximumPartSize";

    /// <summary>The smallest part dimension a source states the process handles.</summary>
    public const string MinimumPartSize = "MinimumPartSize";

    /// <summary>The heaviest part a source states the process handles.</summary>
    public const string MaximumPartMass = "MaximumPartMass";

    /// <summary>The smallest feature a source states the process resolves.</summary>
    public const string MinimumFeatureSize = "MinimumFeatureSize";

    // ----------------------------------------------------------------
    // Text attributes, across kinds
    // ----------------------------------------------------------------

    /// <summary>The subject's own family, as its library names it.</summary>
    public const string Family = "Family";

    /// <summary>The subject's own designation.</summary>
    public const string Designation = "Designation";

    /// <summary>A fastener's or material's property class or grade designation.</summary>
    public const string PropertyClass = "PropertyClass";

    /// <summary>A material's delivery or heat-treatment condition.</summary>
    public const string Condition = "Condition";

    /// <summary>A surface treatment, coating or finish designation.</summary>
    public const string SurfaceTreatment = "SurfaceTreatment";

    /// <summary>A process's own production-scale band.</summary>
    public const string ProductionScale = "ProductionScale";

    /// <summary>Every dimensioned property name declared here, excluding `A1`'s own.</summary>
    public static IReadOnlyList<string> DimensionedProperties { get; } =
    [
        NominalDiameter, ThreadPitch, NominalLength, WidthAcrossFlats,
        ProofStrength, FastenerTensileStrength, ProofLoad, MinimumBreakingLoad,
        BoreDiameter, OutsideDiameter, MaximumSpeed, RatedTorque, SpringRate, Module, Mass,
        FinestAchievableTolerance, CoarsestAchievableTolerance, FinestSurfaceRoughness,
        MinimumWallThickness, MaximumPartSize, MinimumPartSize, MaximumPartMass, MinimumFeatureSize,
    ];

    /// <summary>Every text attribute name declared here.</summary>
    public static IReadOnlyList<string> TextAttributes { get; } =
    [
        Family, Designation, PropertyClass, Condition, SurfaceTreatment, ProductionScale,
    ];

    private static readonly IReadOnlySet<string> Known =
        DimensionedProperties
            .Concat(TextAttributes)
            .Concat(MaterialPropertyNames.All)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Whether <paramref name="name"/> is a property name a subject
    /// adapter is known to expose — including every `A1` material property
    /// name, which this vocabulary defers to rather than restating.
    /// </summary>
    public static bool IsKnown(string? name) => name is not null && Known.Contains(name);
}
