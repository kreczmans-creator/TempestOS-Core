using Tempest.Calc.Import;
using Tempest.Calc.Import.Scan;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>One scan import to preview or apply: the untrusted files as read (each with the stage and serial the person entered), the scan profile and the person's settings.</summary>
/// <param name="ExpectedUpdatedAt">
/// The draft's update time when the preview was taken. Apply is refused when the draft changed since, so what is applied is what was previewed.
/// </param>
public sealed record ScanImportRequest(IReadOnlyList<ScanFileInput> Files, ScanProfile Profile, ScanSettings? Settings = null, DateTimeOffset? ExpectedUpdatedAt = null);

/// <summary>
/// What a scan import would do to a draft, without doing it: the analysis (or why the files were refused) with every feature's statistics, every feature
/// mapped, the differences from the previous import through the same profile, and what the calculation would block or warn about afterwards.
/// </summary>
public sealed record ScanImportPreview(
    ScanAnalysis? Analysis,
    ScanMappingResult? Mapping,
    ScanImportDiff? Diff,
    IReadOnlyList<string> BlocksAfter,
    IReadOnlyList<string> WarningsAfter,
    string? Refusal,
    DateTimeOffset? InstanceUpdatedAt = null)
{
    /// <summary>Whether Apply would proceed.</summary>
    public bool CanApply => Refusal is null && Mapping is { CanApply: true };

    /// <summary>The statistics of every feature at every stage (empty when the files were refused).</summary>
    public IReadOnlyList<ScanFeatureStats> Stats => Analysis?.Stats ?? [];

    /// <summary>The reasons Apply is refused, in words.</summary>
    public IReadOnlyList<string> Reasons => Refusal is not null ? [Refusal] : Mapping?.Refusals ?? [];
}

/// <summary>The result of applying a scan import: the action outcome, the preview it was applied from and the diff against the previous import.</summary>
public sealed record ScanImportOutcome(CalcActionResult Action, ScanImportPreview? Preview, ScanImportDiff? Diff);

public sealed partial class CalcInstanceService
{
    // ------------------------------------------------------------------
    // Scan deviation import (Addendum B, W2)
    // ------------------------------------------------------------------

    /// <summary>
    /// Shows what <paramref name="request"/> would do to a draft, changing nothing. Files the importer refuses are reported in the preview
    /// (<see cref="ScanImportPreview.Refusal"/>), not thrown. The caller's cancellation is honoured.
    /// </summary>
    public async Task<ScanImportPreview> PreviewScanImportAsync(Guid instanceId, ScanImportRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var instance = await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);

        if (instance is null)
            return new ScanImportPreview(null, null, null, [], [], $"No calculation '{instanceId}' exists.");

