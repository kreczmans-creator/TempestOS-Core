using System.Diagnostics;
using System.Globalization;
using System.Text;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;

namespace Tempest.Desktop.Tests.Soak;

internal static partial class SoakScenarios
{
    private const string FrameTemplate = "struct.frame-2d";

    /// <summary>
    /// The inputs CSV (<see cref="CalcCsv"/>: 1 MB, 10,000 rows including the header) at its caps and one past them, then the largest of them
    /// applied to a saved calculation. Every refusal is a block notice, not an exception, and what was read before the cap is kept.
    /// </summary>
    private static async Task InputsCsvAsync(SoakHost host, SoakScale scale, SoakOutcome outcome, CancellationToken cancellationToken)
    {
        var service = host.Service;
        var template = service.Catalogue.Get(FrameTemplate).Template;
        var rows = (int)(CalcCsv.MaxRows * scale.ImportFraction);
        var bytes = (long)(CalcCsv.MaxBytes * scale.ImportFraction);

        // At the caps: exactly the row cap (the header counts) and exactly the byte cap, with and without a final line break.
        var atCaps = SoakImportFiles.InputsCsv(rows, bytes, finalLineBreak: true);
        var watch = Stopwatch.StartNew();
        var (inputs, notices) = CalcCsv.ImportInputs(template, atCaps);
        watch.Stop();
        outcome.Expect(Encoding.UTF8.GetByteCount(atCaps) == bytes, () => $"The generated inputs CSV is {Encoding.UTF8.GetByteCount(atCaps):N0} bytes, not {bytes:N0}.");
        outcome.Expect(notices.All(n => n.Severity != CalcNoticeSeverity.Block), () => $"An inputs CSV of {rows:N0} rows in {bytes:N0} bytes (the caps are {CalcCsv.MaxRows:N0} and {CalcCsv.MaxBytes:N0}) was refused: {notices.FirstOrDefault(n => n.Severity == CalcNoticeSeverity.Block)?.Message}");
        outcome.Expect(inputs.Find("loads")?.Rows?.Count == rows - 1, () => $"The inputs CSV was read as {inputs.Find("loads")?.Rows?.Count:N0} table rows; it holds {rows - 1:N0}.");
        outcome.Fact($"Inputs CSV at the cap: {rows:N0} rows, {bytes:N0} bytes, read in (ms)", watch.Elapsed.TotalMilliseconds, "0");

        var noBreak = SoakImportFiles.InputsCsv(rows, 0, finalLineBreak: false);
        outcome.Expect(CalcCsv.ImportInputs(template, noBreak).Notices.All(n => n.Severity != CalcNoticeSeverity.Block), () => $"An inputs CSV of {rows:N0} rows with no final line break was refused.");

        // One past each cap.
        void Refused(string what, string csv, string expectedText)
        {
            var (kept, why) = CalcCsv.ImportInputs(template, csv);
            var block = why.FirstOrDefault(n => n.Severity == CalcNoticeSeverity.Block);
            outcome.Expect(block is not null && block.Message.Contains(expectedText, StringComparison.OrdinalIgnoreCase), () => $"{what}: expected a block saying '{expectedText}', got '{block?.Message ?? "none (cap not enforced)"}'.");
            outcome.Fact("Refused: " + what, block?.Message ?? "(not refused)");
            _ = kept;
        }

        Refused($"inputs CSV of {CalcCsv.MaxBytes + 1:N0} bytes (one over the 1 MB cap)", SoakImportFiles.InputsCsv(CalcCsv.MaxRows, CalcCsv.MaxBytes + 1, finalLineBreak: true), "larger than");
        Refused($"inputs CSV of {CalcCsv.MaxRows + 1:N0} rows with a final line break (one over the row cap)", SoakImportFiles.InputsCsv(CalcCsv.MaxRows + 1, 0, finalLineBreak: true), "more than");
        Refused($"inputs CSV of {CalcCsv.MaxRows + 1:N0} rows with no final line break (one over the row cap)", SoakImportFiles.InputsCsv(CalcCsv.MaxRows + 1, 0, finalLineBreak: false), "more than");

        // A cell with more than the field cap of characters is cut at the cap, not read whole.
        var (cut, _) = CalcCsv.ImportInputs(template, "symbol,value,unit\nnodes[1].support," + new string('x', 5_000) + ",\n");
        outcome.Expect(cut.Find("nodes")?.Rows?[0].Get("support")?.Text?.Length <= CalcCsv.MaxFieldLength, () => "A 5,000-character cell was read whole.");

        // The inputs of the file at the cap, applied to a saved calculation: it is saved, and read back with every row.
        var project = await host.Host.ProjectDirectory!.CreateAsync("SOAK-5-CSV", "Soak: inputs CSV");
        var baseInputs = SoakExamples.For(FrameTemplate)[0].Inputs;
        var replaced = new List<CalcInputValue>(baseInputs.Where(i => i.Symbol != "loads")) { inputs.Find("loads")! };
        var save = Stopwatch.StartNew();
        var created = await service.CreateAsync(FrameTemplate, project.Id, "Soak imported loads", replaced, cancellationToken);
        save.Stop();
        var back = await service.Store.FindInstanceAsync(created.Id, cancellationToken);
        outcome.Expect(back?.Inputs.First(i => i.Symbol == "loads").Rows?.Count == rows - 1, () => $"The saved calculation holds {back?.Inputs.First(i => i.Symbol == "loads").Rows?.Count:N0} imported rows; the file had {rows - 1:N0}.");
        outcome.Fact("Imported table saved and read back, calculation run (ms)", save.Elapsed.TotalMilliseconds, "0");
    }

