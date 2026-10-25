using Tempest.Calc.Import;
using Tempest.Calc.Import.Scan;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>One scan import to preview or apply: the untrusted files as read (each with the stage and serial the person entered), the scan profile and the person's settings.</summary>
public sealed record ScanImportRequest(IReadOnlyList<ScanFileInput> Files, ScanProfile Profile, ScanSettings? Settings = null);

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
    string? Refusal)
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
            amend: updated => updated with { ScanImports = ReplaceScanRecord(updated.ScanImports, record) }).ConfigureAwait(false);

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
        var previous = instance.ScanImports?.FirstOrDefault(r => string.Equals(r.Key, key, StringComparison.Ordinal));
        var diff = ScanImportDiff.Compute(previous, mapping.Snapshot);

        IReadOnlyList<string> blocks = [];
        IReadOnlyList<string> warnings = [];

        if (mapping.CanApply)
        {
            var run = Preview(instance.TemplateReference, new CalcInputSet(mapping.Inputs), cancellationToken);
            blocks = run.Blocks.Select(b => b.Message).ToList();
            warnings = run.Warnings.Select(w => w.Message).ToList();
        }

        return new ScanImportPreview(analysis, mapping, diff, blocks, warnings, null);
    }

    private static IReadOnlyList<ScanImportRecord> ReplaceScanRecord(IReadOnlyList<ScanImportRecord>? existing, ScanImportRecord record)
    {
        const int MaxRecords = 20;
        var list = (existing ?? []).Where(r => !string.Equals(r.Key, record.Key, StringComparison.Ordinal)).ToList();
        list.Add(record);
        return list.Count > MaxRecords ? list.Skip(list.Count - MaxRecords).ToList() : list;
    }

    // ------------------------------------------------------------------
    // Scan provenance is a claim; it is checked (Security review of W1, M2)
    // ------------------------------------------------------------------

    private sealed record ScanClaim(double? Number, string? Unit, string? Text, CalcProvenance Provenance);

    /// <summary>
    /// A value that claims <see cref="CalcProvenanceKind.ScanImport"/> provenance is accepted only if it is unchanged from the instance it
    /// came from, or it matches what one of the instance's scan import records says that import wrote (same profile and version, same file hash,
    /// same place, same number and unit). A value edited after import, a provenance chosen by hand, or one typed into an imported file or CSV
    /// is refused with the reason; to override a fitted value, give it another source (manual, assumption ...).
    /// </summary>
    private static string? ScanProvenanceProblem(CalcInstance? before, CalcInstance after)
    {
        var claims = ScanClaims(after.Inputs);

        if (claims.Count == 0)
            return null;

        var earlier = before is null ? new Dictionary<string, ScanClaim>() : ScanClaims(before.Inputs);

        foreach (var (path, claim) in claims)
        {
            if (earlier.TryGetValue(path, out var old) && SameClaim(old, claim))
                continue;

            var info = ScanImportProvenance.TryParse(claim.Provenance);

            if (info is null)
                return $"'{path}' claims a scan import as its source, but the claim is not well formed. Choose another source, or import the scan again.";

            var matched = (after.ScanImports ?? []).Any(r =>
                string.Equals(r.ProfileId, info.ProfileId, StringComparison.Ordinal) && r.ProfileVersion == info.ProfileVersion
                && r.Features.Any(f => string.Equals(f.SetSha256, info.Sha256, StringComparison.Ordinal)
                    && (path.EndsWith("#table", StringComparison.Ordinal) || (f.Pushed ?? []).Any(v => string.Equals(v.Path, path, StringComparison.Ordinal) && SameValue(v, claim)))));

            if (!matched)
                return $"'{path}' claims to be fitted from a scan (file {info.Sha256[..12]}..., profile {info.ProfileId}@{info.ProfileVersion}), but its value is not what that import wrote. Import the scan again, or give the value another source such as manual or assumption.";
        }

        return null;
    }

    private static Dictionary<string, ScanClaim> ScanClaims(IEnumerable<CalcInputValue> inputs)
    {
        var claims = new Dictionary<string, ScanClaim>(StringComparer.Ordinal);

        foreach (var input in inputs)
        {
            if (input.Provenance is { Kind: CalcProvenanceKind.ScanImport } p)
                claims[input.Rows is null ? input.Symbol : input.Symbol + "#table"] = new ScanClaim(input.Number, input.Unit, input.Text, p);

            if (input.Rows is null)
                continue;

            for (var i = 0; i < input.Rows.Count; i++)
            {
                var row = input.Rows[i];
                var key = row?.Get("dim")?.Text is { Length: > 0 } dim ? $"{input.Symbol}{{{dim}}}" : $"{input.Symbol}[{i}]";

                foreach (var (column, cell) in row?.Cells ?? new Dictionary<string, CalcCell>())
                {
                    if (cell?.Provenance is { Kind: CalcProvenanceKind.ScanImport } cp)
                        claims[$"{key}.{column}"] = new ScanClaim(cell.Number, cell.Unit, cell.Text, cp);
                }
            }
        }

        return claims;
    }

    private static bool SameClaim(ScanClaim a, ScanClaim b) =>
        a.Provenance == b.Provenance && string.Equals(a.Unit, b.Unit, StringComparison.Ordinal) && string.Equals(a.Text, b.Text, StringComparison.Ordinal) && SameNumber(a.Number, b.Number);

    private static bool SameValue(ScanPushedValue pushed, ScanClaim claim) =>
        string.Equals(pushed.Unit, claim.Unit, StringComparison.Ordinal) && string.Equals(pushed.Text, claim.Text, StringComparison.Ordinal) && SameNumber(pushed.Number, claim.Number);

    private static bool SameNumber(double? x, double? y)
    {
        if (x is null || y is null)
            return x is null && y is null;

        return x == y || Math.Abs(x.Value - y.Value) <= 1e-12 * Math.Max(Math.Abs(x.Value), Math.Abs(y.Value));
    }
}
