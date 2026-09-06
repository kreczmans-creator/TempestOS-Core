using System.Reflection;
using Tempest.Core.EngineeringAssets;
using Tempest.Core.EngineeringAssets.CalculationPacks;
using Tempest.Core.EngineeringAssets.DesignReviews;
using Tempest.Core.EngineeringAssets.TechnicalDocumentation;
using Tempest.Core.EngineeringAssets.Templates;
using Tempest.Core.EngineeringAssets.Verification;
using Tempest.Core.ReferenceData;
using Xunit;

namespace Tempest.Core.Tests.EngineeringAssets;

/// <summary>`E1` / `WP05.1` — templates as structure, and the revision promise.</summary>
public sealed class TemplateTests
{
    [Fact]
    public async Task Using_a_template_pins_the_revision_worked_from()
    {
        var catalog = AssetFixtures.BuildTemplateCatalog();
        await AssetFixtures.RegisterAsync(catalog, "tpl-1", AssetFixtures.Template());

        var pin = await catalog.PinAsync("TPL-1");
        var usage = new TemplateUsage(pin!, "Fixture bracket calculation.", "engineer-1", AssetFixtures.Today);

        await catalog.ReviseAsync(
            "tpl-1",
            AssetFixtures.Template() with { Name = "Revision two" },
            AssetFixtures.Verified(),
            "Changed.");

        var current = await catalog.FindByReferenceAsync("TPL-1");

        // The template moved on; the usage did not.
        Assert.Equal(1, usage.TemplatePin.RevisionNumber);
        Assert.Equal(2, current!.RevisionNumber);
        Assert.True(usage.IsBehind(current.RevisionNumber));
    }

    [Fact]
    public async Task Pinning_takes_the_revision_from_the_record_not_the_caller()
    {
        var catalog = AssetFixtures.BuildTemplateCatalog();
        await AssetFixtures.RegisterAsync(catalog, "tpl-1", AssetFixtures.Template());
        await catalog.ReviseAsync("tpl-1", AssetFixtures.Template() with { Name = "Two" }, AssetFixtures.Verified(), "x");

        var pin = await catalog.PinAsync("TPL-1");

        Assert.Equal(2, pin!.RevisionNumber);
        Assert.Equal(TemplateCatalog.TemplateLibraryName, pin.Library);
    }

    [Fact]
    public async Task Pinning_an_unregistered_template_yields_nothing_rather_than_a_fabricated_pin()
    {
        var catalog = AssetFixtures.BuildTemplateCatalog();

        Assert.Null(await catalog.PinAsync("NOT-REGISTERED"));
    }

    [Fact]
    public async Task Only_released_templates_are_offered_for_use()
    {
        var catalog = AssetFixtures.BuildTemplateCatalog();
        await AssetFixtures.RegisterAsync(catalog, "tpl-draft", AssetFixtures.Template("TPL-DRAFT"));
        await AssetFixtures.RegisterReleasedAsync(catalog, "tpl-live", AssetFixtures.Template("TPL-LIVE"));

        var applicable = await catalog.FindApplicableAsync(new AssetEnquiry());

        Assert.Equal("TPL-LIVE", Assert.Single(applicable).Definition.Reference);
    }

    [Fact]
    public async Task A_choice_field_offering_no_choices_is_an_error()
    {
        var catalog = AssetFixtures.BuildTemplateCatalog();
        var service = new TemplateValidationService(catalog, AssetFixtures.Clock());

        var template = AssetFixtures.Template() with
        {
            Sections =
            [
                new TemplateSection("S1", "Choices", Fields:
                    [new TemplateField("F1", "Pick one", TemplateFieldKind.Choice, IsRequired: true)]),
            ],
        };

        var result = await service.ValidateDefinitionAsync(template, AssetFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == TemplateValidationRules.ChoiceFieldHasNoChoices);
    }

    [Fact]
    public async Task A_mandatory_section_requiring_nothing_is_warned_about()
    {
        var catalog = AssetFixtures.BuildTemplateCatalog();
        var service = new TemplateValidationService(catalog, AssetFixtures.Clock());

        var template = AssetFixtures.Template() with
        {
            Sections =
            [
                new TemplateSection("S1", "Mandatory in name only", IsMandatory: true, Fields:
                    [new TemplateField("F1", "Optional thing")]),
            ],
        };

        var result = await service.ValidateDefinitionAsync(template, AssetFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == TemplateValidationRules.MandatorySectionRequiresNothing);
    }

