using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Materials;

// A1: the materials-specific half of the library. The shared machinery it
// sits on (registration, revision, lifecycle, supersession, hostile data)
// is tested once, in ReferenceDataCatalogTests, and is not restated here.
public class MaterialLibraryTests
{
    private static bool HasError(IValidationResult result, string code) => result.Errors.Any(d => d.Code == code);

    private static bool HasWarning(IValidationResult result, string code) => result.Warnings.Any(d => d.Code == code);

    // ----------------------------------------------------------------
    // Model and taxonomy
    // ----------------------------------------------------------------

    [Fact]
    public void ADefinition_DefaultsEveryOptionalFieldToAbsent()
    {
        var definition = new MaterialDefinition { Name = "X", Family = MaterialFamily.Steel };

        Assert.Null(definition.Designation);
        Assert.Null(definition.Grade);
        Assert.Null(definition.Condition);
        Assert.Null(definition.Supplier);
        Assert.Null(definition.EffectiveDate);
        Assert.Empty(definition.Properties);
        Assert.Empty(definition.Standards);
        Assert.Null(definition.DesignationKey);
    }

    [Fact]
    public void TheDesignationKey_SeparatesAGenericGradeFromASuppliersOwnProduct()
    {
        // Two records may legitimately share a designation when one is a
        // generic grade and the other a named supplier's product, so the
        // supplier is part of the key.
        var generic = new MaterialDefinition { Name = "X", Family = MaterialFamily.Steel, Designation = "FX-1" };
        var supplied = generic with { Supplier = "Fixture Metals" };

        Assert.NotEqual(generic.DesignationKey, supplied.DesignationKey);
        Assert.Equal(generic.DesignationKey, (generic with { Designation = " fx-1 " }).DesignationKey);
    }

    [Theory]
    [InlineData(MaterialFamily.Steel, true)]
    [InlineData(MaterialFamily.Aluminium, true)]
    [InlineData(MaterialFamily.Thermoplastic, false)]
    [InlineData(MaterialFamily.Ceramic, false)]
    public void FamilyTraits_KnowWhichFamiliesAreMetals(MaterialFamily family, bool expected) =>
        Assert.Equal(expected, MaterialFamilyTraits.IsMetal(family));

    [Fact]
    public void FamilyTraits_KnowThatBrittleFamiliesHaveNoYieldPoint()
    {
        Assert.False(MaterialFamilyTraits.HasYieldStrength(MaterialFamily.Ceramic));
        Assert.False(MaterialFamilyTraits.HasYieldStrength(MaterialFamily.Glass));
        Assert.True(MaterialFamilyTraits.HasYieldStrength(MaterialFamily.Steel));
    }

    [Theory]
    [InlineData(MaterialFamily.Unspecified)]
    [InlineData(MaterialFamily.Other)]
    public void FamilyTraits_ReportUnclassifiedFamiliesAsUnknownRatherThanNotApplicable(MaterialFamily family) =>
        Assert.False(MaterialFamilyTraits.IsApplicabilityKnown(family));

    // ----------------------------------------------------------------
    // Property vocabulary
    // ----------------------------------------------------------------

    [Fact]
    public void TheVocabulary_NamesTheDimensionEveryWellKnownPropertyMustCarry()
    {
        Assert.Equal("MassDensity", MaterialPropertyNames.ExpectedDimensionOf(MaterialPropertyNames.Density));
        Assert.Equal("Pressure", MaterialPropertyNames.ExpectedDimensionOf(MaterialPropertyNames.YieldStrength));
        Assert.Equal("Dimensionless", MaterialPropertyNames.ExpectedDimensionOf(MaterialPropertyNames.PoissonsRatio));
        Assert.Equal("Temperature", MaterialPropertyNames.ExpectedDimensionOf(MaterialPropertyNames.MeltingPoint));
    }

    [Fact]
    public void TheVocabulary_StaysOpenToNamesItDoesNotKnow()
    {
        // ADR-0055 rejected closing the property-name set. That decision
        // stands: an unknown name is legitimate, not an error.
        Assert.Null(MaterialPropertyNames.ExpectedDimensionOf("SomeSourceSpecificProperty"));
        Assert.False(MaterialPropertyNames.IsWellKnown("SomeSourceSpecificProperty"));
    }

