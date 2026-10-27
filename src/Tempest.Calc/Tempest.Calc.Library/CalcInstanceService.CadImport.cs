using Tempest.Calc.Import;
using Tempest.Calc.Import.Cad;
using Tempest.Calc.Import.Mapping;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>One CAD import to preview or apply: the untrusted file as read, its format, the mapping profile and the person's settings.</summary>
/// <param name="ExpectedUpdatedAt">
/// The draft's update time when the preview was taken. Apply is refused when the draft changed since, so what is applied is what was
/// previewed and not what a change made in between would map to.
/// </param>
public sealed record CadImportRequest(ImportedFile File, CadSourceFormat Format, CadMappingProfile Profile, CadParseSettings? Settings = null, DateTimeOffset? ExpectedUpdatedAt = null);

/// <summary>
/// What an import would do to a draft, without doing it: the parse (or why the file was refused), every row mapped, the
/// differences from the previous import of the same source, and what the calculation would block or warn about afterwards.
/// </summary>
public sealed record CadImportPreview(
    CadParseResult? Parse,
    CadMappingResult? Mapping,
    CadImportDiff? Diff,
    IReadOnlyList<string> BlocksAfter,
    IReadOnlyList<string> WarningsAfter,
    string? Refusal,
    DateTimeOffset? InstanceUpdatedAt = null)
{
    /// <summary>Whether Apply would proceed.</summary>
    public bool CanApply => Refusal is null && Mapping is { CanApply: true };

    /// <summary>The reasons Apply is refused, in words.</summary>
    public IReadOnlyList<string> Reasons => Refusal is not null ? [Refusal] : Mapping?.Refusals ?? [];
}

/// <summary>The result of applying an import: the action outcome, the preview it was applied from and the diff against the previous import.</summary>
public sealed record CadImportOutcome(CalcActionResult Action, CadImportPreview? Preview, CadImportDiff? Diff);

public sealed partial class CalcInstanceService
{
    // ------------------------------------------------------------------
    // CAD parameter import (Addendum B, W1)
    // ------------------------------------------------------------------

    /// <summary>
    /// Shows what <paramref name="request"/> would do to a draft, changing nothing. A file the importer refuses is reported
    /// in the preview (<see cref="CadImportPreview.Refusal"/>), not thrown. The caller's cancellation is honoured.
    /// </summary>
    public async Task<CadImportPreview> PreviewCadImportAsync(Guid instanceId, CadImportRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var instance = await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);

        if (instance is null)
            return new CadImportPreview(null, null, null, [], [], $"No calculation '{instanceId}' exists.");

