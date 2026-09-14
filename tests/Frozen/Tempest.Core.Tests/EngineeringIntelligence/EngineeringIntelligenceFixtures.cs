using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringIntelligence;
using Tempest.Core.EngineeringIntelligence.Decisions;
using Tempest.Core.EngineeringIntelligence.Reviews;
using Tempest.Core.EngineeringIntelligence.TradeStudies;
using Tempest.Core.Identity;
using Tempest.Core.ReferenceData;
using Tempest.Core.Tests.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.EngineeringIntelligence;

/// <summary>
/// A hand-written assessment subject whose recorded properties are set by
/// the test.
/// </summary>
/// <remarks>
/// Deliberately not a real library's subject adapter. A rule-engine test
/// that failed because a bearing's traits table changed would be testing
/// the wrong thing; the adapters have their own tests. What this fake
/// controls precisely is the one thing the engine cares about — whether a
/// property is Recorded, NotRecorded or NotApplicable, and what its value
/// is when it is recorded.
/// </remarks>
internal sealed class FakeSubject : IAssessmentSubject
{
    private readonly Dictionary<string, SubjectQuantity> _quantities = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SubjectText> _text = new(StringComparer.OrdinalIgnoreCase);

    public FakeSubject(string subjectId = "subject-1", string subjectKind = AssessmentSubjectKinds.Material)
    {
        SubjectId = subjectId;
        SubjectKind = subjectKind;
        DisplayName = subjectId;
    }

    public string SubjectKind { get; init; }

    public string SubjectId { get; }

    public string DisplayName { get; set; }

    public string? Family { get; set; }

    public bool IsApplicabilityKnown { get; set; } = true;

    public ReferencePin? Pin { get; set; }

    /// <summary>Records a property with a value, as a released library record would.</summary>
    public FakeSubject With<TDimension>(string propertyName, Quantity<TDimension> value)
        where TDimension : IDimension
    {
        _quantities[propertyName] = SubjectQuantity.Recorded(
            new ReferenceQuantityValue(ReferenceQuantityCodec.Encode(value), ReferenceValueOrigin.TestReport));

        return this;
    }

    /// <summary>Records a property as genuinely not applicable to this subject, which is not the same as missing.</summary>
    public FakeSubject WithNotApplicable(string propertyName)
    {
        _quantities[propertyName] = SubjectQuantity.NotApplicable;

        return this;
    }

    /// <summary>Records a text attribute.</summary>
    public FakeSubject WithText(string attributeName, string value)
    {
        _text[attributeName] = SubjectText.Recorded(value);

        return this;
    }

    public SubjectQuantity GetQuantity(string propertyName) =>
        _quantities.TryGetValue(propertyName, out var quantity) ? quantity : SubjectQuantity.NotRecorded;

    public SubjectText GetText(string attributeName) =>
        _text.TryGetValue(attributeName, out var text) ? text : SubjectText.NotRecorded;
}

/// <summary>Shared construction for the `P02` test suite.</summary>
internal static class EngineeringIntelligenceFixtures
{
    /// <summary>A fixed instant, so a record's own timestamp is asserted rather than tolerated.</summary>
    public static DateTimeOffset FixedNow { get; } = new(2026, 3, 1, 9, 30, 0, TimeSpan.Zero);

    public static FakeTimeProvider Clock() => new(FixedNow);

    public static RuleCatalog BuildRuleCatalog()
    {
        var persistence = new InMemoryPersistenceStore();

        return new RuleCatalog(new EngineeringDocumentStore(persistence, new CurrentPrincipalAccessor()), persistence);
    }

    public static DecisionTreeCatalog BuildTreeCatalog()
    {
        var persistence = new InMemoryPersistenceStore();

        return new DecisionTreeCatalog(new EngineeringDocumentStore(persistence, new CurrentPrincipalAccessor()), persistence);
    }

    public static ReviewDefinitionCatalog BuildReviewCatalog()
    {
        var persistence = new InMemoryPersistenceStore();

        return new ReviewDefinitionCatalog(new EngineeringDocumentStore(persistence, new CurrentPrincipalAccessor()), persistence);
    }

    public static TradeStudyCatalog BuildTradeStudyCatalog()
    {
        var persistence = new InMemoryPersistenceStore();

        return new TradeStudyCatalog(new EngineeringDocumentStore(persistence, new CurrentPrincipalAccessor()), persistence);
    }

    /// <summary>Provenance a named reviewer has verified — the only kind that can reach Released.</summary>
    public static ReferenceProvenance Verified() => new(
        SourceOrganisation: "TestFixture Engineering",
        SourceDocument: "Fixture design standard (not a real publication)",
        SourceRevision: "1",
        SourceDate: new DateOnly(2026, 1, 1),
        SourceLocation: "Clause 1",
        ExtractionMethod: ReferenceExtractionMethod.ManualTranscription,
        Notes: "Fictional fixture data.")
    {
        VerificationStatus = ReferenceVerificationStatus.VerifiedAgainstSource,
        ReviewerPrincipalId = "reviewer-1",
        VerificationDate = new DateOnly(2026, 2, 1),
    };

    /// <summary>Walks a record through the full lifecycle to Released.</summary>
    public static async Task<IReferenceRecord<TDefinition>> ReleaseAsync<TDefinition>(
        ReferenceDataCatalog<TDefinition> catalog,
        string recordId)
        where TDefinition : class
    {
        await catalog.SetValidationStateAsync(recordId, ReferenceValidationState.Checked, "Checked.");
        await catalog.SetValidationStateAsync(recordId, ReferenceValidationState.Validated, "Rules pass.");
        return await catalog.SetValidationStateAsync(recordId, ReferenceValidationState.Released, "Released.");
    }

    /// <summary>A quantity threshold, stated in the unit an engineer would state it in.</summary>
    public static RuleThreshold Threshold<TDimension>(Quantity<TDimension> value)
        where TDimension : IDimension =>
        RuleThreshold.FromValue(new ReferenceQuantityValue(
            ReferenceQuantityCodec.Encode(value),
            ReferenceValueOrigin.EngineeringReference));
}

/// <summary>A clock the test pins, so "when" is asserted rather than tolerated.</summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FakeTimeProvider(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;
}
