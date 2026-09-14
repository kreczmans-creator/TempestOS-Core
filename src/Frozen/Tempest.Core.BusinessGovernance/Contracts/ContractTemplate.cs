using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Contracts;

/// <summary>What subject a contract clause deals with.</summary>
/// <remarks>
/// A closed list, deliberately. Free-text clause categories make it
/// impossible to answer "does every one of our contracts say something
/// about liability?" — which is the question a contract library exists to
/// answer. <see cref="Other"/> exists for the genuinely unusual clause and
/// is reported by validation so it does not become the default.
/// </remarks>
public enum ClauseCategory
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Who the parties are and in what capacity they contract.</summary>
    Parties,

    /// <summary>What is to be done.</summary>
    Scope,

    /// <summary>What is explicitly not to be done — often the most valuable clause in an engineering contract.</summary>
    Exclusions,

    /// <summary>What will be handed over.</summary>
    Deliverables,

    /// <summary>How a deliverable is judged acceptable.</summary>
    Acceptance,

    /// <summary>What is charged.</summary>
    Price,

    /// <summary>When and how it is paid.</summary>
    Payment,

    /// <summary>How the scope, price or programme may be changed after signature.</summary>
    ChangeControl,

    /// <summary>Who owns what intellectual property, and who may use it.</summary>
    IntellectualProperty,

    /// <summary>What must be kept confidential, by whom, and for how long.</summary>
    Confidentiality,

    /// <summary>How personal data is handled.</summary>
    DataProtection,

    /// <summary>What each party is liable for, and up to what limit.</summary>
    Liability,

    /// <summary>What insurance each party must carry.</summary>
    Insurance,

    /// <summary>What warranties are given, and for how long.</summary>
    Warranty,

    /// <summary>How the contract ends, early or otherwise.</summary>
    Termination,

    /// <summary>How disputes are resolved, and under whose law.</summary>
    DisputeResolution,

    /// <summary>Export control, sanctions, or other trade restrictions.</summary>
    ExportControl,

    /// <summary>Health, safety, environmental or site obligations.</summary>
    HealthAndSafety,

    /// <summary>Something else, described in the clause itself.</summary>
    Other
}

/// <summary>
/// One clause of a contract template.
/// </summary>
/// <remarks>
/// <para>
/// <b>The clause text is the source document's, not TempestOS's.</b> This
/// type holds a clause an organisation's own template already contains,
/// so that the library can report on what its templates say. It is not a
/// clause bank TempestOS supplies, and no clause text ships with this
/// platform: `P07` would otherwise be distributing legal wording nobody
/// had drafted or reviewed.
/// </para>
/// <para>
/// <see cref="IsNegotiable"/> and <see cref="RequiresLegalReview"/> record
/// the organisation's own position on a clause. Neither is a legal
/// determination; both are commercial policy somebody set.
/// </para>
/// </remarks>
/// <param name="Reference">The clause's own number or reference within the template. Required.</param>
/// <param name="Heading">What the clause is called. Required.</param>
/// <param name="Category">What subject it deals with.</param>
/// <param name="Text">The clause wording as the template has it. <see langword="null"/> where the template is held as a document and only its structure is indexed here.</param>
/// <param name="IsMandatory">Whether the organisation's policy is that this clause appears in every contract on this template.</param>
/// <param name="IsNegotiable">Whether the organisation is willing to vary it.</param>
/// <param name="RequiresLegalReview">Whether varying it needs a solicitor rather than a commercial decision.</param>
/// <param name="Guidance">The organisation's own note on how to use the clause. <see langword="null"/> if none.</param>
public sealed record ContractClause(
    string Reference,
    string Heading,
    ClauseCategory Category = ClauseCategory.Unspecified,
    string? Text = null,
    bool IsMandatory = false,
    bool IsNegotiable = true,
    bool RequiresLegalReview = false,
    string? Guidance = null)
{
    /// <summary>The clause's own number or reference within the template.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A contract clause must carry its own reference, or nothing can cite it.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What the clause is called.</summary>
    public string Heading { get; } = string.IsNullOrWhiteSpace(Heading)
        ? throw new ArgumentException("A contract clause must have a heading.", nameof(Heading))
        : Heading.Trim();

    /// <summary>Whether the wording itself is recorded here, or only the clause's existence and shape.</summary>
    public bool HasText => !string.IsNullOrWhiteSpace(Text);

    /// <summary>The case-insensitive key the clause is found by within its template.</summary>
    public string ReferenceKey => Reference.ToUpperInvariant();
}

