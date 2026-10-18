namespace Tempest.Core.Governance;

/// <summary>
/// The one platform-wide answer to "must a second person sign this off?"
/// (Product Owner decision 2026-10-01, `ADR-0161`): whether a person may
/// approve, check, verify or release what they themselves authored,
/// submitted or changed.
/// </summary>
/// <remarks>
/// <para>
/// Every separation-of-duty rule in Core consults this one policy rather
/// than holding its own switch — today that is a quotation's approval
/// (<c>QuotationService.ApproveAsync</c>, runbook C3 and colour review
/// board B1) and evidence's own independent check
/// (<c>EvidenceService.RecordCheckAsync</c>, `ADR-0148`). A rule added
/// later consults it too.
/// </para>
/// <para>
/// <b>Off by default.</b> TempestOS is initially for a single-user
/// consultancy, where nobody else exists to approve anything. Off relaxes
/// only the "must be somebody else" test: an act still needs somebody
/// signed in, still writes its own audit rows, and records that it was a
/// self-approval made with second-person sign-off off, so the record never
/// claims an independence it did not have. On restores every rule exactly
/// as it stood before the switch existed.
/// </para>
/// </remarks>
public interface ISignOffPolicy
{
    /// <summary>
    /// Whether a second person is required — <see langword="true"/> when
    /// the separation-of-duty rules apply, <see langword="false"/> (the
    /// default) when the same person may author and approve.
    /// </summary>
    Task<bool> IsSecondPersonRequiredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns second-person sign-off on or off for every module, durably,
    /// and records an audit row naming who changed it, when, and the old
    /// and new value. Setting the value it already has records nothing.
    /// </summary>
    /// <returns><see langword="true"/> when the value changed.</returns>
    Task<bool> SetSecondPersonRequiredAsync(bool required, CancellationToken cancellationToken = default);
}