    [Fact]
    public void AProperty_RefusesAValueThatIsNotADimensionedQuantity()
    {
        Assert.Throws<ReferenceDataException>(() => new ReferenceQuantityValue("7850 kg/m3", ReferenceValueOrigin.Unknown));
    }

    [Fact]
    public void AProperty_ReportsItsOwnDimensionAndCanonicalValue()
    {
        var density = MaterialFixtures.Property(MaterialFixtures.GramsPerCubicCentimetre(7.85));

        Assert.Equal("MassDensity", density.DimensionName);
        Assert.Equal(7850.0, density.CanonicalValue, 6);
        Assert.False(density.IsDerived);
    }

    // ----------------------------------------------------------------
    // Catalogue behaviour that is materials-specific
    // ----------------------------------------------------------------

    [Fact]
    public async Task RegisterAsync_RoundTripsEveryRecordedField()
    {
        var catalog = MaterialFixtures.BuildCatalog();
        var definition = MaterialFixtures.Steel() with
        {
            Supplier = "Fixture Metals",
            SupplierDesignation = "FM-1",
            SourceClassification = "Structural steel",
            ProcessingNotes = "Fixture processing note.",
            EnvironmentalNotes = "Fixture environmental note.",
            EffectiveDate = new DateOnly(2026, 3, 1),
        };

        await catalog.RegisterAsync("mat-steel", definition, MaterialFixtures.Sourced());
        var found = await catalog.FindAsync("mat-steel");

        var read = found!.Definition;
        Assert.Equal("Fixture Structural Steel", read.Name);
        Assert.Equal(MaterialFamily.Steel, read.Family);
        Assert.Equal("FX-STEEL-1", read.Designation);
        Assert.Equal("FX-A", read.Grade);
        Assert.Equal("Normalised", read.Condition);
        Assert.Equal("Fixture Metals", read.Supplier);
        Assert.Equal("FM-1", read.SupplierDesignation);
        Assert.Equal("Structural steel", read.SourceClassification);
        Assert.Equal("Fixture processing note.", read.ProcessingNotes);
        Assert.Equal(new DateOnly(2026, 3, 1), read.EffectiveDate);
        Assert.Single(read.Standards);
        Assert.Equal(5, read.Properties.Count);
    }

    [Fact]
    public async Task RegisterAsync_RoundTripsABoxedPropertyOfEveryDimensionItAccepts()
    {
        var catalog = MaterialFixtures.BuildCatalog();
        var definition = MaterialFixtures.Steel() with
        {
            Properties = new Dictionary<string, ReferenceQuantityValue>
            {
                [MaterialPropertyNames.Density] = MaterialFixtures.Property(MaterialFixtures.GramsPerCubicCentimetre(7.85)),
                [MaterialPropertyNames.MeltingPoint] = MaterialFixtures.Property(MaterialFixtures.DegreesCelsius(1450)),
                [MaterialPropertyNames.ThermalExpansionCoefficient] = MaterialFixtures.Property(
                    new Quantity<ThermalExpansion>(11.7, ThermalExpansionUnits.MicrometrePerMetreKelvin)),
                [MaterialPropertyNames.ImpactEnergy] = MaterialFixtures.Property(new Quantity<Energy>(27, EnergyUnits.Joule)),
            },
        };

        await catalog.RegisterAsync("mat-steel", definition, MaterialFixtures.Sourced());
        var read = (await catalog.FindAsync("mat-steel"))!.Definition.Properties;

        Assert.Equal(MaterialFixtures.GramsPerCubicCentimetre(7.85), (Quantity<MassDensity>)read[MaterialPropertyNames.Density].Value);
        // The affine unit survives storage with its own offset intact.
        Assert.Equal(1723.15, ((Quantity<Temperature>)read[MaterialPropertyNames.MeltingPoint].Value).BaseValue, 6);
    }

