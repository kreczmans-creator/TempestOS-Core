using System.Text.RegularExpressions;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using static Tempest.Calc.Tests.Library.RecordImport.RecordImportTestSupport;

namespace Tempest.Calc.Tests.Library.RecordImport;

/// <summary>
/// Addendum C, C-3: the importer's catalogue of record templates is held to the template files and to the calculators. Templates 01 to 82 are
/// Track P's (<c>docs/verification/record-templates</c>); 83 to 90 are Track S's (<c>docs/calcs/record-templates</c>, DC-10). If a template file's
/// header changes, a kind is renamed, or a calculator starts or stops declaring a kind, one of these fails and says which.
/// </summary>
public sealed class RecordTemplateContractTests
{
    private const int TrackPTemplates = 82;
    private const int TrackSTemplates = 8;

    /// <summary>The templates whose kind the lifting calculator declares: importable only while the lifting flag is on (ADR-0173).</summary>
    private static readonly string[] LiftingTemplates = ["88", "89", "90"];

    private static IEnumerable<string> TemplateFiles(string folder) =>
        Directory.GetFiles(folder, "*.csv").Where(f => Regex.IsMatch(Path.GetFileName(f), @"^\d\d-")).OrderBy(f => f, StringComparer.Ordinal);

    [Fact]
    public void Every_Template_File_Has_A_Catalogue_Entry_With_The_Same_Header()
    {
        var trackP = TemplateFiles(TemplateFolder).ToList();
        var trackS = TemplateFiles(TrackSTemplateFolder).ToList();
        Assert.Equal(TrackPTemplates, trackP.Count);
        Assert.Equal(TrackSTemplates, trackS.Count);
        Assert.Equal(trackP.Count + trackS.Count, RecordTemplates.All.Count);

        foreach (var file in trackP.Concat(trackS))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var template = RecordTemplates.Find(name);
            Assert.True(template is not null, $"{name} has no entry in RecordTemplates.");
            Assert.Equal(name, template!.FileName);
            Assert.Equal(System.IO.File.ReadAllLines(file)[0].Trim(), template.Header);
            Assert.True(System.IO.File.Exists(Path.ChangeExtension(file, ".md")), $"{name} has no .md note.");
            Assert.Equal(Path.GetFullPath(Path.Combine(RepositoryRoot, template.Folder)), Path.GetFullPath(Path.GetDirectoryName(file)!));
        }

