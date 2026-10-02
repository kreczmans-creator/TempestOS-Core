using System.Text.Json.Nodes;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.People;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Core.ReferenceData.Seeding;
using Tempest.Core.ReferenceData.Seeding.Datasets;

namespace Tempest.Core.Tests.ReferenceData;

/// <summary>
/// Product Owner runbook B2 (2026-10-01): "It jumps to rev 5 — a release
/// shouldn't change the revision of the data. It's a rev 1 item until it's
/// revised." Lifecycle moves (verify, check, validate, release, supersede)
/// leave <see cref="IReferenceRecord{TDefinition}.ContentRevision"/> alone;
/// only a content revision advances it. The stored version stamp
/// (<see cref="IReferenceRecord{TDefinition}.RevisionNumber"/>) still
/// advances on every write — it is what pins and audit rows cite.
/// </summary>
public sealed class ReferenceContentRevisionTests
{
    private const string ReviewerId = "reviewer-1";

    private static PersonCatalog BuildCatalog(out EngineeringDocumentStore documentStore)
    {
        var persistenceStore = new InMemoryPersistenceStore();
        documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        return new PersonCatalog(documentStore, persistenceStore);
    }

    private static ReferenceReviewService Reviewer()
    {
        var principals = new CurrentPrincipalAccessor();
        principals.SetCurrent(new PlatformPrincipal(
            new PlatformIdentity(ReviewerId, ReviewerId),
            [ReferenceReviewService.VerifyPermission, ReferenceReviewService.ReleasePermission]));
        return new ReferenceReviewService(principals);
    }

    private static Person Person(string role = "Structural Engineer") => new() { DisplayName = "A. Fictional Engineer", Role = role };

    [Fact]
    public async Task ANewlyAddedRecord_IsRevision1()
    {
        var catalog = BuildCatalog(out _);

        var record = await catalog.RegisterAsync("person-1", Person(), PersonProvenance.Default);

        Assert.Equal(1, record.ContentRevision);
        Assert.Equal(1, (await catalog.FindAsync("person-1"))!.ContentRevision);
    }

    [Fact]
    public async Task AddThenVerifyAndRelease_StaysRevision1_WhileTheVersionStampAdvances()
    {
        var catalog = BuildCatalog(out _);
        var review = Reviewer();
        await catalog.RegisterAsync("person-1", Person(), PersonProvenance.Default);

        await review.VerifyAsync(catalog, "person-1", new ReferenceReviewStatement("the record's own recorded provenance"));
        var released = await review.ReleaseAsync(catalog, "person-1", "Released for the test.");

        Assert.Equal(ReferenceValidationState.Released, released.ValidationState);
        Assert.Equal(1, released.ContentRevision);
        Assert.Equal(1, (await catalog.FindAsync("person-1"))!.ContentRevision);

        // The internal version stamp still records every write: register,
        // verification stamp, Checked, Validated, Released.
        Assert.Equal(5, released.RevisionNumber);

        // Every historical version reads as content revision 1.
        var history = await catalog.GetHistoryAsync("person-1");
        foreach (var version in history)
            Assert.Equal(1, (await catalog.GetRevisionAsync("person-1", version.RevisionNumber)).ContentRevision);
    }

    [Fact]
    public async Task ReviseWithNewContent_IsRevision2_AndALaterReleaseKeepsIt()
    {
        var catalog = BuildCatalog(out _);
        var review = Reviewer();
        await catalog.RegisterAsync("person-1", Person(), PersonProvenance.Default);

        var revised = await catalog.ReviseAsync("person-1", Person(role: "Principal Engineer"), PersonProvenance.Default, "Promoted.");
        Assert.Equal(2, revised.ContentRevision);

        await review.VerifyAsync(catalog, "person-1", new ReferenceReviewStatement("the record's own recorded provenance"));
        var released = await review.ReleaseAsync(catalog, "person-1", "Released for the test.");

        Assert.Equal(2, released.ContentRevision);
    }

