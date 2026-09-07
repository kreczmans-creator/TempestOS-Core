using Tempest.Core.Constants;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Seeding;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Population;

// The population phase's own gate: the seed datasets are written by hand
// from external documents, so these tests ask the questions a reviewer
// would — did it land, does it still mean the same thing, does it say
// where it came from, and does it refuse to claim more than it can.
public class SeedDatasetTests
{
    [Fact]
    public async Task EveryDataset_RegistersEveryRecordItOffers()
    {
        var harness = new SeedHarness();

        var outcomes = await harness.SeedEverythingAsync();

        Assert.All(outcomes, outcome => Assert.Equal(0, outcome.AlreadyPresentCount));
        Assert.All(outcomes, outcome => Assert.True(outcome.RegisteredCount > 0, $"{outcome.LibraryName} seeded nothing."));

        // Counted per library rather than in total, so that a record lost
        // from one dataset cannot be masked by a record gained in another,
        // and so a change here is a decision somebody makes deliberately
        // rather than a diff nobody notices.
        var counts = outcomes.ToDictionary(o => o.LibraryName, o => o.RegisteredCount);

        Assert.Equal(6, counts[harness.Materials.LibraryName]);
        Assert.Equal(12, counts[harness.Constants.LibraryName]);
        Assert.Equal(7, counts[harness.Fasteners.LibraryName]);
        Assert.Equal(2, counts[harness.Bearings.LibraryName]);
        Assert.Equal(4, counts[harness.Processes.LibraryName]);
        Assert.Equal(14, counts[harness.Standards.LibraryName]);
        Assert.Equal(5, counts[harness.Rules.LibraryName]);
        Assert.Equal(3, counts[harness.Suppliers.LibraryName]);
        Assert.Equal(1, counts[harness.Costs.LibraryName]);
        Assert.Equal(2, counts[harness.LeadTimes.LibraryName]);
        Assert.Equal(1, counts[harness.Templates.LibraryName]);
        Assert.Equal(1, counts[harness.CalculationPacks.LibraryName]);
        Assert.Equal(1, counts[harness.VerificationArtefacts.LibraryName]);
        Assert.Equal(1, counts[harness.DesignReviews.LibraryName]);
        Assert.Equal(1, counts[harness.TechnicalDocuments.LibraryName]);
        Assert.Equal(2, counts[harness.Prompts.LibraryName]);
        Assert.Equal(1, counts[harness.AcademyNodes.LibraryName]);
        Assert.Equal(1, counts[harness.Challenges.LibraryName]);
        Assert.Equal(1, counts[harness.WorkedExamples.LibraryName]);
    }

    [Fact]
    public async Task SeedingTwice_ChangesNothingTheSecondTime()
    {
        // The property that makes seeding safe to run at every start-up,
        // and safe to run against a library somebody has since edited.
        var harness = new SeedHarness();

        await harness.SeedEverythingAsync();
        var second = await harness.SeedEverythingAsync();

        Assert.All(second, outcome => Assert.True(outcome.MadeNoChange, $"{outcome.LibraryName} wrote on a re-run."));
        Assert.All(second, outcome => Assert.Equal(0, outcome.RegisteredCount));
    }

    [Fact]
    public async Task SeedingNeverOverwritesAnEditSomebodyMadeAfterwards()
    {
        var harness = new SeedHarness();
        await harness.SeedEverythingAsync();

        var registered = await harness.Materials.FindAsync(MaterialSeed.S355J2);
        Assert.NotNull(registered);

        await harness.Materials.ReviseAsync(
            MaterialSeed.S355J2,
            registered.Definition with { Notes = "Corrected by a person after seeding." },
            registered.Provenance,
            "Reviewer correction applied after the initial seeding.");

        await harness.SeedEverythingAsync();

        var afterReseed = await harness.Materials.FindAsync(MaterialSeed.S355J2);
        Assert.Equal("Corrected by a person after seeding.", afterReseed!.Definition.Notes);
    }

