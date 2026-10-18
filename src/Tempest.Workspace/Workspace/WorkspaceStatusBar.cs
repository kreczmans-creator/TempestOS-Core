using Tempest.Core.Events;

namespace Tempest.Workspace;

/// <summary>
/// The Status Bar's own current text — the one region of `WP8.0A UI
/// Architecture.md` §1's own five-region layout with no dedicated public
/// contract among the twelve `WP8.0B Workspace Contracts.md` names, since
/// none of the twelve required it. Reacts to
/// <see cref="WorkspaceSelectionChangedEvent"/> exactly as
/// <see cref="PropertyInspector"/> does — the identical "who reacts to
/// what" wiring, applied a second time. Public, not
/// same-assembly-only-via-<c>InternalsVisibleTo</c>, since `WP 17.2B`:
/// both <see cref="Tempest.Harness"/>'s own <c>WorkspaceShell</c> and
/// <c>Tempest.Desktop</c>'s own Status Bar segment need it, and they are
/// now two separate assemblies with no reference to each other.
/// </summary>
public sealed class WorkspaceStatusBar : IEventHandler<WorkspaceSelectionChangedEvent>
{
    private Func<Guid, CancellationToken, Task<string?>>? _displayNameResolver;

    /// <summary>Gets the Status Bar's own current text.</summary>
    public string StatusText { get; private set; } = "Ready.";

    /// <summary>
    /// Supplies the lookup that turns a selected object's id into the name
    /// the operator knows it by (`TD-186`). Without one the segment falls
    /// back to the id — the pre-`v1.0` wording, kept so a harness with no
    /// repository at hand still reads something true.
    /// </summary>
    /// <param name="resolver">Returns the object's display name, or <see langword="null"/> when the id names nothing live.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resolver"/> is <see langword="null"/>.</exception>
    public void UseDisplayNameResolver(Func<Guid, CancellationToken, Task<string?>> resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);

        _displayNameResolver = resolver;
    }

    /// <summary>Sets the Status Bar's own current text directly — used for area-switch and lifecycle status, which have no dedicated event of their own in this Work Package's own scope.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public void SetStatus(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        StatusText = text;
    }

    /// <inheritdoc />
    public async Task HandleAsync(WorkspaceSelectionChangedEvent @event, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (@event.Current is not { } current)
        {
            StatusText = "Ready.";
            return;
        }

        // `TD-186`: "Selected: Calculation 73e89395-…" is a GUID where a name
        // should be. Ask the resolver for the name; a lookup that fails or
        // finds nothing must not take the segment down with it, so the id
        // stays as the honest fallback.
        string? name = null;
        if (_displayNameResolver is { } resolve)
        {
            try
            {
                name = await resolve(current.ObjectId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                name = null;
            }
        }

        StatusText = string.IsNullOrWhiteSpace(name)
            ? $"Selected: {current.Kind} {current.ObjectId}"
            : $"Selected: {current.Kind} {name}";
    }
}
