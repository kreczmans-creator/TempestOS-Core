using System.Globalization;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Import;
using Tempest.Calc.Sheets;
using Tempest.Core.Persistence;

namespace Tempest.Calc.Library;

/// <summary>
/// Addendum C, C-3 — the record import. It reads a CSV that follows one of Track P's record templates through the hardened import primitives
/// (the bounded reader, the strict CSV reader and the declared number format), maps its columns, checks every column's unit against the unit
/// the calculator takes, checks every row (the template's own rules, then the ranges the calculators themselves refuse), and plans the records it
/// would save, each stamped with the provenance the person gave, the signed-in person, the time and the file's SHA-256. Preview changes nothing.
/// Commit saves the whole set in one transaction or none of it: one error anywhere refuses the file, and a failed write leaves the store as it was.
/// </summary>
/// <remarks>
/// Nothing here exports a record. The licence lock (§A5) is unchanged: a record imported as licensed (the default) is licensed in every
/// revision, <see cref="CalcInstanceService.ExportRecordAsync"/> refuses it and a calculation's export withholds its values.
/// </remarks>
public sealed class RecordImportService
{
    /// <summary>The most rows of a file read: a table takes 500 rows at most, and a record is made per row for the other templates.</summary>
    public const int MaxFileRows = 1001;

    private const string NoteSeparator = "\n";

    private readonly CalcInstanceService _service;

    internal RecordImportService(CalcInstanceService service) => _service = service ?? throw new ArgumentNullException(nameof(service));

    /// <summary>The record kinds the visible calculators declare, with what each holds.</summary>
    public IReadOnlyDictionary<string, RecordKindSchema> Schemas() => RecordKindSchema.Build(_service.Catalogue);

    /// <summary>The schema of one record kind, or <see langword="null"/> when no visible calculator declares it.</summary>
    public RecordKindSchema? SchemaFor(string kind) => Schemas().GetValueOrDefault(kind);

    /// <summary>The templates that can be imported into this build: a calculator declares their kind.</summary>
    public IReadOnlyList<RecordTemplate> ImportableTemplates()
    {
        var schemas = Schemas();
        return [.. RecordTemplates.All.Where(t => t.Disposition == RecordTemplateDisposition.Kind && schemas.ContainsKey(t.Kind))];
    }

    /// <summary>A blank template as a file: its header row, ready to be filled in a spreadsheet. It holds no data.</summary>
    public static string BlankTemplateCsv(RecordTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return template.Header + "\r\n";
    }

    /// <summary>
    /// A table typed on the records screen, as the CSV the importer reads: the template's header (with the units the person set), then the rows
    /// that have anything in them. <see cref="RecordGridCsv.GridRowOfLine"/> says which typed row each data line is, so a finding on line 3 is
    /// reported against the right row. The typed table goes through the same checks as a file.
    /// </summary>
    /// <param name="template">The template whose columns the grid has.</param>
    /// <param name="units">The unit of each column as typed (ignored for a text column; blank for a dimensionless one).</param>
    /// <param name="rows">The typed rows, one text per column.</param>
    public static RecordGridCsv GridToCsv(RecordTemplate template, IReadOnlyList<string> units, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(rows);

        var sb = new System.Text.StringBuilder();
        sb.Append(string.Join(',', template.Columns.Select((c, i) => $"{QuoteCsv(c.Symbol)}[{(c.IsText ? "text" : string.IsNullOrWhiteSpace(i < units.Count ? units[i] : null) ? "-" : units[i].Trim())}]"))).Append("\r\n");
        var lineOf = new List<int>();

        for (var r = 0; r < rows.Count; r++)
        {
            var cells = Enumerable.Range(0, template.Columns.Count).Select(i => i < rows[r].Count ? rows[r][i] ?? string.Empty : string.Empty).ToList();

            if (cells.All(string.IsNullOrWhiteSpace))
                continue;

            sb.Append(string.Join(',', cells.Select(c => QuoteCsv(c.Trim())))).Append("\r\n");
            lineOf.Add(r + 1);
        }

        return new RecordGridCsv(new System.Text.UTF8Encoding(false).GetBytes(sb.ToString()), lineOf);
    }

    private static string QuoteCsv(string text) =>
        text.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : text;

    // ------------------------------------------------------------------
    // Provenance file
    // ------------------------------------------------------------------

    /// <summary>
    /// Reads a Track P <c>_provenance.csv</c> (its header, then one row per record) so the screen can fill the provenance fields. A row with a
    /// field that cannot be read is left out and named in <paramref name="problems"/>.
    /// </summary>
    public static IReadOnlyList<RecordProvenanceRow> ReadProvenanceFile(byte[] bytes, out IReadOnlyList<string> problems)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var issues = new List<string>();
        problems = issues;
        var rows = new List<RecordProvenanceRow>();

        CsvTable table;

        try
        {
            var file = BoundedInputReader.FromBytes(bytes, "_provenance.csv");
            table = CsvReader.Read(BoundedInputReader.DecodeText(file), ',', new ImportBudget(new ImportLimits { MaxRows = 500, MaxColumns = 32 }));
        }
        catch (ImportRefusedException ex)
        {
            issues.Add(ex.Message);
            return rows;
        }

        if (table.Rows.Count == 0)
        {
            issues.Add("The provenance file is empty.");
            return rows;
        }

        var header = table.Rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        var missing = new[] { "kind", "title", "source_title", "edition", "table_or_clause", "licence_holder" }.Where(h => !header.Contains(h)).ToList();

        if (missing.Count > 0)
        {
            issues.Add($"The provenance file has no column {string.Join(", ", missing)}.");
            return rows;
        }

        string Cell(IReadOnlyList<string> row, string name) => header.IndexOf(name) is var i and >= 0 && i < row.Count ? row[i].Trim() : string.Empty;

        for (var r = 1; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            var line = table.Lines[r];
            var licensedText = Cell(row, "licensed_source");
            bool licensed;

            if (licensedText.Length == 0 || licensedText.Equals("true", StringComparison.OrdinalIgnoreCase) || licensedText is "1" || licensedText.Equals("yes", StringComparison.OrdinalIgnoreCase))
                licensed = true;
            else if (licensedText.Equals("false", StringComparison.OrdinalIgnoreCase) || licensedText is "0" || licensedText.Equals("no", StringComparison.OrdinalIgnoreCase))
                licensed = false;
            else
            {
                issues.Add($"Line {line}: licensed_source must be true or false, not '{licensedText}'.");
                continue;
            }

            var originText = Cell(row, "origin");
            var origin = CalcDataOrigin.UserEntered;

            if (originText.Length > 0 && (!Enum.TryParse(originText, true, out origin) || origin == CalcDataOrigin.Indicative))
            {
                issues.Add($"Line {line}: origin must be UserEntered, Derived or ManufacturerExample, not '{originText}'.");
                continue;
            }

            var notes = Cell(row, "notes");
            var provenance = new RecordProvenanceInput(
                Cell(row, "title"), Cell(row, "source_title"), Cell(row, "edition"), Cell(row, "amendment"), Cell(row, "table_or_clause"), Cell(row, "licence_holder"),
                licensed, origin, notes.Length == 0 ? null : notes);

            if (provenance.Problem() is { } problem)
            {
                issues.Add($"Line {line}: {problem}");
                continue;
            }

            rows.Add(new RecordProvenanceRow(Cell(row, "kind"), provenance, Cell(row, "entered_by")));
        }

