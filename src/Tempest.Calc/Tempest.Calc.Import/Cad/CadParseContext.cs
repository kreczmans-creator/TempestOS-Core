using Tempest.Calc.Sheets;

namespace Tempest.Calc.Import.Cad;

/// <summary>The working state of one parse: the file, the person's settings, the budget, and what has been collected.</summary>
internal sealed class CadParseContext
{
    private const string Rule = "cad-import";

    public CadParseContext(ImportedFile file, CadParseSettings settings, ImportBudget budget)
    {
        File = file;
        Settings = settings;
        Budget = budget;
    }

    public ImportedFile File { get; }

    public CadParseSettings Settings { get; }

    public ImportBudget Budget { get; }

    public ImportLimits Limits => Settings.Limits;

    public List<CalcNotice> Notices { get; } = [];

    public List<CadParameter> Parameters { get; } = [];

    /// <summary>The CAD version the file states about itself (the person's entry wins when both exist).</summary>
    public string? FileCadVersion { get; set; }

    public string? FilePartName { get; set; }

    public string? FileRevision { get; set; }

    public string? Configuration { get; set; }

    public IReadOnlyList<string>? Configurations { get; set; }

    public void Warn(string message) => Notices.Add(new CalcNotice(Rule, CalcNoticeSeverity.Warning, message));

    public void Block(string message) => Notices.Add(new CalcNotice(Rule, CalcNoticeSeverity.Block, message));

    public void Add(CadParameter parameter)
    {
        if (Parameters.Count >= Limits.MaxParameters)
            throw new ImportRefusedException(ImportRefusalCode.TooManyParameters, $"The file holds more than {Limits.MaxParameters} parameters.");

        Parameters.Add(parameter);
    }

    public string CleanName(string? raw, string where) => CadValueParser.CleanName(raw, Limits, where);

    public CadParseResult Finish(CadSystem system, CadSourceFormat format)
    {
        var stem = Path.GetFileNameWithoutExtension(File.FileName);
        var part = Settings.PartName ?? FilePartName ?? (string.IsNullOrWhiteSpace(stem) ? null : stem);
        var source = new CadSourceInfo(
            system, format, File.FileName, File.Sha256,
            Clean(Settings.CadVersion ?? FileCadVersion, 40),
            Clean(part, 80),
            Clean(Settings.Revision ?? FileRevision, 20),
            Configuration,
            Configurations);

        return new CadParseResult(source, Parameters, Notices);
    }

    private string? Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var clean = CadValueParser.CleanText(value, Limits);
        return clean.Length > max ? clean[..max] : clean;
    }

    /// <summary>Adds the notice that decimal-mark confusion is likely when an expression looks like a number in the other notation.</summary>
    public void Hint(CadValue value, string name)
    {
        if (value.Hint is { } hint)
            Warn($"{name}: {hint}");
    }
}

internal static class CadLines
{
    /// <summary>Lines of <paramref name="text"/> with their 1-based numbers; CRLF, LF and CR all end a line.</summary>
    public static IEnumerable<(int Number, string Text)> Of(string text)
    {
        var number = 0;
        var i = 0;

        while (i <= text.Length)
        {
            var end = text.AsSpan(i).IndexOfAny('\r', '\n');
            number++;

            if (end < 0)
            {
                if (i < text.Length)
                    yield return (number, text[i..]);

                yield break;
            }

            yield return (number, text.Substring(i, end));
            i += end;

            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                i++;

            i++;
        }
    }
}
