using Avalonia.Threading;
using Tempest.Calc.Import;
using Tempest.Calc.Import.Cad;
using Tempest.Calc.Import.Mapping;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;

namespace Tempest.Desktop.CalcSheets;

/// <summary>One line of the import preview: how the row is classed and the words shown for it.</summary>
public sealed record CadPreviewLine(CadRowStatus Status, string Text);

/// <summary>
/// Addendum B, W1: the state behind the worksheet form's "Import CAD parameters…" action, with no UI in it. A CAD file is
/// read through the bounded reader (5 MB, strict UTF-8 or a safe workbook), a format and a stored mapping profile are picked,
/// and a preview says which names are mapped, refused, unmapped, read-only expressions or missing, and what changed since the
/// previous import; Apply then updates the open draft through <see cref="CalcInstanceService.ApplyCadImportAsync"/>. Nothing
/// is evaluated and nothing is applied while any row is refused.
/// <para>
/// Threading: every method is called on the UI thread and continues on it (a file picker's stream completes on a pool thread, and
/// the panel sets Avalonia properties from <see cref="Changed"/>); the events are marshalled to the UI thread whichever thread
/// raised them. The parse and the preview run on the thread pool under a cancellation token (<see cref="Cancel"/>), so a large
/// file does not freeze the form.
/// </para>
/// </summary>
public sealed class CadImportViewModel
{
    private const int MaxNoticeLines = 20;

    private readonly CalcInstanceService _service;
    private ImportedFile? _file;
    private CadImportPreview? _preview;
    private CadSourceFormat? _format;
    private CadMappingProfile? _profile;
    private string _templateId = string.Empty;
    private CancellationTokenSource? _cts;
    private int _operation;
    private bool _applying;

    public CadImportViewModel(CalcInstanceService service) => _service = service ?? throw new ArgumentNullException(nameof(service));

    /// <summary>Raised (on the UI thread) whenever anything the panel shows has changed.</summary>
    public event Action? Changed;

    /// <summary>Raised (on the UI thread) after an import changed the draft, with the updated instance, so the open form can show it.</summary>
    public event Action<CalcInstance>? Applied;

    public Guid? InstanceId { get; private set; }

    public string InstanceName { get; private set; } = string.Empty;

    public string? FileName => _file?.FileName;

    public string? FileHash => _file?.Sha256;

    public IReadOnlyList<CadSourceFormat> Formats { get; private set; } = [];

    /// <summary>The profiles offered: the latest version of each that fits this calculation, and the chosen one if it is an older version.</summary>
    public IReadOnlyList<CadMappingProfile> Profiles { get; private set; } = [];

    public CadSourceFormat? Format => _format;

    public CadMappingProfile? Profile => _profile;

    /// <summary>The person's choice: the file's numbers use a decimal comma. A profile that states its own number format wins; see <see cref="EffectiveNumber"/>.</summary>
    public bool DecimalComma { get; private set; }

    /// <summary>The number format the file is read with: the profile's when it states one, else the person's choice.</summary>
    public NumberProfile EffectiveNumber => _profile?.Number ?? (DecimalComma ? NumberProfile.Comma : NumberProfile.Point);

    /// <summary>The profile fixes the number format, so the person's choice is not used (and the panel does not offer it).</summary>
    public bool NumberFormatFixedByProfile => _profile?.Number is not null;

    /// <summary>The effective number format in words, and who decided it.</summary>
    public string NumberFormatText
    {
        get
        {
            var number = EffectiveNumber;
            var mark = number.Decimal == DecimalMark.Comma ? "decimal comma" : "decimal point";
            var grouping = number.Grouping is { } g ? $", '{(g == ' ' ? "no-break space" : g.ToString())}' as a thousands separator" : string.Empty;
            return $"Numbers are read with a {mark}{grouping} ({(NumberFormatFixedByProfile ? "set by the profile" : "your choice")}).";
        }
    }

    public string CadVersion { get; private set; } = string.Empty;

    public string PartName { get; private set; } = string.Empty;

    public string Revision { get; private set; } = string.Empty;

    public string Configuration { get; private set; } = string.Empty;

    public string SheetName { get; private set; } = string.Empty;

    public IReadOnlyList<CadPreviewLine> Lines { get; private set; } = [];

