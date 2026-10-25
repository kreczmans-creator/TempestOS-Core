using Tempest.Calc.Import;
using Tempest.Calc.Import.Cad;
using Tempest.Calc.Import.Mapping;
using Tempest.Calc.Library;

namespace Tempest.Desktop.CalcSheets;

/// <summary>One line of the import preview: how the row is classed and the words shown for it.</summary>
public sealed record CadPreviewLine(CadRowStatus Status, string Text);

/// <summary>
/// Addendum B, W1: the state behind the worksheet form's "Import CAD parameters…" action, with no UI in it. A CAD file is
/// read through the bounded reader (5 MB, strict UTF-8 or a safe workbook), a format and a stored mapping profile are picked,
/// and a preview says which names are mapped, refused, unmapped, read-only expressions or missing, and what changed since the
/// previous import; Apply then updates the open draft through <see cref="CalcInstanceService.ApplyCadImportAsync"/>. Nothing
/// is evaluated and nothing is applied while any row is refused.
/// </summary>
public sealed class CadImportViewModel
{
    private readonly CalcInstanceService _service;
    private ImportedFile? _file;
    private CadImportPreview? _preview;
    private CadSourceFormat? _format;
    private CadMappingProfile? _profile;
    private IReadOnlyList<CadMappingProfile> _allProfiles = [];
    private string _templateId = string.Empty;

    public CadImportViewModel(CalcInstanceService service) => _service = service ?? throw new ArgumentNullException(nameof(service));

    /// <summary>Raised whenever anything the panel shows has changed.</summary>
    public event Action? Changed;

    /// <summary>Raised after an import changed the draft, with the updated instance, so the open form can show it.</summary>
    public event Action<CalcInstance>? Applied;

    public Guid? InstanceId { get; private set; }

    public string InstanceName { get; private set; } = string.Empty;

    public string? FileName => _file?.FileName;

    public string? FileHash => _file?.Sha256;

    public IReadOnlyList<CadSourceFormat> Formats { get; private set; } = [];

    public IReadOnlyList<CadMappingProfile> Profiles { get; private set; } = [];

    public CadSourceFormat? Format => _format;

    public CadMappingProfile? Profile => _profile;

    /// <summary>The file's numbers use a decimal comma (the profile's own setting, when it has one, wins).</summary>
    public bool DecimalComma { get; private set; }

    public string CadVersion { get; private set; } = string.Empty;

    public string PartName { get; private set; } = string.Empty;

    public string Revision { get; private set; } = string.Empty;

    public string Configuration { get; private set; } = string.Empty;

    public string SheetName { get; private set; } = string.Empty;

    public IReadOnlyList<CadPreviewLine> Lines { get; private set; } = [];

    public IReadOnlyList<string> ChangeLines { get; private set; } = [];

    /// <summary>What the calculation would block or warn about once the import is applied.</summary>
    public IReadOnlyList<string> AfterLines { get; private set; } = [];

    public string Summary { get; private set; } = string.Empty;

    public string Status { get; private set; } = string.Empty;

    public bool CanApply => _preview is { CanApply: true };

    public bool IsOpen => InstanceId is not null;

    /// <summary>The last preview, for callers that want the detail (rows, parse, diff).</summary>
    public CadImportPreview? Preview => _preview;

    // ------------------------------------------------------------------

    /// <summary>Starts an import into a saved draft: forgets any earlier file and lists the profiles that fit its template.</summary>
    public async Task BeginAsync(CalcInstance draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
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
        Summary = string.Empty;
        Status = "Choose a CAD parameter file (NX .exp or .xlsx, Creo or SolidWorks text, a SolidWorks design table, Onshape or generic CSV or JSON).";
        await LoadProfilesAsync(cancellationToken).ConfigureAwait(false);
        Changed?.Invoke();
    }

    public void Close()
    {
        InstanceId = null;
        _file = null;
        _preview = null;
        Lines = [];
        ChangeLines = [];
        AfterLines = [];
        Changed?.Invoke();
    }

