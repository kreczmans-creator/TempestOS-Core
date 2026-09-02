using System.Runtime.CompilerServices;

namespace SampleHarnessLoading;

/// <summary>
/// Loads <c>Tempest.Samples</c> into the test process before any test runs.
/// </summary>
/// <remarks>
/// <para>
/// <b>This used to be production code, and that was `TD-75`.</b>
/// <c>WorkspaceManager</c>'s constructor forced the sample assembly to load
/// so that module Discovery's own scan would find it — because the six
/// discipline explorer modules that declare the product's own navigation
/// lived there. The product reached into the sample harness to find its own
/// Engineering areas.
/// </para>
/// <para>
/// Those modules now live with the disciplines that own them, so the
/// product needs nothing from <c>Tempest.Samples</c> and no longer
/// references it. The <em>tests</em> still do: a great many of them assert
/// against the fictional engineering content the sample modules seed, which
/// is legitimate — it is a test rig asking for test data, rather than a
/// product depending on demo content.
/// </para>
/// <para>
/// A project reference alone does not load an assembly (the documented
/// behaviour `WP 5.0D` first found), so the touch has to be explicit. It is
/// a <see cref="ModuleInitializerAttribute"/> rather than a fixture so that
/// it happens once, before the first host is built, whatever order tests
/// run in.
/// </para>
/// </remarks>
internal static class SampleHarnessLoader
{
    [ModuleInitializer]
    internal static void EnsureSampleAssemblyIsLoaded() => _ = typeof(Tempest.Samples.ClockModule).Assembly;
}
