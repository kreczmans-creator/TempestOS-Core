using Tempest.Core.Identity;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Contracts;

/// <summary>Thrown when a contract would be drawn from a template that has not been released.</summary>
public sealed class UnreleasedContractTemplateException : ReferenceDataException
{
    /// <summary>Initialises a new instance of the <see cref="UnreleasedContractTemplateException"/> class.</summary>
    /// <param name="templateCode">The template that was asked for.</param>
    /// <param name="state">The state it is actually in.</param>
    public UnreleasedContractTemplateException(string templateCode, ReferenceValidationState state)
        : base(
            "BusinessContractTemplates",
            $"Contract template '{templateCode}' is {state}, not Released. A contract must not be drawn from a template nobody "
            + "has finished checking, so the request is refused rather than answered.")
    {
        TemplateCode = templateCode;
        State = state;
    }

    /// <summary>The template that was asked for.</summary>
    public string TemplateCode { get; }

    /// <summary>The state it is actually in.</summary>
    public ReferenceValidationState State { get; }
}

/// <summary>
/// What a contract, or the whole contract library, currently owes and is
/// owed.
/// </summary>
/// <remarks>
/// A report, not a workflow. It answers "what are our obligations?" from
/// records that already exist; it does not chase, remind or escalate,
/// which are `P04`'s business.
/// </remarks>
/// <param name="AsAt">The date the position was taken at.</param>
/// <param name="OverdueObligations">Obligations past their own date, with the contract each belongs to.</param>
/// <param name="ExpiringWithin">Contracts whose term ends within the window asked for.</param>
/// <param name="ExpiredButStillExecuted">Contracts whose term has ended while their status still says Executed.</param>
/// <param name="AwaitingCommitmentAuthority">Contracts recorded as executed that nobody is recorded as having been authorised to sign.</param>
/// <param name="UnacceptedDeliverables">Deliverables nobody has accepted, with the contract each belongs to.</param>
public sealed record ContractObligationPosition(
    DateOnly AsAt,
    IReadOnlyList<ContractObligationEntry> OverdueObligations,
    IReadOnlyList<string> ExpiringWithin,
    IReadOnlyList<string> ExpiredButStillExecuted,
    IReadOnlyList<string> AwaitingCommitmentAuthority,
    IReadOnlyList<ContractDeliverableEntry> UnacceptedDeliverables)
{
    /// <summary>Whether anything at all needs somebody's attention.</summary>
    public bool HasFindings =>
        OverdueObligations.Count > 0
        || ExpiringWithin.Count > 0
        || ExpiredButStillExecuted.Count > 0
        || AwaitingCommitmentAuthority.Count > 0
        || UnacceptedDeliverables.Count > 0;
}

/// <summary>One obligation, named with the contract it belongs to.</summary>
/// <param name="ContractReference">The contract.</param>
/// <param name="Obligation">The obligation.</param>
public sealed record ContractObligationEntry(string ContractReference, ContractObligation Obligation);

/// <summary>One deliverable, named with the contract it belongs to.</summary>
/// <param name="ContractReference">The contract.</param>
/// <param name="Deliverable">The deliverable.</param>
public sealed record ContractDeliverableEntry(string ContractReference, ContractDeliverable Deliverable);

