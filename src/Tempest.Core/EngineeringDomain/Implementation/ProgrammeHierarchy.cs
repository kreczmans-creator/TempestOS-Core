using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringData;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringDomain;

public sealed class Portfolio : EngineeringObjectBase, IPortfolio, IRehydratable<Portfolio>
{
    public IReadOnlyList<Guid> ProgrammeIds { get; }

    public Portfolio(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, IReadOnlyList<Guid>? programmeIds = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        ProgrammeIds = programmeIds ?? Array.Empty<Guid>();
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state) =>
        WriteGuidList(state, nameof(ProgrammeIds), ProgrammeIds);

    static Portfolio IRehydratable<Portfolio>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata, state.TypeGuidList(nameof(ProgrammeIds)));
}

public sealed class Programme : EngineeringObjectBase, IProgramme, IRehydratable<Programme>
{
    public Guid? PortfolioId { get; }
    public IReadOnlyList<Guid> ProjectIds { get; }

    public Programme(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata,
        Guid? portfolioId = null, IReadOnlyList<Guid>? projectIds = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        PortfolioId = portfolioId;
        ProjectIds = projectIds ?? Array.Empty<Guid>();
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        state[nameof(PortfolioId)] = PortfolioId?.ToString();
        WriteGuidList(state, nameof(ProjectIds), ProjectIds);
    }

    static Programme IRehydratable<Programme>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata,
            state.TypeGuid(nameof(PortfolioId)), state.TypeGuidList(nameof(ProjectIds)));
}

/// <summary>
/// A project. Carries its own commercial core (`WP 19.0A`, `ADR-0150`)
/// exactly as <c>Evidence</c> carries its own facets — state via
/// <see cref="CaptureTypeState"/>/<see cref="ApplyTypeState"/>/
/// <see cref="IRehydratable{Project}.Rehydrate"/>, each field written by
/// its own <c>ProjectCommercialService</c> act, one transaction with an
/// audit row.
/// </summary>
public sealed class Project : EngineeringObjectBase, IProject, IRehydratable<Project>
{
    public Guid? ProgrammeId { get; }

    private string? _clientOrganisationId;
    private string? _purchaseOrderReference;
    private Money? _budget;
    private ReferencePin? _rateCardPin;
    private DateOnly? _startDate;
    private DateOnly? _targetDate;
    private string? _projectManagerIdentityId;

    public Project(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, Guid? programmeId = null,
        string? clientOrganisationId = null, string? purchaseOrderReference = null, Money? budget = null,
        ReferencePin? rateCardPin = null, DateOnly? startDate = null, DateOnly? targetDate = null,
        string? projectManagerIdentityId = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        ProgrammeId = programmeId;
        _clientOrganisationId = clientOrganisationId;
        _purchaseOrderReference = purchaseOrderReference;
        _budget = budget;
        _rateCardPin = rateCardPin;
        _startDate = startDate;
        _targetDate = targetDate;
        _projectManagerIdentityId = projectManagerIdentityId;
    }

    /// <inheritdoc />
    public string? ClientOrganisationId => _clientOrganisationId;

    /// <inheritdoc />
    public string? PurchaseOrderReference => _purchaseOrderReference;

    /// <inheritdoc />
    public Money? Budget => _budget;

    /// <inheritdoc />
    public ReferencePin? RateCardPin => _rateCardPin;

    /// <inheritdoc />
    public DateOnly? StartDate => _startDate;

    /// <inheritdoc />
    public DateOnly? TargetDate => _targetDate;

    /// <inheritdoc />
    public string? ProjectManagerIdentityId => _projectManagerIdentityId;

    /// <summary>Sets, or clears, the client this project is for (`ProjectCommercialService.SetClientAsync`). A tag, never validated as a structure.</summary>
    internal Task SetClientAsync(string? clientOrganisationId, CancellationToken cancellationToken = default) =>
        MutateTypeStateAndPersistAsync(
            () => new Dictionary<string, string?>(StringComparer.Ordinal) { [nameof(ClientOrganisationId)] = clientOrganisationId },
            () => _clientOrganisationId = clientOrganisationId,
            clientOrganisationId is { } id ? $"Client set to organisation '{id}'." : "Client cleared.",
            cancellationToken);

    /// <summary>Sets, or clears, the client's own purchase-order reference (`ProjectCommercialService.SetPurchaseOrderAsync`).</summary>
    internal Task SetPurchaseOrderAsync(string? purchaseOrderReference, CancellationToken cancellationToken = default) =>
        MutateTypeStateAndPersistAsync(
            () => new Dictionary<string, string?>(StringComparer.Ordinal) { [nameof(PurchaseOrderReference)] = purchaseOrderReference },
            () => _purchaseOrderReference = purchaseOrderReference,
            purchaseOrderReference is { } reference ? $"Purchase order set to '{reference}'." : "Purchase order cleared.",
            cancellationToken);

