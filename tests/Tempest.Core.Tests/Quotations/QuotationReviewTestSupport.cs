using Tempest.Core.Identity;
using Tempest.Core.Quotations;

namespace Tempest.Core.Tests.Quotations;

/// <summary>
/// Runbook C3: only an approved revision can be sent, and approval needs a
/// second person. Tests whose own concern is what happens after a send
/// (Accept, Decline, chase tasks, dashboards) take a quote through
/// submit → approve here, as a second principal, and restore whoever was
/// signed in before.
/// </summary>
internal static class QuotationReviewTestSupport
{
    public const string ReviewerId = "quotation-second-reviewer";

    /// <summary>Submits <paramref name="quotationId"/> for review as whoever is signed in, then approves it as <see cref="ReviewerId"/>.</summary>
    public static async Task<QuotationResult> SubmitAndApproveAsync(IQuotationService quotations, ICurrentPrincipalAccessor principals, Guid quotationId)
    {
        var submitted = await quotations.SubmitForReviewAsync(quotationId);
        Assert.True(submitted.Succeeded, submitted.Reason);

        var approved = await AsAsync(principals, ReviewerId, () => quotations.ApproveAsync(quotationId));
        Assert.True(approved.Succeeded, approved.Reason);
        return approved;
    }

    /// <summary>Submits, approves (as a second person) and sends <paramref name="quotationId"/>.</summary>
    public static async Task<QuotationResult> ApproveAndSendAsync(IQuotationService quotations, ICurrentPrincipalAccessor principals, Guid quotationId)
    {
        await SubmitAndApproveAsync(quotations, principals, quotationId);
        return await quotations.SendAsync(quotationId);
    }

    /// <summary>Runs <paramref name="act"/> signed in as <paramref name="identityId"/>, then restores whoever was signed in before.</summary>
    public static async Task<T> AsAsync<T>(ICurrentPrincipalAccessor principals, string identityId, Func<Task<T>> act)
    {
        var accessor = (CurrentPrincipalAccessor)principals;
        var previous = accessor.Current;
        accessor.SetCurrent(new PlatformPrincipal(new PlatformIdentity(identityId, identityId), ApplicationPermissions.LocalSession));
        try
        {
            return await act();
        }
        finally
        {
            accessor.SetCurrent(previous);
        }
    }
}
