using System.Diagnostics;
using System.Security.Cryptography;
using Tempest.Calc.Import;
using Tempest.Calc.Import.Cad;
using Tempest.Calc.Import.Mapping;
using Tempest.Calc.Import.Scan;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;

namespace Tempest.Desktop.Tests.Soak;

internal static partial class SoakScenarios
{
    // ------------------------------------------------------------------
    // 5. Imports: each at its cap, and one past it
    // ------------------------------------------------------------------

    private const string ImportTemplate = "mech.tolerance-stack";

    /// <summary>
    /// Imports run through the path the screens use (the bounded reader, then the service): a scan deviation table at the byte cap and at the
    /// row cap, a CAD expression file and a CAD parameter CSV at the parameter cap and, for the expression file, at the byte cap, and
    /// reference records at their own caps. Each one past its cap is refused with a plain sentence, not an exception, and leaves the calculation
    /// byte for byte as it was. In the CI scale the files that are accepted are a tenth of the cap; the refusals always use the real caps.
    /// </summary>
    public static async Task ImportsAsync(SoakHost host, SoakScale scale, SoakOutcome outcome, CancellationToken cancellationToken)
    {
        var service = host.Service;
        var project = await host.Host.ProjectDirectory!.CreateAsync("SOAK-5", "Soak: imports");
        var inputs = SoakExamples.For(ImportTemplate).Single(e => e.Name == "bearing-housing-gap").Inputs;
        var target = await service.CreateAsync(ImportTemplate, project.Id, "Soak import target", inputs, cancellationToken);
        var rawBefore = await host.RawInstanceAsync(target);

        var capBytes = ImportLimits.DefaultMaxBytes;
        var bytesAccepted = (long)(capBytes * scale.ImportFraction);
        var rowsAccepted = (int)(ImportLimits.Scan.MaxRows * scale.ImportFraction);
        var scanSettings = new ScanSettings();
        var timings = new List<string>();

        // ---- scan: the byte cap -------------------------------------------------------------------------
        var profile = new ScanProfile(
            "soak-scan", 1, "Soak scan table", new ScanColumns("Element", Nominal: "Nominal", Actual: "Actual", Deviation: "Deviation"),
            [new ScanFeatureEntry("Housing bore depth", "contributors[0]")], ScanSoftware.Generic, TemplateId: ImportTemplate);
        var (savedProfile, profileRefusal) = await service.SaveScanProfileAsync(profile, cancellationToken);
        outcome.Expect(savedProfile is not null, () => $"The scan profile was refused: {profileRefusal}");

        var (table, tableRows) = SoakImportFiles.ScanTable("Housing bore depth", bytesAccepted);
        outcome.Expect(table.Length == bytesAccepted, () => $"The generated scan table is {table.Length:N0} bytes, not {bytesAccepted:N0}.");
        var read = await ReadAsync(table, "housing.csv", scanSettings.Limits, cancellationToken);
        outcome.Expect(read.File is not null, () => $"A {table.Length:N0}-byte scan table (the cap is {capBytes:N0}) was refused by the reader: {read.Refusal}");

        if (read.File is not null)
        {
            var request = new ScanImportRequest([new ScanFileInput(read.File)], profile, scanSettings);
            var watch = Stopwatch.StartNew();
            var preview = await service.PreviewScanImportAsync(target.Id, request, cancellationToken);
            watch.Stop();
            timings.Add($"scan {table.Length:N0} bytes, {tableRows:N0} rows: preview {watch.Elapsed.TotalSeconds:0.0} s");
            outcome.Expect(preview.Refusal is null && preview.Analysis is not null, () => $"The scan table of {tableRows:N0} rows was refused: {preview.Refusal}");
            outcome.Expect(preview.Stats.Count > 0 && preview.Stats[0].Count == tableRows, () => $"The scan statistics count {preview.Stats.FirstOrDefault()?.Count:N0} parts; the table has {tableRows:N0}.");
            outcome.Expect(preview.CanApply, () => $"The scan table cannot be applied: {string.Join(" ", preview.Reasons.Take(3))}");

            if (preview.CanApply)
            {
                watch.Restart();
                var applied = await service.ApplyScanImportAsync(target.Id, request with { ExpectedUpdatedAt = preview.InstanceUpdatedAt }, cancellationToken);
                watch.Stop();
                timings.Add($"scan apply {watch.Elapsed.TotalSeconds:0.0} s");
                outcome.Expect(applied.Action.Succeeded, () => $"Applying the scan table was refused: {applied.Action.Reason}");
                var stored = await service.Store.FindInstanceAsync(target.Id, cancellationToken);
                outcome.Expect(stored?.ScanImports is { Count: 1 }, () => "The applied scan import was not remembered on the calculation.");
                var storedSize = (await host.RawInstanceAsync(target))?.Length ?? 0;
                outcome.Expect(storedSize < CalcInstanceStore.MaxDocumentBytes, () => $"The calculation's stored record is {storedSize:N0} characters after a {table.Length:N0}-byte import.");
                outcome.Fact("Scan import: stored record after the import (KB)", storedSize / 1024.0, "0");
                target = stored ?? target;
            }
        }

        // ---- scan: the row cap --------------------------------------------------------------------------
        var rowProfile = new ScanProfile(
            "soak-rows", 1, "Soak scan rows", new ScanColumns("E", Deviation: "D"),
            [new ScanFeatureEntry("H", "contributors[0]")], ScanSoftware.Generic, TemplateId: ImportTemplate);
        var rowFile = await ReadAsync(SoakImportFiles.ScanRows(rowsAccepted), "rows.csv", scanSettings.Limits, cancellationToken);
        outcome.Expect(rowFile.File is not null, () => $"A {rowsAccepted:N0}-row scan table (header included) was refused by the reader: {rowFile.Refusal}");

        if (rowFile.File is not null)
        {
            var watch = Stopwatch.StartNew();
            var preview = await service.PreviewScanImportAsync(target.Id, new ScanImportRequest([new ScanFileInput(rowFile.File)], rowProfile, scanSettings), cancellationToken);
            watch.Stop();
            timings.Add($"scan {rowFile.File.Length:N0} bytes, {rowsAccepted:N0} rows: preview {watch.Elapsed.TotalSeconds:0.0} s");
            outcome.Expect(preview.Refusal is null && preview.Analysis is not null, () => $"A scan table of {rowsAccepted:N0} rows, the header among them (the cap is {ImportLimits.Scan.MaxRows:N0}), was refused: {preview.Refusal}");
            outcome.Expect(preview.Stats.Count > 0 && preview.Stats[0].Count == rowsAccepted - 1, () => $"The scan statistics count {preview.Stats.FirstOrDefault()?.Count:N0} parts; the table has {rowsAccepted - 1:N0} data rows.");
            outcome.Fact("Scan import at the row cap (s)", watch.Elapsed.TotalSeconds, "0.0");
        }

        // ---- scan: one past each cap ----------------------------------------------------------------------
        await ExpectRefusedAsync(host, target, outcome, "scan table one byte over the 5 MB cap (through the reader)",
            async () => (await ReadAsync(new byte[capBytes + 1], "big.csv", scanSettings.Limits, cancellationToken)).Refusal, "larger than");

        await ExpectRefusedAsync(host, target, outcome, "scan table one byte over the 5 MB cap (straight to the importer)",
            async () => (await service.PreviewScanImportAsync(target.Id, new ScanImportRequest([new ScanFileInput(Raw("big.csv", new byte[capBytes + 1]))], profile, scanSettings), cancellationToken)).Refusal, "larger than");

        var overRows = SoakImportFiles.ScanRows(ImportLimits.Scan.MaxRows + 1);
        var overRowsFile = await ReadAsync(overRows, "rows-over.csv", scanSettings.Limits, cancellationToken);
        outcome.Expect(overRowsFile.File is not null, () => $"A table of {ImportLimits.Scan.MaxRows + 1:N0} rows of four bytes is only {overRows.Length:N0} bytes and must reach the importer: {overRowsFile.Refusal}");

        if (overRowsFile.File is not null)
        {
            await ExpectRefusedAsync(host, target, outcome, $"scan table of {ImportLimits.Scan.MaxRows + 1:N0} rows, the header among them (one over the row cap)",
                async () => (await service.PreviewScanImportAsync(target.Id, new ScanImportRequest([new ScanFileInput(overRowsFile.File)], rowProfile, scanSettings), cancellationToken)).Refusal, "more than");
        }

        var manyFiles = Enumerable.Range(0, ScanImporter.MaxFiles + 1).Select(i => new ScanFileInput(Raw($"part{i}.csv", System.Text.Encoding.UTF8.GetBytes($"E,D\nH,{i % 7}\nH,{(i + 1) % 7}\n// {i}\n")))).ToList();
        await ExpectRefusedAsync(host, target, outcome, $"{ScanImporter.MaxFiles + 1} scan files at once",
            async () => (await service.PreviewScanImportAsync(target.Id, new ScanImportRequest(manyFiles, rowProfile, scanSettings), cancellationToken)).Refusal, "at most");

        var heavy = Enumerable.Range(0, 5).Select(i => { var bytes = new byte[capBytes]; Array.Fill(bytes, (byte)'\n'); bytes[0] = (byte)('a' + i); return new ScanFileInput(Raw($"heavy{i}.csv", bytes)); }).ToList();
        await ExpectRefusedAsync(host, target, outcome, "five 5 MB scan files together (over the 20 MB total)",
            async () => (await service.PreviewScanImportAsync(target.Id, new ScanImportRequest(heavy, rowProfile, scanSettings), cancellationToken)).Refusal, "together");

        // ---- CAD: expression file and parameter CSV ------------------------------------------------------
        var cadProfile = new CadMappingProfile(
            "soak-nx", 1, "Soak CAD parameters",
            [new CadMappingEntry("plate_len", "contributors[0].nominal"), new CadMappingEntry("gap_min", "minimum"), new CadMappingEntry("gap_max", "maximum")],
            CadSystem.Nx, ImportTemplate);
        var (savedCad, cadRefusal) = await service.SaveImportProfileAsync(cadProfile, cancellationToken);
        outcome.Expect(savedCad is not null, () => $"The CAD profile was refused: {cadRefusal}");
        var cadLimits = ImportLimits.Default;
        var parameterCap = cadLimits.MaxParameters;

        var nx = SoakImportFiles.NxExpressions(parameterCap, bytesAccepted);
        var nxRead = await ReadAsync(nx, "soak.exp", cadLimits, cancellationToken);
        outcome.Expect(nxRead.File is not null, () => $"A {nx.Length:N0}-byte expression file (the cap is {capBytes:N0}) was refused by the reader: {nxRead.Refusal}");

        if (nxRead.File is not null)
        {
            var request = new CadImportRequest(nxRead.File, CadSourceFormat.NxExpressions, cadProfile);
            var watch = Stopwatch.StartNew();
            var preview = await service.PreviewCadImportAsync(target.Id, request, cancellationToken);
            watch.Stop();
            timings.Add($"CAD expression file {nx.Length:N0} bytes, {parameterCap:N0} parameters: preview {watch.Elapsed.TotalSeconds:0.0} s");
            outcome.Expect(preview.Refusal is null && preview.Parse is not null, () => $"An expression file of {parameterCap:N0} parameters in {nx.Length:N0} bytes was refused: {preview.Refusal}");
            outcome.Expect(preview.Parse?.Parameters.Count == parameterCap, () => $"{preview.Parse?.Parameters.Count:N0} parameters were read; the file has {parameterCap:N0}.");
            outcome.Expect(preview.CanApply, () => $"The expression file cannot be applied: {string.Join(" ", preview.Reasons.Take(3))}");

            if (preview.CanApply)
            {
                var applied = await service.ApplyCadImportAsync(target.Id, request with { ExpectedUpdatedAt = preview.InstanceUpdatedAt }, cancellationToken);
                outcome.Expect(applied.Action.Succeeded, () => $"Applying the expression file was refused: {applied.Action.Reason}");
                target = applied.Action.Instance ?? target;
            }
        }

        var csv = SoakImportFiles.GenericCsv(parameterCap);
        var csvRead = await ReadAsync(csv, "soak.csv", cadLimits, cancellationToken);

        if (csvRead.File is not null)
        {
            var watch = Stopwatch.StartNew();
            var preview = await service.PreviewCadImportAsync(target.Id, new CadImportRequest(csvRead.File, CadSourceFormat.GenericCsv, cadProfile), cancellationToken);
            watch.Stop();
            timings.Add($"CAD parameter CSV {csv.Length:N0} bytes, {parameterCap:N0} parameters: preview {watch.Elapsed.TotalSeconds:0.0} s");
            outcome.Expect(preview.Refusal is null && preview.Parse?.Parameters.Count == parameterCap, () => $"A parameter CSV of {parameterCap:N0} parameters was refused or misread: {preview.Refusal}");
        }
        else
            outcome.Expect(false, () => $"A parameter CSV of {csv.Length:N0} bytes was refused by the reader: {csvRead.Refusal}");

        await ExpectRefusedAsync(host, target, outcome, $"expression file of {parameterCap + 1:N0} parameters (one over the parameter cap)",
            async () => await PreviewCadAsync(service, target, SoakImportFiles.NxExpressions(parameterCap + 1, 0), "over.exp", CadSourceFormat.NxExpressions, cadProfile, cancellationToken), "more than");

        await ExpectRefusedAsync(host, target, outcome, $"parameter CSV of {parameterCap + 1:N0} parameters (one over the parameter cap)",
            async () => await PreviewCadAsync(service, target, SoakImportFiles.GenericCsv(parameterCap + 1), "over.csv", CadSourceFormat.GenericCsv, cadProfile, cancellationToken), "more than");

        await ExpectRefusedAsync(host, target, outcome, "expression file one byte over the 5 MB cap (through the reader)",
            async () => (await ReadAsync(SoakImportFiles.NxExpressions(10, capBytes + 1), "huge.exp", cadLimits, cancellationToken)).Refusal, "larger than");

        await ExpectRefusedAsync(host, target, outcome, "expression file one byte over the 5 MB cap (straight to the importer)",
            async () => (await service.PreviewCadImportAsync(target.Id, new CadImportRequest(Raw("huge.exp", SoakImportFiles.NxExpressions(10, capBytes + 1)), CadSourceFormat.NxExpressions, cadProfile), cancellationToken)).Refusal, "larger than");

        // ---- inputs CSV, and a calculation with its tables at their caps -----------------------------------
        await InputsCsvAsync(host, scale, outcome, cancellationToken);
        await FrameAtItsCapsAsync(host, scale, outcome, cancellationToken);

        // ---- reference records at their caps ------------------------------------------------------------
        await RecordCapsAsync(host, outcome, cancellationToken);

        // The refusals changed nothing: the calculation is as the last accepted import left it (the record is read back and kept in step below).
        var rawNow = await host.RawInstanceAsync(target);
        outcome.Expect(rawNow is not null && rawNow != rawBefore, () => "The calculation did not change though the imports at the caps were applied.");

        outcome.Fact("Largest scan table accepted (bytes)", bytesAccepted);
        outcome.Fact("Largest scan table accepted (rows)", tableRows);
        outcome.Fact("Scan table at the row cap (rows, header included)", rowsAccepted);
        outcome.Fact("Largest CAD expression file accepted (bytes)", bytesAccepted);
        outcome.Fact("CAD parameters accepted", parameterCap);
        outcome.Fact("Refusals one past a cap", outcome.Facts.Count(f => f.Key.StartsWith("Refused: ", StringComparison.Ordinal)));

        foreach (var line in timings)
            outcome.Fact("Import timing", line);
    }