    public IReadOnlyList<string> ChangeLines { get; private set; } = [];

    /// <summary>What the calculation would block or warn about once the import is applied.</summary>
    public IReadOnlyList<string> AfterLines { get; private set; } = [];

    /// <summary>The importer's own warnings about the file (a decimal comma in a decimal-point file, a thousands separator used, a unit conflict, …).</summary>
    public IReadOnlyList<string> NoticeLines { get; private set; } = [];

    public string Summary { get; private set; } = string.Empty;

    public string Status { get; private set; } = string.Empty;

    public bool CanApply => !IsBusy && _preview is { CanApply: true };

    public bool IsOpen => InstanceId is not null;

    /// <summary>A read, parse or apply is running (the panel offers Cancel).</summary>
    public bool IsBusy { get; private set; }

    /// <summary>The last preview, for callers that want the detail (rows, parse, diff).</summary>
    public CadImportPreview? Preview => _preview;

    // ------------------------------------------------------------------

    /// <summary>Stops the read, parse or apply that is running. Nothing already applied is undone.</summary>
    public void Cancel()
    {
        // Once the draft is being written there is nothing to cancel: the write finishes or fails as a whole.
        if (!_applying)
            _cts?.Cancel();
    }

    /// <summary>Starts an import into a saved draft: forgets any earlier file and lists the profiles that fit its template.</summary>
    public async Task BeginAsync(CalcInstance draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        Cancel();
        InstanceId = draft.Id;
        InstanceName = draft.Name;
        _templateId = draft.TemplateId;
        _file = null;
        _preview = null;
        _format = null;
        Formats = [];
        Lines = [];
        ChangeLines = [];
        AfterLines = [];
        NoticeLines = [];
        Summary = string.Empty;
        IsBusy = false;
        Status = "Choose a CAD parameter file (NX .exp or .xlsx, Creo or SolidWorks text, a SolidWorks design table, Onshape or generic CSV or JSON).";
        await LoadProfilesAsync(cancellationToken).ConfigureAwait(true);
        Raise();
    }

    public void Close()
    {
        Cancel();
        InstanceId = null;
        _file = null;
        _preview = null;
        Lines = [];
        ChangeLines = [];
        AfterLines = [];
        NoticeLines = [];
        IsBusy = false;
        Raise();
    }

    public async Task LoadProfilesAsync(CancellationToken cancellationToken = default)
    {
        var all = await _service.ListImportProfilesAsync(cancellationToken).ConfigureAwait(true);
        var fitting = all.Where(p => p.TemplateId is null || string.Equals(p.TemplateId, _templateId, StringComparison.Ordinal)).ToList();

        // The chosen profile stays exactly the version it was: a newer version published to the shared store is offered, never switched to.
        if (_profile is not null && !fitting.Any(p => p.Id == _profile.Id && p.Version == _profile.Version))
            fitting.Add(_profile);

        Profiles = fitting;

        if (_profile is null && Profiles.Count == 1)
            _profile = Profiles[0];
    }

    /// <summary>Reads the chosen file through the bounded reader and previews it. A refused file is reported, not thrown.</summary>
    public async Task LoadFileAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var (token, operation) = Begin(cancellationToken);
        SetBusy(true, "Reading the file…");

        try
        {
            _file = await BoundedInputReader.ReadAsync(content, fileName, ImportLimits.Default, token).ConfigureAwait(true);
        }
        catch (ImportRefusedException e)
        {
            _file = null;
            Reset("That file was not read: " + e.Message);
            return;
        }
        catch (OperationCanceledException)
        {
            _file = null;
            Reset("Cancelled.");
            return;
        }

        Formats = CadImporter.CandidatesFor(_file.FileName, _file);
        _format = Formats.Count > 0 ? Formats[0] : null;

        if (_format is null)
        {
            Reset($"A '{Path.GetExtension(_file.FileName)}' file is not one of the CAD parameter formats.");
            return;
        }

