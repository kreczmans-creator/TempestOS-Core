using Tempest.Core.Bearings;
using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.Constants;
using Tempest.Core.Fasteners;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Seeding;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.Standards;
using static Tempest.Core.Tests.Calculations.Modules.ModuleTestSupport;

namespace Tempest.Core.Tests.Population;

/// <summary>
/// PO decision 2026-10-01: the shipped reference libraries are populated
/// from recognised sources and released for day-one use — a hard block
/// against v1.0.0. These tests pin what that means: the counts, that every
/// seeded record passes its own library's validation with no error, that
/// every material carries the properties the calculators read, that seeding
/// releases through the governed review path, that reseeding is idempotent
/// and never touches a person's record, and that a calculation runs on a
/// seeded record with nobody releasing it by hand.
/// </summary>
public sealed class DayOneReferenceLibraryTests
{
    // The shipped counts. A change here is a change to the shipped corpus and
    // must be matched by the Seed Data Sources Register.
    private const int ExpectedStandards = 29;
    private const int ExpectedMaterials = 77;
    private const int ExpectedConstants = 12;
    private const int ExpectedFasteners = 7 + (17 * 5) + (13 * 3);
    private const int ExpectedBearings = 39;

    /// <summary>
    /// Materials whose sources publish no value for one of the
    /// calculator-consumed properties, each with the property it lacks. The
    /// calculators refuse these records for that property rather than use a
    /// borrowed figure; every entry is recorded in the record's own notes and
    /// in the Seed Data Sources Register.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> DocumentedGaps = new Dictionary<string, string[]>
    {
        [MaterialSeed.Steel11SMn30] = [MaterialPropertyNames.ThermalExpansionCoefficient],
        [MaterialSeed.PolymerAbs] = [MaterialPropertyNames.ThermalExpansionCoefficient],
        [MaterialSeed.PolymerPtfe] = [MaterialPropertyNames.YoungsModulus, MaterialPropertyNames.YieldStrength],
        [MaterialSeed.PolymerPmma] = [MaterialPropertyNames.YieldStrength],
        [MaterialSeed.PolymerUhmwPe] = [MaterialPropertyNames.UltimateTensileStrength],
        [MaterialSeed.PolymerHdpe] = [MaterialPropertyNames.UltimateTensileStrength],
        [MaterialSeed.PolymerPp] = [MaterialPropertyNames.UltimateTensileStrength],
        [MaterialSeed.PolymerPvcU] = [MaterialPropertyNames.UltimateTensileStrength],
    };

    /// <summary>The properties the calculation modules and the bracket check read from a material record.</summary>
    private static readonly string[] CalculatorConsumed =
    [
        MaterialPropertyNames.Density,
        MaterialPropertyNames.YoungsModulus,
        MaterialPropertyNames.YieldStrength,
        MaterialPropertyNames.UltimateTensileStrength,
        MaterialPropertyNames.ThermalExpansionCoefficient,
    ];

    private static SeedHarness NewHarness() => new();

    private static ReferenceSeedService Releasing() => new(releasePolicy: new ReferenceSeedReleasePolicy());

    private static async Task SeedAllAsync(SeedHarness harness, ReferenceSeedService seeder)
    {
        await seeder.ApplyAsync(harness.Standards, StandardSeed.Instance);
        await seeder.ApplyAsync(harness.Materials, MaterialSeed.Instance);
        await seeder.ApplyAsync(harness.Constants, ConstantSeed.Instance);
        await seeder.ApplyAsync(harness.Fasteners, FastenerSeed.Instance);
        await seeder.ApplyAsync(harness.Bearings, BearingSeed.Instance);
    }

    [Fact]
    public void TheShippedDatasets_HoldTheDayOneCounts_WithUniqueIdentities()
    {
        Assert.Equal(ExpectedStandards, StandardSeed.Instance.Records.Count);
        Assert.Equal(ExpectedMaterials, MaterialSeed.Instance.Records.Count);
        Assert.Equal(ExpectedConstants, ConstantSeed.Instance.Records.Count);
        Assert.Equal(ExpectedFasteners, FastenerSeed.Instance.Records.Count);
        Assert.Equal(ExpectedBearings, BearingSeed.Instance.Records.Count);

        AssertUnique(StandardSeed.Instance.Records.Select(r => r.RecordId));
        AssertUnique(MaterialSeed.Instance.Records.Select(r => r.RecordId));
        AssertUnique(FastenerSeed.Instance.Records.Select(r => r.RecordId));
        AssertUnique(BearingSeed.Instance.Records.Select(r => r.RecordId));
        AssertUnique(MaterialSeed.Instance.Records.Select(r => r.Definition.DesignationKey!));
    }