    [Fact]
    public async Task FindByDesignationAsync_ResolvesGenericAndSupplierSpecificRecordsSeparately()
    {
        var catalog = MaterialFixtures.BuildCatalog();
        await catalog.RegisterAsync("mat-generic", MaterialFixtures.Steel("FX-1"), MaterialFixtures.Sourced());
        await catalog.RegisterAsync("mat-supplied", MaterialFixtures.Steel("FX-1") with { Supplier = "Fixture Metals" }, MaterialFixtures.Sourced());

        Assert.Equal("mat-generic", (await catalog.FindByDesignationAsync("fx-1"))!.Id);
        Assert.Equal("mat-supplied", (await catalog.FindByDesignationAsync("FX-1", "Fixture Metals"))!.Id);
        Assert.Null(await catalog.FindByDesignationAsync("FX-NOPE"));
    }

    [Fact]
    public async Task RegisterAsync_TwoRecordsSharingASupplierAndDesignation_Throws()
    {
        var catalog = MaterialFixtures.BuildCatalog();
        await catalog.RegisterAsync("mat-1", MaterialFixtures.Steel("FX-1"), MaterialFixtures.Sourced());

        await Assert.ThrowsAsync<DuplicateReferenceKeyException>(
            () => catalog.RegisterAsync("mat-2", MaterialFixtures.Steel("FX-1"), MaterialFixtures.Sourced()));
    }

    [Fact]
    public async Task ARecordWithNoDesignation_IsRegisterableAndCollidesWithNothing()
    {
        var catalog = MaterialFixtures.BuildCatalog();
        var anonymous = new MaterialDefinition { Name = "Unnamed fixture alloy", Family = MaterialFamily.Steel };

        await catalog.RegisterAsync("mat-1", anonymous, MaterialFixtures.Sourced());
        var second = await catalog.RegisterAsync("mat-2", anonymous, MaterialFixtures.Sourced());

        Assert.Equal("mat-2", second.Id);
    }

    // ----------------------------------------------------------------
    // Search
    // ----------------------------------------------------------------

    private static async Task<MaterialCatalog> BuildPopulatedAsync()
    {
        var catalog = MaterialFixtures.BuildCatalog();
        await catalog.RegisterAsync("mat-steel", MaterialFixtures.Steel(), MaterialFixtures.Sourced());
        await catalog.RegisterAsync("mat-poly", MaterialFixtures.Polymer(), MaterialFixtures.Sourced());
        await catalog.RegisterAsync("mat-ceramic", MaterialFixtures.Ceramic(), MaterialFixtures.Sourced());
        return catalog;
    }

    [Fact]
    public async Task SearchAsync_ByFamilyAndName()
    {
        var catalog = await BuildPopulatedAsync();

        Assert.Equal(["mat-steel"], (await catalog.SearchAsync(new MaterialQuery { Families = [MaterialFamily.Steel] })).Select(m => m.Id));
        Assert.Equal(["mat-ceramic"], (await catalog.SearchAsync(new MaterialQuery { NameContains = "ceramic" })).Select(m => m.Id));
    }

    [Fact]
    public async Task SearchAsync_ByDensityRange_ComparesInCanonicalUnits()
    {
        var catalog = await BuildPopulatedAsync();

        var dense = await catalog.SearchAsync(new MaterialQuery
        {
            DensityMinimum = new Quantity<MassDensity>(3000, MassDensityUnits.KilogramPerCubicMetre),
        });

        Assert.Equal(["mat-ceramic", "mat-steel"], dense.Select(m => m.Id));
    }

    [Fact]
    public async Task SearchAsync_ByStrength_DoesNotMatchAMaterialThatRecordsNone()
    {
        // The ceramic records no yield strength at all — an unrecorded
        // value never satisfies a minimum and is never read as zero.
        var catalog = await BuildPopulatedAsync();

        var results = await catalog.SearchAsync(new MaterialQuery { YieldStrengthMinimum = MaterialFixtures.Megapascals(1) });

        Assert.Equal(["mat-steel"], results.Select(m => m.Id));
    }