    [Fact]
    public async Task ReviseWithUnchangedContent_DoesNotAdvanceTheRevision()
    {
        var catalog = BuildCatalog(out _);
        await catalog.RegisterAsync("person-1", Person(), PersonProvenance.Default);

        var revised = await catalog.ReviseAsync("person-1", Person(), PersonProvenance.Default, "No change.");

        Assert.Equal(1, revised.ContentRevision);
        Assert.Equal(2, revised.RevisionNumber);
    }

    [Fact]
    public async Task SupersedingARecord_DoesNotAdvanceItsRevision()
    {
        var catalog = BuildCatalog(out _);
        await catalog.RegisterAsync("person-1", Person(), PersonProvenance.Default);
        await catalog.RegisterAsync("person-2", new Person { DisplayName = "B. Fictional Engineer" }, PersonProvenance.Default);
        var review = Reviewer();
        await review.VerifyAsync(catalog, "person-1", new ReferenceReviewStatement("the record's own recorded provenance"));
        await review.ReleaseAsync(catalog, "person-1", "Released for the test.");

        var superseded = await catalog.SupersedeAsync("person-1", "person-2", "Replaced.");

        Assert.Equal(1, superseded.ContentRevision);
    }

    [Fact]
    public async Task SeededAndReleasedAtSeed_RecordsAreRevision1()
    {
        var persistenceStore = new InMemoryPersistenceStore();
        var documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        var materials = new MaterialCatalog(documentStore, persistenceStore);

        await new ReferenceSeedService(releasePolicy: new ReferenceSeedReleasePolicy()).ApplyAsync(materials, MaterialSeed.Instance);

        var records = await materials.ListAsync();
        Assert.NotEmpty(records);
        Assert.All(records, r =>
        {
            Assert.Equal(ReferenceValidationState.Released, r.ValidationState);
            Assert.Equal(1, r.ContentRevision);
            Assert.True(r.RevisionNumber > 1, "Release at seed still writes lifecycle versions.");
        });
    }

    /// <summary>
    /// Content written before the content revision was stored carries no
    /// value of its own; the catalogue derives one from the history —
    /// counting only versions whose content changed.
    /// </summary>
    [Fact]
    public async Task LegacyContentWithNoStoredRevision_DerivesItFromTheHistory()
    {
        var catalog = BuildCatalog(out var documentStore);
        var registered = await catalog.RegisterAsync("person-1", Person(), PersonProvenance.Default);
        var documentId = registered.UnderlyingDocumentId;

        // Rewrite the record as legacy versions would have stored it: no
        // content revision field. First a content change, then a pure
        // lifecycle move.
        await WriteLegacyVersionAsync(documentStore, documentId, json => json["Definition"]!["Role"] = "Principal Engineer");
        await WriteLegacyVersionAsync(documentStore, documentId, json => json["ValidationState"] = "Checked");

        var record = await catalog.FindAsync("person-1");
        Assert.Equal(2, record!.ContentRevision);
        Assert.Equal(1, (await catalog.GetRevisionAsync("person-1", 1)).ContentRevision);
        Assert.Equal(2, (await catalog.GetRevisionAsync("person-1", 2)).ContentRevision);

        // The next write stamps the derived value and carries on from it.
        var revised = await catalog.ReviseAsync("person-1", Person(role: "Director"), PersonProvenance.Default, "Promoted again.");
        Assert.Equal(3, revised.ContentRevision);
    }

    private static async Task WriteLegacyVersionAsync(EngineeringDocumentStore documentStore, Guid documentId, Action<JsonObject> change)
    {
        var latest = await documentStore.GetLatestRevisionAsync(documentId);
        var json = JsonNode.Parse(latest.Content)!.AsObject();
        json.Remove("ContentRevision");
        change(json);
        await documentStore.ReviseAsync(documentId, json.ToJsonString(), "Legacy write.");
    }
}
