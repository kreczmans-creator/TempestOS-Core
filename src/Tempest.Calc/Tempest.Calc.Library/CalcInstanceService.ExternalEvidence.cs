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
}
