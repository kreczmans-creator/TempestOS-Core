using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Mechanical;

/// <summary>
/// Constructs a <see cref="MechanicalWorkspaceView"/> for one Mechanical
/// Product Structure object Kind — one instance, registered once per Kind
/// (mirrors <see cref="EngineeringObjectFactory{T}"/>'s own "one generic
/// type, instantiated once per Kind" precedent, `ADR-0079`, applied to
/// Workspace views).
/// </summary>
public sealed class MechanicalWorkspaceViewFactory : IWorkspaceViewFactory
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="MechanicalWorkspaceViewFactory"/> class.</summary>
    /// <param name="kind">The single object Kind this factory constructs a view for.</param>
    public MechanicalWorkspaceViewFactory(string kind, EngineeringDomainContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(context);

        Kind = kind;
        _context = context;
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="objectId"/> does not identify a known object of this factory's own <see cref="Kind"/>.</exception>
    /// <remarks>
    /// <see cref="IWorkspaceViewFactory.Create"/> is a frozen, synchronous
    /// `WP8.0B` contract; <see cref="InMemoryEngineeringObjectRepository.FindAsync"/>
    /// always completes synchronously already (no real I/O), so bridging
    /// with <c>GetAwaiter().GetResult()</c> here introduces no actual
    /// blocking — the same sync/async boundary every other purely in-memory
    /// Domain read crosses when called from a contractually synchronous caller.
    /// </remarks>
    public IWorkspaceView Create(Guid objectId, IWorkspaceContext context)
    {
        var target = _context.Repository.FindAsync(objectId).GetAwaiter().GetResult()
            ?? throw new ArgumentException($"'{objectId}' is not a known {Kind}.", nameof(objectId));

        var title = (target as IHasBusinessIdentifier)?.DisplayName ?? objectId.ToString();

        return new MechanicalWorkspaceView(objectId, Kind, title, _context);
    }
}
