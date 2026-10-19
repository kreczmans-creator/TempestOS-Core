using Tempest.Core.Modules;
using Tempest.Core.Navigation;

namespace Tempest.Workspace.Disciplines;

/// <summary>
/// The shared lifecycle behind each discipline's Explorer area module
/// (`ADR-0163`): registers the area's navigation item on start and removes
/// it on stop. Module discovery needs one concrete, attributed class per
/// discipline, below.
/// </summary>
public abstract class DisciplineWorkspaceExplorerModule : ModuleLifecycleBase
{
    private readonly INavigationProvider _navigationProvider;
    private readonly DisciplineDefinition _definition;

    /// <summary>Initialises a new instance of the <see cref="DisciplineWorkspaceExplorerModule"/> class.</summary>
    protected DisciplineWorkspaceExplorerModule(INavigationProvider navigationProvider, DisciplineDefinition definition, string moduleId)
        : base(moduleId, $"{definition.Category} Workspace Explorer", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(navigationProvider);

        _navigationProvider = navigationProvider;
        _definition = definition;
    }

    /// <inheritdoc />
    public override Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _navigationProvider.Register(new NavigationItem(_definition.NavigationItemId, _definition.Category));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DisposeAsync(CancellationToken cancellationToken)
    {
        _navigationProvider.Unregister(_definition.NavigationItemId);
        return Task.CompletedTask;
    }
}

/// <summary>Registers the Structural Explorer area.</summary>
[ModuleMetadata(ModuleId, "Structural Workspace Explorer", "1.0.0")]
public sealed class StructuralWorkspaceExplorerModule(INavigationProvider navigationProvider)
    : DisciplineWorkspaceExplorerModule(navigationProvider, DisciplineCatalogue.Structural, ModuleId)
{
    /// <summary>This module's id.</summary>
    public const string ModuleId = "tempest.structural.workspace-explorer";
}

/// <summary>Registers the Electrical Explorer area.</summary>
[ModuleMetadata(ModuleId, "Electrical Workspace Explorer", "1.0.0")]
public sealed class ElectricalWorkspaceExplorerModule(INavigationProvider navigationProvider)
    : DisciplineWorkspaceExplorerModule(navigationProvider, DisciplineCatalogue.Electrical, ModuleId)
{
    /// <summary>This module's id.</summary>
    public const string ModuleId = "tempest.electrical.workspace-explorer";
}

/// <summary>Registers the Building Services / HVAC Explorer area.</summary>
[ModuleMetadata(ModuleId, "Building Services Workspace Explorer", "1.0.0")]
public sealed class BuildingServicesWorkspaceExplorerModule(INavigationProvider navigationProvider)
    : DisciplineWorkspaceExplorerModule(navigationProvider, DisciplineCatalogue.BuildingServices, ModuleId)
{
    /// <summary>This module's id.</summary>
    public const string ModuleId = "tempest.building-services.workspace-explorer";
}
