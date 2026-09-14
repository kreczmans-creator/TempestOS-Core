using Tempest.Core.Configuration;
using Tempest.Core.EngineeringAssets.CalculationPacks;
using Tempest.Core.Materials;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.EngineeringAssets;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Runtime;

namespace Tempest.Core.Tests.ReferenceData;

/// <summary>
/// `TD-157` (closed `WP 19.10E`) — <c>CalculationPackValidationService</c>'s
/// own optional <c>IEnumerable&lt;IReferencePinResolver&gt;</c> constructor
/// parameter now actually receives resolvers when the service is resolved
/// through the real composition root (<see cref="TempestHost"/>), not only
/// when a test constructs it by hand the way
/// <c>EngineeringAssetBehaviourTests.A_pack_pinned_to_a_superseded_record_is_warned_about_and_never_altered</c>
/// already does.
/// </summary>
public class ReferencePinResolverRegistrationTests
{
    private static async Task RunAgainstRunningHostAsync(string rootPath, Func<ITempestHost, Task> body)
    {
        var host = new TempestHostBuilder(Type.EmptyTypes)
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();

        var runTask = host.RunAsync();
        await RunningHostFixture.WaitUntilRunningAsync(host);
        await body(host);
        await host.StopAsync();
        await runTask;
    }

    /// <summary>
    /// Pins a released material; the material is then superseded by a
    /// second, released material standing in for its newer revision.
    /// Asking the real, DI-resolved <c>ICalculationPackValidationService</c>
    /// to validate the pack that pins the old material now reports
    /// <see cref="CalculationPackValidationRules.PinnedSourceSuperseded"/>,
    /// naming the pinned record and the exact revision the pin names.
    /// </summary>
    /// <remarks>
    /// Before `WP 19.10E`'s registration fix, this same real, running host
    /// reported no warning at all — checked directly against this branch's
    /// pre-fix state (`8a5de242`): <c>CalculationPackValidationService</c>'s
    /// <c>pinResolvers</c> constructor parameter had no registration for
    /// the container to supply, so <c>TempestServiceProvider.Construct</c>
    /// always fell back to its declared default, <see langword="null"/>,
    /// and <c>EvaluatePinsAsync</c> found no resolver for the
    /// <c>"Materials"</c> library and warned about nothing.
    /// </remarks>
    [Fact]
    public async Task PinnedMaterial_SupersededByAReleasedReplacement_ReportsPinnedSourceSupersededThroughTheRealHost()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog))!;
            var packs = (ICalculationPackCatalog)host.Services!.GetService(typeof(ICalculationPackCatalog))!;
            var validation = (ICalculationPackValidationService)host.Services!.GetService(typeof(ICalculationPackValidationService))!;

            var provenance = AssetFixtures.Verified();

            await materials.RegisterAsync(
                "mat-old", new MaterialDefinition { Name = "Fixture Steel, original heat", Family = MaterialFamily.Steel }, provenance);
            await materials.SetValidationStateAsync("mat-old", ReferenceValidationState.Checked, "Checked.");
            await materials.SetValidationStateAsync("mat-old", ReferenceValidationState.Validated, "Rules pass.");
            var pinned = await materials.SetValidationStateAsync("mat-old", ReferenceValidationState.Released, "Released.");

            await materials.RegisterAsync(
                "mat-new", new MaterialDefinition { Name = "Fixture Steel, revised heat", Family = MaterialFamily.Steel }, provenance);
            await materials.SetValidationStateAsync("mat-new", ReferenceValidationState.Checked, "Checked.");
            await materials.SetValidationStateAsync("mat-new", ReferenceValidationState.Validated, "Rules pass.");
            await materials.SetValidationStateAsync("mat-new", ReferenceValidationState.Released, "Released.");

            await materials.SupersedeAsync("mat-old", "mat-new", "Replaced by a revised heat.");

            var pin = ReferencePin.For(materials.LibraryName, pinned);

            var pack = AssetFixtures.Pack() with
            {
                Inputs = [new CalculationInput("I1", "Applied load", "1200 N", pin)],
            };
            await packs.RegisterAsync("calc-1", pack, provenance);

            var result = await validation.ValidateAsync("calc-1");

            var warning = Assert.Single(result.Warnings, w => w.Code == CalculationPackValidationRules.PinnedSourceSuperseded);
            Assert.Contains("mat-old", warning.Message, StringComparison.Ordinal);
            Assert.Contains($"revision {pin.RevisionNumber}", warning.Message, StringComparison.Ordinal);
        });
    }
}