    /// <summary>
    /// A two-dimensional frame with its three tables at their row caps (1,200 nodes, 2,400 elements, 2,400 loads since DC-15 and DC-21; 2,000, 4,000 and 20,000 before), run through the calculator and then saved.
    /// It need not be small or fast: it must finish inside the engine's own limits with an answer or a sentence, and saving it must be a saved calculation
    /// or a refusal that says why, never an exception.
    /// </summary>
    private static async Task FrameAtItsCapsAsync(SoakHost host, SoakScale scale, SoakOutcome outcome, CancellationToken cancellationToken)
    {
        var service = host.Service;
        var spec = service.Catalogue.Get(FrameTemplate).Template;
        var nodeCap = spec.FindInput("nodes")!.MaxRows;
        var elementCap = spec.FindInput("elements")!.MaxRows;
        var loadCap = spec.FindInput("loads")!.MaxRows;

        // A grid of 40 columns by MaxNodes/40 rows (40 × 30 = 1,200 nodes, joined by 2,330 members), pinned along the bottom row, with as many load cases of one nodal load on every node as the load cap allows (two).
        var columns = 40;
        var gridRows = nodeCap / columns;
        var scaleDown = scale.ImportFraction;
        columns = Math.Max(3, (int)(columns * Math.Sqrt(scaleDown)));
        gridRows = Math.Max(3, (int)(gridRows * Math.Sqrt(scaleDown)));
        var nodes = new List<CalcRow>();
        var members = new List<CalcRow>();
        var loads = new List<CalcRow>();
        int Id(int c, int r) => r * columns + c + 1;

        for (var r = 0; r < gridRows; r++)
        {
            for (var c = 0; c < columns; c++)
                nodes.Add(CalcRow.Of(("id", new CalcCell(Id(c, r))), ("x", new CalcCell(c * 1.0, "m")), ("y", new CalcCell(r * 1.0, "m")), ("support", new CalcCell(Text: r == 0 ? "pin" : "free"))));
        }

        CalcRow Member(int i, int j) => CalcRow.Of(("id", new CalcCell(members.Count + 1)), ("i", new CalcCell(i)), ("j", new CalcCell(j)), ("A", new CalcCell(58.7, "cm^2")), ("I", new CalcCell(4568, "cm^4")), ("rel_i", new CalcCell(Flag: false)), ("rel_j", new CalcCell(Flag: false)));

        for (var r = 0; r < gridRows; r++)
        {
            for (var c = 0; c < columns; c++)
            {
                if (c + 1 < columns)
                    members.Add(Member(Id(c, r), Id(c + 1, r)));

                if (r + 1 < gridRows)
                    members.Add(Member(Id(c, r), Id(c, r + 1)));
            }
        }

        var cases = Math.Clamp(loadCap / Math.Max(1, nodes.Count), 1, 10);

        for (var k = 1; k <= cases; k++)
        {
            foreach (var row in nodes.Take(Math.Min(nodes.Count, loadCap - loads.Count)))
            {
                var target = (int)row.Get("id")!.Number!.Value;
                loads.Add(CalcRow.Of(("case", new CalcCell(k)), ("kind", new CalcCell(Text: "nodal")), ("target", new CalcCell(target)), ("a", new CalcCell(0, "m")), ("b", new CalcCell(0, "m")), ("w1", new CalcCell(0, "kN/m")), ("w2", new CalcCell(0, "kN/m")), ("F_x", new CalcCell(k * 0.5, "kN")), ("F_y", new CalcCell(-1.0, "kN")), ("M", new CalcCell(0, "kN*m"))));
            }
        }

        var combinations = Enumerable.Range(1, cases).Select(k => CalcRow.Of(("name", new CalcCell(Text: $"C{k}")), ("case", new CalcCell(k)), ("factor", new CalcCell(1.0)))).ToList();
        var example = SoakExamples.For(FrameTemplate)[0].Inputs;
        var inputs = new List<CalcInputValue>(example.Where(i => i.Symbol is not ("nodes" or "elements" or "loads" or "combinations")))
        {
            CalcInputValue.Table("nodes", nodes, example.First(i => i.Symbol == "nodes").Provenance),
            CalcInputValue.Table("elements", members, example.First(i => i.Symbol == "elements").Provenance),
            CalcInputValue.Table("loads", loads, example.First(i => i.Symbol == "loads").Provenance),
            CalcInputValue.Table("combinations", combinations, example.First(i => i.Symbol == "combinations").Provenance),
        };

        outcome.Expect(nodes.Count <= nodeCap && members.Count <= elementCap && loads.Count <= loadCap, () => $"The generated frame ({nodes.Count:N0} nodes, {members.Count:N0} members, {loads.Count:N0} loads) is over a cap ({nodeCap:N0}, {elementCap:N0}, {loadCap:N0}).");

        // The calculator alone, with a deadline of its own.
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(TimeSpan.FromMinutes(3));
        var run = Stopwatch.StartNew();
        CalcRunResult result;

        try
        {
            result = service.Preview(FrameTemplate, new CalcInputSet(inputs), limit.Token);
        }
        catch (Exception ex)
        {
            outcome.Expect(false, () => $"Running a frame of {nodes.Count:N0} nodes threw {ex.GetType().Name}: {ex.Message}");
            return;
        }

        run.Stop();
        var answered = result.Outputs.Count > 0 || result.Blocks.Count > 0;
        outcome.Expect(answered, () => "The frame at its caps gave neither outputs nor a block.");
        outcome.Expect(result.Blocks.All(b => b.Message.Length is > 0 and < 400), () => $"The frame at its caps was blocked with a message that is not a sentence: {result.Blocks.FirstOrDefault()?.Message}");
        outcome.Fact($"Frame of {nodes.Count:N0} nodes, {members.Count:N0} members, {loads.Count:N0} loads: run (s)", run.Elapsed.TotalSeconds, "0.0");
        outcome.Fact("Frame at its caps: outcome", result.IsBlocked ? $"blocked: {result.Blocks[0].Message}" : $"{result.Outputs.Count} outputs, {result.Checks.Count} checks");

        // Saving it: a saved calculation, or a refusal that says why. The refusal is a result (C-7h), in one sentence that names the calculation by its title, and makes nothing.
        var project = await host.Host.ProjectDirectory!.CreateAsync("SOAK-5-FRAME", "Soak: frame at its caps");
        var objectsBefore = (await host.Domain.Repository.ListAllAsync(cancellationToken)).Count;
        var save = Stopwatch.StartNew();
        CalcActionResult? saved = null;
        Exception? thrown = null;

        try
        {
            saved = await service.TryCreateAsync(FrameTemplate, project.Id, "Soak frame at its caps", inputs, cancellationToken);
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        save.Stop();
        outcome.Expect(thrown is null, () => $"Saving the frame at its caps threw {thrown?.GetType().Name}: {thrown?.Message}");
        outcome.Expect(saved is { Succeeded: true } || saved is { Reason: { Length: > 0 and < 300 } reason } && reason.Contains("'Soak frame at its caps'", StringComparison.Ordinal) && !reason.Contains(project.Id.ToString("D"), StringComparison.Ordinal), () => $"Saving the frame at its caps was neither saved nor refused in a sentence naming it: {saved?.Reason}");

        if (saved is { Succeeded: false })
            outcome.Expect((await host.Domain.Repository.ListAllAsync(cancellationToken)).Count == objectsBefore && (await service.Store.ListInstancesAsync(project.Id, cancellationToken)).Count == 0, () => "The refused frame left an Evidence record, a register entry or a calculation behind.");

        outcome.Fact("Frame at its caps: saved or refused", saved is { Succeeded: true } ? $"saved ({save.Elapsed.TotalSeconds:0.0} s)" : $"refused after {save.Elapsed.TotalSeconds:0.0} s: {saved?.Reason ?? thrown?.Message}");
    }
}
