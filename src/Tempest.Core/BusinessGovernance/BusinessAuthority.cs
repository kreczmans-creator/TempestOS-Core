namespace Tempest.Core.BusinessGovernance;

/// <summary>
/// What kind of authority an act of business governance requires.
/// </summary>
/// <remarks>
/// <para>
/// Approving a rate card, executing a contract, accepting a risk and
/// authorising expenditure are all "approval" in ordinary speech and are
/// four different acts with four different consequences. Naming the kind
/// keeps a record from claiming more authority than the person exercising
/// it actually had.
/// </para>
/// <para>
/// Several of these are authorities TempestOS can never itself hold. The
/// system records that a person exercised one; it does not exercise one.
/// </para>
/// </remarks>
public enum BusinessAuthorityKind
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Confirms the record is accurate and complete. Not an approval of what it describes.</summary>
    Verification,

    /// <summary>Approves an internal position — a rate card, a policy, an operating assumption.</summary>
    InternalApproval,

    /// <summary>Binds the organisation to a third party. The authority behind an executed contract.</summary>
    CommercialCommitment,

    /// <summary>Accepts a risk on the organisation's behalf, knowing it is not eliminated.</summary>
    RiskAcceptance,

    /// <summary>Authorises money to be spent.</summary>
    ExpenditureAuthorisation,

    /// <summary>A determination reserved to a qualified legal adviser.</summary>
    LegalDetermination,

    /// <summary>A determination reserved to a qualified accountant or auditor.</summary>
    AccountingDetermination,

    /// <summary>A decision reserved to the organisation's directors.</summary>
    DirectorDecision
}

/// <summary>
/// A recorded act of business authority: who exercised it, over what, and
/// on what basis.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing in `P07` constructs one of these on its own.</b> There is no
/// service method anywhere in this programme that reads a set of records
/// and produces an approval, an acceptance or an authorisation. A caller
/// acting for a named person constructs it; `P07` records it and reports
/// on its absence.
/// </para>
/// <para>
/// Every field that makes the act attributable is required. An approval
/// with no named approver, no date and no stated basis is not an approval
/// — it is a flag somebody set, and this type will not represent it.
/// </para>
/// </remarks>
/// <param name="Kind">What kind of authority was exercised.</param>
/// <param name="PrincipalId">Who exercised it. Required.</param>
/// <param name="RoleOrTitle">The capacity they acted in — Director, Engineering Manager, the organisation's solicitor. Required.</param>
/// <param name="GrantedOn">When they exercised it.</param>
/// <param name="Basis">What they relied on in doing so. Required.</param>
/// <param name="Conditions">Anything the authority is conditional on. <see langword="null"/> where it is unconditional.</param>
/// <param name="Evidence">What records the act — a signed document, a board minute, an email. Never <see langword="null"/>.</param>
public sealed record BusinessAuthorisation(
    BusinessAuthorityKind Kind,
    string PrincipalId,
    string RoleOrTitle,
    DateOnly GrantedOn,
    string Basis,
    string? Conditions = null,
    IReadOnlyList<BusinessEvidence>? Evidence = null)
{
    /// <summary>Who exercised the authority.</summary>
    public string PrincipalId { get; } = string.IsNullOrWhiteSpace(PrincipalId)
        ? throw new ArgumentException(
            "An act of business authority must name the person who exercised it. TempestOS does not approve, accept or authorise anything.",
            nameof(PrincipalId))
        : PrincipalId.Trim();

    /// <summary>The capacity they acted in.</summary>
    public string RoleOrTitle { get; } = string.IsNullOrWhiteSpace(RoleOrTitle)
        ? throw new ArgumentException(
            "An act of business authority must state the capacity the person acted in: whether they were entitled to act is a "
            + "question somebody must be able to ask of the record.",
            nameof(RoleOrTitle))
        : RoleOrTitle.Trim();

    /// <summary>What they relied on.</summary>
    public string Basis { get; } = string.IsNullOrWhiteSpace(Basis)
        ? throw new ArgumentException("An act of business authority must state what it was based on.", nameof(Basis))
        : Basis.Trim();

    /// <summary>What records the act.</summary>
    public IReadOnlyList<BusinessEvidence> Evidence { get; init; } = Evidence ?? [];

    /// <summary>Whether the authority was given subject to conditions somebody must still satisfy.</summary>
    public bool IsConditional => !string.IsNullOrWhiteSpace(Conditions);

    /// <summary>Whether this act is one TempestOS may never perform for itself.</summary>
    /// <remarks>
    /// Every kind here is, in fact, reserved to a person. The property
    /// exists so that a caller reading a record can assert that plainly
    /// rather than inferring it, and so that a future workflow cannot
    /// quietly acquire one of these by accident.
    /// </remarks>
    public static bool IsReservedToAPerson(BusinessAuthorityKind kind) => kind != BusinessAuthorityKind.Unspecified;
}

/// <summary>
/// What a record still needs before it can be relied on, expressed as a
/// requirement rather than as an absence.
/// </summary>
/// <remarks>
/// The difference matters. "This contract has no approval" is a
/// observation; "this contract requires a commercial commitment by a
/// director and does not have one" is actionable, and names who has to
/// act. `P07` reports the second.
/// </remarks>
/// <param name="Kind">The authority required.</param>
/// <param name="Description">What must be authorised, and why. Required.</param>
/// <param name="RequiredOf">Who is expected to exercise it. <see langword="null"/> where nobody has been named yet — itself worth reporting.</param>
/// <param name="RequiredBy">When it is needed by. <see langword="null"/> where no date applies.</param>
public sealed record AuthorityRequirement(
    BusinessAuthorityKind Kind,
    string Description,
    string? RequiredOf = null,
    DateOnly? RequiredBy = null)
{
    /// <summary>What must be authorised, and why.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("An authority requirement must say what needs authorising.", nameof(Description))
        : Description.Trim();

    /// <summary>Whether the requirement names somebody who is supposed to act on it.</summary>
    public bool HasNamedHolder => !string.IsNullOrWhiteSpace(RequiredOf);

    /// <summary>Whether the requirement is overdue as at <paramref name="asAt"/>.</summary>
    public bool IsOverdueAt(DateOnly asAt) => RequiredBy is { } due && due < asAt;
}
