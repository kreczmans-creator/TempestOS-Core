using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Calculations;

/// <summary>
/// Constructs a <see cref="CalculationsWorkspaceView"/> for one Calculations
/// Kind (<c>"Calculation"</c>/<c>"CalculationSet"</c>/<c>"CalculationTemplate"</c>)
/// — one instance per Kind, mirroring <see cref="Mechanical.MechanicalWorkspaceViewFactory"/>'s
/// own identical shape.
/// </summary>
public sealed class CalculationsWorkspaceViewFactory : IWorkspaceViewFactory
{
    private readonly EngineeringDomainContext _context;
    private readonly CalculationTemplateRegistry _templateRegistry;

    /// <summary>Initialises a new instance of the <see cref="CalculationsWorkspaceViewFactory"/> class.</summary>
    /// <param name="kind">The single object Kind this factory constructs a view for.</param>
    public CalculationsWorkspaceViewFactory(string kind, EngineeringDomainContext context, CalculationTemplateRegistry templateRegistry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(templateRegistry);

        Kind = kind;
        _context = context;
        _templateRegistry = templateRegistry;
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="objectId"/> does not identify a known object of this factory's own <see cref="Kind"/>.</exception>
    /// <remarks>
    /// <see cref="IWorkspaceViewFactory.Create"/> is a frozen, synchronous
    /// `WP8.0B` contract — bridged with <c>GetAwaiter().GetResult()</c> for
    /// <c>"Calculation"</c>/<c>"CalculationSet"</c>, exactly
    /// <see cref="Mechanical.MechanicalWorkspaceViewFactory"/>'s own already-disclosed
    /// precedent (no real I/O, so no actual blocking); <c>"CalculationTemplate"</c>
    /// resolves synchronously from <see cref="CalculationTemplateRegistry"/> directly.
    /// </remarks>
    public IWorkspaceView Create(Guid objectId, IWorkspaceContext context)
    {
        if (string.Equals(Kind, "CalculationTemplate", StringComparison.Ordinal))
        {
            var template = _templateRegistry.FindByNodeId(objectId)
                ?? throw new ArgumentException($"'{objectId}' is not a known {Kind}.", nameof(objectId));

            return new CalculationsWorkspaceView(objectId, Kind, template.Metadata.Name, _context, _templateRegistry);
        }

        var target = _context.Repository.FindAsync(objectId).GetAwaiter().GetResult()
            ?? throw new ArgumentException($"'{objectId}' is not a known {Kind}.", nameof(objectId));

        var title = (target as IHasBusinessIdentifier)?.DisplayName ?? objectId.ToString();

        return new CalculationsWorkspaceView(objectId, Kind, title, _context, _templateRegistry);
    }
}
