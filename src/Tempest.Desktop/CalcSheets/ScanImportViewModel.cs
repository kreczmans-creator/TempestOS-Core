using System.Globalization;
using Tempest.Calc.Import;
using Tempest.Calc.Import.Scan;
using Tempest.Calc.Library;

namespace Tempest.Desktop.CalcSheets;

/// <summary>One line of the import preview: how the row is classed and the words shown for it.</summary>
public sealed record ScanPreviewLine(ScanRowStatus Status, string Text);

/// <summary>A file chosen for the import with the stage and serial entered for it.</summary>
public sealed record ScanChosenFile(ScanFileInput Input, string Name, string Sha256);

/// <summary>
/// Addendum B, W2: the state behind the worksheet form's "Import scan deviations…" action, with no UI in it. Scan tables are read through the
/// bounded reader (5 MB each, strict UTF-8 or a safe workbook), a stored scan profile is picked, and a preview says what each feature's statistics are,
/// which features are mapped, refused, unmapped or missing, what was reported in the tables (blank cells, text where a number should be, removed markup),
/// and what changed since the previous import; Apply then updates the open draft through <see cref="CalcInstanceService.ApplyScanImportAsync"/>.
/// Nothing is applied while any row is refused.
/// <para>
/// Every continuation stays on the caller's context (<c>ConfigureAwait(true)</c>), so <see cref="Changed"/> and <see cref="Applied"/> are raised on the
/// UI thread whatever stream the picker hands over (a real file stream completes on a pool thread); the CPU-bound parse and fit run on the pool and can be
/// cancelled with <see cref="Cancel"/> (W1 security review, H4).
/// </para>
/// </summary>
public sealed class ScanImportViewModel
{
    private readonly CalcInstanceService _service;
    private readonly List<ScanChosenFile> _files = [];
    private ScanImportPreview? _preview;
    private ScanProfile? _profile;
    private CancellationTokenSource? _running;
    private string _templateId = string.Empty;

    public ScanImportViewModel(CalcInstanceService service) => _service = service ?? throw new ArgumentNullException(nameof(service));

    /// <summary>Raised whenever anything the panel shows has changed.</summary>
    public event Action? Changed;

    /// <summary>Raised after an import changed the draft, with the updated instance, so the open form can show it.</summary>
    public event Action<CalcInstance>? Applied;

    public Guid? InstanceId { get; private set; }

    public string InstanceName { get; private set; } = string.Empty;

    public IReadOnlyList<ScanChosenFile> Files => _files;

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

    public IReadOnlyList<string> ChangeLines { get; private set; } = [];

    /// <summary>What the calculation would block or warn about once the import is applied.</summary>
    public IReadOnlyList<string> AfterLines { get; private set; } = [];

    /// <summary>The number format the profile fixes, in words; the profile's own always wins and is shown, not hidden.</summary>
    public string NumberFormatText => _profile is null ? string.Empty : _profile.Number is { Decimal: DecimalMark.Comma } n ? $"Numbers use a decimal comma{(n.Grouping is { } g ? $" with '{g}' as the grouping mark" : string.Empty)} (fixed by the profile)." : "Numbers use a decimal point (fixed by the profile).";

    public string Summary { get; private set; } = string.Empty;

    public string Status { get; private set; } = string.Empty;

    public bool CanApply => _preview is { CanApply: true } && _running is null;

    public bool IsBusy => _running is not null;

    public bool IsOpen => InstanceId is not null;

    /// <summary>The last preview, for callers that want the detail (statistics, rows, diff).</summary>
    public ScanImportPreview? Preview => _preview;

    // ------------------------------------------------------------------

    /// <summary>Starts an import into a saved draft: forgets earlier files and lists the profiles that fit its template.</summary>
    public async Task BeginAsync(CalcInstance draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        InstanceId = draft.Id;
        InstanceName = draft.Name;
        _templateId = draft.TemplateId;
        _files.Clear();
        _preview = null;
        ClearOutput();
        Status = "Choose one or more scan tables (CSV, text or .xlsx exported from ZEISS or GOM Inspect, PolyWorks or Geomagic Control X), then a scan profile that says which column is which.";
        await LoadProfilesAsync(cancellationToken).ConfigureAwait(true);
        Changed?.Invoke();
    }