    /// <summary>Sets, or clears, the project's own budget (`ProjectCommercialService.SetBudgetAsync`).</summary>
    internal Task SetBudgetAsync(Money? budget, CancellationToken cancellationToken = default) =>
        MutateTypeStateAndPersistAsync(
            () =>
            {
                var state = new Dictionary<string, string?>(StringComparer.Ordinal);
                WriteJson(state, nameof(Budget), budget);
                return state;
            },
            () => _budget = budget,
            budget is { } amount ? $"Budget set to {amount}." : "Budget cleared.",
            cancellationToken);

    /// <summary>Pins the Released rate card this project bills against (`ProjectCommercialService.PinRateCardAsync`); permission (the card must be Released) is that service's own concern, not this mutator's.</summary>
    internal Task PinRateCardAsync(ReferencePin rateCardPin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rateCardPin);

        return MutateTypeStateAndPersistAsync(
            () =>
            {
                var state = new Dictionary<string, string?>(StringComparer.Ordinal);
                WriteJson(state, nameof(RateCardPin), rateCardPin);
                return state;
            },
            () => _rateCardPin = rateCardPin,
            $"Rate card pinned: '{rateCardPin}'.",
            cancellationToken);
    }

    /// <summary>Sets the project's own start and target dates (`ProjectCommercialService.SetDatesAsync`).</summary>
    internal Task SetDatesAsync(DateOnly? startDate, DateOnly? targetDate, CancellationToken cancellationToken = default) =>
        MutateTypeStateAndPersistAsync(
            () =>
            {
                var state = new Dictionary<string, string?>(StringComparer.Ordinal);
                WriteJson(state, nameof(StartDate), startDate);
                WriteJson(state, nameof(TargetDate), targetDate);
                return state;
            },
            () =>
            {
                _startDate = startDate;
                _targetDate = targetDate;
            },
            $"Dates set: start {(startDate is { } s ? s.ToString("O") : "(none)")}, target {(targetDate is { } t ? t.ToString("O") : "(none)")}.",
            cancellationToken);

    /// <summary>Sets, or clears, the principal managing this project (`ProjectCommercialService.SetProjectManagerAsync`).</summary>
    internal Task SetProjectManagerAsync(string? projectManagerIdentityId, CancellationToken cancellationToken = default) =>
        MutateTypeStateAndPersistAsync(
            () => new Dictionary<string, string?>(StringComparer.Ordinal) { [nameof(ProjectManagerIdentityId)] = projectManagerIdentityId },
            () => _projectManagerIdentityId = projectManagerIdentityId,
            projectManagerIdentityId is { } pm ? $"Project manager set to '{pm}'." : "Project manager cleared.",
            cancellationToken);

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        state[nameof(ProgrammeId)] = ProgrammeId?.ToString();
        state[nameof(ClientOrganisationId)] = _clientOrganisationId;
        state[nameof(PurchaseOrderReference)] = _purchaseOrderReference;
        WriteJson(state, nameof(Budget), _budget);
        WriteJson(state, nameof(RateCardPin), _rateCardPin);
        WriteJson(state, nameof(StartDate), _startDate);
        WriteJson(state, nameof(TargetDate), _targetDate);
        state[nameof(ProjectManagerIdentityId)] = _projectManagerIdentityId;
    }

    /// <inheritdoc />
    protected override void ApplyTypeState(EngineeringObjectState state)
    {
        _clientOrganisationId = state.Type(nameof(ClientOrganisationId));
        _purchaseOrderReference = state.Type(nameof(PurchaseOrderReference));
        _budget = state.TypeJson<Money>(nameof(Budget));
        _rateCardPin = state.TypeJson<ReferencePin>(nameof(RateCardPin));
        _startDate = state.TypeJson<DateOnly>(nameof(StartDate));
        _targetDate = state.TypeJson<DateOnly>(nameof(TargetDate));
        _projectManagerIdentityId = state.Type(nameof(ProjectManagerIdentityId));
    }

    static Project IRehydratable<Project>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata, state.TypeGuid(nameof(ProgrammeId)),
            state.Type(nameof(ClientOrganisationId)), state.Type(nameof(PurchaseOrderReference)), state.TypeJson<Money>(nameof(Budget)),
            state.TypeJson<ReferencePin>(nameof(RateCardPin)), state.TypeJson<DateOnly>(nameof(StartDate)), state.TypeJson<DateOnly>(nameof(TargetDate)),
            state.Type(nameof(ProjectManagerIdentityId)));
}