    [Fact]
    public async Task SearchAsync_ByModulusRangeGradeConditionAndStandard()
    {
        var catalog = await BuildPopulatedAsync();

        Assert.Equal(["mat-steel"], (await catalog.SearchAsync(new MaterialQuery
        {
            YoungsModulusMinimum = MaterialFixtures.Gigapascals(100),
            YoungsModulusMaximum = MaterialFixtures.Gigapascals(300),
        })).Select(m => m.Id));

        Assert.Equal(["mat-steel"], (await catalog.SearchAsync(new MaterialQuery { Grade = "fx-a" })).Select(m => m.Id));
        Assert.Equal(["mat-steel"], (await catalog.SearchAsync(new MaterialQuery { Condition = "Normalised" })).Select(m => m.Id));
        Assert.Equal(["mat-steel"], (await catalog.SearchAsync(new MaterialQuery { CitesStandardContaining = "steel standard" })).Select(m => m.Id));
    }

    [Fact]
    public async Task SearchAsync_ByRecordedProperties_FindsWhatIsActuallyPopulated()
    {
        var catalog = await BuildPopulatedAsync();

        var results = await catalog.SearchAsync(new MaterialQuery
        {
            RecordsProperties = [MaterialPropertyNames.YieldStrength, MaterialPropertyNames.PoissonsRatio],
        });

        Assert.Equal(["mat-steel"], results.Select(m => m.Id));
    }

    [Fact]
    public async Task SearchAsync_ByValidationState_SeparatesReleasedDataFromDrafts()
    {
        var catalog = MaterialFixtures.BuildCatalog();
        await catalog.RegisterAsync("mat-draft", MaterialFixtures.Steel("FX-D"), MaterialFixtures.Sourced());
        await catalog.RegisterAsync("mat-released", MaterialFixtures.Steel("FX-R"), MaterialFixtures.Verified());
        await MaterialFixtures.ReleaseAsync(catalog, "mat-released");

        var released = await catalog.SearchAsync(new MaterialQuery { ValidationStates = [ReferenceValidationState.Released] });

        Assert.Equal(["mat-released"], released.Select(m => m.Id));
    }

    // ----------------------------------------------------------------
    // Comparison
    // ----------------------------------------------------------------

    [Fact]
    public async Task Compare_AcrossFamilies_DistinguishesNotApplicableFromNotRecorded()
    {
        var catalog = await BuildPopulatedAsync();
        var candidates = new[] { (await catalog.FindAsync("mat-steel"))!, (await catalog.FindAsync("mat-ceramic"))! };

        var comparison = MaterialComparer.Compare(candidates);
        var yield = comparison.Row(MaterialPropertyNames.YieldStrength)!;

        Assert.Equal(ReferencePropertyAvailability.Recorded, yield.Cells[0].Availability);
        Assert.Equal(ReferencePropertyAvailability.NotApplicable, yield.Cells[1].Availability);
        Assert.False(comparison.IsSingleFamily);
    }

    [Fact]
    public async Task Compare_ReportsAGenuineGapAsNotRecorded()
    {
        var catalog = await BuildPopulatedAsync();
        var candidates = new[] { (await catalog.FindAsync("mat-steel"))!, (await catalog.FindAsync("mat-poly"))! };

        var row = MaterialComparer.Compare(candidates).Row(MaterialPropertyNames.YieldStrength)!;

        Assert.Equal(ReferencePropertyAvailability.Recorded, row.Cells[0].Availability);
        Assert.Equal(ReferencePropertyAvailability.NotRecorded, row.Cells[1].Availability);
    }

    [Fact]
    public async Task Compare_OrdersDimensionedPropertiesByTheirCanonicalValue()
    {
        var catalog = await BuildPopulatedAsync();
        var candidates = new[] { (await catalog.FindAsync("mat-poly"))!, (await catalog.FindAsync("mat-steel"))! };

        var density = MaterialComparer.Compare(candidates).Row(MaterialPropertyNames.Density)!;

        Assert.Equal(1140.0, density.Cells[0].CanonicalValue!.Value, 6);
        Assert.Equal(7850.0, density.Cells[1].CanonicalValue!.Value, 6);
    }

