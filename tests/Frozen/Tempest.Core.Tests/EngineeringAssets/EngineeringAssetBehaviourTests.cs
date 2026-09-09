using Tempest.Core.EngineeringAssets;
using Tempest.Core.EngineeringAssets.CalculationPacks;
using Tempest.Core.EngineeringAssets.DesignReviews;
using Tempest.Core.EngineeringAssets.TechnicalDocumentation;
using Tempest.Core.EngineeringAssets.Templates;
using Tempest.Core.EngineeringAssets.Verification;
using Xunit;

namespace Tempest.Core.Tests.EngineeringAssets;

// WP 18.0C (D-028): split out of the live
// tests/Tempest.Core.Tests/EngineeringAssets/EngineeringAssetBehaviourTests.cs
// the same day Templates/CalculationPacks/Verification stayed live.
/// <summary>`E4` / `WP05.4` — review, action, decision and approval kept apart.</summary>
public sealed class DesignReviewTests
{
    [Fact]
    public void A_review_outcome_is_not_an_approval()
    {
        var pack = AssetFixtures.Review(outcome: ReviewOutcome.Proceed);

        Assert.Equal(ReviewOutcome.Proceed, pack.Outcome);
        Assert.Null(pack.Approval);
    }

    [Fact]
    public void An_observation_need_not_carry_a_recommendation()
    {
        var observation = new ReviewObservation("OBS-1", "Something looks wrong.", ObservationSeverity.Major);

        Assert.False(observation.HasRecommendation);
        Assert.False(observation.IsBlocking);
    }

    [Fact]
    public async Task Proceeding_over_an_unanswered_critical_observation_is_an_error()
    {
        var catalog = AssetFixtures.BuildDesignReviewCatalog();
        var service = new DesignReviewValidationService(catalog);

        var pack = AssetFixtures.Review(outcome: ReviewOutcome.Proceed) with
        {
            Observations = [new ReviewObservation("OBS-1", "Structural concern.", ObservationSeverity.Critical)],
            Actions = [],
            Decisions = [],
        };

        Assert.True(pack.ProceedsOverBlockingObservations);

        var result = await service.ValidateDefinitionAsync(pack, AssetFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == DesignReviewValidationRules.ProceedsOverBlockingObservation);
    }

    [Fact]
    public async Task Proceeding_over_a_critical_observation_somebody_decided_about_is_not_an_error()
    {
        var catalog = AssetFixtures.BuildDesignReviewCatalog();
        var service = new DesignReviewValidationService(catalog);

        var pack = AssetFixtures.Review(outcome: ReviewOutcome.Proceed) with
        {
            Observations = [new ReviewObservation("OBS-1", "Structural concern.", ObservationSeverity.Critical)],
            Actions = [],
            Decisions =
            [
                new ReviewDecision("DEC-1", "Accept the concern.", "Fixture rationale.", "chief-1", AssetFixtures.Today, ["OBS-1"]),
            ],
        };

        var result = await service.ValidateDefinitionAsync(pack, AssetFixtures.Verified());

        Assert.DoesNotContain(result.Errors, e => e.Code == DesignReviewValidationRules.ProceedsOverBlockingObservation);
    }

    [Fact]
    public void A_decision_must_name_who_took_it_and_why()
    {
        Assert.Throws<ArgumentException>(() => new ReviewDecision("D1", "Do it.", "Because.", "   "));
        Assert.Throws<ArgumentException>(() => new ReviewDecision("D1", "Do it.", "   ", "chief-1"));
    }

    [Fact]
    public async Task An_action_citing_an_observation_the_pack_does_not_hold_is_an_error()
    {
        var catalog = AssetFixtures.BuildDesignReviewCatalog();
        var service = new DesignReviewValidationService(catalog);

        var pack = AssetFixtures.Review() with
        {
            Actions = [new ReviewAction("ACT-1", "Do a thing.", "engineer-1", ObservationReferences: ["OBS-NOPE"])],
        };

        var result = await service.ValidateDefinitionAsync(pack, AssetFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == DesignReviewValidationRules.ObservationReferenceUnresolved);
    }

    [Fact]
    public async Task A_review_nobody_attended_is_an_error()
    {
        var catalog = AssetFixtures.BuildDesignReviewCatalog();
        var service = new DesignReviewValidationService(catalog);

        var result = await service.ValidateDefinitionAsync(
            AssetFixtures.Review() with { Participants = [] },
            AssetFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == DesignReviewValidationRules.ReviewHadNoParticipants);
    }

    [Fact]
    public async Task Outstanding_actions_are_surfaced_across_every_review_most_overdue_first()
    {
        var catalog = AssetFixtures.BuildDesignReviewCatalog();

        await AssetFixtures.RegisterAsync(catalog, "dr-1", AssetFixtures.Review("DR-1") with
        {
            Actions = [new ReviewAction("ACT-1", "Nearly due.", "engineer-1", AssetFixtures.Today.AddDays(-1))],
        });

        await AssetFixtures.RegisterAsync(catalog, "dr-2", AssetFixtures.Review("DR-2") with
        {
            Actions = [new ReviewAction("ACT-2", "Long overdue.", "engineer-1", AssetFixtures.Today.AddDays(-30))],
        });

        var outstanding = await catalog.FindOutstandingActionsAsync(AssetFixtures.Today);

        Assert.Equal("ACT-2", outstanding[0].Action.Reference);
        Assert.Equal(30, outstanding[0].DaysOverdue);
        Assert.True(outstanding[0].IsOverdue);
    }
}

