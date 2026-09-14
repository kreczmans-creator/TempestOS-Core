using Tempest.Core.EngineeringDomain;
using Tempest.Core.EngineeringIntelligence;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.EngineeringIntelligence;

// A rule is a governed record: authored, reviewed, released, revisioned
// and superseded through the same shared lifecycle as a material
// (`ADR-0128`). These tests are about that governance — what a rule can do
// before it is released, and what happens to an assessment when it changes.
public class RuleCatalogTests
{
    private static Quantity<Pressure> Mpa(double value) => new(value, PressureUnits.Megapascal);

    private static RuleDefinition Rule(string code = "STR-1", RuleSeverity severity = RuleSeverity.Requirement) => new()
    {
        Code = code,
        Name = "Minimum yield strength",
        Statement = "The material's yield strength must be at least 300 MPa.",
        Severity = severity,
        Domain = RuleDomain.Materials,
        Rationale = "Fixture rationale, not real engineering guidance.",
        Condition = new QuantityComparisonExpression(
            MaterialPropertyNamesForTest.YieldStrength,
            QuantityComparator.AtLeast,
            EngineeringIntelligenceFixtures.Threshold(Mpa(300))),
    };

    [Fact]
    public async Task ARegisteredRule_IsFoundByItsCode_IgnoringCase()
    {
        var catalog = EngineeringIntelligenceFixtures.BuildRuleCatalog();

        await catalog.RegisterAsync("rule-1", Rule(), EngineeringIntelligenceFixtures.Verified());

        Assert.NotNull(await catalog.FindByCodeAsync("str-1"));
    }

    [Fact]
    public async Task TwoRulesCannotShareOneCode()
    {
        // A duplicate code would make "the rule that failed" ambiguous in
        // every assessment that reported it.
        var catalog = EngineeringIntelligenceFixtures.BuildRuleCatalog();

        await catalog.RegisterAsync("rule-1", Rule(), EngineeringIntelligenceFixtures.Verified());

        await Assert.ThrowsAnyAsync<ReferenceDataException>(
            () => catalog.RegisterAsync("rule-2", Rule(), EngineeringIntelligenceFixtures.Verified()));
    }

    [Fact]
    public async Task AnUnreleasedRule_IsNotReturnedAsApplicable()
    {
        // A rule somebody is still drafting must not silently start
        // failing designs.
        var catalog = EngineeringIntelligenceFixtures.BuildRuleCatalog();

        await catalog.RegisterAsync("rule-1", Rule(), EngineeringIntelligenceFixtures.Verified());

        var applicable = await catalog.FindReleasedApplicableAsync(new FakeSubject());

        Assert.Empty(applicable);
    }

    [Fact]
    public async Task AReleasedRule_IsReturnedAsApplicable()
    {
        var catalog = EngineeringIntelligenceFixtures.BuildRuleCatalog();

        await catalog.RegisterAsync("rule-1", Rule(), EngineeringIntelligenceFixtures.Verified());
        await EngineeringIntelligenceFixtures.ReleaseAsync(catalog, "rule-1");

        Assert.Single(await catalog.FindReleasedApplicableAsync(new FakeSubject()));
    }

    [Fact]
    public async Task ARuleForAnotherSubjectKind_IsNotReturnedAsApplicable()
    {
        var catalog = EngineeringIntelligenceFixtures.BuildRuleCatalog();

        await catalog.RegisterAsync(
            "rule-1",
            Rule() with { Applicability = new RuleApplicability { SubjectKinds = [AssessmentSubjectKinds.Bearing] } },
            EngineeringIntelligenceFixtures.Verified());
        await EngineeringIntelligenceFixtures.ReleaseAsync(catalog, "rule-1");

        Assert.Empty(await catalog.FindReleasedApplicableAsync(
            new FakeSubject(subjectKind: AssessmentSubjectKinds.Material)));
    }

    [Fact]
    public async Task RevisingADraftRule_ProducesANewRevision_AndTheOldOneIsStillReadable()
    {
        // An assessment recorded last month pinned revision 1. If revision
        // 1 stopped existing, that assessment could never be reproduced,
        // and the record of what was checked would be worthless.
        var catalog = EngineeringIntelligenceFixtures.BuildRuleCatalog();

        var original = await catalog.RegisterAsync("rule-1", Rule(), EngineeringIntelligenceFixtures.Verified());

        await catalog.ReviseAsync(
            "rule-1",
            Rule() with
            {
                Statement = "The material's yield strength must be at least 350 MPa.",
                Condition = new QuantityComparisonExpression(
                    MaterialPropertyNamesForTest.YieldStrength,
                    QuantityComparator.AtLeast,
                    EngineeringIntelligenceFixtures.Threshold(Mpa(350))),
            },
            EngineeringIntelligenceFixtures.Verified(),
            "Threshold raised while still in draft.");

        var atRevisionOne = await catalog.GetRevisionAsync("rule-1", original.RevisionNumber);
        var current = (await catalog.FindAsync("rule-1"))!;

        Assert.Contains("300 MPa", atRevisionOne.Definition.Statement, StringComparison.Ordinal);
        Assert.Contains("350 MPa", current.Definition.Statement, StringComparison.Ordinal);
        Assert.True(current.RevisionNumber > atRevisionOne.RevisionNumber);
    }

