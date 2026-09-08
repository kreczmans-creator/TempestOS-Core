namespace Tempest.App.Workspace;

/// <summary>One displayed fact in the <see cref="IPropertyInspector"/>.</summary>
/// <param name="Name">The facet's own display label.</param>
/// <param name="Value">The facet's own display value.</param>
/// <param name="FacetKind">What category of fact this is.</param>
public sealed record PropertyFacet(string Name, string Value, PropertyFacetKind FacetKind);
