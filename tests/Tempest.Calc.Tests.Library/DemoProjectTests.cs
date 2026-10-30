using System.Text;
using Tempest.Calc.BuildingServices;
using Tempest.Calc.Electrical;
using Tempest.Calc.FluidPower;
using Tempest.Calc.Library;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Calc.Tests.Library.Chains;

namespace Tempest.Calc.Tests.Library;

/// <summary>What the demonstration tests build their hosts from: every calculator, every pack template, and the synthetic cable tables.</summary>
internal static class DemoHost
{
    public static LibraryTestHost New() => new(
        calculators: [.. ElectricalCalculators.All, .. MechanicalCalculators.All, .. BuildingServicesCalculators.All, .. StructuralCalculators.All, .. FluidPowerCalculators.All],
        packTemplates: CalcPackTemplates.All);

    public static CalcDemoProjectService Service(LibraryTestHost host, bool withTables = true) =>
        new(host.Service, host.Context, withTables ? [ConductorSizingCalculator.SyntheticAmpacity(), ConductorSizingCalculator.TypicalRatings()] : []);

    /// <summary>The check the demonstration fails on purpose: the control panel's enclosure cannot lose its heat naturally, which is why the cooling chain exists.</summary>
    public const string DeliberateFail = "Natural temperature rise against the allowed rise";

    public static async Task<IReadOnlyList<CalcInstance>> InstancesAsync(LibraryTestHost host, Guid projectId) => await host.Store.ListInstancesAsync(projectId);
}

/// <summary>The demonstration installed once, for the tests that only read it.</summary>
public sealed class InstalledDemo : IAsyncLifetime
{
    internal LibraryTestHost Host { get; private set; } = null!;

    internal CalcDemoProjectService Demo { get; private set; } = null!;

    public CalcDemoOutcome Outcome { get; private set; } = null!;

    public Guid ProjectId { get; private set; }

