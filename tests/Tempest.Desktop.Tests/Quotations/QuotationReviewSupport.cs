using Tempest.Core.Identity;
using Tempest.Core.Quotations;

namespace Tempest.Desktop.Tests.Quotations;

/// <summary>
/// Runbook C3: only an approved revision can be sent, and approval needs a
/// second person. Desktop tests whose own concern is what follows a send
/// take a quote through submit → approve here, approving as a second
/// session principal (the app's own "Switch person…" path,
/// <see cref="WorkspaceHost.SwitchPrincipal"/>) and switching back.
/// </summary>
internal static class QuotationReviewSupport
{
    public const string ReviewerId = "second-reviewer";

    /// <summary>Submits <paramref name="quotationId"/> as the current principal, then approves it as <see cref="ReviewerId"/>.</summary>
    public static async Task SubmitAndApproveAsync(WorkspaceHost host, IQuotationService quotations, Guid quotationId)
    {
        var submitted = await quotations.SubmitForReviewAsync(quotationId).ConfigureAwait(true);
        Assert.True(submitted.Succeeded, submitted.Reason);

        var approved = await AsReviewerAsync(host, () => quotations.ApproveAsync(quotationId)).ConfigureAwait(true);
        Assert.True(approved.Succeeded, approved.Reason);
    }

    /// <summary>Submits, approves (as a second person) and sends <paramref name="quotationId"/>.</summary>
    public static async Task<QuotationResult> ApproveAndSendAsync(WorkspaceHost host, IQuotationService quotations, Guid quotationId)
    {
        await SubmitAndApproveAsync(host, quotations, quotationId).ConfigureAwait(true);
        return await quotations.SendAsync(quotationId).ConfigureAwait(true);
    }

    /// <summary>Runs <paramref name="act"/> as <see cref="ReviewerId"/>, then switches back to whoever was the session principal.</summary>
    public static async Task<T> AsReviewerAsync<T>(WorkspaceHost host, Func<Task<T>> act)
    {
        var previous = host.SessionPrincipal;
        host.SwitchPrincipal(new SessionPrincipal(ReviewerId, "Second Reviewer", SessionRole.Checker));
        try
        {
            return await act().ConfigureAwait(true);
        }
        finally
        {
            if (previous is not null)
                host.SwitchPrincipal(previous);
        }
    }
}