/// <summary>`E5` / `WP05.5` — governed documents without a competing document system.</summary>
public sealed class TechnicalDocumentTests
{
    [Fact]
    public void Only_an_issued_document_is_in_force()
    {
        Assert.True(DocumentStatuses.IsInForce(DocumentStatus.Issued));
        Assert.False(DocumentStatuses.IsInForce(DocumentStatus.Approved));
        Assert.False(DocumentStatuses.IsInForce(DocumentStatus.Superseded));
    }

    [Fact]
    public void Relationships_are_expressible_as_the_platform_own_document_links()
    {
        var target = Guid.NewGuid();
        var document = AssetFixtures.Document() with
        {
            Relationships = [new DocumentRelationship(Tempest.Core.EngineeringDomain.GovernanceRelationshipKinds.Supersedes, target)],
        };

        var links = document.ToDocumentReferences("engineer-1", DateTimeOffset.UnixEpoch);
        var link = Assert.Single(links);

        Assert.Equal(document.DocumentId, link.SourceDocumentId);
        Assert.Equal(target, link.TargetDocumentId);
        Assert.Equal("supersedes", link.RelationshipKind);
        Assert.Equal("engineer-1", link.CreatedByPrincipalId);
    }

    [Fact]
    public void A_document_holding_no_content_cannot_produce_links()
    {
        var document = AssetFixtures.Document() with { DocumentId = null, ExternalLocation = "Fixture cabinet" };

        Assert.Throws<InvalidOperationException>(() => document.ToDocumentReferences());
    }

    [Fact]
    public async Task An_issued_document_with_no_issue_revision_is_an_error()
    {
        var catalog = AssetFixtures.BuildDocumentCatalog();
        var service = new TechnicalDocumentValidationService(catalog, timeProvider: AssetFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(
            AssetFixtures.Document(issueRevision: null),
            AssetFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == TechnicalDocumentValidationRules.IssuedDocumentHasNoRevision);
    }

    [Fact]
    public async Task Two_live_issues_of_one_document_are_warned_about()
    {
        var catalog = AssetFixtures.BuildDocumentCatalog();
        await AssetFixtures.RegisterAsync(catalog, "doc-a", AssetFixtures.Document("FIX-DWG-001"));

        var service = new TechnicalDocumentValidationService(catalog, timeProvider: AssetFixtures.Clock());

        var successor = AssetFixtures.Document("FIX-DWG-002") with { SupersedesReference = "FIX-DWG-001" };
        var result = await service.ValidateDefinitionAsync(successor, AssetFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == TechnicalDocumentValidationRules.PredecessorStillInForce);
    }

    [Fact]
    public async Task A_record_naming_no_retrievable_content_is_an_error()
    {
        var catalog = AssetFixtures.BuildDocumentCatalog();
        var service = new TechnicalDocumentValidationService(catalog, timeProvider: AssetFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(
            AssetFixtures.Document() with { DocumentId = null, ExternalLocation = null },
            AssetFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == TechnicalDocumentValidationRules.DocumentIsNotRetrievable);
    }

    [Fact]
    public async Task Documents_in_force_exclude_drafts_and_superseded_issues()
    {
        var catalog = AssetFixtures.BuildDocumentCatalog();
        await AssetFixtures.RegisterAsync(catalog, "doc-live", AssetFixtures.Document("FIX-DWG-001"));
        await AssetFixtures.RegisterAsync(catalog, "doc-draft", AssetFixtures.Document("FIX-DWG-002", DocumentStatus.Draft));
        await AssetFixtures.RegisterAsync(catalog, "doc-old", AssetFixtures.Document("FIX-DWG-003", DocumentStatus.Superseded));

        var inForce = await catalog.FindInForceAsync("FIX-PROJ", AssetFixtures.Today);

        Assert.Equal("FIX-DWG-001", Assert.Single(inForce).Definition.Reference);
    }
}

/// <summary>Document-kind uniqueness for the archived P05 kinds.</summary>
public sealed class EngineeringAssetArchivedStructuralTests
{
    [Fact]
    public void Every_archived_P05_document_kind_and_library_name_is_unique()
    {
        string[] libraries =
        [
            DesignReviewCatalog.DesignReviewLibraryName,
            TechnicalDocumentCatalog.TechnicalDocumentLibraryName,
        ];

        string[] kinds =
        [
            DesignReviewCatalog.DesignReviewDocumentKind,
            TechnicalDocumentCatalog.TechnicalDocumentKind,
        ];

        Assert.Equal(libraries.Length, libraries.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(kinds.Length, kinds.Distinct(StringComparer.Ordinal).Count());
    }
}
