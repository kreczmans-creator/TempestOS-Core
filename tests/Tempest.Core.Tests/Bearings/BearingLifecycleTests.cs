using Tempest.Core.Bearings;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Tests.Bearings;

// Lifecycle, provenance-gating, released-record immutability, revision
// history and supersession — the governance that separates released
// engineering reference data from an unverified import.
public class BearingLifecycleTests
{
    // ----------------------------------------------------------------
    // Validation state transitions
    // ----------------------------------------------------------------

    [Fact]
    public async Task SetValidationStateAsync_DraftToChecked_Succeeds()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall());

        var checkedRecord = await catalog.SetValidationStateAsync("brg-0001", BearingValidationState.Checked, "Checked against fixture source.");

        Assert.Equal(BearingValidationState.Checked, checkedRecord.ValidationState);
    }

    [Fact]
    public async Task SetValidationStateAsync_SkippingAState_Throws()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall());

        var exception = await Assert.ThrowsAsync<InvalidBearingValidationStateTransitionException>(
            () => catalog.SetValidationStateAsync("brg-0001", BearingValidationState.Released, null));

        Assert.Equal(BearingValidationState.Draft, exception.From);
        Assert.Equal(BearingValidationState.Released, exception.To);
    }

    [Fact]
    public async Task SetValidationStateAsync_DownTransition_IsPermittedSoADefectCanBeCorrected()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall());
        await catalog.SetValidationStateAsync("brg-0001", BearingValidationState.Checked, null);

        var back = await catalog.SetValidationStateAsync("brg-0001", BearingValidationState.Draft, "Defect found during validation.");

        Assert.Equal(BearingValidationState.Draft, back.ValidationState);
    }

    [Fact]
    public async Task SetValidationStateAsync_RequestingSuperseded_Throws()
    {
        // Supersession must record what replaced the record, which this
        // method structurally cannot.
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall());

        await Assert.ThrowsAsync<ArgumentException>(
            () => catalog.SetValidationStateAsync("brg-0001", BearingValidationState.Superseded, null));
    }

    [Fact]
    public async Task SetValidationStateAsync_UnknownBearing_Throws()
    {
        var catalog = BearingFixtures.BuildCatalog();

        await Assert.ThrowsAsync<BearingNotFoundException>(
            () => catalog.SetValidationStateAsync("brg-missing", BearingValidationState.Checked, null));
    }

    [Fact]
    public async Task SetValidationStateAsync_AdvancesTheRevisionSoTheChangeIsInTheHistory()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall());

        var checkedRecord = await catalog.SetValidationStateAsync("brg-0001", BearingValidationState.Checked, "Checked.");

        Assert.Equal(2, checkedRecord.RevisionNumber);
    }

    // ----------------------------------------------------------------
    // Provenance gates
    // ----------------------------------------------------------------

    [Fact]
    public async Task SetValidationStateAsync_LeavingDraftWithoutASource_Throws()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall(provenance: BearingProvenance.Unknown));

        var exception = await Assert.ThrowsAsync<BearingProvenanceIncompleteException>(
            () => catalog.SetValidationStateAsync("brg-0001", BearingValidationState.Checked, null));

        Assert.Equal(BearingValidationState.Checked, exception.RequestedState);
    }

    [Fact]
    public async Task SetValidationStateAsync_ReleasingUnverifiedData_Throws()
    {
        // Being imported is not being verified — the rule this gate exists
        // for.
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall(provenance: BearingFixtures.SourcedProvenance()));
        await catalog.SetValidationStateAsync("brg-0001", BearingValidationState.Checked, null);
        await catalog.SetValidationStateAsync("brg-0001", BearingValidationState.Validated, null);

        var exception = await Assert.ThrowsAsync<BearingProvenanceIncompleteException>(
            () => catalog.SetValidationStateAsync("brg-0001", BearingValidationState.Released, null));

        Assert.Equal(BearingValidationState.Released, exception.RequestedState);
    }

    [Fact]
    public async Task SetValidationStateAsync_ReleasingVerifiedData_Succeeds()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall(provenance: BearingFixtures.VerifiedProvenance()));

        var released = await BearingFixtures.ReleaseAsync(catalog, "brg-0001");

        Assert.Equal(BearingValidationState.Released, released.ValidationState);
        Assert.True(BearingValidationStates.IsReleased(released.ValidationState));
    }

    [Fact]
    public async Task SetValidationStateAsync_ReturningToDraft_NeedsNoProvenance()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall());
        await catalog.SetValidationStateAsync("brg-0001", BearingValidationState.Checked, null);

        var back = await catalog.SetValidationStateAsync("brg-0001", BearingValidationState.Draft, null);

        Assert.Equal(BearingValidationState.Draft, back.ValidationState);
    }

    // ----------------------------------------------------------------
    // Released-record immutability
    // ----------------------------------------------------------------

    [Fact]
    public async Task ReviseAsync_ReleasedRecord_Throws()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall(provenance: BearingFixtures.VerifiedProvenance()));
        await BearingFixtures.ReleaseAsync(catalog, "brg-0001");

        var exception = await Assert.ThrowsAsync<ReleasedBearingImmutableException>(
            () => catalog.ReviseAsync("brg-0001", BearingFixtures.DeepGrooveBall(widthMillimetres: 9.0), "Should be refused."));

        Assert.Equal(BearingValidationState.Released, exception.State);
    }

    [Fact]
    public async Task ReviseAsync_SupersededRecord_Throws()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall("FX-6000", provenance: BearingFixtures.VerifiedProvenance()));
        await catalog.RegisterAsync("brg-0002", BearingFixtures.DeepGrooveBall("FX-6000-B", provenance: BearingFixtures.VerifiedProvenance()));
        await BearingFixtures.ReleaseAsync(catalog, "brg-0001");
        await catalog.SupersedeAsync("brg-0001", "brg-0002", "Superseded by catalogue revision 2.");

        await Assert.ThrowsAsync<ReleasedBearingImmutableException>(
            () => catalog.ReviseAsync("brg-0001", BearingFixtures.DeepGrooveBall("FX-6000"), null));
    }

    [Fact]
    public async Task ReleasedRecord_RemainsReadableExactlyAsReleased()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall(provenance: BearingFixtures.VerifiedProvenance()));
        await BearingFixtures.ReleaseAsync(catalog, "brg-0001");

        var found = await catalog.FindAsync("brg-0001");

        Assert.Equal(BearingValidationState.Released, found!.ValidationState);
        Assert.Equal(4.6, found.Definition.LoadRatings!.BasicDynamicRadial!.Value.Value);
    }

    // ----------------------------------------------------------------
    // Supersession
    // ----------------------------------------------------------------

    [Fact]
    public async Task SupersedeAsync_MarksTheRecordSupersededAndNamesItsReplacement()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall("FX-6000", provenance: BearingFixtures.VerifiedProvenance()));
        await catalog.RegisterAsync("brg-0002", BearingFixtures.DeepGrooveBall("FX-6000-B", provenance: BearingFixtures.VerifiedProvenance()));
        await BearingFixtures.ReleaseAsync(catalog, "brg-0001");

        var superseded = await catalog.SupersedeAsync("brg-0001", "brg-0002", "Catalogue revision 2.");

        Assert.Equal(BearingValidationState.Superseded, superseded.ValidationState);
        Assert.Equal("brg-0002", superseded.SupersededByBearingId);
    }

    [Fact]
    public async Task SupersedeAsync_RecordsTheSupersedesRelationshipTheRestOfThePlatformAlreadyUses()
    {
        // The replacement links to what it supersedes, in the same
        // direction and under the same relationship kind as
        // Decision.SupersedesAsync — A4 introduces no second vocabulary
        // value for one concept (`ADR-0073`).
        var catalog = BearingFixtures.BuildCatalog(out var documentStore);
        var original = await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall("FX-6000", provenance: BearingFixtures.VerifiedProvenance()));
        var replacement = await catalog.RegisterAsync("brg-0002", BearingFixtures.DeepGrooveBall("FX-6000-B", provenance: BearingFixtures.VerifiedProvenance()));
        await BearingFixtures.ReleaseAsync(catalog, "brg-0001");

        await catalog.SupersedeAsync("brg-0001", "brg-0002", null);
        var references = await documentStore.GetReferencesAsync(replacement.UnderlyingDocumentId);

        var supersession = Assert.Single(references);
        Assert.Equal(GovernanceRelationshipKinds.Supersedes, supersession.RelationshipKind);
        Assert.Equal(original.UnderlyingDocumentId, supersession.TargetDocumentId);
    }

    [Fact]
    public async Task SupersedeAsync_ADraftRecord_Throws()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall("FX-6000"));
        await catalog.RegisterAsync("brg-0002", BearingFixtures.DeepGrooveBall("FX-6000-B"));

        await Assert.ThrowsAsync<InvalidBearingValidationStateTransitionException>(
            () => catalog.SupersedeAsync("brg-0001", "brg-0002", null));
    }

    [Fact]
    public async Task SupersedeAsync_UnknownReplacement_Throws()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall(provenance: BearingFixtures.VerifiedProvenance()));
        await BearingFixtures.ReleaseAsync(catalog, "brg-0001");

        var exception = await Assert.ThrowsAsync<BearingNotFoundException>(
            () => catalog.SupersedeAsync("brg-0001", "brg-missing", null));

        Assert.Equal("brg-missing", exception.BearingId);
    }

    [Fact]
    public async Task SupersedeAsync_ByItself_Throws()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall(provenance: BearingFixtures.VerifiedProvenance()));
        await BearingFixtures.ReleaseAsync(catalog, "brg-0001");

        await Assert.ThrowsAsync<ArgumentException>(() => catalog.SupersedeAsync("brg-0001", "brg-0001", null));
    }

    [Fact]
    public async Task SupersedeAsync_LeavesTheSupersededValuesReadable()
    {
        // The whole point of supersession over deletion: what a past
        // calculation consumed must remain readable.
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall("FX-6000", provenance: BearingFixtures.VerifiedProvenance()));
        await catalog.RegisterAsync("brg-0002", BearingFixtures.DeepGrooveBall("FX-6000-B", provenance: BearingFixtures.VerifiedProvenance()));
        await BearingFixtures.ReleaseAsync(catalog, "brg-0001");
        await catalog.SupersedeAsync("brg-0001", "brg-0002", null);

        var superseded = await catalog.FindAsync("brg-0001");

        Assert.NotNull(superseded);
        Assert.Equal(4.6, superseded!.Definition.LoadRatings!.BasicDynamicRadial!.Value.Value);
    }

    // ----------------------------------------------------------------
    // Revision history
    // ----------------------------------------------------------------

    [Fact]
    public async Task GetHistoryAsync_ReturnsEveryRevisionOldestFirst()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall());
        await catalog.ReviseAsync("brg-0001", BearingFixtures.DeepGrooveBall(widthMillimetres: 9.0), "Width corrected.");
        await catalog.SetValidationStateAsync("brg-0001", BearingValidationState.Checked, "Checked.");

        var history = await catalog.GetHistoryAsync("brg-0001");

        Assert.Equal(3, history.Count);
        Assert.Equal([1, 2, 3], history.Select(r => r.RevisionNumber));
    }

    [Fact]
    public async Task GetHistoryAsync_KeepsTheChangeSummaryExplainingWhyAValueChanged()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall());
        await catalog.ReviseAsync("brg-0001", BearingFixtures.DeepGrooveBall(widthMillimetres: 9.0), "C updated following fixture catalogue revision 2.");

        var history = await catalog.GetHistoryAsync("brg-0001");

        Assert.Equal("C updated following fixture catalogue revision 2.", history[^1].ChangeSummary);
    }

    [Fact]
    public async Task GetHistoryAsync_RecordsWhoAuthoredEachRevision()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall());

        var history = await catalog.GetHistoryAsync("brg-0001");

        Assert.False(string.IsNullOrWhiteSpace(history[0].AuthorPrincipalId));
    }

    [Fact]
    public async Task GetHistoryAsync_UnknownBearing_Throws()
    {
        var catalog = BearingFixtures.BuildCatalog();

        await Assert.ThrowsAsync<BearingNotFoundException>(() => catalog.GetHistoryAsync("brg-missing"));
    }

    [Fact]
    public async Task GetRevisionAsync_ReadsAnEngineeringValueBackExactlyAsAPastRevisionHeldIt()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall(widthMillimetres: 8.0));
        await catalog.ReviseAsync("brg-0001", BearingFixtures.DeepGrooveBall(widthMillimetres: 9.0), "Width corrected.");

        var asRegistered = await catalog.GetRevisionAsync("brg-0001", 1);
        var current = await catalog.FindAsync("brg-0001");

        Assert.Equal(BearingFixtures.Millimetres(8.0), asRegistered.Definition.Geometry.Width);
        Assert.Equal(BearingFixtures.Millimetres(9.0), current!.Definition.Geometry.Width);
    }

    [Fact]
    public async Task GetRevisionAsync_UnknownRevision_Throws()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => catalog.GetRevisionAsync("brg-0001", 7));
    }

    [Fact]
    public async Task GetRevisionAsync_UnknownBearing_Throws()
    {
        var catalog = BearingFixtures.BuildCatalog();

        await Assert.ThrowsAsync<BearingNotFoundException>(() => catalog.GetRevisionAsync("brg-missing", 1));
    }

    [Fact]
    public async Task RevisionHistory_IsNeverOverwritten()
    {
        var catalog = BearingFixtures.BuildCatalog(out var documentStore);
        var bearing = await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall(widthMillimetres: 8.0));
        await catalog.ReviseAsync("brg-0001", BearingFixtures.DeepGrooveBall(widthMillimetres: 9.0), "First correction.");
        await catalog.ReviseAsync("brg-0001", BearingFixtures.DeepGrooveBall(widthMillimetres: 10.0), "Second correction.");

        IReadOnlyList<IDocumentRevision> history = await documentStore.GetRevisionHistoryAsync(bearing.UnderlyingDocumentId);
        var widths = new List<double?>();
        for (var revision = 1; revision <= history.Count; revision++)
            widths.Add((await catalog.GetRevisionAsync("brg-0001", revision)).Definition.Geometry.Width?.Value);

        Assert.Equal(3, history.Count);
        Assert.Equal([8.0, 9.0, 10.0], widths);
    }
}
