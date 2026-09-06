namespace Tempest.Core.Standards;

/// <summary>What kind of document a standard is — what it does, rather than what subject it covers.</summary>
/// <remarks>
/// The subject is <see cref="StandardDiscipline"/>; this is orthogonal to
/// it. A test method and a product specification can both be about
/// fasteners, and confusing the two would make
/// <see cref="StandardClassificationTraits"/> unable to say which fields
/// are meaningful.
/// </remarks>
public enum StandardClassification
{
    /// <summary>Not recorded. The honest default.</summary>
    Unspecified,

    /// <summary>States requirements a product, material or process must meet.</summary>
    Specification,

    /// <summary>Defines how a property is to be measured.</summary>
    TestMethod,

    /// <summary>Recommends practice without stating conformity requirements.</summary>
    CodeOfPractice,

    /// <summary>Informative guidance.</summary>
    Guide,

    /// <summary>Defines terms and definitions.</summary>
    Terminology,

    /// <summary>Defines a classification, designation or numbering system.</summary>
    DesignationSystem,

    /// <summary>Defines symbols, drawing conventions or documentation rules.</summary>
    Documentation,

    /// <summary>States requirements for a management system rather than for a product.</summary>
    ManagementSystem,

    /// <summary>States dimensional or interface requirements that make parts interchangeable.</summary>
    DimensionalStandard,

    /// <summary>A kind this classification does not fit. <see cref="StandardDefinition.SourceClassification"/> must then record the source's own wording.</summary>
    Other
}
