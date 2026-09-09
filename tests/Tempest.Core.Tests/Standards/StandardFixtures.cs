using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.ReferenceData;
using Tempest.Core.Standards;
using Tempest.Core.Tests.Materials;

namespace Tempest.Core.Tests.Standards;

/// <summary>
/// Shared construction helpers for the Standards Library's own tests.
/// </summary>
/// <remarks>
/// <b>Every standard below is fictional.</b> No real standard is named,
/// summarised or reproduced, and no real standards body is used: the
/// publisher is "TFX — TestFixture Standards Institute", the designations
/// are in a deliberately unusable "FX-" series, and the scope summaries
/// are the fixture's own sentences. A2's own charter forbids reproducing
/// standard text, and a fixture that looked like a real standard's entry
/// would be exactly the fabricated reference data that rule exists to
/// prevent.
/// </remarks>
internal static class StandardFixtures
{
    public const string BodyCode = "TFX";

    public static StandardCatalog BuildCatalog() => BuildCatalog(out _, out _);

    public static StandardCatalog BuildCatalog(out EngineeringDocumentStore documentStore, out InMemoryPersistenceStore persistenceStore)
    {
        persistenceStore = new InMemoryPersistenceStore();
        documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        return new StandardCatalog(documentStore, persistenceStore);
    }

    public static StandardsBody Body(StandardsBodyKind kind = StandardsBodyKind.International) =>
        new(BodyCode, "TestFixture Standards Institute (not a real body)", kind);

    public static ReferenceProvenance Sourced() => new(
        SourceOrganisation: "TestFixture Publications",
        SourceDocument: "Fixture standards catalogue (not a real publication)",
        SourceRevision: "1",
        SourceDate: new DateOnly(2026, 1, 1),
        SourceLocation: "Entry 12",
        ExtractionMethod: ReferenceExtractionMethod.ManualTranscription,
        Notes: "Fictional fixture data.");

    public static ReferenceProvenance Verified() => Sourced() with
    {
        VerificationStatus = ReferenceVerificationStatus.VerifiedAgainstSource,
        ReviewerPrincipalId = "reviewer-1",
        VerificationDate = new DateOnly(2026, 2, 1),
    };

    /// <summary>A coherent fictional dimensional standard, complete enough that the rules pass on it.</summary>
    public static StandardDefinition Dimensional(string designation = "FX-100", string? edition = "2026") => new()
    {
        Body = Body(),
        Designation = designation,
        Title = "Fixture dimensional standard for test purposes",
        Edition = edition,
        Classification = StandardClassification.DimensionalStandard,
        Disciplines = [StandardDiscipline.Mechanical, StandardDiscipline.Metrology],
        PublicationStatus = StandardPublicationStatus.Current,
        PublicationDate = new DateOnly(2026, 1, 1),
        EffectiveDate = new DateOnly(2026, 3, 1),
        ScopeSummary = "A fixture standard invented for tests; it specifies nothing real.",
        Language = "en",
    };

    /// <summary>A fictional test method — a classification that states no conformity requirements.</summary>
    public static StandardDefinition TestMethod(string designation = "FX-200", string? edition = "2026") =>
        Dimensional(designation, edition) with
        {
            Title = "Fixture test method for test purposes",
            Classification = StandardClassification.TestMethod,
            Disciplines = [StandardDiscipline.Materials],
        };

    /// <summary>A fictional terminology standard — states no requirements and sources no engineering values.</summary>
    public static StandardDefinition Terminology(string designation = "FX-300", string? edition = "2026") =>
        Dimensional(designation, edition) with
        {
            Title = "Fixture terminology for test purposes",
            Classification = StandardClassification.Terminology,
            Disciplines = [StandardDiscipline.Documentation],
        };

    /// <summary>A fictional withdrawn standard, for the publisher-status rules.</summary>
    public static StandardDefinition Withdrawn(string designation = "FX-100", string? edition = "2018") =>
        Dimensional(designation, edition) with
        {
            PublicationStatus = StandardPublicationStatus.Withdrawn,
            PublicationDate = new DateOnly(2018, 1, 1),
            EffectiveDate = new DateOnly(2018, 3, 1),
            WithdrawalDate = new DateOnly(2026, 1, 1),
        };

    public static async Task<IReferenceRecord<StandardDefinition>> ReleaseAsync(StandardCatalog catalog, string standardId)
    {
        await catalog.SetValidationStateAsync(standardId, ReferenceValidationState.Checked, "Checked.");
        await catalog.SetValidationStateAsync(standardId, ReferenceValidationState.Validated, "Rules pass.");
        return await catalog.SetValidationStateAsync(standardId, ReferenceValidationState.Released, "Released.");
    }
}