    [Fact]
    public async Task A_template_that_asks_for_nothing_is_an_error()
    {
        var catalog = AssetFixtures.BuildTemplateCatalog();
        var service = new TemplateValidationService(catalog, AssetFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(
            AssetFixtures.Template() with { Sections = [] },
            AssetFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == TemplateValidationRules.TemplateHasNoStructure);
    }

    [Fact]
    public void An_unrestricted_applicability_covers_everything_it_does_not_mention()
    {
        var unrestricted = AssetApplicability.Unrestricted;

        Assert.True(unrestricted.CoversDiscipline(EngineeringDiscipline.Mechanical));
        Assert.True(unrestricted.CoversProject("anything"));
        Assert.False(unrestricted.IsRestricted);
    }

    [Fact]
    public void A_restricted_applicability_covers_only_what_it_names()
    {
        var mechanical = new AssetApplicability { Disciplines = [EngineeringDiscipline.Mechanical] };

        Assert.True(mechanical.CoversDiscipline(EngineeringDiscipline.Mechanical));
        Assert.False(mechanical.CoversDiscipline(EngineeringDiscipline.Electrical));
    }
}

/// <summary>`E2` / `WP05.2` — packaging a calculation so it survives its author.</summary>
public sealed class CalculationPackTests
{
    [Fact]
    public void A_pack_with_an_unsourced_input_is_not_reproducible()
    {
        var pack = AssetFixtures.Pack() with
        {
            Inputs = [new CalculationInput("I1", "Applied load", "1200 N")],
        };

        Assert.False(pack.IsReproducible);
        Assert.Single(pack.UnsourcedInputs);
    }

    [Fact]
    public void A_numerical_pack_naming_no_solver_version_is_not_reproducible()
    {
        var pack = AssetFixtures.Pack(method: CalculationMethodKind.Numerical) with
        {
            Method = new CalculationMethod(CalculationMethodKind.Numerical, "Fixture FEA.", ToolName: "FixtureFEA"),
        };

        Assert.False(pack.Method.IsToolIdentified);
        Assert.False(pack.IsReproducible);
    }

    [Fact]
    public void A_numerical_pack_naming_its_solver_and_version_is_reproducible()
    {
        var pack = AssetFixtures.Pack() with
        {
            Method = new CalculationMethod(CalculationMethodKind.Numerical, "Fixture FEA.", ToolName: "FixtureFEA", ToolVersion: "2026.1"),
            Inputs = [new CalculationInput("I1", "Load", "1200 N", new ReferencePin("Materials", "mat-1", 2))],
        };

        Assert.True(pack.IsReproducible);
    }

