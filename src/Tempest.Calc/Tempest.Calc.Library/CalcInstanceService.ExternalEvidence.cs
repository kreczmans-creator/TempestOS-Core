using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>
/// Addendum A §A6 S6: a design done elsewhere (a Hilti PROFIS anchorage report, a manufacturer's selection) is filed
/// as an attachment of the calculation's own Evidence, and the input that relies on it cites the attachment.
/// </summary>
public sealed partial class CalcInstanceService
{
    /// <summary>Largest external report accepted, in bytes.</summary>
    public const int MaxExternalEvidenceBytes = 25 * 1024 * 1024;

    /// <summary>
    /// Attaches <paramref name="content"/> to a draft calculation's Evidence and, when <paramref name="inputSymbol"/> names
    /// a text input, records the file name there with the attachment as its source.
    /// </summary>
    public async Task<CalcActionResult> AttachExternalEvidenceAsync(Guid instanceId, string fileName, string contentType, ReadOnlyMemory<byte> content, string? inputSymbol = null, CancellationToken cancellationToken = default)
    {
        var instance = await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);

        if (instance is null)
            return CalcActionResult.Refused($"No calculation '{instanceId}' exists.");

        if (FeatureRefusal(instance.TemplateReference) is { } switchedOff)
            return CalcActionResult.Refused(switchedOff, instance);

        if (instance.Status != CalcInstanceStatus.Draft)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' is {instance.Status}; external evidence is filed while it is a draft.", instance);

        // A member of a checked pack is frozen like its inputs: nothing is added to its Evidence after the pack was checked (Red R-A-10).
        if (instance.PackId is { } packId && await _store.FindPackAsync(packId, cancellationToken).ConfigureAwait(false) is { Status: not CalcInstanceStatus.Draft } pack)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' belongs to pack '{pack.Title}', which is {pack.Status}; its Evidence is frozen with it.", instance);

        // The citing input is checked before the file is attached, so a refusal leaves no orphan attachment.
        if (inputSymbol is not null && _catalogue.Find(instance.TemplateReference)?.Template.FindInput(inputSymbol) is not { Kind: CalcInputKind.Text })
            return CalcActionResult.Refused($"'{inputSymbol}' is not a text input of '{instance.Name}'; the report was not attached.", instance);

        // Windows paths arrive on every platform; keep the last segment either way so no directory part is stored.
        var name = CalcSheetTextRenderer.Clean(Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/')));

        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return CalcActionResult.Refused("The report needs a file name of at most 200 characters.", instance);

        if (content.Length == 0 || content.Length > MaxExternalEvidenceBytes)
            return CalcActionResult.Refused($"The report must hold between 1 byte and {MaxExternalEvidenceBytes / (1024 * 1024)} MB.", instance);

        if (instance.EvidenceId is not { } evidenceId || await FindEvidenceAsync(evidenceId, cancellationToken).ConfigureAwait(false) is not { } evidence)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' has no Evidence to file the report against.", instance);

        var attachment = await evidence.AttachContentAsync(name, string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType, content, cancellationToken).ConfigureAwait(false);

        if (inputSymbol is null)
            return CalcActionResult.Ok(instance);

        var inputs = instance.Inputs.Where(i => !string.Equals(i.Symbol, inputSymbol, StringComparison.Ordinal))
            .Append(new CalcInputValue(inputSymbol, Text: name, Provenance: CalcProvenance.FromEvidence($"{evidenceId:N}/{attachment.Id:N}")))
            .ToList();
        return await UpdateInputsAsync(instanceId, inputs, instance.UpdatedAt, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The first input (or table cell) whose "evidence" source does not point at a file attached to this calculation's Evidence, as one
    /// plain sentence, or <see langword="null"/>. An evidence source is <c>&lt;evidence id&gt;/&lt;attachment id&gt;</c>; typed text, another
    /// calculation's attachment or an attachment that is not there is not evidence (Purple P-A-02). A revision keeps citing the files its
    /// earlier revisions filed, so the Evidence of every earlier revision of the same calculation counts as its own.
    /// </summary>
    private async Task<string?> UnresolvedEvidenceAsync(Guid? evidenceId, Guid? predecessorId, IEnumerable<CalcInputValue> inputs, CancellationToken cancellationToken)
    {
        var cited = new List<(string Where, CalcProvenance Provenance)>();

        foreach (var input in inputs)
        {
            if (input.Provenance is { Kind: CalcProvenanceKind.Evidence } direct)
                cited.Add((input.Symbol, direct));

            for (var r = 0; r < (input.Rows?.Count ?? 0); r++)
            {
                foreach (var cell in input.Rows![r]?.Cells ?? new Dictionary<string, CalcCell>())
                {
                    if (cell.Value?.Provenance is { Kind: CalcProvenanceKind.Evidence } inCell)
                        cited.Add(($"{input.Symbol}[{r + 1}].{cell.Key}", inCell));
                }
            }
        }

        if (cited.Count == 0)
            return null;

        // The Evidence records that belong to this calculation: its own and those of the revisions before it.
        var own = new List<Guid>();

        if (evidenceId is { } current)
            own.Add(current);

        var seen = new HashSet<Guid>();

        for (var previous = predecessorId; previous is { } id && seen.Add(id) && seen.Count <= 64;)
        {
            var instance = await _store.FindInstanceAsync(id, cancellationToken).ConfigureAwait(false);

            if (instance is null)
                break;

            if (instance.EvidenceId is { } earlier)
                own.Add(earlier);

            previous = instance.PredecessorId;
        }

        foreach (var (where, provenance) in cited)
        {
            var parts = (provenance.Reference ?? string.Empty).Split('/');

            if (parts.Length == 2 && Guid.TryParse(parts[0], out var citedEvidence) && Guid.TryParse(parts[1], out var attachmentId) && own.Contains(citedEvidence)
                && await FindEvidenceAsync(citedEvidence, cancellationToken).ConfigureAwait(false) is { } evidence
                && (await evidence.GetAttachmentsAsync(cancellationToken).ConfigureAwait(false)).Any(a => a.Id == attachmentId))
                continue;

            return $"{where}: the source 'evidence' must point at a file attached to this calculation's Evidence; attach the report to the calculation (attach external report) or choose another source.";
        }

        return null;
    }
}
