using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Constants;

/// <summary>The concrete <see cref="IConstantValidationService"/> implementation.</summary>
/// <remarks>
/// Provenance, verification attributability, supersession and cited-standard
/// resolution are checked by
/// <see cref="ReferenceValidationService{TDefinition}"/>, shared with every
/// Group A library. Everything below is about being a constant: having a
/// dimensioned value at all, saying honestly how well it is known, and
/// carrying a dimension its own category permits.
/// </remarks>
public sealed class ConstantValidationService : ReferenceValidationService<ConstantDefinition>, IConstantValidationService
{
    /// <summary>The dimension name a dimensionless quantity carries.</summary>
    private const string DimensionlessName = "Dimensionless";

    /// <summary>
    /// Initialises a new instance of the <see cref="ConstantValidationService"/> class.
    /// </summary>
    /// <param name="catalog">The library whose records this service validates.</param>
    /// <param name="standardResolver">Resolves a cited standard against the Standards Library. Optional.</param>
    public ConstantValidationService(IConstantCatalog catalog, IStandardResolver? standardResolver = null)
        : base(catalog, materialCatalog: null, standardResolver)
    {
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        ConstantDefinition definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        EvaluateClassification(definition, errors, warnings);
        EvaluateValue(definition, errors, warnings);
        EvaluateUncertainty(definition, errors, warnings);

        await EvaluateStandardReferencesAsync(definition.Standards, warnings, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task EvaluateRecordAsync(
        IReferenceRecord<ConstantDefinition> record,
        IReadOnlyList<IReferenceRecord<ConstantDefinition>>? library,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var key = record.Definition.SymbolKey;
        var others = (library ?? await Catalog.ListAsync(cancellationToken).ConfigureAwait(false))
            .Where(other => !string.Equals(other.Id, record.Id, StringComparison.Ordinal))
            .ToList();

        // Defence in depth: the catalogue already prevents this at write
        // time. Confirming it on read catches an index written before that
        // guard existed, or corrupted since.
        var collisions = others
            .Where(other => string.Equals(other.Definition.SymbolKey, key, StringComparison.Ordinal))
            .Select(other => other.Id)
            .ToList();

        if (collisions.Count > 0)
            errors.Add(Diagnostic(
                ConstantValidationRules.DuplicateSymbol,
                $"Symbol '{record.Definition.Symbol}' is also registered as: {string.Join(", ", collisions)}."));

        // The index cannot catch this one: an alternative symbol is not a
        // key, so two records can legitimately hold it — but a reader
        // looking the symbol up would then find one record by its primary
        // symbol and another claiming the same name.
        var shadowed = others
            .Where(other => other.Definition.AlternativeSymbols.Any(alternative =>
                string.Equals(ConstantDefinition.SymbolKeyFor(alternative), key, StringComparison.Ordinal)))
            .Select(other => other.Id)
            .ToList();

        if (shadowed.Count > 0)
            warnings.Add(Diagnostic(
                ConstantValidationRules.SymbolCollidesWithAnAlternative,
                $"Symbol '{record.Definition.Symbol}' is also listed as an alternative symbol by: {string.Join(", ", shadowed)}."));
    }

    private static void EvaluateClassification(ConstantDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        if (definition.Category == ConstantCategory.Unspecified)
            warnings.Add(Diagnostic(
                ConstantValidationRules.CategoryShouldBeStated,
                $"Constant '{definition.Symbol}' states no category, so where its authority comes from cannot be determined."));

        if (definition.Category == ConstantCategory.Other && string.IsNullOrWhiteSpace(definition.SourceClassification))
            errors.Add(Diagnostic(
                ConstantValidationRules.OtherCategoryNeedsSourceClassification,
                $"Constant '{definition.Symbol}' is categorised 'Other' but records none of the source's own classification wording in SourceClassification."));

        if (ConstantCategories.ExpectsApplicability(definition.Category) && string.IsNullOrWhiteSpace(definition.Applicability))
            warnings.Add(Diagnostic(
                ConstantValidationRules.ApplicabilityShouldBeRecorded,
                $"A {definition.Category} constant is true only within the convention that adopted it, but '{definition.Symbol}' records no statement of where it applies."));
    }

    private static void EvaluateValue(ConstantDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        if (definition.Value is not { } value)
        {
            errors.Add(Diagnostic(
                ConstantValidationRules.ValueMustBeRecorded,
                $"Constant '{definition.Symbol}' records no value. A constant without a value is not a constant."));
            return;
        }

        if (value.IsDerived)
            warnings.Add(Diagnostic(
                ReferenceValidationRules.DerivedValuePresent,
                $"The value of '{definition.Symbol}' was derived by TempestOS and must not be presented as a published constant."));

        if (ConstantCategories.IsAlwaysDimensionless(definition.Category)
            && !string.Equals(value.DimensionName, DimensionlessName, StringComparison.Ordinal))
            errors.Add(Diagnostic(
                ConstantValidationRules.MathematicalConstantMustBeDimensionless,
                $"Constant '{definition.Symbol}' is a mathematical constant but its value is a {value.DimensionName}."));
    }

    private static void EvaluateUncertainty(ConstantDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        var uncertainty = definition.Uncertainty;

        if (uncertainty.Kind == ConstantUncertaintyKind.NotRecorded)
        {
            // Deliberately still reported for a category that is exact by
            // nature: "exact" is a claim the record should make, not one
            // the reader should infer from the category.
            warnings.Add(Diagnostic(
                ConstantValidationRules.UncertaintyShouldBeRecorded,
                $"Constant '{definition.Symbol}' says nothing about how well its value is known. "
                + (ConstantCategories.IsExactByNature(definition.Category)
                    ? "A constant of this category is normally exact — record that explicitly rather than leaving it to be inferred."
                    : "Not recorded is never the same as exact.")));
            return;
        }

        if (uncertainty.IsExact && uncertainty.StatesAFigure)
            errors.Add(Diagnostic(
                ConstantValidationRules.ExactConstantCarriesUncertainty,
                $"Constant '{definition.Symbol}' is recorded as exact but also carries an uncertainty figure; both cannot be true."));

        if (uncertainty.Absolute is { } absolute)
        {
            if (absolute.CanonicalValue < 0)
                errors.Add(Diagnostic(
                    ConstantValidationRules.UncertaintyMustNotBeNegative,
                    $"The uncertainty on '{definition.Symbol}' is {absolute.Value}; an uncertainty describes a magnitude and cannot be negative."));

            if (definition.Value is { } value && !string.Equals(absolute.DimensionName, value.DimensionName, StringComparison.Ordinal))
                errors.Add(Diagnostic(
                    ConstantValidationRules.UncertaintyDimensionMismatch,
                    $"The uncertainty on '{definition.Symbol}' is a {absolute.DimensionName}, but the value it qualifies is a {value.DimensionName}."));
        }

        if (uncertainty.Relative is { } relative)
        {
            if (relative < 0)
                errors.Add(Diagnostic(
                    ConstantValidationRules.UncertaintyMustNotBeNegative,
                    $"The relative uncertainty on '{definition.Symbol}' is {relative}; it cannot be negative."));
            else if (relative >= 1.0)
                errors.Add(Diagnostic(
                    ConstantValidationRules.RelativeUncertaintyImplausible,
                    $"The relative uncertainty on '{definition.Symbol}' is {relative}, which says the value is not known at all. Check whether a percentage was recorded as a fraction."));
        }

        if (uncertainty.Kind == ConstantUncertaintyKind.Expanded && uncertainty.CoverageFactor is null)
            warnings.Add(Diagnostic(
                ConstantValidationRules.ExpandedUncertaintyNeedsCoverageFactor,
                $"The uncertainty on '{definition.Symbol}' is expanded but records no coverage factor, so what it expands by is unknown."));

        if (uncertainty.CoverageFactor is { } factor && factor <= 0)
            errors.Add(Diagnostic(
                ConstantValidationRules.CoverageFactorMustBePositive,
                $"The coverage factor on '{definition.Symbol}' is {factor}; it must be greater than zero."));
    }
}
