using System.Text.RegularExpressions;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Library;
using static Tempest.Calc.Tests.Library.RecordImport.RecordImportTestSupport;

namespace Tempest.Calc.Tests.Library.RecordImport;

/// <summary>
/// Addendum C, C-3: the importer's catalogue of Track P's record templates is held to Track P's files and to the calculators. If a template file's
/// header changes, a kind is renamed, or a calculator starts to declare a proposed kind, one of these fails and says which.
/// </summary>
public sealed class RecordTemplateContractTests
{
    private static IEnumerable<string> TemplateFiles() =>
        Directory.GetFiles(TemplateFolder, "*.csv").Where(f => Regex.IsMatch(Path.GetFileName(f), @"^\d\d-")).OrderBy(f => f, StringComparer.Ordinal);

    [Fact]
    public void Every_Track_P_Template_File_Has_A_Catalogue_Entry_With_The_Same_Header()
    {
        var files = TemplateFiles().ToList();
        Assert.Equal(82, files.Count);
        Assert.Equal(files.Count, RecordTemplates.All.Count);

        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var template = RecordTemplates.Find(name);
            Assert.True(template is not null, $"{name} has no entry in RecordTemplates.");
            Assert.Equal(name, template!.FileName);
            Assert.Equal(System.IO.File.ReadAllLines(file)[0].Trim(), template.Header);
            Assert.True(System.IO.File.Exists(Path.ChangeExtension(file, ".md")), $"{name} has no .md note.");
        }
    }

    [Fact]
    public void The_Template_Files_Hold_A_Header_And_No_Data_Rows()
    {
        foreach (var file in TemplateFiles())
            Assert.Single(System.IO.File.ReadAllLines(file), l => l.Trim().Length > 0);
    }

    [Fact]
    public void Template_Kinds_Agree_With_The_Readme_Kind_Name_Map()
    {
        var readme = System.IO.File.ReadAllText(Path.Combine(TemplateFolder, "README.md"));
        var map = readme[readme.IndexOf("## Kind name map", StringComparison.Ordinal)..];
        map = map[..map.IndexOf("## CSV conventions", StringComparison.Ordinal)];
        var rows = Regex.Matches(map, @"^\| `(?<id>\d\d)-(?<name>[^`]+)` \| (?<kind>[^|]+) \|", RegexOptions.Multiline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        Assert.Equal(82, rows.Count);

        foreach (Match row in rows)
        {
            var template = RecordTemplates.Find(row.Groups["id"].Value)!;
            Assert.Equal(row.Groups["name"].Value, template.Name);
            var cell = row.Groups["kind"].Value.Trim();

            if (cell.StartsWith("none", StringComparison.Ordinal))
            {
                Assert.Equal(RecordTemplateDisposition.Superseded, template.Disposition);
                continue;
            }

            var kind = Regex.Match(cell, @"^`(?<kind>[^`]+)`", RegexOptions.None, TimeSpan.FromSeconds(2)).Groups["kind"].Value;
            Assert.Equal(kind, template.Kind);
            Assert.Equal(cell.Contains("proposed", StringComparison.OrdinalIgnoreCase) ? RecordTemplateDisposition.Proposed : RecordTemplateDisposition.Kind, template.Disposition);
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
    public void Templates_Without_A_Record_Kind_And_Kinds_Without_A_Template_Are_Exactly_The_Known_Gaps()
    {
        var host = Host();
        var schemas = host.Service.RecordImport.Schemas();

        // Proposed kinds: no calculator declares them. When one does, this fails and the template becomes importable.
        var proposed = RecordTemplates.All.Where(t => t.Disposition == RecordTemplateDisposition.Proposed).Select(t => t.Id).Order().ToList();
        Assert.Equal(["03", "05", "19", "42", "43", "44", "48"], proposed);
        Assert.All(RecordTemplates.All.Where(t => t.Disposition == RecordTemplateDisposition.Proposed), t => Assert.DoesNotContain(t.Kind, schemas.Keys));
        Assert.Equal(["09", "35", "36", "37"], RecordTemplates.All.Where(t => t.Disposition == RecordTemplateDisposition.Superseded).Select(t => t.Id).Order());

        // Every other template fills a kind a visible calculator declares.
        foreach (var template in RecordTemplates.All.Where(t => t.Disposition == RecordTemplateDisposition.Kind))
            Assert.True(schemas.ContainsKey(template.Kind), $"{template.FileName} fills '{template.Kind}', which no visible calculator declares.");

        // Kinds with no template at all.
        var covered = RecordTemplates.All.Where(t => t.Disposition == RecordTemplateDisposition.Kind).Select(t => t.Kind).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(["cable.impedance", "motor.rating", "motor.start", "starter.limits", "transformer.data"], schemas.Keys.Where(k => !covered.Contains(k)).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_Header_Only_File_Of_Every_Importable_Template_Maps_Every_Column_And_Passes_Its_Unit_Check()
    {
        var host = Host();
        var service = host.Service.RecordImport;
        var importable = service.ImportableTemplates();
        Assert.Equal(82 - 7 - 4, importable.Count);

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

        var proposed = await service.PreviewAsync(Request("03", Utf8("device[text],t_disc[s],I_n[A],Z_s_max[ohm]\r\nx,0.4,32,1.2\r\n")));
        Assert.Contains("bs7671.max-zs", proposed.Refusal, StringComparison.Ordinal);
        Assert.Contains("no calculator declares", proposed.Refusal, StringComparison.Ordinal);

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
}
