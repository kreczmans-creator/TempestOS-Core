using Tempest.Core.Invoicing;

namespace Tempest.Core.Tests.Invoicing;

/// <summary>
/// Exhaustively pins <c>InvoiceRequestStatusTransitions</c>' own permitted
/// table (`WP 19.1A` acceptance #3) — every one of the eighty-one
/// (from, to) pairs across the nine-value vocabulary, mirroring
/// <c>Evidence.EvidenceStatusTransitionsTests</c>' own exhaustive-table
/// discipline. <c>InvoiceRequestStatusTransitions</c> is <c>internal</c>;
/// this project sees it directly via <c>InternalsVisibleTo</c>.
/// </summary>
public sealed class InvoiceRequestStatusTransitionsTests
{
    public static TheoryData<InvoiceRequestStatus, InvoiceRequestStatus, bool> EveryPair()
    {
        var permitted = new HashSet<(InvoiceRequestStatus, InvoiceRequestStatus)>
        {
            // Draft: SendAsync starts a send; VoidAsync ends a request that
            // never reached the provider.
            (InvoiceRequestStatus.Draft, InvoiceRequestStatus.Sending),
            (InvoiceRequestStatus.Draft, InvoiceRequestStatus.Voided),

            // Sending: SendAsync's own five connector outcomes, verbatim —
            // Unavailable moves here to Draft, never to InvoiceRequestStatus.Unavailable itself.
            (InvoiceRequestStatus.Sending, InvoiceRequestStatus.Sent),
            (InvoiceRequestStatus.Sending, InvoiceRequestStatus.Rejected),
            (InvoiceRequestStatus.Sending, InvoiceRequestStatus.Reauthorise),
            (InvoiceRequestStatus.Sending, InvoiceRequestStatus.Draft),
            (InvoiceRequestStatus.Sending, InvoiceRequestStatus.Unknown),

            // Sent/Accepted: ReconcileAsync reading the connector's own
            // status.
            (InvoiceRequestStatus.Sent, InvoiceRequestStatus.Accepted),
            (InvoiceRequestStatus.Sent, InvoiceRequestStatus.Voided),
            (InvoiceRequestStatus.Accepted, InvoiceRequestStatus.Voided),

            // Rejected: VoidAsync's other Draft-or-Rejected case.
            (InvoiceRequestStatus.Rejected, InvoiceRequestStatus.Voided),

            // Unknown: ReconcileAsync resolving by reference.
            (InvoiceRequestStatus.Unknown, InvoiceRequestStatus.Sent),
            (InvoiceRequestStatus.Unknown, InvoiceRequestStatus.Draft),
        };

        var data = new TheoryData<InvoiceRequestStatus, InvoiceRequestStatus, bool>();

        foreach (var from in InvoiceRequestStatusTransitions.AllStatuses)
        {
            foreach (var to in InvoiceRequestStatusTransitions.AllStatuses)
                data.Add(from, to, permitted.Contains((from, to)));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryPair))]
    public void IsPermitted_MatchesTheAuditedTable_ForEveryPair(InvoiceRequestStatus from, InvoiceRequestStatus to, bool expected)
    {
        Assert.Equal(expected, InvoiceRequestStatusTransitions.IsPermitted(from, to));
    }

    [Theory]
    [InlineData(InvoiceRequestStatus.Voided)]
    [InlineData(InvoiceRequestStatus.Reauthorise)]
    [InlineData(InvoiceRequestStatus.Unavailable)]
    public void ThreeStatuses_AreTerminal_NoTransitionOutOfAnyOfThemIsPermitted(InvoiceRequestStatus terminal)
    {
        foreach (var to in InvoiceRequestStatusTransitions.AllStatuses)
            Assert.False(InvoiceRequestStatusTransitions.IsPermitted(terminal, to));
    }

    [Fact]
    public void Unavailable_IsAlsoUnreachable_NoTransitionIntoItIsPermitted()
    {
        // Declared for the vocabulary the WP 19.1A row names, never
        // actually stored — InvoiceRequestStatus.Unavailable's own remarks.
        foreach (var from in InvoiceRequestStatusTransitions.AllStatuses)
            Assert.False(InvoiceRequestStatusTransitions.IsPermitted(from, InvoiceRequestStatus.Unavailable));
    }

    [Fact]
    public void EveryStatus_PermitsNoTransitionToItself()
    {
        // A same-to-same request is not special-cased: permitted only
        // where the table itself lists it (nowhere, here).
        foreach (var status in InvoiceRequestStatusTransitions.AllStatuses)
            Assert.False(InvoiceRequestStatusTransitions.IsPermitted(status, status));
    }

    [Fact]
    public void AllStatuses_IsExactlyTheNineDeclaredValues()
    {
        Assert.Equal(9, InvoiceRequestStatusTransitions.AllStatuses.Count);
        Assert.Equal(
            InvoiceRequestStatusTransitions.AllStatuses.Count,
            InvoiceRequestStatusTransitions.AllStatuses.Distinct().Count());
    }
}
