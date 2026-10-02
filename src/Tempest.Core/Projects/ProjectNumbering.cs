using System.Globalization;
using System.Text;

namespace Tempest.Core.Projects;

/// <summary>
/// One row of the project-scoped document-type code table
/// (<see cref="ProjectNumbering.DocumentTypes"/>, Product Owner decision
/// 2026-10-01 §3, `ADR-0156`).
/// </summary>
/// <param name="Code">The upper-case code written into the number — the <c>DOCTYPE</c> in <c>CUSTOMER-PROJECTREF-DOCTYPE-NNN</c>.</param>
/// <param name="Description">What kind of record carries it, as a person would say it.</param>
public sealed record ProjectDocumentType(string Code, string Description);

/// <summary>
/// Project-centric numbering (Product Owner decision 2026-10-01 §3,
/// `ADR-0156`, amended by runbook feedback C1): every customer
/// organisation carries a unique five-character <b>customer code</b>, every
/// project a unique six-character <b>project reference</b> — both upper-case
/// letters A–Z and digits 0–9 — a new project is identified as
/// <c>CUSTOMER-PROJECTREF</c> (for example <c>ACME1-BRIDG1</c>), and every
/// generated document number inside it reads
/// <c>CUSTOMER-PROJECTREF-DOCTYPE-NNN</c> (for example
/// <c>ACME1-BRIDG1-Q-001</c>) — a sequence per project per document type,
/// starting at 001, so the first quote in every project is 001.
/// </summary>
/// <remarks>
/// <para>
/// <b>The one place the rules live.</b> The code shape, the suggestion
/// rule, the uniqueness rule, the project identifier's own composition
/// and parsing, and the <see cref="DocumentTypes"/> table are all here,
/// pure and synchronous, so <c>QuotationService</c>,
/// <c>PurchaseOrderService</c>, <c>InvoicingService</c>, the Documents
/// module's own create handler and the Desktop prompts all agree by
/// construction rather than by copy.
/// </para>
/// <para>
/// <b>Read from the project's own identifier, never re-derived.</b> A
/// document number's prefix is the owning project's identifier exactly as
/// it was frozen at creation (<see cref="TryGetDocumentPrefix"/>) — never
/// recomputed from the client's current customer code — so renaming a
/// customer's code later never renumbers, or splits the sequence of, a
/// project that already exists.
/// </para>
/// <para>
/// <b>No migration; the old scheme is the fallback.</b> Projects and
/// records that pre-date this decision keep their identifiers. A project
/// whose identifier is not of the <c>XXXXX-XXXXXX</c> shape (an older
/// <c>P-0001</c>, or a new project created with no client, or whose
/// client has no customer code) is numbered exactly as before:
/// <c>Q-&lt;year&gt;-&lt;nnn&gt;</c>, <c>CO-&lt;year&gt;-&lt;nnn&gt;</c>,
/// <c>PO-&lt;year&gt;-&lt;nnn&gt;</c>, and no identifier at all on an
/// invoice request or a document. The all-letter <c>AAAAA-BBBBB</c> shape
/// the first build of this decision issued (a five-letter project
/// reference) is still recognised as project-centric, so projects created
/// with it keep numbering inside their own identifier.
/// </para>
/// </remarks>
public static class ProjectNumbering
{
    /// <summary>The fixed length of a customer code — five characters A–Z or 0–9.</summary>
    public const int CustomerCodeLength = 5;

    /// <summary>The fixed length of a project reference — six characters A–Z or 0–9.</summary>
    public const int ProjectReferenceLength = 6;

    /// <summary>The length of a project reference issued by the first build of this decision (five letters A–Z) — still recognised inside an existing identifier, never issued.</summary>
    private const int LegacyProjectReferenceLength = 5;

    /// <summary>The characters a code may hold, in the order a suggestion steps through them.</summary>
    private const string CodeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>A quotation (<c>Tempest.Core.Quotations.Quotation</c>, <c>QuotationKind.Quotation</c>).</summary>
    public const string Quotation = "Q";

