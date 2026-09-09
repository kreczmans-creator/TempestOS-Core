using Tempest.Core.ReferenceData;

namespace Tempest.Core.Materials;

/// <summary>The stable property keys a material comparison uses for its own rows.</summary>
/// <remarks>
/// The identity and classification rows are fixed; the property rows are
/// every well-known property name
/// (<see cref="MaterialPropertyNames.All"/>), so a comparison covers the
/// engineering properties engineers actually compare without this list
/// having to restate them.
/// </remarks>
public static class MaterialComparisonProperties
{
    /// <summary>Material name.</summary>
    public const string Name = "Name";

    /// <summary>Material designation.</summary>
    public const string Designation = "Designation";

    /// <summary>Material family.</summary>
    public const string Family = "Family";

    /// <summary>Grade within the designation.</summary>
    public const string Grade = "Grade";

    /// <summary>Delivery or heat-treatment condition.</summary>
    public const string Condition = "Condition";

    /// <summary>Supplier of record.</summary>
    /// <remarks>
    /// The key is deliberately not the bare word "Supplier": that value is
    /// already canonically owned by the workspace's
    /// <c>CanonicalObjectKinds.Supplier</c> object Kind (`ADR-0105`), and a
    /// comparison row key is a different thing entirely from an object Kind.
    /// </remarks>
    public const string Supplier = "SupplierOfRecord";

    /// <summary>Validation state.</summary>
    public const string ValidationState = "ValidationState";

    /// <summary>Every property key, in the order a comparison lays its rows out.</summary>
    public static IReadOnlyList<string> All { get; } =
        new[] { Name, Designation, Family, Grade, Condition, Supplier }
            .Concat(MaterialPropertyNames.All)
            .Append(ValidationState)
            .ToList();
}

/// <summary>Builds a structured, side-by-side comparison of material records.</summary>
/// <remarks>
/// Pure and synchronous, and states no verdict: it says what each material
/// records, never which is better or which should be chosen. Materials of
/// different families compare correctly — a yield strength on a ceramic is
/// reported as not applicable rather than as a gap.
/// </remarks>
public static class MaterialComparer
{
    /// <summary>Compares <paramref name="materials"/> across every property in <see cref="MaterialComparisonProperties.All"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="materials"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="materials"/> is empty, or contains a <see langword="null"/>.</exception>
    public static ReferenceComparisonResult Compare(IReadOnlyList<IReferenceRecord<MaterialDefinition>> materials) =>
        ReferenceComparer.Compare(
            materials,
            MaterialComparisonProperties.All,
            CellFor,
            material => material.Definition.Family.ToString());

    private static ReferenceComparisonCell CellFor(IReferenceRecord<MaterialDefinition> material, string property)
    {
        var definition = material.Definition;
        var family = definition.Family;
        var applicabilityKnown = MaterialFamilyTraits.IsApplicabilityKnown(family);

        switch (property)
        {
            case MaterialComparisonProperties.Name:
                return ReferenceComparisonCell.Text(definition.Name);
            case MaterialComparisonProperties.Designation:
                return ReferenceComparisonCell.Text(definition.Designation);
            case MaterialComparisonProperties.Family:
                return ReferenceComparisonCell.Text(family.ToString());
            case MaterialComparisonProperties.Grade:
                return ReferenceComparisonCell.Text(definition.Grade);
            case MaterialComparisonProperties.Condition:
                return ReferenceComparisonCell.Applicable(
                    definition.Condition, MaterialFamilyTraits.HasHeatTreatmentCondition(family), applicabilityKnown);
            case MaterialComparisonProperties.Supplier:
                return ReferenceComparisonCell.Text(definition.Supplier);
            case MaterialComparisonProperties.ValidationState:
                return ReferenceComparisonCell.Text(material.ValidationState.ToString());
        }

        // A well-known property the family cannot have is not applicable;
        // one it can have but nobody recorded is a data gap.
        if (property == MaterialPropertyNames.YieldStrength
            && applicabilityKnown
            && !MaterialFamilyTraits.HasYieldStrength(family))
            return ReferenceComparisonCell.NotApplicable;

        if (!definition.Properties.TryGetValue(property, out var value))
            return ReferenceComparisonCell.NotRecorded;

        return new ReferenceComparisonCell(
            ReferencePropertyAvailability.Recorded,
            value.Value.ToString(),
            value.CanonicalValue);
    }
}
