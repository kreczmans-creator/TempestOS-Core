namespace Tempest.Core.Standards;

/// <summary>The engineering subject a standard covers.</summary>
/// <remarks>
/// A standard legitimately covers more than one, so
/// <see cref="StandardDefinition.Disciplines"/> is a list rather than a
/// single value. An empty list means nobody recorded a discipline — never
/// that the standard covers none.
/// </remarks>
public enum StandardDiscipline
{
    /// <summary>Not recorded. Present so a caller can state "unclassified" explicitly rather than by omission.</summary>
    Unspecified,

    /// <summary>Mechanical engineering and machine elements.</summary>
    Mechanical,

    /// <summary>Materials, their designation and their properties.</summary>
    Materials,

    /// <summary>Manufacturing and production processes.</summary>
    Manufacturing,

    /// <summary>Metrology, tolerancing and measurement.</summary>
    Metrology,

    /// <summary>Electrical and electronic engineering.</summary>
    Electrical,

    /// <summary>Civil and structural engineering.</summary>
    Structural,

    /// <summary>Fluid power, pressure equipment and piping.</summary>
    FluidSystems,

    /// <summary>Functional safety and machinery safety.</summary>
    Safety,

    /// <summary>Quality, management and assurance.</summary>
    Quality,

    /// <summary>Environmental and sustainability requirements.</summary>
    Environmental,

    /// <summary>Engineering documentation and drawing practice.</summary>
    Documentation,

    /// <summary>A discipline this list does not cover.</summary>
    Other
}
