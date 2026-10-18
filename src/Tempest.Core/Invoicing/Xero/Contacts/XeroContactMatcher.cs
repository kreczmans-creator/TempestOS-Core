using System.Text;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Invoicing.Xero.Api;

namespace Tempest.Core.Invoicing.Xero.Contacts;

/// <summary>
/// Ranks Xero contacts as possible matches for a TempestOS organisation
/// (`v0.24.0` X2, design §5): by VAT number, then <c>ContactNumber</c>
/// (TempestOS's customer code), then exact name, then similar name —
/// strongest first. Pure: no I/O, so every rule is tested directly.
/// </summary>
/// <remarks>
/// A match only <em>offers</em> a contact. Nothing here links one: the
/// Product Owner confirms (<see cref="IXeroContactLinker.LinkExistingAsync"/>),
/// so a name alone never decides which Xero contact a document goes to.
/// Only <c>ACTIVE</c> contacts are offered — an archived contact cannot
/// take a new document.
/// </remarks>
public static class XeroContactMatcher
{
    /// <summary><see cref="XeroContactCandidate.MatchedOn"/>: the VAT numbers are the same.</summary>
    public const string MatchedOnVatNumber = "vat-number";

    /// <summary><see cref="XeroContactCandidate.MatchedOn"/>: Xero's <c>ContactNumber</c> is the organisation's customer code.</summary>
    public const string MatchedOnContactNumber = "contact-number";

    /// <summary><see cref="XeroContactCandidate.MatchedOn"/>: the names are the same, ignoring case and spacing.</summary>
    public const string MatchedOnExactName = "exact-name";

    /// <summary><see cref="XeroContactCandidate.MatchedOn"/>: the names are alike (same words once legal suffixes and punctuation are dropped, one contained in the other, or at least half their words shared).</summary>
    public const string MatchedOnSimilarName = "similar-name";

    /// <summary>Xero's status word for a contact that may be linked.</summary>
    public const string ActiveStatus = "ACTIVE";

    private static readonly HashSet<string> LegalSuffixes = new(StringComparer.Ordinal)
    {
        "LTD", "LIMITED", "PLC", "LLP", "LP", "INC", "INCORPORATED", "LLC", "CORP", "CORPORATION",
        "GMBH", "AG", "SA", "SAS", "SARL", "BV", "NV", "PTY", "CO", "COMPANY", "THE", "AND",
    };

    /// <summary>
    /// The <c>ContactNumber</c> TempestOS gives an organisation's Xero
    /// contact: its customer code (Q7, `ADR-0156`), else its reference;
    /// <see langword="null"/> when that is longer than Xero's 50 characters
    /// (TempestOS then writes none rather than a truncated, ambiguous one).
    /// </summary>
    /// <param name="organisation">The organisation.</param>
    public static string? ContactNumberFor(Organisation organisation)
    {
        ArgumentNullException.ThrowIfNull(organisation);

        var number = (string.IsNullOrWhiteSpace(organisation.CustomerCode) ? organisation.Reference : organisation.CustomerCode).Trim();
        return number.Length is > 0 and <= XeroAccountingApi.MaximumContactNumberLength ? number : null;
    }

    /// <summary>
    /// The text TempestOS searches Xero's contacts with for
    /// <paramref name="name"/> (Xero's <c>searchTerm</c> matches a part of a
    /// name): the name with a leading "The" and trailing legal suffixes
    /// dropped ("Acme Engineering Ltd." → "Acme Engineering", "Smith &amp;
    /// Jones Limited" → "Smith &amp; Jones"), so "Acme Engineering Limited"
    /// in Xero is still found; the trimmed name itself when nothing would be
    /// left.
    /// </summary>
    /// <param name="name">The organisation's name.</param>
    public static string SearchTermFor(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var tokens = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
        while (tokens.Count > 1 && IsSuffixToken(tokens[^1]))
            tokens.RemoveAt(tokens.Count - 1);
        if (tokens.Count > 1 && string.Equals(LettersOf(tokens[0]), "THE", StringComparison.Ordinal))
            tokens.RemoveAt(0);

        var term = string.Join(' ', tokens).TrimEnd(',', '.', ';', ':', '-').Trim();
        return term.Length == 0 ? name.Trim() : term;

        static bool IsSuffixToken(string token) => LettersOf(token) is var letters && (letters.Length == 0 || LegalSuffixes.Contains(letters));
    }