    [Fact]
    public async Task EverySeededRecord_PassesItsOwnLibrarysValidation()
    {
        // The instruction's own rule: validation is not weakened to make
        // an import succeed. If a source disagrees with the model, the
        // dataset changes, never the rules.
        var harness = new SeedHarness();
        await harness.SeedEverythingAsync();

        var reports = new[]
        {
            await new Tempest.Core.Standards.StandardValidationService(harness.Standards).ValidateLibraryAsync(),
            await new MaterialValidationService(harness.Materials).ValidateLibraryAsync(),
            await new ConstantValidationService(harness.Constants).ValidateLibraryAsync(),
            await new Tempest.Core.Fasteners.FastenerValidationService(harness.Fasteners).ValidateLibraryAsync(),
            await new Tempest.Core.Bearings.BearingValidationService(harness.Bearings).ValidateLibraryAsync(),
            await new Tempest.Core.Manufacturing.ProcessValidationService(harness.Processes).ValidateLibraryAsync(),
            await new Tempest.Core.EngineeringIntelligence.RuleValidationService(harness.Rules).ValidateLibraryAsync(),
            await new Tempest.Core.CommercialIntelligence.Suppliers.SupplierValidationService(harness.Suppliers).ValidateLibraryAsync(),
            await new Tempest.Core.CommercialIntelligence.Costs.ProcessCostValidationService(harness.Costs).ValidateLibraryAsync(),
            await new Tempest.Core.CommercialIntelligence.LeadTimes.LeadTimeValidationService(harness.LeadTimes).ValidateLibraryAsync(),
        };

        foreach (var report in reports)
        {
            var failures = report.Findings
                .Where(f => f.Result.Errors.Count > 0)
                .Select(f => $"{f.RecordId}: {string.Join("; ", f.Result.Errors.Select(e => $"{e.Code} {e.Message}"))}")
                .ToList();

            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }
    }

    [Fact]
    public async Task EverySeededRecord_NamesASourceAndClaimsNoVerification()
    {
        var harness = new SeedHarness();
        await harness.SeedEverythingAsync();

        var provenances = new List<(string Library, string RecordId, ReferenceProvenance Provenance, ReferenceValidationState State)>();

        foreach (var record in await harness.Materials.ListAsync())
            provenances.Add(("Materials", record.Id, record.Provenance, record.ValidationState));
        foreach (var record in await harness.Standards.ListAsync())
            provenances.Add(("Standards", record.Id, record.Provenance, record.ValidationState));
        foreach (var record in await harness.Constants.ListAsync())
            provenances.Add(("Constants", record.Id, record.Provenance, record.ValidationState));
        foreach (var record in await harness.Fasteners.ListAsync())
            provenances.Add(("Fasteners", record.Id, record.Provenance, record.ValidationState));
        foreach (var record in await harness.Bearings.ListAsync())
            provenances.Add(("Bearings", record.Id, record.Provenance, record.ValidationState));
        foreach (var record in await harness.Processes.ListAsync())
            provenances.Add(("Processes", record.Id, record.Provenance, record.ValidationState));
        foreach (var record in await harness.Rules.ListAsync())
            provenances.Add(("Rules", record.Id, record.Provenance, record.ValidationState));
        foreach (var record in await harness.Suppliers.ListAsync())
            provenances.Add(("Suppliers", record.Id, record.Provenance, record.ValidationState));
        foreach (var record in await harness.Costs.ListAsync())
            provenances.Add(("Costs", record.Id, record.Provenance, record.ValidationState));
        foreach (var record in await harness.LeadTimes.ListAsync())
            provenances.Add(("LeadTimes", record.Id, record.Provenance, record.ValidationState));

        Assert.NotEmpty(provenances);

        foreach (var (library, recordId, provenance, state) in provenances)
        {
            Assert.True(provenance.IdentifiesASource, $"{library}/{recordId} names no source.");

            // Being imported is not being verified, and the whole
            // credibility of the corpus rests on that staying true.
            Assert.False(provenance.IsVerified, $"{library}/{recordId} claims verification nobody performed.");
            Assert.Equal(ReferenceVerificationStatus.NotVerified, provenance.VerificationStatus);
            Assert.Null(provenance.ReviewerPrincipalId);
            Assert.Null(provenance.VerificationDate);
            Assert.Equal(ReferenceValidationState.Draft, state);
        }
    }

