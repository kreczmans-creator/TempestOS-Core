using System.Globalization;
using Tempest.Calc.Import;
using Tempest.Calc.Library;
using Tempest.Core.Logging;

namespace Tempest.Desktop.CalcSheets;

/// <summary>One record of the shown type, as the screen lists it. A licensed record's values are never listed: only which symbols it holds and how many rows.</summary>
public sealed record CalcRecordRow(Guid Id, int Revision, string Title, string Edition, string TableOrClause, bool Licensed, string Origin, string EnteredBy, string Entered, string Holds);

/// <summary>One column of the typed grid: the template's symbol, the unit the person may set, and what the unit check says about it.</summary>
public sealed class CalcRecordGridColumn
{
    public CalcRecordGridColumn(RecordTemplateColumn template) => Template = template;

    public RecordTemplateColumn Template { get; }

    public string Symbol => Template.Symbol;

    /// <summary>The unit as typed in the header; text columns have none.</summary>
    public string Unit { get; set; } = string.Empty;

    public bool IsText => Template.IsText;

    /// <summary>The calculator's unit for the column, once a preview has run.</summary>
    public string ExpectedUnit { get; set; } = string.Empty;

    public RecordUnitCheck Check { get; set; } = RecordUnitCheck.NotApplicable;

    public string Message { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;
}

/// <summary>
/// Addendum C, C-3: the state behind the records screen, with no UI in it. A record type is picked; a Track P record template for it; the
/// provenance (source, edition and amendment, table or clause, licence holder: all required) is typed or read from a <c>_provenance.csv</c>;
/// then the values come either from a grid typed on the screen or from a CSV file. Both go through the same importer
/// (<see cref="RecordImportService"/>): unit-aware columns, column mapping, row-level errors and warnings, the planned records, and an
/// all-or-nothing commit. The screen never writes a record's values anywhere: there is no export from here (§A5).
/// </summary>
public sealed class CalcRecordsViewModel
{
    /// <summary>The most records listed for a type; the rest are counted.</summary>
    public const int MaxListed = 200;

    private readonly CalcInstanceService _service;
    private readonly Func<string?> _person;
    private readonly ILogger? _logger;
    private readonly List<List<string>> _gridRows = [];
    private readonly List<CalcRecordGridColumn> _gridColumns = [];
    private readonly Dictionary<string, string?> _columnMap = new(StringComparer.Ordinal);
    private IReadOnlyList<int> _gridRowOfLine = [];
    private int _gen;

    public CalcRecordsViewModel(CalcInstanceService service, Func<string?> person, ILogger? logger = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _person = person ?? throw new ArgumentNullException(nameof(person));
        _logger = logger;
    }

    /// <summary>Raised when what the screen shows has changed in structure (the types, the records, the grid, the file).</summary>
    public event Action? Changed;

    /// <summary>Raised when only a preview, a status line or a unit check changed (so a typed cell keeps its focus).</summary>
    public event Action? PreviewChanged;

    /// <summary>Raised after records were saved, with them (the worksheet re-reads what it is missing).</summary>
    public event Action<IReadOnlyList<CalcDataRecord>>? Committed;

    // ------------------------------------------------------------------ the record types

    public IReadOnlyList<RecordTypeInfo> Types { get; private set; } = [];

    public string? SelectedKind { get; private set; }

    public RecordTypeInfo? SelectedType => Types.FirstOrDefault(t => string.Equals(t.Kind, SelectedKind, StringComparison.Ordinal));

    public IReadOnlyList<CalcRecordRow> Records { get; private set; } = [];

    /// <summary>Records of the type beyond <see cref="MaxListed"/>.</summary>
    public int RecordsNotListed { get; private set; }

    /// <summary>The kind's schema: what the calculators take from it.</summary>
    public RecordKindSchema? Schema { get; private set; }

    /// <summary>The templates that import into the shown kind.</summary>
    public IReadOnlyList<RecordTemplate> Templates { get; private set; } = [];

    public RecordTemplate? Template { get; private set; }

    /// <summary>The signed-in person: who the records are stamped as entered by.</summary>
    public string EnteredBy => string.IsNullOrWhiteSpace(_person()) ? "(nobody signed in)" : _person()!;

    public string Status { get; private set; } = string.Empty;

    public bool IsBusy { get; private set; }

    // ------------------------------------------------------------------ provenance

    public string Title { get; set; } = string.Empty;

    public string SourceTitle { get; set; } = string.Empty;

