using Tempest.Core.BusinessGovernance;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessOperations;

/// <summary>
/// Where an operational record has got to.
/// </summary>
/// <remarks>
/// A second axis from <see cref="ReferenceValidationState"/>, on the
/// reasoning `ADR-0129` set out for `P07`. The lifecycle state says how
/// far the record got through governance; this says where the thing it
/// describes has got to. A released, validated record of an open
/// non-conformance is an accurate record.
/// </remarks>
public enum OperationalState
{
    /// <summary>Raised and not yet worked on.</summary>
    Open,

    /// <summary>Being worked on.</summary>
    InProgress,

    /// <summary>Waiting on somebody outside the organisation.</summary>
    AwaitingExternal,

    /// <summary>Waiting on a decision or approval inside it.</summary>
    AwaitingInternal,

    /// <summary>Done.</summary>
    Closed,

    /// <summary>Deliberately not being taken further, with a reason.</summary>
    Cancelled
}

/// <summary>What <see cref="OperationalState"/> means.</summary>
public static class OperationalStates
{
    /// <summary>Whether the record still needs somebody's attention.</summary>
    public static bool IsOutstanding(OperationalState state) =>
        state is OperationalState.Open
            or OperationalState.InProgress
            or OperationalState.AwaitingExternal
            or OperationalState.AwaitingInternal;

    /// <summary>Whether the record is finished, however it finished.</summary>
    public static bool IsFinished(OperationalState state) =>
        state is OperationalState.Closed or OperationalState.Cancelled;

    /// <summary>Whether progress depends on somebody the organisation does not control.</summary>
    public static bool IsBlockedExternally(OperationalState state) => state == OperationalState.AwaitingExternal;
}

/// <summary>
/// A reference to a party the organisation deals with.
/// </summary>
/// <remarks>
/// <para>
/// The type that stops `P04` growing a second supplier database. A
/// purchasing record points at a `P03` supplier by record Id; a customer
/// record points at a `P04` organisation; and where the party is neither,
/// the name is carried as text and <see cref="IsResolved"/> says so
/// (`ADR-0142`).
/// </para>
/// <para>
/// The unresolved case is not a defect to be prevented. A quotation
/// arrives from a company nobody has entered yet, and refusing to record
/// it until somebody does is how operational software becomes the thing
/// people work around.
/// </para>
/// </remarks>
/// <param name="Kind">What sort of party it is.</param>
/// <param name="DisplayName">What the party is called. Required.</param>
/// <param name="OrganisationRecordId">The `P04` organisation record, where the party is one. <see langword="null"/> otherwise.</param>
/// <param name="SupplierRecordId">The `P03` supplier record, where the party is one. <see langword="null"/> otherwise.</param>
public sealed record PartyReference(
    PartyKind Kind,
    string DisplayName,
    string? OrganisationRecordId = null,
    string? SupplierRecordId = null)
{
    /// <summary>What the party is called.</summary>
    public string DisplayName { get; } = string.IsNullOrWhiteSpace(DisplayName)
        ? throw new ArgumentException("A party reference must name the party.", nameof(DisplayName))
        : DisplayName.Trim();

    /// <summary>Whether the party resolves to a governed record rather than to a name.</summary>
    public bool IsResolved => OrganisationRecordId is not null || SupplierRecordId is not null;

    /// <summary>A party the organisation has not yet entered anywhere.</summary>
    /// <exception cref="ArgumentException"><paramref name="displayName"/> is null, empty, or whitespace.</exception>
    public static PartyReference Unresolved(PartyKind kind, string displayName) => new(kind, displayName);

    /// <summary>A party that is a registered `P03` supplier.</summary>
    /// <exception cref="ArgumentException">Either argument is null, empty, or whitespace.</exception>
    public static PartyReference Supplier(string displayName, string supplierRecordId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(supplierRecordId);

        return new PartyReference(PartyKind.Supplier, displayName, SupplierRecordId: supplierRecordId.Trim());
    }

    /// <summary>A party that is a registered `P04` organisation.</summary>
    /// <exception cref="ArgumentException">Either argument is null, empty, or whitespace.</exception>
    public static PartyReference Organisation(PartyKind kind, string displayName, string organisationRecordId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organisationRecordId);

        return new PartyReference(kind, displayName, OrganisationRecordId: organisationRecordId.Trim());
    }
}

