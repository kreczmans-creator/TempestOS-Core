using Tempest.Core.Constants;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.ReferenceData;
using Tempest.Core.Tests.Materials;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Constants;

/// <summary>
/// Shared construction helpers for the Engineering Constants Library's own
/// tests.
/// </summary>
/// <remarks>
/// <b>Every constant below is fictional.</b> No real physical constant is
/// recorded, and no published value or uncertainty is reproduced: the
/// symbols are in a deliberately unusable "fx" series, the source is
/// "TestFixture Metrology", and the numbers are round values chosen to
/// make the rules under test observable. A6's own charter forbids
/// inventing reference values, and a fixture carrying a real constant's
/// digits would be exactly the fabricated reference data that rule exists
/// to prevent — the more so here, since a constant is the one kind of
/// reference data that gets used without being looked at.
/// </remarks>
internal static class ConstantFixtures
{
    public static ConstantCatalog BuildCatalog() => BuildCatalog(out _, out _);

    public static ConstantCatalog BuildCatalog(out EngineeringDocumentStore documentStore, out InMemoryPersistenceStore persistenceStore)
    {
        persistenceStore = new InMemoryPersistenceStore();
        documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        return new ConstantCatalog(documentStore, persistenceStore);
    }

    public static ReferenceQuantityValue Quantity<TDimension>(
        Quantity<TDimension> value,
        ReferenceValueOrigin origin = ReferenceValueOrigin.EngineeringReference)
        where TDimension : IDimension =>
        new(value, origin);

    public static Quantity<Acceleration> MetresPerSecondSquared(double value) =>
        new(value, AccelerationUnits.MetrePerSecondSquared);

    public static Quantity<Dimensionless> Ratio(double value) => new(value, DimensionlessUnits.One);

    public static Quantity<Pressure> Pascals(double value) => new(value, PressureUnits.Pascal);

    public static ReferenceProvenance SourcedProvenance() => new(
        SourceOrganisation: "TestFixture Metrology",
        SourceDocument: "Fixture constants tabulation (not a real publication)",
        SourceRevision: "1",
        SourceDate: new DateOnly(2026, 1, 1),
        SourceLocation: "Table 1",
        ExtractionMethod: ReferenceExtractionMethod.ManualTranscription,
        Notes: "Fictional fixture data.");

    public static ReferenceProvenance VerifiedProvenance() => SourcedProvenance() with
    {
        VerificationStatus = ReferenceVerificationStatus.VerifiedAgainstSource,
        ReviewerPrincipalId = "reviewer-1",
        VerificationDate = new DateOnly(2026, 2, 1),
    };

    /// <summary>A fictional measured constant, carrying an expanded uncertainty.</summary>
    public static ConstantDefinition Measured(string symbol = "fx_a") => new()
    {
        Symbol = symbol,
        Name = "Fixture measured constant",
        Category = ConstantCategory.Universal,
        Value = Quantity(Pascals(1000)),
        Uncertainty = new ConstantUncertainty(
            ConstantUncertaintyKind.Expanded,
            Absolute: Quantity(Pascals(0.5)),
            Relative: 0.0005,
            CoverageFactor: 2,
            Notes: "Fictional fixture uncertainty."),
    };

    /// <summary>A fictional mathematical constant — exact, and dimensionless by its own nature.</summary>
    public static ConstantDefinition Mathematical(string symbol = "fx_m") => new()
    {
        Symbol = symbol,
        Name = "Fixture mathematical constant",
        Category = ConstantCategory.Mathematical,
        Value = Quantity(Ratio(2.5)),
        Uncertainty = ConstantUncertainty.Exact,
    };

    /// <summary>A fictional conventional reference value — exact within its own convention, and true of nowhere in particular.</summary>
    public static ConstantDefinition Conventional(string symbol = "fx_c") => new()
    {
        Symbol = symbol,
        Name = "Fixture conventional acceleration",
        Category = ConstantCategory.ConventionalReference,
        Value = Quantity(MetresPerSecondSquared(10)),
        Uncertainty = ConstantUncertainty.Exact,
        Applicability = "Adopted by the fixture convention for test purposes only; not a value from anywhere real.",
    };

    public static async Task<IReferenceRecord<ConstantDefinition>> ReleaseAsync(ConstantCatalog catalog, string constantId)
    {
        await catalog.SetValidationStateAsync(constantId, ReferenceValidationState.Checked, "Checked.");
        await catalog.SetValidationStateAsync(constantId, ReferenceValidationState.Validated, "Rules pass.");
        return await catalog.SetValidationStateAsync(constantId, ReferenceValidationState.Released, "Released.");
    }
}
