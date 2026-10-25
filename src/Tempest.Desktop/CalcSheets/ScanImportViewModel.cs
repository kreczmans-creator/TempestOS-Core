using System.Globalization;
using Avalonia.Threading;
using Tempest.Calc.Import;
using Tempest.Calc.Import.Scan;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;

namespace Tempest.Desktop.CalcSheets;

/// <summary>One line of the import preview: how the row is classed and the words shown for it.</summary>
public sealed record ScanPreviewLine(ScanRowStatus Status, string Text);

/// <summary>A file chosen for the import with the stage and serial entered for it.</summary>
public sealed record ScanChosenFile(ScanFileInput Input, string Name, string Sha256);

/// <summary>
/// Addendum B, W2: the state behind the worksheet form's "Import scan deviations…" action, with no UI in it. Scan tables are read through the
/// bounded reader (5 MB each, strict UTF-8 or a safe workbook), a stored scan profile is picked, and a preview says what each feature's statistics are,
/// which features are mapped, refused, unmapped or missing, what was reported in the tables (blank cells, text where a number should be, removed markup),
/// and what changed since the previous import; Apply then updates the open draft through <see cref="CalcInstanceService.ApplyScanImportAsync"/> — to the
/// draft as it was previewed: if it changed since, Apply is refused and the import must be previewed again. Nothing is applied while any row is refused.
/// <para>
/// Threading: every method is called on the UI thread and continues on it (a picker's stream completes on a pool thread, and the panel sets Avalonia
/// properties from <see cref="Changed"/>); the events are marshalled to the UI thread whichever thread raised them. The parse and the fit run on the pool
/// under a token <see cref="Cancel"/> trips. Whatever goes wrong in a read, a preview or an apply (an I/O error from a locked file or a dropped share
/// included) is reported in <see cref="Status"/> and leaves the model idle, so Apply, Cancel and Close stay usable.
/// </para>
/// </summary>
public sealed class ScanImportViewModel
{
    /// <summary>The most lines of each kind shown; the rest are summarised (a table of thousands of features must not become thousands of controls).</summary>
    public const int MaxLines = 200;

    private readonly CalcInstanceService _service;
    private readonly List<ScanChosenFile> _files = [];
    private ScanImportPreview? _preview;
    private ScanProfile? _profile;
    private CancellationTokenSource? _cts;
    private int _operation;
    private bool _applying;
    private string _templateId = string.Empty;

    public ScanImportViewModel(CalcInstanceService service) => _service = service ?? throw new ArgumentNullException(nameof(service));

    /// <summary>Raised (on the UI thread) whenever anything the panel shows has changed.</summary>
    public event Action? Changed;

    /// <summary>Raised (on the UI thread) after an import changed the draft, with the updated instance, so the open form can show it.</summary>
    public event Action<CalcInstance>? Applied;

    public Guid? InstanceId { get; private set; }

    public string InstanceName { get; private set; } = string.Empty;

    public IReadOnlyList<ScanChosenFile> Files => _files;

    /// <summary>The profiles offered: the latest version of each that fits this calculation, and the chosen one if it is an older version.</summary>
    public IReadOnlyList<ScanProfile> Profiles { get; private set; } = [];

    public ScanProfile? Profile => _profile;

    /// <summary>The stage entered for the next file chosen (when its table has no stage column).</summary>
    public string NextStage { get; set; } = string.Empty;

    /// <summary>The serial entered for the next file chosen (when its table holds one part).</summary>
    public string NextSerial { get; set; } = string.Empty;

    public string SoftwareVersion { get; private set; } = string.Empty;

    public string Alignment { get; private set; } = string.Empty;

    public int SubgroupSize { get; private set; } = 1;

    public int MinParts { get; private set; } = 30;

    public bool ExcludeOutliers { get; private set; }

    /// <summary>The statistics, one line per feature and stage, with the warnings under it.</summary>
    public IReadOnlyList<string> StatLines { get; private set; } = [];

    /// <summary>What the mapping does to each feature.</summary>
    public IReadOnlyList<ScanPreviewLine> Lines { get; private set; } = [];

    /// <summary>What was reported in the tables: blank cells, text where a number should be, markup removed, rows not used.</summary>
    public IReadOnlyList<string> ProblemLines { get; private set; } = [];

    /// <summary>The importer's own warnings about the tables (whole numbers under a comma delimiter, formula cells, …).</summary>
    public IReadOnlyList<string> NoticeLines { get; private set; } = [];

    public IReadOnlyList<string> ChangeLines { get; private set; } = [];

