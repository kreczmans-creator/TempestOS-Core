using Avalonia.Headless.XUnit;
using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.Identity;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Seeding;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Desktop.Tests;

/// <summary>
/// PO decision 2026-10-01, through the real Desktop composition with its
/// default configuration: the shipped reference libraries are released at
/// seed, so a calculation runs on a seeded record with nobody releasing it by
/// hand — the physical-review runbook step C8 (beam bending with S355J2,
/// E 210 GPa, fy 355 MPa) that could not be run before.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public class DayOneReleaseAtSeedJourneyTests
{
    private const string EngineerId = "desktop-day-one-engineer";

    private static void SignIn(WorkspaceHost host)
    {
        var principalSession = (PrincipalSession)host.Services!.GetService(typeof(PrincipalSession));
        principalSession.Establish(
            new PlatformPrincipal(new PlatformIdentity(EngineerId, EngineerId), ApplicationPermissions.LocalSession));
    }

    [AvaloniaFact]
    public async Task OnAFreshInstall_SeededReferenceDataIsReleased_AndTheRunbookBeamOnS355J2Calculates()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            SignIn(host);

            // Fasteners had nothing of a person's own in them, so start-up
            // seeded them, and seeding released them.
            var bolt = await host.Fasteners!.FindAsync(FastenerSeed.PropertyClassRecordId(12, "8.8"));
            Assert.NotNull(bolt);
            Assert.Equal(ReferenceValidationState.Released, bolt.ValidationState);
            Assert.Equal(ReferenceSeedReleasePolicy.SeedPrincipalId, bolt.Provenance.ReviewerPrincipalId);

            // This test process also loads Tempest.Samples' demonstration
            // alloys, so Materials held records of its own at start-up and was
            // left alone; the Populate action tops it up — and releases too.
            var added = await host.BracketCalculations!.PopulateMaterialLibraryAsync();
            Assert.Equal(MaterialSeed.Instance.Records.Count, added);

            var steel = await host.Materials!.FindAsync(MaterialSeed.S355J2);
            Assert.NotNull(steel);
            Assert.Equal(ReferenceValidationState.Released, steel.ValidationState);
            Assert.Contains("released at seed for day-one use (PO decision 2026-10-01)", steel.Provenance.Notes);

            // Runbook C8: beam bending with S355J2 — E comes from the record. ADR-0167: superseded the legacy Calculators
            // run with the beam worksheet and its reference fill; the deflection is the same 3.96825 mm.
            var beam = host.CalcCatalogue!.Get(Tempest.Calc.Structural.BeamFormulasCalculator.Id);
            var fill = await new Tempest.Calc.Library.CalcReferenceFill(host.Materials!, host.Fasteners!, host.Bearings!)
                .FillAsync(beam.Template, "Materials", MaterialSeed.S355J2);
            Assert.Empty(fill.Problems);
            var modulus = Assert.Single(fill.Values, v => v.Symbol == "E");
            Assert.Equal(Tempest.Calc.Sheets.CalcProvenanceKind.Reference, modulus.Provenance!.Kind);
            Assert.Contains(MaterialSeed.S355J2, modulus.Provenance.Reference, StringComparison.Ordinal);

            var inputs = Tempest.Calc.Sheets.CalcTemplateHasher.DefaultInputs(beam.Template)
                .With(Tempest.Calc.Sheets.CalcInputValue.Choice("case", Tempest.Calc.Structural.BeamFormulasCalculator.SsPoint))
                .With(Tempest.Calc.Sheets.CalcInputValue.Quantity("L", 2000, "mm"))
                .With(Tempest.Calc.Sheets.CalcInputValue.Quantity("I", 2_000_000, "mm^4"))
                .With(Tempest.Calc.Sheets.CalcInputValue.Quantity("F", 10, "kN"))
                .With(Tempest.Calc.Sheets.CalcInputValue.Quantity("a", 1000, "mm"));

            foreach (var value in fill.Values)
                inputs = inputs.With(value);

            var run = host.CalcInstances!.Preview(beam.Reference, inputs);
            Assert.False(run.IsBlocked, string.Join(" ", run.Blocks.Select(b => b.Message)));
            Assert.Equal(3.96825e-3, run.OutputSi("delta_max"), 8);

            // And the bracket check on 6082-T6 runs without a manual release.
            var check = await host.BracketCheck!.CheckAsync(new GovernedBracketCheckRequest(
                MaterialSeed.Aluminium6082T6,
                new Quantity<Force>(12.0, ForceUnits.Kilonewton),
                new Quantity<Area>(60.0, AreaUnits.SquareMillimetre),
                new Quantity<Length>(150.0, LengthUnits.Millimetre),
                new Quantity<Mass>(50.0, MassUnits.Gram)));

            Assert.True(check.WasPerformed, check.Reason);
            Assert.Equal(0.30, check.Result!.StressMargin, 1e-9);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
