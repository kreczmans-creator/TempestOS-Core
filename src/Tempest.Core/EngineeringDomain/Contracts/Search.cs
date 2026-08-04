namespace Tempest.Core.EngineeringDomain;

public interface ISearchQuery
{
    string? Text { get; }
    string? Kind { get; }
    string? Category { get; }
    IReadOnlyDictionary<string, string>? MetadataFilters { get; }
}

public interface ISearchResult
{
    IReadOnlyList<IEngineeringObject> Matches { get; }
    int TotalCount { get; }
}

public interface ISavedQuery
{
    Guid Id { get; }
    string Name { get; }
    ISearchQuery Query { get; }
}