    private static ImportedFile Raw(string name, byte[] bytes) => new(name, bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)));

    /// <summary>The file as the bounded reader hands it to the importers, or the reader's own plain-sentence refusal.</summary>
    private static async Task<(ImportedFile? File, string? Refusal)> ReadAsync(byte[] bytes, string name, ImportLimits limits, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new MemoryStream(bytes, writable: false);
            return (await BoundedInputReader.ReadAsync(stream, name, limits, cancellationToken), null);
        }
        catch (ImportRefusedException ex)
        {
            return (null, ex.Message);
        }
    }

    private static async Task<string?> PreviewCadAsync(CalcInstanceService service, CalcInstance target, byte[] bytes, string name, CadSourceFormat format, CadMappingProfile profile, CancellationToken cancellationToken)
    {
        var read = await ReadAsync(bytes, name, ImportLimits.Default, cancellationToken);

        if (read.File is null)
            return read.Refusal;

        return (await service.PreviewCadImportAsync(target.Id, new CadImportRequest(read.File, format, profile), cancellationToken)).Refusal;
    }

    /// <summary>One request past a cap: it must come back as a refusal sentence containing <paramref name="expectedText"/>, with no exception, and change nothing stored.</summary>
    private static async Task ExpectRefusedAsync(SoakHost host, CalcInstance target, SoakOutcome outcome, string what, Func<Task<string?>> attempt, string expectedText)
    {
        var before = await host.RawInstanceAsync(target);
        string? refusal;

        try
        {
            refusal = await attempt();
        }
        catch (Exception ex)
        {
            outcome.Expect(false, () => $"{what}: threw {ex.GetType().Name} instead of being refused cleanly: {ex.Message}");
            return;
        }

        outcome.Expect(!string.IsNullOrWhiteSpace(refusal), () => $"{what}: was NOT refused (cap not enforced).");
        outcome.Expect(refusal is null || refusal.Contains(expectedText, StringComparison.OrdinalIgnoreCase), () => $"{what}: refused, but with '{refusal}' (expected it to say '{expectedText}').");
        outcome.Expect(refusal is null || (refusal.Length < 300 && !refusal.Contains("Exception", StringComparison.Ordinal)), () => $"{what}: the refusal is not a plain sentence: '{refusal}'.");
        outcome.Expect(string.Equals(before, await host.RawInstanceAsync(target), StringComparison.Ordinal), () => $"{what}: the calculation changed though the import was refused.");
        outcome.Fact("Refused: " + what, refusal ?? "(not refused)");
    }

    // ------------------------------------------------------------------

    private static async Task RecordCapsAsync(SoakHost host, SoakOutcome outcome, CancellationToken cancellationToken)
    {
        var service = host.Service;
        CalcRow Row(int cells) => new(Enumerable.Range(0, cells).ToDictionary(c => $"c{c:00}", c => new CalcCell(c * 1.5, "mm"), StringComparer.Ordinal));
        var table = CalcDataValue_Table("t", Enumerable.Range(0, CalcDataRecord.MaxRows).Select(_ => Row(CalcDataRecord.MaxCellsPerRow)).ToList());
        var longText = new string('x', CalcDataRecord.MaxTextLength);

        CalcDataRecord Record(IReadOnlyList<CalcDataValue> values, string? title = null) =>
            new(Guid.Empty, "soak.table", title ?? "Soak table record", 1, "Soak source", "Soak edition", "Table 1", "Soak licence holder", "soak", false, CalcDataOrigin.UserEntered, values, DateTimeOffset.UtcNow);

        // At the caps: 200 values, one of them a table of 500 rows of 50 cells, every text 200 characters.
        var values = new List<CalcDataValue> { table };
        values.AddRange(Enumerable.Range(1, CalcDataRecord.MaxValues - 1).Select(i => new CalcDataValue($"v{i:000}", i, "mm", longText)));
        var watch = Stopwatch.StartNew();
        var (record, refusal) = await service.SaveRecordAsync(Record(values, longText), cancellationToken);
        watch.Stop();
        outcome.Expect(record is not null, () => $"A reference record at every cap (200 values, a 500 × 50 table, 200-character texts) was refused: {refusal}");
        outcome.Fact("Reference record at its caps: saved in (ms)", watch.Elapsed.TotalMilliseconds, "0");

        if (record is not null)
        {
            var read = await service.Store.FindRecordAsync(record.Id, null, cancellationToken);
            outcome.Expect(read is not null && read.Values.Count == CalcDataRecord.MaxValues && read.Values[0].Rows?.Count == CalcDataRecord.MaxRows, () => "The reference record at its caps did not read back whole.");
        }

        // One past each cap: a plain refusal, nothing saved.
        var past = new (string What, CalcDataRecord Draft)[]
        {
            ($"{CalcDataRecord.MaxValues + 1} values", Record([.. Enumerable.Range(0, CalcDataRecord.MaxValues + 1).Select(i => new CalcDataValue($"v{i:000}", i, "mm"))])),
            ($"{CalcDataRecord.MaxRows + 1} table rows", Record([CalcDataValue_Table("t", [.. Enumerable.Range(0, CalcDataRecord.MaxRows + 1).Select(_ => Row(2))])])),
            ($"{CalcDataRecord.MaxCellsPerRow + 1} cells in a row", Record([CalcDataValue_Table("t", [Row(CalcDataRecord.MaxCellsPerRow + 1)])])),
            ($"a {CalcDataRecord.MaxTextLength + 1}-character text", Record([new CalcDataValue("v", 1, "mm", new string('x', CalcDataRecord.MaxTextLength + 1))])),
        };

        foreach (var (what, draft) in past)
        {
            var (saved, why) = await service.SaveRecordAsync(draft, cancellationToken);
            outcome.Expect(saved is null && !string.IsNullOrWhiteSpace(why) && why.Length < 300, () => $"A reference record with {what} was not refused cleanly ({(saved is null ? why : "saved")}).");
            outcome.Fact("Refused: reference record with " + what, why ?? "(not refused)");
        }

        // The biggest the caps allow in one document: 200 tables of 500 × 50 cells. Whatever the answer, it is a refusal sentence or a saved record, never a crash.
        var huge = Record([.. Enumerable.Range(0, CalcDataRecord.MaxValues).Select(i => CalcDataValue_Table($"t{i:000}", [.. Enumerable.Range(0, CalcDataRecord.MaxRows).Select(_ => Row(CalcDataRecord.MaxCellsPerRow))]))]);

        try
        {
            var (savedHuge, whyHuge) = await service.SaveRecordAsync(huge, cancellationToken);
            outcome.Expect(savedHuge is not null || !string.IsNullOrWhiteSpace(whyHuge), () => "The largest record the caps allow was neither saved nor refused with a sentence.");
            outcome.Fact("Record of 200 tables of 500 × 50 cells", savedHuge is not null ? "saved" : $"refused: {whyHuge}");
        }
        catch (Exception ex)
        {
            outcome.Expect(false, () => $"Saving the largest record the caps allow threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static CalcDataValue CalcDataValue_Table(string symbol, IReadOnlyList<CalcRow> rows) => new(symbol, Rows: rows);
}
