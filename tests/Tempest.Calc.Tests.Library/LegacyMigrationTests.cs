using System.Text.Json;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.EngineeringData;
using Tempest.Core.Evidence;
using Tempest.Core.Identity;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Calc.Tests.Library;

/// <summary>ADR-0167: every stored legacy calculation record becomes a readable archive worksheet; nothing is lost and nothing is deleted.</summary>
public sealed class LegacyMigrationTests
{
    private static (LibraryTestHost Host, CalculationEngine Engine) NewHost()
    {
        var host = new LibraryTestHost();
        var engine = new CalculationEngine(new EngineeringDocumentStore(host.Backing, new CurrentPrincipalAccessor()), new CurrentPrincipalAccessor(), recordIndex: host.Backing);
        engine.RegisterDefinition(new BoltedJointPreloadCalculationDefinition());
        return (host, engine);
    }

    private static BoltedJointPreloadInput Example1() => new(
        null, "ISO 898-1 class 8.8",
        new Quantity<Force>(37_935, ForceUnits.Newton), new Quantity<Force>(20, ForceUnits.Kilonewton),
        new Quantity<Stiffness>(200, StiffnessUnits.KilonewtonPerMillimetre), new Quantity<Stiffness>(600, StiffnessUnits.KilonewtonPerMillimetre),
        new Quantity<Area>(84.3, AreaUnits.SquareMillimetre), new Quantity<Pressure>(600, PressureUnits.Megapascal));

    [Fact]
    public async Task A_Legacy_Record_Migrates_To_An_Archive_Instance_With_Every_Figure()
    {
        var (host, engine) = NewHost();
        var record = await engine.ExecuteAsync<BoltedJointPreloadInput, BoltedJointPreloadResult>(BoltedJointPreloadCalculationDefinition.Id, Example1());

        var report = await new LegacyCalculationMigration(engine, host.Store).MigrateAsync();
        Assert.Equal(1, report.Migrated);
        Assert.Empty(report.Failed);

        var instance = (await host.Store.FindInstanceAsync(record.Id))!;
        Assert.True(instance.IsLegacyArchive);
        Assert.Equal(CalcInstanceStatus.Draft, instance.Status);
        Assert.StartsWith("legacy/calc.bolted-joint-preload@", instance.TemplateReference, StringComparison.Ordinal);

        var preload = instance.Inputs.Single(i => i.Symbol == "Preload");
        Assert.Equal(37_935, preload.Number);
        Assert.Equal("N", preload.Unit);
        Assert.Equal(CalcProvenanceKind.Manual, preload.Provenance?.Kind);
        Assert.Equal("ISO 898-1 class 8.8", instance.Inputs.Single(i => i.Symbol == "FastenerGrade").Text);

        var boltLoad = instance.Outputs.First(o => o.Symbol == "Bolt load");
        Assert.Equal(42_935, boltLoad.Si!.Value, 3);
        Assert.Contains(instance.Outputs, o => o.Symbol == "JointConstant" && Math.Abs(o.Si!.Value - 0.25) < 1e-9);
        // Seven input guards and two engineering checks, every one recorded by the legacy module and every one satisfied.
        Assert.Equal(9, instance.Checks.Count);
        Assert.Contains(instance.Checks, c => c.Name == "The external load must not separate the joint." && c.Passed);
        Assert.All(instance.Checks, c => Assert.True(c.Passed));
        Assert.Contains("Migrated from calculation record", instance.Notes![0], StringComparison.Ordinal);
        Assert.Contains(instance.Notes, n => n.StartsWith("Assumption: The bolt and the clamped members", StringComparison.Ordinal));

        // The legacy record is still there, untouched.
        Assert.NotNull(await engine.FindRecordAsync<BoltedJointPreloadResult>(record.Id));
    }

    [Fact]
    public async Task A_Second_Pass_Writes_Nothing()
    {
        var (host, engine) = NewHost();
        await engine.ExecuteAsync<BoltedJointPreloadInput, BoltedJointPreloadResult>(BoltedJointPreloadCalculationDefinition.Id, Example1());
        var migration = new LegacyCalculationMigration(engine, host.Store);
        Assert.Equal(1, (await migration.MigrateAsync()).Migrated);
        var second = await migration.MigrateAsync();
        Assert.Equal(0, second.Migrated);
        Assert.Equal(1, second.AlreadyMigrated);
        Assert.Single(await host.Store.ListInstancesAsync());
    }

