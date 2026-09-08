namespace Tempest.App.Workspace;

/// <summary>What category of fact a <see cref="PropertyFacet"/> represents.</summary>
public enum PropertyFacetKind
{
    /// <summary>Identity — Id, business identifier (`WP8.0A Navigation Specification.md` §4).</summary>
    Identity,

    /// <summary>Revision history.</summary>
    Revision,

    /// <summary>Provenance — who created/revised the object, and when.</summary>
    Provenance,

    /// <summary>A relationship to another object.</summary>
    Relationship,

    /// <summary>A facet only the selected object's own <c>Kind</c> contributes — for example a Requirement's own <c>RequirementStatus</c>.</summary>
    DisciplineSpecific,
}
