using Tempest.Core.Bearings;
using Tempest.Core.ReferenceData;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Bearings;

// Integration tests: A4 against the existing Engineering Data Model,
// Units & Quantities, Materials, Identity and Validation systems it is
// built on, exercised end to end rather than in isolation.
public class BearingLibraryIntegrationTests
{
    [Fact]
    public async Task BearingRecords_ShareTheEngineeringDocumentStoreWithMaterialsWithoutColliding()
    {
        var persistenceStore = new InMemoryPersistenceStore();
        var documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        var materials = new MaterialCatalog(documentStore, persistenceStore);
        var bearings = new BearingCatalog(documentStore, persistenceStore);

        var material = await materials.RegisterAsync(
            "fixture-ring-steel",
            new MaterialDefinition
            {
                Name = "Fixture ring steel",
                Family = MaterialFamily.Steel,
                Properties = new Dictionary<string, ReferenceQuantityValue>
                {
                    ["ReferenceLength"] = new(new Quantity<Length>(1.0, LengthUnits.Metre), ReferenceValueOrigin.Unknown),
                },
            },
            BearingFixtures.SourcedProvenance());

        var bearing = await bearings.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall() with
        {
            Construction = new BearingConstruction(RingMaterialId: material.Id),
        });

        // Two different Kinds in one store, each still resolvable through
        // its own catalogue and neither visible to the other's enumeration.
        Assert.Equal(BearingCatalog.BearingDocumentKind, (await documentStore.FindAsync(bearing.UnderlyingDocumentId))!.Kind);
        Assert.Equal("MaterialSpecification", (await documentStore.FindAsync(material.UnderlyingDocumentId))!.Kind);
        Assert.Single(await bearings.ListAsync());
        Assert.Single(await materials.ListAsync());
    }

    [Fact]
    public async Task BearingRecords_CanBeLinkedToAnyOtherEngineeringDocument()
    {
        // ADR-0073: relationships between engineering objects are open
        // string DocumentReferences, platform-wide. A4 introduces no
        // traversal mechanism of its own.
        var persistenceStore = new InMemoryPersistenceStore();
        var documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        var bearings = new BearingCatalog(documentStore, persistenceStore);

        var bearing = await bearings.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall());
        var sourceDocument = await documentStore.CreateAsync("EngineeringReference", "Fixture catalogue index.");

        await documentStore.LinkAsync(bearing.UnderlyingDocumentId, sourceDocument.Id, "derivedFrom");
        var references = await documentStore.GetReferencesAsync(bearing.UnderlyingDocumentId);

        Assert.Equal("derivedFrom", Assert.Single(references).RelationshipKind);
    }

    [Fact]
    public async Task ARevisionRecordsWhoMadeItThroughTheIdentitySystem()
    {
        var persistenceStore = new InMemoryPersistenceStore();
        var principalAccessor = new CurrentPrincipalAccessor();
        var documentStore = new EngineeringDocumentStore(persistenceStore, principalAccessor);
        var bearings = new BearingCatalog(documentStore, persistenceStore);

        principalAccessor.SetCurrent(new PlatformPrincipal(new PlatformIdentity("reviewer-1", "Reviewer One"), []));

        await bearings.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall());
        var history = await bearings.GetHistoryAsync("brg-0001");

        // Authorship comes from the platform's own Identity system; A4
        // never records a principal of its own.
        Assert.Equal("reviewer-1", history[0].AuthorPrincipalId);
    }

    [Fact]
    public async Task AFullReferenceDataLifecycle_FromDraftToReleaseToSupersession_IsTraceableEndToEnd()
    {
        var catalog = BearingFixtures.BuildCatalog();
        var validator = new BearingValidationService(catalog);

        // 1. Recorded from a source, unverified.
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall("FX-6000"), BearingFixtures.SourcedProvenance());
        Assert.Equal(ReferenceValidationState.Draft, (await catalog.FindAsync("brg-0001"))!.ValidationState);

        // 2. Checked, then found to need a correction, then re-checked.
        await catalog.SetValidationStateAsync("brg-0001", ReferenceValidationState.Checked, "Checked against fixture source.");
        await catalog.SetValidationStateAsync("brg-0001", ReferenceValidationState.Draft, "Width transcription error found.");
        await catalog.ReviseAsync(
            "brg-0001",
            BearingFixtures.DeepGrooveBall("FX-6000", widthMillimetres: 9.0),
            BearingFixtures.VerifiedProvenance(),
            "Width corrected against source; verified by reviewer-1.");
        await catalog.SetValidationStateAsync("brg-0001", ReferenceValidationState.Checked, "Re-checked.");

        // 3. Validated against the data-quality rules, then released.
        Assert.True((await validator.ValidateAsync("brg-0001")).IsValid);
        await catalog.SetValidationStateAsync("brg-0001", ReferenceValidationState.Validated, "Rules pass.");
        var released = await catalog.SetValidationStateAsync("brg-0001", ReferenceValidationState.Released, "Released.");
        Assert.True(ReferenceValidationStates.IsReleased(released.ValidationState));

        // 4. Released data is immutable; a later catalogue revision is a
        //    new record superseding it, and both survive.
        await Assert.ThrowsAsync<ReleasedReferenceImmutableException>(
            () => catalog.ReviseAsync("brg-0001", BearingFixtures.DeepGrooveBall("FX-6000"), "Refused."));

        await catalog.RegisterAsync("brg-0002", BearingFixtures.DeepGrooveBall("FX-6000-B"), BearingFixtures.VerifiedProvenance());
        await catalog.SupersedeAsync("brg-0001", "brg-0002", "Superseded by fixture catalogue revision 2.");

        var superseded = await catalog.FindAsync("brg-0001");
        Assert.Equal(ReferenceValidationState.Superseded, superseded!.ValidationState);
        Assert.Equal("brg-0002", superseded.SupersededByRecordId);

        // 5. Every step is in the history, in order, with its own reason.
        var history = await catalog.GetHistoryAsync("brg-0001");
        Assert.Equal(8, history.Count);
        Assert.Contains(history, revision => revision.ChangeSummary == "Width transcription error found.");
        Assert.Contains(history, revision => revision.ChangeSummary == "Superseded by fixture catalogue revision 2.");

        // 6. The pre-correction value is still readable exactly as it was.
        Assert.Equal(BearingFixtures.Millimetres(8.0), (await catalog.GetRevisionAsync("brg-0001", 1)).Definition.Geometry.Width);
    }

    [Fact]
    public async Task ReleasedReferenceData_IsDistinguishableFromEveryOtherKindOfRecord()
    {
        // The distinction §16 of A4's own charter makes mandatory: a
        // released record must be separable from drafts, unverified
        // imports and incomplete records in one query.
        var catalog = BearingFixtures.BuildCatalog();

        await catalog.RegisterAsync("brg-draft", BearingFixtures.DeepGrooveBall("FX-6000"));
        await catalog.RegisterAsync(
            "brg-import",
            BearingFixtures.DeepGrooveBall("FX-6001"),
            BearingFixtures.SourcedProvenance() with { ExtractionMethod = ReferenceExtractionMethod.StructuredImport });
        await catalog.RegisterAsync(
            "brg-incomplete",
            BearingFixtures.DeepGrooveBall("FX-6002") with { LoadRatings = null },
            ReferenceProvenance.Unknown);
        await catalog.RegisterAsync("brg-released", BearingFixtures.DeepGrooveBall("FX-6003"), BearingFixtures.VerifiedProvenance());
        await BearingFixtures.ReleaseAsync(catalog, "brg-released");

        var released = await catalog.SearchAsync(new BearingQuery { ValidationStates = [ReferenceValidationState.Released] });
        var notReleased = await catalog.SearchAsync(new BearingQuery
        {
            ValidationStates = [ReferenceValidationState.Draft, ReferenceValidationState.Checked, ReferenceValidationState.Validated],
        });

        Assert.Equal(["brg-released"], released.Select(b => b.Id));
        Assert.Equal(3, notReleased.Count);
    }

    [Fact]
    public async Task ValidationResults_UseThePlatformsOwnValidationResultShape()
    {
        var catalog = BearingFixtures.BuildCatalog();
        var validator = new BearingValidationService(catalog);
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall());

        IValidationResult result = await validator.ValidateAsync("brg-0001");

        Assert.NotNull(result.Errors);
        Assert.NotNull(result.Warnings);
    }

    [Fact]
    public async Task ComparisonConsumesSearchOutputDirectly_TheSeamAFutureSelectionEngineWillUse()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall("FX-6000", 10, 26, 8));
        await catalog.RegisterAsync("brg-0002", BearingFixtures.DeepGrooveBall("FX-6200", 10, 30, 9));
        await catalog.RegisterAsync("brg-0003", BearingFixtures.DeepGrooveBall("FX-6300", 10, 35, 11));

        var candidates = await catalog.SearchAsync(new BearingQuery
        {
            BoreMinimum = BearingFixtures.Millimetres(10),
            BoreMaximum = BearingFixtures.Millimetres(10),
        });

        var comparison = BearingComparer.Compare(candidates);
        var outsideDiameters = comparison.Row(BearingComparisonProperties.OutsideDiameter)!;

        Assert.Equal(3, candidates.Count);
        Assert.Equal([0.026, 0.030, 0.035], outsideDiameters.Cells.Select(cell => Math.Round(cell.CanonicalValue!.Value, 6)));
    }
}
