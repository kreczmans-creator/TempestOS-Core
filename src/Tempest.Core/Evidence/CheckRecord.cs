namespace Tempest.Core.Evidence;

/// <summary>What a checker concluded when reviewing a piece of evidence.</summary>
public enum CheckOutcome
{
    /// <summary>The checker accepted the evidence as it stands.</summary>
    Accepted,

    /// <summary>The checker accepted the evidence, with comments recorded in <see cref="CheckRecord.Statement"/>.</summary>
    AcceptedWithComments,

    /// <summary>The checker rejected the evidence.</summary>
    Rejected,
}

/// <summary>
/// A record of a piece of evidence being checked — the client's own review,
/// entered by hand, or (when <c>Evidence:IndependentCheck</c> is on) a
/// second principal's own recorded act (`ADR-0148`).
/// </summary>
/// <param name="CheckerName">The checker's name, as typed — never validated as an identity, because the checker is very often not a Tempest principal at all (a client's own reviewer).</param>
/// <param name="CheckerOrganisation">The checker's organisation, as typed.</param>
/// <param name="CheckerIdentityId">
/// The checker's own <c>Identity.Id</c>, when the independence rule is on
/// and the checker is therefore a second signed-in principal;
/// <see langword="null"/> when the rule is off and the check was entered
/// by hand on the author's own behalf.
/// </param>
/// <param name="RecordedByIdentityId">The acting principal who recorded this check — always set, whether or not <paramref name="CheckerIdentityId"/> is.</param>
/// <param name="DateUtc">When the check was recorded.</param>
/// <param name="Statement">The checker's own statement, verbatim.</param>
/// <param name="Outcome">What the checker concluded.</param>
public sealed record CheckRecord(
    string CheckerName,
    string CheckerOrganisation,
    string? CheckerIdentityId,
    string RecordedByIdentityId,
    DateTimeOffset DateUtc,
    string Statement,
    CheckOutcome Outcome);
