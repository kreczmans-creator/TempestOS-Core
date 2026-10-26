using System.Diagnostics;
using System.Globalization;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;

namespace Tempest.Desktop.Tests.Soak;

internal static partial class SoakScenarios
{
    // ------------------------------------------------------------------
    // 6. Record import (C-3): templates at their caps, one past each cap, all or nothing, the export lock
    // ------------------------------------------------------------------

    private static RecordProvenanceInput SoakProvenance(string title, bool licensed = true) =>
        new(title, "Soak Standard (invented)", "2026", "A1", "Table S-1", "Soak licence holder", licensed);

    /// <summary>
    /// The record import (<see cref="RecordImportService"/>) over the real store: a representative set of Track P templates is imported with data
    /// sized to the template, then a file at every cap at once (5 MB, 1,001 rows, 64 columns, 4,096-character fields); then one past each cap
    /// (refused in a sentence, nothing saved); then files with one bad row among a thousand good ones, and a batch whose last record cannot be
    /// written (all or nothing, on the real database); then a licensed record revised many times, which must still never export.
    /// </summary>
    public static async Task RecordsAsync(SoakHost host, SoakScale scale, SoakOutcome outcome, CancellationToken cancellationToken)
    {
        var service = host.Service;
        var importer = service.RecordImport;
        var store = service.Store;
        var timings = new List<string>();
        var committedAtCaps = 0;
        var templatesImported = 0;
        var imported = importer.ImportableTemplates().Select(t => t.Id).ToHashSet(StringComparer.Ordinal);

        // The material library seeds its generic starting materials the first time a material is asked for; they are in the store before the counts below begin.
        await service.Materials.EnsureSeededAsync(cancellationToken);
        var databaseBefore = DatabaseBytes(host);

        // ---- the representative set, each at the size its template takes -------------------------------------
        foreach (var sample in SoakRecordFiles.Samples)
        {
            cancellationToken.ThrowIfCancellationRequested();
            outcome.Expect(imported.Contains(sample.Id), () => $"Template {sample.Id} cannot be imported in this build.");
            var rows = sample.PerRow ? Math.Max(10, (int)(sample.MaxRows * scale.ImportFraction)) : sample.MaxRows;
            var bytes = SoakRecordFiles.Build(sample, rows);
            var request = new RecordImportRequest(bytes, $"soak-{sample.Id}.csv", sample.Id, SoakProvenance($"Soak template {sample.Id}"));
            var before = (await store.ListRecordsAsync(null, cancellationToken)).Count;

            var watch = Stopwatch.StartNew();
            var preview = await importer.PreviewAsync(request, cancellationToken);
            var previewMs = watch.Elapsed.TotalMilliseconds;
            outcome.Expect(preview.CanCommit, () => $"Template {sample.Id}: a valid file of {rows:N0} rows cannot be committed: {Describe(preview)}");
            outcome.Expect(preview.Planned.Count == (sample.PerRow ? rows : 1), () => $"Template {sample.Id}: the preview plans {preview.Planned.Count:N0} records for {rows:N0} rows.");

            if (!preview.CanCommit)
                continue;

            watch.Restart();
            var committed = await importer.CommitAsync(request, cancellationToken);
            var commitMs = watch.Elapsed.TotalMilliseconds;
            outcome.Expect(committed.Committed, () => $"Template {sample.Id}: the commit was refused: {committed.Refusal}");

            if (!committed.Committed)
                continue;

            templatesImported++;
            outcome.Expect(committed.Records.Count == preview.Planned.Count, () => $"Template {sample.Id}: {committed.Records.Count:N0} records were saved; the preview planned {preview.Planned.Count:N0}.");
            outcome.Expect(committed.Records.All(r => r.LicensedSource && r.EnteredBy == SoakHost.Author && r.SourceTitle == "Soak Standard (invented)" && r.Edition == "2026; A1"), () => $"Template {sample.Id}: a record was not stamped licensed with its provenance and the signed-in person.");
            var after = (await store.ListRecordsAsync(null, cancellationToken)).Count;
            outcome.Expect(after == before + committed.Records.Count, () => $"Template {sample.Id}: the store holds {after - before:N0} new records; {committed.Records.Count:N0} were committed.");

            foreach (var record in new[] { committed.Records[0], committed.Records[^1] })
            {
                var read = await store.FindRecordAsync(record.Id, null, cancellationToken);
                outcome.Expect(read is not null && CalcInstanceStore.Serialise(read) == CalcInstanceStore.Serialise(record), () => $"Template {sample.Id}: record '{record.Title}' did not read back as it was saved.");
            }

            timings.Add($"template {sample.Id}, {rows:N0} rows, {committed.Records.Count:N0} record(s): preview {previewMs:0} ms, commit {commitMs:0} ms");
        }

        // ---- a file at every cap at once: 1,001 rows, 64 columns, 4,096-character fields, 5 MB --------------------
        var wide = SoakRecordFiles.Samples[0];
        var capRows = Math.Max(10, (int)(1000 * scale.ImportFraction));
        var capBytes = (long)(SoakRecordFiles.ByteCap * scale.ImportFraction);
        var padColumns = SoakRecordFiles.ColumnCap - wide.Template.Columns.Count;
        var capFile = SoakRecordFiles.Build(wide, capRows, padColumns, SoakRecordFiles.FieldCap, capBytes);
        outcome.Expect(capFile.Length == capBytes, () => $"The generated file is {capFile.Length:N0} bytes, not {capBytes:N0}.");
        var capRequest = new RecordImportRequest(capFile, "soak-caps.csv", wide.Id, SoakProvenance("Soak all caps"), ColumnMap: SoakRecordFiles.PadMap(padColumns));
        var capWatch = Stopwatch.StartNew();
        var capPreview = await importer.PreviewAsync(capRequest, cancellationToken);
        var capPreviewMs = capWatch.Elapsed.TotalMilliseconds;
        outcome.Expect(capPreview.CanCommit, () => $"A file of {capFile.Length:N0} bytes, {capRows + 1:N0} rows and {SoakRecordFiles.ColumnCap} columns with 4,096-character fields was refused: {Describe(capPreview)}");
        outcome.Expect(capPreview.Columns.Count == SoakRecordFiles.ColumnCap, () => $"The preview shows {capPreview.Columns.Count} columns of the {SoakRecordFiles.ColumnCap} in the file.");

        if (capPreview.CanCommit)
        {
            capWatch.Restart();
            var committed = await importer.CommitAsync(capRequest, cancellationToken);
            var capCommitMs = capWatch.Elapsed.TotalMilliseconds;
            outcome.Expect(committed.Committed && committed.Records.Count == capRows, () => $"The file at the caps saved {committed.Records.Count:N0} records, not {capRows:N0} ({committed.Refusal}).");
            committedAtCaps = committed.Records.Count;
            timings.Add($"file at the caps ({capFile.Length:N0} bytes, {capRows + 1:N0} rows, {SoakRecordFiles.ColumnCap} columns, 4,096-character fields): preview {capPreviewMs:0} ms, commit {capCommitMs:0} ms");
            outcome.Fact("File at the caps: preview (ms)", capPreviewMs, "0");
            outcome.Fact("File at the caps: commit (ms)", capCommitMs, "0");
        }

        // ---- one past each cap: always the real caps, whatever the scale --------------------------------------
        var table = SoakRecordFiles.Samples.Single(s => s.Id == "04");
        var padMap = SoakRecordFiles.PadMap(padColumns + 5);

        await ExpectRecordRefusedAsync(host, outcome, "a file one byte over the 5 MB cap",
            new RecordImportRequest(SoakRecordFiles.Build(wide, 1000, padColumns, SoakRecordFiles.FieldCap, SoakRecordFiles.ByteCap + 1), "over-bytes.csv", wide.Id, SoakProvenance("Soak over bytes"), ColumnMap: padMap), "larger than");

        await ExpectRecordRefusedAsync(host, outcome, $"a file of {SoakRecordFiles.RowCap + 1:N0} rows (one over the {SoakRecordFiles.RowCap:N0}-row cap, the header among them)",
            new RecordImportRequest(SoakRecordFiles.Build(wide, SoakRecordFiles.RowCap), "over-rows.csv", wide.Id, SoakProvenance("Soak over rows")), "more than");

        await ExpectRecordRefusedAsync(host, outcome, $"a file of {SoakRecordFiles.ColumnCap + 1} columns (one over the {SoakRecordFiles.ColumnCap}-column cap)",
            new RecordImportRequest(SoakRecordFiles.Build(table, 5, SoakRecordFiles.ColumnCap + 1 - table.Template.Columns.Count), "over-columns.csv", table.Id, SoakProvenance("Soak over columns"), ColumnMap: padMap), "more than");

        await ExpectRecordRefusedAsync(host, outcome, $"a field of {SoakRecordFiles.FieldCap + 1:N0} characters (one over the {SoakRecordFiles.FieldCap:N0}-character cap)",
            new RecordImportRequest(WithLongField(table, 5, SoakRecordFiles.FieldCap + 1), "over-field.csv", table.Id, SoakProvenance("Soak over field"), ColumnMap: padMap), "longer than");

        // A text of 4,096 characters (allowed to a field) in a column the record keeps: the record's own 200-character cap says no, on its row.
        var longText = SoakRecordFiles.WithRow(wide, 5, 2, [new string('x', SoakRecordFiles.FieldCap), "10", "5"]);
        await ExpectRecordNotCommittedAsync(host, outcome, "a 4,096-character designation (past the 200 characters a record's text may hold)",
            new RecordImportRequest(longText, "long-text.csv", wide.Id, SoakProvenance("Soak long text")), "characters", "Also refused: ");

        await ExpectRecordRefusedAsync(host, outcome, "an empty file", new RecordImportRequest([], "empty.csv", wide.Id, SoakProvenance("Soak empty")), "empty", "Also refused: ");
        await ExpectRecordNotCommittedAsync(host, outcome, "a header with no data rows",
            new RecordImportRequest(System.Text.Encoding.UTF8.GetBytes(wide.Template.Header + "\r\n"), "header-only.csv", wide.Id, SoakProvenance("Soak header only")), "no data rows", "Also refused: ");

        // ---- all or nothing ------------------------------------------------------------------------------
        var thousand = SoakRecordFiles.Samples[1];
        var goodRows = Math.Max(10, (int)(1000 * scale.ImportFraction));

        foreach (var (where, badRow) in new[] { ("last", goodRows - 1), ("first", 0) })
        {
            var recordsBefore = (await store.ListRecordsAsync(null, cancellationToken)).Count;
            var request = new RecordImportRequest(SoakRecordFiles.WithRow(thousand, goodRows, badRow, ["SOAK-BAD", "0", "40", "-275", "410"]), $"bad-{where}.csv", thousand.Id, SoakProvenance($"Soak bad row {where}"));
            var preview = await importer.PreviewAsync(request, cancellationToken);
            outcome.Expect(!preview.CanCommit && preview.Errors.Any(e => e.Line == badRow + 2), () => $"One bad row ({where} of {goodRows:N0}) did not block the preview or was not named by its line ({badRow + 2}): {Describe(preview)}");
            var refused = await importer.CommitAsync(request, cancellationToken);
            outcome.Expect(!refused.Committed && refused.Records.Count == 0 && refused.Refusal?.StartsWith("Nothing was saved", StringComparison.Ordinal) == true, () => $"One bad row ({where}) was committed or not refused as 'Nothing was saved': {refused.Refusal}");
            outcome.Expect((await store.ListRecordsAsync(null, cancellationToken)).Count == recordsBefore, () => $"One bad row ({where} of {goodRows:N0}) left records in the store.");
            outcome.Fact($"All or nothing: one bad row ({where} of {goodRows:N0}) saved", 0L);
        }

        // On the real database: a batch whose last record cannot be written (that revision exists) leaves none of the others behind.
        var anchor = (await store.ListRecordsAsync(wide.Template.Kind, cancellationToken)).First();
        var fresh = Enumerable.Range(0, 200).Select(i => anchor with { Id = Guid.NewGuid(), Title = $"Soak atomic {i:000}", Revision = 1 }).ToList();
        var recordsAtStart = (await store.ListRecordsAsync(null, cancellationToken)).Count;
        var atomic = await SoakAwait.ThrowsAsync(() => store.SaveRecordsAsync([.. fresh, anchor], cancellationToken));
        outcome.Expect(atomic is CalcInstanceImmutableException, () => $"A batch with one record that exists already was not refused as such: {atomic?.GetType().Name}: {atomic?.Message}");
        outcome.Expect((await store.ListRecordsAsync(null, cancellationToken)).Count == recordsAtStart, () => "A batch whose last record could not be written left some of the others in the store.");

        foreach (var record in fresh.Take(20).Concat(fresh.TakeLast(5)))
            outcome.Expect(await store.FindRecordAsync(record.Id, null, cancellationToken) is null, () => $"'{record.Title}' is in the store though its batch was refused.");

        await store.SaveRecordsAsync(fresh, cancellationToken);
        outcome.Expect((await store.ListRecordsAsync(null, cancellationToken)).Count == recordsAtStart + fresh.Count, () => "The same batch, without the record that exists, was not saved whole.");
        outcome.Fact("All or nothing: batch of 201 with its last record refused, saved", 0L);

        // ---- the export lock after many revisions -----------------------------------------------------------
        await LicenceLockAsync(host, scale, outcome, cancellationToken);

        outcome.Fact("Templates imported", templatesImported);
        outcome.Fact("Records committed at the caps", committedAtCaps);
        outcome.Fact("Refusals one past a cap", outcome.Facts.Count(f => f.Key.StartsWith("Refused: ", StringComparison.Ordinal)));
        outcome.Fact("Database growth per record (KB)", (DatabaseBytes(host) - databaseBefore) / 1024.0 / Math.Max(1, (await store.ListRecordsAsync(null, cancellationToken)).Count), "0.0");

        foreach (var line in timings)
            outcome.Fact("Import timing", line);
    }

