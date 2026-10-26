using System.Diagnostics;
using Tempest.Calc.Library;
using Tempest.Calc.Library.ManualTesting;
using ManualTestOutcome = Tempest.Calc.Library.ManualTesting.ManualTestOutcome;

namespace Tempest.Desktop.Tests.Soak;

internal static partial class SoakScenarios
{
    // ------------------------------------------------------------------
    // 9. Guided test runs (C-5): started, filled in, finished, over and over to the cap, then the older ones deleted
    // ------------------------------------------------------------------

    /// <summary>The run counts at which the soak reads the whole run store and times it (those past the number of runs asked for are left out).</summary>
    private static readonly int[] GuidedReadPoints = [1, 10, 50, 100, 250, 500, 750, 1000];

    private static ManualTestOutcome GuidedOutcome(int run, int step) =>
        (run + step) % 53 == 0 ? ManualTestOutcome.Fail : (run + step) % 41 == 0 ? ManualTestOutcome.Blocked : (run + step) % 29 == 0 ? ManualTestOutcome.Skipped : ManualTestOutcome.Pass;

    /// <summary>
    /// Runs of the guided test are made through the service the screen uses: start, record a result for every step of the script (110), finish,
    /// <see cref="SoakScale.GuidedRuns"/> times (the store's cap is 1,000). At the cap another start is refused in a sentence that names the cure, and
    /// "delete older finished runs" then takes the runs beyond the newest 50 (but not one that holds the latest result of a worked example, so the
    /// dashboard's status does not change). The time of reading the whole run store is measured as the runs pile up and again after the deletion; a run
    /// belongs to the person who started it; and none of it changes a calculation or its Evidence.
    /// </summary>
    public static async Task GuidedAsync(SoakHost host, SoakScale scale, SoakOutcome outcome, CancellationToken cancellationToken)
    {
        var runs = host.Host.ManualTests ?? throw new InvalidOperationException("The host has no guided test service.");
        var service = host.Service;
        var script = runs.Script;
        var steps = script.Steps;
        outcome.Expect(steps.Count == 110, () => $"The guided script has {steps.Count} steps; the specification says 110.");

        // A calculation of the soak's own, to prove no run touches one.
        var project = await host.Host.ProjectDirectory!.CreateAsync("SOAK-9", "Soak: guided runs");
        var canary = await service.CreateAsync("mech.iso286-fit", project.Id, "Soak guided canary", SoakExamples.For("mech.iso286-fit")[0].Inputs, cancellationToken);
        var canaryBefore = await host.RawInstanceAsync(canary);
        var canaryEvidenceBefore = (await host.Domain.Repository.FindAsync(canary.EvidenceId!.Value, cancellationToken) as Tempest.Core.Evidence.Evidence)?.Status;

        var databaseBefore = SoakProbe.LogicalDatabase(host.DatabasePath).UsedBytes;
        var started = new List<double>();
        var recorded = new List<double>();
        var finished = new List<double>();
        var reads = new List<string>();
        var readTotals = new Dictionary<int, double>();
        var readPoints = GuidedReadPoints.Where(p => p <= scale.GuidedRuns).Append(scale.GuidedRuns).Distinct().ToHashSet();
        long documentBytes = 0;
        var stepsRecorded = 0;

        for (var number = 1; number <= scale.GuidedRuns; number++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var watch = Stopwatch.StartNew();
            var run = await runs.StartAsync(cancellationToken);
            started.Add(watch.Elapsed.TotalMilliseconds);

            watch.Restart();

            for (var s = 0; s < steps.Count; s++)
            {
                run = await runs.RecordAsync(run.Id, steps[s].Id, GuidedOutcome(number, s), s % 7 == 0 ? $"Soak note {number}/{s}." : null, $"values shown for {steps[s].Id}", cancellationToken);
                stepsRecorded++;
            }

            recorded.Add(watch.Elapsed.TotalMilliseconds / steps.Count);
            outcome.Expect(run.Results.Count == steps.Count, () => $"Run {number} holds {run.Results.Count} results after {steps.Count} steps were recorded.");

            watch.Restart();
            var done = await runs.FinishAsync(run.Id, cancellationToken);
            finished.Add(watch.Elapsed.TotalMilliseconds);
            outcome.Expect(done.IsFinished && done.Results.Count == steps.Count, () => $"Run {number} is not finished with all its results.");

            if (number == 1)
            {
                var again = await Record_AfterFinishAsync(runs, done, steps[0].Id);
                outcome.Expect(again, () => "A result was accepted for a run that is finished.");
                documentBytes = (await host.Store.ReadAllAsync(ManualTestRunService.Collection, cancellationToken)).Single().Value.Length;
            }

            if (readPoints.Contains(number))
            {
                var line = await MeasureReadsAsync(runs, number, outcome, cancellationToken);
                reads.Add(line.Text);
                readTotals[number] = line.OverviewMs + line.LatestMs;
                SoakScale.Progress($"9-guided: {number} runs, read {line.Text}");
            }

            if (number % 50 == 0)
                SoakScale.Progress($"9-guided: run {number} of {scale.GuidedRuns} finished");
        }


        var cap = ManualTestRunService.DefaultMaxRuns;
        var madeByService = scale.GuidedRuns;

        // The scaled-down soak cannot make a thousand runs a step at a time; it writes the rest in the service's own form, so the cap itself (never scaled) is the real one.
        if (madeByService < cap)
        {
            var like = (await runs.ListAsync(1, cancellationToken))[0];
            var topUp = cap - madeByService;
            var at = DateTimeOffset.UtcNow.AddDays(-60);

            for (var batch = 0; batch < topUp; batch += 50)
            {
                var docs = Enumerable.Range(batch, Math.Min(50, topUp - batch)).Select(i => SyntheticRun(like, script, at.AddMinutes(i * 7), i + 1000)).ToList();
                await host.Store.ExecuteInTransactionAsync(async (tx, token) =>
                {
                    foreach (var doc in docs)
                        await tx.WriteAsync(ManualTestRunService.Collection, doc.Id.ToString("N"), System.Text.Json.JsonSerializer.Serialize(doc, RunJson), token);
                }, cancellationToken);
            }

            var topped = await MeasureReadsAsync(runs, cap, outcome, cancellationToken, $"(the last {topUp} written directly, not through the service)");
            reads.Add(topped.Text);
        }

        var usedAtCap = SoakProbe.LogicalDatabase(host.DatabasePath).UsedBytes;

        // ---- the cap --------------------------------------------------------------------------------------
        var overview = await runs.OverviewAsync(cancellationToken);
        outcome.Expect(overview.Total == cap && overview.Finished == cap && overview.NewestUnfinished is null, () => $"The store holds {overview.Total} runs, {overview.Finished} finished; {cap} were made.");
        var refused = await SoakAwait.ThrowsAsync(() => runs.StartAsync(cancellationToken));
        outcome.Expect(refused is ManualTestRefusedException { Message: { Length: > 0 and < 400 } } && refused.Message.Contains("Delete older finished runs", StringComparison.Ordinal), () => $"Run {cap + 1:N0} was not refused with a sentence naming the cure: {refused?.GetType().Name}: {refused?.Message}");
        outcome.Fact($"Run {cap + 1:N0}", refused?.Message ?? "(started)");
        outcome.Expect((await runs.OverviewAsync(cancellationToken)).Total == cap, () => "A refused start changed the number of runs.");

        // ---- a run belongs to the person who started it -----------------------------------------------------
        var mine = await runs.ListAsync(1, cancellationToken);
        var statusBefore = await runs.LatestByTemplateAsync(cancellationToken);

        // ---- delete the older finished runs -----------------------------------------------------------------
        var plan = await runs.PlanTrimAsync(ManualTestRunService.DefaultKeepFinished, cancellationToken);
        outcome.Expect(plan.Finished == cap && plan.Delete.Count + plan.KeptForStatus == cap - plan.Keep, () => $"The plan to keep {plan.Keep} of {plan.Finished} deletes {plan.Delete.Count} and keeps {plan.KeptForStatus} for the status; that is not {cap - plan.Keep}.");
        outcome.Expect(plan.Delete.Count > 0, () => "The plan deletes nothing though the store is full.");
        var watchTrim = Stopwatch.StartNew();
        var deleted = await runs.DeleteOlderFinishedAsync(ManualTestRunService.DefaultKeepFinished, cancellationToken);
        watchTrim.Stop();
        outcome.Expect(deleted == plan.Delete.Count, () => $"{deleted} runs were deleted; the plan named {plan.Delete.Count}.");
        var afterTrim = await runs.OverviewAsync(cancellationToken);
        outcome.Expect(afterTrim.Total == cap - deleted, () => $"{afterTrim.Total} runs are left; {cap - deleted} were expected.");
        var statusAfter = await runs.LatestByTemplateAsync(cancellationToken);
        outcome.Expect(statusBefore.Count == statusAfter.Count && statusBefore.All(kv => statusAfter.TryGetValue(kv.Key, out var s) && s.Outcome == kv.Value.Outcome && s.RunId == kv.Value.RunId && s.StepsRecorded == kv.Value.StepsRecorded), () => "Deleting the older runs changed the manual status of a template.");
        outcome.Expect(await runs.FindAsync(mine[0].Id, cancellationToken) is not null, () => "The newest run was deleted.");

        var afterRead = await MeasureReadsAsync(runs, afterTrim.Total, outcome, cancellationToken, "after deleting the older runs");
        reads.Add(afterRead.Text);

        // The room it made is room: runs can be started again, up to the cap.
        var next = await runs.StartAsync(cancellationToken);
        outcome.Expect(!next.IsFinished, () => "A run started after the deletion is not open.");

        // The person who started a run is the only one who continues it.
        host.SignInAs(SoakHost.Checker);
        var intruder = await SoakAwait.ThrowsAsync(() => runs.RecordAsync(next.Id, steps[0].Id, ManualTestOutcome.Fail, "not mine", null, cancellationToken));
        outcome.Expect(intruder is ManualTestRefusedException, () => $"Someone else could record a result on a run: {intruder?.GetType().Name}");
        host.SignInAs(SoakHost.Author);
        await runs.FinishAsync(next.Id, cancellationToken);

        // ---- nothing a run does reaches a calculation -----------------------------------------------------
        outcome.Expect(string.Equals(canaryBefore, await host.RawInstanceAsync(canary), StringComparison.Ordinal), () => "A calculation changed while guided runs were made.");
        var canaryEvidenceAfter = (await host.Domain.Repository.FindAsync(canary.EvidenceId.Value, cancellationToken) as Tempest.Core.Evidence.Evidence)?.Status;
        outcome.Expect(canaryEvidenceBefore is not null && canaryEvidenceAfter == canaryEvidenceBefore, () => "A calculation's Evidence changed status while guided runs were made.");

        var usedAfter = SoakProbe.LogicalDatabase(host.DatabasePath);
        outcome.Fact("Runs made through the service (started, 110 steps recorded, finished)", madeByService);
        outcome.Fact("Runs on file when the cap was tried", cap);
        outcome.Fact("Steps recorded", stepsRecorded);
        outcome.Fact("Start a run (mean ms)", started.Average(), "0.0");
        outcome.Fact("Record one step (mean ms)", recorded.Average(), "0.00");
        outcome.Fact("Record one step (first 10 runs / last 10 runs, ms)", $"{recorded.Take(10).Average():0.00} / {recorded.TakeLast(10).Average():0.00}");
        outcome.Fact("Finish a run (mean ms)", finished.Average(), "0.0");
        outcome.Fact("One run's document (KB)", documentBytes / 1024.0, "0.0");
        outcome.Fact("Database the runs take at the cap (MiB)", (usedAtCap - databaseBefore) / (1024.0 * 1024), "0.0");
        outcome.Fact("Delete older finished runs: deleted, kept (the newest 50 and those holding a template's latest result)", $"{deleted}, {afterTrim.Total}");
        outcome.Fact("Delete older finished runs (ms)", watchTrim.Elapsed.TotalMilliseconds, "0");
        outcome.Fact("Database freed by the deletion (MiB, pages on the free list)", usedAfter.FreeBytes / (1024.0 * 1024), "0.0");

        foreach (var line in reads)
            outcome.Fact("Run-store read time", line);

        // Reading must not get dramatically worse than proportional to what is stored: the read at the cap against the read at a tenth of it.
        if (readTotals.Count >= 3)
        {
            var points = readTotals.OrderBy(kv => kv.Key).ToList();
            var (nLow, low) = points[1];
            var (nHigh, high) = points[^1];
            outcome.Expect(high <= low * (nHigh / (double)nLow) * 2.5 + 500, () => $"Reading {nHigh} runs took {high:0} ms against {low:0} ms for {nLow}: worse than proportional.");
        }
    }

