using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Materials;

/// <summary>The concrete <see cref="IMaterialValidationService"/> implementation.</summary>
/// <remarks>
/// Provenance, verification attributability, supersession and standard
/// resolution are checked by
/// <see cref="ReferenceValidationService{TDefinition}"/>, shared with every
/// Group A library. Everything below is materials engineering: the
/// dimension a named property must carry, the values physics forbids, the
/// relationships between properties that cannot both hold, and the
/// type-aware applicability <see cref="MaterialFamilyTraits"/> defines.
/// </remarks>
public sealed class MaterialValidationService : ReferenceValidationService<MaterialDefinition>, IMaterialValidationService
{
    /// <summary>Properties whose physical meaning forbids a negative value.</summary>
    private static readonly IReadOnlySet<string> NonNegative = new HashSet<string>(StringComparer.Ordinal)
    {
        MaterialPropertyNames.ElongationAtBreak,
        MaterialPropertyNames.ImpactEnergy,
    };

    /// <summary>Properties whose physical meaning requires a value strictly above zero.</summary>
    private static readonly IReadOnlySet<string> StrictlyPositive = new HashSet<string>(StringComparer.Ordinal)
    {
        MaterialPropertyNames.Density,
        MaterialPropertyNames.YoungsModulus,
        MaterialPropertyNames.ShearModulus,
        MaterialPropertyNames.YieldStrength,
        MaterialPropertyNames.UltimateTensileStrength,
        MaterialPropertyNames.CompressiveStrength,
        MaterialPropertyNames.FatigueStrength,
        MaterialPropertyNames.ThermalConductivity,
        MaterialPropertyNames.SpecificHeatCapacity,
    };

