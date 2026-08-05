using Tempest.Core.Requirements;

namespace Tempest.App.Workspace.Requirements;

/// <summary>
/// Constructs a <see cref="RequirementsWorkspaceView"/> for one Requirements
/// Kind (<c>"Requirement"</c>/<c>"RequirementCollection"</c>/
/// <c>"RequirementGroup"</c>) — one instance, registered once per Kind,
/// mirroring <c>MechanicalWorkspaceViewFactory</c>'s own identical shape.
/// </summary>
public sealed class RequirementsWorkspaceViewFactory : IWorkspaceViewFactory
{
    private readonly IRequirementsService _requirementsService;

    /// <summary>Initialises a new instance of the <see cref="RequirementsWorkspaceViewFactory"/> class.</summary>
    /// <param name="kind">The single Requirements Kind this factory constructs a view for.</param>
    public RequirementsWorkspaceViewFactory(string kind, IRequirementsService requirementsService)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(requirementsService);

        Kind = kind;
        _requirementsService = requirementsService;
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="objectId"/> does not identify a known object of this factory's own <see cref="Kind"/>.</exception>
    /// <remarks>
    /// <see cref="IWorkspaceViewFactory.Create"/> is a frozen, synchronous
    /// `WP8.0B` contract; <see cref="RequirementsService"/>'s own backing
    /// <c>InMemoryPersistenceStore</c>/<c>InMemoryEngineeringDocumentStore</c>
    /// always completes synchronously already (no real I/O), so bridging
    /// with <c>GetAwaiter().GetResult()</c> here introduces no actual
    /// blocking — the same sync/async boundary <c>MechanicalWorkspaceViewFactory</c>
    /// already crosses for the identical reason.
    /// </remarks>
    public IWorkspaceView Create(Guid objectId, IWorkspaceContext context)
    {
        var title = Kind switch
        {
            RequirementsService.RequirementDocumentKind =>
                _requirementsService.FindAsync(objectId).GetAwaiter().GetResult() is { } requirement
                    ? $"{requirement.Identifier} — {requirement.Statement}"
                    : throw new ArgumentException($"'{objectId}' is not a known Requirement.", nameof(objectId)),

            RequirementsService.RequirementCollectionDocumentKind =>
                _requirementsService.FindCollectionAsync(objectId).GetAwaiter().GetResult() is { } collection
                    ? collection.Name
                    : throw new ArgumentException($"'{objectId}' is not a known Requirement Collection.", nameof(objectId)),

            RequirementsService.RequirementGroupDocumentKind =>
                _requirementsService.FindGroupAsync(objectId).GetAwaiter().GetResult() is { } group
                    ? group.Name
                    : throw new ArgumentException($"'{objectId}' is not a known Requirement Group.", nameof(objectId)),

            _ => throw new InvalidOperationException($"'{Kind}' is not a Requirements Kind this factory can construct a view for."),
        };

        return new RequirementsWorkspaceView(objectId, Kind, title, _requirementsService);
    }
}
