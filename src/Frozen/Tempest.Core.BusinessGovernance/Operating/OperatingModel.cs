using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Operating;

/// <summary>What a resource is, for capacity purposes.</summary>
public enum ResourceKind
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>A person employed by the organisation.</summary>
    Employee,

    /// <summary>A person engaged as a contractor.</summary>
    Contractor,

    /// <summary>A subcontracted organisation.</summary>
    Subcontractor,

    /// <summary>A machine, instrument or rig.</summary>
    Equipment,

    /// <summary>A software licence or service the work depends on.</summary>
    Software,

    /// <summary>Premises or space.</summary>
    Facility
}

/// <summary>What limits the organisation from doing more.</summary>
/// <remarks>
/// The question C7 exists to answer is "what stops us growing?", and it
/// is answerable only if constraints are named rather than felt.
/// </remarks>
public enum ConstraintKind
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Not enough hours.</summary>
    Capacity,

    /// <summary>Nobody has the skill.</summary>
    Capability,

    /// <summary>One person is the single point of failure.</summary>
    KeyPerson,

    /// <summary>Cash, or the ability to fund work before it is paid for.</summary>
    Working_Capital,

    /// <summary>Equipment, tooling or instruments.</summary>
    Equipment,

    /// <summary>Software, licences or infrastructure.</summary>
    Systems,

    /// <summary>A supplier or subcontractor the organisation depends on.</summary>
    SupplierDependency,

    /// <summary>Accreditation, insurance or another permission to trade.</summary>
    Accreditation,

    /// <summary>Space.</summary>
    Facilities,

    /// <summary>Demand — the organisation could do more and nobody is asking.</summary>
    Demand,

    /// <summary>Something else, described in the record.</summary>
    Other
}

/// <summary>
/// A capability the organisation has, or knows it needs.
/// </summary>
/// <remarks>
/// Capability is separate from capacity, and confusing the two is the most
/// common planning error at small scale. Capacity is hours; capability is
/// whether anybody can do the work at all. Doubling the hours of somebody
/// who cannot do fatigue analysis does not give the organisation fatigue
/// analysis.
/// </remarks>
/// <param name="Code">The capability's own identifier. Required.</param>
/// <param name="Name">What it is. Required.</param>
/// <param name="IsHeld">Whether the organisation actually has it today.</param>
/// <param name="HeldBy">Who holds it. Never <see langword="null"/>; a capability held by exactly one person is a key-person risk.</param>
/// <param name="ServiceCodes">The services that depend on it. Never <see langword="null"/>.</param>
/// <param name="AcquisitionRoute">How it would be acquired if it is not held — hire, train, subcontract, buy. <see langword="null"/> where it is held.</param>
/// <param name="AcquisitionCost">What acquiring it would cost. <see langword="null"/> where unknown or not applicable.</param>
public sealed record OperatingCapability(
    string Code,
    string Name,
    bool IsHeld = false,
    IReadOnlyList<string>? HeldBy = null,
    IReadOnlyList<string>? ServiceCodes = null,
    string? AcquisitionRoute = null,
    Money? AcquisitionCost = null)
{
    /// <summary>The capability's own identifier.</summary>
    public string Code { get; } = string.IsNullOrWhiteSpace(Code)
        ? throw new ArgumentException("A capability must carry its own code.", nameof(Code))
        : Code.Trim();

    /// <summary>What it is.</summary>
    public string Name { get; } = string.IsNullOrWhiteSpace(Name)
        ? throw new ArgumentException("A capability must be named.", nameof(Name))
        : Name.Trim();

    /// <summary>Who holds it.</summary>
    public IReadOnlyList<string> HeldBy { get; init; } = HeldBy ?? [];

    /// <summary>The services that depend on it.</summary>
    public IReadOnlyList<string> ServiceCodes { get; init; } = ServiceCodes ?? [];

    /// <summary>Whether exactly one person holds a capability the organisation sells.</summary>
    public bool IsSinglePointOfFailure => IsHeld && HeldBy.Count == 1;

    /// <summary>Whether the organisation sells work that depends on a capability it does not have.</summary>
    public bool IsSoldButNotHeld => !IsHeld && ServiceCodes.Count > 0;
}

