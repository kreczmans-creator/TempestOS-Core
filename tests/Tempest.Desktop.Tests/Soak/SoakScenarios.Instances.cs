using System.Diagnostics;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;

namespace Tempest.Desktop.Tests.Soak;

internal static partial class SoakScenarios
{
    // ------------------------------------------------------------------
    // 1. Instances: create, run and save across every template, from the worked examples
    // ------------------------------------------------------------------

    /// <summary>
    /// Creates <see cref="SoakScale.Instances"/> calculations round the whole catalogue (every one of the 54 templates, from its shipped worked
    /// example), runs each again and saves it again, then reads them all back: every one must have run without a block, must give the same
    /// outputs when run again as it saved, must come back from the store intact, and none may be reported stale.
    /// </summary>
    public static async Task InstancesAsync(SoakHost host, SoakScale scale, SoakOutcome outcome, CancellationToken cancellationToken)
    {
        var service = host.Service;
        var project = await host.Host.ProjectDirectory!.CreateAsync("SOAK-1", "Soak: calculation instances");
        var entries = service.Catalogue.Current();
        outcome.Expect(entries.Count == 54, () => $"The catalogue offers {entries.Count} templates with every optional calculator on; the specification says 54.");

        // Some worked examples cite a shipped indicative material by its record pin. The library seeds itself the first time a screen
        // asks it for a material, so a calculation made from such an example before then cites a record that does not exist yet: it must be
        // reported stale, for that reason, and be fresh as soon as the library is seeded (a stale flag that is right in both directions).
        var plate = SoakExamples.For("struct.plate-bending")[0];
        var earlyProject = await host.Host.ProjectDirectory!.CreateAsync("SOAK-1-E", "Soak: before the library is seeded");
        var early = await service.CreateAsync("struct.plate-bending", earlyProject.Id, "Soak before the library is seeded", plate.Inputs, cancellationToken);
        var beforeSeed = await service.StalenessAsync(early, cancellationToken);
        outcome.Expect(beforeSeed.IsStale && beforeSeed.Reasons[0].Contains("reference record", StringComparison.Ordinal), () => $"A calculation citing a material record that is not in the library was not reported stale for that reason ({string.Join("; ", beforeSeed.Reasons)}).");
        await service.Materials.EnsureSeededAsync(cancellationToken);
        var afterSeed = await service.StalenessAsync(early, cancellationToken);
        outcome.Expect(!afterSeed.IsStale, () => $"The calculation is still stale after the library was seeded: {string.Join("; ", afterSeed.Reasons)}");

        var perTemplate = new Dictionary<string, int>(StringComparer.Ordinal);
        var saved = new List<(Guid Id, string Hash, int Outputs)>(scale.Instances);
        var databaseBefore = DatabaseBytes(host);
        var create = new Stopwatch();
        var run = new Stopwatch();
        var update = new Stopwatch();

        for (var i = 0; i < scale.Instances; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = entries[i % entries.Count];
            var templateId = entry.Template.Id;
            var examples = SoakExamples.For(templateId);

            if (examples.Count == 0)
            {
                outcome.Expect(false, () => $"Template {templateId} has no worked example to create an instance from.");
                outcome.Item(i + 1, scale.Instances, entries.Count);
                continue;
            }

            var example = examples[(i / entries.Count) % examples.Count];
            var name = $"Soak {i:0000} {templateId}";

            create.Start();
            var instance = await service.CreateAsync(entry.Reference, project.Id, name, example.Inputs, cancellationToken);
            create.Stop();

            perTemplate[templateId] = perTemplate.GetValueOrDefault(templateId) + 1;
            outcome.Expect(instance.Blocks.Count == 0, () => $"{name} ({example.Name}) is blocked after create: {instance.Blocks.FirstOrDefault()?.Message}");
            outcome.Expect(instance.Outputs.Count > 0, () => $"{name} saved no outputs.");
            outcome.Expect(instance.Outputs.All(o => o.Si is null || double.IsFinite(o.Si.Value)), () => $"{name} saved a non-finite output.");
            outcome.Expect(instance is { Status: CalcInstanceStatus.Draft, Revision: "A", EvidenceId: not null, OutputsHash: not null }, () => $"{name} was not saved as a draft revision A with its Evidence and an outputs hash.");

            run.Start();
            var ran = await service.RunAsync(instance.Id, cancellationToken);
            run.Stop();
            outcome.Expect(ran is { Result.IsBlocked: false }, () => $"{name} is blocked when run again.");

            if (ran is not null)
            {
                outcome.Expect(ran.Result.Outputs.Count == instance.Outputs.Count, () => $"{name} gives {ran.Result.Outputs.Count} outputs when run again and saved {instance.Outputs.Count}.");
                outcome.Expect(ran.Result.Outputs.Select(o => o.ValueText).SequenceEqual(instance.Outputs.Select(o => o.Text)), () => $"{name} gives different output text when run again from what it saved.");
                outcome.Expect(ran.SheetText.Length > 100, () => $"{name} rendered an empty sheet.");
            }

            update.Start();
            var again = await service.UpdateInputsAsync(instance.Id, instance.Inputs, cancellationToken);
            update.Stop();
            outcome.Expect(again.Succeeded, () => $"{name}: saving the same inputs again was refused: {again.Reason}");
            outcome.Expect(again.Instance?.OutputsHash == instance.OutputsHash, () => $"{name}: saving the same inputs again changed the outputs hash.");
            saved.Add((instance.Id, instance.OutputsHash ?? string.Empty, instance.Outputs.Count));

            // Each of the first round of the catalogue is a different template (its formulas compiled, its worked example read, once): the check on growth
            // within the host begins after them, when every further calculation is more of the same.
            outcome.Item(i + 1, scale.Instances, entries.Count);

            if ((i + 1) % 100 == 0)
                SoakScale.Progress($"instances: {i + 1} of {scale.Instances}");
        }

        outcome.Expect(perTemplate.Count == Math.Min(scale.Instances, entries.Count), () => $"{perTemplate.Count} templates were used; every one of {Math.Min(scale.Instances, entries.Count)} should have been.");

        // Read everything back: the project lists exactly what was saved, intact, and none of it is stale.
        var read = Stopwatch.StartNew();
        var listed = await service.Store.ListInstancesAsync(project.Id, cancellationToken);
        read.Stop();
        outcome.Expect(listed.Count == saved.Count, () => $"The project lists {listed.Count} calculations; {saved.Count} were saved.");
        var byId = listed.ToDictionary(i => i.Id);
        var missing = saved.Count(s => !byId.ContainsKey(s.Id));
        var changed = saved.Count(s => byId.TryGetValue(s.Id, out var found) && (found.OutputsHash != s.Hash || found.Outputs.Count != s.Outputs));
        outcome.Expect(missing == 0, () => $"{missing} saved calculation(s) are missing from the project's listing.");
        outcome.Expect(changed == 0, () => $"{changed} calculation(s) read back with different outputs from those saved.");

        var stale = Stopwatch.StartNew();
        var staleness = await service.StalenessForProjectAsync(project.Id, cancellationToken);
        stale.Stop();
        var flagged = staleness.Where(s => s.Value.IsStale).ToList();
        outcome.Expect(flagged.Count == 0, () => $"{flagged.Count} calculation(s) were reported stale in a project where nothing changed (a stale flag with no cause): "
            + string.Join("; ", flagged.Take(3).Select(s => $"{byId[s.Key].Name}: {s.Value.Reasons.FirstOrDefault()}")));

        outcome.Fact("Calculations created, run and saved", saved.Count);
        outcome.Fact("Templates covered", perTemplate.Count);
        outcome.Fact("Database growth per calculation (KB)", saved.Count == 0 ? 0 : (DatabaseBytes(host) - databaseBefore) / 1024.0 / saved.Count, "0.0");
        outcome.Fact("Create (mean ms)", saved.Count == 0 ? 0 : create.Elapsed.TotalMilliseconds / saved.Count, "0.0");
        outcome.Fact("Run again (mean ms)", saved.Count == 0 ? 0 : run.Elapsed.TotalMilliseconds / saved.Count, "0.0");
        outcome.Fact("Save again (mean ms)", saved.Count == 0 ? 0 : update.Elapsed.TotalMilliseconds / saved.Count, "0.0");
        outcome.Fact("List the whole project (ms)", read.Elapsed.TotalMilliseconds, "0");
        outcome.Fact("Staleness of the whole project (ms)", stale.Elapsed.TotalMilliseconds, "0");
    }