        await PreviewAsync(token, operation).ConfigureAwait(true);
    }

    /// <summary>
    /// Reads a mapping profile from a stream (the 200 KB cap, strict UTF-8 with the BOM dropped), stores it as a versioned profile and
    /// selects it. Returns the reason when it was refused.
    /// </summary>
    public async Task<string?> LoadProfileStreamAsync(Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        try
        {
            var file = await BoundedInputReader.ReadAsync(content, "profile.json", new ImportLimits { MaxBytes = CadMappingProfileJson.MaxBytes }, cancellationToken).ConfigureAwait(true);
            return await SaveProfileJsonAsync(BoundedInputReader.DecodeText(file), cancellationToken).ConfigureAwait(true);
        }
        catch (ImportRefusedException e)
        {
            Status = "That profile was not read: " + e.Message;
            Raise();
            return Status;
        }
    }

    /// <summary>Stores a mapping profile typed or pasted as JSON (a change is a new version) and selects it.</summary>
    public async Task<string?> SaveProfileJsonAsync(string json, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(json);
        CadMappingProfile profile;

        try
        {
            profile = CadMappingProfileJson.Read(json);
        }
        catch (ImportRefusedException e)
        {
            Status = e.Message;
            Raise();
            return e.Message;
        }

        var (saved, refusal) = await _service.SaveImportProfileAsync(profile, cancellationToken).ConfigureAwait(true);

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

    public Task SelectFormatAsync(CadSourceFormat format, CancellationToken cancellationToken = default)
    {
        _format = format;
        return RefreshAsync(cancellationToken);
    }

    public Task SelectProfileAsync(CadMappingProfile? profile, CancellationToken cancellationToken = default)
    {
        _profile = profile;
        return RefreshAsync(cancellationToken);
    }

    /// <summary>What the person entered about the file; each is optional and takes precedence over what the file says.</summary>
    public Task SetSettingsAsync(bool decimalComma, string? cadVersion, string? partName, string? revision, string? configuration, string? sheetName, CancellationToken cancellationToken = default)
    {
        DecimalComma = decimalComma;
        CadVersion = cadVersion?.Trim() ?? string.Empty;
        PartName = partName?.Trim() ?? string.Empty;
        Revision = revision?.Trim() ?? string.Empty;
        Configuration = configuration?.Trim() ?? string.Empty;
        SheetName = sheetName?.Trim() ?? string.Empty;
        return RefreshAsync(cancellationToken);
    }

    /// <summary>Re-reads the file with the current choices and rebuilds the preview.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (InstanceId is null || _file is null || _format is null)
        {
            Raise();
            return;
        }

        var (token, operation) = Begin(cancellationToken);
        await PreviewAsync(token, operation).ConfigureAwait(true);
    }

    /// <summary>
    /// Applies the previewed import to the draft. It is applied to what was previewed: if the draft changed since the preview, it is
    /// refused and must be previewed again. Refused (with the reasons) when anything in the preview is refused.
    /// </summary>
    public async Task<CadImportOutcome?> ApplyAsync(CancellationToken cancellationToken = default)
    {
        if (InstanceId is not { } id || _file is null || _format is null || _profile is null)
            return null;

        if (_preview is not { CanApply: true, InstanceUpdatedAt: { } updatedAt })
        {
            Status = "Preview the import first; there is nothing to apply.";
            Raise();
            return null;
        }

        var (token, operation) = Begin(cancellationToken);
        SetBusy(true, "Applying…");
        var request = new CadImportRequest(_file, _format.Value, _profile, Settings(), updatedAt);
        _applying = true;
        CadImportOutcome outcome;

        try
        {
            outcome = await Task.Run(() => _service.ApplyCadImportAsync(id, request, CancellationToken.None), CancellationToken.None).ConfigureAwait(true);
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
            var done = $"Imported from {_file.FileName}. {outcome.Diff?.Summary}";
            await PreviewAsync(token, operation).ConfigureAwait(true);

            // The re-preview describes what could be applied next ("Ready to apply."); what the person needs to read now is what was done.
            Status = done;
            Summary = done;
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
        _cts?.Cancel();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(external);
        return (_cts.Token, ++_operation);
    }

    private void SetBusy(bool busy, string status)
    {
        IsBusy = busy;
        Status = status;
        Raise();
    }

    private void Reset(string status)
    {
        _preview = null;
        Formats = _file is null ? [] : Formats;
        _format = _file is null ? null : _format;
        Lines = [];
        ChangeLines = [];
        AfterLines = [];
        NoticeLines = [];
        Summary = string.Empty;
        IsBusy = false;
        Status = status;
        Raise();
    }

    private async Task PreviewAsync(CancellationToken token, int operation)
    {
        if (InstanceId is not { } id || _file is null || _format is null)
        {
            IsBusy = false;
            Raise();
            return;
        }

        if (_profile is null)
        {
            _preview = null;
            Lines = [];
            ChangeLines = [];
            AfterLines = [];
            NoticeLines = [];
            Summary = string.Empty;
            IsBusy = false;
            Status = Profiles.Count == 0
                ? "No mapping profile fits this calculation yet. Load a profile (JSON) that maps CAD names to its inputs."
                : "Choose a mapping profile.";
            Raise();
            return;
        }

        SetBusy(true, "Reading and mapping the file…");
        var request = new CadImportRequest(_file, _format.Value, _profile, Settings());
        CadImportPreview preview;

        try
        {
            // The parse is CPU-bound: it runs on the thread pool, under the token Cancel() trips, and the continuation returns to the UI thread.
            preview = await Task.Run(() => _service.PreviewCadImportAsync(id, request, token), CancellationToken.None).WaitAsync(token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            if (operation == _operation)
                SetBusy(false, "Cancelled.");

            return;
        }

        if (operation != _operation)
            return; // a newer read or preview superseded this one

        IsBusy = false;
        _preview = preview;
        Describe(preview);
        Raise();
    }

    private CadParseSettings Settings() => new()
    {
        Number = EffectiveNumber,
        CadVersion = CadVersion.Length == 0 ? null : CadVersion,
        PartName = PartName.Length == 0 ? null : PartName,
        Revision = Revision.Length == 0 ? null : Revision,
        Configuration = Configuration.Length == 0 ? null : Configuration,
        SheetName = SheetName.Length == 0 ? null : SheetName,
    };

    private void Describe(CadImportPreview preview)
    {
        if (preview.Refusal is not null)
        {
            Lines = [];
            ChangeLines = [];
            AfterLines = [];
            NoticeLines = [];
            Summary = string.Empty;
            Status = "That file was not read: " + preview.Refusal;
            return;
        }

        var mapping = preview.Mapping!;
        Lines = mapping.Rows.Select(Line).ToList();
        ChangeLines = preview.Diff is { FirstImport: false } diff
            ? diff.Changes.Select(c => c.Kind switch
            {
                CadChangeKind.Added => $"added   {c.Name}: {c.After}",
                CadChangeKind.Removed => $"removed {c.Name} (was {c.Before}){(c.AffectsInput ? "; its input keeps the last imported value" : string.Empty)}",
                _ => $"changed {c.Name}: {c.Before} → {c.After}",
            }).ToList()
            : [];
        AfterLines = [.. preview.BlocksAfter.Select(b => "block: " + b), .. preview.WarningsAfter.Select(w => "warning: " + w)];

        var notices = (preview.Parse?.Notices ?? []).Where(n => n.Severity == CalcNoticeSeverity.Warning).Select(n => "note: " + n.Message).ToList();
        NoticeLines = notices.Count > MaxNoticeLines ? [.. notices.Take(MaxNoticeLines), $"note: … and {notices.Count - MaxNoticeLines} more"] : notices;

        Summary = preview.CanApply
            ? $"{mapping.Count(CadRowStatus.Mapped)} mapped, {mapping.Count(CadRowStatus.Unmapped)} not in the profile, {mapping.Count(CadRowStatus.Expression)} read-only expression(s), {mapping.Count(CadRowStatus.Missing)} missing. {preview.Diff?.Summary}"
            : $"Nothing can be imported yet: {string.Join(" ", preview.Reasons.Take(3))}";
        Status = preview.CanApply ? "Ready to apply." : Summary;
    }

    private static CadPreviewLine Line(CadMappedRow row) => new(row.Status, row.Status switch
    {
        CadRowStatus.Mapped => $"mapped     {row.CadName} → {row.Target}: {row.Value}",
        CadRowStatus.Refused => $"refused    {row.CadName} ({row.Location}): {row.Message}",
        CadRowStatus.Unmapped => $"unmapped   {row.CadName}: {row.Value}",
        CadRowStatus.Expression => $"expression {row.CadName}: {row.Value} (read-only; never evaluated)",
        _ => $"missing    {row.CadName}: {row.Message}",
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
