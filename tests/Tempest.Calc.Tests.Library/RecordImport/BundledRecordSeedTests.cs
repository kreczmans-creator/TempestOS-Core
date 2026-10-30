using System.Globalization;
using Tempest.Calc.Library;
using Tempest.Calc.Library.RecordSeeds;
using static Tempest.Calc.Tests.Library.RecordImport.RecordImportTestSupport;

namespace Tempest.Calc.Tests.Library.RecordImport;

/// <summary>
/// Owner comment 5 (2026-10-29): the bundled reference records. Every shipped set passes the ordinary record import (units, ranges, the
/// calculators' hard limits), every record and every value carries its source, URL, read date and licence status, and a set that reproduces a paid
/// standard's table, or comes from a secondary source, is locked against export until the owner settles the licence.
/// </summary>
public sealed class BundledRecordSeedTests
{
    [Fact]
    public async Task Every_Bundled_Set_Passes_The_Record_Import_With_No_Error()
    {
        var host = Host();
        var failures = new List<string>();

        foreach (var set in RecordSeedCatalogue.All)
        {
            var preview = await host.Service.RecordImport.PreviewAsync(ReferenceRecordSeeds.Request(set));

            if (!preview.CanCommit)
                failures.Add($"{set.Id}: {Describe(preview)}");
            else if (preview.Planned.Any(p => p.Licensed != set.IsExportLocked))
                failures.Add($"{set.Id}: the planned records' licence lock does not follow the set's licence status.");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Every_Set_Names_Its_Sources_With_Url_Read_Date_And_Licence_Status()
    {
        Assert.NotEmpty(RecordSeedCatalogue.All);
        Assert.Equal(RecordSeedCatalogue.All.Count, RecordSeedCatalogue.All.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count());

        foreach (var set in RecordSeedCatalogue.All)
        {
            Assert.Matches("^[a-z0-9-]+$", set.Id);
            Assert.NotNull(RecordTemplates.Find(set.TemplateId));
            Assert.Equal(RecordTemplateDisposition.Kind, RecordTemplates.Find(set.TemplateId)!.Disposition);
            Assert.False(string.IsNullOrWhiteSpace(set.Title), set.Id);
            Assert.False(string.IsNullOrWhiteSpace(set.Basis), set.Id);
            Assert.True(Enum.IsDefined(set.Licence), set.Id);
            Assert.NotEmpty(set.Sources);
            Assert.Null(set.Provenance().Problem());
            Assert.NotEmpty(ReferenceRecordSeeds.ReadCsv(set));

            foreach (var source in set.Sources)
            {
                Assert.False(string.IsNullOrWhiteSpace(source.Title), set.Id);
                Assert.StartsWith("https://", source.Url, StringComparison.Ordinal);
                Assert.True(Uri.TryCreate(source.Url, UriKind.Absolute, out _), $"{set.Id}: {source.Url}");
                Assert.True(DateOnly.TryParseExact(source.Read, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _), set.Id);
                Assert.True(Enum.IsDefined(source.Licence), set.Id);
            }

            // A set is no freer than its main source, and anything from a secondary site or a paid standard is locked until the owner settles it.
            Assert.True(set.Licence >= set.Sources[0].Licence || set.Licence == RecordSeedLicence.Derived, $"{set.Id}: the set claims a freer licence than its main source");
            Assert.Equal(set.Licence is RecordSeedLicence.ManufacturerToConfirm or RecordSeedLicence.SecondaryToConfirm || set.ReproducesStandard is not null, set.IsExportLocked);
            Assert.Equal(set.IsExportLocked, set.Provenance().LicensedSource);
        }
    }

    [Fact]
    public void Every_Bundled_Csv_Belongs_To_A_Set_With_Its_Provenance()
    {
        const string Prefix = "Tempest.Calc.Library.RecordSeeds.Data.";
        var resources = typeof(ReferenceRecordSeeds).Assembly.GetManifestResourceNames().Where(n => n.StartsWith(Prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(RecordSeedCatalogue.All.Select(s => s.ResourceName).Order(StringComparer.Ordinal), resources);
    }

    [Fact]
    public async Task Seeding_Saves_Every_Set_Once_And_Every_Value_Carries_Its_Source_And_Licence_Status()
    {
        var host = Host();
        var outcomes = await host.Service.RecordSeeds.EnsureSeededAsync();

        Assert.All(outcomes, o => Assert.True(o.Result == RecordSeedResult.Saved, $"{o.Set.Id}: {o.Result} {o.Problem}"));
        var records = await host.Store.ListRecordsAsync();
        Assert.Equal(outcomes.Sum(o => o.Records), records.Count);

        foreach (var set in RecordSeedCatalogue.All)
        {
            var mine = records.Where(r => r.Notes?.Contains(set.Marker, StringComparison.Ordinal) == true).ToList();
            Assert.True(mine.Count > 0, $"{set.Id}: no record carries the marker");

            foreach (var record in mine)
            {
                Assert.Equal(ReferenceRecordSeeds.SeedPrincipal, record.EnteredBy);
                Assert.Equal(set.IsExportLocked, record.IsLicensed());
                Assert.Equal(set.LicenceStatus, record.LicenceHolder);
                Assert.Contains($"read {RecordSeedSet.ReadDate}", record.Edition, StringComparison.Ordinal);
                Assert.Contains(set.LicenceStatus, record.Notes, StringComparison.Ordinal);
                Assert.All(set.Sources, s => Assert.Contains(s.Url, record.Notes, StringComparison.Ordinal));

                if (set.ReproducesStandard is { } standard)
                    Assert.Contains($"Reproduces {standard}", record.Notes, StringComparison.Ordinal);

                // Every value: a source, an edition with the read date, a table or clause, a licence class, who entered it.
                Assert.All(record.Values, v =>
                {
                    Assert.NotNull(v.Source);
                    Assert.False(string.IsNullOrWhiteSpace(v.Source!.Source), $"{set.Id} {v.Symbol}");
                    Assert.Contains($"read {RecordSeedSet.ReadDate}", v.Source.Edition, StringComparison.Ordinal);
                    Assert.False(string.IsNullOrWhiteSpace(v.Source.Reference), $"{set.Id} {v.Symbol}");
                    Assert.Equal(set.IsExportLocked ? CalcLicenceClass.LicensedSource : CalcLicenceClass.Public, v.Source.Licence);
                    Assert.Equal(ReferenceRecordSeeds.SeedPrincipal, v.Source.EnteredBy);
                });
            }
        }

        // A second pass adds nothing.
        var again = await host.Service.RecordSeeds.EnsureSeededAsync();
        Assert.All(again, o => Assert.Equal(RecordSeedResult.AlreadyPresent, o.Result));
        Assert.Equal(records.Count, (await host.Store.ListRecordsAsync()).Count);
    }

    // Seeding speed (2026-10-29): seeding runs at every start-up, so a fresh store takes every set in one transaction, not one per set.
    [Fact]
    public async Task Seeding_A_Fresh_Store_Writes_Every_Set_In_One_Transaction()
    {
        var host = Host();
        var before = host.Backing.CurrentSequence;

        var outcomes = await host.Service.RecordSeeds.EnsureSeededAsync();

        Assert.All(outcomes, o => Assert.Equal(RecordSeedResult.Saved, o.Result));
        Assert.Equal(1, host.Backing.CurrentSequence - before);
        Assert.Equal(outcomes.Sum(o => o.Records), (await host.Store.ListRecordsAsync()).Count);

        // A second pass writes nothing.
        await host.Service.RecordSeeds.EnsureSeededAsync();
        Assert.Equal(1, host.Backing.CurrentSequence - before);
    }

    [Fact]
    public async Task When_The_One_Write_Fails_Each_Set_Is_Written_On_Its_Own()
    {
        var host = Host();
        var before = host.Backing.CurrentSequence;
        host.Failing.FailNextCommit = true;

        var outcomes = await host.Service.RecordSeeds.EnsureSeededAsync();

        Assert.All(outcomes, o => Assert.True(o.Result == RecordSeedResult.Saved, $"{o.Set.Id}: {o.Result} {o.Problem}"));
        Assert.Equal(RecordSeedCatalogue.All.Count, host.Backing.CurrentSequence - before);
        Assert.Equal(outcomes.Sum(o => o.Records), (await host.Store.ListRecordsAsync()).Count);
    }

    [Fact]
    public async Task When_The_Store_Cannot_Write_Every_Set_Is_Refused_Nothing_Is_Saved_And_The_Next_Pass_Saves_Them()
    {
        var host = Host();
        host.Failing.FailEveryCommit = true;

        var refused = await host.Service.RecordSeeds.EnsureSeededAsync();

        Assert.All(refused, o => Assert.Equal(RecordSeedResult.Refused, o.Result));
        Assert.All(refused, o => Assert.StartsWith("Nothing was saved: the store could not commit", o.Problem, StringComparison.Ordinal));
        Assert.Empty(await host.Store.ListRecordsAsync());

        host.Failing.FailEveryCommit = false;
        var saved = await host.Service.RecordSeeds.EnsureSeededAsync();
        Assert.All(saved, o => Assert.Equal(RecordSeedResult.Saved, o.Result));
    }

    [Fact]
    public async Task A_Set_Is_Checked_Against_The_Sets_Planned_Before_It_In_The_Same_Pass()
    {
        // The pass writes once at the end, so a set's title check must see the sets planned before it, as it saw them in the store when each set
        // was written before the next was read: the same set planned twice in one pass is refused the second time, and nothing is written.
        var host = Host();
        var set = RecordSeedCatalogue.Find("joint-friction-mortar")!;
        var run = new RecordSeedRun(host.Service.RecordImport.Schemas(), await host.Store.ListRecordsAsync());

        var (first, firstRefusal) = await host.Service.RecordImport.PlanSeedAsync(ReferenceRecordSeeds.Request(set), ReferenceRecordSeeds.SeedPrincipal, run);
        var (second, secondRefusal) = await host.Service.RecordImport.PlanSeedAsync(ReferenceRecordSeeds.Request(set), ReferenceRecordSeeds.SeedPrincipal, run);

        Assert.Null(firstRefusal);
        Assert.NotEmpty(first);
        Assert.Empty(second);
        Assert.Contains("exists already", secondRefusal, StringComparison.Ordinal);
        Assert.Empty(await host.Store.ListRecordsAsync());
    }

    [Fact]
    public async Task After_Seeding_Only_The_Layer_Build_Up_Is_Still_A_Missing_Required_Record()
    {
        var host = Host();
        var before = await MissingRequiredAsync(host);
        Assert.Contains(("mech.snap-fit", "snapfit.friction"), before);
        Assert.Contains(("elec.conductor-sizing", "cable.ampacity"), before);

        await host.Service.RecordSeeds.EnsureSeededAsync();
        var after = await MissingRequiredAsync(host);

        // material.conductivity is a construction's layer build-up (thickness, conductivity, vapour resistance per layer), not reference data: it stays the user's.
        Assert.Equal([("bs.glaser", "material.conductivity"), ("bs.room-heat-loss", "material.conductivity")], after);
    }

    [Fact]
    public async Task A_Users_Record_With_A_Bundled_Title_Is_Left_As_It_Is_And_That_Set_Is_Not_Saved()
    {
        var host = Host();
        var set = RecordSeedCatalogue.Find("joint-friction-mortar")!;
        var users = await host.Service.RecordImport.CommitAsync(Request("51", Csv("grout[text],C_fd[-]", "Sand-cement mortar,0.3"), Provenance(set.Title, licensed: true)));
        Assert.True(users.Committed, users.Refusal);
        var mine = Assert.Single(users.Records);

        var outcomes = await host.Service.RecordSeeds.EnsureSeededAsync();

        var refused = Assert.Single(outcomes, o => o.Set.Id == set.Id);
        Assert.Equal(RecordSeedResult.Refused, refused.Result);
        Assert.All(outcomes.Where(o => o.Set.Id != set.Id), o => Assert.Equal(RecordSeedResult.Saved, o.Result));
        var stored = (await host.Store.FindRecordAsync(mine.Id))!;
        Assert.Equal(1, stored.Revision);
        Assert.Equal(0.3, stored.Values.Single(v => v.Symbol == "C_fd").Number);
        Assert.Equal("test.engineer", stored.EnteredBy);
    }

    [Fact]
    public async Task A_Bundled_Record_Fills_The_Calculator_With_Its_Pin_Like_Any_Record()
    {
        var host = Host();
        await host.Service.RecordSeeds.EnsureSeededAsync();
        var record = (await host.Store.ListRecordsAsync("joint.friction")).Single();
        var created = await host.Service.CreateAsync("struct.base-plate", await host.ProjectAsync(), "Base plate");

        var applied = await host.Service.ApplyRecordAsync(created.Id, record.Id);

        Assert.True(applied.Succeeded, applied.Reason);
        var input = applied.Instance!.Inputs.Single(i => i.Symbol == "C_fd");
        Assert.Equal(0.2, input.Number);
        Assert.Equal((record.Id, record.Revision), CalcDataRecord.PinOf(input.Provenance));
    }

    [Fact]
    public void Every_Declared_Record_Kind_Is_Either_Bundled_Or_Named_With_The_Reason_It_Is_Not()
    {
        var kinds = HostWithLifting().Service.RecordImport.Schemas().Keys.ToHashSet(StringComparer.Ordinal);
        var bundled = RecordSeedCatalogue.All.Select(s => RecordTemplates.Find(s.TemplateId)!.Kind).ToHashSet(StringComparer.Ordinal);

        var unexplained = kinds.Where(k => !bundled.Contains(k) && !RecordSeedCatalogue.NotBundled.ContainsKey(k)).ToList();
        var stale = RecordSeedCatalogue.NotBundled.Keys.Where(k => !kinds.Contains(k) || bundled.Contains(k)).ToList();
        Assert.True(unexplained.Count == 0, "Neither bundled nor explained: " + string.Join(", ", unexplained));
        Assert.True(stale.Count == 0, "Explained as not bundled, but bundled or not declared: " + string.Join(", ", stale));
    }

    private const string GeneratedStart = "<!-- generated: BundledRecordSeedTests writes this part (TEMPEST_WRITE_RECORD_SOURCES=1) -->";
    private const string GeneratedEnd = "<!-- end of the generated part -->";

    [Fact]
    public async Task The_Owner_List_Of_Record_Sources_Is_Up_To_Date()
    {
        var path = Path.Combine(RepositoryRoot, "docs", "testing", "v0.26.0-record-sources.md");
        var text = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        var start = text.IndexOf(GeneratedStart, StringComparison.Ordinal);
        var end = text.IndexOf(GeneratedEnd, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "The owner list has lost its generated-part markers.");
        var generated = await OwnerListAsync();

        if (Environment.GetEnvironmentVariable("TEMPEST_WRITE_RECORD_SOURCES") == "1")
        {
            File.WriteAllText(path, text[..(start + GeneratedStart.Length)] + "\n" + generated + text[end..]);
            return;
        }

        Assert.Equal(generated, text[(start + GeneratedStart.Length + 1)..end]);
        Assert.All(RecordSeedCatalogue.All, s => Assert.Contains($"`{s.Id}`", text, StringComparison.Ordinal));
    }

    private static async Task<string> OwnerListAsync()
    {
        var host = HostWithLifting();
        var outcomes = await host.Service.RecordSeeds.EnsureSeededAsync();
        var schemas = host.Service.RecordImport.Schemas();
        var sb = new System.Text.StringBuilder();
        static string Cell(string text) => text.Replace("|", "\\|", StringComparison.Ordinal);
        string Status(RecordSeedSet set) => set.LicenceStatus + (set.ReproducesStandard is { } std ? $"; reproduces {std}" : string.Empty);

        var total = outcomes.Sum(o => o.Records);
        sb.Append(CultureInfo.InvariantCulture, $"Bundled sets: {RecordSeedCatalogue.All.Count}. Records they make: {total}.\n\n");
        sb.Append("| Licence status | Sets | Records |\n|---|---|---|\n");

        foreach (var group in RecordSeedCatalogue.All.GroupBy(s => s.Licence).OrderBy(g => g.Key))
            sb.Append(CultureInfo.InvariantCulture, $"| {group.First().LicenceStatus} | {group.Count()} | {group.Sum(s => outcomes.Single(o => o.Set == s).Records)} |\n");

        var viaStandard = RecordSeedCatalogue.All.Where(s => s.ReproducesStandard is not null).ToList();
        sb.Append(CultureInfo.InvariantCulture, $"\nSets that reproduce or derive from a paid standard: {viaStandard.Count}, making {viaStandard.Sum(s => outcomes.Single(o => o.Set == s).Records)} records. They are all locked against export.\n\n");

        sb.Append("### Every record kind\n\n| Kind | Calculators | Need | Bundled sets | Records | Why none is bundled |\n|---|---|---|---|---|---|\n");

        foreach (var (kind, schema) in schemas.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var sets = RecordSeedCatalogue.All.Where(s => RecordTemplates.Find(s.TemplateId)!.Kind == kind).ToList();
            var records = sets.Sum(s => outcomes.Single(o => o.Set == s).Records);
            var users = schema.Users.Select(u => u.TemplateId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
            var needs = users.Select(u => (u, RecordRequirements.For(u, kind).Need)).GroupBy(x => x.Need).OrderBy(g => g.Key).Select(g => $"{g.Key} ({string.Join(", ", g.Select(x => x.u))})");
            var why = sets.Count > 0 ? string.Empty : RecordSeedCatalogue.NotBundled.GetValueOrDefault(kind, string.Empty);
            sb.Append(CultureInfo.InvariantCulture, $"| `{kind}` | {string.Join(", ", users)} | {string.Join("; ", needs)} | {(sets.Count == 0 ? "none" : string.Join(", ", sets.Select(s => $"`{s.Id}`")))} | {records} | {Cell(why)} |\n");
        }

        sb.Append("\n### Every bundled set\n\n| Set | Kind (template) | Records | Licence status | Sources |\n|---|---|---|---|---|\n");

        foreach (var set in RecordSeedCatalogue.All)
        {
            var sources = string.Join("<br>", set.Sources.Select(src => $"{Cell(src.Title)}, <{src.Url.Replace(" ", "%20", StringComparison.Ordinal)}>, read {src.Read} ({RecordSeedSet.LicenceText(src.Licence)})"));
            sb.Append(CultureInfo.InvariantCulture, $"| `{set.Id}` {Cell(set.Title)} | `{RecordTemplates.Find(set.TemplateId)!.Kind}` ({set.TemplateId}) | {outcomes.Single(o => o.Set == set).Records} | {Cell(Status(set))} | {sources} |\n");
        }

        sb.Append("\n### How each set was taken\n\n");

        foreach (var set in RecordSeedCatalogue.All)
            sb.Append(CultureInfo.InvariantCulture, $"- `{set.Id}`: {set.Basis}\n");

        return sb.ToString();
    }

    private static async Task<List<(string Template, string Kind)>> MissingRequiredAsync(LibraryTestHost host)
    {
        var missing = new List<(string, string)>();

        foreach (var (template, kind, need, _) in RecordRequirements.Entries.Where(e => e.Need == RecordNeed.Required).OrderBy(e => e.Template, StringComparer.Ordinal).ThenBy(e => e.Kind, StringComparer.Ordinal))
        {
            var requirement = (await host.Service.RecordImport.RequirementsAsync(template)).Single(r => r.Kind == kind);

            // A kind the form can fill from a built-in set (S5's EN recommended factors) still counts here: the question is whether a record exists.
            if (requirement.IsMissing && need == RecordNeed.Required)
                missing.Add((template, kind));
        }

        return missing;
    }
}
