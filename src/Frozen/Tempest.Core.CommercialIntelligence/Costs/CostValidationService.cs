using Tempest.Core.CommercialIntelligence.Suppliers;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Manufacturing;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.CommercialIntelligence.Costs;

/// <summary>Governance of the process-and-cost library itself.</summary>
public interface IProcessCostValidationService : IReferenceValidationService<ProcessCostRecord>
{
}

/// <summary>The concrete <see cref="IProcessCostValidationService"/> implementation.</summary>
/// <remarks>
/// <para>
/// The checks answer one question: could somebody read this figure in two
/// years and know what it was a price for? A cost with no basis, no
/// quantity band, no currency context or no source fails that, however
/// carefully the number itself was transcribed.
/// </para>
/// <para>
/// Nothing here has a view on whether a price is reasonable. What a
/// process should cost depends on a market this platform does not model.
/// </para>
/// </remarks>
public sealed class ProcessCostValidationService
    : ReferenceValidationService<ProcessCostRecord>, IProcessCostValidationService
{
    /// <summary>How far a component breakdown may fall short of the total before it is reported, as a proportion.</summary>
    /// <remarks>
    /// Generous on purpose. Suppliers routinely omit their own margin from
    /// a breakdown, so a breakdown summing to less than the total is
    /// normal; one summing to more than it is an error somebody made.
    /// </remarks>
    public const decimal ComponentSumTolerance = 0.02m;

    private readonly IProcessCostCatalog _costs;
    private readonly IProcessCatalog? _processes;
    private readonly ISupplierCatalog? _suppliers;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="ProcessCostValidationService"/> class.</summary>
    /// <param name="catalog">The cost library whose records this service validates.</param>
    /// <param name="processes">The `A7` manufacturing library, for confirming that a named process exists. Optional.</param>
    /// <param name="suppliers">The supplier database, for confirming that a named supplier exists. Optional.</param>
    /// <param name="timeProvider">The clock staleness checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public ProcessCostValidationService(
        IProcessCostCatalog catalog,
        IProcessCatalog? processes = null,
        ISupplierCatalog? suppliers = null,
        TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _costs = catalog;
        _processes = processes;
        _suppliers = suppliers;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        ProcessCostRecord definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Cost record '{definition.Reference}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        CommercialContextValidator.Evaluate(
            subject, definition.Applicability, definition.Source, Provenance(definition), today, errors, warnings);

        EvaluateFigure(definition, subject, errors, warnings);
        await EvaluateReferencesAsync(definition, subject, warnings, cancellationToken).ConfigureAwait(false);
        await EvaluateOverlapAsync(definition, subject, warnings, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override Task EvaluateRecordAsync(
        IReferenceRecord<ProcessCostRecord> record,
        IReadOnlyList<IReferenceRecord<ProcessCostRecord>>? library,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        // The record's own provenance is only reachable here, so the
        // context check that needs it runs against the registered record
        // rather than a bare definition.
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
        var quality = CommercialContextValidator.DeriveQuality(
            record.Definition.Applicability, record.Definition.Source, record.Provenance, today);

        if (quality == CommercialQuality.Incomplete)
            warnings.Add(Diagnostic(
                CommercialContextRules.SourceNotIdentified,
                $"Cost record '{record.Definition.Reference}' is {quality}: it cannot support a commercial decision until its "
                + "quantity basis, validity and source are all recorded."));

        return Task.CompletedTask;
    }

    private void EvaluateFigure(
        ProcessCostRecord definition,
        string subject,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
    {
        if (definition.Basis == CostBasis.Unspecified)
            errors.Add(Diagnostic(
                CostValidationRules.CostBasisMustBeStated,
                $"{subject} does not say what its figure is charged against. £40 per part and £40 per batch differ by the batch "
                + "size, and a figure with neither cannot be applied to anything."));

        if (definition.Cost.IsUnknown)
        {
            warnings.Add(Diagnostic(
                CostValidationRules.CostIsUnknown,
                $"{subject} records no figure. That is a legitimate way to say nobody has priced this, and it means the record "
                + "adds nothing to an estimate beyond marking the gap."));
        }
        else
        {
            if (definition.Cost.Lowest!.Value.IsZero)
                warnings.Add(Diagnostic(
                    CostValidationRules.CostIsZero,
                    $"{subject} records a cost of zero. If nobody has priced it, an unknown figure says so honestly; a zero "
                    + "silently makes every estimate containing it too cheap."));

            EvaluateCurrencyConsistency(definition, subject, errors);

            if (definition.MinimumCharge is { } minimum
                && !minimum.IsUnknown
                && minimum.Currency == definition.Currency
                && minimum.Highest!.Value <= definition.Cost.Lowest!.Value)
                warnings.Add(Diagnostic(
                    CostValidationRules.MinimumChargeIsIneffective,
                    $"{subject} has a minimum charge of {minimum} against a figure of {definition.Cost}, so the minimum can "
                    + "never bite."));

            EvaluateComponents(definition, subject, warnings);
        }

        if (definition.IsContradicted)
            warnings.Add(Diagnostic(
                CostValidationRules.CostIsContradicted,
                $"{subject} is contradicted by {string.Join(", ", definition.ContradictedBy)}. Two credible sources "
                + "disagreeing is as often a fact about the market as an error, and it needs a person either way."));
    }

    private void EvaluateCurrencyConsistency(ProcessCostRecord definition, string subject, List<IValidationDiagnostic> errors)
    {
        var currency = definition.Currency;

        foreach (var (name, figure) in new (string, CostFigure?)[]
                 {
                     ("minimum charge", definition.MinimumCharge),
                     ("setup cost", definition.SetupCost),
                     ("tooling cost", definition.ToolingCost),
                 })
        {
            if (figure is { IsUnknown: false } present && present.Currency != currency)
                errors.Add(Diagnostic(
                    CostValidationRules.CurrencyMustBeConsistent,
                    $"{subject} states its {name} in {present.Currency} against a figure in {currency}. Totalling them would "
                    + "need an exchange rate, which TempestOS does not hold."));
        }
    }

    private void EvaluateComponents(ProcessCostRecord definition, string subject, List<IValidationDiagnostic> warnings)
    {
        var priced = definition.Components.Where(c => !c.Amount.IsUnknown).ToList();

        if (priced.Count == 0 || priced.Any(c => c.Amount.Currency != definition.Currency))
            return;

        var componentTotal = priced.Sum(c => c.Amount.Lowest!.Value.Amount);
        var total = definition.Cost.Lowest!.Value.Amount;

        if (total == 0m)
            return;

        var overrun = (componentTotal - total) / total;

        if (overrun > ComponentSumTolerance)
            warnings.Add(Diagnostic(
                CostValidationRules.ComponentsDoNotSum,
                $"{subject}'s components total {componentTotal} against a figure of {total}. A breakdown summing to less than "
                + "the total is ordinary — suppliers omit their margin — but one summing to more is a transcription error."));
    }

    private async Task EvaluateReferencesAsync(
        ProcessCostRecord definition,
        string subject,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        if (definition.Applicability.ProcessRecordId is not { } processId)
        {
            warnings.Add(Diagnostic(
                CostValidationRules.ProcessNotIdentified,
                $"{subject} names no `A7` process, so nothing ties the figure to a piece of work anybody can look up."));
        }
        else if (_processes is not null
                 && await _processes.FindAsync(processId, cancellationToken).ConfigureAwait(false) is null)
        {
            warnings.Add(Diagnostic(
                CostValidationRules.ProcessMustResolve,
                $"{subject} names process '{processId}', which the `A7` manufacturing library does not hold."));
        }

        if (definition.Applicability.SupplierReference is { } supplier
            && _suppliers is not null
            && await _suppliers.FindByReferenceAsync(supplier, cancellationToken).ConfigureAwait(false) is null)
            warnings.Add(Diagnostic(
                CostValidationRules.SupplierMustResolve,
                $"{subject} names supplier '{supplier}', which the supplier database does not hold."));

        foreach (var contradiction in definition.ContradictedBy)
        {
            if (await _costs.FindByReferenceAsync(contradiction, cancellationToken).ConfigureAwait(false) is null)
                warnings.Add(Diagnostic(
                    CostValidationRules.ContradictionMustResolve,
                    $"{subject} names contradicting record '{contradiction}', which the cost library does not hold."));
        }
    }

    private async Task EvaluateOverlapAsync(
        ProcessCostRecord definition,
        string subject,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        if (definition.Applicability.Quantities is not { } band || definition.Applicability.ProcessRecordId is not { } process)
            return;

        var others = await _costs.ListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var other in others)
        {
            var candidate = other.Definition;

            if (string.Equals(candidate.ReferenceKey, definition.ReferenceKey, StringComparison.Ordinal))
                continue;

            if (other.ValidationState is ReferenceValidationState.Superseded or ReferenceValidationState.Draft)
                continue;

            if (!string.Equals(candidate.Applicability.ProcessRecordId, process, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.Equals(
                    candidate.Applicability.SupplierReference,
                    definition.Applicability.SupplierReference,
                    StringComparison.OrdinalIgnoreCase))
                continue;

            if (candidate.Applicability.Quantities is not { } otherBand || !band.Overlaps(otherBand))
                continue;

            if (candidate.Basis != definition.Basis)
                continue;

            warnings.Add(Diagnostic(
                CostValidationRules.OverlappingCostRecords,
                $"{subject} ({band}) covers the same process, supplier and basis as '{candidate.Reference}' ({otherBand}). "
                + "Two figures claiming the same quantity is a question nobody looking one up can answer."));
        }
    }

    /// <summary>
    /// The provenance the definition-level check runs against.
    /// </summary>
    /// <remarks>
    /// A bare definition carries none of its own — provenance belongs to
    /// the registered record — so the shared context check sees an empty
    /// one here and <see cref="EvaluateRecordAsync"/> supplies the real
    /// thing once the record exists. The consequence is that
    /// <c>ValidateDefinitionAsync</c> always reports the source as
    /// unidentified, which is correct: an unregistered definition has no
    /// source until somebody registers it with one.
    /// </remarks>
    private static ReferenceProvenance Provenance(ProcessCostRecord definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return ReferenceProvenance.Unknown;
    }
}
