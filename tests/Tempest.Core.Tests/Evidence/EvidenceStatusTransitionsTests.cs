using Tempest.Core.Evidence;

namespace Tempest.Core.Tests.Evidence;

/// <summary>
/// Exhaustively pins <c>EvidenceStatusTransitions</c>' own permitted table
/// (`WP 18.0A` acceptance #2) — every one of the sixteen (from, to) pairs,
/// mirroring <c>Requirements.RequirementStatusTransitionsTests</c>' own
/// exhaustive-table discipline. <c>EvidenceStatusTransitions</c> is
/// <c>internal</c>; this project sees it directly via
/// <c>InternalsVisibleTo</c>.
/// </summary>
public sealed class EvidenceStatusTransitionsTests
{
    public static TheoryData<EvidenceStatus, EvidenceStatus, bool> EveryPair()
    {
        var permitted = new HashSet<(EvidenceStatus, EvidenceStatus)>
        {
            (EvidenceStatus.Draft, EvidenceStatus.Checked),
            (EvidenceStatus.Checked, EvidenceStatus.Issued),
            (EvidenceStatus.Issued, EvidenceStatus.Draft),
            (EvidenceStatus.Issued, EvidenceStatus.Superseded),
        };

        var data = new TheoryData<EvidenceStatus, EvidenceStatus, bool>();

        foreach (var from in EvidenceStatusTransitions.AllStatuses)
        {
            foreach (var to in EvidenceStatusTransitions.AllStatuses)
                data.Add(from, to, permitted.Contains((from, to)));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryPair))]
    public void IsPermitted_MatchesTheAuditedTable_ForEveryPair(EvidenceStatus from, EvidenceStatus to, bool expected)
    {
        Assert.Equal(expected, EvidenceStatusTransitions.IsPermitted(from, to));
    }

    [Fact]
    public void Superseded_IsTerminal_NoTransitionOutOfItIsPermitted()
    {
        foreach (var to in EvidenceStatusTransitions.AllStatuses)
            Assert.False(EvidenceStatusTransitions.IsPermitted(EvidenceStatus.Superseded, to));
    }

    [Fact]
    public void EveryStatus_PermitsNoTransitionToItself()
    {
        // A same-to-same request is not special-cased, mirroring
        // RequirementStatusTransitions' own identical discipline: permitted
        // only where the table itself lists it (nowhere, for Evidence).
        foreach (var status in EvidenceStatusTransitions.AllStatuses)
            Assert.False(EvidenceStatusTransitions.IsPermitted(status, status));
    }
}