    public void Close()
    {
        Cancel();
        InstanceId = null;
        _files.Clear();
        _preview = null;
        ClearOutput();
        Changed?.Invoke();
    }

    /// <summary>Stops a read or fit that is running.</summary>
    public void Cancel() => _running?.Cancel();

    public async Task LoadProfilesAsync(CancellationToken cancellationToken = default)
    {
        var all = await _service.ListScanProfilesAsync(cancellationToken).ConfigureAwait(true);
        Profiles = all.Where(p => p.TemplateId is null || string.Equals(p.TemplateId, _templateId, StringComparison.Ordinal)).ToList();

        // A chosen profile stays the version chosen: a newer version of the same id is offered, never switched to silently.
        if (_profile is not null && !Profiles.Any(p => p.Id == _profile.Id))
            _profile = null;

        if (_profile is null && Profiles.Count == 1)
            _profile = Profiles[0];
    }

    /// <summary>Reads a chosen table through the bounded reader and previews it. A refused file is reported, not thrown.</summary>
    public async Task AddFileAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (_files.Count >= ScanImporter.MaxFiles)
        {
            Status = $"At most {ScanImporter.MaxFiles} files are imported at once.";
            Changed?.Invoke();
            return;
        }

        using var cts = StartRun(cancellationToken);

        try
        {
            var file = await BoundedInputReader.ReadAsync(content, fileName, new ScanSettings().Limits, cts.Token).ConfigureAwait(true);
            _files.Add(new ScanChosenFile(new ScanFileInput(file, Clean(NextStage), Clean(NextSerial)), file.FileName, file.Sha256));
        }
        catch (ImportRefusedException e)
        {
            Status = "That file was not read: " + e.Message;
            EndRun();
            Changed?.Invoke();
            return;
        }
        catch (OperationCanceledException)
        {
            Status = "Cancelled.";
            EndRun();
            Changed?.Invoke();
            return;
        }

        EndRun();
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    public async Task RemoveFileAsync(int index, CancellationToken cancellationToken = default)
    {
        if (index < 0 || index >= _files.Count)
            return;

        _files.RemoveAt(index);
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Stores a scan profile typed or loaded as JSON (a new version of an existing id is a new profile version) and selects it.</summary>
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
            Changed?.Invoke();
            return e.Message;
        }

        var (saved, refusal) = await _service.SaveScanProfileAsync(profile, cancellationToken).ConfigureAwait(true);

        if (saved is null)
        {
            Status = refusal ?? "The profile was not saved.";
            Changed?.Invoke();
            return Status;
        }

