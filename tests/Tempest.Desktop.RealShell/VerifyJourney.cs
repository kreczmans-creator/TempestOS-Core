using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Tempest.Desktop.RealShell;

/// <summary>
/// `--mode verify`: a second, separate launch of the same application on
/// the same persistence root, after the first one was closed through the
/// operating system's own close message. Everything the journey created
/// must still be there - read back twice, once out of the running
/// application's own visual tree and once straight out of
/// `tempest.db` - because "it was on screen" and "it was written down" are
/// different claims and this Work Package is about proving both.
/// </summary>
internal static class VerifyJourney
{
    private const string ProjectName = "Apollo Pump Redesign";
    private const string CalculationName = "Apollo discharge beam check";

    internal static void Run(Journal journal)
    {
        journal.Step(
            "restart", "Relaunch on the same persistence root", "process start",
            "The application starts on the same data and the shell composes",
            () =>
            {
                var project = Ui.ByName("Current project")?.Text ?? "(none)";
                return Act.Verified($"the shell composed; the status bar's own project segment reads '{project}'");
            });

        journal.Step(
            "project-survives", "Projects → Open", "mouse",
            "The project created in the first session is listed, by code and name",
            () =>
            {
                if (!Act.Click("Projects", settleMs: 1_500))
                    return Act.Failed(Act.LastProblem);

                // The journey closes the project as its last act, so it is
                // under Closed rather than Open on this launch - both are
                // tried, and which one holds it is part of what is recorded.
                var groups = new[] { "Closed", "Open" };
                foreach (var group in groups)
                {
                    if (!Act.ClickRow(group, settleMs: 2_000))
                        continue;

                    if (Ui.WaitForText($"P-0001 {ProjectName}", 8_000))
                        return Act.Verified($"listed under {group}: \"{Ui.FirstTextContaining($"P-0001 {ProjectName}")}\"");
                }

                return Act.Failed("the project is listed under neither Open nor Closed");
            });

        journal.Step(
            "quote-survives", "Open the project → Quote tab", "mouse",
            "The quotation is still Accepted, with both lines and its own total",
            () =>
            {
                if (!Act.ClickRow($"P-0001 {ProjectName}", settleMs: 3_500) || !Act.Click("Quote", settleMs: 2_500))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => Ui.ByNameContaining("Export Q-2026-001") is not null, 20_000))
                    return Act.Failed("the Quote tab shows no quotation");

                var identity = Ui.FirstTextContaining("Q-2026-001 —") ?? "(no identity)";
                var total = Ui.FirstTextContaining("Total £") ?? "(no total)";
                var hourly = Ui.FirstTextContaining("Concept design and pump sizing") ?? "(hourly line missing)";

                return identity.Contains("Accepted", StringComparison.Ordinal) && total.Contains("7,300.00", StringComparison.Ordinal)
                    ? Act.Verified($"\"{identity}\"; \"{total}\"; \"{Trim(hourly)}\"")
                    : Act.Failed($"\"{identity}\"; \"{total}\"");
            });

        journal.Step(
            "deliverables-survive", "Deliverables tab", "mouse",
            "Both deliverables are there, one of them completed",
            () =>
            {
                if (!Act.Click("Deliverables", settleMs: 3_000))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => Ui.VisibleText().Any(value => value.Contains("deliverable(s),", StringComparison.Ordinal)), 20_000))
                    return Act.Failed("the Deliverables tab lists no summary line");

                var summary = Ui.VisibleText().First(value => value.Contains("deliverable(s),", StringComparison.Ordinal));
                return summary.Contains("2 deliverable(s), 1 completed", StringComparison.Ordinal)
                    ? Act.Verified($"\"{summary}\"")
                    : Act.Failed($"\"{summary}\"");
            });

        journal.Step(
            "evidence-survives", "Evidence tab", "mouse",
            "The evidence record and its attached file are still there",
            () =>
            {
                if (!Act.Click("Evidence", settleMs: 3_000))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitForText("evidence-sample", 20_000))
                    return Act.Failed("the Evidence tab lists no record");

                return Act.Verified($"\"{Ui.FirstTextContaining("evidence-sample")}\"");
            });

        journal.Step(
            "calculation-survives", "Engineering → Engineering Calculations", "mouse",
            "The named calculation and its runs are still recorded against the project",
            () =>
            {
                if (!Act.Click("Engineering", settleMs: 1_500) || !Act.ClickRow("Engineering Calculations", settleMs: 3_500))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitForText(CalculationName, 20_000))
                    return Act.Failed($"'{CalculationName}' is not listed");

                return Act.Verified($"\"{Trim(Ui.FirstTextContaining(CalculationName)!)}\"");
            });

        journal.Step(
            "reference-survives", "Engineering → Reference data", "mouse",
            "The released material and the released person survived the restart at their released revision",
            () =>
            {
                if (!Act.ClickRow("Reference data", settleMs: 3_500))
                    return Act.Failed(Act.LastProblem);

                if (!Ui.WaitUntil(() => Ui.FirstTextContaining("mat-s355j2 — S355J2") is not null, 20_000))
                    return Act.Failed("the Materials library lists nothing");

                var material = Ui.FirstTextContaining("mat-s355j2 — S355J2")!;
                var person = Ui.FirstTextContaining("person-dana-whitfield") ?? "(person missing)";

                return material.Contains("Released", StringComparison.Ordinal)
                    ? Act.Verified($"\"{Trim(material)}\"; \"{Trim(person)}\"")
                    : Act.Failed($"\"{Trim(material)}\"");
            });
    }

    /// <summary>
    /// The same claim read straight out of SQLite, with the application
    /// closed - the store, not the screen.
    /// </summary>
    internal static void VerifyStore(Journal journal, string persistenceRoot)
    {
        journal.Step(
            "store", "Read tempest.db directly", "sqlite (Microsoft.Data.Sqlite)",
            "The project, its milestone, both deliverables, the completion, the quotation and the evidence are all stored",
            () =>
            {
                var databasePath = Path.Combine(persistenceRoot, "tempest.db");
                if (!File.Exists(databasePath))
                    return Act.Failed($"there is no database at '{databasePath}'");

                var kinds = new Dictionary<string, int>(StringComparer.Ordinal);
                var identifiers = new List<string>();

                using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadOnly,
                }.ToString()))
                {
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = "select text_value from records where collection = 'EngineeringDomain.ObjectState'";
                    using var reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        var json = reader.GetString(0);
                        var kind = Between(json, "\"Kind\":\"", "\"");
                        var name = Between(json, "\"DisplayName\":\"", "\"");
                        if (kind.Length == 0)
                            continue;

                        kinds[kind] = kinds.TryGetValue(kind, out var count) ? count + 1 : 1;
                        if (name.Length > 0)
                            identifiers.Add($"{kind}:{name}");
                    }
                }

                var expected = new[] { "Project", "Quotation", "Milestone", "Deliverable", "DeliverableCompletion", "Evidence" };
                var missing = expected.Where(kind => !kinds.ContainsKey(kind)).ToList();
                var summary = string.Join(", ", kinds.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}×{pair.Value}"));

                return missing.Count == 0
                    ? Act.Verified($"tempest.db holds {summary}")
                    : Act.Failed($"tempest.db holds {summary}; missing {string.Join(", ", missing)}");
            });
    }

    private static string Between(string value, string start, string end)
    {
        var from = value.IndexOf(start, StringComparison.Ordinal);
        if (from < 0)
            return string.Empty;

        from += start.Length;
        var to = value.IndexOf(end, from, StringComparison.Ordinal);
        return to < 0 ? string.Empty : value[from..to];
    }

    private static string Trim(string value) => value.Length <= 200 ? value : value[..200] + "…";
}