        Assert.All(RecordTemplates.All, t => Assert.Equal(string.CompareOrdinal(t.Id, "82") <= 0, t.IsTrackP));
    }

    [Fact]
    public async Task Every_Number_That_Is_Stored_And_Never_Applied_Has_A_Hard_Range_With_Both_Ends()
    {
        // Review finding 4: a stored-but-unapplied column (theta_a, t_min, altitude, ...) is read by no calculator, so no calculator limit judges it;
        // the template must, or 1e300 would be saved as a temperature.
        var service = HostWithLifting().Service.RecordImport;
        var missing = new List<string>();

        foreach (var template in service.ImportableTemplates())
        {
            var preview = await service.PreviewAsync(Request(template.Id, Utf8(RecordImportService.BlankTemplateCsv(template))));

            foreach (var column in preview.Columns.Where(c => c.Role == RecordColumnRole.Extra && c.TemplateSymbol is not null))
            {
                var hard = template.Checks.Any(c => c.Kind == RecordCheckKind.Range && c.Severity == RecordSeverity.Error && c.Columns.Contains(column.TemplateSymbol!) && c.Lo is not null && c.Hi is not null);

                if (!hard)
                    missing.Add($"{template.Id}:{column.TemplateSymbol}");
            }
        }

        Assert.True(missing.Count == 0, "No hard range on: " + string.Join(", ", missing));
    }

    [Fact]
    public void The_Template_Files_Hold_A_Header_And_No_Data_Rows()
    {
        foreach (var file in TemplateFiles(TemplateFolder).Concat(TemplateFiles(TrackSTemplateFolder)))
            Assert.Single(System.IO.File.ReadAllLines(file), l => l.Trim().Length > 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Template_Kinds_Agree_With_The_Readme_Kind_Name_Map(bool trackP)
    {
        // Track P's README is read-only and still says "proposed" for the kinds Track S built under DC-10: a proposed cell therefore accepts the
        // template as built (Kind) as well as still proposed; the kind name must agree either way. Track S's README is held exactly.
        var readme = System.IO.File.ReadAllText(Path.Combine(trackP ? TemplateFolder : TrackSTemplateFolder, "README.md"));
        var map = readme[readme.IndexOf("## Kind name map", StringComparison.Ordinal)..];
        map = map[..map.IndexOf("## CSV conventions", StringComparison.Ordinal)];
        var rows = Regex.Matches(map, @"^\| `(?<id>\d\d)-(?<name>[^`]+)` \| (?<kind>[^|]+) \|", RegexOptions.Multiline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        Assert.Equal(trackP ? TrackPTemplates : TrackSTemplates, rows.Count);

        foreach (Match row in rows)
        {
            var template = RecordTemplates.Find(row.Groups["id"].Value)!;
            Assert.Equal(row.Groups["name"].Value, template.Name);
            Assert.Equal(trackP, template.IsTrackP);
            var cell = row.Groups["kind"].Value.Trim();

            if (cell.StartsWith("none", StringComparison.Ordinal))
            {
                Assert.Equal(RecordTemplateDisposition.Superseded, template.Disposition);
                continue;
            }

            var kind = Regex.Match(cell, @"^`(?<kind>[^`]+)`", RegexOptions.None, TimeSpan.FromSeconds(2)).Groups["kind"].Value;
            Assert.Equal(kind, template.Kind);

            if (cell.Contains("proposed", StringComparison.OrdinalIgnoreCase))
                Assert.True(template.Disposition is RecordTemplateDisposition.Proposed or RecordTemplateDisposition.Kind, $"{template.FileName} is {template.Disposition}");
            else
                Assert.Equal(RecordTemplateDisposition.Kind, template.Disposition);
        }
    }

    [Fact]
    public void Every_Check_Names_A_Column_Of_Its_Template_And_Every_Header_Unit_Is_Understood()
    {
        foreach (var template in RecordTemplates.All)
        {
            var symbols = template.Columns.Select(c => c.Symbol).ToList();
            Assert.Equal(symbols.Count, symbols.Distinct(StringComparer.Ordinal).Count());

            foreach (var check in template.Checks)
            {
                foreach (var name in check.Columns.Concat(check.Group))
                    Assert.True(symbols.Contains(name), $"{template.FileName}: check {check.Kind} names '{name}', which is not in the header.");
            }

            foreach (var column in template.Columns.Where(c => !c.IsText && c.EngineUnit.Length > 0))
                Assert.True(UnitCatalog.TryParse(column.EngineUnit, out _, out var error), $"{template.FileName}: unit '{column.Unit}' ({column.EngineUnit}) is not understood: {error}");
        }
    }

    [Fact]
    public void A_Mapped_Column_Names_A_Kind_Symbol_Its_Calculator_Takes_And_Nothing_Else_Does()
    {
        // DC-10: Track P wrote 42 and 43 under the standard's symbols (theta_i, n_ach) where B4 takes T_int and n. The map is the only place a column
        // fills a symbol of another name, and the symbol it names is one the kind's calculator takes.
        var schemas = HostWithLifting().Service.RecordImport.Schemas();
        var mapped = RecordTemplates.All.SelectMany(t => t.Checks.Where(c => c.Kind == RecordCheckKind.MapsTo).Select(c => (Template: t, Column: c.Columns[0], Symbol: c.Text!))).ToList();
        Assert.Equal([("42", "theta_i", "T_int"), ("43", "n_ach", "n")], mapped.Select(m => (m.Template.Id, m.Column, m.Symbol)));

        foreach (var (template, column, symbol) in mapped)
        {
            Assert.Equal(symbol, template.TargetSymbol(column));
            Assert.Contains(symbol, schemas[template.Kind].Symbols.Select(s => s.Symbol));
        }

        Assert.All(RecordTemplates.All, t => Assert.All(t.Columns.Where(c => !mapped.Any(m => m.Template == t && m.Column == c.Symbol)), c => Assert.Equal(c.Symbol, t.TargetSymbol(c.Symbol))));
    }

    [Fact]
    public void Templates_Without_A_Record_Kind_And_Kinds_Without_A_Template_Are_Exactly_The_Known_Gaps()
    {
        var visible = Host().Service.RecordImport.Schemas();
        var withLifting = HostWithLifting().Service.RecordImport.Schemas();

        // DC-10 built the kinds of 03, 05, 19, 42, 43 and 44 (E5, E3, S6 and B4). 48 stays proposed: the surface resistances are cells of the B4
        // elements table, which is the room's geometry and not record data (Black B-A-10), and the engine binds a record to a whole input.
        var proposed = RecordTemplates.All.Where(t => t.Disposition == RecordTemplateDisposition.Proposed).Select(t => t.Id).Order().ToList();
        Assert.Equal(["48"], proposed);
        Assert.All(RecordTemplates.All.Where(t => t.Disposition == RecordTemplateDisposition.Proposed), t => Assert.DoesNotContain(t.Kind, withLifting.Keys));
        Assert.Equal(["09", "35", "36", "37"], RecordTemplates.All.Where(t => t.Disposition == RecordTemplateDisposition.Superseded).Select(t => t.Id).Order());

        // Every other template fills a kind a visible calculator declares; the three lifting templates fill kinds the lifting calculator declares.
        foreach (var template in RecordTemplates.All.Where(t => t.Disposition == RecordTemplateDisposition.Kind))
        {
            Assert.True(withLifting.ContainsKey(template.Kind), $"{template.FileName} fills '{template.Kind}', which no calculator declares.");
            Assert.Equal(LiftingTemplates.Contains(template.Id), !visible.ContainsKey(template.Kind));
        }

        // Kinds with no template at all: none since DC-10 (motor.rating, cable.impedance, motor.start, starter.limits, transformer.data and the three lifting kinds have 83 to 90).
        var covered = RecordTemplates.All.Where(t => t.Disposition == RecordTemplateDisposition.Kind).Select(t => t.Kind).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(withLifting.Keys, k => !covered.Contains(k));
        string[] motorTemplates = ["83", "84", "85", "86", "87"];
        Assert.Equal(["cable.impedance", "motor.rating", "motor.start", "starter.limits", "transformer.data"], motorTemplates.Select(id => RecordTemplates.Find(id)!.Kind).Order(StringComparer.Ordinal));
        Assert.Equal(["lifting.allowable-stress", "lifting.design-factors", "lifting.limits"], LiftingTemplates.Select(id => RecordTemplates.Find(id)!.Kind).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_Header_Only_File_Of_Every_Importable_Template_Maps_Every_Column_And_Passes_Its_Unit_Check()
    {
        var host = HostWithLifting();
        var service = host.Service.RecordImport;
        var importable = service.ImportableTemplates();
        Assert.Equal(TrackPTemplates + TrackSTemplates - 1 - 4, importable.Count);
        Assert.Equal(importable.Count - LiftingTemplates.Length, Host().Service.RecordImport.ImportableTemplates().Count);

        foreach (var template in importable)
        {
            var preview = await service.PreviewAsync(Request(template.Id, Utf8(template.Header + "\r\n")));
            Assert.True(preview.Refusal is null, $"{template.FileName}: {preview.Refusal}");
            Assert.Equal(template.Columns.Count, preview.Columns.Count);
            Assert.All(preview.Columns, c => Assert.True(c.Role != RecordColumnRole.Ignored, $"{template.FileName}: column {c.Header} is left out."));
            Assert.All(preview.Columns, c => Assert.True(c.Unit is RecordUnitCheck.Match or RecordUnitCheck.Convertible or RecordUnitCheck.NotApplicable, $"{template.FileName}: {c.Header}: {c.UnitMessage}"));

            // The only error a header-only file has is that it holds no rows (a template that needs a row count says so as well).
            Assert.All(preview.Errors, e => Assert.True(e.Message.Contains("no data rows", StringComparison.Ordinal) || e.Message.Contains("data rows", StringComparison.Ordinal), $"{template.FileName}: {e}"));
            Assert.False(preview.CanCommit);

            // No label or extra column sits in a table template: the importer reads the table's columns and nothing else.
            if (preview.Columns.Any(c => c.Role == RecordColumnRole.TableColumn))
                Assert.DoesNotContain(preview.Columns, c => c.Role is RecordColumnRole.Label or RecordColumnRole.Extra);
        }
    }

    [Fact]
    public async Task A_Superseded_Or_Proposed_Template_Is_Refused_With_The_Reason_And_Nothing_Is_Saved()
    {
        var host = Host();
        var service = host.Service.RecordImport;

        var superseded = await service.PreviewAsync(Request("09", Utf8("C_c[-]\r\n0.9\r\n")));
        Assert.Contains("Superseded", superseded.Refusal, StringComparison.Ordinal);
        Assert.Contains("derives C_c", superseded.Refusal, StringComparison.Ordinal);

        var proposed = await service.PreviewAsync(Request("48", Utf8("Rsi[m2*K/W],Rse[m2*K/W],Rg[m2*K/W]\r\n0.13,0.04,\r\n")));
        Assert.Contains("surface.resistances", proposed.Refusal, StringComparison.Ordinal);
        Assert.Contains("no calculator declares", proposed.Refusal, StringComparison.Ordinal);
        Assert.Contains("B-A-10", proposed.Refusal, StringComparison.Ordinal);

        // A lifting template is importable only while the lifting flag is on: without it the kind is declared by no visible calculator.
        var hidden = await service.PreviewAsync(Request("88", Utf8("design_category[text],service_class[text],Nd[-],k_svc[-]\r\nA,1,2,1\r\n")));
        Assert.Contains("lifting.design-factors", hidden.Refusal, StringComparison.Ordinal);
        Assert.Contains("No calculator available in this build", hidden.Refusal, StringComparison.Ordinal);

        var unknown = await service.PreviewAsync(Request("99", Utf8("x\r\n")));
        Assert.Contains("no record template '99'", unknown.Refusal, StringComparison.Ordinal);

        Assert.Empty(await host.Store.ListRecordsAsync());
    }

    [Fact]
    public void The_Requirements_Table_Names_Real_Templates_And_Kinds_Those_Templates_Declare()
    {
        var catalogue = Host().Catalogue;

        foreach (var (templateId, kind, _, reason) in RecordRequirements.Entries)
        {
            var entry = catalogue.Current().FirstOrDefault(e => e.Template.Id == templateId);
            Assert.True(entry is not null, $"RecordRequirements names '{templateId}', which is not a calculator.");
            Assert.Contains(entry!.Template.RecordKinds ?? [], k => k.Kind == kind);
            Assert.NotEmpty(reason);
        }
    }

    [Fact]
    public void Every_New_Kind_Is_Titled_By_Its_Noun_And_Carries_No_Value()
    {
        // DC-10: a kind carries field names, units and the source to cite, never a value from a licensed standard; every template of a new kind says what to copy from and holds no data row.
        var schemas = HostWithLifting().Service.RecordImport.Schemas();

        foreach (var id in new[] { "03", "05", "19", "42", "43", "44", "83", "84", "85", "86", "87", "88", "89", "90" })
        {
            var template = RecordTemplates.Find(id)!;
            Assert.Equal(RecordTemplateDisposition.Kind, template.Disposition);
            var schema = schemas[template.Kind];
            Assert.NotEmpty(schema.Symbols);
            Assert.NotEmpty(schema.Users);
            Assert.Single(System.IO.File.ReadAllLines(Path.Combine(RepositoryRoot, template.Folder, template.FileName + ".csv")), l => l.Trim().Length > 0);
            var note = System.IO.File.ReadAllText(Path.Combine(RepositoryRoot, template.Folder, template.FileName + ".md"));
            Assert.Contains("## Copy from", note, StringComparison.Ordinal);
            Assert.Contains("## Importer checks", note, StringComparison.Ordinal);
        }
    }
}
