using Tempest.App.Workspace;
using Tempest.App.Workspace.Calculations;
using Tempest.App.Workspace.Documents;
using Tempest.App.Workspace.Manufacturing;
using Tempest.App.Workspace.Mechanical;
using Tempest.App.Workspace.Requirements;
using Tempest.App.Workspace.Verification;
using Tempest.Core.Commands;
using Tempest.Desktop.Composition;

namespace Tempest.Desktop.Tests;

/// <summary>
/// WP-B1 (`TD-107`) — the two encodings of per-Kind rename/revise/delete
/// eligibility must agree.
/// </summary>
/// <remarks>
/// <para>
/// This platform states the same fact twice. <see cref="CommandBinding.AppliesToKinds"/>
/// drives <see cref="ICommandRegistry.Evaluate"/>, and therefore the Ribbon
/// and the Command Palette. The <c>Register{Rename,Delete,Revise}Factory</c>
/// Kind maps drive <see cref="IWorkspaceManager.CanRename"/>/
/// <see cref="IWorkspaceManager.CanRevise"/>/<see cref="IWorkspaceManager.CanDelete"/>,
/// and therefore the Project Explorer's context menu and inline rename, the
/// Property Inspector's name field, and the Object Editor's Name/Content
/// fields <i>and its save path</i>. `TD-77` Stage 5 moved the Ribbon from the
/// second encoding to the first, which is what split them.
/// </para>
/// <para>
/// <b>The invariant is directional, not equality</b> — and the asymmetry is
/// deliberate, not drift. Manufacturing registers <i>Documents'</i> rename
/// command for its own <c>"WorkInstruction"</c> Kind and <i>Verification's</i>
/// for <c>"Inspection"</c> (disclosed cross-Work-Package reuse, `WP 9.5A`),
/// so the manager's rename map is wider than any single descriptor's
/// <c>AppliesToKinds</c>. Asserting set equality per descriptor would fail on
/// correct code. The two directions below are what actually protect a user.
/// </para>
/// <para>
/// <b>This is `WP-B1`, deliberately a test and not a redesign.</b> The
/// approved disposition for `TD-107` is the cheapest robust invariant;
/// unifying the two encodings is `WP-B2`, which is deferred and requires its
/// own ADR. Nothing here changes production code.
/// </para>
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class KindEligibilityInvariantTests
{
    private enum Verb { Rename, Revise, Delete }

    /// <summary>
    /// Which manager capability each Object-Editor-routed command needs.
    /// Written out rather than parsed from the Id: reading a command's
    /// meaning out of its own trailing word is the defect `TD-77` Stage 5
    /// removed, and a test is not the place to reintroduce it.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Verb> EditorRouted =
        new Dictionary<string, Verb>(StringComparer.Ordinal)
        {
            ["calculations.rename"] = Verb.Rename,
            ["documents.rename"] = Verb.Rename,
            ["manufacturing.rename"] = Verb.Rename,
            ["mechanical.rename"] = Verb.Rename,
            ["verification.rename"] = Verb.Rename,
            ["calculations.edit"] = Verb.Revise,
            ["documents.edit"] = Verb.Revise,
            ["manufacturing.edit"] = Verb.Revise,
            ["mechanical.edit"] = Verb.Revise,
            ["verification.edit"] = Verb.Revise,
            ["requirements.revise"] = Verb.Revise,
        };

    private static readonly IReadOnlyList<string> DeleteRouted =
    [
        "calculations.delete", "documents.delete", "manufacturing.delete", "mechanical.delete",
        "verification.delete", "requirements.delete", "requirements.delete-group",
        "requirements.delete-collection",
    ];

    /// <summary>Every Kind this platform declares, from the disciplines' own public constants.</summary>
    private static IReadOnlyList<string> AllDeclaredKinds() =>
    [
        .. CalculationsWorkspaceRegistration.SupportedKinds,
        .. DocumentsWorkspaceRegistration.SupportedKinds,
        .. ManufacturingWorkspaceRegistration.SupportedKinds,
        .. MechanicalObjectFactoryRegistry.SupportedKinds,
        .. RequirementsWorkspaceRegistration.SupportedKinds,
        .. VerificationWorkspaceRegistration.SupportedKinds,
    ];

    private static bool Can(IWorkspaceManager manager, Verb verb, string kind) => verb switch
    {
        Verb.Rename => manager.CanRename(kind),
        Verb.Revise => manager.CanRevise(kind),
        _ => manager.CanDelete(kind),
    };

    // ==================================================================
    // The test's own map cannot drift from the policy it describes
    // ==================================================================

    [Fact]
    public void TheVerbMap_CoversExactlyTheCommandsTheRibbonRoutesAwayFromTheirBinding()
    {
        Assert.Equal(
            SurfaceCommandPolicy.ObjectEditorCommandIds.OrderBy(id => id, StringComparer.Ordinal),
            EditorRouted.Keys.OrderBy(id => id, StringComparer.Ordinal));

        Assert.Equal(
            SurfaceCommandPolicy.DeleteCommandIds.OrderBy(id => id, StringComparer.Ordinal),
            DeleteRouted.OrderBy(id => id, StringComparer.Ordinal));
    }

    // ==================================================================
    // Direction 1 — the dangerous one
    // ==================================================================

    /// <summary>
    /// Every Kind a routed command declares it applies to must have the
    /// matching factory registered. Without this, <c>Evaluate</c> enables the
    /// button and the dispatch then fails with "No delete capability is
    /// registered for Kind 'X'" — or the Object Editor opens with the field
    /// the command exists to edit greyed out.
    /// </summary>
    [Fact]
    public async Task EveryKindABindingClaims_HasTheMatchingWorkspaceManagerFactory()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var manager = host.Manager!;
            var gaps = new List<string>();
            var checkedPairs = 0;

            foreach (var (commandId, verb) in EditorRouted.Concat(DeleteRouted.Select(id => new KeyValuePair<string, Verb>(id, Verb.Delete))))
            {
                var descriptor = registry.Items.Single(d => d.Id == commandId);
                var kinds = descriptor.Binding?.AppliesToKinds;

                Assert.NotNull(kinds);

                foreach (var kind in kinds!)
                {
                    checkedPairs++;
                    if (!Can(manager, verb, kind))
                        gaps.Add($"'{commandId}' applies to Kind '{kind}', but IWorkspaceManager.Can{verb}(\"{kind}\") is false.");
                }
            }

            Assert.Empty(gaps);
            Assert.True(checkedPairs >= 40, $"Expected the routed commands to cover a substantial Kind surface; only {checkedPairs} pairs were checked.");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ==================================================================
    // Direction 2 — the discoverability one
    // ==================================================================

    /// <summary>
    /// Every Kind the manager will rename/revise/delete must be claimed by at
    /// least one command that declares it. Without this, the Project Explorer
    /// offers an action on an object the Ribbon shows disabled — the same
    /// disagreement, seen from the other side.
    /// </summary>
    [Fact]
    public async Task EveryKindTheManagerSupports_IsClaimedByAtLeastOneCommand()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var manager = host.Manager!;
            var unclaimed = new List<string>();

            foreach (var verb in new[] { Verb.Rename, Verb.Revise, Verb.Delete })
            {
                var claimed = (verb == Verb.Delete ? DeleteRouted : EditorRouted.Where(e => e.Value == verb).Select(e => e.Key))
                    .SelectMany(id => registry.Items.Single(d => d.Id == id).Binding?.AppliesToKinds ?? [])
                    .ToHashSet(StringComparer.Ordinal);

                foreach (var kind in AllDeclaredKinds().Distinct(StringComparer.Ordinal))
                {
                    if (Can(manager, verb, kind) && !claimed.Contains(kind))
                        unclaimed.Add($"IWorkspaceManager.Can{verb}(\"{kind}\") is true, but no command declares it.");
                }
            }

            Assert.Empty(unclaimed);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ==================================================================
    // The asymmetry itself, pinned so it reads as intent, not as drift
    // ==================================================================

    /// <summary>
    /// Manufacturing's own Kinds are renamable through <i>other</i>
    /// disciplines' commands — disclosed cross-Work-Package reuse (`WP 9.5A`).
    /// Pinned so a future reader meets it as a decision rather than as an
    /// inconsistency, and so the directional invariants above are not
    /// "corrected" into a symmetric one that would fail on correct code.
    /// </summary>
    [Fact]
    public async Task TheManagerIsDeliberatelyWiderThanAnySingleDescriptor()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var manager = host.Manager!;

            // Documents' own rename command does not claim these two Kinds…
            var documentsRename = registry.Items.Single(d => d.Id == "documents.rename").Binding!.AppliesToKinds!;
            Assert.DoesNotContain("WorkInstruction", documentsRename);
            Assert.DoesNotContain("Inspection", documentsRename);

            // …yet the manager renames both, via Documents'/Verification's own
            // commands registered by Manufacturing for its Kinds.
            Assert.True(manager.CanRename("WorkInstruction"));
            Assert.True(manager.CanRename("Inspection"));

            // Manufacturing's own command is what declares them.
            Assert.Contains("WorkInstruction", registry.Items.Single(d => d.Id == "manufacturing.rename").Binding!.AppliesToKinds!);
            Assert.Contains("Inspection", registry.Items.Single(d => d.Id == "manufacturing.rename").Binding!.AppliesToKinds!);

            // And the synthetic Calculation Template Kind is excluded on both
            // sides — it is not an EngineeringDomainContext object at all.
            Assert.False(manager.CanRename("CalculationTemplate"));
            Assert.DoesNotContain("CalculationTemplate", registry.Items.Single(d => d.Id == "calculations.rename").Binding!.AppliesToKinds!);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
