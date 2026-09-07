namespace Tempest.Core.Standards;

/// <summary>
/// What kind of organisation publishes a standard — the classification of
/// the publisher, not of the standard.
/// </summary>
/// <remarks>
/// Closed and deliberately coarse. The publishing organisations themselves
/// are an open set that no enum could honestly close, so they are recorded
/// as <see cref="StandardsBody.Code"/> free text; what <em>is</em>
/// classifiable is the kind of authority the organisation carries, and
/// that is a small, stable set.
/// </remarks>
public enum StandardsBodyKind
{
    /// <summary>Not recorded. The honest default — never a claim the body has no kind.</summary>
    Unspecified,

    /// <summary>An international standards organisation.</summary>
    International,

    /// <summary>A regional standards organisation covering a group of countries.</summary>
    Regional,

    /// <summary>A national standards body.</summary>
    National,

    /// <summary>A trade, professional or industry association that publishes standards.</summary>
    IndustryAssociation,

    /// <summary>A defence or military standards authority.</summary>
    Military,

    /// <summary>A regulator whose published requirements are recorded here as standards.</summary>
    Regulator,

    /// <summary>A single company's own internal standard.</summary>
    Company,

    /// <summary>A publisher this classification does not fit. <see cref="StandardDefinition.SourceClassification"/> must then record the source's own wording.</summary>
    Other
}
