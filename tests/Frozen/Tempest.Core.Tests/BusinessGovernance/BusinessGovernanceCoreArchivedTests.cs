using Tempest.Core.BusinessGovernance;

namespace Tempest.Core.Tests.BusinessGovernance;

// WP 18.0C (D-028): split out of the live
// tests/Tempest.Core.Tests/BusinessGovernance/BusinessGovernanceCoreTests.cs
// the same day DeterminationState.cs was the one BusinessGovernance-root
// file nothing kept needed - Money, EffectivePeriod, ConfidentialityClassification,
// BusinessAuthorisation, BusinessOwnership, BusinessEvidence and
// ReviewSchedule all stayed live and their tests with them.
public class BusinessGovernanceCoreArchivedTests
{
    [Fact]
    public void OnlyRecorded_CountsAsEstablished()
    {
        // An assumption is not a fact, and an open question is not an
        // answer. Exactly one state means "you may rely on this".
        Assert.True(DeterminationStates.IsEstablished(DeterminationState.Recorded));

        foreach (var state in DeterminationStates.All.Where(s => s != DeterminationState.Recorded))
            Assert.False(DeterminationStates.IsEstablished(state));
    }

    [Fact]
    public void ASetOfDeterminations_IsOnlyAsStrongAsItsWeakest()
    {
        Assert.Equal(
            DeterminationState.ReviewRequired,
            DeterminationStates.Weakest([DeterminationState.Recorded, DeterminationState.ReviewRequired, DeterminationState.Recorded]));
    }

    [Fact]
    public void AnEmptySetOfDeterminations_IsNotDetermined_NotEstablished()
    {
        Assert.Equal(DeterminationState.NotDetermined, DeterminationStates.Weakest([]));
    }
}
