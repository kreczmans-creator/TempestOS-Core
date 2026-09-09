using System.Text.Json;
using Tempest.Core.EngineeringAssets.DesignReviews;
using Tempest.Core.EngineeringAssets.TechnicalDocumentation;
using Xunit;

namespace Tempest.Core.Tests.EngineeringAssets;

/// <summary>
/// The full persistence cycle §35 requires, for the archived `P05`
/// libraries: DesignReviews, TechnicalDocumentation.
/// </summary>
/// <remarks>
/// WP 18.0C (D-028): split out of the live
/// tests/Tempest.Core.Tests/EngineeringAssets/EngineeringAssetPersistenceTests.cs
/// the same day Templates/CalculationPacks/Verification stayed live.
/// </remarks>
public sealed class EngineeringAssetArchivedPersistenceTests
{
    [Fact]
    public async Task A_design_review_pack_survives_persistence_with_its_graph_intact()
    {
        var catalog = AssetFixtures.BuildDesignReviewCatalog();

        await AssetFixtures.RegisterAsync(catalog, "dr-1", AssetFixtures.Review());

        var loaded = (await catalog.FindByReferenceAsync("DR-1"))!.Definition;

        Assert.Equal(3, loaded.Participants.Count);
        Assert.True(loaded.HasIndependentReviewer);
        Assert.Single(loaded.Observations);
        Assert.Single(loaded.Actions);
        Assert.True(loaded.IsAnswered("OBS-1"));
        Assert.Equal(AssetFixtures.RequirementId, Assert.Single(loaded.RequirementIds));
        Assert.Equal(ReviewOutcome.ProceedWithActions, loaded.Outcome);
    }

    [Fact]
    public async Task A_technical_document_survives_persistence_with_both_revision_axes_intact()
    {
        var catalog = AssetFixtures.BuildDocumentCatalog();
        var document = AssetFixtures.Document();

        await AssetFixtures.RegisterAsync(catalog, "doc-1", document);

        var record = (await catalog.FindByReferenceAsync("FIX-DWG-001"))!;

        // The two revision axes must not have collapsed into one.
        Assert.Equal("B", record.Definition.IssueRevision);
        Assert.Equal(1, record.RevisionNumber);
        Assert.Equal(document.DocumentId, record.Definition.DocumentId);
        Assert.Equal(DocumentStatus.Issued, record.Definition.Status);
        Assert.True(record.Definition.IsInForceAt(AssetFixtures.Today));
        Assert.NotNull(record.Definition.Governance.Approval);
        Assert.Equal("chief-1", record.Definition.Governance.Approval.PrincipalId);
    }

    [Theory]
    [MemberData(nameof(RoundTrippableAssets))]
    public void Every_archived_P05_asset_type_round_trips_through_JSON(object asset)
    {
        var json = JsonSerializer.Serialize(asset, asset.GetType());
        var restored = JsonSerializer.Deserialize(json, asset.GetType());

        Assert.NotNull(restored);
        Assert.Equal(json, JsonSerializer.Serialize(restored, asset.GetType()));
    }

    public static TheoryData<object> RoundTrippableAssets() =>
    [
        AssetFixtures.Review(),
        AssetFixtures.Document(),
    ];
}
