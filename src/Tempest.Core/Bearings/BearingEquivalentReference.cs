namespace Tempest.Core.Bearings;

/// <summary>
/// A designation another source uses for what that source claims is the
/// same bearing.
/// </summary>
/// <remarks>
/// <b>Recording a claim is not making one.</b> This library never derives
/// an equivalence from matching dimensions, and never presents a recorded
/// equivalence as interchangeability: whether two bearings may be
/// substituted for one another depends on load, speed, clearance, fit,
/// tolerance and duty, and is engineering judgement a future selection
/// capability owns. <see cref="ClaimedBy"/> is required precisely so that
/// every equivalence in this library can be attributed to whoever claimed
/// it.
/// </remarks>
/// <param name="Manufacturer">The manufacturer whose designation this is.</param>
/// <param name="Designation">The designation itself, verbatim.</param>
/// <param name="ClaimedBy">Who claims the equivalence (an organisation, a catalogue, an internal reviewer). Required.</param>
/// <param name="Notes">What the claim covers or excludes, as the claimant states it. <see langword="null"/> if none.</param>
public sealed record BearingEquivalentReference(
    string Manufacturer,
    string Designation,
    string ClaimedBy,
    string? Notes = null)
{
    /// <summary>Who claims the equivalence.</summary>
    public string ClaimedBy { get; } = string.IsNullOrWhiteSpace(ClaimedBy)
        ? throw new ArgumentException("An equivalence must record who claims it — this library never asserts one on its own authority.", nameof(ClaimedBy))
        : ClaimedBy;
}