    private static void AssertUnique(IEnumerable<string> values)
    {
        var duplicates = values.GroupBy(v => v, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(duplicates.Count == 0, "Duplicated: " + string.Join(", ", duplicates));
    }

    [Fact]
    public void EveryShippedRecord_NamesASourceAndADocument_AndIsNotYetVerified()
    {
        AssertSourced(StandardSeed.Instance.Records);
        AssertSourced(MaterialSeed.Instance.Records);
        AssertSourced(ConstantSeed.Instance.Records);
        AssertSourced(FastenerSeed.Instance.Records);
        AssertSourced(BearingSeed.Instance.Records);
    }

    private static void AssertSourced<TDefinition>(IReadOnlyList<ReferenceSeedRecord<TDefinition>> records)
    {
        Assert.All(records, r =>
        {
            Assert.True(r.Provenance.IdentifiesASource, $"{r.RecordId} names no source.");
            Assert.Equal(ReferenceVerificationStatus.NotVerified, r.Provenance.VerificationStatus);
            Assert.NotNull(r.Source);
        });
    }

    [Fact]
    public void EveryMaterial_CarriesEveryCalculatorConsumedProperty_ExceptItsDocumentedGaps()
    {
        var missing = new List<string>();

        foreach (var record in MaterialSeed.Instance.Records)
        {
            DocumentedGaps.TryGetValue(record.RecordId, out var allowed);

            foreach (var property in CalculatorConsumed)
            {
                var present = record.Definition.Properties.ContainsKey(property);
                var excused = allowed?.Contains(property) == true;

                if (!present && !excused)
                    missing.Add($"{record.RecordId}: {property}");

                // An excused gap that has since been filled must be removed from the list.
                if (present && excused)
                    missing.Add($"{record.RecordId}: {property} is present but still listed as a documented gap");
            }

            if (allowed is not null)
                Assert.False(string.IsNullOrWhiteSpace(record.Definition.Notes), $"{record.RecordId} must explain its gap.");
        }

        Assert.True(missing.Count == 0, "Missing calculator-consumed properties:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void EveryMaterialValueFromASecondDocument_NamesThatDocument()
    {
        foreach (var record in MaterialSeed.Instance.Records)
        {
            foreach (var (name, value) in record.Definition.Properties)
            {
                Assert.False(string.IsNullOrWhiteSpace(value.Conditions), $"{record.RecordId}.{name} states no conditions.");
                Assert.NotEqual(ReferenceValueOrigin.Unknown, value.Origin);
                Assert.NotEqual(ReferenceValueOrigin.DerivedByTempestOS, value.Origin);

                if (value.Conditions!.StartsWith("SUPPLEMENTARY SOURCE:", StringComparison.Ordinal))
                    Assert.True(value.Conditions.Contains("http", StringComparison.Ordinal),
                        $"{record.RecordId}.{name} cites a supplementary source without its address.");
            }
        }
    }

    [Fact]
    public async Task EverySeededRecord_PassesItsOwnLibrarysValidation_WithNoError()
    {
        var harness = NewHarness();
        await SeedAllAsync(harness, Releasing());

        var reports = new[]
        {
            await new StandardValidationService(harness.Standards).ValidateLibraryAsync(),
            await new MaterialValidationService(harness.Materials, harness.Standards).ValidateLibraryAsync(),
            await new ConstantValidationService(harness.Constants, harness.Standards).ValidateLibraryAsync(),
            await new FastenerValidationService(harness.Fasteners, harness.Materials, harness.Standards).ValidateLibraryAsync(),
            await new BearingValidationService(harness.Bearings, harness.Materials, harness.Standards).ValidateLibraryAsync(),
        };

        var errors = reports
            .SelectMany(report => report.Findings
                .Where(f => f.Result.Errors.Count > 0)
                .Select(f => $"{report.Library}/{f.RecordId}: {string.Join("; ", f.Result.Errors.Select(e => $"{e.Code} {e.Message}"))}"))
            .ToList();

        Assert.True(errors.Count == 0, string.Join("\n", errors));

        // And no standard a record cites is missing from the index.
        var unresolved = reports
            .SelectMany(report => report.Findings
                .SelectMany(f => f.Result.Warnings
                    .Where(w => w.Code == ReferenceValidationRules.StandardReferenceUnresolved)
                    .Select(w => $"{report.Library}/{f.RecordId}: {w.Message}")))
            .ToList();

        Assert.True(unresolved.Count == 0, string.Join("\n", unresolved));
    }

    [Fact]
    public async Task SeedingWithTheReleasePolicy_ReleasesEveryRecord_ThroughTheReviewPath_AsTheNamedSeedPrincipal()
    {
        var harness = NewHarness();
        await SeedAllAsync(harness, Releasing());

        AssertReleasedAtSeed(await harness.Standards.ListAsync(), ExpectedStandards);
        AssertReleasedAtSeed(await harness.Materials.ListAsync(), ExpectedMaterials);
        AssertReleasedAtSeed(await harness.Constants.ListAsync(), ExpectedConstants);
        AssertReleasedAtSeed(await harness.Fasteners.ListAsync(), ExpectedFasteners);
        AssertReleasedAtSeed(await harness.Bearings.ListAsync(), ExpectedBearings);

        // The release went Draft -> Checked -> Validated -> Released, revision by revision.
        var history = await harness.Materials.GetHistoryAsync(MaterialSeed.S355J2);
        Assert.True(history.Count >= 4, $"Expected the governed walk through the lifecycle, found {history.Count} revisions.");
    }

    private static void AssertReleasedAtSeed<TDefinition>(IReadOnlyList<IReferenceRecord<TDefinition>> records, int expected)
        where TDefinition : class
    {
        Assert.Equal(expected, records.Count);
        Assert.All(records, r =>
        {
            Assert.Equal(ReferenceValidationState.Released, r.ValidationState);
            Assert.True(r.Provenance.IsVerified);
            Assert.Equal(ReferenceSeedReleasePolicy.SeedPrincipalId, r.Provenance.ReviewerPrincipalId);
            Assert.Contains("released at seed for day-one use (PO decision 2026-10-01)", r.Provenance.Notes, StringComparison.Ordinal);
            Assert.Contains("verify against the primary standard before issue", r.Provenance.Notes, StringComparison.Ordinal);
            Assert.Contains("not against the primary standard itself", r.Provenance.Notes, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task SeedingWithoutTheReleasePolicy_StillLandsEveryRecordDraft()
    {
        var harness = NewHarness();
        var outcome = await new ReferenceSeedService().ApplyAsync(harness.Materials, MaterialSeed.Instance);

        Assert.Equal(ExpectedMaterials, outcome.RegisteredCount);
        Assert.Equal(0, outcome.ReleasedCount);
        Assert.All(await harness.Materials.ListAsync(), r => Assert.Equal(ReferenceValidationState.Draft, r.ValidationState));
    }

    [Fact]
    public async Task ReseedingAnAlreadySeededLibrary_IsANoOp()
    {
        var harness = NewHarness();
        var seeder = Releasing();
        await SeedAllAsync(harness, seeder);

        var before = (await harness.Materials.ListAsync()).ToDictionary(r => r.Id, r => r.RevisionNumber);

        var second = await seeder.ApplyAsync(harness.Materials, MaterialSeed.Instance);

        Assert.True(second.MadeNoChange);
        Assert.Equal(ExpectedMaterials, second.AlreadyPresentCount);
        Assert.Equal(ExpectedMaterials, (await harness.Materials.ListAsync()).Count);
        Assert.All(await harness.Materials.ListAsync(), r => Assert.Equal(before[r.Id], r.RevisionNumber));
    }

    [Fact]
    public async Task ReseedingOverAnEarlierDraftSeed_RefreshesAndReleasesTheUntouchedRecords_ButNeverOneAPersonRevised()
    {
        var harness = NewHarness();

        // An installation seeded before the PO decision: everything Draft.
        await new ReferenceSeedService().ApplyAsync(harness.Standards, StandardSeed.Instance);
        await new ReferenceSeedService().ApplyAsync(harness.Materials, MaterialSeed.Instance);

        // A person corrected one record's notes since; that is their work.
        var touched = (await harness.Materials.FindAsync(MaterialSeed.Aluminium6082T6))!;
        await harness.Materials.ReviseAsync(
            touched.Id,
            touched.Definition with { Notes = "Checked against our own mill certificate." },
            touched.Provenance,
            "A person's own correction.");

        var outcome = await Releasing().ApplyAsync(harness.Materials, MaterialSeed.Instance);

        Assert.Equal(ExpectedMaterials - 1, outcome.RefreshedCount);
        Assert.Equal(1, outcome.AlreadyPresentCount);
        Assert.Equal(0, outcome.RegisteredCount);

        var personal = (await harness.Materials.FindAsync(MaterialSeed.Aluminium6082T6))!;
        Assert.Equal(ReferenceValidationState.Draft, personal.ValidationState);
        Assert.Equal("Checked against our own mill certificate.", personal.Definition.Notes);

        var refreshed = (await harness.Materials.FindAsync(MaterialSeed.S355J2))!;
        Assert.Equal(ReferenceValidationState.Released, refreshed.ValidationState);
        Assert.True(refreshed.Definition.Properties.ContainsKey(MaterialPropertyNames.PoissonsRatio));
    }

    [Fact]
    public async Task ATopUpNeverOverwritesARecordAPersonRegisteredUnderTheSameDesignation()
    {
        var harness = NewHarness();
        await new ReferenceSeedService().ApplyAsync(harness.Standards, StandardSeed.Instance);

        await harness.Materials.RegisterAsync(
            "mat-users-s275",
            new MaterialDefinition { Name = "Our S275JR", Family = MaterialFamily.Steel, Designation = "S275JR" },
            new ReferenceProvenance(SourceOrganisation: "Our test house", SourceDocument: "Report 42"));

        var outcome = await Releasing().ApplyAsync(harness.Materials, MaterialSeed.Instance);

        Assert.Equal(1, outcome.KeyConflictCount);
        Assert.Contains(outcome.Entries, e => e.RecordId == MaterialSeed.S275JR && e.Action == ReferenceSeedAction.KeyConflict);
        Assert.Null(await harness.Materials.FindAsync(MaterialSeed.S275JR));
        Assert.Equal("Our S275JR", (await harness.Materials.FindAsync("mat-users-s275"))!.Definition.Name);
    }

    [Fact]
    public async Task TheBeamCalculation_RunsOnTheSeededS355J2_WithNobodyReleasingItByHand()
    {
        var harness = NewHarness();
        await SeedAllAsync(harness, Releasing());

        var service = new CalculationModuleService(harness.Materials, harness.Fasteners, harness.Bearings, BareEngine());

        var outcome = await service.RunAsync(new CalculationModuleRequest(BeamDeflectionCalculationDefinition.Id,
        [
            new("MaterialPin", RecordId: MaterialSeed.S355J2),
            new("Support", Choice: nameof(BeamSupport.SimplySupported)),
            new("Loading", Choice: nameof(BeamLoading.PointLoad)),
            new("Load", "10", "kN"),
            new("Span", "2000", "mm"),
            new("SecondMomentOfArea", "2000000", "mm^4"),
            new("ExtremeFibreDistance", "50", "mm"),
            new("DeflectionLimit", "8", "mm"),
        ]));

        Assert.True(outcome.WasPerformed, outcome.Reason);
        Assert.Contains(MaterialSeed.S355J2, outcome.Run!.ReferencedMaterialIds);

        // E 210 GPa and fy 355 MPa came from the record itself.
        Assert.Contains(outcome.Fills, f => f.ReferenceInputName == "MaterialPin" && f.Record.RecordId == MaterialSeed.S355J2);
        Assert.Contains(outcome.Run.Results, r => r.Label == "Maximum deflection" && r.Display == "3.96825 mm");
    }

    [Fact]
    public async Task TheBoltedJointCalculation_ReadsGradeStressAreaAndProofStrengthFromASeededFastener()
    {
        var harness = NewHarness();
        await SeedAllAsync(harness, Releasing());

        var service = new CalculationModuleService(harness.Materials, harness.Fasteners, harness.Bearings, BareEngine());
        var fastenerId = FastenerSeed.PropertyClassRecordId(12, "8.8");

        var options = await service.ListReleasedAsync(ReferenceLibrary.Fasteners);
        Assert.Contains(options, o => o.RecordId == fastenerId);

        var fastener = (await harness.Fasteners.FindAsync(fastenerId))!;
        Assert.Equal("8.8", fastener.Definition.Mechanical.PropertyClass);
        Assert.Equal(84.3e-6, fastener.Definition.Mechanical.StressArea!.CanonicalValue, 12);
        Assert.Equal(580e6, fastener.Definition.Mechanical.ProofStrength!.CanonicalValue, 3);
    }
}
