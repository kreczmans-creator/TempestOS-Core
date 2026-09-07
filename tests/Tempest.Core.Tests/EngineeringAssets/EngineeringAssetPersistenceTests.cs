using System.Text.Json;
using Tempest.Core.EngineeringAssets;
using Tempest.Core.EngineeringAssets.CalculationPacks;
using Tempest.Core.EngineeringAssets.DesignReviews;
using Tempest.Core.EngineeringAssets.TechnicalDocumentation;
using Tempest.Core.EngineeringAssets.Templates;
using Tempest.Core.EngineeringAssets.Verification;
using Tempest.Core.ReferenceData;
using Xunit;

namespace Tempest.Core.Tests.EngineeringAssets;

/// <summary>
/// The full persistence cycle §35 requires, run against the real
/// document-backed store rather than an in-memory shortcut.
/// </summary>
/// <remarks>
/// Create, persist, reload, compare, revise, retrieve the historical
/// revision, supersede, retrieve the superseded record — for every `P05`
/// library. The `P03` <c>CostFigure</c> defect went undetected precisely
/// because nothing exercised this path.
/// </remarks>
public sealed class EngineeringAssetPersistenceTests
{
    [Fact]
    public async Task A_template_survives_the_full_create_revise_supersede_cycle()
    {
        var catalog = AssetFixtures.BuildTemplateCatalog();

        await AssetFixtures.RegisterAsync(catalog, "tpl-1", AssetFixtures.Template());

        var loaded = await catalog.FindByReferenceAsync("TPL-1");
        Assert.NotNull(loaded);
        Assert.Equal(TemplateKind.Calculation, loaded.Definition.Kind);
        Assert.Equal(3, loaded.Definition.AllFields.Count());
        Assert.Equal("Force", loaded.Definition.FindField("F1")!.ExpectedDimension);
        Assert.Equal(2, loaded.Definition.RequiredFields.Count(f => f.Reference is "F1" or "F2"));

        // Nested structure, enums, nullable strings and the governance
        // graph all have to come back.
        Assert.Equal("owner-1", loaded.Definition.Governance.Ownership!.OwnerPrincipalId);
        Assert.True(loaded.Definition.Governance.IsReviewed);
        Assert.Equal(AssetFixtures.Today.AddDays(-14), loaded.Definition.Governance.Authorship!.AuthoredOn);

        var revised = await catalog.ReviseAsync(
            "tpl-1",
            AssetFixtures.Template() with { Name = "Revised fixture template" },
            AssetFixtures.Verified(),
            "Renamed.");

        Assert.Equal(2, revised.RevisionNumber);

        var historical = await catalog.GetRevisionAsync("tpl-1", 1);
        Assert.Equal("Fictional bracket calculation template", historical.Definition.Name);

        await AssetFixtures.ReleaseAsync(catalog, "tpl-1");
        await AssetFixtures.RegisterAsync(catalog, "tpl-2", AssetFixtures.Template("TPL-2"));
        await catalog.SupersedeAsync("tpl-1", "tpl-2", "Replaced.");

        var superseded = await catalog.FindAsync("tpl-1");
        Assert.Equal(ReferenceValidationState.Superseded, superseded!.ValidationState);
        Assert.Equal("tpl-2", superseded.SupersededByRecordId);
    }

    [Fact]
    public async Task A_released_template_cannot_be_revised_in_place()
    {
        var catalog = AssetFixtures.BuildTemplateCatalog();

        await AssetFixtures.RegisterReleasedAsync(catalog, "tpl-1", AssetFixtures.Template());

        await Assert.ThrowsAsync<ReleasedReferenceImmutableException>(() =>
            catalog.ReviseAsync("tpl-1", AssetFixtures.Template() with { Name = "Sneaky" }, AssetFixtures.Verified(), "No."));
    }

    [Fact]
    public async Task A_calculation_pack_survives_persistence_with_its_pins_intact()
    {
        var catalog = AssetFixtures.BuildCalculationPackCatalog();

        await AssetFixtures.RegisterAsync(catalog, "calc-1", AssetFixtures.Pack());

        var loaded = (await catalog.FindByReferenceAsync("CALC-1"))!.Definition;

        Assert.Equal(2, loaded.Inputs.Count);
        Assert.Equal(new ReferencePin("Materials", "mat-1", 2), loaded.Inputs[0].SourcePin);
        Assert.Single(loaded.AllPins);
        Assert.Equal("sigma = M / Z", Assert.Single(loaded.Method.GoverningEquations));
        Assert.Equal("Below the fixture allowable of 120 MPa.", loaded.Outputs[0].AcceptanceCriterion);
        Assert.True(loaded.Assumptions[0].IsJustified);
    }

    [Fact]
    public async Task A_verification_artefact_survives_persistence_with_its_standing_intact()
    {
        var catalog = AssetFixtures.BuildVerificationCatalog();

        await AssetFixtures.RegisterAsync(catalog, "ver-1", AssetFixtures.Artefact());

        var loaded = (await catalog.FindByReferenceAsync("VER-1"))!.Definition;

        Assert.Equal(VerificationStanding.Passed, loaded.Standing);
        Assert.True(loaded.IsDemonstrated);
        Assert.True(loaded.IsEvidenced);
        Assert.True(loaded.HasIndependentEvidence);
        Assert.Equal(3, loaded.Requirement.RevisionAtVerification);
        Assert.Equal(AssetFixtures.Today.AddDays(-3), loaded.Result!.PerformedOn);
    }

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
    public void Every_P05_asset_type_round_trips_through_JSON(object asset)
    {
        // A blanket guard of the shape that would have caught the P03
        // CostFigure defect: serialise, deserialise, compare the rendered
        // form. A type whose constructor the serialiser cannot call fails
        // here rather than the first time a catalogue reads it back.
        var json = JsonSerializer.Serialize(asset, asset.GetType());
        var restored = JsonSerializer.Deserialize(json, asset.GetType());

        Assert.NotNull(restored);
        Assert.Equal(json, JsonSerializer.Serialize(restored, asset.GetType()));
    }

    public static TheoryData<object> RoundTrippableAssets() =>
    [
        AssetFixtures.Template(),
        AssetFixtures.Pack(),
        AssetFixtures.Artefact(),
        AssetFixtures.Review(),
        AssetFixtures.Document(),
        AssetFixtures.Governance(),
        new EngineeringEvidence(EngineeringEvidenceKind.TestReport, "Fixture.", Reference: "FIX-1"),
        new AssetApplicability { Disciplines = [EngineeringDiscipline.Mechanical], SubjectKinds = ["Bracket"] },
    ];
}