    private sealed record ReadTimes(string Text, double OverviewMs, double LatestMs);

    /// <summary>Times what the Guided test screen and the dashboard read from the run store, with the runs counted as a check.</summary>
    private static async Task<ReadTimes> MeasureReadsAsync(ManualTestRunService runs, int expectedRuns, SoakOutcome outcome, CancellationToken cancellationToken, string? when = null)
    {
        var watch = Stopwatch.StartNew();
        var overview = await runs.OverviewAsync(cancellationToken);
        var overviewMs = watch.Elapsed.TotalMilliseconds;
        watch.Restart();
        var listed = await runs.ListAsync(ManualTestRunService.MaxListed, cancellationToken);
        var listMs = watch.Elapsed.TotalMilliseconds;
        watch.Restart();
        var latest = await runs.LatestByTemplateAsync(cancellationToken);
        var latestMs = watch.Elapsed.TotalMilliseconds;
        watch.Restart();
        var plan = await runs.PlanTrimAsync(ManualTestRunService.DefaultKeepFinished, cancellationToken);
        var planMs = watch.Elapsed.TotalMilliseconds;

        outcome.Expect(overview.Total == expectedRuns, () => $"The overview counts {overview.Total} runs; {expectedRuns} are stored {when}.");
        outcome.Expect(listed.Count == Math.Min(expectedRuns, ManualTestRunService.MaxListed), () => $"The list returns {listed.Count} runs of {expectedRuns} {when}.");
        outcome.Expect(listed.Select(r => r.StartedUtc).SequenceEqual(listed.Select(r => r.StartedUtc).OrderDescending()), () => $"The listing is not newest first {when}.");
        outcome.Expect(latest.Count > 0, () => $"No template has a latest result among {expectedRuns} finished runs {when}.");
        _ = plan;
        return new ReadTimes($"{(when is null ? $"{expectedRuns:N0} runs" : $"{expectedRuns:N0} runs {when}")}: overview {overviewMs:0} ms, newest 100 {listMs:0} ms, status of every template {latestMs:0} ms, plan to delete {planMs:0} ms", overviewMs, latestMs);
    }

    private static async Task<bool> Record_AfterFinishAsync(ManualTestRunService runs, ManualTestRun finishedRun, string stepId)
    {
        var refused = await SoakAwait.ThrowsAsync(() => runs.RecordAsync(finishedRun.Id, stepId, ManualTestOutcome.Pass, null, null));
        return refused is ManualTestRefusedException;
    }
}