        return BuildCadImportPreview(instance, request, _time.GetUtcNow(), cancellationToken);
    }

    /// <summary>
    /// Applies a CAD import to a draft: the mapped values replace the draft's inputs (each carrying its
    /// <see cref="CalcProvenanceKind.CadImport"/> provenance), the import is remembered on the draft, and the draft is
    /// recalculated through the ordinary input update — so a calculation linked to this one goes stale through the existing
    /// staleness machinery when its outputs move, and a re-import of a changed file reports the added, removed and changed
    /// values. Nothing is applied while anything is refused. A checked, issued or superseded calculation is never changed in
    /// place: revise it first.
    /// </summary>
    public async Task<CadImportOutcome> ApplyCadImportAsync(Guid instanceId, CadImportRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var instance = await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);

        if (instance is null)
            return new CadImportOutcome(CalcActionResult.Refused($"No calculation '{instanceId}' exists."), null, null);

        if (instance.Status != CalcInstanceStatus.Draft)
            return new CadImportOutcome(CalcActionResult.Refused($"Calculation '{instance.Name}' is {instance.Status}; only a draft takes new inputs. Revise it first, then import.", instance), null, null);

        if (request.ExpectedUpdatedAt is { } expected && expected != instance.UpdatedAt)
            return new CadImportOutcome(CalcActionResult.Refused($"Calculation '{instance.Name}' was changed after this import was previewed; preview it again before applying.", instance), null, null);

        var now = _time.GetUtcNow();
        var preview = BuildCadImportPreview(instance, request, now, cancellationToken);

        if (!preview.CanApply)
            return new CadImportOutcome(CalcActionResult.Refused("Nothing was imported: " + string.Join(" ", preview.Reasons.Take(5)), instance), preview, preview.Diff);

        var parse = preview.Parse!;
        var mapping = preview.Mapping!;
        var record = new CadImportRecord(
            CadImportRecord.KeyFor(parse.Source.System, request.Profile.Id, parse.Source.ExplicitPartName),
            parse.Source.FileName, parse.Source.Sha256, parse.Source.System, parse.Source.Format,
            parse.Source.CadVersion, parse.Source.PartName, parse.Source.PartRevision, parse.Source.Configuration,
            request.Profile.Id, request.Profile.Version, now, CurrentPrincipal(), mapping.Snapshot);

        var result = await UpdateInputsCoreAsync(
            instanceId, mapping.Inputs, instance.UpdatedAt, repin: false, cancellationToken,
            amend: updated => updated with { CadImports = CadImportVerifier.Merge(updated.CadImports, record, updated.Inputs) }).ConfigureAwait(false);

        return new CadImportOutcome(result, preview, preview.Diff);
    }

    /// <summary>The imports a calculation remembers (latest per source).</summary>
    public async Task<IReadOnlyList<CadImportRecord>> CadImportsAsync(Guid instanceId, CancellationToken cancellationToken = default) =>
        (await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false))?.CadImports ?? [];

    /// <summary>Saves a mapping profile version. A profile that does not validate is refused; a stored version is never overwritten.</summary>
    public async Task<(CadMappingProfile? Profile, string? Refusal)> SaveImportProfileAsync(CadMappingProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.Validate() is { Count: > 0 } problems)
            return (null, "The profile cannot be saved: " + string.Join(" ", problems));

        try
        {
            await _store.SaveImportProfileAsync(profile, cancellationToken).ConfigureAwait(false);
        }
        catch (CalcInstanceImmutableException ex)
        {
            return (null, ex.Message);
        }

        return (profile, null);
    }

    /// <summary>A mapping profile at a version, or its latest.</summary>
    public Task<CadMappingProfile?> FindImportProfileAsync(string id, int? version = null, CancellationToken cancellationToken = default) =>
        _store.FindImportProfileAsync(id, version, cancellationToken);

    /// <summary>The latest version of every mapping profile.</summary>
    public Task<IReadOnlyList<CadMappingProfile>> ListImportProfilesAsync(CancellationToken cancellationToken = default) =>
        _store.ListImportProfilesAsync(cancellationToken);

    // ------------------------------------------------------------------

    private CadImportPreview BuildCadImportPreview(CalcInstance instance, CadImportRequest request, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var entry = _catalogue.Find(instance.TemplateReference);

        if (entry is null)
            return new CadImportPreview(null, null, null, [], [], OlderVersionRefusal(instance));

        var settings = request.Settings ?? new CadParseSettings();

        if (request.Profile.Number is { } number)
            settings = settings with { Number = number };

        CadParseResult parse;

        try
        {
            parse = CadImporter.Parse(request.File, request.Format, settings, cancellationToken);
        }
        catch (ImportRefusedException ex)
        {
            return new CadImportPreview(null, null, null, [], [], ex.Message);
        }

        var mapping = CadMapper.Map(parse, request.Profile, entry.Template, instance.Inputs, at);
        var key = CadImportRecord.KeyFor(parse.Source.System, request.Profile.Id, parse.Source.ExplicitPartName);
        var previous = CadImportVerifier.Latest(instance.CadImports, key);
        var diff = CadImportDiff.Compute(previous, mapping.Snapshot);

        IReadOnlyList<string> blocks = [];
        IReadOnlyList<string> warnings = [];

        if (mapping.CanApply)
        {
            var run = Preview(instance.TemplateReference, new CalcInputSet(mapping.Inputs), cancellationToken);
            blocks = run.Blocks.Select(b => b.Message).ToList();
            warnings = run.Warnings.Select(w => w.Message).ToList();
        }

        return new CadImportPreview(parse, mapping, diff, blocks, warnings, null, instance.UpdatedAt);
    }
}
