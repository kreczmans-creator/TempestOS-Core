using Tempest.App.Workspace;
using Tempest.Core.Commands;
using Tempest.Desktop.Composition;

namespace Tempest.Desktop.Tests;

/// <summary>
/// WP-A1 (`F-10`, `TD-108`) — <see cref="SurfaceCommandPolicy"/>'s explicit
/// Delete list must stay complete against the commands actually registered.
/// </summary>
/// <remarks>
/// <para>
/// A Delete command routed through its own binding deletes the object
/// correctly and leaves the shell pointing at it: the Property Inspector
/// still shows it, the Explorer still lists it selected, and a second Delete
/// is still offered. <see cref="IWorkspaceManager.DeleteObjectAsync"/> is
/// where a successful delete clears the selection (<c>TD-58</c>), so a
/// deleting command that is not in <see cref="SurfaceCommandPolicy.DeleteCommandIds"/>
/// reintroduces that defect for exactly the objects it deletes. Nothing in
/// the compiler or in the Ribbon notices; the button works.
/// </para>
/// <para>
/// <b>The policy stays explicit; only the check is derived.</b> WP-A1's
/// ruling is that the production routing list is written out by hand and is
/// not to be replaced by string-based routing. This test does not route
/// anything — it independently asks the registry which registered commands
/// are deletes, and requires that answer and the hand-written list to be the
/// same set. The two failure directions are both real: a discipline that
/// adds a Delete and forgets the policy, and a policy entry that outlives
/// (or never matched) a registered command.
/// </para>
/// <para>
/// <b>How a delete is recognised, without parsing an Id.</b> Reading a
/// command's meaning out of the text after the last dot in its Id is the
/// exact defect `TD-77` Stage 5 removed — it made
/// <c>requirements.delete-group</c> unreachable. The signal used here is
/// structural instead: every Delete binding declares its confirmation
/// through <see cref="WorkspaceCommandBindings.DeleteConfirmation"/>, and
/// that helper's own output is what this test matches against. Reword the
/// confirmation and the detector follows it; write a delete without one and
/// the shape below stops recognising it — which is why the recognised set is
/// also pinned to a floor, so silently detecting nothing cannot pass.
/// </para>
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class SurfaceCommandPolicyCompletenessTests
{
    /// <summary>
    /// A noun no product string contains, used to split
    /// <see cref="WorkspaceCommandBindings.DeleteConfirmation"/>'s own
    /// wording into the fixed halves either side of its argument.
    /// </summary>
    private const string NounSentinel = "NOUN";

    private static (string Prefix, string Suffix) DeleteConfirmationShape()
    {
        var template = WorkspaceCommandBindings.DeleteConfirmation(NounSentinel);
        var split = template.Split(NounSentinel);

        Assert.Equal(2, split.Length);

        return (split[0], split[1]);
    }

    /// <summary>Whether this descriptor's binding declares a delete confirmation — the structural mark of an object delete.</summary>
    private static bool IsObjectDelete(CommandDescriptor descriptor)
    {
        var message = descriptor.Binding?.ConfirmationMessage;

        if (message is null)
            return false;

        var (prefix, suffix) = DeleteConfirmationShape();

        return message.StartsWith(prefix, StringComparison.Ordinal)
               && message.EndsWith(suffix, StringComparison.Ordinal)
               && message.Length > prefix.Length + suffix.Length;
    }

    private static async Task<T> WithRegistryAsync<T>(Func<ICommandRegistry, T> read)
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            return read((ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry)));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// The whole guard rail, in one direction each way: every registered
    /// delete is in the policy (a new discipline Delete cannot ship
    /// unrouted), and every policy entry is a registered delete (an entry
    /// cannot point at a renamed, removed, or never-existing command, nor at
    /// a command that does not delete).
    /// </summary>
    [Fact]
    public async Task ThePolicyCoversExactlyTheRegisteredDeleteCommands()
    {
        var registered = await WithRegistryAsync(registry => registry.Items
            .Where(IsObjectDelete)
            .Select(descriptor => descriptor.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList());

        // A detector that silently recognised nothing would make the
        // equality below pass against an empty policy. The six disciplines
        // between them delete objects; this is the floor, not a count of the
        // policy, so adding a delete raises both sides together.
        Assert.True(
            registered.Count >= 8,
            $"Only {registered.Count} delete commands were recognised — the detector, not the policy, is what broke. "
            + $"A delete binding declares its confirmation through WorkspaceCommandBindings.DeleteConfirmation; "
            + $"if that wording moved, this test's shape check moved with it and found nothing.");

        Assert.Equal(
            registered,
            SurfaceCommandPolicy.DeleteCommandIds.OrderBy(id => id, StringComparer.Ordinal));
    }

    /// <summary>
    /// Both policy sets — Delete and Object-Editor — must name commands that
    /// actually exist. An Id that matches nothing is a Ribbon button that
    /// quietly stopped being routed, which looks like working software.
    /// </summary>
    [Fact]
    public async Task EveryPolicyId_NamesARegisteredDescriptor()
    {
        var registeredIds = await WithRegistryAsync(registry =>
            registry.Items.Select(descriptor => descriptor.Id).ToHashSet(StringComparer.Ordinal));

        var missing = SurfaceCommandPolicy.DeleteCommandIds
            .Concat(SurfaceCommandPolicy.ObjectEditorCommandIds)
            .Where(id => !registeredIds.Contains(id))
            .ToList();

        Assert.True(
            missing.Count == 0,
            "SurfaceCommandPolicy names commands that are not registered: " + string.Join(", ", missing));
    }

    /// <summary>
    /// The Object-Editor set is the other half of the policy, and its entries
    /// must not be deletes: routing a delete to the editor would open a tab
    /// on an object the user asked to remove.
    /// </summary>
    [Fact]
    public async Task NoObjectEditorRoutedCommand_IsADelete()
    {
        var editorDeletes = await WithRegistryAsync(registry => registry.Items
            .Where(descriptor => SurfaceCommandPolicy.ObjectEditorCommandIds.Contains(descriptor.Id))
            .Where(IsObjectDelete)
            .Select(descriptor => descriptor.Id)
            .ToList());

        Assert.Empty(editorDeletes);

        // The two sets are disjoint by construction; asserted so a future
        // edit cannot put one Id in both and leave the routing order to
        // decide which wins.
        Assert.Empty(SurfaceCommandPolicy.DeleteCommandIds.Intersect(SurfaceCommandPolicy.ObjectEditorCommandIds, StringComparer.Ordinal));
    }

    /// <summary>
    /// Every policy-routed delete must still be invocable through the
    /// canonical contract for the Kinds it claims — the policy decides
    /// <i>where</i> a Ribbon click dispatches, never whether the command
    /// exists.
    /// </summary>
    [Fact]
    public async Task EveryPolicyDelete_CarriesAnInvocableBinding_AndDeclaresItsKinds()
    {
        var faults = await WithRegistryAsync(registry =>
        {
            var found = new List<string>();

            foreach (var id in SurfaceCommandPolicy.DeleteCommandIds)
            {
                var binding = registry.Items.SingleOrDefault(descriptor => descriptor.Id == id)?.Binding;

                if (binding is null)
                    found.Add($"'{id}' has no binding at all.");
                else if (!binding.IsInvocable)
                    found.Add($"'{id}' is declared unavailable ({binding.UnavailableReason}).");
                else if (binding.Requires is not CommandContextRequirement.SelectedObject)
                    found.Add($"'{id}' does not require a selected object ({binding.Requires}).");
                else if (binding.AppliesToKinds is null or { Count: 0 })
                    found.Add($"'{id}' declares no Kinds, so it claims to delete anything.");
            }

            return found;
        });

        Assert.Empty(faults);
    }
}