    [Fact]
    public async Task An_unsourced_input_is_an_error_and_an_unpinned_one_only_a_warning()
    {
        var catalog = AssetFixtures.BuildCalculationPackCatalog();
        var service = new CalculationPackValidationService(catalog, timeProvider: AssetFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(AssetFixtures.Pack(), AssetFixtures.Verified());

        // I2 names its source in words but pins nothing: a warning.
        Assert.Contains(result.Warnings, w => w.Code == CalculationPackValidationRules.InputIsUntraceable);
        Assert.DoesNotContain(result.Errors, e => e.Code == CalculationPackValidationRules.InputHasNoSource);

        var unsourced = await service.ValidateDefinitionAsync(
            AssetFixtures.Pack() with { Inputs = [new CalculationInput("I1", "Load", "1200 N")] },
            AssetFixtures.Verified());

        Assert.Contains(unsourced.Errors, e => e.Code == CalculationPackValidationRules.InputHasNoSource);
    }

    [Fact]
    public async Task A_pack_naming_a_platform_calculation_but_no_execution_is_warned_about()
    {
        var catalog = AssetFixtures.BuildCalculationPackCatalog();
        var service = new CalculationPackValidationService(catalog, timeProvider: AssetFixtures.Clock());

        var pack = AssetFixtures.Pack() with
        {
            Method = new CalculationMethod(CalculationMethodKind.ClosedForm, "Platform.", CalculationDefinitionId: "beam-bending"),
        };

        var result = await service.ValidateDefinitionAsync(pack, AssetFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == CalculationPackValidationRules.PlatformCalculationHasNoExecution);
    }

    [Fact]
    public async Task A_pack_pinned_to_a_superseded_record_is_warned_about_and_never_altered()
    {
        var templates = AssetFixtures.BuildTemplateCatalog();
        var original = await AssetFixtures.RegisterReleasedAsync(templates, "tpl-1", AssetFixtures.Template());
        await AssetFixtures.RegisterAsync(templates, "tpl-2", AssetFixtures.Template("TPL-2"));
        await templates.SupersedeAsync("tpl-1", "tpl-2", "Replaced.");

        var packs = AssetFixtures.BuildCalculationPackCatalog();
        var pack = AssetFixtures.Pack() with
        {
            Inputs = [new CalculationInput("I1", "Load", "1200 N", new ReferencePin(templates.LibraryName, "tpl-1", original.RevisionNumber))],
        };

        var service = new CalculationPackValidationService(
            packs,
            [new CatalogPinResolver<EngineeringTemplate>(templates)],
            AssetFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(pack, AssetFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == CalculationPackValidationRules.PinnedSourceSuperseded);
        Assert.Equal("1200 N", pack.Inputs[0].Value);
    }

    [Fact]
    public async Task Packs_citing_a_revised_record_can_be_found_afterwards()
    {
        var packs = AssetFixtures.BuildCalculationPackCatalog();
        await AssetFixtures.RegisterAsync(packs, "calc-1", AssetFixtures.Pack());
        await AssetFixtures.RegisterAsync(packs, "calc-2", AssetFixtures.Pack("CALC-2") with { Inputs = [new CalculationInput("I1", "x", "1", new ReferencePin("Materials", "mat-9", 1))] });

        var citing = await packs.FindCitingAsync(new ReferencePin("Materials", "mat-1", 5));

        Assert.Equal("CALC-1", Assert.Single(citing).Definition.Reference);
    }
}

/// <summary>`E3` / `WP05.3` — missing evidence is never a pass.</summary>
public sealed class VerificationArtefactTests
{
    [Fact]
    public void An_artefact_with_no_result_stands_at_not_performed()
    {
        var artefact = AssetFixtures.Artefact(standing: VerificationStanding.NotPerformed);

        Assert.Equal(VerificationStanding.NotPerformed, artefact.Standing);
        Assert.False(artefact.IsDemonstrated);
        Assert.True(artefact.IsOutstanding);
    }

    [Fact]
    public void Not_applicable_is_not_a_pass()
    {
        Assert.False(VerificationStandings.IsDemonstrated(VerificationStanding.NotApplicable));
        Assert.False(VerificationStandings.IsPerformed(VerificationStanding.NotApplicable));
    }

    [Fact]
    public void Verifying_nothing_is_not_verifying_everything()
    {
        Assert.Equal(VerificationStanding.NotPerformed, VerificationStandings.Weakest([]));
        Assert.Equal(
            VerificationStanding.Failed,
            VerificationStandings.Weakest([VerificationStanding.Passed, VerificationStanding.Failed]));
    }

    [Fact]
    public async Task A_pass_with_no_locatable_evidence_is_an_error()
    {
        var catalog = AssetFixtures.BuildVerificationCatalog();
        var service = new VerificationArtefactValidationService(catalog);

        var result = await service.ValidateDefinitionAsync(
            AssetFixtures.Artefact(evidenced: false),
            AssetFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == VerificationValidationRules.PassIsUnevidenced);
    }

    [Fact]
    public async Task An_unattributable_result_is_an_error()
    {
        var catalog = AssetFixtures.BuildVerificationCatalog();
        var service = new VerificationArtefactValidationService(catalog);

        var artefact = AssetFixtures.Artefact() with
        {
            Result = new VerificationResult(VerificationStanding.Passed, "It passed."),
        };

        var result = await service.ValidateDefinitionAsync(artefact, AssetFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == VerificationValidationRules.ResultIsNotAttributable);
    }

    [Fact]
    public async Task Declaring_a_requirement_inapplicable_without_a_reason_is_an_error()
    {
        var catalog = AssetFixtures.BuildVerificationCatalog();
        var service = new VerificationArtefactValidationService(catalog);

        var artefact = AssetFixtures.Artefact(standing: VerificationStanding.NotPerformed) with
        {
            NotApplicableReason = "   ",
        };

        // Whitespace is not a reason, so the artefact reads as NotPerformed
        // rather than slipping through as an unexplained NotApplicable.
        Assert.Equal(VerificationStanding.NotPerformed, artefact.Standing);

        var declared = artefact with { NotApplicableReason = null, Result = new VerificationResult(VerificationStanding.NotApplicable, "n/a", "tester-1", AssetFixtures.Today) };
        var result = await service.ValidateDefinitionAsync(declared, AssetFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == VerificationValidationRules.NotApplicableWithoutReason);
    }

    [Fact]
    public async Task Tracing_an_unverified_requirement_reports_a_concern_rather_than_a_clean_result()
    {
        var catalog = AssetFixtures.BuildVerificationCatalog();
        var service = new VerificationTraceService(catalog);

        var trace = await service.TraceAsync(AssetFixtures.RequirementId);

        Assert.False(trace.IsDemonstrated);
        Assert.False(trace.IsPlanned);
        Assert.False(trace.IsClean);
        Assert.Equal(VerificationStanding.NotPerformed, trace.Standing);
    }

    [Fact]
    public async Task Tracing_reports_the_weakest_standing_across_every_artefact()
    {
        var catalog = AssetFixtures.BuildVerificationCatalog();
        await AssetFixtures.RegisterAsync(catalog, "ver-1", AssetFixtures.Artefact("VER-1"));
        await AssetFixtures.RegisterAsync(catalog, "ver-2", AssetFixtures.Artefact("VER-2", VerificationStanding.Failed, method: VerificationMethod.Inspection));

        var trace = await new VerificationTraceService(catalog).TraceAsync(AssetFixtures.RequirementId);

        Assert.Equal(VerificationStanding.Failed, trace.Standing);
        Assert.False(trace.IsDemonstrated);
        Assert.Contains(trace.Concerns, c => c.Contains("VER-2"));
    }

    [Fact]
    public async Task Outstanding_verifications_come_back_failures_first()
    {
        var catalog = AssetFixtures.BuildVerificationCatalog();
        await AssetFixtures.RegisterAsync(catalog, "ver-1", AssetFixtures.Artefact("VER-1", VerificationStanding.NotPerformed));
        await AssetFixtures.RegisterAsync(catalog, "ver-2", AssetFixtures.Artefact("VER-2", VerificationStanding.Failed));

        var outstanding = await catalog.FindOutstandingAsync();

        Assert.Equal("VER-2", outstanding[0].Definition.Reference);
    }
}

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
            Relationships = [new DocumentRelationship(DocumentRelationship.Kinds.Supersedes, target)],
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

/// <summary>
/// Structural guards over `P05`: assertions about what the code does
/// <em>not</em> do.
/// </summary>
public sealed class EngineeringAssetStructuralTests
{
    private static readonly string[] ForbiddenNameFragments =
    [
        "Approve",
        "Authorise",
        "Authorize",
        "SignOff",
        "Certify",
        "GrantApproval",
        "IssueApproval",
    ];

    public static TheoryData<Type> AssetTypes
    {
        get
        {
            var data = new TheoryData<Type>();

            foreach (var type in typeof(EngineeringTemplate).Assembly
                         .GetTypes()
                         .Where(t => t.Namespace?.StartsWith("Tempest.Core.EngineeringAssets", StringComparison.Ordinal) == true))
                data.Add(type);

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(AssetTypes))]
    public void No_P05_type_performs_an_act_of_approval(Type type)
    {
        // P05 records approvals a named person gave; it confers none.
        var members = type
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => m is MethodInfo { IsSpecialName: false })
            .Select(m => m.Name)
            .ToList();

        foreach (var forbidden in ForbiddenNameFragments)
            Assert.DoesNotContain(members, name => name.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Every_P05_document_kind_and_library_name_is_unique()
    {
        string[] libraries =
        [
            TemplateCatalog.TemplateLibraryName,
            CalculationPackCatalog.CalculationPackLibraryName,
            VerificationArtefactCatalog.VerificationArtefactLibraryName,
            DesignReviewCatalog.DesignReviewLibraryName,
            TechnicalDocumentCatalog.TechnicalDocumentLibraryName,
        ];

        string[] kinds =
        [
            TemplateCatalog.TemplateDocumentKind,
            CalculationPackCatalog.CalculationPackDocumentKind,
            VerificationArtefactCatalog.VerificationArtefactDocumentKind,
            DesignReviewCatalog.DesignReviewDocumentKind,
            TechnicalDocumentCatalog.TechnicalDocumentKind,
        ];

        Assert.Equal(libraries.Length, libraries.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(kinds.Length, kinds.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void P05_does_not_duplicate_the_requirements_or_calculation_models()
    {
        // E3 references a requirement by Id; it must not carry a
        // Requirements type. E2 links calculation records by Id; it must
        // not carry a Calculations type.
        var offenders = typeof(EngineeringTemplate).Assembly
            .GetTypes()
            .Where(t => t.Namespace?.StartsWith("Tempest.Core.EngineeringAssets", StringComparison.Ordinal) == true)
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(p => p.PropertyType.Namespace is "Tempest.Core.Requirements" or "Tempest.Core.Calculations")
            .ToList();

        Assert.Empty(offenders);
    }
}
