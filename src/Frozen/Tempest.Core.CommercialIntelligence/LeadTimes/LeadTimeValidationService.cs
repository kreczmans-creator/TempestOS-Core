using Tempest.Core.CommercialIntelligence.Suppliers;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Manufacturing;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.CommercialIntelligence.LeadTimes;

/// <summary>Governance of the lead-time library itself.</summary>
public interface ILeadTimeValidationService : IReferenceValidationService<LeadTimeRecord>
{
}

/// <summary>The concrete <see cref="ILeadTimeValidationService"/> implementation.</summary>
/// <remarks>
/// The findings are about what a figure can be used for. A lead time with
/// no kind might be a contractual commitment or somebody's guess; a
/// historical average over two orders is not the evidence its label
/// suggests; a quoted figure with no quotation behind it cannot be held
/// to. None of these is a wrong number.
/// </remarks>
public sealed class LeadTimeValidationService : ReferenceValidationService<LeadTimeRecord>, ILeadTimeValidationService
{
    /// <summary>How few observations make a historical figure worth qualifying.</summary>
    public const int SmallSampleThreshold = 3;

    /// <summary>The longest lead time treated as plausible, in calendar days.</summary>
    /// <remarks>
    /// Three years. Long-lead castings and forgings genuinely run to many
    /// months, so the threshold is set well beyond anything ordinary and
    /// exists to catch a units mistake — twelve entered as months where
    /// weeks were meant — rather than to second-guess a supplier.
    /// </remarks>
    public const int ImplausibleAfterCalendarDays = 1095;

    private readonly ILeadTimeCatalog _leadTimes;
    private readonly IProcessCatalog? _processes;
    private readonly ISupplierCatalog? _suppliers;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="LeadTimeValidationService"/> class.</summary>
    /// <param name="catalog">The lead-time library whose records this service validates.</param>
    /// <param name="processes">The `A7` manufacturing library, for confirming that a named process exists. Optional.</param>
    /// <param name="suppliers">The supplier database, for confirming that a named supplier exists. Optional.</param>
    /// <param name="timeProvider">The clock staleness checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public LeadTimeValidationService(
        ILeadTimeCatalog catalog,
        IProcessCatalog? processes = null,
        ISupplierCatalog? suppliers = null,
        TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _leadTimes = catalog;
        _processes = processes;
        _suppliers = suppliers;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        LeadTimeRecord definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Lead-time record '{definition.Reference}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        // A lead time is meaningful without a quantity band far more often
        // than a price is — many processes take the same time for one part
        // as for ten — so the shared check runs with the basis optional.
        CommercialContextValidator.Evaluate(
            subject,
            definition.Applicability,
            definition.Source,
            ReferenceProvenance.Unknown,
            today,
            errors,
            warnings,
            requiresQuantityBasis: false);