/// <summary>What sort of party the organisation is dealing with.</summary>
public enum PartyKind
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Somebody who buys, or might buy.</summary>
    Customer,

    /// <summary>Somebody who might buy and has not yet.</summary>
    Prospect,

    /// <summary>Somebody the organisation buys from.</summary>
    Supplier,

    /// <summary>Somebody who does work under the organisation's direction.</summary>
    Subcontractor,

    /// <summary>An auditor, a certification body, a regulator.</summary>
    Authority,

    /// <summary>Somebody the organisation works alongside.</summary>
    Partner,

    /// <summary>Something else.</summary>
    Other
}

/// <summary>
/// The facts every operational record carries.
/// </summary>
/// <remarks>
/// Composed rather than inherited, following `P05`'s
/// <c>AssetGovernanceFacts</c> and `P07`'s <c>BusinessGovernanceFacts</c>.
/// `P04` records share these facts and share no hierarchy.
/// </remarks>
public sealed record OperationalFacts
{
    /// <summary>Where the thing described has got to.</summary>
    public OperationalState State { get; init; } = OperationalState.Open;

    /// <summary>Who is responsible for it. <see langword="null"/> where nobody is.</summary>
    public string? OwnerPrincipalId { get; init; }

    /// <summary>Who raised it. <see langword="null"/> where unrecorded.</summary>
    public string? RaisedByPrincipalId { get; init; }

    /// <summary>When it was raised. <see langword="null"/> where unrecorded.</summary>
    public DateOnly? RaisedOn { get; init; }

    /// <summary>When it is needed by. <see langword="null"/> where nobody said.</summary>
    public DateOnly? DueBy { get; init; }

    /// <summary>When it was finished. <see langword="null"/> until it is.</summary>
    public DateOnly? ClosedOn { get; init; }

    /// <summary>Why it was cancelled, where it was. <see langword="null"/> otherwise.</summary>
    public string? CancellationReason { get; init; }

    /// <summary>The project it belongs to. <see langword="null"/> where it belongs to none.</summary>
    /// <remarks>
    /// A reference into the existing project architecture in
    /// <c>Tempest.App/Projects</c>, never a copy of it. `P04` builds no
    /// project model (`ADR-0142`).
    /// </remarks>
    public Guid? ProjectId { get; init; }

    /// <summary>How sensitive the record is.</summary>
    public ConfidentialityClassification Classification { get; init; } = ConfidentialityClassification.Internal;

    /// <summary>Supporting material. Never <see langword="null"/>.</summary>
    public IReadOnlyList<BusinessEvidence> Evidence { get; init; } = [];

    /// <summary>Governed records this one rests on, at the revisions relied on. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReferencePin> SourcePins { get; init; } = [];

    /// <summary>Whether the record still needs somebody's attention.</summary>
    public bool IsOutstanding => OperationalStates.IsOutstanding(State);

    /// <summary>Whether it is finished, however it finished.</summary>
    public bool IsFinished => OperationalStates.IsFinished(State);

    /// <summary>Whether anybody is responsible for it.</summary>
    public bool IsOwned => !string.IsNullOrWhiteSpace(OwnerPrincipalId);

    /// <summary>Whether it is past its own due date as at <paramref name="asAt"/>.</summary>
    public bool IsOverdueAt(DateOnly asAt) => IsOutstanding && DueBy is { } due && due < asAt;

    /// <summary>How many days past its due date, or <see langword="null"/> where it has none or is not yet due.</summary>
    public int? DaysOverdueAt(DateOnly asAt) =>
        IsOverdueAt(asAt) ? asAt.DayNumber - DueBy!.Value.DayNumber : null;

    /// <summary>Whether it is closed without a closing date, or cancelled without a reason.</summary>
    /// <remarks>
    /// Both are the same failure: a record that stopped being worked on
    /// and cannot say when or why.
    /// </remarks>
    public bool IsIncompletelyClosed =>
        (State == OperationalState.Closed && ClosedOn is null)
        || (State == OperationalState.Cancelled && string.IsNullOrWhiteSpace(CancellationReason));
}
