using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Operating;

/// <summary>A deterministic filter over the operating-model library.</summary>
public sealed record OperatingScenarioQuery
{
    /// <summary>Matches any model whose reference, name or purpose contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches only the approved current model, or only the others. <see langword="null"/> to match any.</summary>
    public bool? IsCurrentModel { get; init; }

    /// <summary>Matches models whose period contains this date. <see langword="null"/> to match any.</summary>
    public DateOnly? CoveringDate { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The library of operating models.</summary>
public interface IOperatingScenarioCatalog : IReferenceDataCatalog<OperatingScenario>
{
    /// <summary>Returns the model registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<OperatingScenario>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered model matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<OperatingScenario>>> SearchAsync(
        OperatingScenarioQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IOperatingScenarioCatalog"/> implementation.</summary>
public sealed class OperatingScenarioCatalog : ReferenceDataCatalog<OperatingScenario>, IOperatingScenarioCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every operating-model record's own backing document carries.</summary>
    public const string OperatingScenarioDocumentKind = "BusinessOperatingModel";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>modelId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "BusinessOperatingModels.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each model reference to the <c>modelId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "BusinessOperatingModels.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="OperatingScenarioCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own model records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public OperatingScenarioCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "BusinessOperatingModels";

    /// <inheritdoc />
    public override string DocumentKind => OperatingScenarioDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<OperatingScenario>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(OperatingScenario.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<OperatingScenario>>> SearchAsync(
        OperatingScenarioQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(OperatingScenario definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(OperatingScenario definition) => $"Operating model reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<OperatingScenario> record, OperatingScenarioQuery query)
    {
        var model = record.Definition;

        if (query.TextContains is { } text
            && !model.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !model.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !model.Purpose.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.IsCurrentModel is { } current && model.IsCurrentModel != current)
            return false;

        if (query.CoveringDate is { } date && !model.Period.Period.Contains(date))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>The diagnostic codes C7's validation service reports.</summary>
public static class OperatingValidationRules
{
    /// <summary>The model records no resources, so it describes no capacity.</summary>
    public const string ModelMustHaveResources = "TEMPEST-BGO-001";

    /// <summary>Two resources in one model share a code.</summary>
    public const string DuplicateResourceCode = "TEMPEST-BGO-002";

    /// <summary>Two capabilities in one model share a code.</summary>
    public const string DuplicateCapabilityCode = "TEMPEST-BGO-003";

    /// <summary>A utilisation assumption leaves no room for anything going wrong.</summary>
    public const string UtilisationIsOptimistic = "TEMPEST-BGO-004";

    /// <summary>The modelled demand exceeds the capacity actually committed.</summary>
    public const string DemandExceedsCapacity = "TEMPEST-BGO-005";

    /// <summary>Exactly one person holds a capability the organisation sells.</summary>
    public const string CapabilityIsSinglePointOfFailure = "TEMPEST-BGO-006";

    /// <summary>The organisation sells work depending on a capability it does not have.</summary>
    public const string CapabilitySoldButNotHeld = "TEMPEST-BGO-007";

    /// <summary>A resource claims a capability the model does not declare.</summary>
    public const string ResourceClaimsUnknownCapability = "TEMPEST-BGO-008";

    /// <summary>The model records no assumptions. Every operating model rests on some.</summary>
    public const string AssumptionsShouldBeRecorded = "TEMPEST-BGO-009";

    /// <summary>A constraint names nobody answerable for it.</summary>
    public const string ConstraintHasNoOwner = "TEMPEST-BGO-010";

    /// <summary>A constraint has no route to relieving it.</summary>
    public const string ConstraintHasNoRelief = "TEMPEST-BGO-011";

    /// <summary>Two gates in one model share a code.</summary>
    public const string DuplicateGateCode = "TEMPEST-BGO-012";

    /// <summary>A gate has never been measured, so it can never fire.</summary>
    public const string GateHasNeverBeenMeasured = "TEMPEST-BGO-013";

    /// <summary>A gate's measurement is too old to rely on.</summary>
    public const string GateMeasurementIsStale = "TEMPEST-BGO-014";

    /// <summary>A gate's condition is met and somebody is being asked to decide.</summary>
    public const string GateConditionIsMet = "TEMPEST-BGO-015";

    /// <summary>A gate's own review is due.</summary>
    public const string GateReviewIsDue = "TEMPEST-BGO-016";

    /// <summary>Nobody has approved the model as the one the organisation operates to.</summary>
    public const string ModelIsNotApproved = "TEMPEST-BGO-017";

    /// <summary>Two operating models share one reference.</summary>
    public const string DuplicateModelReference = "TEMPEST-BGO-018";
}

/// <summary>Governance of operating models themselves.</summary>
public interface IOperatingScenarioValidationService : IReferenceValidationService<OperatingScenario>
{
}

/// <summary>The concrete <see cref="IOperatingScenarioValidationService"/> implementation.</summary>
/// <remarks>
/// <para>
/// A met decision gate is reported here as a finding, which is exactly the
/// right strength for it: something a person must look at, recorded on the
/// model, and never acted on by the system.
/// </para>
/// <para>
/// Nothing here decides whether to hire, subcontract or invest. The
/// findings say what the model shows — that demand exceeds committed
/// capacity, that one person holds a capability the organisation sells,
/// that a gate has been crossed — and stop.
/// </para>
/// </remarks>
public sealed class OperatingScenarioValidationService
    : ReferenceValidationService<OperatingScenario>, IOperatingScenarioValidationService
{
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="OperatingScenarioValidationService"/> class.</summary>
    /// <param name="catalog">The operating-model library whose records this service validates.</param>
    /// <param name="timeProvider">The clock gate status is judged against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public OperatingScenarioValidationService(IOperatingScenarioCatalog catalog, TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override Task EvaluateDefinitionAsync(
        OperatingScenario definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Operating model '{definition.Reference}' ({definition.Name})";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        BusinessGovernanceValidator.Evaluate(subject, definition.Governance, today, errors, warnings, expectEvidence: false);

        EvaluateResources(definition, subject, errors, warnings);
        EvaluateCapabilities(definition, subject, errors, warnings);
        EvaluateConstraints(definition, subject, warnings);
        EvaluateGates(definition, subject, today, errors, warnings);

        if (definition.Assumptions.Count == 0)
            warnings.Add(Diagnostic(
                OperatingValidationRules.AssumptionsShouldBeRecorded,
                $"{subject} records no assumptions. Every operating model rests on some; one with none recorded has not "
                + "identified them rather than not having any."));

        if (definition.DemandExceedsCommittedCapacity)
            warnings.Add(Diagnostic(
                OperatingValidationRules.DemandExceedsCapacity,
                $"{subject} is sized against {definition.DemandDaysPerPeriod} days of demand and commits "
                + $"{definition.CommittedProductiveDays} productive days. The difference is work the organisation cannot "
                + "currently do."));

        if (!definition.IsCurrentModel)
            warnings.Add(Diagnostic(
                OperatingValidationRules.ModelIsNotApproved,
                $"{subject} is not the approved current model. Expected of a scale case; reported so a model nobody approved is "
                + "never mistaken for the one the organisation operates to."));

        return Task.CompletedTask;
    }

    private void EvaluateResources(
        OperatingScenario definition,
        string subject,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
    {
        if (definition.Resources.Count == 0)
            errors.Add(Diagnostic(
                OperatingValidationRules.ModelMustHaveResources,
                $"{subject} records no resources, so it describes no capacity and nothing can be planned against it."));

        foreach (var duplicate in definition.Resources
                     .GroupBy(r => r.ResourceCode, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1)
                     .Select(g => g.Key))
            errors.Add(Diagnostic(
                OperatingValidationRules.DuplicateResourceCode,
                $"{subject} declares resource '{duplicate}' more than once, so its capacity is counted twice."));

        foreach (var resource in definition.Resources.Where(r => r.IsOptimisticUtilisation))
            warnings.Add(Diagnostic(
                OperatingValidationRules.UtilisationIsOptimistic,
                $"{subject} assumes '{resource.Name}' is {resource.UtilisationAssumption:P0} utilised, which leaves no room for "
                + "business development, admin, illness or work that overruns."));

        var declared = definition.Capabilities.Select(c => c.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var resource in definition.Resources)
        {
            foreach (var capability in resource.CapabilityCodes.Where(c => !declared.Contains(c)))
                warnings.Add(Diagnostic(
                    OperatingValidationRules.ResourceClaimsUnknownCapability,
                    $"{subject} says '{resource.Name}' holds capability '{capability}', which the model does not declare."));
        }
    }

    private void EvaluateCapabilities(
        OperatingScenario definition,
        string subject,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
    {
        foreach (var duplicate in definition.Capabilities
                     .GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1)
                     .Select(g => g.Key))
            errors.Add(Diagnostic(
                OperatingValidationRules.DuplicateCapabilityCode,
                $"{subject} declares capability '{duplicate}' more than once."));

        foreach (var capability in definition.KeyPersonCapabilities)
            warnings.Add(Diagnostic(
                OperatingValidationRules.CapabilityIsSinglePointOfFailure,
                $"{subject} shows '{capability.Name}' held by one person ('{capability.HeldBy[0]}'). Everything sold against it "
                + "stops if they do."));

        foreach (var capability in definition.MissingCapabilities)
            errors.Add(Diagnostic(
                OperatingValidationRules.CapabilitySoldButNotHeld,
                $"{subject} shows services depending on '{capability.Name}', which the organisation does not have."));
    }

    private void EvaluateConstraints(OperatingScenario definition, string subject, List<IValidationDiagnostic> warnings)
    {
        foreach (var constraint in definition.Constraints)
        {
            if (string.IsNullOrWhiteSpace(constraint.OwnerPrincipalId))
                warnings.Add(Diagnostic(
                    OperatingValidationRules.ConstraintHasNoOwner,
                    $"{subject} records constraint '{constraint.Code}' ({constraint.Kind}) with nobody answerable for it."));

            if (!constraint.HasReliefRoute)
                warnings.Add(Diagnostic(
                    OperatingValidationRules.ConstraintHasNoRelief,
                    $"{subject} records constraint '{constraint.Code}' with no route to relieving it, so it is a limit the "
                    + "organisation has accepted rather than one it is working on."));
        }
    }

    private void EvaluateGates(
        OperatingScenario definition,
        string subject,
        DateOnly today,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
    {
        foreach (var duplicate in definition.Gates
                     .GroupBy(g => g.CodeKey, StringComparer.Ordinal)
                     .Where(g => g.Count() > 1)
                     .Select(g => g.First().Code))
            errors.Add(Diagnostic(
                OperatingValidationRules.DuplicateGateCode,
                $"{subject} declares decision gate '{duplicate}' more than once."));

        foreach (var gate in definition.Gates)
        {
            switch (gate.StatusAt(today))
            {
                case GateStatus.NotMeasured:
                    warnings.Add(Diagnostic(
                        OperatingValidationRules.GateHasNeverBeenMeasured,
                        $"{subject} carries gate '{gate.Code}' that has never been measured, so it can never fire: {gate.Question}"));
                    break;

                case GateStatus.MeasurementStale:
                    warnings.Add(Diagnostic(
                        OperatingValidationRules.GateMeasurementIsStale,
                        $"{subject} carries gate '{gate.Code}' whose last measurement was {gate.MeasuredOn:O}, too old to rely on."));
                    break;

                case GateStatus.ConditionMet:
                    warnings.Add(Diagnostic(
                        OperatingValidationRules.GateConditionIsMet,
                        gate.Describe(today)));
                    break;
            }

            if (gate.IsReviewDueAt(today))
                warnings.Add(Diagnostic(
                    OperatingValidationRules.GateReviewIsDue,
                    $"{subject} carries gate '{gate.Code}', due for review on {gate.ReviewBy:O}, owned by "
                    + $"'{gate.DecisionOwnerPrincipalId}'."));
        }
    }
}