        EvaluateFigures(definition, subject, errors, warnings);
        EvaluateKind(definition, subject, warnings);
        await EvaluateReferencesAsync(definition, subject, warnings, cancellationToken).ConfigureAwait(false);
        await EvaluateOverlapAsync(definition, subject, warnings, cancellationToken).ConfigureAwait(false);
    }

    private void EvaluateFigures(
        LeadTimeRecord definition,
        string subject,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
    {
        if (!definition.Typical.IsSpecified)
            errors.Add(Diagnostic(
                LeadTimeValidationRules.UnitMustBeStated,
                $"{subject} states {definition.Typical.Amount} without a unit, so nobody can tell whether it means days, "
                + "weeks or months."));

        if (definition.Typical.Amount == 0m)
            warnings.Add(Diagnostic(
                LeadTimeValidationRules.LeadTimeIsZero,
                $"{subject} records a lead time of zero. Ex-stock supply is real; a zero recorded against a manufacturing "
                + "process is usually a figure nobody filled in."));

        if (!definition.BoundsAreConsistent)
            errors.Add(Diagnostic(
                LeadTimeValidationRules.BoundsAreInconsistent,
                $"{subject} states a minimum of {definition.Minimum?.ToString() ?? "none"}, a typical of "
                + $"{definition.Typical}, and a maximum of {definition.Maximum?.ToString() ?? "none"}. Either they are out of "
                + "order, or they are in units that cannot be compared without a calendar."));

        if (definition.Typical.ToElapsed() is { } elapsed
            && elapsed.ConvertTo(UnitsAndQuantities.DurationUnits.Day).Value > ImplausibleAfterCalendarDays)
            warnings.Add(Diagnostic(
                LeadTimeValidationRules.LeadTimeIsImplausible,
                $"{subject} records a typical lead time of {definition.Typical}, over three years. That is possible for a "
                + "long-lead casting and is far more often a units mistake."));

        if (definition.Assumptions.Count == 0 && definition.Excludes.Count == 0)
            warnings.Add(Diagnostic(
                LeadTimeValidationRules.ConditionsNotStated,
                $"{subject} states neither what it assumes nor what it excludes. Whether the figure covers tooling, material "
                + "procurement, inspection and carriage changes it entirely."));
    }

    private void EvaluateKind(LeadTimeRecord definition, string subject, List<IValidationDiagnostic> warnings)
    {
        if (definition.Kind == LeadTimeKind.Unspecified)
        {
            warnings.Add(Diagnostic(
                LeadTimeValidationRules.KindMustBeStated,
                $"{subject} does not say where its figure came from. A reader cannot tell a contractual commitment from "
                + "somebody's guess, and will assume the stronger of the two."));

            return;
        }

        if (definition.Kind == LeadTimeKind.Historical)
        {
            if (definition.ObservationCount is null)
                warnings.Add(Diagnostic(
                    LeadTimeValidationRules.HistoricalNeedsObservationCount,
                    $"{subject} is a historical figure and does not say how many orders it is drawn from. An average over two "
                    + "and one over forty are different kinds of evidence."));
            else if (definition.ObservationCount < SmallSampleThreshold)
                warnings.Add(Diagnostic(
                    LeadTimeValidationRules.HistoricalSampleIsSmall,
                    $"{subject} is a historical figure drawn from {definition.ObservationCount} order(s), which is too few to "
                    + "be treated as a pattern."));
        }

        if (definition.Kind is LeadTimeKind.Quoted or LeadTimeKind.Committed)
        {
            if (!definition.Applicability.IsSupplierSpecific)
                warnings.Add(Diagnostic(
                    LeadTimeValidationRules.SupplierFigureNeedsSupplier,
                    $"{subject} is recorded as {definition.Kind} and names no supplier. Somebody quoted or committed to it; "
                    + "the record does not say who."));

            if (string.IsNullOrWhiteSpace(definition.SourceDocumentReference))
                warnings.Add(Diagnostic(
                    LeadTimeValidationRules.SourceDocumentMissing,
                    $"{subject} is recorded as {definition.Kind} with no quotation or order behind it, so the supplier could "
                    + "not be held to it."));
        }
    }

    private async Task EvaluateReferencesAsync(
        LeadTimeRecord definition,
        string subject,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        if (definition.Applicability.ProcessRecordId is { } processId
            && _processes is not null
            && await _processes.FindAsync(processId, cancellationToken).ConfigureAwait(false) is null)
            warnings.Add(Diagnostic(
                LeadTimeValidationRules.ProcessMustResolve,
                $"{subject} names process '{processId}', which the `A7` manufacturing library does not hold."));

        if (definition.Applicability.SupplierReference is { } supplier
            && _suppliers is not null
            && await _suppliers.FindByReferenceAsync(supplier, cancellationToken).ConfigureAwait(false) is null)
            warnings.Add(Diagnostic(
                LeadTimeValidationRules.SupplierMustResolve,
                $"{subject} names supplier '{supplier}', which the supplier database does not hold."));
    }

    private async Task EvaluateOverlapAsync(
        LeadTimeRecord definition,
        string subject,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var others = await _leadTimes.ListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var other in others)
        {
            var candidate = other.Definition;

            if (string.Equals(candidate.ReferenceKey, definition.ReferenceKey, StringComparison.Ordinal))
                continue;

            if (other.ValidationState is ReferenceValidationState.Superseded or ReferenceValidationState.Draft)
                continue;

            if (candidate.Kind != definition.Kind)
                continue;

            if (!string.Equals(
                    candidate.Applicability.ProcessRecordId,
                    definition.Applicability.ProcessRecordId,
                    StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.Equals(
                    candidate.Applicability.SupplierReference,
                    definition.Applicability.SupplierReference,
                    StringComparison.OrdinalIgnoreCase))
                continue;

            var bandsCollide = (definition.Applicability.Quantities, candidate.Applicability.Quantities) switch
            {
                (null, null) => true,
                ({ } mine, { } theirs) => mine.Overlaps(theirs),
                _ => false,
            };

            if (!bandsCollide)
                continue;

            warnings.Add(Diagnostic(
                LeadTimeValidationRules.OverlappingLeadTimeRecords,
                $"{subject} covers the same supplier, process, quantities and kind as '{candidate.Reference}'. Two figures of "
                + "the same standing for one question is a disagreement, and it should be recorded as one."));
        }
    }
}