    /// <summary>
    /// A wider search term for <paramref name="name"/>, used only when
    /// <see cref="SearchTermFor"/> found no alike name: its longest
    /// distinctive word (at least three characters, legal suffixes
    /// excluded), so "Acme Eng." in Xero is still offered for "Acme
    /// Engineering Ltd"; <see langword="null"/> when the name has a single
    /// such word (the first search already covered it) or none.
    /// </summary>
    /// <param name="name">The organisation's name.</param>
    public static string? WiderSearchTermFor(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var words = Words(name, original: true)
            .Where(w => w.Length >= 3 && !LegalSuffixes.Contains(w.ToUpperInvariant()))
            .ToList();
        return words.Count < 2
            ? null
            : words.OrderByDescending(w => w.Length).ThenBy(w => w, StringComparer.Ordinal).First();
    }

    /// <summary>
    /// A VAT number reduced for comparison: letters and digits only, upper
    /// case ("gb 123 4567 89" → "GB123456789"); <see langword="null"/> when
    /// nothing is left.
    /// </summary>
    /// <param name="vatNumber">The VAT number as entered.</param>
    public static string? NormaliseVatNumber(string? vatNumber)
    {
        if (string.IsNullOrWhiteSpace(vatNumber))
            return null;

        var builder = new StringBuilder(vatNumber.Length);
        foreach (var c in vatNumber)
        {
            if (char.IsAsciiLetterOrDigit(c))
                builder.Append(char.ToUpperInvariant(c));
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    /// <summary>
    /// Whether two VAT numbers are the same registration: equal once
    /// normalised, or equal once a two-letter country prefix present on one
    /// side only is dropped ("GB123456789" and "123456789").
    /// </summary>
    /// <param name="left">One VAT number.</param>
    /// <param name="right">The other.</param>
    public static bool SameVatNumber(string? left, string? right)
    {
        var a = NormaliseVatNumber(left);
        var b = NormaliseVatNumber(right);
        if (a is null || b is null)
            return false;

        return a == b || WithoutCountryPrefix(a) == b || a == WithoutCountryPrefix(b);
    }

    /// <summary>Whether two names are the same name, ignoring case and runs of spaces.</summary>
    /// <param name="left">One name.</param>
    /// <param name="right">The other.</param>
    public static bool SameName(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right)
        && string.Equals(CollapseSpaces(left), CollapseSpaces(right), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether two names are alike enough to offer: the same words once
    /// legal suffixes and punctuation are dropped, one's words a contiguous
    /// part of the other's, or at least half of all their distinct words
    /// shared.
    /// </summary>
    /// <param name="left">One name.</param>
    /// <param name="right">The other.</param>
    public static bool SimilarName(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        var a = CoreWords(left);
        var b = CoreWords(right);
        if (a.Count == 0 || b.Count == 0)
            return false;

        var joinedA = string.Join(' ', a);
        var joinedB = string.Join(' ', b);
        if (joinedA == joinedB || $" {joinedA} ".Contains($" {joinedB} ", StringComparison.Ordinal) || $" {joinedB} ".Contains($" {joinedA} ", StringComparison.Ordinal))
            return true;

        var setA = a.ToHashSet(StringComparer.Ordinal);
        var setB = b.ToHashSet(StringComparer.Ordinal);
        var shared = setA.Intersect(setB, StringComparer.Ordinal).Count();
        var all = setA.Union(setB, StringComparer.Ordinal).Count();
        return shared * 2 >= all;
    }

    /// <summary>
    /// Ranks <paramref name="contacts"/> (every contact the lookups found,
    /// duplicates allowed) as candidates for <paramref name="organisation"/>:
    /// each <c>ACTIVE</c> contact once, under its strongest reason, those
    /// matching on nothing dropped. Within a reason, a contact Xero already
    /// marks with the organisation's role (customer or supplier) comes
    /// first, then by name and <c>ContactID</c> (ordinal), so the order is
    /// stable.
    /// </summary>
    /// <param name="organisation">The TempestOS organisation.</param>
    /// <param name="contacts">The contacts Xero answered.</param>
    public static IReadOnlyList<XeroContactCandidate> Rank(Organisation organisation, IEnumerable<XeroWireContact> contacts)
    {
        ArgumentNullException.ThrowIfNull(organisation);
        ArgumentNullException.ThrowIfNull(contacts);

        var contactNumber = ContactNumberFor(organisation);
        var best = new Dictionary<string, (int Rank, XeroWireContact Contact)>(StringComparer.OrdinalIgnoreCase);

        foreach (var contact in contacts)
        {
            if (string.IsNullOrWhiteSpace(contact.ContactID) || !IsActive(contact))
                continue;

            var rank = RankOf(organisation, contactNumber, contact);
            if (rank < 0)
                continue;

            if (!best.TryGetValue(contact.ContactID, out var existing) || rank < existing.Rank)
                best[contact.ContactID] = (rank, contact);
        }

        return
        [
            .. best.Values
                .OrderBy(b => b.Rank)
                .ThenBy(b => HasRole(organisation, b.Contact) ? 0 : 1)
                .ThenBy(b => b.Contact.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(b => b.Contact.ContactID, StringComparer.Ordinal)
                .Select(b => ToCandidate(b.Contact, ReasonOf(b.Rank))),
        ];
    }

    /// <summary>Whether <paramref name="contact"/> may be linked: Xero holds it as <c>ACTIVE</c> (a contact with no status word reads as active, as Xero's default).</summary>
    /// <param name="contact">The contact.</param>
    public static bool IsActive(XeroWireContact contact)
    {
        ArgumentNullException.ThrowIfNull(contact);

        return string.IsNullOrWhiteSpace(contact.ContactStatus) || string.Equals(contact.ContactStatus, ActiveStatus, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The candidate shape of <paramref name="contact"/>, offered for <paramref name="matchedOn"/>.</summary>
    /// <param name="contact">The contact.</param>
    /// <param name="matchedOn">Why it was offered.</param>
    public static XeroContactCandidate ToCandidate(XeroWireContact contact, string matchedOn)
    {
        ArgumentNullException.ThrowIfNull(contact);

        return new XeroContactCandidate(
            contact.ContactID ?? string.Empty,
            contact.Name ?? string.Empty,
            Blank(contact.TaxNumber),
            Blank(contact.ContactNumber),
            Blank(contact.EmailAddress),
            contact.IsCustomer ?? false,
            contact.IsSupplier ?? false,
            string.IsNullOrWhiteSpace(contact.ContactStatus) ? ActiveStatus : contact.ContactStatus.Trim().ToUpperInvariant(),
            matchedOn);
    }

    private static int RankOf(Organisation organisation, string? contactNumber, XeroWireContact contact)
    {
        if (SameVatNumber(organisation.TaxRegistration, contact.TaxNumber))
            return 0;
        if (contactNumber is not null && string.Equals(contact.ContactNumber?.Trim(), contactNumber, StringComparison.OrdinalIgnoreCase))
            return 1;
        if (SameName(organisation.Name, contact.Name))
            return 2;
        if (SimilarName(organisation.Name, contact.Name))
            return 3;
        return -1;
    }

    private static string ReasonOf(int rank) => rank switch
    {
        0 => MatchedOnVatNumber,
        1 => MatchedOnContactNumber,
        2 => MatchedOnExactName,
        _ => MatchedOnSimilarName,
    };

    private static bool HasRole(Organisation organisation, XeroWireContact contact) => organisation.TradingType switch
    {
        OrganisationTradingType.Supplier => contact.IsSupplier == true,
        OrganisationTradingType.Both => contact.IsCustomer == true || contact.IsSupplier == true,
        _ => contact.IsCustomer == true,
    };

    private static List<string> CoreWords(string name) =>
        [.. Words(name, original: false).Where(w => !LegalSuffixes.Contains(w))];

    /// <summary>The name's words: runs of letters and digits ("&amp;" and other punctuation separate words); upper case unless <paramref name="original"/>.</summary>
    private static IEnumerable<string> Words(string name, bool original)
    {
        var builder = new StringBuilder();
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(original ? c : char.ToUpperInvariant(c));
                continue;
            }

            if (builder.Length > 0)
            {
                yield return builder.ToString();
                builder.Clear();
            }
        }

        if (builder.Length > 0)
            yield return builder.ToString();
    }

    private static string LettersOf(string token)
    {
        var builder = new StringBuilder(token.Length);
        foreach (var c in token)
        {
            if (char.IsLetterOrDigit(c))
                builder.Append(char.ToUpperInvariant(c));
        }

        return builder.ToString();
    }

    private static string CollapseSpaces(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string WithoutCountryPrefix(string normalised) =>
        normalised.Length > 2 && char.IsAsciiLetter(normalised[0]) && char.IsAsciiLetter(normalised[1]) ? normalised[2..] : normalised;

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
