using Tempest.Core.ReferenceData;

namespace Tempest.Core.CommercialIntelligence.Suppliers;

/// <summary>Why two supplier records look like they might be the same company.</summary>
public enum IdentityMatchBasis
{
    /// <summary>Their registration numbers are the same. The only basis this platform treats as conclusive.</summary>
    RegistrationNumber,

    /// <summary>Their legal names are the same.</summary>
    LegalName,

    /// <summary>One's legal name appears among the other's aliases, or the reverse.</summary>
    AliasOverlap,

    /// <summary>Their names differ only in punctuation, spacing, case or a company suffix.</summary>
    NormalisedName
}

/// <summary>
/// One supplier record that might be the same company as another.
/// </summary>
/// <param name="Reference">The candidate supplier's reference.</param>
/// <param name="LegalName">The candidate's legal name, so a person can judge without a second lookup.</param>
/// <param name="Basis">Why the two look alike.</param>
/// <param name="IsConclusive">Whether the basis settles the question on its own.</param>
public sealed record SupplierIdentityMatch(string Reference, string LegalName, IdentityMatchBasis Basis, bool IsConclusive);

/// <summary>
/// What the supplier database can say about whether a name or
/// registration belongs to a supplier it already holds.
/// </summary>
/// <remarks>
/// <b>A result, not an action.</b> The service never merges, never
/// deduplicates and never rewrites a reference. Where it finds matches it
/// hands back candidates and says how strong each is; deciding that two
/// records are one company — and which survives — is a person's job,
/// carried out through the ordinary supersession mechanism.
/// </remarks>
/// <param name="SearchedFor">What was looked up.</param>
/// <param name="Matches">Every supplier that might be it, strongest basis first.</param>
public sealed record SupplierIdentityResolution(string SearchedFor, IReadOnlyList<SupplierIdentityMatch> Matches)
{
    /// <summary>Whether nothing in the database looks like this supplier.</summary>
    public bool IsUnmatched => Matches.Count == 0;

    /// <summary>Whether exactly one candidate matched on a conclusive basis.</summary>
    public bool IsUnambiguous => Matches.Count(m => m.IsConclusive) == 1;

    /// <summary>
    /// Whether several candidates matched, or one matched only on a name.
    /// </summary>
    /// <remarks>
    /// A single name match is ambiguous, not a hit. Two firms called
    /// "Precision Engineering Ltd" is an ordinary occurrence, and a
    /// commercial library that treats a name collision as an identity has
    /// merged two companies.
    /// </remarks>
    public bool IsAmbiguous => Matches.Count > 1 || (Matches.Count == 1 && !Matches[0].IsConclusive);

    /// <summary>The single conclusive match, where there is exactly one.</summary>
    public SupplierIdentityMatch? ConclusiveMatch =>
        IsUnambiguous ? Matches.First(m => m.IsConclusive) : null;
}

/// <summary>
/// Establishes whether a supplier the organisation is about to record is
/// one it already holds.
/// </summary>
/// <remarks>
/// Exists because the alternative — letting whoever is typing decide —
/// is how a database ends up with the same subcontractor four times under
/// four spellings, each with a different fragment of the capability
/// picture.
/// </remarks>
public interface ISupplierIdentityService
{
    /// <summary>Finds supplier records that might be the company described by <paramref name="candidate"/>.</summary>
    /// <param name="candidate">The identity being looked up. Its own <see cref="SupplierIdentity.Reference"/> is excluded from the results.</param>
    /// <param name="cancellationToken">A token to observe while awaiting.</param>
    /// <exception cref="ArgumentNullException"><paramref name="candidate"/> is <see langword="null"/>.</exception>
    Task<SupplierIdentityResolution> ResolveAsync(SupplierIdentity candidate, CancellationToken cancellationToken = default);

