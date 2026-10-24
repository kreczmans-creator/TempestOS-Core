namespace Tempest.Calc.Sheets;

/// <summary>
/// One input bound to an output of another calculation (Addendum A, L1): the upstream instance, the output symbol and
/// the revision it is pinned to. Read from a <see cref="CalcProvenanceKind.Link"/> provenance, whose reference is
/// <c>calc:&lt;id&gt;#&lt;symbol&gt;</c> and whose revision is the pinned revision.
/// </summary>
public sealed record CalcLink(Guid UpstreamId, string OutputSymbol, string Revision)
{
    public const string Prefix = "calc:";

    /// <summary>The link a provenance states, or <see langword="null"/> when it is not a well-formed link.</summary>
    public static CalcLink? From(CalcProvenance? provenance)
    {
        if (provenance is not { Kind: CalcProvenanceKind.Link, Reference: { } reference } || !reference.StartsWith(Prefix, StringComparison.Ordinal))
            return null;

        var hash = reference.IndexOf('#', StringComparison.Ordinal);

        if (hash < 0 || hash == reference.Length - 1 || !Guid.TryParse(reference[Prefix.Length..hash], out var id) || id == Guid.Empty)
            return null;

        return new CalcLink(id, reference[(hash + 1)..], provenance.Revision ?? string.Empty);
    }
}

/// <summary>
/// An output a calculator offers for linking: its symbol, its unit (which fixes the dimension a bound input must
/// share) and what it is (Addendum A, L1). Dimensionless outputs carry an empty unit.
/// </summary>
public sealed record CalcOutputPort(string Symbol, string Unit, string Description);

/// <summary>
/// A kind of reference record a template draws on (Addendum A, §A3): the inputs a record of that kind fills, and what
/// the user should take them from. Tabulated values (ampacities, correction factors, X/Y factors, annex factor sets)
/// come from records, which carry their source, edition and licence.
/// </summary>
public sealed record CalcRecordKind(string Kind, string Title, IReadOnlyList<string> Symbols, string Guidance);