/// <summary>
/// A controlled contract template — the organisation's own standard form
/// for a kind of engagement.
/// </summary>
/// <remarks>
/// <para>
/// <b>A template is not a contract, and a registered template is not legal
/// advice.</b> Registering a template here records that the organisation
/// uses it; it says nothing about whether it is fit for a given
/// engagement, enforceable, or current with the law. Those are
/// determinations for a solicitor, and the template's own governance
/// facts are where that review is recorded — or recorded as missing.
/// </para>
/// <para>
/// The template is a governed reference-data record on the shared
/// `Group A` lifecycle (`ADR-0128`, extended by `ADR-0130`): authored,
/// sourced, checked, released, revisioned and superseded. That is what
/// makes the guarantee C1 exists to give — that revising a template
/// cannot alter a contract already issued from it — mechanical rather
/// than procedural.
/// </para>
/// </remarks>
public sealed record ContractTemplate
{
    /// <summary>The identifier the template is known by. Required.</summary>
    public required string Code { get; init; }

    /// <summary>What the template is called. Required.</summary>
    public required string Name { get; init; }

    /// <summary>What kind of engagement it is for, and when to use it. Required.</summary>
    public required string Purpose { get; init; }

    /// <summary>The governance every `P07` record carries. Required.</summary>
    public required BusinessGovernanceFacts Governance { get; init; }

    /// <summary>The clauses the template contains, in the order it contains them. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ContractClause> Clauses { get; init; } = [];

    /// <summary>The commercial terms the template defaults to. <see langword="null"/> where it states none and each engagement sets its own.</summary>
    public CommercialTerms? DefaultCommercialTerms { get; init; }

    /// <summary>
    /// The source document the template lives in, where the authoritative
    /// wording is a document rather than the clause text held here.
    /// </summary>
    /// <remarks>
    /// The common case, and the honest one. A contract template is a legal
    /// document that a solicitor drafted and a word processor holds;
    /// TempestOS indexes it, governs it and reports on it, and does not
    /// claim to be its authoritative form.
    /// </remarks>
    public Guid? SourceDocumentId { get; init; }

    /// <summary>The law the template is written under, as the template itself states it. <see langword="null"/> if it does not say.</summary>
    /// <remarks>
    /// Recorded as the template's own statement, not as a determination
    /// that the stated law in fact governs a contract issued from it.
    /// </remarks>
    public string? StatedGoverningLaw { get; init; }

    /// <summary>Whether a solicitor has reviewed this revision of the template, and what came of it.</summary>
    /// <remarks>
    /// <see cref="DeterminationState.NotDetermined"/> by default and
    /// deliberately: the overwhelmingly common state of a template
    /// somebody adapted from a previous engagement, and the one a contract
    /// library must be able to report.
    /// </remarks>
    public DeterminationState LegalReviewState { get; init; } = DeterminationState.NotDetermined;

    /// <summary>Anything else about the template. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>The clauses the organisation's policy says must appear in every contract on this template.</summary>
    public IEnumerable<ContractClause> MandatoryClauses => Clauses.Where(c => c.IsMandatory);

    /// <summary>The clauses that need a solicitor before they may be varied.</summary>
    public IEnumerable<ContractClause> ClausesNeedingLegalReview => Clauses.Where(c => c.RequiresLegalReview);

    /// <summary>Whether the template says anything at all about <paramref name="category"/>.</summary>
    public bool Covers(ClauseCategory category) => Clauses.Any(c => c.Category == category);

    /// <summary>Returns the clause with <paramref name="reference"/>, or <see langword="null"/> if the template has none.</summary>
    public ContractClause? FindClause(string reference) =>
        Clauses.FirstOrDefault(c => string.Equals(c.Reference, reference, StringComparison.OrdinalIgnoreCase));

    /// <summary>The case-insensitive key <see cref="Code"/> is indexed under.</summary>
    public string CodeKey => CodeKeyFor(Code);

    /// <summary>The case-insensitive key <paramref name="code"/> would be indexed under.</summary>
    /// <exception cref="ArgumentException"><paramref name="code"/> is null, empty, or whitespace.</exception>
    public static string CodeKeyFor(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return code.Trim().ToUpperInvariant();
    }
}