    /// <summary>Finds supplier records answering to <paramref name="name"/>, however loosely.</summary>
    /// <param name="name">The name to look up.</param>
    /// <param name="cancellationToken">A token to observe while awaiting.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty, or whitespace.</exception>
    Task<SupplierIdentityResolution> ResolveByNameAsync(string name, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="ISupplierIdentityService"/> implementation.</summary>
public sealed class SupplierIdentityService : ISupplierIdentityService
{
    /// <summary>
    /// The company-form suffixes stripped before comparing two names.
    /// </summary>
    /// <remarks>
    /// A deliberately short, published list. "Precision Engineering Ltd"
    /// and "Precision Engineering Limited" are almost certainly the same
    /// firm; stripping more aggressively starts matching genuinely
    /// different companies.
    /// </remarks>
    public static IReadOnlyList<string> CompanySuffixes { get; } =
        ["LIMITED", "LTD", "PLC", "LLP", "LP", "INC", "INCORPORATED", "GMBH", "BV", "AB", "AS", "SA", "SRL", "PTY", "CO"];

    private readonly ISupplierCatalog _suppliers;

    /// <summary>Initialises a new instance of the <see cref="SupplierIdentityService"/> class.</summary>
    /// <param name="suppliers">The supplier database.</param>
    /// <exception cref="ArgumentNullException"><paramref name="suppliers"/> is <see langword="null"/>.</exception>
    public SupplierIdentityService(ISupplierCatalog suppliers)
    {
        ArgumentNullException.ThrowIfNull(suppliers);

        _suppliers = suppliers;
    }

    /// <inheritdoc />
    public async Task<SupplierIdentityResolution> ResolveAsync(
        SupplierIdentity candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var all = await _suppliers.ListAsync(cancellationToken).ConfigureAwait(false);
        var matches = new List<SupplierIdentityMatch>();

        foreach (var record in all)
        {
            var held = record.Definition.Identity;

            if (string.Equals(held.ReferenceKey, candidate.ReferenceKey, StringComparison.Ordinal))
                continue;

            if (Match(held, candidate) is { } basis)
                matches.Add(new SupplierIdentityMatch(
                    held.Reference,
                    held.LegalName,
                    basis,
                    basis == IdentityMatchBasis.RegistrationNumber));
        }

        return new SupplierIdentityResolution(candidate.LegalName, Ordered(matches));
    }

    /// <inheritdoc />
    public async Task<SupplierIdentityResolution> ResolveByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmed = name.Trim();
        var normalised = Normalise(trimmed);
        var all = await _suppliers.ListAsync(cancellationToken).ConfigureAwait(false);
        var matches = new List<SupplierIdentityMatch>();

        foreach (var record in all)
        {
            var held = record.Definition.Identity;

            var basis = held.AnswersTo(trimmed)
                ? string.Equals(held.LegalName, trimmed, StringComparison.OrdinalIgnoreCase)
                    ? IdentityMatchBasis.LegalName
                    : IdentityMatchBasis.AliasOverlap
                : held.AllNames.Any(n => string.Equals(Normalise(n), normalised, StringComparison.Ordinal))
                    ? IdentityMatchBasis.NormalisedName
                    : (IdentityMatchBasis?)null;

            if (basis is { } found)
                matches.Add(new SupplierIdentityMatch(held.Reference, held.LegalName, found, IsConclusive: false));
        }

        // Never conclusive: a name is not an identity, however exactly it
        // matches, and a caller that gets one hit still has to look.
        return new SupplierIdentityResolution(trimmed, Ordered(matches));
    }

    /// <summary>
    /// A name reduced to what is worth comparing — upper case, no
    /// punctuation, no company-form suffix.
    /// </summary>
    /// <remarks>
    /// Published as public API so a caller can see exactly what the
    /// service considers "the same name", rather than being surprised by
    /// a match it cannot account for.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty, or whitespace.</exception>
    public static string Normalise(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var letters = new string(name.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray());
        var words = letters
            .ToUpperInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        while (words.Count > 1 && CompanySuffixes.Contains(words[^1], StringComparer.Ordinal))
            words.RemoveAt(words.Count - 1);

        return string.Join(' ', words);
    }

    private static IdentityMatchBasis? Match(SupplierIdentity held, SupplierIdentity candidate)
    {
        if (held.HasHardIdentifier
            && candidate.HasHardIdentifier
            && string.Equals(held.RegistrationNumber!.Trim(), candidate.RegistrationNumber!.Trim(), StringComparison.OrdinalIgnoreCase))
            return IdentityMatchBasis.RegistrationNumber;

        if (string.Equals(held.LegalName, candidate.LegalName, StringComparison.OrdinalIgnoreCase))
            return IdentityMatchBasis.LegalName;

        if (candidate.AllNames.Any(held.AnswersTo))
            return IdentityMatchBasis.AliasOverlap;

        var candidateNames = candidate.AllNames.Select(Normalise).ToHashSet(StringComparer.Ordinal);

        return held.AllNames.Select(Normalise).Any(candidateNames.Contains)
            ? IdentityMatchBasis.NormalisedName
            : null;
    }

    private static IReadOnlyList<SupplierIdentityMatch> Ordered(List<SupplierIdentityMatch> matches) =>
        matches
            .OrderBy(m => (int)m.Basis)
            .ThenBy(m => m.Reference, StringComparer.Ordinal)
            .ToList();
}