    public string Edition { get; set; } = string.Empty;

    public string Amendment { get; set; } = string.Empty;

    public string TableOrClause { get; set; } = string.Empty;

    public string LicenceHolder { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    /// <summary>The source is licensed: the values never leave through an export. On by default.</summary>
    public bool Licensed { get; set; } = true;

    public CalcDataOrigin Origin { get; set; } = CalcDataOrigin.UserEntered;

    /// <summary>When a record of this title exists, save the import as its next revision.</summary>
    public bool AsRevision { get; set; }

    /// <summary>The file's numbers use a decimal comma.</summary>
    public bool DecimalComma { get; private set; }

    public RecordProvenanceInput Provenance => new(Title, SourceTitle, Edition, Amendment, TableOrClause, LicenceHolder, Licensed, Origin, string.IsNullOrWhiteSpace(Notes) ? null : Notes);

    /// <summary>The field the provenance is still missing, in words; <see langword="null"/> when it is complete.</summary>
    public string? ProvenanceProblem => Provenance.Problem(titleOptional: Schema?.IsMaterial == true);

    public NumberProfile NumberProfile => DecimalComma ? NumberProfile.Comma : NumberProfile.Point;

    // ------------------------------------------------------------------ the typed grid

    public IReadOnlyList<CalcRecordGridColumn> GridColumns => _gridColumns;

    public IReadOnlyList<IReadOnlyList<string>> GridRows => _gridRows;

    public RecordImportPreview? GridPreview { get; private set; }

    /// <summary>The findings of the typed grid, one line each, with the typed row they belong to.</summary>
    public IReadOnlyList<string> GridIssueLines { get; private set; } = [];

    public bool CanSaveGrid => !IsBusy && GridPreview is { CanCommit: true } && ProvenanceProblem is null;

    // ------------------------------------------------------------------ the file

    public string? FileName { get; private set; }

    public string? FileHash { get; private set; }

    private byte[]? _fileBytes;

    public IReadOnlyDictionary<string, string?> ColumnMap => _columnMap;

    public RecordImportPreview? FilePreview { get; private set; }

    public IReadOnlyList<string> FileIssueLines { get; private set; } = [];

    public bool CanImportFile => !IsBusy && FilePreview is { CanCommit: true } && ProvenanceProblem is null;

    // ------------------------------------------------------------------ actions

    /// <summary>Re-reads the types and the records of the shown one; <paramref name="selectKind"/> shows that type.</summary>
    public async Task RefreshAsync(string? selectKind = null, CancellationToken cancellationToken = default)
    {
        Types = await _service.RecordImport.ListTypesAsync(cancellationToken).ConfigureAwait(true);

        if (selectKind is not null && Types.Any(t => t.Kind == selectKind))
        {
            // Another kind than the one shown: what was chosen or typed for the first (a file, a grid) is not carried over to it.
            if (!string.Equals(selectKind, SelectedKind, StringComparison.Ordinal))
                ResetEntry();

            SelectedKind = selectKind;
        }
        else if (SelectedKind is null || Types.All(t => t.Kind != SelectedKind))
            SelectedKind = Types.FirstOrDefault()?.Kind;

        await LoadKindAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Shows a record type: its records, its templates, and a blank grid for the first template.</summary>
    public async Task SelectKindAsync(string kind, CancellationToken cancellationToken = default)
    {
        if (Types.All(t => t.Kind != kind))
            return;

        SelectedKind = kind;
        ResetEntry();
        await LoadKindAsync(cancellationToken).ConfigureAwait(true);
    }

    private async Task LoadKindAsync(CancellationToken cancellationToken)
    {
        Schema = SelectedKind is null ? null : _service.RecordImport.SchemaFor(SelectedKind);
        var importable = _service.RecordImport.ImportableTemplates();
        Templates = SelectedKind is null ? [] : [.. importable.Where(t => t.Kind == SelectedKind)];

        var rows = new List<CalcRecordRow>();
        RecordsNotListed = 0;

        if (SelectedKind is { } kind)
        {
            if (MaterialLibrary.IsMaterial(kind))
                await _service.Materials.EnsureSeededAsync(cancellationToken).ConfigureAwait(true);

            var records = await _service.Store.ListRecordsAsync(kind, cancellationToken).ConfigureAwait(true);
            RecordsNotListed = Math.Max(0, records.Count - MaxListed);

            foreach (var record in records.Take(MaxListed))
            {
                var licensed = record.IsLicensed() || await _service.Store.IsLicensedInAnyRevisionAsync(record.Id, cancellationToken).ConfigureAwait(true);
                rows.Add(new CalcRecordRow(
                    record.Id, record.Revision, record.Title, record.Edition, record.TableOrClause, licensed, record.Origin.ToString(), record.EnteredBy,
                    record.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    string.Join(", ", record.Values.Select(v => v.Rows is { } r ? $"{v.Symbol} ({r.Count} rows)" : v.Symbol))));
            }
        }

        Records = rows;

        if (Template is null || Templates.All(t => t.Id != Template.Id))
            SetTemplateCore(Templates.FirstOrDefault());

        Changed?.Invoke();
        await PreviewGridAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Picks the record template: the grid takes its columns and units, and a file is read again against it.</summary>
    public async Task SelectTemplateAsync(string id, CancellationToken cancellationToken = default)
    {
        if (Templates.FirstOrDefault(t => t.Id == id) is not { } template || Template?.Id == id)
            return;

        SetTemplateCore(template);
        _columnMap.Clear();
        Changed?.Invoke();
        await PreviewGridAsync(cancellationToken).ConfigureAwait(true);

        if (_fileBytes is not null)
            await PreviewFileAsync(cancellationToken).ConfigureAwait(true);
    }

    private void SetTemplateCore(RecordTemplate? template)
    {
        Template = template;
        _gridColumns.Clear();
        _gridRows.Clear();
        GridPreview = null;
        GridIssueLines = [];

        if (template is null)
            return;

        foreach (var column in template.Columns)
            _gridColumns.Add(new CalcRecordGridColumn(column) { Unit = column.IsText ? string.Empty : column.Unit });

        AddRowCore();

        if (string.IsNullOrWhiteSpace(Title) && Schema?.IsMaterial != true)
            Title = template.Title;
    }

    private void ResetEntry()
    {
        Template = null;
        _fileBytes = null;
        FileName = null;
        FileHash = null;
        FilePreview = null;
        FileIssueLines = [];
        _columnMap.Clear();
        _gridRows.Clear();
        _gridColumns.Clear();
        GridPreview = null;
        GridIssueLines = [];
        Title = string.Empty;
        Status = string.Empty;
    }

    // ------------------------------------------------------------------ the typed grid

    public void AddRow()
    {
        AddRowCore();
        Changed?.Invoke();
    }

    private void AddRowCore() => _gridRows.Add([.. Enumerable.Repeat(string.Empty, _gridColumns.Count)]);

    public void RemoveLastRow()
    {
        if (_gridRows.Count > 1)
            _gridRows.RemoveAt(_gridRows.Count - 1);

        Changed?.Invoke();
    }

    public void ClearGrid()
    {
        _gridRows.Clear();
        AddRowCore();
        GridPreview = null;
        GridIssueLines = [];
        Changed?.Invoke();
    }

    /// <summary>Sets a typed cell. It does not redraw (the cell being typed in keeps its focus); call <see cref="PreviewGridAsync"/>.</summary>
    public void SetCell(int row, int column, string text)
    {
        if (row < 0 || row >= _gridRows.Count || column < 0 || column >= _gridColumns.Count)
            return;

        _gridRows[row][column] = text ?? string.Empty;
    }

    public void SetUnit(int column, string text)
    {
        if (column >= 0 && column < _gridColumns.Count && !_gridColumns[column].IsText)
            _gridColumns[column].Unit = text ?? string.Empty;
    }

    /// <summary>Checks the typed grid with the same importer as a file: units, rows, ranges, the template's own rules.</summary>
    public async Task PreviewGridAsync(CancellationToken cancellationToken = default)
    {
        var generation = ++_gen;

        if (Template is not { } template)
        {
            GridPreview = null;
            GridIssueLines = [];
            PreviewChanged?.Invoke();
            return;
        }

        var csv = RecordImportService.GridToCsv(template, [.. _gridColumns.Select(c => c.Unit)], [.. _gridRows.Select(r => (IReadOnlyList<string>)r)]);
        _gridRowOfLine = csv.GridRowOfLine;

        if (csv.Problems.Count > 0)
        {
            GridPreview = null;
            GridIssueLines = [.. csv.Problems];
            PreviewChanged?.Invoke();
            return;
        }

        if (csv.GridRowOfLine.Count == 0)
        {
            GridPreview = null;
            GridIssueLines = ["Type at least one row. Nothing is saved from an empty grid."];

            foreach (var column in _gridColumns)
            {
                column.Check = RecordUnitCheck.NotApplicable;
                column.Message = string.Empty;
            }

            PreviewChanged?.Invoke();
            return;
        }

        var preview = await _service.RecordImport.PreviewAsync(Request(csv.Bytes, null, null), cancellationToken).ConfigureAwait(true);

        if (generation != _gen)
            return;

        GridPreview = preview;
        GridIssueLines = Lines(preview, line => line is { } l && l >= 2 && l - 2 < _gridRowOfLine.Count ? $"Row {_gridRowOfLine[l - 2]}" : line is null ? "Table" : "Header");

        foreach (var column in _gridColumns)
        {
            var info = preview.Columns.FirstOrDefault(c => string.Equals(c.TemplateSymbol, column.Symbol, StringComparison.Ordinal));
            column.Check = info?.Unit ?? RecordUnitCheck.NotApplicable;
            column.Message = info?.UnitMessage ?? string.Empty;
            column.ExpectedUnit = info?.ExpectedUnit ?? string.Empty;
            column.Target = info?.Target ?? string.Empty;
        }

        PreviewChanged?.Invoke();
    }

    /// <summary>Saves the typed rows as records, all or none. <see langword="null"/> when there is nothing to save them as, or a unit was typed that cannot be read (the status says which).</summary>
    public async Task<RecordImportOutcome?> SaveGridAsync(CancellationToken cancellationToken = default)
    {
        if (Template is not { } template)
            return null;

        var csv = RecordImportService.GridToCsv(template, [.. _gridColumns.Select(c => c.Unit)], [.. _gridRows.Select(r => (IReadOnlyList<string>)r)]);
        _gridRowOfLine = csv.GridRowOfLine;

        if (csv.Problems.Count > 0)
        {
            Status = "Nothing was saved: " + csv.Problems[0];
            GridIssueLines = [.. csv.Problems];
            Changed?.Invoke();
            return null;
        }

        return await CommitAsync(Request(csv.Bytes, null, null), "typed", cancellationToken).ConfigureAwait(true);
    }

    // ------------------------------------------------------------------ the file

    /// <summary>Reads a CSV from a stream through the bounded reader (the byte cap and the time limit hold whatever the file claims) and previews it.</summary>
    public async Task LoadFileAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ImportedFile file;

        try
        {
            file = await BoundedInputReader.ReadAsync(content, fileName, ImportLimits.Default, cancellationToken).ConfigureAwait(true);
        }
        catch (ImportRefusedException e)
        {
            ImportLog.Info(_logger, "Record import: file refused", ("file", ImportLog.FileName(fileName)), ("code", e.Code));
            Status = "That file was not read: " + e.Message;
            Changed?.Invoke();
            return;
        }

        await LoadFileCoreAsync(file, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>The file as already read (the same cap and hash as a stream): for a clipboard or a test.</summary>
    public async Task LoadFileBytesAsync(string fileName, byte[] bytes, CancellationToken cancellationToken = default)
    {
        try
        {
            await LoadFileCoreAsync(BoundedInputReader.FromBytes(bytes, fileName), cancellationToken).ConfigureAwait(true);
        }
        catch (ImportRefusedException e)
        {
            Status = "That file was not read: " + e.Message;
            Changed?.Invoke();
        }
    }

    private async Task LoadFileCoreAsync(ImportedFile file, CancellationToken cancellationToken)
    {
        _fileBytes = file.Bytes;
        FileName = file.FileName;
        FileHash = file.Sha256;
        _columnMap.Clear();
        ImportLog.Info(_logger, "Record import: file read", ("file", ImportLog.FileName(file.FileName)), ("bytes", file.Bytes.Length), ("sha256", ImportLog.HashPrefix(file.Sha256)));
        Status = $"Read '{file.FileName}' ({file.Bytes.Length} bytes).";

        // A file whose header is exactly another template's of this kind belongs to that template: say so, and use it.
        if (HeaderOf(file.Bytes) is { } header && Templates.FirstOrDefault(t => string.Equals(t.Header, header, StringComparison.Ordinal)) is { } exact && exact.Id != Template?.Id)
        {
            SetTemplateCore(exact);
            Status += $" Its header is that of template {exact.FileName}, so that template is used.";
        }

        Changed?.Invoke();
        await PreviewFileAsync(cancellationToken).ConfigureAwait(true);
    }

    private static string? HeaderOf(byte[] bytes)
    {
        try
        {
            var text = BoundedInputReader.DecodeText(BoundedInputReader.FromBytes(bytes, "header.csv"));
            var end = text.IndexOfAny(['\r', '\n']);
            return (end < 0 ? text : text[..end]).Trim();
        }
        catch (ImportRefusedException)
        {
            return null;
        }
    }

    /// <summary>Maps a file column to a template column (or, with <see langword="null"/>, leaves it out) and previews again.</summary>
    public async Task SetColumnMapAsync(string header, string? templateSymbol, CancellationToken cancellationToken = default)
    {
        _columnMap[header] = templateSymbol;
        await PreviewFileAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Chooses how the file writes its numbers (a point or a comma) and previews again. Nothing is guessed.</summary>
    public async Task SetDecimalCommaAsync(bool comma, CancellationToken cancellationToken = default)
    {
        DecimalComma = comma;

        if (_fileBytes is not null)
            await PreviewFileAsync(cancellationToken).ConfigureAwait(true);

        await PreviewGridAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Checks the chosen file against the template: its columns, units and rows.</summary>
    public async Task PreviewFileAsync(CancellationToken cancellationToken = default)
    {
        if (_fileBytes is null || Template is null)
        {
            FilePreview = null;
            FileIssueLines = [];
            PreviewChanged?.Invoke();
            return;
        }

        var preview = await _service.RecordImport.PreviewAsync(Request(_fileBytes, FileName, _columnMap), cancellationToken).ConfigureAwait(true);
        FilePreview = preview;
        FileIssueLines = Lines(preview, line => line is { } l ? $"Line {l}" : "File");
        ImportLog.Info(_logger, "Record import: previewed", ("file", ImportLog.FileName(FileName)), ("template", Template.Id), ("errors", preview.Errors.Count), ("warnings", preview.Warnings.Count), ("records", preview.Planned.Count), ("refused", preview.Refusal is not null));
        PreviewChanged?.Invoke();
    }

    /// <summary>Imports the chosen file: all of it or none of it.</summary>
    public async Task<RecordImportOutcome?> ImportFileAsync(CancellationToken cancellationToken = default)
    {
        if (_fileBytes is null || Template is null)
            return null;

        return await CommitAsync(Request(_fileBytes, FileName, _columnMap), "file", cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Forgets the chosen file.</summary>
    public void ClearFile()
    {
        _fileBytes = null;
        FileName = null;
        FileHash = null;
        FilePreview = null;
        FileIssueLines = [];
        _columnMap.Clear();
        Changed?.Invoke();
    }

    /// <summary>Reads a Track P <c>_provenance.csv</c> and fills the provenance fields from its row for the shown kind.</summary>
    public void LoadProvenanceFile(byte[] bytes)
    {
        var rows = RecordImportService.ReadProvenanceFile(bytes, out var problems);
        var mine = rows.Where(r => string.Equals(r.Kind, SelectedKind, StringComparison.Ordinal)).ToList();

        if (mine.Count == 0)
        {
            Status = rows.Count == 0
                ? "The provenance file has no usable row. " + string.Join(" ", problems.Take(3))
                : $"The provenance file has no row for '{SelectedKind}' (it has {string.Join(", ", rows.Select(r => r.Kind).Distinct(StringComparer.Ordinal))}).";
            Changed?.Invoke();
            return;
        }

        // The file's title ends with a label in brackets when several records of one kind share a template; the first row is used, and the rest named.
        var p = mine[0].Provenance;
        Title = p.Title;
        SourceTitle = p.SourceTitle;
        Edition = p.Edition;
        Amendment = p.Amendment;
        TableOrClause = p.TableOrClause;
        LicenceHolder = p.LicenceHolder;
        Licensed = p.LicensedSource;
        Origin = p.Origin;
        Notes = p.Notes ?? string.Empty;
        Status = mine.Count == 1
            ? $"Provenance read from the file: '{p.Title}'. Who entered it is the signed-in person, not the file's entered_by."
            : $"Provenance read from row 1 of {mine.Count} for this kind ('{p.Title}'). The others: {string.Join("; ", mine.Skip(1).Select(m => m.Provenance.Title))}.";

        if (problems.Count > 0)
            Status += " " + string.Join(" ", problems.Take(2));

        Changed?.Invoke();
        _ = RefreshPreviewsAsync();
    }

    /// <summary>Previews the grid and the file again (after the provenance, the number format or the template changed).</summary>
    public async Task RefreshPreviewsAsync(CancellationToken cancellationToken = default)
    {
        await PreviewGridAsync(cancellationToken).ConfigureAwait(true);

        if (_fileBytes is not null)
            await PreviewFileAsync(cancellationToken).ConfigureAwait(true);
    }

    // ------------------------------------------------------------------

    private RecordImportRequest Request(byte[] bytes, string? fileName, IReadOnlyDictionary<string, string?>? map) =>
        new(bytes, fileName, Template!.Id, Provenance, NumberProfile, null, map is { Count: > 0 } ? new Dictionary<string, string?>(map, StringComparer.Ordinal) : null, AsRevision);

    private async Task<RecordImportOutcome> CommitAsync(RecordImportRequest request, string source, CancellationToken cancellationToken)
    {
        IsBusy = true;
        PreviewChanged?.Invoke();
        RecordImportOutcome outcome;

        try
        {
            outcome = await _service.RecordImport.CommitAsync(request, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }

        ImportLog.Info(_logger, "Record import: committed", ("source", source), ("template", Template?.Id), ("saved", outcome.Records.Count), ("committed", outcome.Committed));

        if (!outcome.Committed)
        {
            Status = outcome.Refusal ?? "Nothing was saved.";

            if (source == "file")
            {
                FilePreview = outcome.Preview;
                FileIssueLines = Lines(outcome.Preview, line => line is { } l ? $"Line {l}" : "File");
            }
            else
            {
                GridPreview = outcome.Preview;
                GridIssueLines = Lines(outcome.Preview, line => line is { } l && l >= 2 && l - 2 < _gridRowOfLine.Count ? $"Row {_gridRowOfLine[l - 2]}" : line is null ? "Table" : "Header");
            }

            PreviewChanged?.Invoke();
            return outcome;
        }

        var first = outcome.Records[0];
        var licensed = outcome.Records.Any(r => r.IsLicensed());
        Status = outcome.Records.Count == 1
            ? $"Saved '{first.Title}' as a reference record (rev {first.Revision}{(licensed ? ", licensed: its values never leave in an export" : string.Empty)}). Apply it from a worksheet."
            : $"Saved {outcome.Records.Count} reference records, from '{first.Title}' to '{outcome.Records[^1].Title}'{(licensed ? " (licensed: their values never leave in an export)" : string.Empty)}. Apply them from a worksheet.";

        if (source == "file")
        {
            _fileBytes = null;
            FileName = null;
            FileHash = null;
            FilePreview = null;
            FileIssueLines = [];
            _columnMap.Clear();
        }
        else
        {
            _gridRows.Clear();
            AddRowCore();
        }

        var saved = Status;
        await RefreshAsync(SelectedKind, cancellationToken).ConfigureAwait(true);
        Status = saved;
        Changed?.Invoke();
        Committed?.Invoke(outcome.Records);
        return outcome;
    }

    private static List<string> Lines(RecordImportPreview preview, Func<int?, string> where)
    {
        var lines = new List<string>();

        if (preview.Refusal is { } refusal)
        {
            lines.Add("Refused: " + refusal);
            return lines;
        }

        foreach (var issue in preview.AllIssues.OrderBy(i => i.Severity).ThenBy(i => i.Line ?? 0))
            lines.Add($"{(issue.Severity == RecordSeverity.Error ? "Error" : "Warning")} · {where(issue.Line)}{(issue.Column is { } c ? ", " + c : string.Empty)}: {issue.Message}");

        return lines;
    }

    /// <summary>The summary line above a preview: what would be saved, or why nothing can be.</summary>
    public static string Summary(RecordImportPreview? preview)
    {
        if (preview is null)
            return string.Empty;

        if (preview.Refusal is not null)
            return "Nothing can be saved: the file was refused.";

        if (!preview.CanCommit)
            return $"Nothing will be saved: {preview.Errors.Count} error(s) must be fixed first ({preview.Warnings.Count} warning(s)).";

        var licensed = preview.Planned.Any(p => p.Licensed);
        return preview.Planned.Count == 1
            ? $"Ready: 1 record, '{preview.Planned[0].Title}', {preview.Planned[0].Rows} table row(s), {preview.Warnings.Count} warning(s){(licensed ? ", licensed (never exported)" : string.Empty)}."
            : $"Ready: {preview.Planned.Count} records, {preview.Warnings.Count} warning(s){(licensed ? ", licensed (never exported)" : string.Empty)}.";
    }
}
