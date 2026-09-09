using Tempest.Workspace.Engineering;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringAssets.CalculationPacks;
using Tempest.Core.EngineeringAssets.Templates;
using Tempest.Core.Materials;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Seeding;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.Runtime;
using Tempest.Core.Standards;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.UnitsAndQuantities;

using Tempest.Core.Tests.Runtime;
namespace Tempest.Core.Tests.Integration;

// Persistence, revision reproduction and traceability, exercised through
// the real host against the real file-backed store — not against an
// in-memory catalogue a test built for itself. An engineering platform that
// only proves its objects were right in memory has proved nothing about
// whether an engineer can come back to them next year.
public class PersistenceAndTraceabilityTests
{
    private static async Task RunAgainstRunningHostAsync(string rootPath, Func<ITempestHost, Task> body)
    {
        var host = new TempestHostBuilder(Type.EmptyTypes)
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();


        var runTask = host.RunAsync();

        await RunningHostFixture.WaitUntilRunningAsync(host);

        await body(host);

        await host.StopAsync();
        await runTask;
    }

    private static async Task SeedScenarioAsync(ITempestHost host)
    {
        var seeder = (ReferenceSeedService)host.Services!.GetService(typeof(ReferenceSeedService))!;
        var standards = (IStandardCatalog)host.Services!.GetService(typeof(IStandardCatalog))!;
        var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog))!;
        var templates = (ITemplateCatalog)host.Services!.GetService(typeof(ITemplateCatalog))!;
        var packs = (ICalculationPackCatalog)host.Services!.GetService(typeof(ICalculationPackCatalog))!;

        await seeder.ApplyAsync(standards, StandardSeed.Instance);
        await seeder.ApplyAsync(materials, MaterialSeed.Instance);
        await seeder.ApplyAsync(templates, EngineeringAssetSeed.Templates);

        var assets = await EngineeringAssetSeed.CreateAsync(materials, templates);
        await seeder.ApplyAsync(packs, assets.CalculationPacks);
    }

    private static EngineeringTraceRegister TraceRegister(ITempestHost host) =>
        new(
            (ICalculationPackCatalog)host.Services!.GetService(typeof(ICalculationPackCatalog))!,
            (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog))!,
            (ITemplateCatalog)host.Services!.GetService(typeof(ITemplateCatalog))!);

    // ---- Section 14: create -> persist -> reload -> inspect ----

    [Fact]
    public async Task EverythingSurvivesAHostRestart_NotJustTheIdentifiers()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, SeedScenarioAsync);

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog))!;
            var packs = (ICalculationPackCatalog)host.Services!.GetService(typeof(ICalculationPackCatalog))!;

            var material = await materials.FindAsync(MaterialSeed.Aluminium6082T6);
            Assert.NotNull(material);

            // Identifier.
            Assert.Equal(MaterialSeed.Aluminium6082T6, material.Id);

            // Quantity and unit, unconverted.
            var yield = material.Definition.Properties[MaterialPropertyNames.YieldStrength];
            var quantity = Assert.IsType<Quantity<Pressure>>(yield.Value);
            Assert.Equal(260.0, quantity.Value, 6);
            Assert.Equal("MPa", quantity.Unit.Symbol);

            // The conditions that make the number mean anything.
            Assert.Contains("20 mm to 150 mm", yield.Conditions);
            Assert.Equal(ReferenceValueOrigin.Standard, yield.Origin);

            // Provenance.
            Assert.Equal("Aalco Metals Limited", material.Provenance.SourceOrganisation);
            Assert.Equal(ReferenceVerificationStatus.NotVerified, material.Provenance.VerificationStatus);

            // Lifecycle state.
            Assert.Equal(ReferenceValidationState.Draft, material.ValidationState);

            // Cross-record references.
            var citation = Assert.Single(material.Definition.Standards, s => s.Designation == "EN 755-2");
            Assert.Equal(StandardSeed.En755Part2, citation.StandardId);

            // Nested objects, and the reference pin inside one of them.
            var pack = await packs.FindAsync(EngineeringAssetSeed.CalculationPackRecordId);
            var input = Assert.Single(pack!.Definition.Inputs, i => i.SourcePin is not null);
            Assert.Equal(MaterialSeed.Aluminium6082T6, input.SourcePin!.RecordId);
            Assert.NotEmpty(pack.Definition.Assumptions);
            Assert.NotEmpty(pack.Definition.Method.GoverningEquations);

            // Revision.
            Assert.True(material.RevisionNumber >= 1);
        });
    }

    // ---- Section 13: revision reproduction ----

    [Fact]
    public async Task ChangingAReferenceDoesNotChangeHistoricalEngineeringWork()
    {
        using var temp = new TempDirectory();

        int pinnedRevision = 0;
        double pinnedYield = 0;

        // 1. Use reference revision N, and 2. record an artefact against it.
        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            await SeedScenarioAsync(host);

            var trace = await TraceRegister(host).TraceCalculationAsync(EngineeringAssetSeed.CalculationPackRecordId);
            var reference = Assert.Single(trace!.AllReferences, r => r.RecordId == MaterialSeed.Aluminium6082T6);

            pinnedRevision = reference.PinnedRevision;
            Assert.True(reference.IsResolved);
            Assert.False(reference.HasMovedOnSincePinned);

            var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog))!;
            var record = await materials.FindAsync(MaterialSeed.Aluminium6082T6);
            pinnedYield = record!.Definition.Properties[MaterialPropertyNames.YieldStrength].CanonicalValue;
        });

        // 3. Create revision N+1 of the reference, in a later session.
        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog))!;
            var record = await materials.FindAsync(MaterialSeed.Aluminium6082T6);

            var corrected = record!.Definition.Properties.ToDictionary(p => p.Key, p => p.Value);
            corrected[MaterialPropertyNames.YieldStrength] = new ReferenceQuantityValue(
                new Quantity<Pressure>(240.0, PressureUnits.Megapascal),
                ReferenceValueOrigin.Standard,
                "Corrected after review against EN 755-2.",
                "0.2% Proof Stress min");

            await materials.ReviseAsync(
                MaterialSeed.Aluminium6082T6,
                record.Definition with { Properties = corrected },
                record.Provenance,
                "Proof stress corrected from 260 MPa to 240 MPa.");
        });

        // 4-5. Retrieve the old artefact and verify its reference is still N.
        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var trace = await TraceRegister(host).TraceCalculationAsync(EngineeringAssetSeed.CalculationPackRecordId);
            var reference = Assert.Single(trace!.AllReferences, r => r.RecordId == MaterialSeed.Aluminium6082T6);

            Assert.Equal(pinnedRevision, reference.PinnedRevision);
            Assert.True(reference.HasMovedOnSincePinned);
            Assert.True(reference.CurrentRevision > pinnedRevision);

            // And the pinned revision still holds what the calculation used,
            // read back out of the durable store after two restarts.
            var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog))!;
            var asUsed = await materials.GetRevisionAsync(MaterialSeed.Aluminium6082T6, pinnedRevision);

            Assert.Equal(pinnedYield, asUsed.Definition.Properties[MaterialPropertyNames.YieldStrength].CanonicalValue, 0);
            Assert.Equal(260e6, asUsed.Definition.Properties[MaterialPropertyNames.YieldStrength].CanonicalValue, 0);

            var current = await materials.FindAsync(MaterialSeed.Aluminium6082T6);
            Assert.Equal(240e6, current!.Definition.Properties[MaterialPropertyNames.YieldStrength].CanonicalValue, 0);
        });
    }

    // ---- Section 12: traceability ----

    [Fact]
    public async Task AnEngineeringResultTracesBackToItsSourceDocument()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            await SeedScenarioAsync(host);

            var trace = await TraceRegister(host).TraceCalculationAsync(EngineeringAssetSeed.CalculationPackRecordId);
            Assert.NotNull(trace);

            Assert.Equal("TDE-CPK-001", trace.PackReference);
            Assert.True(trace.IsFullyResolved);
            Assert.Empty(trace.DanglingReferences);

            // calculation -> input -> reference record -> revision -> source
            var material = Assert.Single(trace.AllReferences, r => r.RecordId == MaterialSeed.Aluminium6082T6);
            Assert.Equal("Aalco Metals Limited", material.SourceOrganisation);
            Assert.Contains("6082 - T6 Extrusions", material.SourceDocument);
            Assert.Contains("20 mm to 150 mm band", material.SourceLocation);

            // The citation reads as one line an engineer can put in a report.
            Assert.Contains("r1", material.Citation, StringComparison.Ordinal);
            Assert.Contains("Aalco Metals Limited", material.Citation, StringComparison.Ordinal);

            // The template the pack was recorded on is traced too.
            Assert.NotNull(trace.TemplateUsed);
            Assert.True(trace.TemplateUsed!.IsResolved);

            // And the honest part: this calculation does NOT rest entirely
            // on verified data, and two of its inputs rest on nothing at all.
            Assert.False(trace.RestsEntirelyOnVerifiedData);
            Assert.Equal(2, trace.UntraceableInputs.Count);
            Assert.All(trace.UntraceableInputs, i => Assert.Equal("not established", i.Value));
        });
    }

    [Fact]
    public async Task ADanglingReferenceIsReportedRatherThanFallingBackToTheCurrentRevision()
    {
        // If the pinned revision cannot be read, the trace must say so. The
        // dangerous alternative is quietly answering with today's values,
        // which looks like a successful trace and is a different fact.
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            await SeedScenarioAsync(host);

            var packs = (ICalculationPackCatalog)host.Services!.GetService(typeof(ICalculationPackCatalog))!;
            var pack = await packs.FindAsync(EngineeringAssetSeed.CalculationPackRecordId);

            // A pin at a revision that does not exist, and one naming a
            // library the register does not resolve into.
            var broken = pack!.Definition.Inputs
                .Select(i => i.SourcePin is null
                    ? i
                    : i with { SourcePin = new ReferencePin(i.SourcePin.Library, i.SourcePin.RecordId, 99) })
                .Append(new CalculationInput(
                    "IN-FOREIGN",
                    "A value from a library this register does not know",
                    "irrelevant",
                    SourcePin: new ReferencePin("Bearings", "brg-rhd-6205", 1)))
                .ToList();

            await packs.ReviseAsync(
                EngineeringAssetSeed.CalculationPackRecordId,
                pack.Definition with { Inputs = broken },
                pack.Provenance,
                "Deliberately broken pins, to prove the trace reports them.");

            var trace = await TraceRegister(host).TraceCalculationAsync(EngineeringAssetSeed.CalculationPackRecordId);

            Assert.False(trace!.IsFullyResolved);
            Assert.Equal(2, trace.DanglingReferences.Count);

            var missingRevision = trace.DanglingReferences.Single(r => r.RecordId == MaterialSeed.Aluminium6082T6);
            Assert.Contains("Revision 99", missingRevision.UnresolvedReason);
            Assert.Contains("UNRESOLVED", missingRevision.Citation, StringComparison.Ordinal);

            var foreignLibrary = trace.DanglingReferences.Single(r => r.Library == "Bearings");
            Assert.Contains("resolves pins into", foreignLibrary.UnresolvedReason);
        });
    }

    // ---- Section 11: the application read model over the real host ----

    [Fact]
    public async Task TheApplicationSeesThePopulatedLibrariesAndWhyTheyCannotYetBeUsed()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var register = new ReferenceLibraryRegister(
                (IStandardCatalog)host.Services!.GetService(typeof(IStandardCatalog))!,
                (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog))!,
                (Tempest.Core.Constants.IConstantCatalog)host.Services!.GetService(typeof(Tempest.Core.Constants.IConstantCatalog))!,
                (Tempest.Core.Fasteners.IFastenerCatalog)host.Services!.GetService(typeof(Tempest.Core.Fasteners.IFastenerCatalog))!,
                (Tempest.Core.Bearings.IBearingCatalog)host.Services!.GetService(typeof(Tempest.Core.Bearings.IBearingCatalog))!,
                (Tempest.Core.Manufacturing.IProcessCatalog)host.Services!.GetService(typeof(Tempest.Core.Manufacturing.IProcessCatalog))!);

            // Before seeding: genuinely empty, and it says empty.
            var before = await register.ListAsync();
            Assert.All(before, s => Assert.True(s.IsEmpty));
            Assert.All(before, s => Assert.False(s.IsPopulatedButUnusable));

            await SeedScenarioAsync(host);

            var after = await register.ListLibraryAsync("Materials");

            Assert.Equal(6, after.RecordCount);
            Assert.Equal(0, after.ReleasedCount);

            // The distinction the whole lifecycle exists to protect: this
            // library is full and unusable, which is not the same as empty.
            Assert.False(after.IsEmpty);
            Assert.True(after.IsPopulatedButUnusable);

            var entry = Assert.Single(after.Entries, e => e.RecordId == MaterialSeed.Aluminium6082T6);
            Assert.Equal("6082-T6 wrought aluminium alloy", entry.DisplayName);
            Assert.False(entry.IsUsableAsAuthoritative);
            Assert.False(entry.IsVerified);
            Assert.NotNull(entry.UnusableReason);
            Assert.Contains("nobody has checked its values", entry.UnusableReason);

            // A library name this build does not know answers "nothing"
            // rather than throwing at a surface that asked a fair question.
            var unknown = await register.ListLibraryAsync("Unicorns");
            Assert.True(unknown.IsEmpty);
        });
    }
}
