using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Quotations;

namespace Tempest.Core.Invoicing.Xero.Sync.Quotes;

// ============================================================================
// `v0.24.0` task X3 (D2, Q1) — what a TempestOS quotation looks like to the
// Xero quote sync, and how it maps to Xero's wire shape
// (`docs/releases/v0.24.0/Xero Technical Design.md` §3, §4.1). The planner
// and the push handlers read a quotation only through `IXeroQuoteSource`,
// as the plain-data `XeroQuoteSnapshot` — so they never touch the domain
// model's mutators, and tests can drive them without a workspace.
// ============================================================================

/// <summary>One line of a <see cref="XeroQuoteSnapshot"/>, already in Xero's terms.</summary>
/// <param name="Description">The line's description.</param>
/// <param name="Quantity">Hours for an hourly line; <c>1</c> for a fixed-price line.</param>
/// <param name="UnitAmount">The hourly rate, or the fixed price — net of VAT.</param>
/// <param name="VatRate">The line's VAT treatment; mapped to Xero's output tax type when pushed (X1).</param>
public sealed record XeroQuoteLine(string Description, decimal Quantity, decimal UnitAmount, VatRate VatRate);

/// <summary>
/// A TempestOS quotation as the Xero quote sync reads it (X3): plain data,
/// read from the workspace by <see cref="IXeroQuoteSource"/>.
/// </summary>
/// <param name="QuotationId">The quotation's id — the link's TempestOS key.</param>
/// <param name="Reference">The quotation's reference — Xero's <c>QuoteNumber</c> (the same number in both, Principle).</param>
/// <param name="Status">The quotation's status.</param>
/// <param name="RevisionNumber">The latest approved revision (<c>1</c> for R1); <c>0</c> when none was ever approved (a quotation sent before revisions existed).</param>
/// <param name="Title">The quotation's display name — Xero's <c>Title</c>.</param>
/// <param name="ProjectSummary">The project it belongs to (code and name) — Xero's <c>Summary</c>; <see langword="null"/> when unknown.</param>
/// <param name="ClientOrganisationReference">The client's <c>Organisation.Reference</c>, which the X2 contact link is keyed by; <see langword="null"/> when the quotation's client does not resolve to an organisation.</param>
/// <param name="QuoteDate">The quote date — Xero's <c>Date</c>.</param>
/// <param name="ValidityDays">Days the quote is valid; Xero's <c>ExpiryDate</c> is <see cref="QuoteDate"/> plus these.</param>
/// <param name="Terms">The quote's terms; <see langword="null"/> when none.</param>
/// <param name="CurrencyCode">The ISO currency code.</param>
/// <param name="Lines">The lines, in order.</param>
/// <param name="IssuedAtUtc">When the current revision was issued: the latest revision's approval time, else (a quotation sent before revisions existed) the start of its sent date; <see langword="null"/> when neither is known. Q8 compares it with when Xero sync began.</param>
public sealed record XeroQuoteSnapshot(
    Guid QuotationId,
    string Reference,
    QuotationStatus Status,
    int RevisionNumber,
    string Title,
    string? ProjectSummary,
    string? ClientOrganisationReference,
    DateOnly QuoteDate,
    int ValidityDays,
    string? Terms,
    string CurrencyCode,
    IReadOnlyList<XeroQuoteLine> Lines,
    DateTimeOffset? IssuedAtUtc)
{
    /// <summary>The revision label Xero's <c>Reference</c> carries (<c>R1</c>, <c>R2</c>, …); <see langword="null"/> when no revision was ever approved.</summary>
    public string? RevisionLabel => RevisionNumber > 0 ? QuotationReview.LabelFor(RevisionNumber) : null;

    /// <summary>
    /// Whether the quotation has been issued: an approved revision
    /// (<see cref="QuotationStatus.Approved"/>) or already sent and answered.
    /// A draft or one in review is not — and neither is an approved quotation
    /// edited back to draft: nothing is pushed until the next approval.
    /// </summary>
    public bool IsIssued =>
        (Status == QuotationStatus.Approved && RevisionNumber > 0)
        || Status is QuotationStatus.Sent or QuotationStatus.Accepted or QuotationStatus.Declined;

    /// <summary>Xero's <c>ExpiryDate</c>: <see cref="QuoteDate"/> plus <see cref="ValidityDays"/>.</summary>
    public DateOnly ExpiryDate => QuoteDate.AddDays(Math.Max(0, ValidityDays));
}