    // ------------------------------------------------------------------
    // 2. Chains: link, change the head, every step goes stale, update in order, every step is current
    // ------------------------------------------------------------------

    private const string ChainTemplate = "mech.iso286-fit";
    private const string ChainOutput = "D_hole_max";

    /// <summary>
    /// Builds <see cref="SoakScale.Chains"/> chains of <see cref="SoakScale.ChainDepth"/> links (the hole's upper limit of one calculation is the
    /// nominal size of the next), two chains to a project. For each: no step is stale; the head's input changes; every step after it is stale and
    /// the other chain of the project is not; each is updated in order and is current straight after, while the next is still stale; at the end all
    /// are current and every link gives exactly the value its upstream has. A link past the depth cap and a link round a loop are refused.
    /// </summary>
    public static async Task ChainsAsync(SoakHost host, SoakScale scale, SoakOutcome outcome, CancellationToken cancellationToken)
    {
        var service = host.Service;
        var example = SoakExamples.For(ChainTemplate)[0].Inputs;
        var staleAfterChange = 0;
        var updated = 0;
        var linkWatch = new Stopwatch();
        var staleWatch = new Stopwatch();
        var updateWatch = new Stopwatch();
        var staleQueries = 0;
        var projectCount = (scale.Chains + 1) / 2;

        for (var p = 0; p < projectCount; p++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var project = await host.Host.ProjectDirectory!.CreateAsync($"SOAK-2-{p:00}", $"Soak: chains {p}");
            var chains = new List<List<CalcInstance>>();

            for (var c = 0; c < 2 && p * 2 + c < scale.Chains; c++)
            {
                var chainIndex = p * 2 + c;
                var steps = new List<CalcInstance>();

                for (var k = 0; k <= scale.ChainDepth; k++)
                {
                    var step = await service.CreateAsync(ChainTemplate, project.Id, $"Chain {chainIndex:00} step {k:00}", WithD(example, 25 + chainIndex), cancellationToken);
                    outcome.Expect(step.Blocks.Count == 0, () => $"Chain {chainIndex} step {k} is blocked: {step.Blocks.FirstOrDefault()?.Message}");

                    if (k > 0)
                    {
                        linkWatch.Start();
                        var bound = await service.BindAsync(step.Id, "D", steps[k - 1].Id, ChainOutput, cancellationToken);
                        linkWatch.Stop();
                        outcome.Expect(bound.Succeeded, () => $"Chain {chainIndex}: linking step {k} to step {k - 1} was refused: {bound.Reason}");
                        step = bound.Instance ?? step;
                    }

                    steps.Add(step);
                }

                chains.Add(steps);
            }

            // Nothing has changed yet: nothing may be stale.
            staleWatch.Start();
            var before = await service.StalenessForProjectAsync(project.Id, cancellationToken);
            staleWatch.Stop();
            staleQueries++;
            outcome.Expect(before.Values.All(s => !s.IsStale), () => $"Project {p}: {before.Values.Count(s => s.IsStale)} step(s) are stale before anything changed.");

            // The head of the first chain changes (its nominal size grows by 1 mm).
            var head = chains[0][0];
            var changedHead = await service.UpdateInputsAsync(head.Id, WithD(head.Inputs, ((head.Inputs.First(i => i.Symbol == "D").Number) ?? 25) + 1), cancellationToken);
            outcome.Expect(changedHead.Succeeded, () => $"Project {p}: changing the head's input was refused: {changedHead.Reason}");

            var downstream = await service.DownstreamAsync(head.Id, project.Id, cancellationToken);
            outcome.Expect(downstream.Count == scale.ChainDepth, () => $"Project {p}: {downstream.Count} calculations are downstream of the head; {scale.ChainDepth} are linked to it.");

            staleWatch.Start();
            var afterChange = await service.StalenessForProjectAsync(project.Id, cancellationToken);
            staleWatch.Stop();
            staleQueries++;
            outcome.Expect(!afterChange[head.Id].IsStale, () => $"Project {p}: the head, which was only edited, is reported stale.");

            for (var k = 1; k <= scale.ChainDepth; k++)
            {
                var flagged = afterChange[chains[0][k].Id].IsStale;
                staleAfterChange += flagged ? 1 : 0;
                outcome.Expect(flagged, () => $"Project {p}: step {k} of the first chain is not stale after the head changed (a missed stale flag).");
            }

            if (chains.Count > 1)
            {
                var untouched = chains[1].Count(s => afterChange[s.Id].IsStale);
                outcome.Expect(untouched == 0, () => $"Project {p}: {untouched} step(s) of the other chain are stale though only the first chain changed (a false stale flag).");
            }

            // Update in order: each step is current as soon as it is updated, and the next one is still stale until its turn.
            for (var k = 1; k <= scale.ChainDepth; k++)
            {
                updateWatch.Start();
                var result = await service.UpdateLinksAsync(chains[0][k].Id, cancellationToken);
                updateWatch.Stop();
                outcome.Expect(result.Succeeded, () => $"Project {p}: updating step {k} was refused: {result.Reason}");
                updated += result.Succeeded ? 1 : 0;

                var now = await service.StalenessForProjectAsync(project.Id, cancellationToken);
                outcome.Expect(!now[chains[0][k].Id].IsStale, () => $"Project {p}: step {k} is still stale straight after it was updated.");

                if (k < scale.ChainDepth)
                    outcome.Expect(now[chains[0][k + 1].Id].IsStale, () => $"Project {p}: step {k + 1} is not stale though step {k} gave it a new value.");
            }

            var after = await service.StalenessForProjectAsync(project.Id, cancellationToken);
            outcome.Expect(after.Values.All(s => !s.IsStale), () => $"Project {p}: {after.Values.Count(s => s.IsStale)} step(s) are still stale after the whole chain was updated.");

            // Every link now gives exactly its upstream's value.
            var readBack = (await service.Store.ListInstancesAsync(project.Id, cancellationToken)).ToDictionary(i => i.Id);

            foreach (var steps in chains)
            {
                for (var k = 1; k < steps.Count; k++)
                {
                    var upstream = readBack[steps[k - 1].Id];
                    var current = readBack[steps[k].Id];
                    var given = upstream.Outputs.First(o => o.Symbol == ChainOutput).Si!.Value;
                    var taken = current.Inputs.First(i => i.Symbol == "D").Number!.Value / 1000.0;
                    outcome.Expect(Math.Abs(given - taken) <= 1e-12 * Math.Max(1, Math.Abs(given)), () => $"{current.Name} takes {taken:R} m from {upstream.Name}, which gives {given:R} m.");
                    outcome.Expect(current.Links.Single().Link.Revision == upstream.Revision, () => $"{current.Name} is pinned to revision {current.Links.Single().Link.Revision} of {upstream.Name}, which is at {upstream.Revision}.");
                }
            }

            // One link too many, and a link round the loop, are refused.
            var tooDeep = await service.CreateAsync(ChainTemplate, project.Id, $"Chain {p * 2:00} step {scale.ChainDepth + 1:00}", WithD(example, 25), cancellationToken);
            var refusedDepth = await service.BindAsync(tooDeep.Id, "D", chains[0][scale.ChainDepth].Id, ChainOutput, cancellationToken);
            outcome.Expect(!refusedDepth.Succeeded && refusedDepth.Reason?.Contains($"at most {CalcInstanceService.MaxChainDepth}", StringComparison.Ordinal) == true, () => $"Project {p}: a chain of {scale.ChainDepth + 1} links was not refused for its depth: {refusedDepth.Reason}");
            var loop = await service.BindAsync(chains[0][0].Id, "D", chains[0][scale.ChainDepth].Id, ChainOutput, cancellationToken);
            outcome.Expect(!loop.Succeeded && loop.Reason?.Contains("cycle", StringComparison.Ordinal) == true, () => $"Project {p}: a link round a loop was not refused as a cycle: {loop.Reason}");

            outcome.Item(Math.Min(scale.Chains, (p + 1) * 2) * (scale.ChainDepth + 1), scale.Chains * (scale.ChainDepth + 1));
            SoakScale.Progress($"chains: project {p + 1} of {projectCount}");
        }

        outcome.Fact("Chains", scale.Chains);
        outcome.Fact("Links per chain", scale.ChainDepth);
        outcome.Fact("Calculations in chains", (long)scale.Chains * (scale.ChainDepth + 1));
        outcome.Fact("Steps reported stale after the head changed", staleAfterChange);
        outcome.Fact("Steps updated in order", updated);
        outcome.Fact("Link a step (mean ms)", linkWatch.Elapsed.TotalMilliseconds / Math.Max(1, scale.Chains * scale.ChainDepth), "0.0");
        outcome.Fact("Update a step (mean ms)", updateWatch.Elapsed.TotalMilliseconds / Math.Max(1, updated), "0.0");
        outcome.Fact("Staleness query (mean ms)", staleWatch.Elapsed.TotalMilliseconds / Math.Max(1, staleQueries), "0.0");
    }

    /// <summary>The database file and its write-ahead log, as they are now.</summary>
    private static long DatabaseBytes(SoakHost host) => SoakProbe.FileBytes(host.DatabasePath) + SoakProbe.FileBytes(host.DatabasePath + "-wal");

    private static IReadOnlyList<CalcInputValue> WithD(IReadOnlyList<CalcInputValue> inputs, double millimetres) =>
        [.. inputs.Select(i => i.Symbol == "D" ? i with { Number = millimetres } : i)];
}
