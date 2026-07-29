namespace Tempest.Core.Reporting;

/// <summary>
/// Marks a concrete report definition — identity and the shape of the
/// data it produces. Carries no rendering logic of its own, mirroring
/// how <see cref="Commands.ICommand"/> carries no handling logic.
/// </summary>
public interface IReportDefinition
{
    /// <summary>A stable, unique identifier for this report definition.</summary>
    string Id { get; }

    /// <summary>A human-readable display name.</summary>
    string Name { get; }
}