    private static string Describe(RecordImportPreview preview) => string.Join(" | ", new[] { preview.Refusal }.Concat(preview.AllIssues.Take(3).Select(i => i.ToString())).Where(s => s is not null));

    private static byte[] WithLongField(SoakRecordFiles.Sample sample, int dataRows, int chars)
    {
        var lines = new List<string> { sample.Template.Header + ",pad01" };
        lines.AddRange(Enumerable.Range(0, dataRows).Select(i => string.Join(',', sample.Row(i)) + "," + (i == 0 ? new string('p', chars) : string.Empty)));
        return new System.Text.UTF8Encoding(false).GetBytes(string.Join("\r\n", lines) + "\r\n");
    }

    /// <summary>A request that must be refused whole, in a plain sentence, with nothing saved: the preview says so and the commit saves nothing.</summary>
    private static async Task ExpectRecordRefusedAsync(SoakHost host, SoakOutcome outcome, string what, RecordImportRequest request, string expectedText, string factPrefix = "Refused: ")
    {
        var store = host.Service.Store;
        var before = (await store.ListRecordsAsync()).Count;
        RecordImportPreview preview;
        RecordImportOutcome commit;

        try
        {
            preview = await host.Service.RecordImport.PreviewAsync(request);
            commit = await host.Service.RecordImport.CommitAsync(request);
        }
        catch (Exception ex)
        {
            outcome.Expect(false, () => $"{what}: threw {ex.GetType().Name} instead of being refused cleanly: {ex.Message}");
            return;
        }

        var refusal = preview.Refusal;
        outcome.Expect(!string.IsNullOrWhiteSpace(refusal), () => $"{what}: was NOT refused (cap not enforced): {Describe(preview)}");
        outcome.Expect(refusal is null || refusal.Contains(expectedText, StringComparison.OrdinalIgnoreCase), () => $"{what}: refused, but with '{refusal}' (expected it to say '{expectedText}').");
        outcome.Expect(refusal is null || (refusal.Length < 300 && !refusal.Contains("Exception", StringComparison.Ordinal)), () => $"{what}: the refusal is not a plain sentence: '{refusal}'.");
        outcome.Expect(!preview.CanCommit && !commit.Committed && commit.Records.Count == 0, () => $"{what}: the import was not stopped.");
        outcome.Expect((await store.ListRecordsAsync()).Count == before, () => $"{what}: records were saved though the import was refused.");
        outcome.Fact(factPrefix + what, refusal ?? "(not refused)");
    }