    [Fact]
    public async Task Compare_ReportsAConditionAsNotApplicableToANonMetal()
    {
        var catalog = await BuildPopulatedAsync();
        var candidates = new[] { (await catalog.FindAsync("mat-poly"))! };

        var row = MaterialComparer.Compare(candidates).Row(MaterialComparisonProperties.Condition)!;

        Assert.Equal(ReferencePropertyAvailability.NotApplicable, row.Cells[0].Availability);
    }

    // ----------------------------------------------------------------
    // Validation
    // ----------------------------------------------------------------

    [Fact]
    public async Task ValidateAsync_ACoherentRecord_HasNoErrors()
    {
        var catalog = MaterialFixtures.BuildCatalog();
        var validator = new MaterialValidationService(catalog);
        await catalog.RegisterAsync("mat-steel", MaterialFixtures.Steel(), MaterialFixtures.Sourced());

        Assert.True((await validator.ValidateAsync("mat-steel")).IsValid);
    }

    [Fact]
    public async Task ValidateDefinitionAsync_ADensityRecordedAsAPressure_IsAnError()
    {
        // The whole point of the controlled vocabulary.
        var validator = new MaterialValidationService(MaterialFixtures.BuildCatalog());
        var definition = MaterialFixtures.Steel() with
        {
            Properties = new Dictionary<string, ReferenceQuantityValue>
            {
                [MaterialPropertyNames.Density] = MaterialFixtures.Property(MaterialFixtures.Megapascals(7850)),
            },
        };

        Assert.True(HasError(
            await validator.ValidateDefinitionAsync(definition, MaterialFixtures.Sourced()),
            MaterialValidationRules.PropertyDimensionMismatch));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_AnUnknownPropertyName_IsNeitherErrorNorWarning()
    {
        var validator = new MaterialValidationService(MaterialFixtures.BuildCatalog());
        var definition = MaterialFixtures.Steel() with
        {
            Properties = new Dictionary<string, ReferenceQuantityValue>
            {
                ["FixtureSpecificProperty"] = MaterialFixtures.Property(MaterialFixtures.Megapascals(1)),
            },
        };

        var result = await validator.ValidateDefinitionAsync(definition, MaterialFixtures.Sourced());

        Assert.False(HasError(result, MaterialValidationRules.PropertyDimensionMismatch));
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public async Task ValidateDefinitionAsync_ANonPositiveDensity_IsAnError(double density)
    {
        var validator = new MaterialValidationService(MaterialFixtures.BuildCatalog());
        var definition = MaterialFixtures.Steel() with
        {
            Properties = new Dictionary<string, ReferenceQuantityValue>
            {
                [MaterialPropertyNames.Density] = MaterialFixtures.Property(MaterialFixtures.GramsPerCubicCentimetre(density)),
            },
        };

        Assert.True(HasError(
            await validator.ValidateDefinitionAsync(definition, MaterialFixtures.Sourced()),
            MaterialValidationRules.PropertyMustBePositive));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_AYieldStrengthAboveTheUltimate_IsAnError()
    {
        var validator = new MaterialValidationService(MaterialFixtures.BuildCatalog());
        var definition = MaterialFixtures.Steel() with
        {
            Properties = new Dictionary<string, ReferenceQuantityValue>
            {
                [MaterialPropertyNames.YieldStrength] = MaterialFixtures.Property(MaterialFixtures.Megapascals(500)),
                [MaterialPropertyNames.UltimateTensileStrength] = MaterialFixtures.Property(MaterialFixtures.Megapascals(450)),
            },
        };

        Assert.True(HasError(
            await validator.ValidateDefinitionAsync(definition, MaterialFixtures.Sourced()),
            MaterialValidationRules.YieldStrengthExceedsUltimate));
    }

    [Theory]
    [InlineData(0.6)]
    [InlineData(-1.5)]
    public async Task ValidateDefinitionAsync_AnImpossiblePoissonsRatio_IsAnError(double ratio)
    {
        var validator = new MaterialValidationService(MaterialFixtures.BuildCatalog());
        var definition = MaterialFixtures.Steel() with
        {
            Properties = new Dictionary<string, ReferenceQuantityValue>
            {
                [MaterialPropertyNames.PoissonsRatio] = MaterialFixtures.Property(MaterialFixtures.Ratio(ratio)),
            },
        };

        Assert.True(HasError(
            await validator.ValidateDefinitionAsync(definition, MaterialFixtures.Sourced()),
            MaterialValidationRules.PoissonsRatioOutOfRange));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_AYieldStrengthOnABrittleFamily_IsAnError()
    {
        var validator = new MaterialValidationService(MaterialFixtures.BuildCatalog());
        var definition = MaterialFixtures.Ceramic() with
        {
            Properties = new Dictionary<string, ReferenceQuantityValue>
            {
                [MaterialPropertyNames.YieldStrength] = MaterialFixtures.Property(MaterialFixtures.Megapascals(100)),
            },
        };

        Assert.True(HasError(
            await validator.ValidateDefinitionAsync(definition, MaterialFixtures.Sourced()),
            MaterialValidationRules.YieldStrengthNotApplicableToFamily));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_AnInvertedServiceTemperatureRange_IsAnError()
    {
        var validator = new MaterialValidationService(MaterialFixtures.BuildCatalog());
        var definition = MaterialFixtures.Polymer() with
        {
            Properties = new Dictionary<string, ReferenceQuantityValue>
            {
                [MaterialPropertyNames.MinimumServiceTemperature] = MaterialFixtures.Property(MaterialFixtures.DegreesCelsius(120)),
                [MaterialPropertyNames.MaximumServiceTemperature] = MaterialFixtures.Property(MaterialFixtures.DegreesCelsius(100)),
            },
        };

        Assert.True(HasError(
            await validator.ValidateDefinitionAsync(definition, MaterialFixtures.Sourced()),
            MaterialValidationRules.ServiceTemperatureRangeInverted));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_NoFamilyOrNoProperties_AreReportedAtTheRightSeverity()
    {
        var validator = new MaterialValidationService(MaterialFixtures.BuildCatalog());

        var noFamily = await validator.ValidateDefinitionAsync(
            new MaterialDefinition { Name = "X", Family = MaterialFamily.Unspecified },
            MaterialFixtures.Sourced());

        Assert.True(HasError(noFamily, MaterialValidationRules.FamilyMustBeStated));
        Assert.True(HasWarning(noFamily, MaterialValidationRules.NoPropertiesRecorded));
        Assert.True(HasWarning(noFamily, MaterialValidationRules.DesignationShouldBeRecorded));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_FamilyOtherWithoutTheSourcesOwnWording_IsAnError()
    {
        var validator = new MaterialValidationService(MaterialFixtures.BuildCatalog());

        var result = await validator.ValidateDefinitionAsync(
            MaterialFixtures.Steel() with { Family = MaterialFamily.Other },
            MaterialFixtures.Sourced());

        Assert.True(HasError(result, MaterialValidationRules.OtherFamilyNeedsSourceClassification));
    }

    [Fact]
    public async Task ValidateDefinitionAsync_ADerivedProperty_IsFlagged()
    {
        var validator = new MaterialValidationService(MaterialFixtures.BuildCatalog());
        var definition = MaterialFixtures.Steel() with
        {
            Properties = new Dictionary<string, ReferenceQuantityValue>
            {
                [MaterialPropertyNames.Density] = MaterialFixtures.Property(
                    MaterialFixtures.GramsPerCubicCentimetre(7.85), ReferenceValueOrigin.DerivedByTempestOS),
            },
        };

        Assert.True(HasWarning(
            await validator.ValidateDefinitionAsync(definition, MaterialFixtures.Sourced()),
            ReferenceValidationRules.DerivedValuePresent));
    }

    [Fact]
    public async Task ValidateLibraryAsync_ReportsOnlyRecordsWithSomethingToSay()
    {
        var catalog = MaterialFixtures.BuildCatalog();
        var validator = new MaterialValidationService(catalog);
        await catalog.RegisterAsync("mat-good", MaterialFixtures.Steel("FX-GOOD"), MaterialFixtures.Sourced());
        await catalog.RegisterAsync(
            "mat-bad",
            MaterialFixtures.Steel("FX-BAD") with
            {
                Properties = new Dictionary<string, ReferenceQuantityValue>
                {
                    [MaterialPropertyNames.Density] = MaterialFixtures.Property(MaterialFixtures.Megapascals(1)),
                },
            },
            MaterialFixtures.Sourced());

        var report = await validator.ValidateLibraryAsync();

        Assert.Equal("Materials", report.Library);
        Assert.Equal(2, report.RecordsExamined);
        Assert.Equal(["mat-bad"], report.Findings.Select(f => f.RecordId));
        Assert.False(report.IsClean);
    }

    // ----------------------------------------------------------------
    // Integration
    // ----------------------------------------------------------------

    [Fact]
    public async Task AFullMaterialLifecycle_IsTraceableEndToEnd()
    {
        var catalog = MaterialFixtures.BuildCatalog();
        var validator = new MaterialValidationService(catalog);

        await catalog.RegisterAsync("mat-1", MaterialFixtures.Steel("FX-1"), MaterialFixtures.Sourced());
        await catalog.SetValidationStateAsync("mat-1", ReferenceValidationState.Checked, "Checked against fixture handbook.");
        await catalog.ReviseAsync(
            "mat-1",
            MaterialFixtures.Steel("FX-1") with
            {
                Properties = MaterialFixtures.Steel().Properties
                    .Append(new KeyValuePair<string, ReferenceQuantityValue>(
                        MaterialPropertyNames.ThermalConductivity,
                        MaterialFixtures.Property(new Quantity<ThermalConductivity>(50, ThermalConductivityUnits.WattPerMetreKelvin))))
                    .ToDictionary(p => p.Key, p => p.Value),
            },
            MaterialFixtures.Verified(),
            "Thermal conductivity added from the same source table; verified.");

        Assert.True((await validator.ValidateAsync("mat-1")).IsValid);

        await catalog.SetValidationStateAsync("mat-1", ReferenceValidationState.Validated, "Rules pass.");
        await catalog.SetValidationStateAsync("mat-1", ReferenceValidationState.Released, "Released.");

        await Assert.ThrowsAsync<ReleasedReferenceImmutableException>(
            () => catalog.ReviseAsync("mat-1", MaterialFixtures.Steel("FX-1"), MaterialFixtures.Verified(), "Refused."));

        await catalog.RegisterAsync("mat-2", MaterialFixtures.Steel("FX-2"), MaterialFixtures.Verified());
        await catalog.SupersedeAsync("mat-1", "mat-2", "Superseded by fixture handbook revision 2.");

        var superseded = await catalog.FindAsync("mat-1");
        Assert.Equal("mat-2", superseded!.SupersededByRecordId);

        // The pre-revision record is still readable exactly as it was.
        var asRegistered = await catalog.GetRevisionAsync("mat-1", 1);
        Assert.False(asRegistered.Definition.Properties.ContainsKey(MaterialPropertyNames.ThermalConductivity));
    }

    [Fact]
    public async Task MaterialsAndBearings_ShareOneDocumentStoreWithoutCollision()
    {
        var persistenceStore = new InMemoryPersistenceStore();
        var documentStore = new EngineeringDocumentStore(persistenceStore, new Tempest.Core.Identity.CurrentPrincipalAccessor());
        var materials = new MaterialCatalog(documentStore, persistenceStore);

        await materials.RegisterAsync("mat-1", MaterialFixtures.Steel(), MaterialFixtures.Sourced());
        var document = await documentStore.FindAsync((await materials.FindAsync("mat-1"))!.UnderlyingDocumentId);

        // The document Kind is unchanged from before the shared layer was
        // extracted, so records written by the earlier implementation are
        // still this library's own.
        Assert.Equal("MaterialSpecification", document!.Kind);
        Assert.Equal(MaterialCatalog.MaterialSpecificationDocumentKind, document.Kind);
    }
}
