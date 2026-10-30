using System.Diagnostics;
using Tempest.Calc.Import.Scan;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Evidence;

namespace Tempest.Desktop.Tests.Soak;

internal static partial class SoakScenarios
{
    // ------------------------------------------------------------------
    // 7. The demonstration project (C-6): installed and removed over and over, a real project beside it untouched
    // ------------------------------------------------------------------

    /// <summary>The documents of a real project, raw as the store holds them: what an install or a removal of the demonstration must never change.</summary>
    internal sealed record RealFingerprint(IReadOnlyDictionary<string, string?> Documents)
    {
        public IEnumerable<string> Differences(RealFingerprint other) =>
            Documents.Where(kv => !string.Equals(kv.Value, other.Documents.GetValueOrDefault(kv.Key), StringComparison.Ordinal)).Select(kv => kv.Key)
                .Concat(other.Documents.Keys.Where(k => !Documents.ContainsKey(k)));
    }

    /// <summary>
    /// A real project (calculations, one of them issued, a pack, a scan profile of the person's own and a reference record) is made first. The
    /// demonstration is then installed and removed <see cref="SoakScale.DemoCycles"/> times. After every install and every removal the real project's
    /// documents are compared byte for byte with what they were; the removal must take every calculation, pack and scan profile of the demonstration
    /// and nothing else; and the database, counted in its own pages, must not grow by more than the soft-deleted objects a removal keeps by design
    /// (the project and the Evidence and register entries, so that history and the audit trail stay).
    /// </summary>
    public static async Task DemoAsync(SoakHost host, SoakScale scale, SoakOutcome outcome, CancellationToken cancellationToken)
    {
        var service = host.Service;
        var store = service.Store;
        var demo = host.Host.CalcDemo ?? throw new InvalidOperationException("The host has no demonstration service.");
        var domain = host.Domain;

        // ---- a real project beside it ------------------------------------------------------------------------
        var real = await host.Host.ProjectDirectory!.CreateAsync("SOAK-7-REAL", "Soak: a real project");
        var created = new List<CalcInstance>();

        foreach (var entry in service.Catalogue.Current().Where(e => SoakExamples.For(e.Template.Id).Count > 0).Take(6))
            created.Add(await service.CreateAsync(entry.Reference, real.Id, $"Soak real {entry.Template.Id}", SoakExamples.For(entry.Template.Id)[0].Inputs, cancellationToken));

        var pack = await service.CreatePackAsync(real.Id, "Soak real pack", [created[0].Id, created[1].Id], cancellationToken);

        // One issued calculation: an issued record is the one thing a removal must never come near.
        host.SignInAs(SoakHost.Checker);
        var checkedResult = await service.CheckAsync(created[2].Id, "Soak Checker", "Soak Checking Ltd", "Checked by the soak harness.", CheckOutcome.Accepted, cancellationToken);
        host.SignInAs(SoakHost.Author);
        var issued = checkedResult.Succeeded ? await service.IssueAsync(created[2].Id, "SOAK-REAL-1", "Soak Client Ltd", cancellationToken) : checkedResult;
        outcome.Expect(issued.Succeeded, () => $"The real project's calculation could not be checked and issued: {issued.Reason ?? checkedResult.Reason}");

        // A scan profile of the person's own, and a reference record: global rows the demonstration's removal might in a bad day touch.
        var profile = new ScanProfile("soak-real-scan", 1, "Soak real scan profile", new ScanColumns("Element", Deviation: "Deviation"), [new ScanFeatureEntry("Housing bore", "contributors[0]")], ScanSoftware.Generic, TemplateId: "mech.tolerance-stack");
        var (savedProfile, profileRefusal) = await service.SaveScanProfileAsync(profile, cancellationToken);
        outcome.Expect(savedProfile is not null, () => $"The real scan profile was refused: {profileRefusal}");
        var recordImport = await service.RecordImport.CommitAsync(new RecordImportRequest(
            System.Text.Encoding.UTF8.GetBytes("emitter[text],n[-]\r\nSoak emitter,1.3\r\n"), "real.csv", "28", SoakProvenance("Soak real record", licensed: false)), cancellationToken);
        outcome.Expect(recordImport.Committed, () => $"The real reference record was not imported: {recordImport.Refusal}");

        async Task<RealFingerprint> CaptureRealAsync()
        {
            var documents = new Dictionary<string, string?>(StringComparer.Ordinal);

            foreach (var instance in await store.ListInstancesAsync(real.Id, cancellationToken))
                documents["instance " + instance.Name] = await host.RawInstanceAsync(instance, cancellationToken);

            var packKey = CalcInstanceStore.Key(real.Id, pack.Id);
            documents["pack"] = (await host.Store.ReadManyAsync(CalcInstanceStore.PackCollection, [packKey], cancellationToken)).GetValueOrDefault(packKey);
            var scanKey = CalcInstanceStore.ImportProfileKey(profile.Id, profile.Version);
            documents["scan profile"] = (await host.Store.ReadManyAsync(CalcInstanceStore.ScanProfileCollection, [scanKey], cancellationToken)).GetValueOrDefault(scanKey);

            foreach (var record in recordImport.Records)
                documents["record " + record.Title] = CalcInstanceStore.Serialise((await store.FindRecordAsync(record.Id, null, cancellationToken))!);

            return new RealFingerprint(documents);
        }

        var realBefore = await CaptureRealAsync();
        outcome.Expect(realBefore.Documents.Count == created.Count + 3 && realBefore.Documents.Values.All(v => !string.IsNullOrEmpty(v)), () => $"The real project's fingerprint holds {realBefore.Documents.Count} documents; it should hold {created.Count + 3} (its calculations, its pack, a scan profile and a record).");

        // ---- the cycles ------------------------------------------------------------------------------------
        var status = await demo.StatusAsync(cancellationToken);
        outcome.Expect(!status.Installed && status.InstallRefusal is null, () => $"The demonstration reads as installed (or refused) before anything installed it: {status.InstallRefusal}");
        var installMs = new List<double>();
        var removeMs = new List<double>();
        var installedBytes = new List<long>();
        var residualBytes = new List<long>();
        var keptObjects = new List<int>();
        var documentBytes = new List<long>();
        var (usedAtStart, _) = SoakProbe.LogicalDatabase(host.DatabasePath);
        var objectsAtStart = (await domain.Repository.ListAllAsync(cancellationToken)).Count;
        var calculationsPerInstall = 0;
        var packsPerInstall = 0;

        for (var cycle = 1; cycle <= scale.DemoCycles; cycle++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var watch = Stopwatch.StartNew();
            var install = await demo.InstallAsync(cancellationToken: cancellationToken);
            watch.Stop();
            installMs.Add(watch.Elapsed.TotalMilliseconds);
            outcome.Expect(install.Succeeded, () => $"Cycle {cycle}: the install was refused: {install.Message}");

            if (!install.Succeeded)
                break;

            calculationsPerInstall = install.Calculations;
            packsPerInstall = install.Packs;
            var installedStatus = await demo.StatusAsync(cancellationToken);
            outcome.Expect(installedStatus is { Installed: true, ProjectId: not null }, () => $"Cycle {cycle}: the demonstration is not reported as installed after its install.");
            var projectId = installedStatus.ProjectId;
            outcome.Expect((await store.ListInstancesAsync(projectId, cancellationToken)).Count == install.Calculations, () => $"Cycle {cycle}: the store holds {(store.ListInstancesAsync(projectId, cancellationToken).Result.Count):N0} calculations of the demonstration; the install says {install.Calculations:N0}.");
            outcome.Expect((await store.ListPacksAsync(projectId, cancellationToken)).Count == install.Packs, () => $"Cycle {cycle}: the store holds a different number of packs from the {install.Packs} the install made.");
            outcome.Expect(await store.FindScanProfileAsync(CalcDemoProject.StackProfileId, null, cancellationToken) is not null, () => $"Cycle {cycle}: the demonstration's scan profile is not saved.");
            outcome.Expect((await store.ListInstancesAsync(projectId, cancellationToken)).All(i => i.Status == CalcInstanceStatus.Draft && i.Name.StartsWith(CalcDemoProject.NamePrefix, StringComparison.Ordinal)), () => $"Cycle {cycle}: a calculation of the demonstration is not a draft named with the demonstration's prefix.");

            var differences = (await CaptureRealAsync()).Differences(realBefore).ToList();
            outcome.Expect(differences.Count == 0, () => $"Cycle {cycle}: installing the demonstration changed the real project: {string.Join(", ", differences.Take(5))}.");
            installedBytes.Add(SoakProbe.LogicalDatabase(host.DatabasePath).UsedBytes - usedAtStart);

            // What the removal is to give back: the calculation and pack documents, as the store holds them.
            long documents = 0;

            foreach (var instance in await store.ListInstancesAsync(projectId, cancellationToken))
                documents += (await host.RawInstanceAsync(instance, cancellationToken))?.Length ?? 0;

            foreach (var demoPack in await store.ListPacksAsync(projectId, cancellationToken))
                documents += (await host.Store.ReadManyAsync(CalcInstanceStore.PackCollection, [CalcInstanceStore.Key(demoPack.ProjectId, demoPack.Id)], cancellationToken)).Values.Sum(v => v?.Length ?? 0);

            documentBytes.Add(documents);

            watch.Restart();
            var removal = await demo.RemoveAsync(cancellationToken);
            watch.Stop();
            removeMs.Add(watch.Elapsed.TotalMilliseconds);
            outcome.Expect(removal.Succeeded && removal.Calculations == install.Calculations && removal.Packs == install.Packs, () => $"Cycle {cycle}: the removal said '{removal.Message}' ({removal.Calculations} calculations, {removal.Packs} packs); the install made {install.Calculations} and {install.Packs}.");
            outcome.Expect(!(await demo.StatusAsync(cancellationToken)).Installed, () => $"Cycle {cycle}: the demonstration is still reported installed after its removal.");
            outcome.Expect((await store.ListInstancesAsync(projectId, cancellationToken)).Count == 0 && (await store.ListPacksAsync(projectId, cancellationToken)).Count == 0, () => $"Cycle {cycle}: calculations or packs of the demonstration are left after its removal.");
            outcome.Expect(await store.FindScanProfileAsync(CalcDemoProject.StackProfileId, null, cancellationToken) is null && await store.FindScanProfileAsync(CalcDemoProject.GlandProfileId, null, cancellationToken) is null, () => $"Cycle {cycle}: the demonstration's scan profiles are left after its removal.");
            outcome.Expect((await store.ListInstancesAsync(null, cancellationToken)).Count == created.Count, () => $"Cycle {cycle}: the store holds {(store.ListInstancesAsync(null, cancellationToken).Result.Count):N0} calculations; the real project made {created.Count}.");

            differences = [.. (await CaptureRealAsync()).Differences(realBefore)];
            outcome.Expect(differences.Count == 0, () => $"Cycle {cycle}: removing the demonstration changed the real project: {string.Join(", ", differences.Take(5))}.");
            outcome.Expect(await store.FindScanProfileAsync("soak-real-scan", 1, cancellationToken) is not null, () => $"Cycle {cycle}: the real scan profile is gone.");

            residualBytes.Add(SoakProbe.LogicalDatabase(host.DatabasePath).UsedBytes - usedAtStart);
            keptObjects.Add((await domain.Repository.ListAllAsync(cancellationToken)).Count - objectsAtStart);

            // The unit of what the application may keep for each cycle is a calculation or a pack (its Evidence and register entry stay, by design).
            outcome.Item(cycle * (calculationsPerInstall + packsPerInstall), scale.DemoCycles * (calculationsPerInstall + packsPerInstall));
            SoakScale.Progress($"7-demo: cycle {cycle} done, install {installMs[^1]:0} ms, remove {removeMs[^1]:0} ms, {residualBytes[^1] / 1024.0:0} KiB left");
        }

        // ---- what the removal leaves, against what the install adds ------------------------------------------
        var cycles = residualBytes.Count;

        if (cycles > 0)
        {
            var perCycle = residualBytes[0];
            var objectsPerCycle = keptObjects[0];
            outcome.Expect(objectsPerCycle > 0, () => "A removal kept no soft-deleted objects: the project and its Evidence should stay (ADR-0074).");

            // The soft-deleted objects are the same set every cycle: the count of them grows by the same amount each time.
            outcome.Expect(keptObjects.Select((n, i) => n - (i == 0 ? 0 : keptObjects[i - 1])).All(d => d == objectsPerCycle), () => $"The objects a removal keeps are not the same every cycle: {string.Join(", ", keptObjects.Select((n, i) => n - (i == 0 ? 0 : keptObjects[i - 1])))}.");

            // The bound. A removal keeps, by design, the soft-deleted project and the Evidence and register objects (their rows, and the audit rows); it deletes the
            // calculation, pack and scan-profile rows and their search-index rows, whose pages go to the free list and are reused by the next install. So (1) the
            // removal gives back what it deletes: the database falls from where the install left it by at least 60 % of the calculation and pack documents' own
            // size (the rest of what the install added is the soft-deleted objects), and (2) what it keeps is the same every cycle: after N cycles the database holds
            // N times the first cycle's residual, to within page granularity and the one-off rows of the first (a quarter of a cycle's residual and 64 KiB), and not more.
            var installAdds = installedBytes[0];
            var given = installAdds - perCycle;
            outcome.Expect(given >= documentBytes[0] * 0.6, () => $"The removal gave back {given / 1024.0:N0} KiB of the database; the calculation and pack documents it deleted were {documentBytes[0] / 1024.0:N0} KiB (the removal must give back at least 60 % of that).");
            var allowed = (long)(perCycle * cycles * 1.25) + 64 * 1024;
            outcome.Expect(residualBytes[^1] <= allowed, () => $"After {cycles} cycles the database holds {residualBytes[^1] / 1024.0:N0} KiB more than before the first install; {cycles} × the first cycle's {perCycle / 1024.0:N0} KiB (+25 %, +64 KiB) allows {allowed / 1024.0:N0} KiB.");

            // And the installs after the first are not bigger than the first: nothing accumulates in what an install adds.
            outcome.Expect(installedBytes[^1] - residualBytes.ElementAtOrDefault(cycles - 2) <= installedBytes[0] * 1.25 + 64 * 1024, () => $"The last install added {(installedBytes[^1] - residualBytes.ElementAtOrDefault(cycles - 2)) / 1024.0:N0} KiB; the first added {installedBytes[0] / 1024.0:N0} KiB.");

            var head = installMs.Take(5).Average();
            var tail = installMs.TakeLast(Math.Min(5, installMs.Count)).Average();
            outcome.Expect(tail <= head * 2 + 2000, () => $"Installs slowed with use: the first five took {head:0} ms on average, the last five {tail:0} ms.");
            var removeHead = removeMs.Take(5).Average();
            var removeTail = removeMs.TakeLast(Math.Min(5, removeMs.Count)).Average();
            outcome.Expect(removeTail <= removeHead * 2 + 1000, () => $"Removals slowed with use: the first five took {removeHead:0} ms on average, the last five {removeTail:0} ms.");

            outcome.Fact("Install and remove cycles", cycles);
            outcome.Fact("Calculations and packs per install", $"{calculationsPerInstall} and {packsPerInstall}");
            outcome.Fact("Install (mean ms)", installMs.Average(), "0");
            outcome.Fact("Install (first / last ms)", $"{installMs[0]:0} / {installMs[^1]:0}");
            outcome.Fact("Remove (mean ms)", removeMs.Average(), "0");
            outcome.Fact("Remove (first / last ms)", $"{removeMs[0]:0} / {removeMs[^1]:0}");
            outcome.Fact("Database an install adds (KiB)", installedBytes[0] / 1024.0, "0");
            outcome.Fact("Calculation and pack documents an install writes and a removal deletes (KiB)", documentBytes[0] / 1024.0, "0");
            outcome.Fact("Database a removal gives back (KiB)", (installedBytes[0] - perCycle) / 1024.0, "0");
            outcome.Fact("Database a removal leaves, first cycle (KiB)", perCycle / 1024.0, "0");
            outcome.Fact("Database a removal leaves per soft-deleted object (KiB)", perCycle / 1024.0 / objectsPerCycle, "0.0");
            outcome.Fact("Database a removal leaves, after the last cycle (KiB, whole)", residualBytes[^1] / 1024.0, "0");
            outcome.Fact("Bound on what the cycles leave (KiB)", allowed / 1024.0, "0");
            outcome.Fact("Soft-deleted objects kept per removal", objectsPerCycle);
            outcome.Fact("Real project documents compared after every install and removal", realBefore.Documents.Count);
        }

        // ---- an issued calculation of the demonstration is never removed ---------------------------------------
        var again = await demo.InstallAsync(cancellationToken: cancellationToken);

        if (again.Succeeded)
        {
            var projectId = (await demo.StatusAsync(cancellationToken)).ProjectId;
            var target = (await store.ListInstancesAsync(projectId, cancellationToken)).OrderBy(i => i.Name, StringComparer.Ordinal).FirstOrDefault(i => i.Blocks.Count == 0 && i.Links.Count == 0 && i.UpstreamIds.Count == 0);
            var calculationsAfterInstall = (await store.ListInstancesAsync(projectId, cancellationToken)).Count;

            if (target is not null)
            {
                host.SignInAs(SoakHost.Checker);
                var demoCheck = await service.CheckAsync(target.Id, "Soak Checker", "Soak Checking Ltd", "Checked by the soak harness.", CheckOutcome.Accepted, cancellationToken);
                host.SignInAs(SoakHost.Author);
                var demoIssue = demoCheck.Succeeded ? await service.IssueAsync(target.Id, "SOAK-DEMO-1", "Soak Client Ltd", cancellationToken) : demoCheck;

                if (demoIssue.Succeeded)
                {
                    var refused = await demo.RemoveAsync(cancellationToken);
                    outcome.Expect(!refused.Succeeded && refused.Message.Contains("issued", StringComparison.OrdinalIgnoreCase), () => $"Removing a demonstration with an issued calculation did not refuse in a sentence about it: {refused.Message}");
                    outcome.Expect((await store.ListInstancesAsync(projectId, cancellationToken)).Count == calculationsAfterInstall, () => "The refused removal deleted calculations of the demonstration.");
                    outcome.Expect((await demo.StatusAsync(cancellationToken)).Installed, () => "The refused removal left the demonstration reported as not installed.");
                    outcome.Fact("Removal with an issued calculation", "refused: " + refused.Message);
                }
                else
                {
                    // Not "not tried": that an issued calculation stops the removal cannot be shown then, and that is a failure of the soak, not a thing to skip.
                    outcome.Expect(false, () => $"A calculation of the demonstration could not be issued, so that an issued calculation stops its removal was not tried: {demoIssue.Reason}");
                    outcome.Fact("Removal with an issued calculation", $"NOT TRIED, a failure: the calculation could not be issued ({demoIssue.Reason})");
                }
            }
            else
                outcome.Expect(false, () => "The demonstration holds no calculation that can be checked and issued on its own, so that an issued calculation stops its removal was not tried.");
        }
        else
            outcome.Expect(false, () => $"The final install was refused: {again.Message}");

        var finalDifferences = (await CaptureRealAsync()).Differences(realBefore).ToList();
        outcome.Expect(finalDifferences.Count == 0, () => $"The real project changed by the end: {string.Join(", ", finalDifferences.Take(5))}.");
    }
}