    /// <summary>A request the importer reads but cannot commit (a row error, not a file refusal): the sentence says why and nothing is saved.</summary>
    private static async Task ExpectRecordNotCommittedAsync(SoakHost host, SoakOutcome outcome, string what, RecordImportRequest request, string expectedText, string factPrefix = "Refused: ")
    {
        var store = host.Service.Store;
        var before = (await store.ListRecordsAsync()).Count;
        RecordImportPreview preview;
        RecordImportOutcome commit;

        try
        {
            preview = await host.Service.RecordImport.PreviewAsync(request);
            commit = await host.Service.RecordImport.CommitAsync(request);
        }
        catch (Exception ex)
        {
            outcome.Expect(false, () => $"{what}: threw {ex.GetType().Name} instead of being refused cleanly: {ex.Message}");
            return;
        }

        var blocker = preview.Blocker;
        outcome.Expect(!preview.CanCommit && blocker is not null, () => $"{what}: the preview did not block the import.");
        outcome.Expect(blocker is null || blocker.Contains(expectedText, StringComparison.OrdinalIgnoreCase), () => $"{what}: blocked, but with '{blocker}' (expected it to say '{expectedText}').");
        outcome.Expect(blocker is null || (blocker.Length < 400 && !blocker.Contains("Exception", StringComparison.Ordinal)), () => $"{what}: the blocker is not a plain sentence: '{blocker}'.");
        outcome.Expect(!commit.Committed && commit.Records.Count == 0, () => $"{what}: the commit saved records.");
        outcome.Expect((await store.ListRecordsAsync()).Count == before, () => $"{what}: records were saved though the import was blocked.");
        outcome.Fact(factPrefix + what, blocker ?? "(not blocked)");
    }