        return BuildScanImportPreview(instance, request, _time.GetUtcNow(), cancellationToken);
    }

    /// <summary>
    /// Applies a scan import to a draft: the fitted distributions replace the mapped inputs (each cell carrying its
    /// <see cref="CalcProvenanceKind.ScanImport"/> provenance), the import is remembered on the draft, and the draft is recalculated through the
    /// ordinary input update — so a calculation linked to this one goes stale through the existing staleness machinery when its outputs move, and a
    /// re-import of changed tables reports the added, removed and changed features. Nothing is applied while anything is refused. A checked, issued or
    /// superseded calculation is never changed in place: revise it first.
    /// </summary>
    public async Task<ScanImportOutcome> ApplyScanImportAsync(Guid instanceId, ScanImportRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var instance = await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);

        if (instance is null)
            return new ScanImportOutcome(CalcActionResult.Refused($"No calculation '{instanceId}' exists."), null, null);

        if (instance.Status != CalcInstanceStatus.Draft)
            return new ScanImportOutcome(CalcActionResult.Refused($"Calculation '{instance.Name}' is {instance.Status}; only a draft takes new inputs. Revise it first, then import.", instance), null, null);

        if (request.ExpectedUpdatedAt is { } expected && expected != instance.UpdatedAt)
            return new ScanImportOutcome(CalcActionResult.Refused($"Calculation '{instance.Name}' was changed after this import was previewed; preview it again before applying.", instance), null, null);

        var now = _time.GetUtcNow();
        var preview = BuildScanImportPreview(instance, request, now, cancellationToken);

        if (!preview.CanApply)
            return new ScanImportOutcome(CalcActionResult.Refused("Nothing was imported: " + string.Join(" ", preview.Reasons.Take(5)), instance), preview, preview.Diff);

        var analysis = preview.Analysis!;
        var mapping = preview.Mapping!;
        var settings = analysis.Settings;
        var record = new ScanImportRecord(
            ScanImportRecord.KeyFor(request.Profile.Id), request.Profile.Id, request.Profile.Version, request.Profile.SoftwareLabel, settings.SoftwareVersion, settings.Alignment,
            analysis.Data.Files.Select(f => new ScanImportFile(f.FileName, f.Sha256, f.Stage, f.Serial)).ToList(), now, CurrentPrincipal(), mapping.Snapshot);

        var result = await UpdateInputsCoreAsync(
            instanceId, mapping.Inputs, instance.UpdatedAt, repin: false, cancellationToken,
            amend: updated => updated with { ScanImports = ScanImportVerifier.Merge(updated.ScanImports, record, updated.Inputs) }).ConfigureAwait(false);

        return new ScanImportOutcome(result, preview, preview.Diff);
    }

    /// <summary>The scan imports a calculation remembers (latest per profile).</summary>
    public async Task<IReadOnlyList<ScanImportRecord>> ScanImportsAsync(Guid instanceId, CancellationToken cancellationToken = default) =>
        (await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false))?.ScanImports ?? [];

    /// <summary>Saves a scan profile version. A profile that does not validate is refused; a stored version is never overwritten.</summary>
    public async Task<(ScanProfile? Profile, string? Refusal)> SaveScanProfileAsync(ScanProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.Validate() is { Count: > 0 } problems)
            return (null, "The profile cannot be saved: " + string.Join(" ", problems));

        try
        {
            await _store.SaveScanProfileAsync(profile, cancellationToken).ConfigureAwait(false);
        }
        catch (CalcInstanceImmutableException ex)
        {
            return (null, ex.Message);
        }

        return (profile, null);
    }

    /// <summary>A scan profile at a version, or its latest.</summary>
    public Task<ScanProfile?> FindScanProfileAsync(string id, int? version = null, CancellationToken cancellationToken = default) =>
        _store.FindScanProfileAsync(id, version, cancellationToken);

    /// <summary>The latest version of every scan profile.</summary>
    public Task<IReadOnlyList<ScanProfile>> ListScanProfilesAsync(CancellationToken cancellationToken = default) =>
        _store.ListScanProfilesAsync(cancellationToken);

    // ------------------------------------------------------------------

    private ScanImportPreview BuildScanImportPreview(CalcInstance instance, ScanImportRequest request, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var entry = _catalogue.Find(instance.TemplateReference);

        if (entry is null)
            return new ScanImportPreview(null, null, null, [], [], $"Template '{instance.TemplateReference}' is no longer registered.");

        ScanAnalysis analysis;

        try
        {
            analysis = ScanImporter.Analyse(request.Files, request.Profile, request.Settings, cancellationToken);
        }
        catch (ImportRefusedException ex)
        {
            return new ScanImportPreview(null, null, null, [], [], ex.Message);
        }

        var mapping = ScanMapper.Map(analysis, entry.Template, instance.Inputs, at);
        var key = ScanImportRecord.KeyFor(request.Profile.Id);
        var previous = ScanImportVerifier.Latest(instance.ScanImports, key);
        var diff = ScanImportDiff.Compute(previous, mapping.Snapshot);

        IReadOnlyList<string> blocks = [];
        IReadOnlyList<string> warnings = [];

        if (mapping.CanApply)
        {
            var run = Preview(instance.TemplateReference, new CalcInputSet(mapping.Inputs), cancellationToken);
            blocks = run.Blocks.Select(b => b.Message).ToList();
            warnings = run.Warnings.Select(w => w.Message).ToList();
        }

        return new ScanImportPreview(analysis, mapping, diff, blocks, warnings, null, instance.UpdatedAt);
    }

}
