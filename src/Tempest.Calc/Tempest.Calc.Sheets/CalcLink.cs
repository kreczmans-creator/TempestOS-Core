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
/// <param name="MeaningChangedAt">
/// The template version at which this port's symbol (and public name) came to mean something else (an axis swapped: <c>W_el_y</c> minor → major),
/// with <paramref name="MeaningWas"/> and <paramref name="MeaningNow"/> saying what in plain words. A link or a pack binding that names the symbol
/// or the public name on a calculation made before that version is refused rather than rebound (ADR-0177 review); a link under an alias keeps
/// resolving, since the alias is declared precisely because that old symbol is this port.
/// </param>
/// <param name="MeaningWas">What the symbol meant before <paramref name="MeaningChangedAt"/>.</param>
/// <param name="MeaningNow">What it means from <paramref name="MeaningChangedAt"/> on.</param>
public sealed record CalcOutputPort(string Symbol, string Unit, string Description, string? Name = null, IReadOnlyList<string>? Aliases = null, string? MeaningChangedAt = null, string? MeaningWas = null, string? MeaningNow = null)
{
    /// <summary>Whether this port declares that its meaning changed at a version (<see cref="MeaningChangedAt"/>).</summary>
    public bool HasMeaningChange => MeaningChangedAt is { Length: > 0 };

    /// <summary>
    /// Whether <paramref name="nameOrSymbol"/>, on a calculation made on template version <paramref name="version"/>, names a value that meant
    /// something else: the name is the port's symbol or public name (an alias keeps its meaning), the port declares a change, and the version is
    /// older than it.
    /// </summary>
    public bool MeaningChangedSince(string nameOrSymbol, string version) =>
        HasMeaningChange
        && (string.Equals(Symbol, nameOrSymbol, StringComparison.Ordinal) || (Name is { Length: > 0 } && string.Equals(Name, nameOrSymbol, StringComparison.Ordinal)))
        && CalcTemplateVersion.Compare(version, MeaningChangedAt!) < 0;

    /// <summary>"W_el_y meant … on id@1.1.0; since id@1.2.0 it means …", for the sentence that refuses the link or binding; names the port's symbol whatever name the link used.</summary>
    public string MeaningChangeText(string templateId, string oldReference) =>
        CalcTemplateVersion.MeaningChangeText(Symbol, templateId, oldReference, MeaningChangedAt, MeaningWas, MeaningNow);

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