    public async Task InitializeAsync()
    {
        Host = DemoHost.New();
        Demo = DemoHost.Service(Host);
        Outcome = await Demo.InstallAsync();
        ProjectId = (await Demo.StatusAsync()).ProjectId ?? Guid.Empty;
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

/// <summary>
/// Addendum C, C-6: the seeded demonstration project "Demo — Hydraulic Press Cell". Installed and removed through the services a person uses; every built chain runs,
/// is current and can be checked; nothing licensed is in it; every sheet says DEMO; removing it changes nothing of any other project.
/// </summary>
public sealed class DemoProjectTests(InstalledDemo demo) : IClassFixture<InstalledDemo>
{
    private LibraryTestHost Host => demo.Host;

    private async Task<IReadOnlyList<CalcInstance>> InstancesAsync() => await DemoHost.InstancesAsync(Host, demo.ProjectId);

    // ---- install ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Install_Makes_The_Flagged_Project_Through_The_Services_And_Says_What_It_Made()
    {
        Assert.True(demo.Outcome.Succeeded, demo.Outcome.Message);
        Assert.NotEqual(Guid.Empty, demo.ProjectId);
        Assert.Contains("Demo — Hydraulic Press Cell", demo.Outcome.Message, StringComparison.Ordinal);

        var project = (Project)(await Host.Context.Repository.FindAsync(demo.ProjectId))!;
        Assert.Equal(CalcDemoProject.Identifier, project.Identifier);
        Assert.Equal(CalcDemoProject.DisplayName, project.DisplayName);
        Assert.True(CalcDemoProject.IsDemo(project));
        Assert.True(CalcDemoProject.IsTheDemonstration(project));
        Assert.Contains(CalcDemoProject.Tag, project.Tags);
        Assert.StartsWith("Demo —", project.DisplayName, StringComparison.Ordinal);
        Assert.Equal(CalcDemoProject.Story, project.Notes);

        // The message is one sentence naming the project and the counts it made.
        var instances = await InstancesAsync();
        Assert.Equal(instances.Count, demo.Outcome.Calculations);
        Assert.Equal(CalcDemoProject.Areas.Count + 1, demo.Outcome.Packs);
    }

    [Fact]
    public async Task Every_Built_Chain_Is_In_The_Project_As_A_Pack_And_The_Scan_To_Seal_Pair_Beside_Them()
    {
        // Every pack template this build ships is used; none is left out.
        Assert.Equal(CalcPackTemplates.All.Select(t => t.Id).Order(), CalcDemoProject.Areas.Select(a => a.TemplateId).Order());

        var packs = await Host.Store.ListPacksAsync(demo.ProjectId);
        var expected = CalcDemoProject.Areas.Select(CalcDemoProject.PackTitle).Append($"{CalcDemoProject.NamePrefix}{CalcDemoProject.SealArea}").Order();
        Assert.Equal(expected, packs.Select(p => p.Title).Order());
        Assert.All(packs, p => Assert.Equal(CalcInstanceStatus.Draft, p.Status));

        var instances = await InstancesAsync();
        var names = instances.Select(i => i.Name).ToHashSet(StringComparer.Ordinal);

        // The seven chains the spec names, each with the step that makes it what it is.
        foreach (var (templateId, calculator) in new[]
        {
            ("pack.drive-train", "mech.shaft"), ("pack.motor-circuit", "elec.conductor-sizing"), ("pack.hydraulic-power-unit", "fp.accumulator"),
            ("pack.steel-support", "struct.base-plate"), ("pack.wet-heating", "bs.sealed-system"), ("pack.cooling", "elec.enclosure-heat"),
        })
        {
            var area = CalcDemoProject.Areas.Single(a => a.TemplateId == templateId);
            var template = CalcPackTemplates.All.Single(t => t.Id == templateId);
            Assert.All(template.Steps, step => Assert.Contains(CalcDemoProject.StepName(area, step), names));
            Assert.Contains(instances, i => i.TemplateReference.StartsWith(calculator + "@", StringComparison.Ordinal) && i.Name.StartsWith($"{CalcDemoProject.NamePrefix}{area.Area}:", StringComparison.Ordinal));
        }

        // Scan to seal: the stack and the gland, and the pack that holds them.
        var seal = $"{CalcDemoProject.NamePrefix}{CalcDemoProject.SealArea}";
        Assert.Contains($"{seal}: Groove root stack", names);
        Assert.Contains($"{seal}: Piston seal gland", names);
        Assert.Equal(2, Assert.Single(packs, p => p.Title == seal).InstanceIds.Count);

        Assert.Equal(CalcPackTemplates.All.Sum(t => t.Steps.Count) + 2, instances.Count);
    }

    [Fact]
    public async Task Every_Calculation_Runs_Has_No_Block_Is_Current_And_The_Only_Failing_Check_Is_The_One_Shown_On_Purpose()
    {
        var instances = await InstancesAsync();
        var failing = new List<string>();

        foreach (var instance in instances)
        {
            Assert.False(instance.Blocks.Count > 0, $"{instance.Name}: {string.Join("; ", instance.Blocks.Select(b => b.Message))}");
            Assert.False((await Host.Service.StalenessAsync(instance)).IsStale, $"{instance.Name} is stale");
            Assert.Equal(CalcInstanceStatus.Draft, instance.Status);
            Assert.NotEmpty(instance.Outputs);
            failing.AddRange(instance.Checks.Where(c => !c.Passed).Select(c => $"{instance.Name} | {c.Name}"));
        }

        // The control panel's enclosure cannot lose its heat by itself: that FAIL is why the chain goes on to a cooling coil. Nothing else fails.
        var enclosure = $"{CalcDemoProject.NamePrefix}Control panel cooling: Enclosure heat | {DemoHost.DeliberateFail}";
        Assert.Equal([enclosure], failing);
    }

    [Fact]
    public async Task Every_Link_Of_Every_Chain_Is_Bound_And_Current()
    {
        var instances = (await InstancesAsync()).ToDictionary(i => i.Name, StringComparer.Ordinal);

        foreach (var area in CalcDemoProject.Areas)
        {
            var template = CalcPackTemplates.All.Single(t => t.Id == area.TemplateId);
            var byKey = template.Steps.ToDictionary(s => s.Key, s => instances[CalcDemoProject.StepName(area, s)], StringComparer.Ordinal);

            foreach (var binding in template.Bindings)
            {
                var target = byKey[binding.ToStep];
                var upstream = byKey[binding.FromStep];
                var linked = binding.Row is { } row && binding.Column is { } column
                    ? target.CellLinks.Any(l => l.Symbol == binding.Input && l.Row == row && l.Column == column && l.Link.UpstreamId == upstream.Id && l.Link.OutputSymbol == binding.Output)
                    : target.Links.Any(l => l.Input.Symbol == binding.Input && l.Link.UpstreamId == upstream.Id && l.Link.OutputSymbol == binding.Output);
                Assert.True(linked, $"{area.Area}: {binding.FromStep}.{binding.Output} → {binding.ToStep}.{binding.Input} is not linked");
            }
        }

        // The scan-to-seal gland takes its groove diameter from the stack.
        var stack = instances[$"{CalcDemoProject.NamePrefix}{CalcDemoProject.SealArea}: Groove root stack"];
        var gland = instances[$"{CalcDemoProject.NamePrefix}{CalcDemoProject.SealArea}: Piston seal gland"];
        Assert.Contains(gland.Links, l => l.Input.Symbol == "D_g" && l.Link.UpstreamId == stack.Id && l.Link.OutputSymbol == "x_mean");
        Assert.False((await Host.Service.StalenessAsync(gland)).IsStale);
    }

    [Fact]
    public async Task The_Scan_To_Seal_Pair_Is_Fed_By_The_Synthetic_Table_With_Its_Provenance_On_Every_Fitted_Value()
    {
        var instances = (await InstancesAsync()).ToDictionary(i => i.Name, StringComparer.Ordinal);
        var stack = instances[$"{CalcDemoProject.NamePrefix}{CalcDemoProject.SealArea}: Groove root stack"];
        var gland = instances[$"{CalcDemoProject.NamePrefix}{CalcDemoProject.SealArea}: Piston seal gland"];

        var stackImport = Assert.Single(stack.ScanImports!);
        var glandImport = Assert.Single(gland.ScanImports!);
        Assert.Equal(CalcDemoProject.StackProfileId, stackImport.ProfileId);
        Assert.Equal(CalcDemoProject.GlandProfileId, glandImport.ProfileId);
        Assert.Equal(CalcDemoProject.ScanFileName, stackImport.Files.Single().FileName);
        Assert.Equal(64, stackImport.Files.Single().Sha256.Length);

        var groove = stack.Inputs.Single(i => i.Symbol == "contributors").Rows![0];
        Assert.Equal(CalcProvenanceKind.ScanImport, groove.Get("upper")!.Provenance!.Kind);
        Assert.Equal("normal", groove.Get("distribution")!.Text);

        var measured = gland.Inputs.Single(i => i.Symbol == "measured").Rows!;
        Assert.Equal(["D_s", "b"], measured.Select(r => r.Get("dim")!.Text!).ToArray());
        Assert.All(measured, r => Assert.Equal(CalcProvenanceKind.ScanImport, r.Get("mean")!.Provenance!.Kind));

        // The 30 invented parts: every fitted mean is where the generator put it (to a few parts in a thousand of the spread).
        Assert.Equal(51.485, ChainKit.Si(stack, "x_mean") * 1000, 2);
        Assert.Equal(56.922, measured[0].Get("mean")!.Number!.Value, 2);
        Assert.Equal(4.835, measured[1].Get("mean")!.Number!.Value, 2);
        Assert.Equal(ChainKit.Si(stack, "x_mean") * 1000, gland.Inputs.Single(i => i.Symbol == "D_g").Number!.Value, 9);
    }

    [Fact]
    public void The_Synthetic_Scan_Table_Is_The_Same_Everywhere_Thirty_Parts_Under_The_Import_Limits_And_Printed_In_The_Docs()
    {
        var csv = CalcDemoProject.SealScanCsv();
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Element,Nominal,Actual,Deviation,Upper tol.,Lower tol.,Serial", lines[0]);
        Assert.Equal(1 + (3 * CalcDemoProject.ScanParts), lines.Length);
        Assert.Equal(csv, CalcDemoProject.SealScanCsv());
        Assert.True(Encoding.UTF8.GetByteCount(csv) < 10_000, "a few kilobytes against the 5 MB limit");
        Assert.Equal(CalcDemoProject.ScanParts, lines.Skip(1).Select(l => l.Split(',')[^1]).Distinct().Count());
        Assert.All(lines.Skip(1), l => Assert.Equal(7, l.Split(',').Length));

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "VERSION")))
            root = root.Parent;

        var path = Path.Combine(root!.FullName, "docs", "worked-examples", "demo", CalcDemoProject.ScanFileName);