    /// <summary>A change order (<c>Tempest.Core.Quotations.Quotation</c>, <c>QuotationKind.ChangeOrder</c>).</summary>
    public const string ChangeOrder = "CO";

    /// <summary>A purchase order (<c>Tempest.Core.PurchaseOrders.PurchaseOrder</c>).</summary>
    public const string PurchaseOrder = "PO";

    /// <summary>An invoice request (<c>Tempest.Core.Invoicing.InvoiceRequest</c>).</summary>
    public const string InvoiceRequest = "INV";

    /// <summary>A document (the Documents module's own <c>Document</c> kind).</summary>
    public const string Document = "DOC";

    /// <summary>A drawing (the Documents module's own <c>Drawing</c> kind).</summary>
    public const string Drawing = "DWG";

    /// <summary>A CAD model (the Documents module's own <c>CadModel</c> kind).</summary>
    public const string CadModel = "CAD";

    /// <summary>An engineering calculation (reserved — calculations are not yet numbered by the platform; see `ADR-0156`).</summary>
    public const string Calculation = "CALC";

    /// <summary>
    /// The document-type code table — the one definition of every
    /// <c>DOCTYPE</c> the platform writes into a project-centric number
    /// (`ADR-0156` reproduces it). Codes are never reassigned.
    /// </summary>
    public static IReadOnlyList<ProjectDocumentType> DocumentTypes { get; } =
    [
        new(Quotation, "Quotation"),
        new(ChangeOrder, "Change order"),
        new(PurchaseOrder, "Purchase order"),
        new(InvoiceRequest, "Invoice request"),
        new(Document, "Document"),
        new(Drawing, "Drawing"),
        new(CadModel, "CAD model"),
        new(Calculation, "Calculation (reserved)"),
    ];

    /// <summary>Whether <paramref name="code"/> is exactly five characters, each an upper-case letter A–Z or a digit 0–9 — the shape of a customer code.</summary>
    public static bool IsValidCustomerCode(string? code) => IsCode(code, CustomerCodeLength, allowDigits: true);

    /// <summary>Whether <paramref name="code"/> is exactly six characters, each an upper-case letter A–Z or a digit 0–9 — the shape of a new project reference.</summary>
    public static bool IsValidProjectReference(string? code) => IsCode(code, ProjectReferenceLength, allowDigits: true);

    /// <summary>Trims and upper-cases what a person typed — <c>" acme1 "</c> → <c>"ACME1"</c> — without otherwise validating it; <see langword="null"/> stays <see langword="null"/>.</summary>
    public static string? Normalise(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();

    /// <summary>
    /// Suggests a five-character customer code derived from
    /// <paramref name="name"/> that is not already in
    /// <paramref name="taken"/> (compared case-insensitively) — see
    /// <see cref="SuggestCode"/>.
    /// </summary>
    public static string SuggestCustomerCode(string? name, IEnumerable<string?> taken) =>
        SuggestCode(name, taken, CustomerCodeLength);

    /// <summary>
    /// Suggests a six-character project reference derived from
    /// <paramref name="name"/> that is not already in
    /// <paramref name="taken"/> (compared case-insensitively) — see
    /// <see cref="SuggestCode"/>.
    /// </summary>
    public static string SuggestProjectReference(string? name, IEnumerable<string?> taken) =>
        SuggestCode(name, taken, ProjectReferenceLength);

    /// <summary>
    /// Suggests a <paramref name="length"/>-character code derived from
    /// <paramref name="name"/> that is not already in
    /// <paramref name="taken"/> (compared case-insensitively).
    /// </summary>
    /// <remarks>
    /// The first <paramref name="length"/> letters and digits of
    /// <paramref name="name"/> (accents folded, everything that is not A–Z
    /// or 0–9 dropped), padded with <c>X</c> — at five characters "Acme
    /// Engineering" → <c>ACMEE</c>, "Acme 1" → <c>ACME1</c>, "Ox" →
    /// <c>OXXXX</c>; at six "Bridge" → <c>BRIDGE</c>. Where that is taken,
    /// the last character steps through A–Z then 0–9, then the last two,
    /// until a free code is found — deterministic, so the same name over
    /// the same taken set always suggests the same code. A person may
    /// always overwrite the suggestion.
    /// </remarks>
    public static string SuggestCode(string? name, IEnumerable<string?> taken, int length)
    {
        ArgumentNullException.ThrowIfNull(taken);
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);

        var takenSet = new HashSet<string>(
            taken.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!.Trim().ToUpperInvariant()),
            StringComparer.Ordinal);

