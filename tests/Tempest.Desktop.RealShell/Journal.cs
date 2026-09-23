using System.Globalization;
using System.Text;

namespace Tempest.Desktop.RealShell;

/// <summary>What the runner is willing to claim about a step.</summary>
internal enum Verdict
{
    /// <summary>Driven through real input and read back off the live visual tree.</summary>
    Verified,

    /// <summary>Driven, and the outcome follows from what was read, but the runner did not read the thing itself.</summary>
    Inferred,

    /// <summary>Driven, and the runner could not tell either way. Never counted as a pass.</summary>
    Unknown,

    /// <summary>Deliberately not driven, with the reason recorded. Never counted as a pass or a failure.</summary>
    NotExercised,

    /// <summary>Driven, and the application did not do what it says it does.</summary>
    Failed,
}

internal sealed record StepRow(
    int Ordinal,
    string Id,
    string Action,
    string Input,
    string Expected,
    string Observed,
    Verdict Verdict,
    string Screenshot,
    bool Required);

/// <summary>
/// The journey's own record: one row per step, a screenshot per step, and a
/// `journey.md` at the end. Nothing here is allowed to make a failure look
/// like a pass - a step that could not be read records Unknown, and a step
/// the application got wrong records Failed, both of which the exit code
/// honours.
/// </summary>
internal sealed class Journal(string outputDirectory, string mode)
{
    private readonly List<StepRow> _rows = [];
    private readonly List<string> _notes = [];
    private readonly DateTime _started = DateTime.Now;
    private int _ordinal;

    internal string OutputDirectory { get; } = outputDirectory;

    internal bool Failed => _rows.Any(row => row.Required && row.Verdict is Verdict.Failed or Verdict.Unknown);

    internal void Note(string note)
    {
        _notes.Add($"{Program.Now()}  {note}");
        Console.WriteLine($"      . {note}");
    }

    /// <summary>Records a step that was deliberately not driven, and why.</summary>
    internal void NotExercised(string id, string action, string reason) =>
        Add(new StepRow(++_ordinal, id, action, "-", "-", reason, Verdict.NotExercised, string.Empty, Required: false));

    internal void Fail(string id, string reason) =>
        Add(new StepRow(++_ordinal, id, "runner", "-", "-", reason, Verdict.Failed, string.Empty, Required: true));

    /// <summary>
    /// Drives one step and records what happened. The body performs the real
    /// input and returns what it could read back; the screenshot is taken
    /// after it, so the PNG shows the state the row claims.
    /// </summary>
    internal bool Step(
        string id,
        string action,
        string input,
        string expected,
        Func<(Verdict Verdict, string Observed)> body,
        bool required = true)
    {
        var ordinal = ++_ordinal;
        Console.WriteLine($"[{Program.Now()}] {ordinal,3}. {id} - {action}");

        Verdict verdict;
        string observed;

        try
        {
            (verdict, observed) = body();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            verdict = Verdict.Failed;
            observed = $"the step threw {exception.GetType().Name}: {exception.Message}";
        }

        var screenshot = $"{ordinal:D2}-{id}.png";
        if (!OsInput.Screenshot(Path.Combine(OutputDirectory, screenshot)))
            screenshot = string.Empty;

        Add(new StepRow(ordinal, id, action, input, expected, observed, verdict, screenshot, required));
        Console.WriteLine($"      -> {verdict}: {Trim(observed, 220)}");
        return verdict is Verdict.Verified or Verdict.Inferred;
    }

    private void Add(StepRow row) => _rows.Add(row);

    internal int Count(Verdict verdict) => _rows.Count(row => row.Verdict == verdict);

    internal string Summary()
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"Real-shell journey ({mode}): {_rows.Count} steps - ");
        builder.Append(CultureInfo.InvariantCulture, $"{Count(Verdict.Verified)} Verified, ");
        builder.Append(CultureInfo.InvariantCulture, $"{Count(Verdict.Inferred)} Inferred, ");
        builder.Append(CultureInfo.InvariantCulture, $"{Count(Verdict.Unknown)} Unknown, ");
        builder.Append(CultureInfo.InvariantCulture, $"{Count(Verdict.NotExercised)} not exercised, ");
        builder.Append(CultureInfo.InvariantCulture, $"{Count(Verdict.Failed)} FAILED.");
        return builder.ToString();
    }

    /// <summary>Writes `journey.md` beside the screenshots.</summary>
    internal void Write(string persistenceRoot)
    {
        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"# Real-shell journey ({mode})");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Started: {_started:yyyy-MM-dd HH:mm:ss}, finished {DateTime.Now:HH:mm:ss}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Display: `{OsInput.Display}`, X window `{OsInput.WindowId}`");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Persistence root: `{persistenceRoot}`");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- {Summary()}");
        builder.AppendLine();
        builder.AppendLine("Every action below was delivered to the application as real X11 input");
        builder.AppendLine("(`xdotool` pointer moves, button presses, key events and typed characters).");
        builder.AppendLine("The visual tree was read only to locate controls and to assert what is on");
        builder.AppendLine("screen. No service, view-model or command was called directly.");
        builder.AppendLine();
        builder.AppendLine("| # | Step | Action | Input | Expected | Observed | Verdict | Screenshot |");
        builder.AppendLine("|---|---|---|---|---|---|---|---|");

        foreach (var row in _rows)
        {
            builder.Append(CultureInfo.InvariantCulture, $"| {row.Ordinal} | {Cell(row.Id)} | {Cell(row.Action)} | {Cell(row.Input)} ");
            builder.Append(CultureInfo.InvariantCulture, $"| {Cell(row.Expected)} | {Cell(row.Observed)} | {row.Verdict}{(row.Required ? string.Empty : " (optional)")} ");
            builder.AppendLine(CultureInfo.InvariantCulture, $"| {(row.Screenshot.Length == 0 ? "-" : $"`{row.Screenshot}`")} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Runner log");
        builder.AppendLine();
        builder.AppendLine("```");
        foreach (var note in _notes)
            builder.AppendLine(note);
        builder.AppendLine("```");

        File.WriteAllText(Path.Combine(OutputDirectory, "journey.md"), builder.ToString());

        var machine = new StringBuilder();
        foreach (var row in _rows)
            machine.AppendLine(CultureInfo.InvariantCulture, $"{row.Ordinal}\t{row.Id}\t{row.Verdict}\t{Trim(row.Observed, 400).Replace('\t', ' ')}");
        File.WriteAllText(Path.Combine(OutputDirectory, "journey.tsv"), machine.ToString());
    }

    private static string Cell(string value) =>
        Trim(value, 340).Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Replace("\r", string.Empty, StringComparison.Ordinal);

    private static string Trim(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
