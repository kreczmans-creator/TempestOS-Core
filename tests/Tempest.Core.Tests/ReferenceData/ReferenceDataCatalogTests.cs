using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Tests.ReferenceData;

// The shared Group A machinery, tested against a deliberately trivial fake
// domain so a failure here means the shared layer is wrong rather than any
// one library. Every real library's own tests then cover only what is
// genuinely theirs.
public class ReferenceDataCatalogTests
{
    // ----------------------------------------------------------------
    // Registration and identity
    // ----------------------------------------------------------------

    [Fact]
    public async Task RegisterAsync_ReturnsARecordCarryingDefinitionProvenanceAndDraftState()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();

        var record = await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget("W-1", "red"), ReferenceDataFixtures.Sourced());

        Assert.Equal("w-1", record.Id);
        Assert.Equal("W-1", record.Definition.Designation);
        Assert.Equal("red", record.Definition.Colour);
        Assert.Equal("TestFixture Publications", record.Provenance.SourceOrganisation);
        Assert.Equal(ReferenceValidationState.Draft, record.ValidationState);
        Assert.Null(record.SupersededByRecordId);
        Assert.Equal(1, record.RevisionNumber);
    }

    [Fact]
    public async Task RegisterAsync_BacksTheRecordWithADocumentOfTheLibrarysOwnKind()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog(out var documentStore, out _);

        var record = await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget(), ReferenceDataFixtures.Sourced());
        var document = await documentStore.FindAsync(record.UnderlyingDocumentId);

        Assert.Equal("WidgetReference", document!.Kind);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateId_Throws()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();
        await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget("W-1"), ReferenceDataFixtures.Sourced());

        var exception = await Assert.ThrowsAsync<DuplicateReferenceRecordException>(
            () => catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget("W-2"), ReferenceDataFixtures.Sourced()));

        Assert.Equal("w-1", exception.RecordId);
        Assert.Equal("Widgets", exception.Library);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateSecondaryKey_Throws()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();
        await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget("W-1"), ReferenceDataFixtures.Sourced());

        var exception = await Assert.ThrowsAsync<DuplicateReferenceKeyException>(
            () => catalog.RegisterAsync("w-2", ReferenceDataFixtures.Widget(" w-1 "), ReferenceDataFixtures.Sourced()));

        Assert.Equal("w-1", exception.ExistingRecordId);
    }

    [Fact]
    public async Task RegisterAsync_ALibraryWithNoSecondaryKey_NeverReportsADuplicateKey()
    {
        var persistenceStore = new InMemoryPersistenceStore();
        var documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        var catalog = new KeylessWidgetCatalog(documentStore, persistenceStore);

        await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget("W-1"), ReferenceDataFixtures.Sourced());
        var second = await catalog.RegisterAsync("w-2", ReferenceDataFixtures.Widget("W-1"), ReferenceDataFixtures.Sourced());

        Assert.Equal("w-2", second.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RegisterAsync_BlankId_Throws(string recordId)
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();

        await Assert.ThrowsAsync<ArgumentException>(
            () => catalog.RegisterAsync(recordId, ReferenceDataFixtures.Widget(), ReferenceDataFixtures.Sourced()));
    }

    [Fact]
    public async Task RegisterAsync_NullDefinitionOrProvenance_Throws()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();

        await Assert.ThrowsAsync<ArgumentNullException>(() => catalog.RegisterAsync("w-1", null!, ReferenceDataFixtures.Sourced()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget(), null!));
    }

    [Fact]
    public async Task RegisterAsync_ConcurrentRegistrationsOfTheSameId_OnlyOneSucceeds()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(async () =>
        {
            try
            {
                await catalog.RegisterAsync("w-race", ReferenceDataFixtures.Widget($"W-{i}"), ReferenceDataFixtures.Sourced());
                return true;
            }
            catch (ReferenceDataException)
            {
                return false;
            }
        })));

        Assert.Equal(1, results.Count(succeeded => succeeded));
    }

    [Fact]
    public async Task RegisterAsync_ConcurrentRegistrationsSharingASecondaryKey_OnlyOneSucceeds()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(async () =>
        {
            try
            {
                await catalog.RegisterAsync($"w-{i}", ReferenceDataFixtures.Widget("SHARED"), ReferenceDataFixtures.Sourced());
                return true;
            }
            catch (ReferenceDataException)
            {
                return false;
            }
        })));

        Assert.Equal(1, results.Count(succeeded => succeeded));
    }

    // ----------------------------------------------------------------
    // Reading
    // ----------------------------------------------------------------

    [Fact]
    public async Task FindAsync_UnknownId_ReturnsNullRatherThanThrowing()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();

        Assert.Null(await catalog.FindAsync("w-missing"));
    }

    [Fact]
    public async Task FindBySecondaryKey_ResolvesTheRecord()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();
        await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget("W-1"), ReferenceDataFixtures.Sourced());

        Assert.Equal("w-1", (await catalog.FindByDesignationAsync(" w-1 "))!.Id);
        Assert.Null(await catalog.FindByDesignationAsync("W-NOPE"));
    }

    [Fact]
    public async Task ListAsync_ReturnsEveryRecordInDeterministicOrder()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();
        await catalog.RegisterAsync("w-3", ReferenceDataFixtures.Widget("W-3"), ReferenceDataFixtures.Sourced());
        await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget("W-1"), ReferenceDataFixtures.Sourced());
        await catalog.RegisterAsync("w-2", ReferenceDataFixtures.Widget("W-2"), ReferenceDataFixtures.Sourced());

        Assert.Equal(["w-1", "w-2", "w-3"], (await catalog.ListAsync()).Select(r => r.Id));
    }

    [Fact]
    public async Task FilterAsync_IsADeterministicSubsetOfListAsync()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();
        await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget("W-1", "red"), ReferenceDataFixtures.Sourced());
        await catalog.RegisterAsync("w-2", ReferenceDataFixtures.Widget("W-2", "blue"), ReferenceDataFixtures.Sourced());
        await catalog.RegisterAsync("w-3", ReferenceDataFixtures.Widget("W-3", "red"), ReferenceDataFixtures.Sourced());

        Assert.Equal(["w-1", "w-3"], (await catalog.WithColourAsync("RED")).Select(r => r.Id));
    }

    // ----------------------------------------------------------------
    // Revision
    // ----------------------------------------------------------------

    [Fact]
    public async Task ReviseAsync_AdvancesTheRevisionAndReplacesDefinitionAndProvenance()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();
        await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget("W-1", "red"), ReferenceDataFixtures.Sourced());

        var revised = await catalog.ReviseAsync(
            "w-1",
            ReferenceDataFixtures.Widget("W-1", "green"),
            ReferenceDataFixtures.Verified(),
            "Colour corrected against source.");

        Assert.Equal(2, revised.RevisionNumber);
        Assert.Equal("green", revised.Definition.Colour);
        Assert.True(revised.Provenance.IsVerified);
    }

    [Fact]
    public async Task ReviseAsync_ChangingTheSecondaryKey_MovesTheIndexEntry()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();
        await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget("W-1"), ReferenceDataFixtures.Sourced());

        await catalog.ReviseAsync("w-1", ReferenceDataFixtures.Widget("W-1B"), ReferenceDataFixtures.Sourced(), "Renamed.");

        Assert.Null(await catalog.FindByDesignationAsync("W-1"));
        Assert.Equal("w-1", (await catalog.FindByDesignationAsync("W-1B"))!.Id);
    }

    [Fact]
    public async Task ReviseAsync_OntoAnotherRecordsSecondaryKey_Throws()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();
        await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget("W-1"), ReferenceDataFixtures.Sourced());
        await catalog.RegisterAsync("w-2", ReferenceDataFixtures.Widget("W-2"), ReferenceDataFixtures.Sourced());

        await Assert.ThrowsAsync<DuplicateReferenceKeyException>(
            () => catalog.ReviseAsync("w-2", ReferenceDataFixtures.Widget("W-1"), ReferenceDataFixtures.Sourced(), "Collide."));
    }

    [Fact]
    public async Task ReviseAsync_UnknownRecord_Throws()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();

        await Assert.ThrowsAsync<ReferenceRecordNotFoundException>(
            () => catalog.ReviseAsync("w-missing", ReferenceDataFixtures.Widget(), ReferenceDataFixtures.Sourced(), null));
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsEveryRevisionOldestFirstWithItsOwnReason()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();
        await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget("W-1", "red"), ReferenceDataFixtures.Sourced());
        await catalog.ReviseAsync("w-1", ReferenceDataFixtures.Widget("W-1", "green"), ReferenceDataFixtures.Sourced(), "Colour corrected.");
        await catalog.SetValidationStateAsync("w-1", ReferenceValidationState.Checked, "Checked.");

        var history = await catalog.GetHistoryAsync("w-1");

        Assert.Equal([1, 2, 3], history.Select(r => r.RevisionNumber));
        Assert.Equal("Colour corrected.", history[1].ChangeSummary);
        Assert.False(string.IsNullOrWhiteSpace(history[0].AuthorPrincipalId));
    }

    [Fact]
    public async Task GetRevisionAsync_ReadsARecordBackExactlyAsAPastRevisionHeldIt()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();
        await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget("W-1", "red"), ReferenceDataFixtures.Sourced());
        await catalog.ReviseAsync("w-1", ReferenceDataFixtures.Widget("W-1", "green"), ReferenceDataFixtures.Sourced(), "Colour corrected.");

        Assert.Equal("red", (await catalog.GetRevisionAsync("w-1", 1)).Definition.Colour);
        Assert.Equal("green", (await catalog.FindAsync("w-1"))!.Definition.Colour);
    }

    [Fact]
    public async Task GetRevisionAsync_UnknownRevision_Throws()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();
        await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget(), ReferenceDataFixtures.Sourced());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => catalog.GetRevisionAsync("w-1", 9));
    }

    // ----------------------------------------------------------------
    // Lifecycle and provenance gates
    // ----------------------------------------------------------------

    [Fact]
    public async Task SetValidationStateAsync_LeavingDraftWithoutASource_Throws()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();
        await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget(), ReferenceProvenance.Unknown);

        var exception = await Assert.ThrowsAsync<ReferenceProvenanceIncompleteException>(
            () => catalog.SetValidationStateAsync("w-1", ReferenceValidationState.Checked, null));

        Assert.Equal(ReferenceValidationState.Checked, exception.RequestedState);
    }

    [Fact]
    public async Task SetValidationStateAsync_ReleasingUnverifiedData_Throws()
    {
        // Being imported is not being verified — the rule this gate exists
        // for, shared by every Group A library.
        var catalog = ReferenceDataFixtures.BuildCatalog();
        await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget(), ReferenceDataFixtures.Sourced());
        await catalog.SetValidationStateAsync("w-1", ReferenceValidationState.Checked, null);
        await catalog.SetValidationStateAsync("w-1", ReferenceValidationState.Validated, null);

        await Assert.ThrowsAsync<ReferenceProvenanceIncompleteException>(
            () => catalog.SetValidationStateAsync("w-1", ReferenceValidationState.Released, null));
    }

    [Fact]
    public async Task SetValidationStateAsync_ReleasingVerifiedData_Succeeds()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();
        await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget(), ReferenceDataFixtures.Verified());

        var released = await ReferenceDataFixtures.ReleaseAsync(catalog, "w-1");

        Assert.Equal(ReferenceValidationState.Released, released.ValidationState);
    }

    [Fact]
    public async Task SetValidationStateAsync_SkippingAState_Throws()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();
        await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget(), ReferenceDataFixtures.Verified());

        var exception = await Assert.ThrowsAsync<InvalidReferenceStateTransitionException>(
            () => catalog.SetValidationStateAsync("w-1", ReferenceValidationState.Released, null));

        Assert.Equal(ReferenceValidationState.Draft, exception.From);
        Assert.Equal(ReferenceValidationState.Released, exception.To);
    }

    [Fact]
    public async Task SetValidationStateAsync_RequestingSuperseded_Throws()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();
        await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget(), ReferenceDataFixtures.Verified());

        await Assert.ThrowsAsync<ArgumentException>(
            () => catalog.SetValidationStateAsync("w-1", ReferenceValidationState.Superseded, null));
    }

    // ----------------------------------------------------------------
    // Released immutability and supersession
    // ----------------------------------------------------------------

    [Fact]
    public async Task ReviseAsync_AReleasedRecord_Throws()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();
        await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget("W-1"), ReferenceDataFixtures.Verified());
        await ReferenceDataFixtures.ReleaseAsync(catalog, "w-1");

        var exception = await Assert.ThrowsAsync<ReleasedReferenceImmutableException>(
            () => catalog.ReviseAsync("w-1", ReferenceDataFixtures.Widget("W-1", "green"), ReferenceDataFixtures.Verified(), null));

        Assert.Equal(ReferenceValidationState.Released, exception.State);
    }

    [Fact]
    public async Task SupersedeAsync_MarksTheRecordAndLinksTheReplacementWithTheSupersedesKind()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog(out var documentStore, out _);
        var original = await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget("W-1"), ReferenceDataFixtures.Verified());
        var replacement = await catalog.RegisterAsync("w-2", ReferenceDataFixtures.Widget("W-2"), ReferenceDataFixtures.Verified());
        await ReferenceDataFixtures.ReleaseAsync(catalog, "w-1");

        var superseded = await catalog.SupersedeAsync("w-1", "w-2", "Replaced.");
        var references = await documentStore.GetReferencesAsync(replacement.UnderlyingDocumentId);

        Assert.Equal(ReferenceValidationState.Superseded, superseded.ValidationState);
        Assert.Equal("w-2", superseded.SupersededByRecordId);
        Assert.Equal(GovernanceRelationshipKinds.Supersedes, Assert.Single(references).RelationshipKind);
        Assert.Equal(original.UnderlyingDocumentId, references[0].TargetDocumentId);
    }

    [Fact]
    public async Task SupersedeAsync_LeavesTheSupersededValuesReadable()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();
        await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget("W-1", "red"), ReferenceDataFixtures.Verified());
        await catalog.RegisterAsync("w-2", ReferenceDataFixtures.Widget("W-2", "green"), ReferenceDataFixtures.Verified());
        await ReferenceDataFixtures.ReleaseAsync(catalog, "w-1");
        await catalog.SupersedeAsync("w-1", "w-2", null);

        Assert.Equal("red", (await catalog.FindAsync("w-1"))!.Definition.Colour);
    }

    [Fact]
    public async Task SupersedeAsync_ADraftRecordOrItself_Throws()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();
        await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget("W-1"), ReferenceDataFixtures.Verified());
        await catalog.RegisterAsync("w-2", ReferenceDataFixtures.Widget("W-2"), ReferenceDataFixtures.Verified());

        await Assert.ThrowsAsync<InvalidReferenceStateTransitionException>(() => catalog.SupersedeAsync("w-1", "w-2", null));
        await Assert.ThrowsAsync<ArgumentException>(() => catalog.SupersedeAsync("w-1", "w-1", null));
    }

    [Fact]
    public async Task SupersedeAsync_UnknownReplacement_Throws()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog();
        await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget("W-1"), ReferenceDataFixtures.Verified());
        await ReferenceDataFixtures.ReleaseAsync(catalog, "w-1");

        await Assert.ThrowsAsync<ReferenceRecordNotFoundException>(() => catalog.SupersedeAsync("w-1", "w-missing", null));
    }

    // ----------------------------------------------------------------
    // Hostile data
    // ----------------------------------------------------------------

    [Fact]
    public async Task FindAsync_CorruptedIndexEntry_ThrowsAControlledExceptionNamingTheEntry()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog(out _, out var persistenceStore);
        await persistenceStore.WriteAsync("Widgets.Index", "w-1", "not-a-guid");

        var exception = await Assert.ThrowsAsync<ReferenceDataException>(() => catalog.FindAsync("w-1"));

        Assert.Contains("w-1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("not-a-guid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindAsync_IndexPointingAtAMissingOrForeignDocument_ReadsAsNoSuchRecord()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog(out var documentStore, out var persistenceStore);
        await persistenceStore.WriteAsync("Widgets.Index", "w-gone", Guid.NewGuid().ToString("N"));
        var foreign = await documentStore.CreateAsync("MaterialSpecification", "{}");
        await persistenceStore.WriteAsync("Widgets.Index", "w-foreign", foreign.Id.ToString("N"));

        Assert.Null(await catalog.FindAsync("w-gone"));
        Assert.Null(await catalog.FindAsync("w-foreign"));
    }

    [Fact]
    public async Task ListAsync_SkipsAStaleIndexEntryRatherThanAbortingTheWholeListing()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog(out _, out var persistenceStore);
        await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget("W-1"), ReferenceDataFixtures.Sourced());
        await persistenceStore.WriteAsync("Widgets.Index", "w-stale", Guid.NewGuid().ToString("N"));

        Assert.Equal(["w-1"], (await catalog.ListAsync()).Select(r => r.Id));
    }

    [Fact]
    public async Task FindAsync_UnreadableContent_ThrowsAControlledException()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog(out var documentStore, out _);
        var record = await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget(), ReferenceDataFixtures.Sourced());
        await documentStore.ReviseAsync(record.UnderlyingDocumentId, "{ not json", "Corrupted.");

        var exception = await Assert.ThrowsAsync<ReferenceDataException>(() => catalog.FindAsync("w-1"));

        Assert.IsType<System.Text.Json.JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task FindAsync_ContentMissingItsProvenance_ThrowsAControlledException()
    {
        var catalog = ReferenceDataFixtures.BuildCatalog(out var documentStore, out _);
        var record = await catalog.RegisterAsync("w-1", ReferenceDataFixtures.Widget(), ReferenceDataFixtures.Sourced());
        await documentStore.ReviseAsync(
            record.UnderlyingDocumentId,
            "{\"RecordId\":\"w-1\",\"Definition\":{\"Designation\":\"W-1\"},\"ValidationState\":\"Draft\"}",
            "Provenance removed.");

        var exception = await Assert.ThrowsAsync<ReferenceDataException>(() => catalog.FindAsync("w-1"));

        Assert.Contains("provenance", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_NullStores_Throw()
    {
        var persistenceStore = new InMemoryPersistenceStore();
        var documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());

        Assert.Throws<ArgumentNullException>(() => new KeylessWidgetCatalog(null!, persistenceStore));
        Assert.Throws<ArgumentNullException>(() => new KeylessWidgetCatalog(documentStore, null!));
    }

    // ----------------------------------------------------------------
    // Two libraries share one document store without colliding
    // ----------------------------------------------------------------

    [Fact]
    public async Task TwoLibraries_ShareTheDocumentStoreWithoutSeeingEachOthersRecords()
    {
        var persistenceStore = new InMemoryPersistenceStore();
        var documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        var widgets = new WidgetCatalog(documentStore, persistenceStore);
        var keyless = new KeylessWidgetCatalog(documentStore, persistenceStore);

        await widgets.RegisterAsync("w-1", ReferenceDataFixtures.Widget("W-1"), ReferenceDataFixtures.Sourced());
        await keyless.RegisterAsync("k-1", ReferenceDataFixtures.Widget("K-1"), ReferenceDataFixtures.Sourced());

        Assert.Single(await widgets.ListAsync());
        Assert.Single(await keyless.ListAsync());
        Assert.Null(await widgets.FindAsync("k-1"));
    }
}
