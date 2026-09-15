using System.Reflection;
using Tempest.Core.Modules;

namespace Tempest.Core.Tests.Modules;

/// <summary>
/// `TD-05`: every concrete <see cref="IModule"/> this platform's own
/// product assemblies declare carries <see cref="ModuleMetadataAttribute"/>
/// — the metadata-first path <see cref="ReflectionFrameworkDiscoveryService"/>
/// uses, never its own parameterless-constructor-and-instantiate fallback.
/// </summary>
/// <remarks>
/// A structural guard, not a behavioural one: nothing observes a runtime
/// failure from a module missing the attribute until discovery actually
/// runs against it with a constructor dependency in play, and by then the
/// module has already shipped without ever having been exercised this way.
/// Scanning every <see cref="IModule"/> this solution's own <c>src/</c>
/// assemblies declare, rather than waiting on that, means a future module
/// added without the attribute is caught here — on the old fallback path,
/// silently — before it ever reaches discovery for real.
/// </remarks>
public class ModuleMetadataCoverageTests
{
    [Fact]
    public void EveryConcreteModuleType_CarriesTheModuleMetadataAttribute()
    {
        // One type per `src/` assembly that is known to declare IModule
        // implementations (`WP 20.3B` audit: Tempest.Core itself declares
        // none directly today, but is included so a future one there is
        // caught too). Deliberately not AppDomain.CurrentDomain.GetAssemblies():
        // that would also pick up this very test assembly's own
        // ConstructorDependencyModuleWithoutMetadata/InvalidIdModule fixtures
        // (ModuleFixtures.cs), which exist specifically to be missing the
        // attribute.
        var assemblies = new[]
        {
            typeof(IModule).Assembly,
            typeof(Tempest.Samples.AuditSampleModule).Assembly,
            typeof(Tempest.Workspace.Calculations.CalculationsWorkspaceExplorerModule).Assembly,
            typeof(Tempest.Validation.FaultInjection.DuplicateNavigationModule).Assembly,
        }.Distinct();

        var moduleTypes = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(IModule).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract && !type.IsGenericTypeDefinition)
            .ToList();

        // Pinned (`WP 20.3B` audit): 25 in Tempest.Samples, 6 in
        // Tempest.Workspace, 1 in Tempest.Validation (the fault-injection
        // DuplicateNavigationModule - `[ModuleMetadata]` applies to it
        // exactly as to any other module) and none yet in Tempest.Core
        // itself. A deliberate count change updates this line with a
        // one-line rationale rather than loosening the assertion below.
        Assert.Equal(32, moduleTypes.Count);

        var missingMetadata = moduleTypes
            .Where(type => type.GetCustomAttribute<ModuleMetadataAttribute>() is null)
            .Select(type => type.FullName)
            .ToList();

        Assert.Empty(missingMetadata);
    }
}