    [Fact]
    public async Task AReleasedRule_CannotBeQuietlyChanged()
    {
        // `P02` inherits this from the shared lifecycle rather than
        // reimplementing it: a released rule is what a recorded assessment
        // pinned, and editing it in place would rewrite history. The
        // correct move is to supersede.
        var catalog = EngineeringIntelligenceFixtures.BuildRuleCatalog();

        await catalog.RegisterAsync("rule-1", Rule(), EngineeringIntelligenceFixtures.Verified());
        await EngineeringIntelligenceFixtures.ReleaseAsync(catalog, "rule-1");

        await Assert.ThrowsAsync<ReleasedReferenceImmutableException>(
            () => catalog.ReviseAsync(
                "rule-1",
                Rule() with { Statement = "Something else entirely." },
                EngineeringIntelligenceFixtures.Verified(),
                "Attempted in-place edit."));
    }

    [Fact]
    public async Task ASupersededRule_StopsBeingApplied_ButItsRecordSurvives()
    {
        // The superseded rule must still be readable: an assessment that
        // pinned it has to remain reproducible even though the rule no
        // longer governs new work.
        var catalog = EngineeringIntelligenceFixtures.BuildRuleCatalog();
        var subject = new FakeSubject().With(MaterialPropertyNamesForTest.YieldStrength, Mpa(320));

        await catalog.RegisterAsync("rule-1", Rule(), EngineeringIntelligenceFixtures.Verified());
        var released = await EngineeringIntelligenceFixtures.ReleaseAsync(catalog, "rule-1");

        var atRelease = RuleEngine.Evaluate(
            released.Definition,
            ReferencePin.For(catalog.LibraryName, released),
            subject,
            ConstantResolutionSet.Empty);

        await catalog.RegisterAsync(
            "rule-2",
            Rule("STR-2") with
            {
                Statement = "The material's yield strength must be at least 350 MPa.",
                Condition = new QuantityComparisonExpression(
                    MaterialPropertyNamesForTest.YieldStrength,
                    QuantityComparator.AtLeast,
                    EngineeringIntelligenceFixtures.Threshold(Mpa(350))),
            },
            EngineeringIntelligenceFixtures.Verified());
        await EngineeringIntelligenceFixtures.ReleaseAsync(catalog, "rule-2");
        await catalog.SupersedeAsync("rule-1", "rule-2", "Threshold raised after release.");

        var applicable = await catalog.FindReleasedApplicableAsync(subject);

        // Only the replacement governs new work, and it reaches the
        // opposite conclusion about the same material.
        Assert.Equal("STR-2", Assert.Single(applicable).Definition.Code);
        Assert.Equal(
            AssessmentOutcome.Fail,
            RuleEngine.Evaluate(
                applicable[0].Definition,
                ReferencePin.For(catalog.LibraryName, applicable[0]),
                subject,
                ConstantResolutionSet.Empty).Outcome);

        // The pinned revision still says what it said, so the earlier
        // assessment reproduces unchanged.
        var pinned = await catalog.GetRevisionAsync("rule-1", atRelease.RulePin.RevisionNumber);

        Assert.Equal(AssessmentOutcome.Pass, atRelease.Outcome);
        Assert.Equal(
            AssessmentOutcome.Pass,
            RuleEngine.Evaluate(pinned.Definition, atRelease.RulePin, subject, ConstantResolutionSet.Empty).Outcome);
    }

    [Fact]
    public async Task SearchingByDomain_ReturnsOnlyThatDomainsRules()
    {
        var catalog = EngineeringIntelligenceFixtures.BuildRuleCatalog();

        await catalog.RegisterAsync("rule-1", Rule(), EngineeringIntelligenceFixtures.Verified());
        await catalog.RegisterAsync(
            "rule-2",
            Rule("FIT-1") with { Domain = RuleDomain.Tolerances },
            EngineeringIntelligenceFixtures.Verified());

        var found = await catalog.SearchAsync(new RuleQuery { Domains = [RuleDomain.Tolerances] });

        Assert.Equal("FIT-1", Assert.Single(found).Definition.Code);
    }
}

/// <summary>The `A1` property names these tests read, named once so a rename shows up here rather than in twenty string literals.</summary>
internal static class MaterialPropertyNamesForTest
{
    public const string YieldStrength = "YieldStrength";
}
