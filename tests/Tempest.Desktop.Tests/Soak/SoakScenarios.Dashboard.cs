using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tempest.Calc.Library;
using Tempest.Calc.Library.Dashboard;
using Tempest.Calc.Library.ManualTesting;
using DashboardOutcome = Tempest.Calc.Library.Dashboard.ManualTestOutcome;
using ManualTestOutcome = Tempest.Calc.Library.ManualTesting.ManualTestOutcome;

namespace Tempest.Desktop.Tests.Soak;

internal static partial class SoakScenarios
{
    // ------------------------------------------------------------------
    // 8. The verification dashboard (C-4): built over and over on a store with many records and many guided runs
    // ------------------------------------------------------------------

    /// <summary>The options the run service writes its documents with (the dashboard's soak writes the bulk of its runs in the same form and proves it by reading the service's own back).</summary>
    private static readonly JsonSerializerOptions RunJson = new(JsonSerializerDefaults.General)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false,
    };

    /// <summary>
    /// The dashboard is built (<see cref="SoakScale.DashboardBuilds"/> times) first on the seeded database, then after the store has been given
    /// <see cref="SoakScale.DashboardRecords"/> reference records (imported through the record import) and <see cref="SoakScale.DashboardRuns"/> finished guided
    /// runs of the whole script. Every build must give the same rows, none may take longer as they go on, and building one must write nothing. The time per
    /// build is the measurement; the time of what it reads (all the records, all the runs) is measured on its own to say where it goes.
    /// </summary>
    public static async Task DashboardAsync(SoakHost host, SoakScale scale, SoakOutcome outcome, CancellationToken cancellationToken)
    {
        var service = host.Service;
        var store = service.Store;
        var manual = host.Host.ManualTests ?? throw new InvalidOperationException("The host has no guided test service.");
        var dashboard = Tempest.Desktop.Composition.MainWindowComposer.ComposeVerificationService(service, manual);

        // ---- built on the seeded database --------------------------------------------------------------
        var empty = await BuildRepeatedlyAsync(dashboard, Math.Max(3, scale.DashboardBuilds / 5), outcome, "on the seeded database", cancellationToken);
        var rowCount = empty.Report.Rows.Count;
        outcome.Expect(rowCount == service.Catalogue.Current().Count + service.PackTemplates.Count, () => $"The dashboard has {rowCount} rows for {service.Catalogue.Current().Count} calculators and {service.PackTemplates.Count} chains.");
        outcome.Expect(!empty.Report.Truncated && empty.Report.DataProblem is null, () => $"The dashboard is truncated or has a data problem: {empty.Report.DataProblem}");
        outcome.Expect(empty.Report.Rows.All(r => r.ManualTest.Outcome == DashboardOutcome.NotTested), () => "A row shows a manual test result on a database with no guided runs.");

        // ---- many reference records, through the real import --------------------------------------------------
        var perRecordFile = SoakRecordFiles.Samples[0];
        var written = 0;
        var watch = Stopwatch.StartNew();

        for (var file = 0; written < scale.DashboardRecords; file++)
        {
            var rows = Math.Min(1000, scale.DashboardRecords - written);
            var request = new RecordImportRequest(SoakRecordFiles.Build(perRecordFile, rows), $"dashboard-{file}.csv", perRecordFile.Id, SoakProvenance($"Soak dashboard records {file:000}"));
            var committed = await service.RecordImport.CommitAsync(request, cancellationToken);
            outcome.Expect(committed.Committed, () => $"Importing the records for the dashboard (file {file}) was refused: {committed.Refusal}");

            if (!committed.Committed)
                break;

            written += committed.Records.Count;
        }

        watch.Stop();
        outcome.Fact("Reference records in the store", written);
        outcome.Fact("Seconds to import them", watch.Elapsed.TotalSeconds, "0.0");

        // ---- many guided runs: a few through the service, the rest written in the service's own form ----------
        var script = manual.Script;
        var viaService = Math.Min(3, scale.DashboardRuns);

        for (var i = 0; i < viaService; i++)
        {
            var run = await manual.StartAsync(cancellationToken);

            foreach (var step in script.Steps)
                run = await manual.RecordAsync(run.Id, step.Id, ManualTestOutcome.Pass, "Soak: passed.", "values as the form showed them", cancellationToken);

            await manual.FinishAsync(run.Id, cancellationToken);
        }

        var serviceRuns = await manual.ListAsync(cancellationToken: cancellationToken);
        var sample = serviceRuns[0];
        var rawSample = (await host.Store.ReadManyAsync(ManualTestRunService.Collection, [sample.Id.ToString("N")], cancellationToken)).Values.Single()!;
        outcome.Expect(JsonSerializer.Serialize(JsonSerializer.Deserialize<ManualTestRun>(rawSample, RunJson), RunJson) == rawSample, () => "The soak's form of a run is not the form the service writes.");

        watch.Restart();
        var bulk = scale.DashboardRuns - viaService;
        var started = DateTimeOffset.UtcNow.AddDays(-30);

        for (var batch = 0; batch < bulk; batch += 50)
        {
            var count = Math.Min(50, bulk - batch);
            var docs = Enumerable.Range(batch, count).Select(i => SyntheticRun(sample, script, started.AddMinutes(i * 7), i)).ToList();
            await host.Store.ExecuteInTransactionAsync(async (tx, token) =>
            {
                foreach (var doc in docs)
                    await tx.WriteAsync(ManualTestRunService.Collection, doc.Id.ToString("N"), JsonSerializer.Serialize(doc, RunJson), token);
            }, cancellationToken);
        }

        watch.Stop();
        var overview = await manual.OverviewAsync(cancellationToken);
        outcome.Expect(overview.Total == scale.DashboardRuns && overview.Finished == scale.DashboardRuns, () => $"The store holds {overview.Total} runs, {overview.Finished} finished; {scale.DashboardRuns} were written.");
        outcome.Fact("Finished guided runs in the store", overview.Finished);
        outcome.Fact("Steps in each run", script.Steps.Count);
        outcome.Fact("Run documents (KB each)", rawSample.Length / 1024.0, "0.0");

        // ---- what the build reads, on its own -------------------------------------------------------------
        var fingerprintBefore = await StoreFingerprintAsync(host, cancellationToken);
        var recordsRead = Stopwatch.StartNew();
        var recordCount = (await store.ListRecordsAsync(null, cancellationToken)).Count;
        recordsRead.Stop();
        var runsRead = Stopwatch.StartNew();
        var statuses = await manual.LatestByTemplateAsync(cancellationToken);
        runsRead.Stop();
        outcome.Expect(recordCount >= scale.DashboardRecords, () => $"Reading the records gives {recordCount:N0}; {scale.DashboardRecords:N0} were imported.");
        outcome.Fact("Reading all the records (ms)", recordsRead.Elapsed.TotalMilliseconds, "0");
        outcome.Fact("Reading all the runs for the manual status (ms)", runsRead.Elapsed.TotalMilliseconds, "0");

        // ---- built on the full store ------------------------------------------------------------------------
        var full = await BuildRepeatedlyAsync(dashboard, scale.DashboardBuilds, outcome, "on the full store", cancellationToken);
        outcome.Expect(full.Report.Rows.Count == rowCount, () => $"The dashboard has {full.Report.Rows.Count} rows on the full store and {rowCount} on the seeded one.");
        var tested = full.Report.Rows.Count(r => r.ManualTest.Outcome == DashboardOutcome.Passed);
        outcome.Expect(tested > 0 && tested >= statuses.Count(kv => kv.Value.StepsRecorded >= kv.Value.StepsInScript) - 1, () => $"{tested} rows show a passed manual test; the runs hold {statuses.Count} templates with results.");
        outcome.Expect(full.Report.Rows.All(r => r.MissingRecordKinds.All(k => r.RecordKinds.Contains(k))), () => "A row lists a missing record kind it does not draw on.");

        var markdown = Stopwatch.StartNew();
        var text = VerificationMarkdown.Write(full.Report, full.Report.Rows, "all rows", "TempestOS soak");
        markdown.Stop();
        var pdf = Stopwatch.StartNew();
        var pdfBytes = new Tempest.Desktop.CalcSheets.VerificationPdfRenderer().Render(full.Report, full.Report.Rows, "all rows", "TempestOS soak");
        pdf.Stop();
        outcome.Expect(text.StartsWith("# TempestOS verification dashboard", StringComparison.Ordinal) && text.Length > 2000 && pdfBytes.Length > 1000, () => $"The markdown ({text.Length} characters) or PDF ({pdfBytes.Length} bytes) export of the full dashboard is empty or wrong.");

        var fingerprintAfter = await StoreFingerprintAsync(host, cancellationToken);
        outcome.Expect(fingerprintAfter == fingerprintBefore, () => $"Building the dashboard changed the store: {fingerprintBefore} before, {fingerprintAfter} after (records, runs, calculations, a hash of the runs).");

        outcome.Fact("Dashboard rows", rowCount);
        outcome.Fact("Builds", empty.Millis.Count + full.Millis.Count);
        outcome.Fact("Build on the seeded database (mean ms)", empty.Millis.Average(), "0");
        outcome.Fact("Build on the full store: mean (ms)", full.Millis.Average(), "0");
        outcome.Fact("Build on the full store: first / median / 95th percentile / slowest (ms)", $"{full.Millis[0]:0} / {Percentile(full.Millis, 0.5):0} / {Percentile(full.Millis, 0.95):0} / {full.Millis.Max():0}");
        outcome.Fact("Dashboard export: markdown (ms), PDF (ms)", $"{markdown.Elapsed.TotalMilliseconds:0}, {pdf.Elapsed.TotalMilliseconds:0}");
        outcome.Fact("Markdown export (KB), PDF export (KB)", $"{text.Length / 1024.0:0}, {pdfBytes.Length / 1024.0:0}");
    }

    private sealed record Built(VerificationReport Report, IReadOnlyList<double> Millis);

    /// <summary>Builds the dashboard <paramref name="count"/> times; every build must give the same rows as the first, and the last few may not be slower than the first few.</summary>
    private static async Task<Built> BuildRepeatedlyAsync(VerificationDashboardService dashboard, int count, SoakOutcome outcome, string where, CancellationToken cancellationToken)
    {
        var millis = new List<double>(count);
        VerificationReport? first = null;
        string? firstHash = null;

        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var watch = Stopwatch.StartNew();
            var report = await dashboard.BuildAsync(cancellationToken);
            watch.Stop();
            millis.Add(watch.Elapsed.TotalMilliseconds);
            var hash = Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", report.Rows.Select(r => JsonSerializer.Serialize(r))))))[..16];

            if (first is null)
            {
                first = report;
                firstHash = hash;
            }
            else
                outcome.Expect(hash == firstHash, () => $"Build {i + 1} {where} gave different rows from build 1.");
        }

        var head = millis.Take(Math.Min(5, count)).Average();
        var tail = millis.TakeLast(Math.Min(5, count)).Average();
        outcome.Expect(tail <= head * 2 + 1000, () => $"The dashboard got slower with use {where}: the first builds took {head:0} ms on average, the last {tail:0} ms.");
        return new Built(first!, millis);
    }

    private static double Percentile(IReadOnlyList<double> values, double p)
    {
        var sorted = values.Order().ToList();
        return sorted[Math.Clamp((int)Math.Ceiling(p * sorted.Count) - 1, 0, sorted.Count - 1)];
    }

    /// <summary>A finished run of the whole script, in every way but its identity the shape the service writes: every step passed, a few failed, blocked or skipped.</summary>
    private static ManualTestRun SyntheticRun(ManualTestRun like, GuidedTestScript script, DateTimeOffset at, int index) =>
        like with
        {
            Id = Guid.NewGuid(),
            StartedUtc = at,
            FinishedUtc = at.AddMinutes(5),
            Results =
            [
                .. script.Steps.Select((s, n) => new ManualTestStepResult(
                    s.Id,
                    (n + index) % 97 == 0 ? ManualTestOutcome.Fail : (n + index) % 89 == 0 ? ManualTestOutcome.Blocked : (n + index) % 83 == 0 ? ManualTestOutcome.Skipped : ManualTestOutcome.Pass,
                    (n + index) % 5 == 0 ? "Soak: a note on this step." : string.Empty,
                    "values as the form showed them",
                    at.AddSeconds(n * 20))),
            ],
        };

    /// <summary>A short statement of what the store holds, to show a read changed nothing: the number of records, runs and calculations and a hash of every run's document.</summary>
    private static async Task<string> StoreFingerprintAsync(SoakHost host, CancellationToken cancellationToken)
    {
        var runs = await host.Store.ReadAllAsync(ManualTestRunService.Collection, cancellationToken);
        using var hash = SHA256.Create();

        foreach (var (key, json) in runs.OrderBy(r => r.Key, StringComparer.Ordinal))
            hash.TransformBlock(System.Text.Encoding.UTF8.GetBytes(key + json), 0, System.Text.Encoding.UTF8.GetByteCount(key + json), null, 0);

        hash.TransformFinalBlock([], 0, 0);
        var records = (await host.Service.Store.ListRecordsAsync(null, cancellationToken)).Count;
        var calculations = (await host.Service.Store.ListInstancesAsync(null, cancellationToken)).Count;
        return $"{records} records, {runs.Count} runs, {calculations} calculations, runs hash {Convert.ToHexStringLower(hash.Hash!)[..12]}";
    }
}