/// <summary>Reads quotations for the Xero quote sync — locally, never a network call.</summary>
public interface IXeroQuoteSource
{
    /// <summary>The quotation <paramref name="quotationId"/>, or <see langword="null"/> when there is none.</summary>
    Task<XeroQuoteSnapshot?> FindAsync(Guid quotationId, CancellationToken cancellationToken = default);

    /// <summary>The id of every quotation in the workspace — for the start-up and Refresh scan (§6.2).</summary>
    Task<IReadOnlyList<Guid>> ListIdsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="IXeroQuoteSource"/> over the workspace: the
/// <see cref="Quotation"/> from the engineering repository, its parent
/// <see cref="Project"/> for the summary, and its client from the
/// organisation catalogue (for the X2 contact link).
/// </summary>
public sealed class DomainXeroQuoteSource : IXeroQuoteSource
{
    private readonly EngineeringDomainContext _domain;
    private readonly IOrganisationCatalog _organisations;

    /// <summary>Initialises a new instance of the <see cref="DomainXeroQuoteSource"/> class.</summary>
    /// <param name="domain">The engineering domain (its repository holds quotations and projects).</param>
    /// <param name="organisations">The customers and suppliers.</param>
    public DomainXeroQuoteSource(EngineeringDomainContext domain, IOrganisationCatalog organisations)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(organisations);

        _domain = domain;
        _organisations = organisations;
    }

    /// <inheritdoc />
    public async Task<XeroQuoteSnapshot?> FindAsync(Guid quotationId, CancellationToken cancellationToken = default)
    {
        if (await _domain.Repository.FindAsync(quotationId, cancellationToken).ConfigureAwait(false) is not Quotation quote)
            return null;

        Project? project = null;
        if (quote.ParentId is { } projectId)
            project = await _domain.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) as Project;

        string? clientReference = null;
        if (!string.IsNullOrWhiteSpace(quote.ClientOrganisationId)
            && await _organisations.FindAsync(quote.ClientOrganisationId, cancellationToken).ConfigureAwait(false) is { } organisation)
        {
            clientReference = organisation.Definition.Reference;
        }

        return XeroQuoteMapper.ToSnapshot(quote, project, clientReference);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> ListIdsAsync(CancellationToken cancellationToken = default)
    {
        var entries = await _domain.Repository.ListByKindAsync(Quotation.CanonicalKind, cancellationToken).ConfigureAwait(false);
        return [.. entries.Select(e => e.Id)];
    }
}

/// <summary>
/// Maps a TempestOS quotation to Xero's quote (design §3): pure functions,
/// no I/O — the planner's content hash, the wire body, the attachment name.
/// </summary>
public static class XeroQuoteMapper
{
    /// <summary>The <see cref="XeroLink.LinkedBy"/> for a quote TempestOS created in Xero.</summary>
    public const string LinkedByCreated = "created";

    /// <summary>The <see cref="XeroLink.LinkedBy"/> for a quote found in Xero by its number after an uncertain answer, instead of created again.</summary>
    public const string LinkedByReconciled = "reconciled";

    private static readonly JsonSerializerOptions HashOptions = new() { WriteIndented = false };