        return rows;
    }

    // ------------------------------------------------------------------
    // Preview and commit
    // ------------------------------------------------------------------

    /// <summary>What <paramref name="request"/> would do, without doing it.</summary>
    public async Task<RecordImportPreview> PreviewAsync(RecordImportRequest request, CancellationToken cancellationToken = default) =>
        (await AnalyseAsync(request, cancellationToken).ConfigureAwait(false)).Preview;

    /// <summary>
    /// Saves every record of the import, or none. The file is analysed again first, against the store as it is now: if anything is an error nothing
    /// is written; the records are then written in one transaction, and a failed write leaves the store as it was.
    /// </summary>
    public async Task<RecordImportOutcome> CommitAsync(RecordImportRequest request, CancellationToken cancellationToken = default)
    {
        var (preview, records) = await AnalyseAsync(request, cancellationToken).ConfigureAwait(false);

        if (!preview.CanCommit || records.Count == 0)
            return new RecordImportOutcome(false, [], $"Nothing was saved: {preview.Blocker ?? "there is nothing to save."}", preview);

        try
        {
            await _service.Store.SaveRecordsAsync(records, cancellationToken).ConfigureAwait(false);
        }
        catch (CalcInstanceImmutableException)
        {
            return new RecordImportOutcome(false, [], "Nothing was saved: another save took one of these record revisions at the same moment. Preview the file again and commit it again.", preview);
        }
        catch (InvalidOperationException ex)
        {
            return new RecordImportOutcome(false, [], $"Nothing was saved: {ex.Message}", preview);
        }
        catch (PersistenceException ex)
        {
            return new RecordImportOutcome(false, [], $"Nothing was saved: the store could not commit ({ex.Message}).", preview);
        }

        return new RecordImportOutcome(true, records, null, preview);
    }

    private sealed record Analysis(RecordImportPreview Preview, IReadOnlyList<CalcDataRecord> Records);

    private static Analysis Refuse(string reason, RecordTemplate? template = null, string? fileName = null, string? sha = null) =>
        new(RecordImportPreview.Refused(reason, template, fileName, sha), []);

    private async Task<Analysis> AnalyseAsync(RecordImportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Provenance);

        var template = RecordTemplates.Find(request.TemplateId);

        if (template is null)
            return Refuse($"There is no record template '{request.TemplateId}'. Pick one from the list.");

        switch (template.Disposition)
        {
            case RecordTemplateDisposition.Superseded:
                return Refuse($"Template {template.FileName} is not imported. {template.Note}", template);

            case RecordTemplateDisposition.Proposed:
                return Refuse($"Template {template.FileName} fills the proposed kind '{template.Kind}', which no calculator declares yet, so there is no record to save it as. {template.Note}", template);
        }

        var schema = SchemaFor(template.Kind);

        if (schema is null)
            return Refuse($"No calculator available in this build declares the record kind '{template.Kind}', so template {template.FileName} cannot be imported.", template);

        // Read the file through the bounded reader and the strict CSV reader: a malformed file is refused whole, with the line.
        ImportedFile file;
        CsvTable table;
        char delimiter;

        try
        {
            if (request.Bytes is not { Length: > 0 })
                return Refuse("The file is empty.", template, request.FileName);

            file = BoundedInputReader.FromBytes(request.Bytes, request.FileName ?? "typed-entry.csv");
            var text = BoundedInputReader.DecodeText(file);
            delimiter = request.Delimiter ?? CsvReader.DetectDelimiter(text);
            table = CsvReader.Read(text, delimiter, new ImportBudget(new ImportLimits { MaxRows = MaxFileRows, MaxColumns = 64 }, cancellationToken));
        }
        catch (ImportRefusedException ex)
        {
            return Refuse(ex.Message, template, request.FileName);
        }

        var sha = request.FileName is null ? null : file.Sha256;
        var fileIssues = new List<RecordImportIssue>();
        var profile = request.Number ?? NumberProfile.Point;

        if (!profile.IsValid)
            return Refuse("The number format is not valid: the grouping character cannot be the decimal mark.", template, file.FileName, sha);

        if (table.Rows.Count == 0)
            return Refuse("The file is empty.", template, file.FileName, sha);

        var provenance = request.Provenance;
        var isMaterial = schema.IsMaterial;

        if (provenance.Problem(titleOptional: isMaterial) is { } provenanceProblem)
            fileIssues.Add(new RecordImportIssue(RecordSeverity.Error, null, null, provenanceProblem));

        if (delimiter != ',')
            fileIssues.Add(new RecordImportIssue(RecordSeverity.Warning, 1, null, $"The file separates its fields with '{(delimiter == '\t' ? "tab" : delimiter.ToString())}', not a comma. Check the number format below matches how its numbers are written."));

        // Header, mapping, roles and units.
        var headerCells = table.Rows[0];
        var bound = template.Columns.ToDictionary(c => c.Symbol, c => new BoundColumn(c), StringComparer.Ordinal);
        BindRoles(template, schema, bound);
        var fileColumns = MapColumns(request, template, headerCells, bound, fileIssues);

        foreach (var column in bound.Values.Where(c => c.Mapped))
            CheckUnit(column, fileIssues);

        foreach (var column in bound.Values.Where(c => !c.Mapped))
        {
            var needed = template.Checks.Any(c => c.Severity == RecordSeverity.Error && c.Kind is RecordCheckKind.Required or RecordCheckKind.RequiredFirstRow && c.Columns.Contains(column.Symbol, StringComparer.Ordinal));
            fileIssues.Add(new RecordImportIssue(needed ? RecordSeverity.Error : RecordSeverity.Warning, 1, column.Symbol, needed
                ? $"The file has no column for {column.Template.Header}; this template needs it."
                : $"The file has no column for {column.Template.Header}; it is left blank."));
        }

        var tableSymbol = bound.Values.FirstOrDefault(c => c.Role == RecordColumnRole.TableColumn)?.Table;

        if (tableSymbol is not null)
        {
            foreach (var column in bound.Values.Where(c => c.Mapped && c.Role is RecordColumnRole.Label or RecordColumnRole.Extra))
                fileIssues.Add(new RecordImportIssue(RecordSeverity.Error, 1, column.Symbol, $"{column.Symbol} is not a column of the table '{tableSymbol.Symbol}' that this template fills ({string.Join(", ", tableSymbol.Columns.Select(c => c.Key))}). Leave it out of the file or map it to one of those."));

            var absent = tableSymbol.Columns.Where(c => c.IsRequired && !bound.Values.Any(b => b.Role == RecordColumnRole.TableColumn && string.Equals(b.Target, c.Key, StringComparison.Ordinal))).Select(c => c.Key).ToList();

            if (absent.Count > 0)
                fileIssues.Add(new RecordImportIssue(RecordSeverity.Warning, null, null, $"This template holds only some columns of the table '{tableSymbol.Symbol}' (not {string.Join(", ", absent)}). Applying the record replaces the whole table on the sheet, so enter the rest there."));
        }

        // Rows.
        var rows = ParseRows(table, headerCells.Count, bound, profile);

        if (rows.Count == 0 && fileIssues.All(i => i.Severity != RecordSeverity.Error))
            fileIssues.Add(new RecordImportIssue(RecordSeverity.Error, null, null, "The file holds a header row and no data rows."));

        SchemaChecks(schema, template, rows, bound);
        RecordTemplateChecks.Run(template, rows, bound, fileIssues);

        var columns = BuildColumnViews(fileColumns);
        var views = rows.Select(r => new RecordImportRowView(r.Line, r.Raw, r.Issues)).ToList();
        var errors = fileIssues.Any(i => i.Severity == RecordSeverity.Error) || rows.Any(r => r.Issues.Any(i => i.Severity == RecordSeverity.Error));

        if (errors)
            return new Analysis(new RecordImportPreview(template, file.FileName, sha, columns, views, fileIssues, [], null, delimiter), []);

        // Plan and prepare the records.
        var plan = await PlanAsync(request, template, schema, bound, rows, file, sha, fileIssues, cancellationToken).ConfigureAwait(false);
        var planned = plan.Select(p => new RecordImportPlanned(p.Record.Title, p.Record.Kind, p.Record.Revision, p.Record.Revision > 1 ? p.Record.Id : null, [.. p.Record.Values.Select(v => v.Symbol)], p.Record.Values.Max(v => v.Rows?.Count ?? 0), p.Record.IsLicensed())).ToList();
        var stillErrors = fileIssues.Any(i => i.Severity == RecordSeverity.Error) || rows.Any(r => r.Issues.Any(i => i.Severity == RecordSeverity.Error));

        return new Analysis(
            new RecordImportPreview(template, file.FileName, sha, columns, views, fileIssues, stillErrors ? [] : planned, null, delimiter),
            stillErrors ? [] : [.. plan.Select(p => p.Record)]);
    }

    // ------------------------------------------------------------------
    // Columns
    // ------------------------------------------------------------------

    private sealed record FileColumn(int Index, string Header, string Symbol, string Unit, BoundColumn? Target);

    private static List<FileColumn> MapColumns(RecordImportRequest request, RecordTemplate template, IReadOnlyList<string> headerCells, Dictionary<string, BoundColumn> bound, List<RecordImportIssue> issues)
    {
        var list = new List<FileColumn>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < headerCells.Count; i++)
        {
            var raw = headerCells[i].Trim();
            var (symbol, unit) = RecordUnits.SplitHeader(raw);

            if (symbol.Length == 0)
            {
                issues.Add(new RecordImportIssue(RecordSeverity.Error, 1, null, $"Column {i + 1} of the header has no name."));
                list.Add(new FileColumn(i, raw, symbol, unit, null));
                continue;
            }

            if (!seen.Add(symbol))
            {
                issues.Add(new RecordImportIssue(RecordSeverity.Error, 1, symbol, $"The header names '{symbol}' twice."));
                list.Add(new FileColumn(i, raw, symbol, unit, null));
                continue;
            }

            string? target = symbol;
            var mapped = false;

            if (request.ColumnMap is { } map && (map.TryGetValue(raw, out var viaRaw) || map.TryGetValue(symbol, out viaRaw)))
            {
                target = viaRaw;
                mapped = true;
            }

            if (target is null)
            {
                list.Add(new FileColumn(i, raw, symbol, unit, null));
                continue;
            }

            if (!bound.TryGetValue(target, out var column))
            {
                issues.Add(new RecordImportIssue(RecordSeverity.Error, 1, symbol, mapped
                    ? $"'{target}' is not a column of template {template.FileName}."
                    : $"'{symbol}' is not a column of template {template.FileName} ({string.Join(", ", template.Columns.Select(c => c.Symbol))}). Map it to one of them, or leave it out."));
                list.Add(new FileColumn(i, raw, symbol, unit, null));
                continue;
            }

            if (column.Mapped)
            {
                issues.Add(new RecordImportIssue(RecordSeverity.Error, 1, symbol, $"Two columns of the file are mapped to {column.Symbol}."));
                list.Add(new FileColumn(i, raw, symbol, unit, null));
                continue;
            }

            column.FileIndex = i;
            column.FileSaysText = string.Equals(unit, "text", StringComparison.OrdinalIgnoreCase);
            column.FileUnitText = column.FileSaysText || unit == "-" ? string.Empty : unit;
            list.Add(new FileColumn(i, raw, symbol, unit, column));
        }

        return list;
    }

    /// <summary>Works out, for each template column, what it becomes: a scalar of the kind, a column of its table, a label, an extra, or a material property.</summary>
    private static void BindRoles(RecordTemplate template, RecordKindSchema schema, Dictionary<string, BoundColumn> bound)
    {
        if (schema.IsMaterial)
        {
            BindMaterialRoles(template, bound);
            return;
        }

        // The table this template fills: the table symbol that holds the most of its columns.
        var tableSymbol = schema.Tables
            .Select(t => (Table: t, Count: bound.Keys.Count(k => t.Column(k) is not null && schema.Symbol(k) is not { IsTable: false })))
            .Where(x => x.Count > 0)
            .OrderByDescending(x => x.Count)
            .Select(x => x.Table)
            .FirstOrDefault();

        foreach (var column in bound.Values)
        {
            if (schema.Symbol(column.Symbol) is { IsTable: false } scalar)
            {
                column.Role = RecordColumnRole.Scalar;
                column.Target = scalar.Symbol;
                column.ScalarSymbol = scalar;
                Expect(column, scalar.Kind, scalar.Unit);
            }
            else if (tableSymbol?.Column(column.Symbol) is { } tableColumn)
            {
                column.Role = RecordColumnRole.TableColumn;
                column.Target = tableColumn.Key;
                column.Table = tableSymbol;
                column.TableColumn = tableColumn;
                Expect(column, tableColumn.Kind, tableColumn.Unit);
            }
            else if (column.Template.IsText)
            {
                column.Role = RecordColumnRole.Label;
                column.ExpectedKind = RecordColumnKind.Text;
            }
            else
            {
                column.Role = RecordColumnRole.Extra;
                Expect(column, column.Template.EngineUnit.Length > 0 ? CalcInputKind.Quantity : CalcInputKind.Number, column.Template.EngineUnit);
            }
        }
    }

    private static void BindMaterialRoles(RecordTemplate template, Dictionary<string, BoundColumn> bound)
    {
        foreach (var column in bound.Values)
        {
            switch (template.Id, column.Symbol)
            {
                case (_, "material"):
                    column.Role = RecordColumnRole.MaterialName;
                    column.ExpectedKind = RecordColumnKind.Text;
                    break;

                case ("62", var property) when MaterialProperties.Find(property, out _) is { Shape: MaterialPropertyShape.Scalar } def:
                    column.Role = RecordColumnRole.MaterialProperty;
                    column.Target = def.Key;
                    column.Material = def;
                    ExpectMaterial(column, def.SiUnit);
                    break;

                case ("63", "T"):
                    column.Role = RecordColumnRole.MaterialProperty;
                    column.Target = "T";
                    Expect(column, CalcInputKind.Quantity, "K");
                    column.DimensionOnly = true;
                    break;

                case ("63", "v"):
                    column.Role = RecordColumnRole.MaterialProperty;
                    column.Target = "v";
                    column.ExpectedKind = RecordColumnKind.Number;
                    break;

                case ("64", "eps"):
                    column.Role = RecordColumnRole.MaterialProperty;
                    column.Target = "eps";
                    column.ExpectedKind = RecordColumnKind.Number;
                    break;

                case ("64", "Es"):
                    column.Role = RecordColumnRole.MaterialProperty;
                    column.Target = "Es";
                    Expect(column, CalcInputKind.Quantity, "Pa");
                    column.DimensionOnly = true;
                    break;

                case ("65", "mu"):
                    column.Role = RecordColumnRole.MaterialProperty;
                    column.Target = "mu";
                    column.ExpectedKind = RecordColumnKind.Number;
                    break;

                default:
                    column.Role = RecordColumnRole.Label;
                    column.Target = column.Symbol;
                    column.ExpectedKind = RecordColumnKind.Text;
                    break;
            }
        }
    }

    private static void ExpectMaterial(BoundColumn column, string? siUnit)
    {
        if (siUnit is null)
        {
            column.ExpectedKind = RecordColumnKind.Quantity;
            column.AnyUnit = true;
            return;
        }

        if (siUnit.Length == 0)
        {
            column.ExpectedKind = RecordColumnKind.Number;
            return;
        }

        Expect(column, CalcInputKind.Quantity, siUnit);
        column.DimensionOnly = true;
    }

    private static void Expect(BoundColumn column, CalcInputKind kind, string unit)
    {
        column.ExpectedKind = kind switch
        {
            CalcInputKind.Quantity or CalcInputKind.Vector => RecordColumnKind.Quantity,
            CalcInputKind.Number => RecordColumnKind.Number,
            CalcInputKind.Boolean => RecordColumnKind.Boolean,
            CalcInputKind.Choice => RecordColumnKind.Choice,
            _ => RecordColumnKind.Text,
        };

        column.ExpectedUnitText = RecordUnits.Normalise(unit);

        if (column.ExpectedKind == RecordColumnKind.Quantity && column.ExpectedUnitText.Length > 0 && UnitCatalog.TryParse(column.ExpectedUnitText, out var parsed, out _))
            column.ExpectedUnit = parsed;
    }

    private static void CheckUnit(BoundColumn column, List<RecordImportIssue> issues)
    {
        var (check, message) = EvaluateUnit(column);
        column.UnitIsUsable = check is RecordUnitCheck.Match or RecordUnitCheck.Convertible or RecordUnitCheck.NotApplicable;
        column.UnitCheck = check;
        column.UnitMessage = message;

        if (check is RecordUnitCheck.Missing or RecordUnitCheck.Mismatch or RecordUnitCheck.Unknown)
            issues.Add(new RecordImportIssue(RecordSeverity.Error, 1, column.Symbol, message));
        else if (check == RecordUnitCheck.Convertible)
            issues.Add(new RecordImportIssue(RecordSeverity.Warning, 1, column.Symbol, message));
    }

    private static (RecordUnitCheck Check, string Message) EvaluateUnit(BoundColumn c)
    {
        var symbol = c.Symbol;
        var given = c.FileUnitText;

        if (c.FileSaysText)
            return c.ExpectedKind is RecordColumnKind.Text or RecordColumnKind.Choice
                ? (RecordUnitCheck.NotApplicable, string.Empty)
                : (RecordUnitCheck.Mismatch, $"{symbol}: the header says text, but the calculator takes a number there{ExpectedPhrase(c)}.");

        switch (c.ExpectedKind)
        {
            case RecordColumnKind.Text:
            case RecordColumnKind.Choice:
                return given.Length == 0 ? (RecordUnitCheck.NotApplicable, string.Empty) : (RecordUnitCheck.Mismatch, $"{symbol} is text, but its header gives the unit '{given}'.");

            case RecordColumnKind.Number:
            case RecordColumnKind.Boolean:
                return given.Length == 0 ? (RecordUnitCheck.NotApplicable, string.Empty) : (RecordUnitCheck.Mismatch, $"{symbol} is a plain number (no unit), but its header gives the unit '{given}'.");
        }

        if (given.Length == 0)
            return (RecordUnitCheck.Missing, $"{symbol}: the header gives no unit; write {symbol}[{(c.AnyUnit ? "unit" : c.ExpectedUnitText.Length > 0 ? c.ExpectedUnitText : "unit")}]. A number without its unit is not read as SI.");

        var norm = RecordUnits.Normalise(given);

        if (!UnitCatalog.TryParse(norm, out var fileUnit, out var error))
            return (RecordUnitCheck.Unknown, $"{symbol}: the unit '{given}' is not understood ({error}).");

        c.FileUnit = fileUnit;

        if (c.AnyUnit)
            return (RecordUnitCheck.Match, string.Empty);

        if (c.ExpectedUnit is not { } expected)
            return (RecordUnitCheck.Match, string.Empty);

        if (fileUnit.Dimension != expected.Dimension)
            return (RecordUnitCheck.Mismatch, $"{symbol}: the unit '{given}' does not measure the same quantity as '{c.ExpectedUnitText}', the unit the calculator takes.");

        if (c.DimensionOnly)
            return (RecordUnitCheck.Match, string.Empty);

        if (Math.Abs(fileUnit.Factor - expected.Factor) <= 1e-12 * Math.Abs(expected.Factor) && Math.Abs(fileUnit.Offset - expected.Offset) <= 1e-12 * Math.Max(1, Math.Abs(expected.Offset)))
            return (RecordUnitCheck.Match, string.Empty);

        return (RecordUnitCheck.Convertible, $"{symbol}: the header gives '{given}' where the calculator takes '{c.ExpectedUnitText}'. The values are kept as typed and judged in {c.ExpectedUnitText}.");
    }

    private static string ExpectedPhrase(BoundColumn c) => c.ExpectedUnitText.Length > 0 ? $" in {c.ExpectedUnitText}" : string.Empty;

    private static IReadOnlyList<RecordImportColumn> BuildColumnViews(List<FileColumn> fileColumns)
    {
        var list = new List<RecordImportColumn>();

        foreach (var f in fileColumns)
        {
            if (f.Target is not { } b)
            {
                list.Add(new RecordImportColumn(f.Index, f.Header, f.Symbol, f.Unit, null, RecordColumnRole.Ignored, null, string.Empty, RecordUnitCheck.NotApplicable, "Left out."));
                continue;
            }

            var target = b.Role switch
            {
                RecordColumnRole.TableColumn => $"{b.Table!.Symbol}.{b.Target}",
                RecordColumnRole.Scalar => b.Target,
                RecordColumnRole.MaterialProperty => b.Target,
                RecordColumnRole.Label => "label (joins the title)",
                RecordColumnRole.MaterialName => "material (the title)",
                RecordColumnRole.Extra => "stored, never applied",
                _ => null,
            };
            list.Add(new RecordImportColumn(f.Index, f.Header, f.Symbol, b.FileUnitText, b.Symbol, b.Role, target, b.ExpectedUnitText, b.UnitCheck, b.UnitMessage));
        }

        return list;
    }

    // ------------------------------------------------------------------
    // Rows
    // ------------------------------------------------------------------

    private static List<ParsedRow> ParseRows(CsvTable table, int headerCount, Dictionary<string, BoundColumn> bound, NumberProfile profile)
    {
        var rows = new List<ParsedRow>();

        for (var i = 1; i < table.Rows.Count; i++)
        {
            var cells = table.Rows[i];
            var row = new ParsedRow(table.Lines[i], cells);
            rows.Add(row);

            if (cells.Count != headerCount)
            {
                row.Broken = true;
                row.Add(RecordSeverity.Error, null, $"The row has {cells.Count} field(s) and the header has {headerCount}. A quoted comma, or a missing or extra delimiter, is the usual cause.");
                continue;
            }

            foreach (var column in bound.Values.Where(c => c.Mapped))
            {
                var cell = new ParsedCell(cells[column.FileIndex].Trim());
                row.Cells[column.Symbol] = cell;

                if (cell.IsBlank)
                    continue;

                ParseCell(row, column, cell, profile);
            }
        }

        return rows;
    }

    private static void ParseCell(ParsedRow row, BoundColumn column, ParsedCell cell, NumberProfile profile)
    {
        switch (column.ExpectedKind)
        {
            case RecordColumnKind.Text:
            case RecordColumnKind.Choice:
                if (CsvSafeWriter.IsFormulaLike(cell.Raw))
                    row.Add(RecordSeverity.Error, column.Symbol, $"{column.Symbol}: '{Clip(cell.Raw)}' starts with = + - or @, which a spreadsheet would read as a formula. Remove the sign.");
                else if (cell.Raw.Length > CalcDataRecord.MaxTextLength)
                    row.Add(RecordSeverity.Error, column.Symbol, $"{column.Symbol}: the text is longer than {CalcDataRecord.MaxTextLength} characters.");
                else if (column.ExpectedKind == RecordColumnKind.Choice && column.TableColumn?.Sources.FirstOrDefault().Spec?.Choices is { Count: > 0 } choices && !choices.Contains(cell.Raw, StringComparer.OrdinalIgnoreCase))
                    row.Add(RecordSeverity.Error, column.Symbol, $"{column.Symbol}: '{Clip(cell.Raw)}' is not one of the choices the calculator takes ({string.Join(", ", choices)}).");

                break;

            case RecordColumnKind.Boolean:
                if (cell.Raw is "1" || cell.Raw.Equals("true", StringComparison.OrdinalIgnoreCase) || cell.Raw.Equals("yes", StringComparison.OrdinalIgnoreCase))
                    cell.Flag = true;
                else if (cell.Raw is "0" || cell.Raw.Equals("false", StringComparison.OrdinalIgnoreCase) || cell.Raw.Equals("no", StringComparison.OrdinalIgnoreCase))
                    cell.Flag = false;
                else
                    row.Add(RecordSeverity.Error, column.Symbol, $"{column.Symbol}: '{Clip(cell.Raw)}' is not 1 or 0.");

                if (cell.Flag is { } flag)
                    cell.Number = flag ? 1 : 0;

                break;

            default:
                if (LocaleNumber.TryParse(cell.Raw, profile, out var value, out var problem, out var grouped))
                {
                    cell.Number = value;

                    if (grouped)
                        row.Add(RecordSeverity.Warning, column.Symbol, $"{column.Symbol}: '{Clip(cell.Raw)}' was read as {value.ToString("R", CultureInfo.InvariantCulture)} (the grouping mark was removed). Check that is what it means.");
                }
                else
                {
                    var hint = LocaleNumber.LooksLikeOtherMark(cell.Raw, profile) ? " If the file's numbers use the other decimal mark, choose that number format." : string.Empty;
                    row.Add(RecordSeverity.Error, column.Symbol, $"{column.Symbol}: {problem}{hint}");
                }

                break;
        }
    }

    private static string Clip(string text) => text.Length > 30 ? text[..30] + "…" : text;

    /// <summary>The limits the calculators themselves apply: a value no calculator would take is an error; one that some refuse is a warning; the soft range is a warning.</summary>
    private void SchemaChecks(RecordKindSchema schema, RecordTemplate template, List<ParsedRow> rows, Dictionary<string, BoundColumn> bound)
    {
        foreach (var column in bound.Values.Where(c => c.Mapped && c.IsNumeric && c.Role is RecordColumnRole.Scalar or RecordColumnRole.TableColumn))
        {
            var limits = column.Role == RecordColumnRole.Scalar
                ? column.ScalarSymbol!.Sources.Select(s => (s.TemplateId, s.Spec.HardMin, s.Spec.HardMax, s.Spec.Min, s.Spec.Max, s.Spec.DefaultUnit)).ToList()
                : column.TableColumn!.Sources.Select(s => (s.TemplateId, s.Spec.HardMin, s.Spec.HardMax, s.Spec.Min, s.Spec.Max, s.Spec.DefaultUnit)).ToList();
            var templateWarns = template.Checks.Any(c => c.Kind == RecordCheckKind.Range && c.Columns.Contains(column.Symbol, StringComparer.Ordinal));

            foreach (var row in rows.Where(r => !r.Broken))
            {
                if (row.Cell(column.Symbol)?.Number is not { } v || !column.UnitIsUsable)
                    continue;

                var hardBy = new List<string>();
                var softBy = 0;
                string? describe = null;

                foreach (var (templateId, hardMin, hardMax, min, max, unitText) in limits)
                {
                    var unit = RecordUnits.Normalise(unitText) is { Length: > 0 } u && UnitCatalog.TryParse(u, out var parsed, out _) ? parsed : null;

                    if (column.In(v, unit) is not { } x)
                        continue;

                    var shown = RecordTemplateChecks.Show(x) + (unitText.Length > 0 ? " " + RecordUnits.Normalise(unitText) : string.Empty);

                    if ((hardMin is { } lo && x < lo) || (hardMax is { } hi && x > hi))
                    {
                        hardBy.Add(UserTitle(schema, templateId));
                        describe ??= $"{shown} against {Range(hardMin, hardMax)}{(unitText.Length > 0 ? " " + RecordUnits.Normalise(unitText) : string.Empty)}";
                    }
                    else if ((min is { } smin && x < smin) || (max is { } smax && x > smax))
                    {
                        softBy++;
                    }
                }

                if (hardBy.Count == limits.Count && limits.Count > 0)
                    row.Add(RecordSeverity.Error, column.Symbol, $"{column.Symbol}: the value is outside what {string.Join(", ", hardBy.Distinct(StringComparer.Ordinal))} accept{(hardBy.Distinct(StringComparer.Ordinal).Count() == 1 ? "s" : string.Empty)} ({describe}). A slip in the unit or a column is likely.");
                else if (hardBy.Count > 0)
                    row.Add(RecordSeverity.Warning, column.Symbol, $"{column.Symbol}: {string.Join(", ", hardBy.Distinct(StringComparer.Ordinal))} would refuse this value ({describe}); other calculators take it.");
                else if (softBy == limits.Count && limits.Count > 0 && !templateWarns && limits.Any(l => l.Min is not null || l.Max is not null))
                    row.Add(RecordSeverity.Warning, column.Symbol, $"{column.Symbol}: the value is outside the usual range of the calculator that reads it. Check it against your source.");
            }
        }
    }

    private static string UserTitle(RecordKindSchema schema, string templateId) => schema.Users.FirstOrDefault(u => string.Equals(u.TemplateId, templateId, StringComparison.Ordinal))?.TemplateTitle ?? templateId;

    private static string Range(double? lo, double? hi) => (lo, hi) switch
    {
        ({ } a, { } b) => $"{RecordTemplateChecks.Show(a)} to {RecordTemplateChecks.Show(b)}",
        ({ } a, null) => $"at least {RecordTemplateChecks.Show(a)}",
        (null, { } b) => $"at most {RecordTemplateChecks.Show(b)}",
        _ => "any",
    };

    // ------------------------------------------------------------------
    // Planning the records
    // ------------------------------------------------------------------

    private sealed record PlannedRecord(CalcDataRecord Record, int Line);

    private async Task<List<PlannedRecord>> PlanAsync(
        RecordImportRequest request,
        RecordTemplate template,
        RecordKindSchema schema,
        Dictionary<string, BoundColumn> bound,
        List<ParsedRow> rows,
        ImportedFile file,
        string? sha,
        List<RecordImportIssue> fileIssues,
        CancellationToken cancellationToken)
    {
        var prov = request.Provenance;
        var now = _service.Time.GetUtcNow();
        var person = _service.SignedInPerson;
        var licence = prov.LicensedSource ? CalcLicenceClass.LicensedSource : CalcLicenceClass.Public;
        var source = new CalcValueSource(prov.SourceTitle, prov.EditionAndAmendment, prov.TableOrClause, licence, person, now, sha);
        var valid = rows.Where(r => !r.Broken).ToList();
        var note = (request.FileName is null ? "Typed on the records screen" : $"Imported from '{file.FileName}' (SHA-256 {file.Sha256})") + $" with record template {template.FileName}, {valid.Count} data row(s).";
        var notes = string.IsNullOrWhiteSpace(prov.Notes) ? note : prov.Notes.Trim() + NoteSeparator + note;

        var drafts = new List<(CalcDataRecord Draft, int Line, ParsedRow? Row)>();

        if (schema.IsMaterial)
            await PlanMaterialsAsync(template, bound, valid, source, notes, prov, drafts, cancellationToken).ConfigureAwait(false);
        else if (bound.Values.Any(c => c.Role == RecordColumnRole.TableColumn))
            PlanTable(template, bound, valid, source, notes, prov, drafts, fileIssues);
        else
            PlanRows(template, bound, valid, source, notes, prov, drafts);

        // Titles: a title already in the store is refused unless the import is its next revision; no title may repeat within the import.
        var existing = await _service.Store.ListRecordsAsync(template.Kind, cancellationToken).ConfigureAwait(false);
        var titles = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var prepared = new List<PlannedRecord>();

        foreach (var (draft, line, row) in drafts)
        {
            void Report(string message)
            {
                if (row is not null)
                    row.Add(RecordSeverity.Error, null, message);
                else
                    fileIssues.Add(new RecordImportIssue(RecordSeverity.Error, line > 0 ? line : null, null, message));
            }

            var title = Sheets.CalcSheetTextRenderer.Clean(draft.Title);

            if (titles.TryGetValue(title, out var firstLine))
            {
                Report($"This record's title '{title}' repeats the one from line {firstLine}; each record needs its own title.");
                continue;
            }

            titles[title] = line;
            var current = draft;

            if (draft.Id == Guid.Empty)
            {
                var same = existing.Where(r => string.Equals(r.Title, title, StringComparison.OrdinalIgnoreCase)).ToList();

                if (same.Count > 1)
                {
                    Report($"{same.Count} records of this kind are already titled '{title}'; change the title so the import is unambiguous.");
                    continue;
                }

                if (same.Count == 1)
                {
                    if (!request.AsRevision)
                    {
                        Report($"A record titled '{title}' of this kind exists already (revision {same[0].Revision}). Change the title, or save this import as its next revision (calculations that use it then go stale until the record is applied again).");
                        continue;
                    }

                    current = draft with { Id = same[0].Id };
                }
            }

            var (record, refusal) = await _service.PrepareRecordAsync(current, false, cancellationToken).ConfigureAwait(false);

            if (record is null)
            {
                Report(refusal ?? "The record cannot be saved.");
                continue;
            }

            prepared.Add(new PlannedRecord(record, line));
        }

        return prepared;
    }

    private static CalcDataRecord NewDraft(RecordTemplate template, RecordProvenanceInput prov, string title, IReadOnlyList<CalcDataValue> values, string notes, string person, DateTimeOffset now) =>
        new(Guid.Empty, template.Kind, title, 0, prov.SourceTitle.Trim(), prov.EditionAndAmendment, prov.TableOrClause.Trim(), prov.LicenceHolder.Trim(), person, prov.LicensedSource, prov.Origin, values, now, notes);

    private void PlanRows(RecordTemplate template,Dictionary<string, BoundColumn> bound, List<ParsedRow> valid, CalcValueSource source, string notes, RecordProvenanceInput prov, List<(CalcDataRecord, int, ParsedRow?)> drafts)
    {
        var now = _service.Time.GetUtcNow();
        var person = _service.SignedInPerson;
        var n = 0;

        foreach (var row in valid)
        {
            n++;
            var values = new List<CalcDataValue>();
            var labels = new List<string>();
            var scalars = 0;

            foreach (var column in bound.Values.Where(c => c.Mapped))
            {
                if (row.Cell(column.Symbol) is not { IsBlank: false } cell)
                    continue;

                switch (column.Role)
                {
                    case RecordColumnRole.Scalar:
                        values.Add(ScalarValue(column, cell, source));
                        scalars++;
                        break;

                    case RecordColumnRole.Label:
                        values.Add(new CalcDataValue(column.Symbol, Text: cell.Raw, Source: source));
                        labels.Add(cell.Raw);
                        break;

                    case RecordColumnRole.Extra when cell.Number is { } x:
                        values.Add(new CalcDataValue(column.Symbol, Number: x, Unit: UnitOrNull(column), Source: source));
                        labels.Add($"{column.Symbol} {RecordTemplateChecks.Show(x)}{(UnitOrNull(column) is { } u ? " " + u : string.Empty)}");
                        break;
                }
            }

            if (scalars == 0)
            {
                row.Add(RecordSeverity.Error, null, "No value of this record kind is filled in on this row, so there is nothing to save.");
                continue;
            }

            var title = labels.Count > 0 ? $"{prov.Title.Trim()} [{string.Join(", ", labels)}]" : valid.Count > 1 ? $"{prov.Title.Trim()} [row {n}]" : prov.Title.Trim();
            drafts.Add((NewDraft(template, prov, Truncate(title), values, notes, person, now), row.Line, row));
        }
    }

    private void PlanTable(RecordTemplate template,Dictionary<string, BoundColumn> bound, List<ParsedRow> valid, CalcValueSource source, string notes, RecordProvenanceInput prov, List<(CalcDataRecord, int, ParsedRow?)> drafts, List<RecordImportIssue> fileIssues)
    {
        var now = _service.Time.GetUtcNow();
        var person = _service.SignedInPerson;
        var tableColumns = bound.Values.Where(c => c.Mapped && c.Role == RecordColumnRole.TableColumn).ToList();
        var table = tableColumns[0].Table!;
        var values = new List<CalcDataValue>();

        // Scalars of a mixed template are read from the first data row; a different value later is ignored, and said so.
        foreach (var column in bound.Values.Where(c => c.Mapped && c.Role == RecordColumnRole.Scalar))
        {
            if (valid.Count > 0 && valid[0].Cell(column.Symbol) is { IsBlank: false } first)
                values.Add(ScalarValue(column, first, source));

            foreach (var later in valid.Skip(1))
            {
                if (later.Cell(column.Symbol) is { IsBlank: false } c && valid[0].Cell(column.Symbol)?.Raw != c.Raw)
                    later.Add(RecordSeverity.Warning, column.Symbol, $"{column.Symbol} is read from the first data row only; this value is ignored.");
            }
        }

        var tableRows = new List<CalcRow>();

        foreach (var row in valid)
        {
            var cells = new Dictionary<string, CalcCell>(StringComparer.Ordinal);

            foreach (var column in tableColumns)
            {
                if (row.Cell(column.Symbol) is not { IsBlank: false } cell)
                    continue;

                cells[column.Target!] = column.ExpectedKind switch
                {
                    RecordColumnKind.Quantity => new CalcCell(Number: cell.Number, Unit: UnitOrNull(column)),
                    RecordColumnKind.Number => new CalcCell(Number: cell.Number),
                    RecordColumnKind.Boolean => new CalcCell(Flag: cell.Flag),
                    _ => new CalcCell(Text: CanonicalChoice(column, cell.Raw)),
                };
            }

            if (cells.Count > 0)
                tableRows.Add(new CalcRow(cells));
        }

        if (tableRows.Count == 0)
        {
            fileIssues.Add(new RecordImportIssue(RecordSeverity.Error, null, null, $"The file holds no row for the table '{table.Symbol}'."));
            return;
        }

        var maxRows = Math.Min(table.MaxRows, CalcDataRecord.MaxRows);

        if (tableRows.Count > maxRows)
        {
            fileIssues.Add(new RecordImportIssue(RecordSeverity.Error, null, null, $"The table '{table.Symbol}' takes at most {maxRows} rows; the file has {tableRows.Count}."));
            return;
        }

        values.Add(new CalcDataValue(table.Symbol, Rows: tableRows, Source: source));
        drafts.Add((NewDraft(template, prov, prov.Title.Trim(), values, notes, person, now), valid.Count > 0 ? valid[0].Line : 0, null));
    }

    private static string CanonicalChoice(BoundColumn column, string raw) =>
        column.TableColumn?.Sources.FirstOrDefault().Spec?.Choices?.FirstOrDefault(c => string.Equals(c, raw, StringComparison.OrdinalIgnoreCase)) ?? raw;

    private static string? UnitOrNull(BoundColumn column) => column.FileUnitText.Length == 0 ? null : RecordUnits.Normalise(column.FileUnitText);

    private static CalcDataValue ScalarValue(BoundColumn column, ParsedCell cell, CalcValueSource source) => column.ExpectedKind switch
    {
        RecordColumnKind.Quantity => new CalcDataValue(column.Target!, Number: cell.Number, Unit: UnitOrNull(column), Source: source),
        RecordColumnKind.Number => new CalcDataValue(column.Target!, Number: cell.Number, Source: source),
        RecordColumnKind.Boolean => new CalcDataValue(column.Target!, Number: cell.Number, Source: source),
        _ => new CalcDataValue(column.Target!, Text: cell.Raw, Source: source),
    };

    private static string Truncate(string title) => title.Length <= CalcDataRecord.MaxTextLength ? title : title[..(CalcDataRecord.MaxTextLength - 1)] + "…";

    // ------------------------------------------------------------------
    // Materials (templates 62 to 65)
    // ------------------------------------------------------------------

    private async Task PlanMaterialsAsync(
        RecordTemplate template,
        Dictionary<string, BoundColumn> bound,
        List<ParsedRow> valid,
        CalcValueSource source,
        string notes,
        RecordProvenanceInput prov,
        List<(CalcDataRecord, int, ParsedRow?)> drafts,
        CancellationToken cancellationToken)
    {
        var now = _service.Time.GetUtcNow();
        var person = _service.SignedInPerson;
        await _service.Materials.EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        var library = await _service.Store.ListRecordsAsync(MaterialLibrary.Kind, cancellationToken).ConfigureAwait(false);

        if (template.Id == "62")
        {
            foreach (var row in valid)
            {
                var name = row.Cell("material")?.Raw ?? string.Empty;
                var values = new List<CalcDataValue>();

                foreach (var column in bound.Values.Where(c => c.Mapped && c.Role == RecordColumnRole.MaterialProperty))
                {
                    if (row.Cell(column.Symbol) is { IsBlank: false, Number: { } number })
                        values.Add(new CalcDataValue(column.Target!, Number: number, Unit: UnitOrNull(column), Source: source));
                }

                if (values.Count == 0)
                {
                    row.Add(RecordSeverity.Error, null, "No property is filled in on this row, so there is no material to save.");
                    continue;
                }

                drafts.Add((new CalcDataRecord(Guid.Empty, MaterialLibrary.Kind, name, 0, prov.SourceTitle.Trim(), prov.EditionAndAmendment, prov.TableOrClause.Trim(), prov.LicenceHolder.Trim(), person, prov.LicensedSource, prov.Origin, values, now, notes), row.Line, row));
            }

            return;
        }

        // Templates 63 to 65 add a table to a material already in the library: one new revision per material.
        var additions = new Dictionary<string, (CalcDataRecord Material, List<CalcDataValue> Values, int Line, ParsedRow Row)>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in valid.GroupBy(r => r.Cell("material")?.Raw ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            var first = group.First();
            var existing = library.Where(m => string.Equals(m.Title, group.Key, StringComparison.OrdinalIgnoreCase)).ToList();

            if (existing.Count != 1)
            {
                first.Add(RecordSeverity.Error, "material", existing.Count == 0
                    ? $"No material called '{group.Key}' is in the library. Import it with template 62 (or save it on the screen) first."
                    : $"{existing.Count} materials are called '{group.Key}'; the name must pick one.");
                continue;
            }

            var newValues = template.Id switch
            {
                "63" => TemperatureValues(group.ToList(), bound, source),
                "64" => CurveValue(group.ToList(), bound, source),
                _ => PairsValue(group.ToList(), source),
            };

            if (newValues is null)
                continue;

            additions[group.Key] = (existing[0], newValues, first.Line, first);
        }

        foreach (var (material, values, line, row) in additions.Values)
        {
            var kept = material.Values.Where(v => !values.Any(n => string.Equals(n.Symbol, v.Symbol, StringComparison.Ordinal))).ToList();
            var combined = material.Notes is { Length: > 0 } ? material.Notes + NoteSeparator + notes : notes;
            drafts.Add((material with { Values = [.. kept, .. values], LicensedSource = material.LicensedSource || prov.LicensedSource, Notes = combined }, line, row));
        }
    }

    private static List<CalcDataValue>? TemperatureValues(List<ParsedRow> group, Dictionary<string, BoundColumn> bound, CalcValueSource source)
    {
        var values = new List<CalcDataValue>();
        var t = bound["T"];
        var failed = false;

        foreach (var byProperty in group.GroupBy(r => r.Cell("property")?.Raw ?? string.Empty, StringComparer.Ordinal))
        {
            var first = byProperty.First();
            var def = MaterialProperties.Find(byProperty.Key + MaterialProperties.TemperatureSuffix, out var isTable);

            if (def is null || !isTable)
            {
                first.Add(RecordSeverity.Error, "property", $"'{byProperty.Key}' has no temperature table; the properties that do are {string.Join(", ", MaterialProperties.All.Where(p => p.TemperatureDependent).Select(p => p.Key))}.");
                failed = true;
                continue;
            }

            var units = byProperty.Select(r => r.Cell("v_unit")?.Raw ?? string.Empty).Distinct(StringComparer.Ordinal).ToList();

            if (units.Count > 1)
            {
                first.Add(RecordSeverity.Error, "v_unit", $"The rows of {def.TemperatureKey} give more than one unit ({string.Join(", ", units.Select(u => u.Length == 0 ? "none" : u))}); use one unit for the whole table.");
                failed = true;
                continue;
            }

            var (unitText, unitProblem) = ValueUnit(def, units[0]);

            if (unitProblem is not null)
            {
                first.Add(RecordSeverity.Error, "v_unit", unitProblem);
                failed = true;
                continue;
            }

            if (byProperty.Count() < 2)
            {
                first.Add(RecordSeverity.Error, "T", $"{def.TemperatureKey} needs at least two rows for '{first.Cell("material")?.Raw}'.");
                failed = true;
                continue;
            }

            var rows = byProperty.Select(r => new CalcRow(new Dictionary<string, CalcCell>(StringComparer.Ordinal)
            {
                [MaterialProperties.TemperatureColumn] = new CalcCell(Number: r.Cell("T")?.Number, Unit: UnitOrNull(t)),
                [MaterialProperties.ValueColumn] = new CalcCell(Number: r.Cell("v")?.Number, Unit: unitText),
            })).ToList();
            values.Add(new CalcDataValue(def.TemperatureKey, Rows: rows, Source: source));
        }

        return failed ? null : values;
    }

    private static (string? Unit, string? Problem) ValueUnit(MaterialPropertyDef def, string unit)
    {
        if (def.SiUnit is { Length: 0 })
            return unit.Length == 0 ? (null, null) : (null, $"{def.Key} is a plain number; leave v_unit blank, not '{unit}'.");

        if (unit.Length == 0)
            return (null, $"v_unit is blank; {def.Key} ({def.Title}) needs its unit ({def.SiUnit}).");

        var norm = RecordUnits.Normalise(unit);

        if (!UnitCatalog.TryParse(norm, out var parsed, out var error))
            return (null, $"v_unit '{unit}' is not understood ({error}).");

        return def.Dimension is { } dimension && parsed.Dimension != dimension
            ? (null, $"v_unit '{unit}' does not measure {def.Title}; use a unit of {def.SiUnit}.")
            : (norm, null);
    }

    private static List<CalcDataValue>? CurveValue(List<ParsedRow> group, Dictionary<string, BoundColumn> bound, CalcValueSource source)
    {
        if (group.Count < 2)
        {
            group[0].Add(RecordSeverity.Error, "eps", $"{MaterialProperties.SecantCurve} needs at least two points for '{group[0].Cell("material")?.Raw}'.");
            return null;
        }

        var es = bound["Es"];
        var rows = group.Select(r => new CalcRow(new Dictionary<string, CalcCell>(StringComparer.Ordinal)
        {
            ["eps"] = new CalcCell(Number: r.Cell("eps")?.Number),
            ["Es"] = new CalcCell(Number: r.Cell("Es")?.Number, Unit: UnitOrNull(es)),
        })).ToList();

        return [new CalcDataValue(MaterialProperties.SecantCurve, Rows: rows, Source: source)];
    }

    private static List<CalcDataValue> PairsValue(List<ParsedRow> group, CalcValueSource source)
    {
        var rows = group.Select(r => new CalcRow(new Dictionary<string, CalcCell>(StringComparer.Ordinal)
        {
            ["pair"] = new CalcCell(Text: r.Cell("pair")?.Raw),
            ["mu"] = new CalcCell(Number: r.Cell("mu")?.Number),
        })).ToList();

        return [new CalcDataValue(MaterialProperties.FrictionPairs, Rows: rows, Source: source)];
    }

    // ------------------------------------------------------------------
    // What a calculator is missing, and the records screen's list
    // ------------------------------------------------------------------

    /// <summary>
    /// The record kinds one calculator draws on, each with whether a record that holds a value it takes exists, how much the calculator needs it
    /// (<see cref="RecordRequirements"/>) and the Track P templates that fill it. The material library counts as present: it ships generic seeds.
    /// </summary>
    public async Task<IReadOnlyList<RecordRequirement>> RequirementsAsync(string templateReference, CancellationToken cancellationToken = default)
    {
        var entry = _service.Catalogue.Find(templateReference);

        if (entry is null)
            return [];

        var result = new List<RecordRequirement>();

        foreach (var kind in entry.Template.RecordKinds ?? [])
        {
            if (MaterialLibrary.IsMaterial(kind.Kind))
                await _service.Materials.EnsureSeededAsync(cancellationToken).ConfigureAwait(false);

            var records = await _service.Store.ListRecordsAsync(kind.Kind, cancellationToken).ConfigureAwait(false);
            var present = MaterialLibrary.IsMaterial(kind.Kind)
                ? records.Count > 0
                : records.Any(r => r.Values.Any(v => kind.Symbols.Contains(v.Symbol, StringComparer.Ordinal)));
            var (need, reason) = RecordRequirements.For(entry.Template.Id, kind.Kind);
            result.Add(new RecordRequirement(kind.Kind, kind.Title, kind.Symbols, need, reason, present, records.Count, RecordTemplates.ForKind(kind.Kind)));
        }

        return result;
    }

    /// <summary>The required records of a calculator that no record satisfies.</summary>
    public async Task<IReadOnlyList<RecordRequirement>> MissingRequiredAsync(string templateReference, CancellationToken cancellationToken = default) =>
        [.. (await RequirementsAsync(templateReference, cancellationToken).ConfigureAwait(false)).Where(r => r.Need == RecordNeed.Required && r.IsMissing)];

    /// <summary>Every record kind a visible calculator draws on, with its count, its licensed count, the calculators and the templates; for the records screen.</summary>
    public async Task<IReadOnlyList<RecordTypeInfo>> ListTypesAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<RecordTypeInfo>();
        var entries = _service.Catalogue.Current();

        foreach (var schema in Schemas().Values.OrderBy(s => s.Title, StringComparer.Ordinal).ThenBy(s => s.Kind, StringComparer.Ordinal))
        {
            if (schema.IsMaterial)
                await _service.Materials.EnsureSeededAsync(cancellationToken).ConfigureAwait(false);

            var records = await _service.Store.ListRecordsAsync(schema.Kind, cancellationToken).ConfigureAwait(false);
            var licensed = 0;

            foreach (var record in records)
            {
                if (await _service.IsLicensedRecordAsync(record, cancellationToken).ConfigureAwait(false))
                    licensed++;
            }

            var requiredBy = entries
                .Where(e => (e.Template.RecordKinds ?? []).Any(k => k.Kind == schema.Kind) && RecordRequirements.For(e.Template.Id, schema.Kind).Need == RecordNeed.Required)
                .Select(e => e.Template.Title)
                .ToList();

            list.Add(new RecordTypeInfo(schema.Kind, schema.IsMaterial ? "Material properties" : schema.Title, [.. schema.Users.Select(u => u.TemplateTitle).Distinct(StringComparer.Ordinal)], RecordTemplates.ForKind(schema.Kind), records.Count, licensed, requiredBy));
        }

        return list;
    }
}
