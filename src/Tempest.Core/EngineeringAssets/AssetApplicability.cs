using Tempest.Core.BusinessGovernance;

namespace Tempest.Core.EngineeringAssets;

/// <summary>The engineering discipline an asset belongs to.</summary>
/// <remarks>
/// A closed vocabulary because these are the disciplines TempestOS
/// reasons about, and an open string here would make it impossible to
/// ask "which mechanical templates do we hold?" without string matching.
/// <see cref="Other"/> exists so an asset outside them is still
/// recordable rather than mislabelled.
/// </remarks>
public enum EngineeringDiscipline
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Mechanical design and analysis.</summary>
    Mechanical,

    /// <summary>Structural analysis.</summary>
    Structural,

    /// <summary>Materials and metallurgy.</summary>
    Materials,

    /// <summary>Manufacturing and process engineering.</summary>
    Manufacturing,

    /// <summary>Electrical and electronic.</summary>
    Electrical,

    /// <summary>Control and instrumentation.</summary>
    Control,

    /// <summary>Thermal and fluid.</summary>
    ThermoFluids,

    /// <summary>Systems engineering and requirements.</summary>
    Systems,

    /// <summary>Quality, inspection and metrology.</summary>
    Quality,

    /// <summary>Something else.</summary>
    Other
}

/// <summary>
/// Where and when an engineering asset applies.
/// </summary>
/// <remarks>
/// <para>
/// Every dimension is optional, and an unstated dimension is read as
/// <em>this asset does not restrict on that dimension</em> — which is the
/// opposite convention from `P03`'s <c>CommercialApplicability</c>, and
/// deliberately so. A price that does not say which supplier it is from
/// applies to no supplier in particular; a review checklist that does not
/// say which discipline it is for applies to all of them. Absence means
/// "unrestricted" here and "unknown" there, and conflating the two would
/// be wrong in one domain or the other.
/// </para>
/// <para>
/// The one exception is <see cref="Validity"/>: an asset outside its own
/// effective period does not apply, whichever way the other dimensions
/// read.
/// </para>
/// </remarks>
public sealed record AssetApplicability
{
    /// <summary>The disciplines the asset is for. Never <see langword="null"/>; empty means all.</summary>
    public IReadOnlyList<EngineeringDiscipline> Disciplines { get; init; } = [];

    /// <summary>The projects the asset is for, by project identifier. Never <see langword="null"/>; empty means all.</summary>
    public IReadOnlyList<string> ProjectIdentifiers { get; init; } = [];

    /// <summary>The kinds of subject the asset is for — a component, an assembly, a system. Never <see langword="null"/>; empty means all.</summary>
    public IReadOnlyList<string> SubjectKinds { get; init; } = [];

    /// <summary>Over what period it applies. <see langword="null"/> where it always has.</summary>
    public EffectivePeriod? Validity { get; init; }

    /// <summary>Anything else limiting where it applies, in plain words. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> Conditions { get; init; } = [];

    /// <summary>An applicability that restricts nothing.</summary>
    public static AssetApplicability Unrestricted { get; } = new();

    /// <summary>Whether the asset restricts on any dimension at all.</summary>
    public bool IsRestricted =>
        Disciplines.Count > 0 || ProjectIdentifiers.Count > 0 || SubjectKinds.Count > 0
        || Validity is not null || Conditions.Count > 0;

    /// <summary>Whether the asset has run past its own effective period as at <paramref name="asAt"/>.</summary>
    public bool IsExpiredAt(DateOnly asAt) => Validity?.HasExpiredBy(asAt) ?? false;

    /// <summary>Whether the asset applies to <paramref name="discipline"/>.</summary>
    public bool CoversDiscipline(EngineeringDiscipline discipline) =>
        Disciplines.Count == 0 || Disciplines.Contains(discipline);

    /// <summary>Whether the asset applies to <paramref name="projectIdentifier"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="projectIdentifier"/> is null, empty, or whitespace.</exception>
    public bool CoversProject(string projectIdentifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectIdentifier);

        return ProjectIdentifiers.Count == 0
               || ProjectIdentifiers.Contains(projectIdentifier.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Whether the asset applies to <paramref name="subjectKind"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="subjectKind"/> is null, empty, or whitespace.</exception>
    public bool CoversSubjectKind(string subjectKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectKind);

        return SubjectKinds.Count == 0
               || SubjectKinds.Contains(subjectKind.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Whether the asset applies to <paramref name="enquiry"/> on every dimension the enquiry states.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="enquiry"/> is <see langword="null"/>.</exception>
    public bool AppliesTo(AssetEnquiry enquiry)
    {
        ArgumentNullException.ThrowIfNull(enquiry);

        if (enquiry.Discipline is { } discipline && !CoversDiscipline(discipline))
            return false;

        if (enquiry.ProjectIdentifier is { } project && !CoversProject(project))
            return false;

        if (enquiry.SubjectKind is { } subjectKind && !CoversSubjectKind(subjectKind))
            return false;

        if (enquiry.AsAt is { } date && Validity is { } validity && !validity.Contains(date))
            return false;

        return true;
    }
}

/// <summary>What a caller is looking for an engineering asset to cover.</summary>
/// <remarks>
/// Every dimension is optional; an unstated one leaves that dimension
/// open rather than requiring the asset to be silent on it.
/// </remarks>
public sealed record AssetEnquiry
{
    /// <summary>The discipline. <see langword="null"/> to leave it open.</summary>
    public EngineeringDiscipline? Discipline { get; init; }

    /// <summary>The project. <see langword="null"/> to leave it open.</summary>
    public string? ProjectIdentifier { get; init; }

    /// <summary>The kind of subject. <see langword="null"/> to leave it open.</summary>
    public string? SubjectKind { get; init; }

    /// <summary>The date the asset must be effective on. <see langword="null"/> to leave it open.</summary>
    public DateOnly? AsAt { get; init; }
}