    /// <summary>Reads a <see cref="Quotation"/> (and its project and client reference) into a <see cref="XeroQuoteSnapshot"/>.</summary>
    /// <param name="quote">The quotation.</param>
    /// <param name="project">Its project; <see langword="null"/> when not found.</param>
    /// <param name="clientOrganisationReference">The client's <c>Organisation.Reference</c>; <see langword="null"/> when not resolved.</param>
    public static XeroQuoteSnapshot ToSnapshot(Quotation quote, Project? project, string? clientOrganisationReference)
    {
        ArgumentNullException.ThrowIfNull(quote);

        var lines = quote.Lines.Select(ToLine).ToList();

        DateTimeOffset? issuedAt = quote.Review.Revisions.Count > 0
            ? quote.Review.Revisions[^1].ApprovedAt
            : quote.SentOn is { } sent ? new DateTimeOffset(sent.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null;

        string? summary = null;
        if (project is not null)
        {
            summary = string.IsNullOrWhiteSpace(project.Identifier) ? project.DisplayName : $"{project.Identifier} {project.DisplayName}".Trim();
        }

        return new XeroQuoteSnapshot(
            quote.Id,
            quote.Reference,
            quote.Status,
            quote.RevisionNumber,
            string.IsNullOrWhiteSpace(quote.DisplayName) ? quote.Reference : quote.DisplayName,
            summary,
            clientOrganisationReference,
            quote.QuoteDate,
            quote.ValidityDays,
            quote.Terms,
            quote.Currency.IsSpecified ? quote.Currency.ToString() : CurrencyCode.Gbp.ToString(),
            lines,
            issuedAt);
    }

    /// <summary>A quotation line in Xero's terms: hours × rate, or one × the fixed price.</summary>
    /// <param name="line">The quotation line.</param>
    public static XeroQuoteLine ToLine(QuotationLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        return line.Basis == QuotationLineBasis.Hourly && line.Hours is { } hours && line.Rate is { } rate
            ? new XeroQuoteLine(line.Description, hours, rate.Amount, line.VatRate)
            : new XeroQuoteLine(line.Description, 1m, (line.FixedPrice ?? line.Amount).Amount, line.VatRate);
    }

    /// <summary>
    /// The hash of everything a <see cref="XeroOperation.PushQuote"/> carries
    /// from TempestOS — number, revision, title, project, client, dates,
    /// terms, currency and lines — as <c>{revision}.{sha256}</c>: the
    /// revision token (<see cref="RevisionToken"/>, <c>R1</c>, <c>R2</c>, …;
    /// <c>R0</c> when none was ever approved), a dot, and the lower-case hex
    /// SHA-256 (all safe in an <c>Idempotency-Key</c>). An unchanged
    /// quotation hashes the same, so it is never pushed twice; a new
    /// revision hashes differently. The revision prefix lets a link say
    /// which revision Xero holds (<see cref="IsRevisionNotSent"/>) without
    /// confusing a renamed client or project on the same revision with a
    /// revision that was not sent.
    /// </summary>
    /// <param name="quote">The quotation.</param>
    public static string ContentHash(XeroQuoteSnapshot quote)
    {
        ArgumentNullException.ThrowIfNull(quote);

        var canonical = new
        {
            v = 1,
            number = quote.Reference,
            revision = quote.RevisionLabel,
            title = quote.Title,
            summary = quote.ProjectSummary,
            client = quote.ClientOrganisationReference,
            date = XeroWire.FormatDate(quote.QuoteDate),
            expiry = XeroWire.FormatDate(quote.ExpiryDate),
            terms = quote.Terms,
            currency = quote.CurrencyCode,
            lines = quote.Lines.Select(l => new
            {
                d = l.Description,
                q = l.Quantity.ToString(CultureInfo.InvariantCulture),
                u = l.UnitAmount.ToString(CultureInfo.InvariantCulture),
                t = l.VatRate.ToString(),
            }),
        };

        return $"{RevisionToken(quote.RevisionLabel)}.{Sha256Hex(JsonSerializer.Serialize(canonical, HashOptions))}";
    }

    /// <summary>
    /// The revision token of a revision label or of a Xero quote's
    /// <c>Reference</c>: <c>R1</c>, <c>R2</c>, … (upper-cased); <c>R0</c> for
    /// none (a quotation never approved as a revision);
    /// <see cref="UnknownRevision"/> for a reference that names no revision
    /// (a quote keyed into Xero by hand with its own reference).
    /// </summary>
    /// <param name="labelOrReference">The revision label, or Xero's <c>Reference</c>.</param>
    public static string RevisionToken(string? labelOrReference)
    {
        var value = labelOrReference?.Trim();
        if (string.IsNullOrEmpty(value))
            return "R0";

        return value.Length > 1
               && value[0] is 'R' or 'r'
               && value[1] != '0'
               && value.AsSpan(1).IndexOfAnyExceptInRange('0', '9') < 0
               && value.Length <= 10
            ? "R" + value[1..]
            : UnknownRevision;
    }

    /// <summary>The revision token (<see cref="RevisionToken"/>) of a Xero quote whose <c>Reference</c> names no revision TempestOS knows.</summary>
    public const string UnknownRevision = "Rx";

    /// <summary>
    /// The revision a link's <see cref="XeroLink.LastPushedContentHash"/>
    /// records Xero as holding — the token before the dot of a
    /// <see cref="ContentHash"/> or <see cref="ReconciledHash"/>; <see langword="null"/>
    /// when none is recorded.
    /// </summary>
    /// <param name="contentHash">The recorded hash.</param>
    public static string? RevisionOf(string? contentHash)
    {
        var dot = contentHash?.IndexOf('.', StringComparison.Ordinal) ?? -1;
        return dot > 0 ? contentHash![..dot] : null;
    }

    /// <summary>
    /// What a link records for a Xero quote found by its number whose content
    /// TempestOS did not push (keyed in by hand, or <em>Send to Xero</em> on
    /// an older quotation): the revision its <c>Reference</c> names, and no
    /// content hash — so it never matches <see cref="ContentHash"/> (a DRAFT
    /// copy is brought up to date), while <see cref="IsRevisionNotSent"/> can
    /// still tell a copy past DRAFT that shows another revision.
    /// A copy with no <c>Reference</c> at all names no revision either
    /// (<see cref="UnknownRevision"/>): it is not taken for another revision.
    /// </summary>
    /// <param name="xeroReference">The Xero quote's <c>Reference</c>.</param>
    public static string ReconciledHash(string? xeroReference) =>
        $"{(string.IsNullOrWhiteSpace(xeroReference) ? UnknownRevision : RevisionToken(xeroReference))}.reconciled";

    /// <summary>The content hash of a <see cref="XeroOperation.SetQuoteStatus"/> to <paramref name="status"/> — the same for every quote and every attempt, so the outbox never queues the same status change twice.</summary>
    /// <param name="status">The target status.</param>
    public static string StatusHash(XeroQuoteWriteStatus status) => Sha256Hex($"quote-status:{StatusWord(status)}");

    /// <summary>The Xero word for <paramref name="status"/> (<c>DRAFT</c>, <c>SENT</c>, <c>ACCEPTED</c>, <c>DECLINED</c>).</summary>
    /// <param name="status">The status.</param>
    public static string StatusWord(XeroQuoteWriteStatus status) => status.ToString().ToUpperInvariant();

    /// <summary>Parses a Xero quote status word TempestOS writes; <see langword="null"/> for any other word (<c>INVOICED</c>, <c>DELETED</c>, …).</summary>
    /// <param name="word">The word.</param>
    public static XeroQuoteWriteStatus? ParseWriteStatus(string? word) =>
        word?.Trim().ToUpperInvariant() switch
        {
            "DRAFT" => XeroQuoteWriteStatus.Draft,
            "SENT" => XeroQuoteWriteStatus.Sent,
            "ACCEPTED" => XeroQuoteWriteStatus.Accepted,
            "DECLINED" => XeroQuoteWriteStatus.Declined,
            _ => null,
        };

    /// <summary>
    /// The Xero statuses TempestOS walks the quote through to follow
    /// <paramref name="status"/> (§4.1, D2): none before it is sent;
    /// <c>SENT</c> once sent; <c>SENT</c> then <c>ACCEPTED</c> or
    /// <c>DECLINED</c> once answered.
    /// </summary>
    /// <param name="status">The TempestOS quotation status.</param>
    public static IReadOnlyList<XeroQuoteWriteStatus> StatusPath(QuotationStatus status) => status switch
    {
        QuotationStatus.Sent => [XeroQuoteWriteStatus.Sent],
        QuotationStatus.Accepted => [XeroQuoteWriteStatus.Sent, XeroQuoteWriteStatus.Accepted],
        QuotationStatus.Declined => [XeroQuoteWriteStatus.Sent, XeroQuoteWriteStatus.Declined],
        _ => [],
    };

    /// <summary>How far along TempestOS's own walk a Xero status word is: <c>DRAFT</c> 0, <c>SENT</c> 1, <c>ACCEPTED</c>/<c>DECLINED</c> 2; <see langword="null"/> for a word off the walk (<c>INVOICED</c>, <c>DELETED</c>, unknown).</summary>
    /// <param name="word">Xero's status word.</param>
    public static int? Rank(string? word) => ParseWriteStatus(word) switch
    {
        XeroQuoteWriteStatus.Draft => 0,
        XeroQuoteWriteStatus.Sent => 1,
        XeroQuoteWriteStatus.Accepted or XeroQuoteWriteStatus.Declined => 2,
        _ => null,
    };

    /// <summary>
    /// The file name the quote's PDF carries in Xero: the quotation's own
    /// reference plus <c>.pdf</c> — stable across revisions, so a new
    /// revision's PDF replaces the old one by name (§3). Characters a file
    /// name cannot hold become <c>-</c>.
    /// </summary>
    /// <param name="reference">The quotation's reference.</param>
    public static string AttachmentFileName(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(reference.Length + 4);
        foreach (var c in reference.Trim())
            builder.Append(invalid.Contains(c) || c is '/' or '\\' or '?' or '#' or '%' || char.IsControl(c) ? '-' : c);
        return builder.Append(".pdf").ToString();
    }

    /// <summary>
    /// The wire body for <paramref name="quote"/>: lines with the output tax
    /// type for their VAT rate and the sales account code, the contact by
    /// <c>ContactID</c>. <paramref name="blockedReason"/> is set (and the
    /// body <see langword="null"/>) when a line's tax type or the account is
    /// Blocked (§6.8: never sent half-formed).
    /// </summary>
    /// <param name="quote">The quotation.</param>
    /// <param name="contact">The linked contact.</param>
    /// <param name="taxTypeFor">The X1 tax-type resolution for a VAT rate on a sales line.</param>
    /// <param name="salesAccount">The X1 sales account resolution.</param>
    /// <param name="blockedReason">Why the body cannot be built.</param>
    public static XeroWireQuoteWrite? Build(
        XeroQuoteSnapshot quote, XeroWireContactRef contact, Func<VatRate, XeroCodeResolution> taxTypeFor, XeroCodeResolution salesAccount,
        out string? blockedReason)
    {
        ArgumentNullException.ThrowIfNull(quote);
        ArgumentNullException.ThrowIfNull(contact);
        ArgumentNullException.ThrowIfNull(taxTypeFor);
        ArgumentNullException.ThrowIfNull(salesAccount);

        if (quote.Lines.Count == 0)
        {
            blockedReason = $"Quotation {quote.Reference} has no lines; Xero needs at least one.";
            return null;
        }

        if (salesAccount.IsBlocked)
        {
            blockedReason = salesAccount.BlockedReason;
            return null;
        }

        var lines = new List<XeroWireLineItem>(quote.Lines.Count);
        foreach (var line in quote.Lines)
        {
            var tax = taxTypeFor(line.VatRate);
            if (tax.IsBlocked)
            {
                blockedReason = tax.BlockedReason;
                return null;
            }

            lines.Add(new XeroWireLineItem(
                string.IsNullOrWhiteSpace(line.Description) ? "(no description)" : line.Description,
                line.Quantity, line.UnitAmount, salesAccount.Code, tax.Code));
        }

        blockedReason = null;
        return new XeroWireQuoteWrite(
            QuoteNumber: quote.Reference,
            Reference: quote.RevisionLabel,
            Title: Truncate(quote.Title, XeroAccountingApi.MaximumQuoteTitleLength),
            Summary: Truncate(quote.ProjectSummary, XeroAccountingApi.MaximumQuoteSummaryLength),
            Contact: contact,
            Date: XeroWire.FormatDate(quote.QuoteDate),
            ExpiryDate: XeroWire.FormatDate(quote.ExpiryDate),
            Terms: Truncate(quote.Terms, XeroAccountingApi.MaximumQuoteTermsLength),
            CurrencyCode: quote.CurrencyCode,
            LineAmountTypes: XeroWire.LineAmountTypesExclusive,
            LineItems: lines);
    }

    /// <summary>
    /// The note the badge shows when Xero's status for a linked quote no
    /// longer follows TempestOS (design §4.1, Xero → TempestOS: Xero never
    /// changes a TempestOS quote); <see langword="null"/> when it follows.
    /// </summary>
    /// <param name="xeroStatus">Xero's status word, as last read.</param>
    /// <param name="tempestStatus">The TempestOS quotation's status.</param>
    public static string? DriftNote(string? xeroStatus, QuotationStatus tempestStatus)
    {
        if (string.IsNullOrWhiteSpace(xeroStatus))
            return null;

        var word = xeroStatus.Trim().ToUpperInvariant();
        if (word == "INVOICED")
            return "Invoiced in Xero — raising it from TempestOS too would bill twice.";
        if (word == "DELETED")
            return "Deleted in Xero.";

        var path = StatusPath(tempestStatus);
        var expected = path.Count == 0 ? "DRAFT" : StatusWord(path[^1]);
        return word == expected ? null : $"Xero shows this quote as {word}; TempestOS has it as {tempestStatus}.";
    }

    /// <summary>
    /// The note the badge shows for a linked quote (design §4.1, Q1):
    /// when Xero holds it past <c>DRAFT</c> and the current revision's
    /// content never reached it (<see cref="IsRevisionNotSent"/>), that
    /// revision was not sent and must be changed in Xero by hand — the same
    /// reason a refused content push gives
    /// (<see cref="RevisionNotSentNote"/>); otherwise
    /// <see cref="DriftNote(string?, QuotationStatus)"/>. <c>INVOICED</c> and
    /// <c>DELETED</c> keep their own notes.
    /// </summary>
    /// <param name="quote">The TempestOS quotation.</param>
    /// <param name="link">Its Xero link.</param>
    public static string? DriftNote(XeroQuoteSnapshot quote, XeroLink link)
    {
        ArgumentNullException.ThrowIfNull(quote);
        ArgumentNullException.ThrowIfNull(link);

        var word = link.LastKnownXeroStatus?.Trim().ToUpperInvariant();
        if (word is not ("INVOICED" or "DELETED") && IsRevisionNotSent(quote, link))
            return RevisionNotSentNote(link.XeroNumber ?? quote.Reference, word!, quote.RevisionLabel);

        return DriftNote(link.LastKnownXeroStatus, quote.Status);
    }

    /// <summary>
    /// Whether Xero is known to hold the linked quote past <c>DRAFT</c> while
    /// carrying another revision than <paramref name="quote"/>'s current one:
    /// the revision the link records (<see cref="RevisionOf"/> of
    /// <see cref="XeroLink.LastPushedContentHash"/> — what TempestOS pushed,
    /// or, for a reconciled link, what Xero's <c>Reference</c> names) differs
    /// from <see cref="XeroQuoteSnapshot.RevisionLabel"/>. Xero changes content
    /// only while <c>DRAFT</c> (Q1), so that revision — and its PDF, which
    /// follows its content — never reaches Xero. It compares revisions, not
    /// whole content: a client or project renamed on the same revision is not
    /// a revision that was not sent. A copy whose reference names no revision
    /// (<see cref="UnknownRevision"/>) is not flagged.
    /// </summary>
    /// <param name="quote">The TempestOS quotation.</param>
    /// <param name="link">Its Xero link.</param>
    public static bool IsRevisionNotSent(XeroQuoteSnapshot quote, XeroLink link)
    {
        ArgumentNullException.ThrowIfNull(quote);
        ArgumentNullException.ThrowIfNull(link);

        var word = link.LastKnownXeroStatus?.Trim().ToUpperInvariant();
        return !string.IsNullOrEmpty(word)
               && word != StatusWord(XeroQuoteWriteStatus.Draft)
               && RevisionOf(link.LastPushedContentHash) is { } held
               && held != UnknownRevision
               && !string.Equals(held, RevisionToken(quote.RevisionLabel), StringComparison.Ordinal);
    }

    /// <summary>The reason a revision's content (and PDF) did not go to a Xero quote held past <c>DRAFT</c> (Q1).</summary>
    /// <param name="number">The quote's number.</param>
    /// <param name="xeroStatus">Xero's status word.</param>
    /// <param name="revisionLabel">The revision that was not sent (<c>R2</c>, …).</param>
    public static string RevisionNotSentNote(string number, string xeroStatus, string? revisionLabel) =>
        $"Xero holds quote {number} as {xeroStatus}, and Xero changes a quote's content only while it is DRAFT, "
        + $"so revision {revisionLabel ?? "(unnumbered)"} was not sent (Q1: the Xero copy follows a new revision only until the quote is sent). "
        + "Change it in Xero by hand, or unlink it and issue a new quotation.";

    private static string? Truncate(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];

    private static string Sha256Hex(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
