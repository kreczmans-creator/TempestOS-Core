namespace Tempest.Core.Constants;

/// <summary>What kind of constant a record holds.</summary>
/// <remarks>
/// Classifies where a constant's own authority comes from, which is the
/// question that matters when deciding whether it may be relied on: a
/// value fixed by definition, a value measured with uncertainty, and a
/// value adopted by convention are three different kinds of fact.
/// </remarks>
public enum ConstantCategory
{
    /// <summary>Not recorded. The honest default.</summary>
    Unspecified,

    /// <summary>A fundamental physical constant of nature.</summary>
    Universal,

    /// <summary>An electromagnetic constant.</summary>
    Electromagnetic,

    /// <summary>A thermodynamic constant.</summary>
    Thermodynamic,

    /// <summary>An atomic, nuclear or particle constant.</summary>
    AtomicAndNuclear,

    /// <summary>A mathematical constant, exact by definition and always dimensionless.</summary>
    Mathematical,

    /// <summary>
    /// A value adopted by convention for engineering use rather than
    /// measured — a standard acceleration of free fall, a standard
    /// atmosphere, a reference temperature. Exact within its own
    /// convention, and true of nowhere in particular.
    /// </summary>
    ConventionalReference,

    /// <summary>A conversion factor between two systems of measurement, where a source publishes one as a constant in its own right.</summary>
    ConversionFactor,

    /// <summary>A category this taxonomy does not classify. <see cref="ConstantDefinition.SourceClassification"/> must then record the source's own wording.</summary>
    Other
}

/// <summary>Questions about a <see cref="ConstantCategory"/>, answered in one place.</summary>
public static class ConstantCategories
{
    /// <summary>Whether constants in this category are dimensionless by their own nature.</summary>
    public static bool IsAlwaysDimensionless(ConstantCategory category) => category is ConstantCategory.Mathematical;

    /// <summary>
    /// Whether constants in this category are exact rather than measured —
    /// fixed by definition or adopted by convention, and so carrying no
    /// experimental uncertainty.
    /// </summary>
    public static bool IsExactByNature(ConstantCategory category) =>
        category is ConstantCategory.Mathematical or ConstantCategory.ConventionalReference;

    /// <summary>
    /// Whether a statement of where the constant applies is expected. A
    /// universal constant applies everywhere and needs none; a
    /// conventional reference value is true only within the convention
    /// that adopted it, and a record that does not say which convention is
    /// incomplete.
    /// </summary>
    public static bool ExpectsApplicability(ConstantCategory category) =>
        category is ConstantCategory.ConventionalReference or ConstantCategory.ConversionFactor or ConstantCategory.Other;

    /// <summary>
    /// Whether this table can speak for <paramref name="category"/> at all.
    /// <see cref="ConstantCategory.Unspecified"/> and
    /// <see cref="ConstantCategory.Other"/> are unclassified by
    /// construction: every answer above is conservative for them and must
    /// be read as "not known to apply", never "known not to apply".
    /// </summary>
    public static bool IsApplicabilityKnown(ConstantCategory category) =>
        category is not (ConstantCategory.Unspecified or ConstantCategory.Other);
}