    [Fact]
    public async Task NoSeededRecord_CanBeReleasedWithoutAPersonVerifyingIt()
    {
        // Not a test of the seed so much as a test that the seed cannot
        // route around the lifecycle: a populated library must not be able
        // to present itself as an authoritative one.
        var harness = new SeedHarness();
        await harness.SeedEverythingAsync();

        await harness.Materials.SetValidationStateAsync(MaterialSeed.S355J2, ReferenceValidationState.Checked, "Checked against the cited source.");
        await harness.Materials.SetValidationStateAsync(MaterialSeed.S355J2, ReferenceValidationState.Validated, "Passed the library's data-quality rules.");

        var refused = await Assert.ThrowsAsync<ReferenceProvenanceIncompleteException>(
            () => harness.Materials.SetValidationStateAsync(MaterialSeed.S355J2, ReferenceValidationState.Released, "Attempted release."));

        Assert.Contains("verified", refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AKnownMaterial_ComesBackWithItsUnitsAndConditionsIntact()
    {
        var harness = new SeedHarness();
        await harness.SeedEverythingAsync();

        var record = await harness.Materials.FindAsync(MaterialSeed.S355J2);
        Assert.NotNull(record);

        var yield = record.Definition.Properties[MaterialPropertyNames.YieldStrength];

        // The stored value is still 355 MPa, not 355 of something else and
        // not 355 000 000 silently normalised into pascals on the way in.
        var quantity = Assert.IsType<Quantity<Pressure>>(yield.Value);
        Assert.Equal(355.0, quantity.Value, 6);
        Assert.Equal("MPa", quantity.Unit.Symbol);
        Assert.Equal(355e6, yield.CanonicalValue, 0);

        // And it still knows it is a minimum for one thickness band, which
        // is the difference between a usable figure and a misleading one.
        Assert.Contains("t <= 16 mm", yield.Conditions);
        Assert.Equal(ReferenceValueOrigin.Standard, yield.Origin);

        var impact = record.Definition.Properties[MaterialPropertyNames.ImpactEnergy];
        Assert.Contains("-20 degC", impact.Conditions);
    }

    [Fact]
    public async Task TheAluminiumRecordWithABadPublishedDensity_HasNoDensityAtAll()
    {
        // The source prints 265 g/cm3. Recording it would be false;
        // correcting it to 2.65 would be publishing a figure no source
        // states. The record omits it, and this test stops a later tidy-up
        // from quietly filling the gap in.
        var harness = new SeedHarness();
        await harness.SeedEverythingAsync();

        var record = await harness.Materials.FindAsync(MaterialSeed.Aluminium5083OH111);
        Assert.NotNull(record);

        Assert.False(record.Definition.Properties.ContainsKey(MaterialPropertyNames.Density));
        Assert.Contains("DENSITY DELIBERATELY NOT RECORDED", record.Definition.Notes);
    }

    [Fact]
    public async Task AnExactConstantIsExact_AndAMeasuredOneCarriesItsUncertainty()
    {
        var harness = new SeedHarness();
        await harness.SeedEverythingAsync();

        var lightSpeed = await harness.Constants.FindAsync("const-speed-of-light");
        Assert.NotNull(lightSpeed);
        Assert.Equal(ConstantUncertaintyKind.Exact, lightSpeed.Definition.Uncertainty.Kind);
        Assert.Null(lightSpeed.Definition.Uncertainty.Absolute);

        var electronMass = await harness.Constants.FindAsync("const-electron-mass");
        Assert.NotNull(electronMass);
        Assert.Equal(ConstantUncertaintyKind.Standard, electronMass.Definition.Uncertainty.Kind);

        var uncertainty = Assert.IsType<Quantity<Mass>>(electronMass.Definition.Uncertainty.Absolute!.Value);
        Assert.Equal(2.8e-40, uncertainty.Value, 15);

        // Exact and "no uncertainty recorded" are different facts, and the
        // model keeps them different.
        Assert.NotEqual(ConstantUncertaintyKind.NotRecorded, lightSpeed.Definition.Uncertainty.Kind);
    }

    [Fact]
    public async Task TheFastenerRecords_AdmitTheyCarryNoStrength()
    {
        // ISO 898-1 was not obtainable. A consumer must be able to detect
        // that rather than read a zero or a default.
        var harness = new SeedHarness();
        await harness.SeedEverythingAsync();

        var fasteners = await harness.Fasteners.ListAsync();
        Assert.NotEmpty(fasteners);

        Assert.All(fasteners, record =>
        {
            Assert.False(record.Definition.Mechanical.IsRecorded);
            Assert.Null(record.Definition.Mechanical.PropertyClass);
            Assert.Null(record.Definition.Mechanical.ProofStrength);
        });

        var m8 = await harness.Fasteners.FindAsync("fst-m8-coarse");
        Assert.NotNull(m8);
        var pitch = m8.Definition.Thread!.Pitch!.Value;
        Assert.Equal(1.25, pitch.Value, 6);
        Assert.Equal("mm", pitch.Unit.Symbol);
    }

    [Fact]
    public async Task NoStandardRepeatsItsOwnBodyCodeInItsDesignation()
    {
        // Found by the integration pass, not by population: every indexed
        // standard was rendering as "EN EN 10025-2:2019", because the seed
        // wrote the body prefix into Designation and FullDesignation adds
        // it again. The model's contract is clear — Designation is the
        // number, Body.Code is the prefix — so the data was wrong, and this
        // test stops it drifting back.
        var harness = new SeedHarness();
        await harness.SeedEverythingAsync();

        foreach (var record in await harness.Standards.ListAsync())
        {
            var definition = record.Definition;

            Assert.DoesNotContain(
                $"{definition.Body.Code} {definition.Body.Code} ",
                definition.FullDesignation,
                StringComparison.Ordinal);

            Assert.False(
                definition.Designation.StartsWith(definition.Body.Code + " ", StringComparison.OrdinalIgnoreCase),
                $"{record.Id} writes the body code into its own designation: '{definition.Designation}'.");
        }

        var iso15 = await harness.Standards.FindAsync(StandardSeed.Iso15);
        Assert.Equal("ISO 15:2017", iso15!.Definition.FullDesignation);
    }
}