    /// <summary>
    /// Initialises a new instance of the <see cref="MaterialValidationService"/> class.
    /// </summary>
    /// <param name="catalog">The catalogue whose records this service validates.</param>
    /// <param name="standardResolver">Resolves a cited standard against the Standards Library. Optional.</param>
    public MaterialValidationService(IMaterialCatalog catalog, IStandardResolver? standardResolver = null)
        : base(catalog, materialCatalog: null, standardResolver)
    {
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        MaterialDefinition definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        EvaluateClassification(definition, errors, warnings);
        EvaluateProperties(definition, errors, warnings);
        EvaluatePropertyRelationships(definition, errors);

        await EvaluateStandardReferencesAsync(definition.Standards, warnings, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task EvaluateRecordAsync(
        IReferenceRecord<MaterialDefinition> record,
        IReadOnlyList<IReferenceRecord<MaterialDefinition>>? library,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var key = record.Definition.DesignationKey;
        if (key is null)
            return;

        // Defence in depth: the catalogue already prevents this at write
        // time. Confirming it on read catches an index written before that
        // guard existed, or corrupted since.
        var others = library ?? await Catalog.ListAsync(cancellationToken).ConfigureAwait(false);
        var collisions = others
            .Where(other => !string.Equals(other.Id, record.Id, StringComparison.Ordinal))
            .Where(other => string.Equals(other.Definition.DesignationKey, key, StringComparison.Ordinal))
            .Select(other => other.Id)
            .ToList();

        if (collisions.Count > 0)
            errors.Add(Diagnostic(
                MaterialValidationRules.DuplicateDesignation,
                $"Designation '{record.Definition.Designation}' is also registered as: {string.Join(", ", collisions)}."));
    }

    private static void EvaluateClassification(MaterialDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        if (definition.Family == MaterialFamily.Unspecified)
            errors.Add(Diagnostic(
                MaterialValidationRules.FamilyMustBeStated,
                "The record states no material family, so nothing else on it can be interpreted."));

        if (definition.Family == MaterialFamily.Other && string.IsNullOrWhiteSpace(definition.SourceClassification))
            errors.Add(Diagnostic(
                MaterialValidationRules.OtherFamilyNeedsSourceClassification,
                "A material classified 'Other' must record the source's own classification wording in SourceClassification."));

        if (string.IsNullOrWhiteSpace(definition.Designation))
            warnings.Add(Diagnostic(
                MaterialValidationRules.DesignationShouldBeRecorded,
                "The record carries a name but no material designation."));

        if (definition.Condition is not null
            && MaterialFamilyTraits.IsApplicabilityKnown(definition.Family)
            && !MaterialFamilyTraits.HasHeatTreatmentCondition(definition.Family))
            warnings.Add(Diagnostic(
                MaterialValidationRules.ConditionNotApplicableToFamily,
                $"A heat-treatment condition is recorded, but a {definition.Family} material has none."));

        if (definition.Properties.Count == 0)
            warnings.Add(Diagnostic(
                MaterialValidationRules.NoPropertiesRecorded,
                "No engineering property of any kind is recorded — a data gap, not a material with no properties."));
    }

    private static void EvaluateProperties(MaterialDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        foreach (var (name, property) in definition.Properties)
        {
            // The controlled vocabulary's whole purpose: a density
            // recorded as a pressure is caught, and an unknown name is
            // still perfectly legitimate.
            var expected = MaterialPropertyNames.ExpectedDimensionOf(name);
            if (expected is not null && !string.Equals(expected, property.DimensionName, StringComparison.Ordinal))
            {
                errors.Add(Diagnostic(
                    MaterialValidationRules.PropertyDimensionMismatch,
                    $"Property '{name}' must be a {expected}, but is recorded as a {property.DimensionName}."));
                continue;
            }

            if (property.IsDerived)
                warnings.Add(Diagnostic(
                    ReferenceValidationRules.DerivedValuePresent,
                    $"Property '{name}' is derived by TempestOS and must not be presented as source reference data."));

            if (StrictlyPositive.Contains(name) && property.CanonicalValue <= 0)
                errors.Add(Diagnostic(
                    MaterialValidationRules.PropertyMustBePositive,
                    $"Property '{name}' is {property.Value}; a recorded value here must be greater than zero."));

            if (NonNegative.Contains(name) && property.CanonicalValue < 0)
                errors.Add(Diagnostic(
                    MaterialValidationRules.PropertyMustNotBeNegative,
                    $"Property '{name}' is {property.Value}; this property cannot be negative."));

            if (name == MaterialPropertyNames.PoissonsRatio && property.CanonicalValue is <= -1.0 or >= 0.5)
                errors.Add(Diagnostic(
                    MaterialValidationRules.PoissonsRatioOutOfRange,
                    $"Poisson's ratio is {property.Value}; a real material's own value lies above -1 and below 0.5."));

            if (name == MaterialPropertyNames.YieldStrength
                && MaterialFamilyTraits.IsApplicabilityKnown(definition.Family)
                && !MaterialFamilyTraits.HasYieldStrength(definition.Family))
                errors.Add(Diagnostic(
                    MaterialValidationRules.YieldStrengthNotApplicableToFamily,
                    $"A yield strength is recorded, but a {definition.Family} material fails without a yield point."));
        }
    }

    private static void EvaluatePropertyRelationships(MaterialDefinition definition, List<IValidationDiagnostic> errors)
    {
        if (TryCanonical(definition, MaterialPropertyNames.YieldStrength, out var yield)
            && TryCanonical(definition, MaterialPropertyNames.UltimateTensileStrength, out var ultimate)
            && yield > ultimate)
            errors.Add(Diagnostic(
                MaterialValidationRules.YieldStrengthExceedsUltimate,
                "Yield strength exceeds ultimate tensile strength; no real material yields above the stress that breaks it."));

        if (TryCanonical(definition, MaterialPropertyNames.MinimumServiceTemperature, out var minimum)
            && TryCanonical(definition, MaterialPropertyNames.MaximumServiceTemperature, out var maximum)
            && minimum > maximum)
            errors.Add(Diagnostic(
                MaterialValidationRules.ServiceTemperatureRangeInverted,
                "The minimum service temperature is above the maximum."));
    }

    private static bool TryCanonical(MaterialDefinition definition, string propertyName, out double canonical)
    {
        if (definition.Properties.TryGetValue(propertyName, out var property)
            && string.Equals(MaterialPropertyNames.ExpectedDimensionOf(propertyName), property.DimensionName, StringComparison.Ordinal))
        {
            canonical = property.CanonicalValue;
            return true;
        }

        canonical = default;
        return false;
    }
}
