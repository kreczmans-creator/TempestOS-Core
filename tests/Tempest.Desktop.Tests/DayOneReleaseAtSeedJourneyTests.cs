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

            // Runbook C8: beam bending with S355J2 — E and fy come from the record.
            var outcome = await host.CalculationModuleService!.RunAsync(new CalculationModuleRequest(
                BeamDeflectionCalculationDefinition.Id,
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
            Assert.Contains(outcome.Run.Results, r => r.Label == "Maximum deflection" && r.Display == "3.96825 mm");

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