    /// <summary>What the calculation would block or warn about once the import is applied.</summary>
    public IReadOnlyList<string> AfterLines { get; private set; } = [];

    /// <summary>The number format the profile fixes, in words; the profile's own always wins and is shown, not hidden.</summary>
    public string NumberFormatText => _profile is null ? string.Empty : _profile.Number is { Decimal: DecimalMark.Comma } n ? $"Numbers use a decimal comma{(n.Grouping is { } g ? $" with '{(g == ' ' ? "no-break space" : g.ToString())}' as the grouping mark" : string.Empty)} (fixed by the profile)." : "Numbers use a decimal point (fixed by the profile).";

    public string Summary { get; private set; } = string.Empty;

    public string Status { get; private set; } = string.Empty;

    public bool CanApply => !IsBusy && _preview is { CanApply: true, InstanceUpdatedAt: not null };

    /// <summary>A read, parse or apply is running (the panel offers Cancel).</summary>
    public bool IsBusy { get; private set; }

    public bool IsOpen => InstanceId is not null;

    /// <summary>The last preview, for callers that want the detail (statistics, rows, diff).</summary>
    public ScanImportPreview? Preview => _preview;

    // ------------------------------------------------------------------

    /// <summary>Stops the read, parse or apply that is running. Once the draft is being written there is nothing to cancel: the write finishes or fails as a whole.</summary>
    public void Cancel()
    {
        if (_applying)
            return;

        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Nothing running: nothing to stop.
        }
    }

    /// <summary>Starts an import into a saved draft: forgets earlier files and lists the profiles that fit its template.</summary>
    public async Task BeginAsync(CalcInstance draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        Cancel();
        InstanceId = draft.Id;
        InstanceName = draft.Name;
        _templateId = draft.TemplateId;
        _files.Clear();
        _preview = null;
        ClearOutput();
        IsBusy = false;
        Status = "Choose one or more scan tables (CSV, text or .xlsx exported from ZEISS or GOM Inspect, PolyWorks or Geomagic Control X), then a scan profile that says which column is which.";

        try
        {
            await LoadProfilesAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Status = "The stored scan profiles could not be listed: " + e.Message;
        }

        Raise();
    }

    public void Close()
    {
        Cancel();
        InstanceId = null;
        _files.Clear();
        _preview = null;
        ClearOutput();
        IsBusy = false;
        Raise();
    }

    public async Task LoadProfilesAsync(CancellationToken cancellationToken = default)
    {
        var all = await _service.ListScanProfilesAsync(cancellationToken).ConfigureAwait(true);
        var fitting = all.Where(p => p.TemplateId is null || string.Equals(p.TemplateId, _templateId, StringComparison.Ordinal)).ToList();

        // The chosen profile stays exactly the version it was: a newer version published to the shared store is offered, never switched to.
        if (_profile is not null && !fitting.Any(p => p.Id == _profile.Id && p.Version == _profile.Version))
            fitting.Add(_profile);

        Profiles = fitting;

        if (_profile is null && Profiles.Count == 1)
            _profile = Profiles[0];
    }

    /// <summary>
    /// Reads a chosen table through the bounded reader and (unless <paramref name="refresh"/> is false, for the first files of several) previews it.
    /// A refused file, an I/O error or a cancellation is reported, not thrown; the same file twice is refused.
    /// </summary>
    public async Task AddFileAsync(string fileName, Stream content, bool refresh = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (_files.Count >= ScanImporter.MaxFiles)
        {
            SetBusy(false, $"At most {ScanImporter.MaxFiles} files are imported at once.");
            return;
        }

        var (token, operation) = Begin(cancellationToken);
        SetBusy(true, "Reading the file…");
        ImportedFile file;

        try
        {
            file = await BoundedInputReader.ReadAsync(content, fileName, new ScanSettings().Limits, token).ConfigureAwait(true);
        }
        catch (ImportRefusedException e)
        {
            Fail(operation, "That file was not read: " + e.Message);
            return;
        }
        catch (OperationCanceledException)
        {
            Fail(operation, "Cancelled.");
            return;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ObjectDisposedException or InvalidOperationException)
        {
            Fail(operation, "That file could not be read: " + e.Message);
            return;
        }

        if (operation != _operation)
            return; // a newer read superseded this one

        if (_files.FirstOrDefault(f => string.Equals(f.Sha256, file.Sha256, StringComparison.Ordinal)) is { } same)
        {
            SetBusy(false, $"'{file.FileName}' is the same file as '{same.Name}' (identical SHA-256) and was not added: it would count every part twice.");
            return;
        }

        if (_files.Sum(f => (long)f.Input.File.Bytes.Length) + file.Bytes.Length > new ScanSettings().MaxTotalBytes)
        {
            SetBusy(false, $"'{file.FileName}' was not added: the tables together may hold at most {new ScanSettings().MaxTotalBytes / (1024 * 1024)} MB.");
            return;
        }

        _files.Add(new ScanChosenFile(new ScanFileInput(file, Clean(NextStage), Clean(NextSerial)), file.FileName, file.Sha256));

        if (refresh)
            await PreviewAsync(token, operation).ConfigureAwait(true);
        else
            SetBusy(false, $"{_files.Count} table(s) chosen.");
    }

    public async Task RemoveFileAsync(int index, CancellationToken cancellationToken = default)
    {
        if (index < 0 || index >= _files.Count)
            return;

        _files.RemoveAt(index);
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Reads a scan profile from a stream (the 200 KB cap, strict UTF-8 with the BOM dropped), stores it as a versioned profile and selects it.
    /// Returns the reason when it was refused.
    /// </summary>
    public async Task<string?> LoadProfileStreamAsync(Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        try
        {
            var file = await BoundedInputReader.ReadAsync(content, "profile.json", ImportLimits.Default with { MaxBytes = ScanProfileJson.MaxBytes }, cancellationToken).ConfigureAwait(true);
            return await SaveProfileJsonAsync(BoundedInputReader.DecodeText(file), cancellationToken).ConfigureAwait(true);
        }
        catch (ImportRefusedException e)
        {
            Status = "That profile was not read: " + e.Message;
            Raise();
            return Status;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ObjectDisposedException)
        {
            Status = "That profile could not be read: " + e.Message;
            Raise();
            return Status;
        }
    }

    /// <summary>Stores a scan profile typed or pasted as JSON (a new version of an existing id is a new profile version) and selects it.</summary>
    public async Task<string?> SaveProfileJsonAsync(string json, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(json);
        ScanProfile profile;

        try
        {
            profile = ScanProfileJson.Read(json);
        }
        catch (ImportRefusedException e)
        {
            Status = e.Message;
            Raise();
            return e.Message;
        }

        var (saved, refusal) = await _service.SaveScanProfileAsync(profile, cancellationToken).ConfigureAwait(true);

        if (saved is null)
        {
            Status = refusal ?? "The profile was not saved.";
            Raise();
            return Status;
        }

        _profile = saved;
        await LoadProfilesAsync(cancellationToken).ConfigureAwait(true);
        Status = $"Profile {saved.Label} saved.";
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
        return null;
    }

    public Task SelectProfileAsync(ScanProfile? profile, CancellationToken cancellationToken = default)
    {
        if (profile is not null && _profile is not null && profile.Id == _profile.Id && profile.Version == _profile.Version)
            return Task.CompletedTask;

        _profile = profile;
        return RefreshAsync(cancellationToken);
    }

    /// <summary>What the person entered about the measurement; each goes into provenance or the statistics. Nothing is re-read when nothing changed.</summary>
    public Task SetSettingsAsync(string? softwareVersion, string? alignment, int subgroupSize, int minParts, bool excludeOutliers, CancellationToken cancellationToken = default)
    {
        var version = softwareVersion?.Trim() ?? string.Empty;
        var align = alignment?.Trim() ?? string.Empty;
        var sub = Math.Clamp(subgroupSize, 1, 1000);
        var min = Math.Clamp(minParts, 2, 100_000);

        if (version == SoftwareVersion && align == Alignment && sub == SubgroupSize && min == MinParts && excludeOutliers == ExcludeOutliers)
            return Task.CompletedTask;

        SoftwareVersion = version;
        Alignment = align;
        SubgroupSize = sub;
        MinParts = min;
        ExcludeOutliers = excludeOutliers;
        return RefreshAsync(cancellationToken);
    }

    /// <summary>Re-reads the files with the current choices and rebuilds the preview.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (InstanceId is null || _files.Count == 0)
        {
            if (_files.Count == 0)
            {
                _preview = null;
                ClearOutput();
            }

            Raise();
            return;
        }

        var (token, operation) = Begin(cancellationToken);
        await PreviewAsync(token, operation).ConfigureAwait(true);
    }

    /// <summary>
    /// Applies the previewed import to the draft. It is applied to what was previewed: if the draft changed since the preview, it is refused and must be
    /// previewed again. Refused (with the reasons) when anything in the preview is refused.
    /// </summary>
    public async Task<ScanImportOutcome?> ApplyAsync(CancellationToken cancellationToken = default)
    {
        if (InstanceId is not { } id || _files.Count == 0 || _profile is null)
            return null;

        if (_preview is not { CanApply: true, InstanceUpdatedAt: { } updatedAt })
        {
            SetBusy(false, "Preview the import first; there is nothing to apply.");
            return null;
        }

        var (token, operation) = Begin(cancellationToken);
        SetBusy(true, "Applying…");
        var request = Request(updatedAt);
        ScanImportOutcome outcome;
        _applying = true;

        try
        {
            outcome = await Task.Run(() => _service.ApplyScanImportAsync(id, request, CancellationToken.None), CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _applying = false;

            if (operation == _operation)
                SetBusy(false, "The import could not be applied: " + e.Message);

            return null;
        }
        finally
        {
            _applying = false;
        }

        IsBusy = false;

        if (operation != _operation)
            return outcome;

        if (outcome.Action.Succeeded)
        {
            var applied = $"Imported from {string.Join(", ", _files.Select(f => f.Name))}. {outcome.Diff?.Summary}{(string.IsNullOrEmpty(outcome.Action.Reason) ? string.Empty : " " + outcome.Action.Reason)}";
            await PreviewAsync(token, operation).ConfigureAwait(true);
            Status = applied;
            Summary = applied;
        }
        else
        {
            Status = outcome.Action.Reason ?? "Nothing was imported.";
        }

        Raise();

        if (outcome.Action is { Succeeded: true, Instance: { } instance })
            RaiseApplied(instance);

        return outcome;
    }

    // ------------------------------------------------------------------

    private (CancellationToken Token, int Operation) Begin(CancellationToken external)
    {
        Cancel();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(external);
        return (_cts.Token, ++_operation);
    }

    private void SetBusy(bool busy, string status)
    {
        IsBusy = busy;
        Status = status;
        Raise();
    }

    /// <summary>The operation failed or was cancelled: back to idle with the preview cleared, unless a newer operation has taken over.</summary>
    private void Fail(int operation, string status)
    {
        if (operation != _operation)
            return;

        _preview = null;
        ClearOutput();
        SetBusy(false, status);
    }

    private ScanImportRequest Request(DateTimeOffset? expectedUpdatedAt = null) => new(
        _files.Select(f => f.Input).ToList(),
        _profile!,
        new ScanSettings
        {
            SoftwareVersion = SoftwareVersion.Length == 0 ? null : SoftwareVersion,
            Alignment = Alignment.Length == 0 ? null : Alignment,
            SubgroupSize = SubgroupSize,
            MinParts = MinParts,
            ExcludeOutliers = ExcludeOutliers,
        },
        expectedUpdatedAt);

    private async Task PreviewAsync(CancellationToken token, int operation)
    {
        if (InstanceId is not { } id || _files.Count == 0)
        {
            SetBusy(false, Status);
            return;
        }

        if (_profile is null)
        {
            _preview = null;
            ClearOutput();
            SetBusy(false, Profiles.Count == 0
                ? "No scan profile fits this calculation yet. Load a profile (JSON) that names the table's columns and maps its features to inputs."
                : "Choose a scan profile.");
            return;
        }

        SetBusy(true, "Reading and fitting…");
        var request = Request();
        ScanImportPreview preview;

        try
        {
            // The parse and the statistics are CPU-bound: on the pool, under the token Cancel() trips, and the continuation returns to the UI thread.
            preview = await Task.Run(() => _service.PreviewScanImportAsync(id, request, token), CancellationToken.None).WaitAsync(token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            Fail(operation, "Cancelled.");
            return;
        }
        catch (Exception e)
        {
            Fail(operation, "The tables could not be previewed: " + e.Message);
            return;
        }

        if (operation != _operation)
            return; // a newer read or preview superseded this one

        IsBusy = false;
        _preview = preview;
        Describe(preview);
        Raise();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void ClearOutput()
    {
        StatLines = [];
        Lines = [];
        ProblemLines = [];
        NoticeLines = [];
        ChangeLines = [];
        AfterLines = [];
        Summary = string.Empty;
    }

    private static IReadOnlyList<T> Cap<T>(IReadOnlyList<T> lines, Func<int, T> more) =>
        lines.Count <= MaxLines ? lines : [.. lines.Take(MaxLines), more(lines.Count - MaxLines)];

    private void Describe(ScanImportPreview preview)
    {
        if (preview.Refusal is not null)
        {
            ClearOutput();
            Status = "The tables were not read: " + preview.Refusal;
            return;
        }

        var analysis = preview.Analysis!;
        var mapping = preview.Mapping!;
        var unit = analysis.Profile.Unit;

        StatLines = Cap(analysis.Stats.SelectMany(s => StatLine(s, unit)).ToList(), n => $"… and {n} more line(s)");
        ProblemLines = Cap(analysis.Data.Problems.Select(p => $"{p.Kind.ToString().ToLowerInvariant()} in {p.Column}: {p.Count} time(s). {p.Examples.FirstOrDefault()}").ToList(), n => $"… and {n} more kind(s) of problem");
        NoticeLines = Cap(analysis.Data.Notices.Where(n => n.Severity == CalcNoticeSeverity.Warning).Select(n => "note: " + n.Message).ToList(), n => $"note: … and {n} more");
        Lines = Cap(mapping.Rows.Select(Line).ToList(), n => new ScanPreviewLine(ScanRowStatus.Unmapped, $"… and {n} more row(s)"));
        ChangeLines = preview.Diff is { FirstImport: false } diff
            ? Cap(diff.Changes.Select(c => c.Kind switch
            {
                ScanChangeKind.Added => $"added   {c.Feature} ({Stage(c.Stage)}): {c.After}",
                ScanChangeKind.Removed => $"removed {c.Feature} ({Stage(c.Stage)}) (was {c.Before}){(c.AffectsInput ? "; its input keeps the last imported value" : string.Empty)}",
                _ => $"changed {c.Feature} ({Stage(c.Stage)}): {c.Before} → {c.After}",
            }).ToList(), n => $"… and {n} more change(s)")
            : [];
        AfterLines = [.. preview.BlocksAfter.Take(MaxLines).Select(b => "block: " + b), .. preview.WarningsAfter.Take(MaxLines).Select(w => "warning: " + w)];

        Summary = preview.CanApply
            ? $"{mapping.Count(ScanRowStatus.Mapped)} mapped, {mapping.Count(ScanRowStatus.Unmapped)} not in the profile, {mapping.Count(ScanRowStatus.Missing)} missing; {analysis.Data.Observations.Count} usable rows, {analysis.Data.ProblemCount} reported. {preview.Diff?.Summary}"
            : $"Nothing can be imported yet: {string.Join(" ", preview.Reasons.Take(3))}";
        Status = preview.CanApply ? "Ready to apply." : Summary;
    }

    private static IEnumerable<string> StatLine(ScanFeatureStats s, string unit)
    {
        if (!s.Computable)
        {
            yield return $"{s.Feature} ({Stage(s.Stage)}): n = {s.Count} — not computed";
        }
        else
        {
            yield return string.Create(
                CultureInfo.InvariantCulture,
                $"{s.Feature} ({Stage(s.Stage)}): n = {s.Count}, mean {s.Mean:0.#####} {unit}, σ {s.Sigma:0.######} {unit}, Cp {Num(s.Cp)} Cpk {Num(s.Cpk)} Pp {Num(s.Pp)} Ppk {Num(s.Ppk)}, normality {NormalityText(s.Normality)}");
        }

        foreach (var w in s.Warnings)
            yield return "    " + w;
    }

    private static string NormalityText(ScanNormality n) => n.Verdict switch
    {
        NormalityVerdict.Consistent => string.Create(CultureInfo.InvariantCulture, $"consistent (p = {n.PValue:0.###})"),
        NormalityVerdict.Rejected => string.Create(CultureInfo.InvariantCulture, $"REJECTED (p = {n.PValue:0.###})"),
        _ => "not assessed",
    };

    private static string Num(double? v) => v is { } x ? x.ToString("0.###", CultureInfo.InvariantCulture) : "—";

    private static string Stage(string stage) => stage.Length == 0 ? "no stage" : stage;

    private static ScanPreviewLine Line(ScanMappedRow row) => new(row.Status, row.Status switch
    {
        ScanRowStatus.Mapped => $"mapped     {row.Feature} ({Stage(row.Stage)}) → {row.Target}: {row.Value}",
        ScanRowStatus.Refused => $"refused    {row.Feature}: {row.Message}",
        ScanRowStatus.Unmapped => $"unmapped   {row.Feature} ({Stage(row.Stage)}): {row.Value}",
        _ => $"missing    {row.Feature}: {row.Message}",
    });

    /// <summary>Raises <see cref="Changed"/> on the UI thread.</summary>
    private void Raise() => OnUiThread(() => Changed?.Invoke());

    private void RaiseApplied(CalcInstance instance) => OnUiThread(() => Applied?.Invoke(instance));

    private static void OnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }
}
