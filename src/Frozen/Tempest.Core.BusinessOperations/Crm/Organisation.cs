namespace Tempest.Core.BusinessOperations.Crm;

// WP 18.0C (D-028): split out of the live
// src/Tempest.Core/BusinessOperations/Crm/Organisation.cs the same day
// InteractionCatalog moved; Organisation and Contact stayed live.

/// <summary>How an interaction happened.</summary>
public enum InteractionChannel
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>A telephone call.</summary>
    Telephone,

    /// <summary>An e-mail.</summary>
    Email,

    /// <summary>A meeting in person.</summary>
    MeetingInPerson,

    /// <summary>A meeting by video.</summary>
    MeetingRemote,

    /// <summary>A site visit.</summary>
    SiteVisit,

    /// <summary>Something written and sent.</summary>
    Correspondence,

    /// <summary>Met at an event.</summary>
    Event,

    /// <summary>Something else.</summary>
    Other
}

/// <summary>
/// Something that passed between the business and an organisation.
/// </summary>
/// <remarks>
/// Distinct from `P07`'s <c>OpportunityInteraction</c>, which records
/// contact <em>about a specific opportunity</em>. Most contact is not
/// about an opportunity — a courtesy call, a technical question, a
/// complaint — and a CRM that can only record pursuit misses most of the
/// relationship. Where an interaction <em>is</em> about an opportunity,
/// <see cref="OpportunityReference"/> links it.
/// </remarks>
public sealed record Interaction
{
    /// <summary>The reference the interaction is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>The organisation, by reference. Required.</summary>
    public required string OrganisationReference { get; init; }

    /// <summary>What happened. Required.</summary>
    public required string Summary { get; init; }

    /// <summary>When. Required.</summary>
    public required DateOnly OccurredOn { get; init; }

    /// <summary>How.</summary>
    public InteractionChannel Channel { get; init; } = InteractionChannel.Unspecified;

    /// <summary>The contacts involved, by reference. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> ContactReferences { get; init; } = [];

    /// <summary>Who from this business took part. <see langword="null"/> where unrecorded.</summary>
    public string? OwnPrincipalId { get; init; }

    /// <summary>The `P07` opportunity it concerned, where it concerned one. <see langword="null"/> otherwise.</summary>
    public string? OpportunityReference { get; init; }

    /// <summary>What was agreed to happen next. <see langword="null"/> where nothing was.</summary>
    public string? AgreedNextAction { get; init; }

    /// <summary>By when. <see langword="null"/> where nobody said.</summary>
    public DateOnly? NextActionDue { get; init; }

    /// <summary>Whether the next action has been done.</summary>
    public bool NextActionCompleted { get; init; }

    /// <summary>Anything else about it. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>Whether something was agreed and has not been done.</summary>
    public bool HasOutstandingAction =>
        !string.IsNullOrWhiteSpace(AgreedNextAction) && !NextActionCompleted;

    /// <summary>Whether the agreed action is past its date as at <paramref name="asAt"/>.</summary>
    public bool IsActionOverdueAt(DateOnly asAt) =>
        HasOutstandingAction && NextActionDue is { } due && due < asAt;

    /// <summary>The case-insensitive key <see cref="Reference"/> is indexed under.</summary>
    public string ReferenceKey => ReferenceKeyFor(Reference);

    /// <summary>The case-insensitive key <paramref name="reference"/> would be indexed under.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    public static string ReferenceKeyFor(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        return reference.Trim().ToUpperInvariant();
    }
}
