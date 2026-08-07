using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Verification;

/// <summary>
/// Constructs a <see cref="VerificationActivityWorkspaceView"/> for
/// <c>"VerificationActivity"</c> — mirrors
/// <see cref="Mechanical.MechanicalWorkspaceViewFactory"/>'s own identical
/// shape.
/// </summary>
public sealed class VerificationActivityWorkspaceViewFactory : IWorkspaceViewFactory
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="VerificationActivityWorkspaceViewFactory"/> class.</summary>
    public VerificationActivityWorkspaceViewFactory(string kind, EngineeringDomainContext context)
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
    /// `WP8.0B` contract — bridged with <c>GetAwaiter().GetResult()</c>,
    /// exactly <see cref="Mechanical.MechanicalWorkspaceViewFactory"/>'s own
    /// already-disclosed precedent (no real I/O, so no actual blocking).
    /// </remarks>
    public IWorkspaceView Create(Guid objectId, IWorkspaceContext context)
    {
        var target = _context.Repository.FindAsync(objectId).GetAwaiter().GetResult()
            ?? throw new ArgumentException($"'{objectId}' is not a known {Kind}.", nameof(objectId));

        var title = (target as IHasBusinessIdentifier)?.DisplayName ?? objectId.ToString();

        return new VerificationActivityWorkspaceView(objectId, Kind, title, _context);
    }
}
