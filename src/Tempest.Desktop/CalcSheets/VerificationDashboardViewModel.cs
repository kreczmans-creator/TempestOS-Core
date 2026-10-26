using System.Globalization;
using System.Text;
using Tempest.Calc.Library.Dashboard;
using Tempest.Workspace.Files;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// Addendum C, C-4: the state behind Calculations → Verification, with no UI in it. It builds the report through
/// <see cref="VerificationDashboardService"/>, holds the discipline and status filters, and exports what is shown to markdown and PDF through
/// the file picker the shell already uses. It only reads: nothing here changes a calculation, its Evidence status or an issued record.
/// <para>
/// Threading: every method is called on the UI thread and continues on it; <see cref="Changed"/> is raised on that thread.
/// </para>
/// </summary>
public sealed class VerificationDashboardViewModel
{
    /// <summary>The first choice of the discipline filter.</summary>
    public const string AllDisciplines = "All disciplines";

    /// <summary>The largest file written by an export (bytes). The markdown is capped at about 1 MB and a 500-row PDF is well under this.</summary>
    public const int MaxExportBytes = 8_000_000;

    private readonly VerificationDashboardService _service;
    private readonly IFilePicker _filePicker;
    private readonly VerificationPdfRenderer _pdf;
    private readonly Func<string> _applicationVersion;
    private VerificationReport? _report;
    private bool _busy;

    public VerificationDashboardViewModel(VerificationDashboardService service, IFilePicker filePicker, VerificationPdfRenderer pdf, Func<string> applicationVersion)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _filePicker = filePicker ?? throw new ArgumentNullException(nameof(filePicker));
        _pdf = pdf ?? throw new ArgumentNullException(nameof(pdf));
        _applicationVersion = applicationVersion ?? throw new ArgumentNullException(nameof(applicationVersion));
    }

    /// <summary>Raised (on the UI thread) whenever anything the view shows has changed.</summary>
    public event Action? Changed;

    /// <summary>The discipline filter: <see cref="AllDisciplines"/> or one discipline's label ("Chains" included).</summary>
    public string DisciplineFilter { get; private set; } = AllDisciplines;

    /// <summary>The status filter.</summary>
    public VerificationStatusFilter StatusFilter { get; private set; } = VerificationStatusFilter.All;

    /// <summary>The disciplines to choose from: <see cref="AllDisciplines"/> first.</summary>
    public IReadOnlyList<string> DisciplineChoices { get; private set; } = [AllDisciplines];

    /// <summary>The rows shown: the report's rows under both filters.</summary>
    public IReadOnlyList<VerificationRow> Rows { get; private set; } = [];

    /// <summary>The report the rows come from; <see langword="null"/> until the first <see cref="RefreshAsync"/> succeeds.</summary>
    public VerificationReport? Report => _report;

    /// <summary>One line about what is shown ("12 of 65 rows: 3 failing, ...").</summary>
    public string Summary { get; private set; } = "Opening the verification dashboard…";

    /// <summary>The last thing that happened (a file saved, a refusal), in one plain sentence; empty before any.</summary>
    public string Status { get; private set; } = string.Empty;

    /// <summary>Whether a refresh or an export is running (the view disables its buttons).</summary>
    public bool IsBusy => _busy;

    /// <summary>The words each status filter has in the view.</summary>
    public static IReadOnlyList<(VerificationStatusFilter Filter, string Label)> StatusChoices { get; } =
    [
        (VerificationStatusFilter.All, "All statuses"),
        (VerificationStatusFilter.Failing, "Failing"),
        (VerificationStatusFilter.OpenDefects, "Open defects"),
        (VerificationStatusFilter.UnconfirmedOrSecondary, "Unconfirmed or Secondary"),
        (VerificationStatusFilter.MissingRecords, "Records not entered"),
        (VerificationStatusFilter.NotRun, "Not run"),
        (VerificationStatusFilter.NotRederived, "Not re-derived"),
        (VerificationStatusFilter.NotManuallyTested, "Not manually tested"),
        (VerificationStatusFilter.Clean, "Clean"),
    ];

    /// <summary>The filter in words, as the exports print it.</summary>
    public string FilterText =>
        $"Discipline: {(DisciplineFilter == AllDisciplines ? "all" : DisciplineFilter)}; status: {StatusChoices.First(c => c.Filter == StatusFilter).Label.ToLowerInvariant()}";

    /// <summary>Reads everything again (the catalogue, the document data, the records store, the manual-test status) and applies the filters.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_busy)
            return;

        _busy = true;
        Changed?.Invoke();

        try
        {
            _report = await _service.BuildAsync(cancellationToken).ConfigureAwait(true);
            DisciplineChoices = [AllDisciplines, .. _report.Disciplines];

            // A refresh never keeps a discipline the report no longer has.
            if (!DisciplineChoices.Contains(DisciplineFilter, StringComparer.Ordinal))
                DisciplineFilter = AllDisciplines;

            Apply();
            Status = _report.DataProblem is { Length: > 0 } problem ? problem : string.Empty;
        }
        catch (OperationCanceledException)
        {
            Status = "Opening the verification dashboard was cancelled.";
        }
#pragma warning disable CA1031 // The screen opens this without awaiting it: whatever fails while the report is built is shown as one sentence, never left unobserved.
        catch (Exception)
        {
            Status = "The verification dashboard could not be built, so it is empty. Close it and open it again; if that does not help, restart TempestOS.";
        }
