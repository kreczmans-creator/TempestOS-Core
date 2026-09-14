using Tempest.Core.Quotations;

namespace Tempest.Core.Tests.Quotations;

/// <summary>
/// Exhaustively pins <c>QuotationStatusTransitions</c>' own permitted table
/// (`WP 19.5A`, `ADR-0152`) — every one of the sixteen (from, to) pairs
/// across the four-value vocabulary, mirroring
/// <c>Invoicing.InvoiceRequestStatusTransitionsTests</c>' own exhaustive-
/// table discipline. <c>QuotationStatusTransitions</c> is <c>internal</c>;
/// this project sees it directly via <c>InternalsVisibleTo</c>.
/// </summary>
public sealed class QuotationStatusTransitionsTests
{
    public static TheoryData<QuotationStatus, QuotationStatus, bool> EveryPair()
    {
        var permitted = new HashSet<(QuotationStatus, QuotationStatus)>
        {
            // Draft: SendAsync.
            (QuotationStatus.Draft, QuotationStatus.Sent),

            // Sent: AcceptAsync or DeclineAsync.
            (QuotationStatus.Sent, QuotationStatus.Accepted),
            (QuotationStatus.Sent, QuotationStatus.Declined),
        };

        var data = new TheoryData<QuotationStatus, QuotationStatus, bool>();

        foreach (var from in QuotationStatusTransitions.AllStatuses)
        {
            foreach (var to in QuotationStatusTransitions.AllStatuses)
                data.Add(from, to, permitted.Contains((from, to)));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryPair))]
    public void IsPermitted_MatchesTheAuditedTable_ForEveryPair(QuotationStatus from, QuotationStatus to, bool expected)
    {
        Assert.Equal(expected, QuotationStatusTransitions.IsPermitted(from, to));
    }

    [Theory]
    [InlineData(QuotationStatus.Accepted)]
    [InlineData(QuotationStatus.Declined)]
    public void AcceptedAndDeclined_AreTerminal_NoTransitionOutOfEitherIsPermitted(QuotationStatus terminal)
    {
        // ADR-0152: no revision of an accepted (or declined) quotation in
        // this release — a change is a new quotation.
        foreach (var to in QuotationStatusTransitions.AllStatuses)
            Assert.False(QuotationStatusTransitions.IsPermitted(terminal, to));
    }

    [Fact]
    public void EveryStatus_PermitsNoTransitionToItself()
    {
        // A same-to-same request is not special-cased: permitted only
        // where the table itself lists it (nowhere, here).
        foreach (var status in QuotationStatusTransitions.AllStatuses)
            Assert.False(QuotationStatusTransitions.IsPermitted(status, status));
    }

    [Fact]
    public void AllStatuses_IsExactlyTheFourDeclaredValues()
    {
        Assert.Equal(4, QuotationStatusTransitions.AllStatuses.Count);
        Assert.Equal(
            QuotationStatusTransitions.AllStatuses.Count,
            QuotationStatusTransitions.AllStatuses.Distinct().Count());
    }
}