    [Fact]
    public async Task An_Archive_Renders_As_A_Sheet_And_Refuses_Every_Lifecycle_Action()
    {
        var (host, engine) = NewHost();
        var record = await engine.ExecuteAsync<BoltedJointPreloadInput, BoltedJointPreloadResult>(BoltedJointPreloadCalculationDefinition.Id, Example1());
        await new LegacyCalculationMigration(engine, host.Store).MigrateAsync();
        var instance = (await host.Store.FindInstanceAsync(record.Id))!;

        var run = await host.Service.RunAsync(instance);
        Assert.Contains("Archive of a legacy calculation record", run.SheetText, StringComparison.Ordinal);
        Assert.Contains("Preload | ", run.SheetText, StringComparison.Ordinal);
        Assert.Contains("Bolt load = 42935 N", run.SheetText, StringComparison.Ordinal);
        Assert.Contains("PASS  The external load must not separate the joint.", run.SheetText, StringComparison.Ordinal);
        Assert.Contains("A1. Migrated from calculation record", run.SheetText, StringComparison.Ordinal);
        Assert.Contains("WARNING legacy.archive", run.SheetText, StringComparison.Ordinal);
        Assert.Empty(run.Result.Steps);

        foreach (var reason in new[]
        {
            (await host.Service.UpdateInputsAsync(instance.Id, LibraryTestHost.ProvenancedInputs())).Reason,
            (await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Reason,
            (await host.Service.IssueAsync(instance.Id, "ISS-L", "Client Co")).Reason,
            (await host.Service.ReviseAsync(instance.Id)).Reason,
        })
        {
            Assert.Contains("migrated legacy record", reason, StringComparison.Ordinal);
        }

        Assert.Equal(CalcInstanceStatus.Draft, (await host.Store.FindInstanceAsync(instance.Id))!.Status);
    }

    [Fact]
    public void Mapping_Reads_Quantities_Numbers_Flags_And_Text_And_Keeps_Unknown_Shapes_As_Text()
    {
        var input = JsonSerializer.SerializeToElement(new
        {
            Load = new { Value = 12.5, Unit = new { Symbol = "kN", ToBaseUnitFactor = 1000.0 } },
            Count = 3,
            Hollow = true,
            Grade = "S275",
            Points = new[] { 1, 2, 3 },
            Nothing = (string?)null,
        });
        var result = JsonSerializer.SerializeToElement(new { Stress = new { Value = 2.5, Unit = "MPa" }, Ratio = 0.5, Passed = false });
        var record = new CalculationRecordDocument(
            Guid.NewGuid(), "calc.test", result, "T", [new CalculationAssumption("A", "because")],
            [new CalculationIntermediateResult("Area", 42.0)], new CalculationValidationResult(CalculationValidationOutcome.Valid, [new CalculationConstraintCheck("positive", true, "12.5 > 0")]),
            ["mat-1"], new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), "steven", 2, input, "T.Input", null);

        var instance = LegacyCalculationMigration.ToInstance(record, DateTimeOffset.UnixEpoch);

        Assert.Equal(record.Id, instance.Id);
        var load = instance.Inputs.Single(i => i.Symbol == "Load");
        Assert.Equal(12.5, load.Number);
        Assert.Equal("kN", load.Unit);
        Assert.Equal(3, instance.Inputs.Single(i => i.Symbol == "Count").Number);
        Assert.True(instance.Inputs.Single(i => i.Symbol == "Hollow").Flag);
        Assert.Equal("S275", instance.Inputs.Single(i => i.Symbol == "Grade").Text);
        Assert.Equal("[1,2,3]", instance.Inputs.Single(i => i.Symbol == "Points").Text);
        Assert.Equal(string.Empty, instance.Inputs.Single(i => i.Symbol == "Nothing").Text);
        Assert.Equal("2.5 MPa", instance.Outputs.Single(o => o.Symbol == "Stress").Text);
        Assert.Equal("42", instance.Outputs.Single(o => o.Symbol == "Area").Text);
        Assert.Equal("no", instance.Outputs.Single(o => o.Symbol == "Passed").Text);
        Assert.Single(instance.Checks);
        Assert.Contains("Referenced materials: mat-1.", instance.Notes!);
        Assert.Equal("steven", instance.CreatedBy);
        Assert.Equal(record.ExecutedAt, instance.CreatedAt);
    }
}
