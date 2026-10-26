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
/// <param name="Symbol">The output symbol of a run: what a saved link and an instance's stored outputs are keyed by.</param>
/// <param name="Unit">The unit it is offered in; empty for a plain number.</param>
/// <param name="Description">What it is.</param>
/// <param name="Name">
/// The public port name, <c>&lt;area&gt;.&lt;quantity&gt;</c> (C-2, row 43): what a link, a pack binding and the port list show. A port with no
/// name is known by its <paramref name="Symbol"/> alone.
/// </param>
/// <param name="Aliases">
/// Further names that still resolve to this port: a symbol the output had before it was renamed. A link saved under an old symbol keeps
/// resolving; nothing writes an alias.
/// </param>
public sealed record CalcOutputPort(string Symbol, string Unit, string Description, string? Name = null, IReadOnlyList<string>? Aliases = null)
{
    /// <summary>The name a port is listed under: its public <see cref="Name"/>, or its <see cref="Symbol"/> until it has one.</summary>
    public string PublicName => Name is { Length: > 0 } ? Name : Symbol;

    /// <summary>Whether <paramref name="nameOrSymbol"/> names this port: its public name, its output symbol or an alias.</summary>
    public bool Matches(string nameOrSymbol) =>
        string.Equals(Symbol, nameOrSymbol, StringComparison.Ordinal)
        || (Name is { Length: > 0 } && string.Equals(Name, nameOrSymbol, StringComparison.Ordinal))
        || (Aliases is { Count: > 0 } && Aliases.Contains(nameOrSymbol, StringComparer.Ordinal));
}

/// <summary>
/// A kind of reference record a template draws on (Addendum A, §A3): the inputs a record of that kind fills, and what
/// the user should take them from. Tabulated values (ampacities, correction factors, X/Y factors, annex factor sets)
/// come from records, which carry their source, edition and licence.
/// </summary>
public sealed record CalcRecordKind(string Kind, string Title, IReadOnlyList<string> Symbols, string Guidance);