    /// <summary>
    /// A licensed record is revised <see cref="SoakScale.RecordRevisions"/> times, each import trying (and failing) to release it from the licence lock
    /// first. After every revision, and at the end, no revision of it exports, a calculation that uses it withholds its values, and an unlicensed
    /// record of the same kind of file still exports (so the check can tell).
    /// </summary>
    private static async Task LicenceLockAsync(SoakHost host, SoakScale scale, SoakOutcome outcome, CancellationToken cancellationToken)
    {
        var service = host.Service;
        var importer = service.RecordImport;
        var store = service.Store;
        const string title = "Soak licensed ampacity";
        var table = SoakRecordFiles.Samples.Single(s => s.Id == "04");

        // Eight rows of capacity rising with the cross-section; revision k adds k amperes to every row.
        byte[] File(int k) => System.Text.Encoding.UTF8.GetBytes(table.Template.Header + "\r\n" + string.Concat(Enumerable.Range(0, 8).Select(i => $"{(1 + i * 0.5).ToString(CultureInfo.InvariantCulture)},,{15 + i * 3 + k}\r\n")));

        var first = await importer.CommitAsync(new RecordImportRequest(File(0), "licensed-1.csv", table.Id, SoakProvenance(title)), cancellationToken);
        outcome.Expect(first.Committed, () => $"The licensed record was not imported: {first.Refusal}");

        if (!first.Committed)
            return;

        var id = first.Records[0].Id;
        var released = 0;
        var watch = Stopwatch.StartNew();

        for (var k = 1; k <= scale.RecordRevisions; k++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // An import that says the source is not licensed cannot release the record.
            var tryRelease = await importer.CommitAsync(new RecordImportRequest(File(k), $"release-{k}.csv", table.Id, SoakProvenance(title, licensed: false), AsRevision: true), cancellationToken);

            if (tryRelease.Committed)
                released++;

            var revised = await importer.CommitAsync(new RecordImportRequest(File(k), $"licensed-{k + 1}.csv", table.Id, SoakProvenance(title), AsRevision: true), cancellationToken);
            outcome.Expect(revised.Committed && revised.Records[0].Id == id && revised.Records[0].Revision == k + 1, () => $"Revision {k + 1} of the licensed record was not saved as the next revision ({revised.Refusal}).");

            if (!revised.Committed)
                break;

            outcome.Item(k, scale.RecordRevisions);

            // Looked at every tenth revision, and the last: the lock holds as the revisions pile up.
            if (k % 10 == 0 || k == scale.RecordRevisions)
            {
                var (csv, refusal) = await service.ExportRecordAsync(id, null, cancellationToken);
                outcome.Expect(csv is null && refusal?.Contains("never exported", StringComparison.Ordinal) == true, () => $"After {k + 1} revisions the licensed record exported ({refusal}).");
            }
        }

        watch.Stop();
        outcome.Expect(released == 0, () => $"{released} import(s) saying the source was not licensed were accepted as a revision of a licensed record.");
        var latest = await store.FindRecordAsync(id, null, cancellationToken);
        outcome.Expect(latest is { LicensedSource: true } && latest.Revision == scale.RecordRevisions + 1, () => $"The licensed record is at revision {latest?.Revision}, licensed {latest?.LicensedSource}; expected revision {scale.RecordRevisions + 1}, licensed.");
        outcome.Expect(await store.IsLicensedInAnyRevisionAsync(id, cancellationToken), () => "The store does not say the record is licensed.");

        var exported = 0;

        for (var revision = 1; revision <= scale.RecordRevisions + 1; revision++)
        {
            var read = await store.FindRecordAsync(id, revision, cancellationToken);
            outcome.Expect(read is { LicensedSource: true }, () => $"Revision {revision} of the licensed record is not licensed.");
            var (csv, _) = await service.ExportRecordAsync(id, revision, cancellationToken);

            if (csv is not null)
                exported++;
        }

        outcome.Expect(exported == 0, () => $"{exported} revision(s) of the licensed record exported.");

        // A calculation that takes the latest revision withholds it from an export.
        var project = await host.Host.ProjectDirectory!.CreateAsync("SOAK-6", "Soak: record import");
        CalcInstance? calc = null;

        try
        {
            calc = await service.CreateAsync("elec.conductor-sizing", project.Id, "Soak feeder", cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            outcome.Expect(false, () => $"The calculation that takes the record could not be made: {ex.GetType().Name}: {ex.Message}");
        }

        if (calc is not null)
        {
            var applied = await service.ApplyRecordAsync(calc.Id, id, null, cancellationToken);
            outcome.Expect(applied.Succeeded, () => $"The licensed record could not be applied: {applied.Reason}");

            if (applied.Instance is { } instance)
            {
                var withheld = await service.WithholdLicensedAsync(new CalcInputSet(instance.Inputs), cancellationToken);
                var ampacity = withheld.Find("ampacity");
                outcome.Expect(ampacity is { Rows: null }, () => "The licensed record's table is in the inputs a calculation exports.");
                outcome.Expect(ampacity?.Provenance?.Reference?.Contains("licensed value withheld", StringComparison.Ordinal) == true, () => "The withheld table does not say its values were withheld.");
            }
        }

        // The control: a record that is not licensed exports, so "refused" above means something.
        var open = await importer.CommitAsync(new RecordImportRequest(System.Text.Encoding.UTF8.GetBytes("grout[text],C_fd[-]\r\nSoak grout,0.2\r\n"), "open.csv", "51", SoakProvenance("Soak open grout", licensed: false)), cancellationToken);
        outcome.Expect(open.Committed, () => $"The control record was not imported: {open.Refusal}");

        if (open.Committed)
        {
            var (csv, refusal) = await service.ExportRecordAsync(open.Records[0].Id, null, cancellationToken);
            outcome.Expect(csv is not null && refusal is null, () => $"The record that is not licensed did not export: {refusal}");
        }

        outcome.Fact("Licensed record: revisions saved", scale.RecordRevisions + 1L);
        outcome.Fact("Licensed record: attempts to release it accepted", released);
        outcome.Fact("Licensed record: revisions that exported", exported);
        outcome.Fact("Licensed record: seconds for the revisions", watch.Elapsed.TotalSeconds, "0.0");
    }
}

/// <summary>Await an exception without a test framework's helper: the soak is not a test class.</summary>
internal static class SoakAwait
{
    public static async Task<Exception?> ThrowsAsync(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}