#pragma warning restore CA1031
        finally
        {
            _busy = false;
            Changed?.Invoke();
        }
    }

    /// <summary>Filters by discipline; a name that is not in <see cref="DisciplineChoices"/> shows every discipline.</summary>
    public void SetDiscipline(string? discipline)
    {
        DisciplineFilter = discipline is not null && DisciplineChoices.Contains(discipline, StringComparer.Ordinal) ? discipline : AllDisciplines;
        Apply();
        Changed?.Invoke();
    }

    /// <summary>Filters by status.</summary>
    public void SetStatus(VerificationStatusFilter status)
    {
        StatusFilter = Enum.IsDefined(status) ? status : VerificationStatusFilter.All;
        Apply();
        Changed?.Invoke();
    }

    private void Apply()
    {
        if (_report is null)
            return;

        Rows = _report.Filter(DisciplineFilter == AllDisciplines ? null : DisciplineFilter, StatusFilter);
        var failing = Rows.Count(r => r.IsFailing);

        // Without the document data the open defects are unknown, which is not the same as none.
        var defects = Rows.Any(r => !r.DefectsKnown) ? "open defects unknown" : $"{Rows.Sum(r => r.OpenDefectCount)} open defect(s) on {Rows.Count(r => r.HasOpenDefects)} row(s)";
        Summary = _report.Rows.Count == 0
            ? "There are no calculators or chains to verify."
            : $"{Rows.Count} of {_report.Rows.Count} rows: {failing} failing, {defects}, {Rows.Count(r => r.HasUnconfirmedOrSecondary)} with Unconfirmed or Secondary cases, {Rows.Count(r => r.HasMissingRecords)} with records not entered, {Rows.Count(r => r.KatState == KatRunState.NotRun)} not run, {Rows.Count(r => !r.IsRederived)} not re-derived."
              + (_report.Truncated ? $" The first {_report.Rows.Count} of {_report.TotalRows} are listed." : string.Empty);
    }

    /// <summary>Saves the rows shown as a markdown file the person chooses.</summary>
    public Task ExportMarkdownAsync(CancellationToken cancellationToken = default) =>
        ExportAsync("md", "markdown", report => Encoding.UTF8.GetBytes(VerificationMarkdown.Write(report, Rows, FilterText, Version())), cancellationToken);

    /// <summary>Saves the rows shown as a PDF the person chooses.</summary>
    public Task ExportPdfAsync(CancellationToken cancellationToken = default) =>
        ExportAsync("pdf", "PDF", report => _pdf.Render(report, Rows, FilterText, Version()).ToArray(), cancellationToken);

    private string Version() => _applicationVersion() is { Length: > 0 } text ? text : "development build";

    /// <summary>
    /// Writes <paramref name="bytes"/> to a temporary file in the same folder and then moves it into place, so a failure half way leaves the
    /// file that was there untouched and never a partly written export.
    /// </summary>
    private static async Task WriteThroughTempFileAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        var full = Path.GetFullPath(path);
        var temp = Path.Combine(Path.GetDirectoryName(full) ?? ".", $".{Path.GetFileName(full)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllBytesAsync(temp, bytes, cancellationToken).ConfigureAwait(true);
            File.Move(temp, full, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A temporary file that cannot be removed is left for the person to delete; it is not the export's result.
            }
        }
    }

    private async Task ExportAsync(string extension, string noun, Func<VerificationReport, byte[]> render, CancellationToken cancellationToken)
    {
        if (_busy)
            return;

        if (_report is null || Rows.Count == 0)
        {
            Status = "There are no rows to export. Change the filter so that at least one row is shown.";
            Changed?.Invoke();
            return;
        }

        _busy = true;
        Changed?.Invoke();

        try
        {
            var suggested = $"tempestos-verification-{_report.GeneratedAt.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.{extension}";
            var path = await _filePicker.PickSavePathAsync(new SavePickerRequest($"Export the verification dashboard as {noun}", suggested), cancellationToken).ConfigureAwait(true);

            if (string.IsNullOrWhiteSpace(path))
            {
                Status = $"The {noun} export was cancelled; nothing was saved.";
                return;
            }

            // The file's extension is the export's: a name that has another (or none) gets ".md" or ".pdf" appended, so an export can never be
            // saved as an .exe or a .txt. The picker's own overwrite prompt covered the name the person typed, not the name with the extension
            // added, so an added extension never replaces a file that is already there.
            var wanted = "." + extension;
            var appended = !string.Equals(Path.GetExtension(path), wanted, StringComparison.OrdinalIgnoreCase);

            if (appended)
                path += wanted;

            if (appended && File.Exists(path))
            {
                Status = $"A file named {Path.GetFileName(path)} already exists in that folder, so nothing was saved. Choose a file name that ends in {wanted}, or another folder.";
                return;
            }

            var bytes = render(_report);

            if (bytes.Length == 0 || bytes.Length > MaxExportBytes)
            {
                Status = $"The {noun} file would be {(bytes.Length == 0 ? "empty" : "too large")}, so it was not saved. Filter the rows to fewer and try again.";
                return;
            }

            await WriteThroughTempFileAsync(path, bytes, cancellationToken).ConfigureAwait(true);
            Status = $"Saved {Rows.Count} row(s) of the verification dashboard to {Path.GetFileName(path)}.";
        }
        catch (OperationCanceledException)
        {
            Status = $"The {noun} export was cancelled; nothing was saved.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            Status = $"The {noun} file could not be saved ({ex.Message.TrimEnd('.')}). Choose another folder or file name and try again.";
        }
#pragma warning disable CA1031 // The export buttons start this without awaiting it: whatever else goes wrong (the renderer, the picker) is shown as a sentence, never left unobserved.
        catch (Exception)
        {
            Status = $"The {noun} file could not be saved. Try again, and if it still fails choose another folder or restart TempestOS.";
        }
#pragma warning restore CA1031
        finally
        {
            _busy = false;
            Changed?.Invoke();
        }
    }
}
