namespace Tempest.Core.EngineeringDomain;

/// <summary>The plain data backing <see cref="IHasMetadata"/> — supplied by a factory at construction, never re-derived from <see cref="IHasRevisions.Content"/>.</summary>
public sealed record EngineeringObjectMetadata(
    string? Category = null,
    string? Discipline = null,
    string? Owner = null,
    IReadOnlyList<string>? Tags = null,
    string? Classification = null,
    string? Notes = null)
{
    public static readonly EngineeringObjectMetadata Empty = new();

    public IReadOnlyList<string> TagsOrEmpty => Tags ?? Array.Empty<string>();
}