    public async Task LoadProfilesAsync(CancellationToken cancellationToken = default)
    {
        _allProfiles = await _service.ListImportProfilesAsync(cancellationToken).ConfigureAwait(false);
        Profiles = _allProfiles.Where(p => p.TemplateId is null || string.Equals(p.TemplateId, _templateId, StringComparison.Ordinal)).ToList();

        if (_profile is not null && Profiles.FirstOrDefault(p => p.Id == _profile.Id) is { } latest)
            _profile = latest;
        else if (_profile is not null)
            _profile = null;

        if (_profile is null && Profiles.Count == 1)
            _profile = Profiles[0];
    }

    /// <summary>Reads the chosen file through the bounded reader and previews it. A refused file is reported, not thrown.</summary>
    public async Task LoadFileAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        try
        {
            _file = await BoundedInputReader.ReadAsync(content, fileName, ImportLimits.Default, cancellationToken).ConfigureAwait(false);
        }
        catch (ImportRefusedException e)
        {
            _file = null;
            _preview = null;
            Formats = [];
            _format = null;
            Lines = [];
            ChangeLines = [];
            AfterLines = [];
            Summary = string.Empty;
            Status = "That file was not read: " + e.Message;
            Changed?.Invoke();
            return;
        }

        Formats = CadImporter.CandidatesFor(_file.FileName, _file);
        _format = Formats.Count > 0 ? Formats[0] : null;

        if (_format is null)
        {
            _preview = null;
            Status = $"A '{Path.GetExtension(_file.FileName)}' file is not one of the CAD parameter formats.";
            Changed?.Invoke();
            return;
        }

        await RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Stores a mapping profile typed or pasted as JSON (a new version of an existing id is a new profile version) and selects it.</summary>
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
            Changed?.Invoke();
            return e.Message;
        }

        var (saved, refusal) = await _service.SaveImportProfileAsync(profile, cancellationToken).ConfigureAwait(false);

        if (saved is null)
        {
            Status = refusal ?? "The profile was not saved.";
            Changed?.Invoke();
            return Status;
        }

        await LoadProfilesAsync(cancellationToken).ConfigureAwait(false);
        _profile = Profiles.FirstOrDefault(p => p.Id == saved.Id && p.Version == saved.Version) ?? saved;
        Status = $"Profile {saved.Label} saved.";
        await RefreshAsync(cancellationToken).ConfigureAwait(false);
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
        if (InstanceId is not { } id || _file is null || _format is null)
        {
            Changed?.Invoke();
            return;
        }

        if (_profile is null)
        {
            _preview = null;
            Lines = [];
            ChangeLines = [];
            AfterLines = [];
            Summary = string.Empty;
            Status = Profiles.Count == 0
                ? "No mapping profile fits this calculation yet. Load a profile (JSON) that maps CAD names to its inputs."
                : "Choose a mapping profile.";
            Changed?.Invoke();
            return;
        }

        _preview = await _service.PreviewCadImportAsync(id, new CadImportRequest(_file, _format.Value, _profile, Settings()), cancellationToken).ConfigureAwait(false);
        Describe(_preview);
        Changed?.Invoke();
    }

    /// <summary>Applies the previewed import to the draft. Refused (with the reasons) when anything in the preview is refused.</summary>
    public async Task<CadImportOutcome?> ApplyAsync(CancellationToken cancellationToken = default)
    {
        if (InstanceId is not { } id || _file is null || _format is null || _profile is null)
            return null;

        var outcome = await _service.ApplyCadImportAsync(id, new CadImportRequest(_file, _format.Value, _profile, Settings()), cancellationToken).ConfigureAwait(false);

        if (outcome.Action.Succeeded)
        {
            Status = $"Imported from {_file.FileName}. {outcome.Diff?.Summary}";
            _preview = await _service.PreviewCadImportAsync(id, new CadImportRequest(_file, _format.Value, _profile, Settings()), cancellationToken).ConfigureAwait(false);
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

    private CadParseSettings Settings() => new()
    {
        Number = DecimalComma ? NumberProfile.Comma : NumberProfile.Point,
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
}