/// <summary>
/// How much of a resource is available, and how much of it is assumed to
/// be productive.
/// </summary>
/// <remarks>
/// <para>
/// The utilisation assumption is the one that quietly breaks capacity
/// plans. A person is not available 220 days a year to do client work:
/// there is business development, admin, training, illness and the work
/// that does not get billed. Recording gross days and a utilisation
/// assumption separately keeps the optimism visible and adjustable.
/// </para>
/// <para>
/// Both figures are assumptions, not measurements. Where the organisation
/// has actual timesheet data, that is an actual and belongs in C5.
/// </para>
/// </remarks>
/// <param name="ResourceCode">The resource's own identifier. Required.</param>
/// <param name="Name">Who or what it is. Required.</param>
/// <param name="Kind">What kind of resource it is.</param>
/// <param name="GrossDaysPerPeriod">How many days exist in the period before anything is taken off.</param>
/// <param name="UtilisationAssumption">The proportion assumed to be productive, between 0 and 1.</param>
/// <param name="CapabilityCodes">What this resource can actually do. Never <see langword="null"/>.</param>
/// <param name="CostPerPeriod">What the resource costs over the period. <see langword="null"/> where not recorded.</param>
/// <param name="IsCommitted">Whether the resource is actually secured, as distinct from planned.</param>
public sealed record ResourceCapacity(
    string ResourceCode,
    string Name,
    ResourceKind Kind,
    decimal GrossDaysPerPeriod,
    decimal UtilisationAssumption,
    IReadOnlyList<string>? CapabilityCodes = null,
    Money? CostPerPeriod = null,
    bool IsCommitted = true)
{
    /// <summary>The resource's own identifier.</summary>
    public string ResourceCode { get; } = string.IsNullOrWhiteSpace(ResourceCode)
        ? throw new ArgumentException("A resource must carry its own code.", nameof(ResourceCode))
        : ResourceCode.Trim();

    /// <summary>Who or what it is.</summary>
    public string Name { get; } = string.IsNullOrWhiteSpace(Name)
        ? throw new ArgumentException("A resource must be named.", nameof(Name))
        : Name.Trim();

    /// <summary>How many days exist in the period before anything is taken off.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="GrossDaysPerPeriod"/> is negative.</exception>
    public decimal GrossDaysPerPeriod { get; } = GrossDaysPerPeriod < 0m
        ? throw new ArgumentOutOfRangeException(nameof(GrossDaysPerPeriod), GrossDaysPerPeriod, "A resource cannot have negative days.")
        : GrossDaysPerPeriod;

    /// <summary>The proportion assumed to be productive.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="UtilisationAssumption"/> is outside 0–1.</exception>
    public decimal UtilisationAssumption { get; } = UtilisationAssumption is < 0m or > 1m
        ? throw new ArgumentOutOfRangeException(
            nameof(UtilisationAssumption),
            UtilisationAssumption,
            "A utilisation assumption is a proportion between 0 and 1. Nobody is productive more than all of the time.")
        : UtilisationAssumption;

    /// <summary>What this resource can actually do.</summary>
    public IReadOnlyList<string> CapabilityCodes { get; init; } = CapabilityCodes ?? [];

    /// <summary>The days actually assumed to be available for chargeable work.</summary>
    /// <remarks>Exact decimal arithmetic, and deterministic: the same inputs always give the same figure.</remarks>
    public decimal ProductiveDays => GrossDaysPerPeriod * UtilisationAssumption;

    /// <summary>Whether the utilisation assumption is one an organisation would struggle to sustain.</summary>
    /// <remarks>
    /// A reporting heuristic, not a rule. Above 85 per cent leaves no room
    /// for business development, admin, illness or the work that overruns,
    /// and a plan resting on it is a plan resting on nothing going wrong.
    /// </remarks>
    public bool IsOptimisticUtilisation => UtilisationAssumption > 0.85m;
}
