namespace Tempest.Calc.Sheets;

/// <summary>
/// The pin an input's provenance carries when its value comes from a reference record: <c>record:&lt;id&gt;@&lt;revision&gt;</c>,
/// optionally followed by a space and the table or clause. Only a pin that parses counts as one.
/// </summary>
public static class CalcRecordPin
{
    public const string Prefix = "record:";

    /// <summary>The record and revision a provenance pins, or <see langword="null"/> when it is not a well-formed pin.</summary>
    public static (Guid Id, int Revision)? From(CalcProvenance? provenance)
    {
        if (provenance is not { Kind: CalcProvenanceKind.Reference, Reference: { } reference } || !reference.StartsWith(Prefix, StringComparison.Ordinal))
            return null;

        var body = reference[Prefix.Length..].Split(' ', 2)[0];
        var at = body.IndexOf('@', StringComparison.Ordinal);

        return at > 0
            && Guid.TryParse(body[..at], out var id) && id != Guid.Empty
            && int.TryParse(body[(at + 1)..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var revision) && revision > 0
            ? (id, revision)
            : null;
    }
}
