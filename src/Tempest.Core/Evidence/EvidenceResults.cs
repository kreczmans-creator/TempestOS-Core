namespace Tempest.Core.Evidence;

/// <summary>Why an <see cref="IEvidenceService"/> act was refused, or <see cref="None"/> if it was not.</summary>
/// <remarks>
/// A refusal is a first-class answer here, exactly as
/// <c>Tempest.Core.Calculations.BracketCheckRefusal</c> is for a governed
/// bracket check: citing an unreleased record, checking out of turn, or a
/// checker who is also the author are ordinary engineering-governance
/// findings a surface should show, not error conditions. Genuinely invalid
/// input (a blank checker name, an evidence id that is not even a
/// <see cref="Guid"/>) still throws.
/// </remarks>
public enum EvidenceRefusal
{
    /// <summary>Nothing was refused.</summary>
    None,

    /// <summary>No evidence is registered under the requested id.</summary>
    EvidenceNotFound,

    /// <summary>No reference record is registered under the requested library and id.</summary>
    RecordNotFound,

    /// <summary>The reference record exists but has not been released, so evidence may not cite it.</summary>
    RecordNotReleased,

    /// <summary>The requested status move is not in <c>EvidenceStatusTransitions</c>' own permitted table.</summary>
    TransitionNotPermitted,

    /// <summary>The independent-check rule is on, and the acting principal is also this evidence's own author.</summary>
    CheckerMustDifferFromAuthor,

    /// <summary>The independent-check rule is on, and nobody is signed in to be held to the check.</summary>
    NoPrincipalSignedIn,

    /// <summary>The evidence is <see cref="EvidenceStatus.Issued"/> and its subject tag may no longer be changed — revise it first (`WP 18.2B`).</summary>
    SubjectLockedAfterIssue,
}

/// <summary>The outcome of <see cref="IEvidenceService.CiteAsync"/>: either a recorded citation, or a refusal that says exactly what was missing.</summary>
/// <param name="Refusal">Why the citation was refused, or <see cref="EvidenceRefusal.None"/>.</param>
/// <param name="Reason">The refusal in a sentence an engineer can read, naming the record. <see langword="null"/> when nothing was refused.</param>
/// <param name="Evidence">The evidence cited against, when it could be resolved.</param>
/// <param name="Citation">The citation recorded. <see langword="null"/> when the citation was refused.</param>
public sealed record EvidenceCitationResult(EvidenceRefusal Refusal, string? Reason, Evidence? Evidence, EvidenceCitation? Citation)
{
    /// <summary>Whether the citation was actually recorded.</summary>
    public bool Succeeded => Refusal == EvidenceRefusal.None && Citation is not null;
}

/// <summary>The outcome of an <see cref="IEvidenceService"/> act that moves <see cref="Evidence.Status"/> (check, issue, revise): either it happened, or a refusal that says why it did not.</summary>
/// <param name="Refusal">Why the act was refused, or <see cref="EvidenceRefusal.None"/>.</param>
/// <param name="Reason">The refusal in a sentence an engineer can read. <see langword="null"/> when nothing was refused.</param>
/// <param name="Evidence">The evidence acted on. For a refused <c>Revise</c>, the predecessor; for a successful one, the new revision.</param>
public sealed record EvidenceActionResult(EvidenceRefusal Refusal, string? Reason, Evidence? Evidence)
{
    /// <summary>Whether the act actually happened.</summary>
    public bool Succeeded => Refusal == EvidenceRefusal.None;
}