        await LoadProfilesAsync(cancellationToken).ConfigureAwait(true);
        _profile = Profiles.FirstOrDefault(p => p.Id == saved.Id && p.Version == saved.Version) ?? saved;
        Status = $"Profile {saved.Label} saved.";
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
        return null;
    }

    public Task SelectProfileAsync(ScanProfile? profile, CancellationToken cancellationToken = default)
    {
        _profile = profile;
        return RefreshAsync(cancellationToken);
    }

    /// <summary>What the person entered about the measurement; each goes into provenance or the statistics.</summary>
    public Task SetSettingsAsync(string? softwareVersion, string? alignment, int subgroupSize, int minParts, bool excludeOutliers, CancellationToken cancellationToken = default)
    {
        SoftwareVersion = softwareVersion?.Trim() ?? string.Empty;
        Alignment = alignment?.Trim() ?? string.Empty;
        SubgroupSize = Math.Clamp(subgroupSize, 1, 1000);
        MinParts = Math.Clamp(minParts, 2, 100_000);
        ExcludeOutliers = excludeOutliers;
        return RefreshAsync(cancellationToken);
    }

    /// <summary>Re-reads the files with the current choices and rebuilds the preview.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (InstanceId is not { } id || _files.Count == 0)
        {
            Changed?.Invoke();
            return;
        }

        if (_profile is null)
        {
            _preview = null;
            ClearOutput();
            Status = Profiles.Count == 0
                ? "No scan profile fits this calculation yet. Load a profile (JSON) that names the table's columns and maps its features to inputs."
                : "Choose a scan profile.";
            Changed?.Invoke();
            return;
        }

        var request = Request();
        using var cts = StartRun(cancellationToken);
        Status = "Reading and fitting…";
        Changed?.Invoke();

        try
        {
            // The parse and the statistics are CPU-bound: off the UI thread, back on it for the result.
            _preview = await Task.Run(() => _service.PreviewScanImportAsync(id, request, cts.Token), cts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            _preview = null;
            ClearOutput();
            Status = "Cancelled.";
            EndRun();
            Changed?.Invoke();
            return;
        }

        EndRun();
        Describe(_preview);
        Changed?.Invoke();
    }

    /// <summary>Applies the previewed import to the draft. Refused (with the reasons) when anything in the preview is refused.</summary>
    public async Task<ScanImportOutcome?> ApplyAsync(CancellationToken cancellationToken = default)
    {
        if (InstanceId is not { } id || _files.Count == 0 || _profile is null)
            return null;

        var request = Request();
        using var cts = StartRun(cancellationToken);
        ScanImportOutcome outcome;

        try
        {
            outcome = await Task.Run(() => _service.ApplyScanImportAsync(id, request, cts.Token), cts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            Status = "Cancelled.";
            EndRun();
            Changed?.Invoke();
            return null;
        }

        EndRun();

        if (outcome.Action.Succeeded)
        {
            Status = $"Imported from {string.Join(", ", _files.Select(f => f.Name))}. {outcome.Diff?.Summary}";
            _preview = outcome.Preview;

            if (_preview is not null)
                Describe(_preview);

            Summary = Status;
        }
        else
        {
            Status = outcome.Action.Reason ?? "Nothing was imported.";
        }

        Changed?.Invoke();

        if (outcome.Action is { Succeeded: true, Instance: { } instance })
            Applied?.Invoke(instance);

        return outcome;
    }

    // ------------------------------------------------------------------

    private ScanImportRequest Request() => new(
        _files.Select(f => f.Input).ToList(),
        _profile!,
        new ScanSettings
        {
            SoftwareVersion = SoftwareVersion.Length == 0 ? null : SoftwareVersion,
            Alignment = Alignment.Length == 0 ? null : Alignment,
            SubgroupSize = SubgroupSize,
            MinParts = MinParts,
            ExcludeOutliers = ExcludeOutliers,
        });

    private CancellationTokenSource StartRun(CancellationToken external)
    {
        _running?.Cancel();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(external);
        _running = cts;
        return cts;
    }

    private void EndRun() => _running = null;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void ClearOutput()
    {
        StatLines = [];
        Lines = [];
        ProblemLines = [];
        ChangeLines = [];
        AfterLines = [];
        Summary = string.Empty;
    }

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

        StatLines = analysis.Stats.SelectMany(s => StatLine(s, unit)).ToList();
        ProblemLines = analysis.Data.Problems.Select(p => $"{p.Kind.ToString().ToLowerInvariant()} in {p.Column}: {p.Count} time(s). {p.Examples.FirstOrDefault()}").ToList();
        Lines = mapping.Rows.Select(Line).ToList();
        ChangeLines = preview.Diff is { FirstImport: false } diff
            ? diff.Changes.Select(c => c.Kind switch
            {
                ScanChangeKind.Added => $"added   {c.Feature} ({Stage(c.Stage)}): {c.After}",
                ScanChangeKind.Removed => $"removed {c.Feature} ({Stage(c.Stage)}) (was {c.Before}){(c.AffectsInput ? "; its input keeps the last imported value" : string.Empty)}",
                _ => $"changed {c.Feature} ({Stage(c.Stage)}): {c.Before} → {c.After}",
            }).ToList()
            : [];
        AfterLines = [.. preview.BlocksAfter.Select(b => "block: " + b), .. preview.WarningsAfter.Select(w => "warning: " + w)];

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
}