/// <summary>
/// Draws contracts from controlled templates, and reports on what the
/// contract library obliges the organisation to do.
/// </summary>
/// <remarks>
/// <b>Nothing here executes a contract.</b> There is no method that moves
/// a contract to <see cref="ContractStatus.Executed"/>, because doing so
/// binds the organisation and that is an act of authority a person
/// exercises. <see cref="PrepareFromTemplateAsync"/> produces a draft with
/// the template revision pinned; a caller acting for a named person
/// records the commitment.
/// </remarks>
public interface IContractService
{
    /// <summary>
    /// Prepares a draft contract from the released template registered
    /// under <paramref name="templateCode"/>, pinned to the revision read.
    /// </summary>
    /// <param name="templateCode">The released template to draw from.</param>
    /// <param name="reference">The reference the new contract will be known by.</param>
    /// <param name="title">What the contract is for.</param>
    /// <param name="parties">Who it will bind.</param>
    /// <param name="governance">Owner, classification, review cycle and the authority the contract will still need.</param>
    /// <param name="cancellationToken">A token to observe while awaiting.</param>
    /// <returns>A contract in <see cref="ContractStatus.Draft"/>, carrying the template's own default commercial terms and its pin.</returns>
    /// <exception cref="ArgumentException">A string argument is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="parties"/> or <paramref name="governance"/> is <see langword="null"/>.</exception>
    /// <exception cref="ReferenceRecordNotFoundException">No template is registered under <paramref name="templateCode"/>.</exception>
    /// <exception cref="UnreleasedContractTemplateException">The template is registered but not Released.</exception>
    Task<IssuedContract> PrepareFromTemplateAsync(
        string templateCode,
        string reference,
        string title,
        IReadOnlyList<ContractParty> parties,
        BusinessGovernanceFacts governance,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the template revision a contract was drawn from, exactly as
    /// it stood when the contract was drawn.
    /// </summary>
    /// <param name="contract">The contract whose template is wanted.</param>
    /// <param name="cancellationToken">A token to observe while awaiting.</param>
    /// <returns>The pinned template revision, or <see langword="null"/> where the contract is bespoke.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="contract"/> is <see langword="null"/>.</exception>
    Task<IReferenceRecord<ContractTemplate>?> ResolveTemplateAsync(
        IssuedContract contract,
        CancellationToken cancellationToken = default);

    /// <summary>Reports what the contract library obliges the organisation to do as at <paramref name="asAt"/>.</summary>
    /// <param name="asAt">The date to take the position at.</param>
    /// <param name="expiryWindowDays">How far ahead to look for contracts whose term is ending.</param>
    /// <param name="cancellationToken">A token to observe while awaiting.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="expiryWindowDays"/> is negative.</exception>
    Task<ContractObligationPosition> ReportObligationsAsync(
        DateOnly asAt,
        int expiryWindowDays = 90,
        CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IContractService"/> implementation.</summary>
public sealed class ContractService : IContractService
{
    private readonly IContractTemplateCatalog _templates;
    private readonly IIssuedContractCatalog _contracts;
    private readonly ICurrentPrincipalAccessor _principals;

    /// <summary>Initialises a new instance of the <see cref="ContractService"/> class.</summary>
    /// <param name="templates">The controlled template library.</param>
    /// <param name="contracts">The issued-contract library.</param>
    /// <param name="principals">The platform's own identity boundary, for attributing a preparation.</param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public ContractService(
        IContractTemplateCatalog templates,
        IIssuedContractCatalog contracts,
        ICurrentPrincipalAccessor principals)
    {
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(contracts);
        ArgumentNullException.ThrowIfNull(principals);

        _templates = templates;
        _contracts = contracts;
        _principals = principals;
    }

    /// <inheritdoc />
    public async Task<IssuedContract> PrepareFromTemplateAsync(
        string templateCode,
        string reference,
        string title,
        IReadOnlyList<ContractParty> parties,
        BusinessGovernanceFacts governance,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(parties);
        ArgumentNullException.ThrowIfNull(governance);

        var record = await _templates.FindByCodeAsync(templateCode, cancellationToken).ConfigureAwait(false)
            ?? throw new ReferenceRecordNotFoundException(_templates.LibraryName, templateCode);

        if (record.ValidationState != ReferenceValidationState.Released)
            throw new UnreleasedContractTemplateException(record.Definition.Code, record.ValidationState);

        // The commitment the contract will need before it can bind the
        // organisation is stated on the draft from the outset, so that a
        // contract sitting unsigned reports as waiting on a named person
        // rather than as merely incomplete.
        var required = governance.OutstandingAuthorities
            .Concat([
                new AuthorityRequirement(
                    BusinessAuthorityKind.CommercialCommitment,
                    $"Signature binding the organisation to contract '{reference.Trim()}'.",
                    RequiredOf: governance.Ownership.OwnerPrincipalId),
            ])
            .DistinctBy(r => (r.Kind, r.Description))
            .ToList();

        return new IssuedContract
        {
            Reference = reference.Trim(),
            Title = title.Trim(),
            Parties = parties,
            Governance = governance with { OutstandingAuthorities = required },
            Status = ContractStatus.Draft,
            TemplatePin = ReferencePin.For(_templates.LibraryName, record),
            CommercialTerms = record.Definition.DefaultCommercialTerms,
            Notes = $"Prepared from template '{record.Definition.Code}' revision {record.RevisionNumber} by "
                    + $"'{_principals.Current?.Identity.Id ?? UnknownPreparerPrincipalId}'. Not executed: a contract binds the "
                    + "organisation only when a person with commercial authority signs it.",
        };
    }

    /// <summary>The principal id recorded when no principal is available.</summary>
    public const string UnknownPreparerPrincipalId = "unknown";

    /// <inheritdoc />
    public async Task<IReferenceRecord<ContractTemplate>?> ResolveTemplateAsync(
        IssuedContract contract,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contract);

        if (contract.TemplatePin is not { } pin)
            return null;

        if (!string.Equals(pin.Library, _templates.LibraryName, StringComparison.Ordinal))
            throw new ArgumentException(
                $"Contract '{contract.Reference}' pins {pin}, which names library '{pin.Library}' rather than the contract-template library.",
                nameof(contract));

        return await _templates.GetRevisionAsync(pin.RecordId, pin.RevisionNumber, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ContractObligationPosition> ReportObligationsAsync(
        DateOnly asAt,
        int expiryWindowDays = 90,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expiryWindowDays);

        var all = await _contracts.ListAsync(cancellationToken).ConfigureAwait(false);
        var horizon = asAt.AddDays(expiryWindowDays);

        var overdue = new List<ContractObligationEntry>();
        var expiring = new List<string>();
        var staleExecuted = new List<string>();
        var awaitingAuthority = new List<string>();
        var unaccepted = new List<ContractDeliverableEntry>();

        foreach (var record in all)
        {
            var contract = record.Definition;

            // A superseded record is history: reporting its obligations
            // as live would double-count against the record that replaced it.
            if (record.ValidationState == ReferenceValidationState.Superseded)
                continue;

            overdue.AddRange(contract.OverdueObligations(asAt)
                .Select(o => new ContractObligationEntry(contract.Reference, o)));

            if (contract.Status == ContractStatus.Executed && contract.Term is { To: { } end })
            {
                if (end < asAt)
                    staleExecuted.Add(contract.Reference);
                else if (end <= horizon)
                    expiring.Add(contract.Reference);
            }

            if (ContractStatuses.HasBeenExecuted(contract.Status)
                && !contract.Governance.HasAuthority(BusinessAuthorityKind.CommercialCommitment))
                awaitingAuthority.Add(contract.Reference);

            if (contract.IsBinding)
                unaccepted.AddRange(contract.UnacceptedDeliverables
                    .Select(d => new ContractDeliverableEntry(contract.Reference, d)));
        }

        return new ContractObligationPosition(
            asAt,
            overdue.OrderBy(e => e.Obligation.DueBy).ThenBy(e => e.ContractReference, StringComparer.Ordinal).ToList(),
            expiring.OrderBy(r => r, StringComparer.Ordinal).ToList(),
            staleExecuted.OrderBy(r => r, StringComparer.Ordinal).ToList(),
            awaitingAuthority.OrderBy(r => r, StringComparer.Ordinal).ToList(),
            unaccepted.OrderBy(e => e.ContractReference, StringComparer.Ordinal).ToList());
    }
}