        // Like the worked examples, the printed copy is written by the test when asked to (TEMPEST_WRITE_WORKED_EXAMPLES=1) and compared with otherwise.
        if (Environment.GetEnvironmentVariable("TEMPEST_WRITE_WORKED_EXAMPLES") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, csv.Replace("\r\n", "\n", StringComparison.Ordinal), new UTF8Encoding(false));
        }

        var printed = File.ReadAllText(path);
        Assert.Equal(csv.Replace("\r\n", "\n", StringComparison.Ordinal), printed.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    // ---- records and data ------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task No_Licensed_Record_Is_Referenced_And_No_Record_Is_Stored_Beyond_The_Indicative_Or_Synthetic()
    {
        var instances = await InstancesAsync();
        var pins = new List<CalcProvenance>();

        foreach (var instance in instances)
        {
            foreach (var input in instance.Inputs)
            {
                if (input.Provenance is { } p)
                    pins.Add(p);

                foreach (var cell in input.Rows?.SelectMany(r => r.Cells.Values) ?? [])
                    if (cell.Provenance is { } cp)
                        pins.Add(cp);
            }
        }

        // Every record the demonstration pins is unlicensed and indicative or synthetic.
        foreach (var pin in pins.Select(CalcDataRecord.PinOf).Where(p => p is not null).Select(p => p!.Value).Distinct())
        {
            var record = await Host.Store.FindRecordAsync(pin.Id, pin.Revision);
            Assert.NotNull(record);
            Assert.False(record!.IsLicensed(), record.Title);
            Assert.False(await Host.Store.IsLicensedInAnyRevisionAsync(pin.Id), record.Title);
            Assert.True(record.Origin == CalcDataOrigin.Indicative || record.Title.Contains("SYNTHETIC", StringComparison.Ordinal), record.Title);
        }

        // The install stores no record of its own (the cable tables are labelled synthetic inputs), and so no licensed one exists.
        Assert.Empty(await Host.Store.ListRecordsAsync());

        // The synthetic tables say so on every sheet that holds them.
        foreach (var instance in instances.Where(i => i.TemplateReference.StartsWith("elec.conductor-sizing@", StringComparison.Ordinal)))
        {
            Assert.All(instance.Inputs.Where(i => i.Symbol is "ampacity" or "ratings"), t => Assert.Contains("SYNTHETIC", t.Provenance!.Reference, StringComparison.Ordinal));
            Assert.Contains("SYNTHETIC", CalcSheetTextRenderer.Render(await Host.Service.SheetAsync(instance)), StringComparison.Ordinal);
        }
    }

    // ---- the DEMO watermark ----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Every_Calculation_Title_And_Reference_Says_Demo_And_Every_Sheet_Carries_The_Watermark_From_The_Project_Flag()
    {
        foreach (var instance in await InstancesAsync())
        {
            Assert.StartsWith(CalcDemoProject.NamePrefix, instance.Name, StringComparison.Ordinal);
            var evidence = (Evidence)(await Host.Context.Repository.FindAsync(instance.EvidenceId!.Value))!;
            Assert.StartsWith(CalcDemoProject.NamePrefix, evidence.DisplayName, StringComparison.Ordinal);

            var sheet = await Host.Service.SheetAsync(instance);
            Assert.Equal(CalcDemoProject.Watermark, sheet.Header.Watermark);
            Assert.StartsWith("DEMO", sheet.Header.CalcId, StringComparison.Ordinal);
            Assert.Contains("DEMO", sheet.Header.Project, StringComparison.OrdinalIgnoreCase);

            var text = CalcSheetTextRenderer.Render(sheet);
            Assert.Contains($"*** {CalcDemoProject.Watermark} ***", text, StringComparison.Ordinal);
            Assert.Equal(1, text.Split("\n").Count(l => l.StartsWith("*** DEMO", StringComparison.Ordinal)));
        }

        foreach (var pack in await Host.Store.ListPacksAsync(demo.ProjectId))
        {
            Assert.StartsWith(CalcDemoProject.NamePrefix, pack.Title, StringComparison.Ordinal);
            var evidence = (Evidence)(await Host.Context.Repository.FindAsync(pack.EvidenceId!.Value))!;
            Assert.StartsWith(CalcDemoProject.NamePrefix, evidence.DisplayName, StringComparison.Ordinal);
        }

        // The mark is the project's flag, not words in a calculation: no input or assumption text of a template says DEMO.
        Assert.DoesNotContain("DEMO", string.Concat(Host.Catalogue.All().SelectMany(e => e.Template.Inputs.Select(i => i.Description))), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Calculation_In_A_Real_Project_Carries_No_Demo_Mark()
    {
        var host = DemoHost.New();
        var project = await host.ProjectAsync();
        var real = await host.Service.CreateAsync(PackTemplateFirstStep("pack.drive-train"), project, "Real motor sizing", DriveInputs(host));
        var sheet = await host.Service.SheetAsync(real);

        Assert.Null(sheet.Header.Watermark);
        Assert.DoesNotContain("DEMO", CalcSheetTextRenderer.Render(sheet), StringComparison.Ordinal);
    }

    private static string PackTemplateFirstStep(string id) => CalcPackTemplates.All.Single(t => t.Id == id).Steps[0].TemplateId;

    private static IReadOnlyList<CalcInputValue> DriveInputs(LibraryTestHost host)
    {
        var template = CalcPackTemplates.DriveTrain;
        return host.Service.ExampleInputs(template, template.Steps[0], CalcProvenance.Specification("SPEC-REAL-1"), CalcProvenance.Assumed("Sheet default."))!;
    }

    // ---- refusal -------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Installing_Again_Is_Refused_In_A_Sentence_And_Makes_Nothing()
    {
        var before = (await InstancesAsync()).Count;
        var projects = (await Host.Context.Repository.ListByKindAsync(CalcDemoProject.ProjectKind)).Count;

        var again = await demo.Demo.InstallAsync();

        Assert.False(again.Succeeded);
        Assert.Contains("installed already", again.Message, StringComparison.Ordinal);
        Assert.Contains(CalcDemoProject.DisplayName, again.Message, StringComparison.Ordinal);
        Assert.Equal(before, (await InstancesAsync()).Count);
        Assert.Equal(projects, (await Host.Context.Repository.ListByKindAsync(CalcDemoProject.ProjectKind)).Count);
        var status = await demo.Demo.StatusAsync();
        Assert.True(status.Installed);
        Assert.Equal(demo.ProjectId, status.ProjectId);
    }

    [Fact]
    public async Task A_Real_Project_That_Holds_The_Number_Stops_The_Install_And_Is_Never_Removed_As_The_Demo()
    {
        var host = DemoHost.New();
        var service = DemoHost.Service(host);
        var factory = new EngineeringObjectFactory<Project>(
            CalcDemoProject.ProjectKind, host.Context, (doc, rev) => new Project(doc, rev, host.Context, CalcDemoProject.Identifier, "A customer's real job", EngineeringObjectMetadata.Empty));
        var real = await factory.CreateAsync("A real project that happens to hold the number.");
        var before = host.Backing.Snapshot();

        var install = await service.InstallAsync();
        var remove = await service.RemoveAsync();

        Assert.False(install.Succeeded);
        Assert.Contains("not the demo project", install.Message, StringComparison.Ordinal);
        Assert.False(remove.Succeeded);
        Assert.Contains("not the demo project", remove.Message, StringComparison.Ordinal);
        Assert.False(((IDeletable)real).IsDeleted);
        AssertSame(before, host.Backing.Snapshot());

        // A project that carries only the flag, under another number, is not the demonstration either.
        var tagged = new EngineeringObjectFactory<Project>(
            CalcDemoProject.ProjectKind, host.Context, (doc, rev) => new Project(doc, rev, host.Context, "OTHER-PROJ01", "Tagged by hand", new EngineeringObjectMetadata(Tags: [CalcDemoProject.Tag])));
        var other = await tagged.CreateAsync("Tagged by hand.");
        Assert.False((await service.RemoveAsync()).Succeeded);
        Assert.False(((IDeletable)other).IsDeleted);
    }

    [Fact]
    public async Task A_Failed_Install_Removes_What_It_Made_And_Says_Why()
    {
        var host = DemoHost.New();
        var service = DemoHost.Service(host, withTables: false);

        var outcome = await service.InstallAsync();

        Assert.False(outcome.Succeeded);
        Assert.Contains("could not be installed", outcome.Message, StringComparison.Ordinal);
        Assert.Contains("nothing of it was kept", outcome.Message, StringComparison.Ordinal);
        Assert.False((await service.StatusAsync()).Installed);
        Assert.Empty(host.Backing.CommittedKeys(CalcInstanceStore.InstanceCollection));
        Assert.Empty(host.Backing.CommittedKeys(CalcInstanceStore.PackCollection));
        Assert.Empty(host.Backing.CommittedKeys(CalcInstanceStore.ScanProfileCollection));
        Assert.All(await host.Context.Repository.ListAllAsync(), entry => Assert.True(entry.IsDeleted, $"{entry.Kind} '{entry.DisplayName}' is left live"));

        // It can be installed afterwards, with its tables.
        Assert.True((await DemoHost.Service(host).InstallAsync()).Succeeded);
    }

    [Fact]
    public async Task A_Build_Without_A_Pack_Template_Refuses_To_Install_And_Makes_Nothing()
    {
        var host = new LibraryTestHost(calculators: [.. ElectricalCalculators.All, .. MechanicalCalculators.All], packTemplates: [CalcPackTemplates.DriveTrain]);
        var before = host.Backing.Snapshot();

        var outcome = await DemoHost.Service(host).InstallAsync();

        Assert.False(outcome.Succeeded);
        Assert.Contains("cannot be installed", outcome.Message, StringComparison.Ordinal);
        AssertSame(before, host.Backing.Snapshot());
    }

    // ---- nothing happens by itself ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Building_The_Services_And_Asking_Its_Status_On_An_Empty_Database_Makes_Nothing()
    {
        var host = DemoHost.New();
        var before = host.Backing.Snapshot();

        var service = DemoHost.Service(host);
        var status = await service.StatusAsync();

        Assert.False(status.Installed);
        Assert.Null(status.InstallRefusal);
        AssertSame(before, host.Backing.Snapshot());
        Assert.Empty(await host.Context.Repository.ListAllAsync());
        Assert.Empty(host.Backing.CommittedKeys(CalcInstanceStore.InstanceCollection));
    }

    // ---- remove ----------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Remove_Deletes_Only_The_Demo_Rows_In_One_Transaction_And_Leaves_A_Real_Project_Exactly_As_It_Was()
    {
        var host = DemoHost.New();
        var service = DemoHost.Service(host);

        // A real project with a real chain, a real calculation by itself, a pack, a scan profile and a record: all must survive.
        var realProject = await host.ProjectAsync();
        var realPack = await InstantiateRealAsync(host, realProject);
        var realStandalone = await host.Service.CreateAsync("test.direct-stress", realProject, "Real direct stress", LibraryTestHost.ProvenancedInputs());
        await host.Service.SaveScanProfileAsync(CalcDemoProject.StackProfile with { Id = "real-seal-stack" });
        await host.Service.SaveScanProfileAsync(CalcDemoProject.GlandProfile with { Id = "real-seal-gland" });
        var (record, refusal) = await host.Service.SaveRecordAsync(new CalcDataRecord(
            Guid.Empty, "cable.ampacity", "Real ampacity", 0, "A catalogue", "2020", "Table 1", "Holder", "tester", false, CalcDataOrigin.UserEntered,
            [new CalcDataValue("ampacity", Rows: ConductorSizingCalculator.SyntheticAmpacity().Rows)], DateTimeOffset.UnixEpoch));
        Assert.Null(refusal);
        var before = host.Backing.Snapshot();

        Assert.True((await service.InstallAsync()).Succeeded);
        var demoId = (await service.StatusAsync()).ProjectId!.Value;
        Assert.NotEqual(before.Count, host.Backing.Snapshot().Count);

        // One transaction means all or nothing: a failure in the middle of the batch of deletes (the 40th of about eighty) leaves every row where it was and
        // commits nothing. A count of commits would pass for a store that deleted half and committed twice.
        var installed = host.Backing.Snapshot();
        var commitsBefore = host.Backing.CommitCount;
        var bomb = new CalcInstanceStore(new FailOnNthDeleteStore(host.Failing, failOn: 40));
        await Assert.ThrowsAsync<InvalidOperationException>(() => bomb.DeleteDemonstrationRowsAsync(demoId, CalcDemoProject.ScanProfiles));
        AssertSame(installed, host.Backing.Snapshot());
        Assert.Equal(commitsBefore, host.Backing.CommitCount);
        Assert.Equal(60, (await host.Store.ListInstancesAsync(demoId)).Count);
        Assert.Equal(13, (await host.Store.ListPacksAsync(demoId)).Count);
        Assert.NotNull(await host.Service.FindScanProfileAsync(CalcDemoProject.StackProfileId));

        var outcome = await service.RemoveAsync();
        Assert.True(outcome.Succeeded, outcome.Message);
        Assert.Contains("Removed the demo project", outcome.Message, StringComparison.Ordinal);

        // The demo's calculation, pack and scan-profile rows are all gone.
        Assert.Empty(await host.Store.ListInstancesAsync(demoId));
        Assert.Empty(await host.Store.ListPacksAsync(demoId));
        Assert.Null(await host.Service.FindScanProfileAsync(CalcDemoProject.StackProfileId));
        Assert.Null(await host.Service.FindScanProfileAsync(CalcDemoProject.GlandProfileId));
        Assert.False((await service.StatusAsync()).Installed);

        // Nothing of the real project changed, nor any row that existed before the install: byte for byte.
        var after = host.Backing.Snapshot();
        foreach (var (key, value) in before)
            Assert.True(after.TryGetValue(key, out var now) && now == value, $"{key.Collection}/{key.Key} changed or was deleted");

        // The Calc collections hold exactly what they held before the install.
        foreach (var collection in new[] { CalcInstanceStore.InstanceCollection, CalcInstanceStore.PackCollection, CalcInstanceStore.ScanProfileCollection, CalcInstanceStore.RecordCollection })
            Assert.Equal(before.Where(kv => kv.Key.Collection == collection).Select(kv => kv.Key.Key).Order(), after.Where(kv => kv.Key.Collection == collection).Select(kv => kv.Key.Key).Order());

        // And what the install added elsewhere is the demo's own objects, now marked deleted (the platform never erases an engineering object).
        var addedStates = after.Where(kv => kv.Key.Collection == "EngineeringDomain.ObjectState" && !before.ContainsKey(kv.Key)).ToList();
        Assert.NotEmpty(addedStates);
        Assert.All(addedStates, kv => Assert.Contains("\"IsDeleted\":true", kv.Value, StringComparison.Ordinal));
        Assert.All(await host.Context.Repository.ListAllAsync(), entry => Assert.True(entry.IsDeleted == (entry.Id == demoId || entry.ParentId == demoId), $"{entry.Kind} '{entry.DisplayName}'"));

        // The real calculations still run and are read through the same service.
        Assert.False(((IDeletable)(await host.Context.Repository.FindAsync(realProject!.Value))!).IsDeleted);
        var survivor = (await host.Store.FindInstanceAsync(realStandalone.Id))!;
        Assert.Equal(CalcInstanceStatus.Draft, survivor.Status);
        Assert.Equal(realPack.Instances.Count + 1, (await host.Store.ListInstancesAsync(realProject)).Count);
        Assert.NotNull(await host.Service.FindScanProfileAsync("real-seal-stack"));
        Assert.NotNull(await host.Store.FindRecordAsync(record!.Id));
        Assert.Null((await host.Service.SheetAsync(survivor)).Header.Watermark);
    }

    private static async Task<CalcPackInstantiation> InstantiateRealAsync(LibraryTestHost host, Guid? project)
    {
        var template = CalcPackTemplates.DriveTrain;
        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(template, project, "Real conveyor drive", null);
        Assert.True(refusal is null, refusal);
        return made!;
    }

    [Fact]
    public async Task Remove_Works_On_Checked_Packs_And_Install_Can_Follow_It()
    {
        var host = DemoHost.New();
        var service = DemoHost.Service(host);
        Assert.True((await service.InstallAsync()).Succeeded);
        var first = (await service.StatusAsync()).ProjectId!.Value;

        // A second person checks every pack: the chains are checkable (no block, nothing stale) and the demonstration stays removable once checked.
        foreach (var pack in await host.Store.ListPacksAsync(first))
        {
            var members = new Dictionary<string, CalcInstance>(StringComparer.Ordinal);

            foreach (var id in pack.InstanceIds)
            {
                var member = (await host.Store.FindInstanceAsync(id))!;
                members[member.Name] = member;
            }

            await ChainKit.AuthoredByAnotherAsync(host, new CalcPackInstantiation(pack, members));
            host.Principal = "j.reviewer";
            var check = await host.Service.CheckPackAsync(pack.Id, "J. Reviewer", "Demo Client", "Checked for the demonstration.", CheckOutcome.Accepted);
            host.Principal = "test.engineer";
            Assert.True(check.Succeeded, $"{pack.Title}: {check.Reason}");
            Assert.Equal(CalcInstanceStatus.Checked, (await host.Store.FindPackAsync(pack.Id))!.Status);
        }

        var removed = await service.RemoveAsync();
        Assert.True(removed.Succeeded, removed.Message);
        Assert.Empty(await host.Store.ListInstancesAsync(first));
        Assert.Empty(await host.Store.ListPacksAsync(first));

        var again = await service.InstallAsync();
        Assert.True(again.Succeeded, again.Message);
        Assert.NotEqual(first, (await service.StatusAsync()).ProjectId);
        Assert.Equal(demo.Outcome.Calculations, again.Calculations);
    }

    [Fact]
    public async Task An_Issued_Demo_Pack_Is_Never_Deleted_So_The_Demo_Cannot_Be_Removed_And_Its_Issued_Sheet_Says_Demo()
    {
        var host = DemoHost.New();
        var service = DemoHost.Service(host);
        Assert.True((await service.InstallAsync()).Succeeded);
        var projectId = (await service.StatusAsync()).ProjectId!.Value;
        var pack = (await host.Store.ListPacksAsync(projectId)).Single(p => p.Title == CalcDemoProject.PackTitle(CalcDemoProject.Areas.Single(a => a.TemplateId == "pack.drive-train")));
        var members = new Dictionary<string, CalcInstance>(StringComparer.Ordinal);

        foreach (var id in pack.InstanceIds)
        {
            var member = (await host.Store.FindInstanceAsync(id))!;
            members[member.Name] = member;
        }

        await ChainKit.AuthoredByAnotherAsync(host, new CalcPackInstantiation(pack, members));
        host.Principal = "j.reviewer";
        Assert.True((await host.Service.CheckPackAsync(pack.Id, "J. Reviewer", "Demo Client", "OK", CheckOutcome.Accepted)).Succeeded);
        var issued = await host.Service.IssuePackAsync(pack.Id, "DEMO-ISSUE-1", "Demo Client");
        host.Principal = "test.engineer";
        Assert.True(issued.Succeeded, issued.Reason);

        // The issued sheet carries the mark: the pack's index sheet, and the sheet of every member, as frozen at issue.
        var evidence = (Evidence)(await host.Context.Repository.FindAsync((await host.Store.FindPackAsync(pack.Id))!.EvidenceId!.Value))!;
        var attachment = Assert.Single(await evidence.GetAttachmentsAsync());
        var sheet = Encoding.UTF8.GetString((await evidence.ReadAttachmentContentAsync(attachment.Id)).Bytes);
        Assert.Contains($"*** {CalcDemoProject.Watermark} ***", sheet, StringComparison.Ordinal);
        Assert.Contains("PACK INDEX", sheet, StringComparison.Ordinal);
        Assert.All(pack.InstanceIds, id => Assert.Contains($"*** {CalcDemoProject.Watermark} ***", host.Store.FindInstanceAsync(id).GetAwaiter().GetResult()!.Issue!.SheetText, StringComparison.Ordinal));

        // The removal refuses, names what is in the way, and deletes nothing.
        var before = host.Backing.Snapshot();
        var removal = await service.RemoveAsync();

        Assert.False(removal.Succeeded);
        Assert.Contains("never deleted", removal.Message, StringComparison.Ordinal);
        Assert.Contains("DEMO — ", removal.Message, StringComparison.Ordinal);
        AssertSame(before, host.Backing.Snapshot());
        Assert.True((await service.StatusAsync()).Installed);

        // Below the service, the store refuses to delete an issued row too, and its one transaction rolls back whole.
        await Assert.ThrowsAsync<CalcInstanceImmutableException>(() => host.Store.DeleteDemonstrationRowsAsync(projectId, CalcDemoProject.ScanProfiles));
        AssertSame(before, host.Backing.Snapshot());
    }

    private sealed class RecordingAudit : Tempest.Core.Audit.IAuditRecorder
    {
        public List<(string Action, IReadOnlyDictionary<string, string>? Detail)> Rows { get; } = [];

        public Task RecordAsync(string action, IReadOnlyDictionary<string, string>? detail = null, CancellationToken cancellationToken = default)
        {
            Rows.Add((action, detail));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task An_Install_And_A_Removal_Each_Leave_One_Audit_Row_And_A_Refusal_Leaves_None()
    {
        var host = DemoHost.New();
        var audit = new RecordingAudit();
        var service = new CalcDemoProjectService(host.Service, host.Context, [ConductorSizingCalculator.SyntheticAmpacity(), ConductorSizingCalculator.TypicalRatings()], audit);

        Assert.False((await service.RemoveAsync()).Succeeded);
        Assert.Empty(audit.Rows);

        var installed = await service.InstallAsync();
        Assert.True(installed.Succeeded, installed.Message);
        var row = Assert.Single(audit.Rows);
        Assert.Equal("Demo.Installed", row.Action);
        Assert.Equal(CalcDemoProject.Identifier, row.Detail!["Project"]);
        Assert.Equal(installed.Calculations.ToString(System.Globalization.CultureInfo.InvariantCulture), row.Detail["Calculations"]);

        Assert.False((await service.InstallAsync()).Succeeded);
        Assert.Single(audit.Rows);

        var removed = await service.RemoveAsync();
        Assert.True(removed.Succeeded, removed.Message);
        Assert.Equal(["Demo.Installed", "Demo.Removed"], audit.Rows.Select(r => r.Action).ToArray());
        Assert.Equal(removed.Packs.ToString(System.Globalization.CultureInfo.InvariantCulture), audit.Rows[1].Detail!["Packs"]);
    }

    [Fact]
    public async Task The_Store_Deletes_Nothing_But_A_Projects_Rows_And_Demonstration_Scan_Profiles()
    {
        var host = DemoHost.New();
        var real = await host.Service.CreateAsync("test.direct-stress", await host.ProjectAsync(), "Real", LibraryTestHost.ProvenancedInputs());
        var before = host.Backing.Snapshot();

        await Assert.ThrowsAsync<ArgumentException>(() => host.Store.DeleteDemonstrationRowsAsync(Guid.Empty, []));
        await Assert.ThrowsAsync<ArgumentException>(() => host.Store.DeleteDemonstrationRowsAsync(real.ProjectId!.Value, [("real-seal-stack", 1)]));
        await Assert.ThrowsAsync<ArgumentException>(() => host.Store.DeleteDemonstrationRowsAsync(real.ProjectId!.Value, [("demo-press-seal-stack", 1), ("any-other", 1)]));
        AssertSame(before, host.Backing.Snapshot());

        // Given a real project's id it would delete that project's rows: which is why only the service, after proving the project is the demonstration, calls it.
        var (instances, packs, profiles) = await host.Store.DeleteDemonstrationRowsAsync(Guid.NewGuid(), [(CalcDemoProject.StackProfileId, 1)]);
        Assert.Equal((0, 0, 0), (instances, packs, profiles));
        AssertSame(before, host.Backing.Snapshot());
    }

    /// <summary>A store whose transaction throws on its Nth delete, partway through a batch: what a failing disk or a killed process does between two writes.</summary>
    private sealed class FailOnNthDeleteStore(Tempest.Core.Persistence.IQueryablePersistenceStore inner, int failOn) : Tempest.Core.Persistence.IQueryablePersistenceStore
    {
        public long CurrentSequence => inner.CurrentSequence;

        public Task<IReadOnlyList<string>> ListKeysAsync(string collection, string keyPrefix, CancellationToken cancellationToken = default) => inner.ListKeysAsync(collection, keyPrefix, cancellationToken);

        public Task<IReadOnlyList<KeyValuePair<string, string>>> ReadAllAsync(string collection, CancellationToken cancellationToken = default) => inner.ReadAllAsync(collection, cancellationToken);

        public Task<IReadOnlyDictionary<string, string?>> ReadManyAsync(string collection, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default) => inner.ReadManyAsync(collection, keys, cancellationToken);

        public Task ExecuteInTransactionAsync(Func<Tempest.Core.Persistence.IPersistenceTransaction, CancellationToken, Task> work, CancellationToken cancellationToken = default)
        {
            var deletes = 0;
            return inner.ExecuteInTransactionAsync((tx, token) => work(new Counting(tx, () => ++deletes == failOn), token), cancellationToken);
        }

        public Task<T> ExecuteInReadTransactionAsync<T>(Func<Tempest.Core.Persistence.IPersistenceReadTransaction, CancellationToken, Task<T>> read, CancellationToken cancellationToken = default) => inner.ExecuteInReadTransactionAsync(read, cancellationToken);

        public Task<IReadOnlyList<Tempest.Core.Persistence.SearchHit>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default) => inner.SearchAsync(query, limit, cancellationToken);

        public Task<bool> IsSearchIndexEmptyAsync(CancellationToken cancellationToken = default) => inner.IsSearchIndexEmptyAsync(cancellationToken);

        private sealed class Counting(Tempest.Core.Persistence.IPersistenceTransaction tx, Func<bool> failNow) : Tempest.Core.Persistence.IPersistenceTransaction
        {
            public Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default) => tx.ReadAsync(collection, key, cancellationToken);

            public Task WriteAsync(string collection, string key, string value, CancellationToken cancellationToken = default) => tx.WriteAsync(collection, key, value, cancellationToken);

            public Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default) =>
                failNow() ? throw new InvalidOperationException("Injected: the store failed in the middle of the batch.") : tx.DeleteAsync(collection, key, cancellationToken);

            public Task<byte[]?> ReadBytesAsync(string collection, string key, CancellationToken cancellationToken = default) => tx.ReadBytesAsync(collection, key, cancellationToken);

            public Task WriteBytesAsync(string collection, string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default) => tx.WriteBytesAsync(collection, key, value, cancellationToken);

            public Task<IReadOnlyList<string>> ListKeysAsync(string collection, string keyPrefix, CancellationToken cancellationToken = default) => tx.ListKeysAsync(collection, keyPrefix, cancellationToken);

            public Task IndexTextAsync(Guid objectId, string kind, Guid? projectId, string title, string? identifier, string? refs, CancellationToken cancellationToken = default) =>
                tx.IndexTextAsync(objectId, kind, projectId, title, identifier, refs, cancellationToken);

            public Task RemoveFromIndexAsync(Guid objectId, CancellationToken cancellationToken = default) => tx.RemoveFromIndexAsync(objectId, cancellationToken);
        }
    }

    /// <summary>An audit recorder that lets a test break the store at a chosen moment: right after the row for <paramref name="action"/> is written.</summary>
    private sealed class BreakingAudit(LibraryTestHost host, string action, bool alsoThrow = false) : Tempest.Core.Audit.IAuditRecorder
    {
        public List<(string Action, IReadOnlyDictionary<string, string>? Detail)> Rows { get; } = [];

        public Task RecordAsync(string recorded, IReadOnlyDictionary<string, string>? detail = null, CancellationToken cancellationToken = default)
        {
            Rows.Add((recorded, detail));

            if (recorded == action)
            {
                // Every commit from here on fails: the second step of the removal, or the install's own clean-up, cannot land.
                host.Failing.FailEveryCommit = true;

                if (alsoThrow)
                    throw new InvalidOperationException("the audit store went away");
            }

            return Task.CompletedTask;
        }
    }

    private static CalcDemoProjectService ServiceWith(LibraryTestHost host, Tempest.Core.Audit.IAuditRecorder audit) =>
        new(host.Service, host.Context, [ConductorSizingCalculator.SyntheticAmpacity(), ConductorSizingCalculator.TypicalRatings()], audit);

    [Fact]
    public async Task The_Row_Deletion_Is_One_Transaction_Body_And_The_Removal_Counts_What_It_Deleted()
    {
        var host = DemoHost.New();
        var service = DemoHost.Service(host);
        Assert.True((await service.InstallAsync()).Succeeded);
        var projectId = (await service.StatusAsync()).ProjectId!.Value;

        var bodies = host.Failing.BodiesCompleted;
        var commits = host.Backing.CommitCount;
        var deleted = await host.Store.DeleteDemonstrationRowsAsync(projectId, CalcDemoProject.ScanProfiles);

        Assert.Equal((60, 13, 2), deleted);
        Assert.Equal(1, host.Failing.BodiesCompleted - bodies);
        Assert.Equal(commits + 1, host.Backing.CommitCount);
    }

    [Fact]
    public async Task Remove_Deletes_Only_The_Scan_Profile_Versions_The_Install_Wrote_And_Keeps_A_Later_Version_A_Person_Saved()
    {
        var host = DemoHost.New();
        var service = DemoHost.Service(host);
        Assert.True((await service.InstallAsync()).Succeeded);

        // A person builds on the demonstration's profile: a new version of it (a profile is immutable, so a change is a version).
        var (saved, refusal) = await host.Service.SaveScanProfileAsync(CalcDemoProject.StackProfile with { Version = 2, Title = "My own version of the groove-root profile" });
        Assert.Null(refusal);
        Assert.Equal(2, saved!.Version);
        var theirs = host.Backing.Snapshot().Single(kv => kv.Key.Collection == CalcInstanceStore.ScanProfileCollection && kv.Key.Key == CalcInstanceStore.ImportProfileKey(CalcDemoProject.StackProfileId, 2));

        var removed = await service.RemoveAsync();

        Assert.True(removed.Succeeded, removed.Message);
        var after = host.Backing.Snapshot();
        Assert.False(after.ContainsKey((CalcInstanceStore.ScanProfileCollection, CalcInstanceStore.ImportProfileKey(CalcDemoProject.StackProfileId, 1))));
        Assert.False(after.ContainsKey((CalcInstanceStore.ScanProfileCollection, CalcInstanceStore.ImportProfileKey(CalcDemoProject.GlandProfileId, 1))));
        Assert.Equal(theirs.Value, after[theirs.Key]);
        Assert.Equal(2, (await host.Service.FindScanProfileAsync(CalcDemoProject.StackProfileId))!.Version);
    }

    [Fact]
    public async Task A_Failure_After_The_Rows_Are_Deleted_Is_Audited_With_The_Real_Counts_Said_In_A_Sentence_And_A_Retry_Finishes_Without_A_Second_Audit_Row()
    {
        var host = DemoHost.New();
        var audit = new BreakingAudit(host, "Demo.Removed");
        var service = ServiceWith(host, audit);
        Assert.True((await service.InstallAsync()).Succeeded);
        var projectId = (await service.StatusAsync()).ProjectId!.Value;

        // The row transaction commits, the audit row is written, then every commit fails: the soft delete of the project cannot land.
        var first = await service.RemoveAsync();

        Assert.False(first.Succeeded);
        Assert.True(first.PartlyDone);
        Assert.Equal("The demo's calculations were removed; finishing the removal failed — press Remove demo project again.", first.Message);
        Assert.Equal((60, 13), (first.Calculations, first.Packs));
        Assert.Empty(await host.Store.ListInstancesAsync(projectId));
        Assert.Empty(await host.Store.ListPacksAsync(projectId));
        var row = Assert.Single(audit.Rows, r => r.Action == "Demo.Removed");
        Assert.Equal("60", row.Detail!["Calculations"]);
        Assert.Equal("13", row.Detail["Packs"]);
        Assert.False(((IDeletable)(await host.Context.Repository.FindAsync(projectId))!).IsDeleted);
        Assert.True((await service.StatusAsync()).Installed);

        // Pressing the action again finishes the job, and does not write a second, empty record of a deletion that was already recorded.
        host.Failing.FailEveryCommit = false;
        var retry = await service.RemoveAsync();

        Assert.True(retry.Succeeded, retry.Message);
        Assert.Contains("had been removed already", retry.Message, StringComparison.Ordinal);
        Assert.True(((IDeletable)(await host.Context.Repository.FindAsync(projectId))!).IsDeleted);
        Assert.False((await service.StatusAsync()).Installed);
        Assert.Single(audit.Rows, r => r.Action == "Demo.Removed");
    }

    [Fact]
    public async Task A_Removal_The_Store_Cannot_Make_Says_So_In_A_Sentence_Changes_Nothing_And_Can_Be_Pressed_Again()
    {
        var host = DemoHost.New();
        var service = DemoHost.Service(host);
        Assert.True((await service.InstallAsync()).Succeeded);
        var before = host.Backing.Snapshot();

        host.Failing.FailNextCommit = true;
        var outcome = await service.RemoveAsync();

        Assert.False(outcome.Succeeded);
        Assert.False(outcome.PartlyDone);
        Assert.Contains("was not removed", outcome.Message, StringComparison.Ordinal);
        Assert.Contains("nothing was deleted", outcome.Message, StringComparison.Ordinal);
        AssertSame(before, host.Backing.Snapshot());
        Assert.True((await DemoHost.Service(host).RemoveAsync()).Succeeded);
    }

    [Fact]
    public async Task Issued_Evidence_Under_The_Demo_Refuses_The_Removal_Naming_It_And_Changes_Nothing()
    {
        var host = DemoHost.New();
        var service = DemoHost.Service(host);
        Assert.True((await service.InstallAsync()).Succeeded);
        var projectId = (await service.StatusAsync()).ProjectId!.Value;

        // A person files their own evidence under the demonstration and issues it: not a calculation or a pack, so only the Evidence check can see it.
        var evidence = await host.Evidence.CreateAsync(projectId, "Anchorage report DEMO-ANC-1", EvidenceClassification.Calculation);
        Assert.True((await host.Evidence.RecordCheckAsync(evidence.Id, "J. Reviewer", "Demo Client", "OK", CheckOutcome.Accepted)).Succeeded);
        var issued = await host.Evidence.IssueAsync(evidence.Id, "DEMO-ANC-ISS-1", "A", "Demo Client");
        Assert.True(issued.Succeeded, issued.Reason);
        var before = host.Backing.Snapshot();

        var removal = await service.RemoveAsync();

        Assert.False(removal.Succeeded);
        Assert.Contains("Anchorage report DEMO-ANC-1", removal.Message, StringComparison.Ordinal);
        Assert.Contains("issued", removal.Message, StringComparison.Ordinal);
        Assert.Contains("never deleted", removal.Message, StringComparison.Ordinal);
        AssertSame(before, host.Backing.Snapshot());
        Assert.Equal(60, (await host.Store.ListInstancesAsync(projectId)).Count);
        Assert.False(((IDeletable)(await host.Context.Repository.FindAsync(evidence.Id))!).IsDeleted);
        Assert.True((await service.StatusAsync()).Installed);

        // A draft piece of evidence of the person's own does not stand in the way: it goes with the project, as the platform's soft delete.
        var draftHost = DemoHost.New();
        var draftService = DemoHost.Service(draftHost);
        Assert.True((await draftService.InstallAsync()).Succeeded);
        var draftProject = (await draftService.StatusAsync()).ProjectId!.Value;
        var draft = await draftHost.Evidence.CreateAsync(draftProject, "A note of mine", EvidenceClassification.Calculation);
        Assert.True((await draftService.RemoveAsync()).Succeeded);
        Assert.True(((IDeletable)(await draftHost.Context.Repository.FindAsync(draft.Id))!).IsDeleted);
    }

    [Fact]
    public async Task A_Failed_Install_Whose_Clean_Up_Also_Fails_Says_Part_Of_It_Is_Left_And_Remove_Clears_It()
    {
        var host = DemoHost.New();

        // The install is complete (60 calculations) when its audit row fails, and the store then refuses every commit: so the clean-up fails as well.
        var service = ServiceWith(host, new BreakingAudit(host, "Demo.Installed", alsoThrow: true));
        var outcome = await service.InstallAsync();

        Assert.False(outcome.Succeeded);
        Assert.True(outcome.PartlyDone);
        Assert.Contains("could not be installed", outcome.Message, StringComparison.Ordinal);
        Assert.Contains("part of it is left; use Remove demo project to clear it", outcome.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("nothing of it was kept", outcome.Message, StringComparison.Ordinal);
        Assert.Equal(60, host.Backing.CommittedKeys(CalcInstanceStore.InstanceCollection).Count);

        // The message told the truth: the project is flagged and live, and the removal the message names clears it.
        host.Failing.FailEveryCommit = false;
        var status = await service.StatusAsync();
        Assert.True(status.Installed);
        var removed = await service.RemoveAsync();
        Assert.True(removed.Succeeded, removed.Message);
        Assert.Equal(60, removed.Calculations);
        Assert.Empty(host.Backing.CommittedKeys(CalcInstanceStore.InstanceCollection));
        Assert.Empty(host.Backing.CommittedKeys(CalcInstanceStore.PackCollection));
        Assert.Empty(host.Backing.CommittedKeys(CalcInstanceStore.ScanProfileCollection));
        Assert.False((await service.StatusAsync()).Installed);
    }

    [Fact]
    public async Task A_Stale_Update_Of_A_Calculation_Deleted_Since_It_Was_Read_Is_Refused_And_Does_Not_Bring_It_Back()
    {
        var host = DemoHost.New();
        var service = DemoHost.Service(host);
        Assert.True((await service.InstallAsync()).Succeeded);
        var projectId = (await service.StatusAsync()).ProjectId!.Value;
        var read = (await host.Store.ListInstancesAsync(projectId))[0];

        Assert.True((await service.RemoveAsync()).Succeeded);
        var before = host.Backing.Snapshot();

        var refused = await Assert.ThrowsAsync<CalcConcurrencyException>(() => host.Store.SaveInstanceAsync(read with { UpdatedAt = DateTimeOffset.UtcNow }, read.UpdatedAt));

        Assert.Contains(read.Name, refused.Message, StringComparison.Ordinal);
        Assert.Contains("no longer exists", refused.Message, StringComparison.Ordinal);
        AssertSame(before, host.Backing.Snapshot());
        Assert.Null(await host.Store.FindInstanceAsync(read.Id));
        Assert.Empty(await host.Store.ListInstancesAsync(projectId));

        // The same through the batch write Revise uses; and a first write (nothing expected) still creates, as every new calculation does.
        await Assert.ThrowsAsync<CalcConcurrencyException>(() => host.Store.SaveInstancesAsync([(read, read.UpdatedAt)]));
        AssertSame(before, host.Backing.Snapshot());
        await host.Store.SaveInstanceAsync(read);
        Assert.NotNull(await host.Store.FindInstanceAsync(read.Id));
    }

    [Fact]
    public async Task A_Stale_Update_Of_A_Pack_Deleted_Since_It_Was_Read_Is_Refused_And_Does_Not_Bring_It_Back()
    {
        var host = DemoHost.New();
        var service = DemoHost.Service(host);
        Assert.True((await service.InstallAsync()).Succeeded);
        var projectId = (await service.StatusAsync()).ProjectId!.Value;
        var read = (await host.Store.ListPacksAsync(projectId))[0];

        Assert.True((await service.RemoveAsync()).Succeeded);
        var before = host.Backing.Snapshot();

        // The guarded pack write (a check, a second check or a reopen) expects the pack as it was read; with the row gone it is refused.
        var refused = await Assert.ThrowsAsync<CalcConcurrencyException>(() => host.Store.SavePackAsync(read with { UpdatedAt = DateTimeOffset.UtcNow }, read.UpdatedAt));

        Assert.Contains(read.Title, refused.Message, StringComparison.Ordinal);
        Assert.Contains("no longer exists", refused.Message, StringComparison.Ordinal);
        AssertSame(before, host.Backing.Snapshot());
        Assert.Null(await host.Store.FindPackAsync(read.Id));
        Assert.Empty(await host.Store.ListPacksAsync(projectId));

        // An unguarded save (nothing expected) still creates, as a new pack does.
        await host.Store.SavePackAsync(read);
        Assert.NotNull(await host.Store.FindPackAsync(read.Id));
    }

    [Fact]
    public async Task The_Published_Section_Dimensions_Cite_Themselves_As_Indicative_Not_The_Invented_Specification_And_The_Install_Message_Says_So()
    {
        Assert.Contains("indicative", demo.Outcome.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("all drafts with invented data", demo.Outcome.Message, StringComparison.Ordinal);

        var sections = new List<CalcInstance>();
        var chosen = 0;

        foreach (var instance in await InstancesAsync())
        {
            var h = instance.Inputs.SingleOrDefault(i => i.Symbol == "h");
            var b = instance.Inputs.SingleOrDefault(i => i.Symbol == "b");

            if (h?.Number is not 203.2 || b?.Number is not 203.6)
                continue;

            sections.Add(instance);

            // Every dimension of the section this sheet states cites the published section, whatever else the sheet's figures cite.
            foreach (var symbol in new[] { "h", "b", "t_w", "t_f", "r" }.Where(sym => instance.Inputs.Any(i => i.Symbol == sym)))
            {
                var provenance = instance.Inputs.Single(i => i.Symbol == symbol).Provenance!;
                Assert.Equal(CalcDemoProject.SectionDimensions, provenance);
                Assert.Equal("published UC 203×203×46 dimensions (indicative)", provenance.Reference);
                Assert.DoesNotContain("DEMO-SPEC", provenance.Reference, StringComparison.Ordinal);
            }

            // The figures the demonstration does choose still cite its specification, so the exception is exactly the section.
            chosen += instance.Inputs.Count(i => i.Provenance?.Reference?.Contains("(invented demonstration figure)", StringComparison.Ordinal) == true);
        }

        // The steel support's column (S3) and base plate (S6), and the heating-main anchor's base plate.
        Assert.Equal(3, sections.Count);
        Assert.True(chosen > 0);
        Assert.Contains("published UC 203×203×46 dimensions (indicative)", CalcSheetTextRenderer.Render(await Host.Service.SheetAsync(sections[0])), StringComparison.Ordinal);
    }

    private static void AssertSame(IReadOnlyDictionary<(string Collection, string Key), string> expected, IReadOnlyDictionary<(string Collection, string Key), string> actual)
    {
        Assert.Equal(expected.Count, actual.Count);

        foreach (var (key, value) in expected)
            Assert.True(actual.TryGetValue(key, out var now) && now == value, $"{key.Collection}/{key.Key} changed");
    }
}