        var stem = CodeCharactersOf(name);
        if (stem.Length == 0)
            stem = "X";

        var baseCode = stem.Length >= length ? stem[..length] : stem.PadRight(length, 'X');
        if (!takenSet.Contains(baseCode))
            return baseCode;

        // Step the last character, then the last two, through A–Z then 0–9.
        for (var suffixLength = 1; suffixLength <= length; suffixLength++)
        {
            var head = baseCode[..(length - suffixLength)];
            var combinations = (long)Math.Pow(CodeAlphabet.Length, suffixLength);
            for (long i = 0; i < combinations; i++)
            {
                var candidate = head + EncodeCharacters(i, suffixLength);
                if (!takenSet.Contains(candidate))
                    return candidate;
            }
        }

        throw new InvalidOperationException($"Every {length}-character code is already taken.");
    }

    /// <summary>
    /// Why <paramref name="code"/> cannot be used as a customer code, or
    /// <see langword="null"/> when it can: it must be exactly five
    /// characters A–Z or 0–9 (after <see cref="Normalise"/>) and not in
    /// <paramref name="taken"/>.
    /// </summary>
    /// <param name="code">The candidate, as typed.</param>
    /// <param name="taken">Every code already in use elsewhere — the caller excludes the record being edited.</param>
    public static string? ValidateCustomerCode(string? code, IEnumerable<string?> taken) =>
        Validate(code, taken, "Customer code", CustomerCodeLength);

    /// <summary>
    /// Why <paramref name="code"/> cannot be used as a new project
    /// reference, or <see langword="null"/> when it can: it must be exactly
    /// six characters A–Z or 0–9 (after <see cref="Normalise"/>) and not in
    /// <paramref name="taken"/>.
    /// </summary>
    /// <param name="code">The candidate, as typed.</param>
    /// <param name="taken">Every project reference already in use.</param>
    public static string? ValidateProjectReference(string? code, IEnumerable<string?> taken) =>
        Validate(code, taken, "Project reference", ProjectReferenceLength);

    /// <summary>Composes a project-centric project identifier — <c>ACME1</c> + <c>BRIDG1</c> → <c>ACME1-BRIDG1</c>.</summary>
    /// <exception cref="ArgumentException">The customer code is not five characters, or the project reference not six, A–Z or 0–9.</exception>
    public static string ComposeProjectIdentifier(string customerCode, string projectReference)
    {
        var customer = Normalise(customerCode);
        var project = Normalise(projectReference);

        if (!IsValidCustomerCode(customer))
            throw new ArgumentException($"'{customerCode}' is not a five-character customer code.", nameof(customerCode));
        if (!IsValidProjectReference(project))
            throw new ArgumentException($"'{projectReference}' is not a six-character project reference.", nameof(projectReference));

        return $"{customer}-{project}";
    }

    /// <summary>
    /// Splits a project-centric identifier (<c>ACME1-BRIDG1</c>) into its
    /// customer code and project reference; <see langword="false"/> for
    /// any other shape (<c>P-0001</c>, blank, <see langword="null"/>).
    /// </summary>
    /// <remarks>
    /// The all-letter <c>AAAAA-BBBBB</c> identifiers the first build of
    /// this decision issued (a five-letter project reference) are still
    /// recognised, so numbering keeps working inside projects created with
    /// them.
    /// </remarks>
    public static bool TryParseProjectIdentifier(string? identifier, out string customerCode, out string projectReference)
    {
        customerCode = string.Empty;
        projectReference = string.Empty;

        if (identifier is null)
            return false;

        var trimmed = identifier.Trim();
        if (trimmed.Length <= CustomerCodeLength || trimmed[CustomerCodeLength] != '-')
            return false;

        var customer = trimmed[..CustomerCodeLength];
        var project = trimmed[(CustomerCodeLength + 1)..];
        if (!IsValidCustomerCode(customer))
            return false;

        var isCurrent = IsValidProjectReference(project);
        var isLegacy = IsCode(customer, CustomerCodeLength, allowDigits: false)
                       && IsCode(project, LegacyProjectReferenceLength, allowDigits: false);
        if (!isCurrent && !isLegacy)
            return false;

        customerCode = customer;
        projectReference = project;
        return true;
    }

    /// <summary>The project reference half of every project-centric identifier in <paramref name="projectIdentifiers"/> — the set a new project reference must not collide with.</summary>
    public static IReadOnlyList<string> ProjectReferencesIn(IEnumerable<string?> projectIdentifiers)
    {
        ArgumentNullException.ThrowIfNull(projectIdentifiers);

        var references = new List<string>();
        foreach (var identifier in projectIdentifiers)
        {
            if (TryParseProjectIdentifier(identifier, out _, out var reference))
                references.Add(reference);
        }

        return references;
    }

    /// <summary>
    /// The <c>CUSTOMER-PROJECTREF-DOCTYPE-</c> prefix for
    /// <paramref name="documentType"/> inside the project identified as
    /// <paramref name="projectIdentifier"/>, or <see langword="false"/>
    /// when that project pre-dates (or opted out of) project-centric
    /// numbering — the caller then falls back to its own old scheme.
    /// </summary>
    public static bool TryGetDocumentPrefix(string? projectIdentifier, string documentType, out string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentType);

        prefix = string.Empty;
        if (!TryParseProjectIdentifier(projectIdentifier, out var customer, out var project))
            return false;

        prefix = $"{customer}-{project}-{documentType}-";
        return true;
    }

    /// <summary>
    /// The next <c>&lt;prefix&gt;NNN</c> number — one past the highest
    /// three-digit (or wider) suffix already used under
    /// <paramref name="prefix"/> among <paramref name="existing"/> (live or
    /// not: a number, once used, is never reissued) — <c>001</c> when none is.
    /// </summary>
    public static string NextNumber(string prefix, IEnumerable<string?> existing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentNullException.ThrowIfNull(existing);

        var max = 0;
        foreach (var candidate in existing)
        {
            if (candidate is not null
                && candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(candidate.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var n)
                && n > max)
            {
                max = n;
            }
        }

        return $"{prefix}{(max + 1).ToString("000", CultureInfo.InvariantCulture)}";
    }

    private static string? Validate(string? code, IEnumerable<string?> taken, string what, int length)
    {
        ArgumentNullException.ThrowIfNull(taken);

        var normalised = Normalise(code);
        if (!IsCode(normalised, length, allowDigits: true))
            return $"{what} must be exactly {length} characters, letters A–Z or digits 0–9.";

        if (taken.Any(t => string.Equals(Normalise(t), normalised, StringComparison.Ordinal)))
            return $"{what} '{normalised}' is already in use.";

        return null;
    }

    private static bool IsCode(string? code, int length, bool allowDigits)
    {
        if (code is null || code.Length != length)
            return false;

        foreach (var c in code)
        {
            if (c is >= 'A' and <= 'Z')
                continue;
            if (allowDigits && c is >= '0' and <= '9')
                continue;
            return false;
        }

        return true;
    }

    private static string CodeCharactersOf(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var builder = new StringBuilder();
        foreach (var c in name.Normalize(NormalizationForm.FormD))
        {
            var upper = char.ToUpperInvariant(c);
            if (upper is (>= 'A' and <= 'Z') or (>= '0' and <= '9'))
                builder.Append(upper);
        }

        return builder.ToString();
    }

    private static string EncodeCharacters(long value, int length)
    {
        var chars = new char[length];
        for (var i = length - 1; i >= 0; i--)
        {
            chars[i] = CodeAlphabet[(int)(value % CodeAlphabet.Length)];
            value /= CodeAlphabet.Length;
        }

        return new string(chars);
    }
}
