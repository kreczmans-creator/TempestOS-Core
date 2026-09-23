using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Tempest.Core.Events;

namespace Tempest.Desktop.Views;

/// <summary>
/// Keeps one <see cref="IWorkspaceChanges"/> subscription alive across
/// however many times its owning <see cref="Control"/> is shown, hidden
/// and shown again.
/// </summary>
/// <remarks>
/// <para>
/// `WP 19.7C` (2026-09-14), found by `WP 19.7B`, present since `WP 18.2A`:
/// thirteen Desktop views each carried this identical shape — the public
/// <c>WorkspaceChanges</c> property's own setter subscribed the newly
/// assigned feed and unsubscribed the old one, and each view additionally
/// wired <c>this.DetachedFromVisualTree += (_, _) =&gt; WorkspaceChanges =
/// null;</c> so a view that left the visual tree would not keep its
/// subscription alive forever. The composer
/// (<c>MainWindowComposer.cs</c>, <c>MainWindowComposer.Coordinators.cs</c>,
/// <see cref="Tempest.Desktop.Editors.ObjectEditorView.TryCreate"/>) assigns
/// the feed exactly once, by object initializer, immediately after
/// construction — nothing ever re-assigns it. The first time an affected
/// view left the visual tree (navigating to another rail area, switching a
/// project tab) it therefore lost its subscription for good: every later
/// visit still showed correct data, because every area also re-reads on
/// entry (the "load when you land here" discipline), but a change made
/// <em>while the view was already on screen</em> — the concrete report was
/// visiting Tasks, leaving to Home, returning to Tasks, then pressing New
/// task — went unseen until the area was re-entered.
/// </para>
/// <para>
/// Three things hid this for as long as they did: the on-entry re-read
/// above, which makes every revisit look correct regardless of whether the
/// live subscription still works underneath it; the Desktop journey tests,
/// each of which visits its own surface exactly once (`WP 19.7B`'s own
/// audit is what first left and returned to a surface and noticed); and
/// <see cref="Tempest.Desktop.Editors.ObjectEditorView"/>, the one
/// per-object editor, which looked as if it only ever closed. It does not:
/// the Document Area is a <see cref="TabControl"/>, and a tab control
/// detaches the content of the tab it leaves and reattaches it on return
/// (pinned by <c>TabControlDetachTests</c>), so an editor on a background
/// tab shared the defect exactly and takes this helper for that reason.
/// What remains, disclosed in the v0.19.1 notes: no host re-reads an editor
/// when its tab is reselected, so a change made while it was hidden shows
/// only at the next change or its own Save.
/// </para>
/// <para>
/// This type replaces each view's own setter-managed subscription: it
/// subscribes the assigned <see cref="Feed"/> (if any) on every
/// <see cref="Visual.AttachedToVisualTree"/> and unsubscribes on every
/// <see cref="Visual.DetachedFromVisualTree"/>, so the subscription's
/// lifetime follows the owning control's own visibility rather than a
/// single set-once assignment.
/// </para>
/// </remarks>
public sealed class WorkspaceChangesSubscription : IDisposable
{
    private readonly Control _owner;
    private readonly Action<WorkspaceChange> _handler;
    private IWorkspaceChanges? _feed;
    private bool _subscribed;

    /// <summary>
    /// Wires itself to <paramref name="owner"/>'s own attach/detach
    /// lifecycle. Subscribes nothing until <see cref="Feed"/> is set — a
    /// view that never assigns a feed (a test built directly, with no
    /// change feed threaded through) behaves exactly as before: a silent,
    /// permanent no-op.
    /// </summary>
    /// <param name="owner">The control whose visual-tree lifetime governs the subscription.</param>
    /// <param name="handler">The view's own <c>OnWorkspaceChanged</c> — invoked exactly as the feed itself invokes it; this helper does not catch, marshal or filter anything the handler does not already do.</param>
    public WorkspaceChangesSubscription(Control owner, Action<WorkspaceChange> handler)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(handler);

        _owner = owner;
        _handler = handler;

        _owner.AttachedToVisualTree += OnAttachedToVisualTree;
        _owner.DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    /// <summary>
    /// The change feed the owning control reacts to, or <see langword="null"/>
    /// for none. Setting it while the owner is attached to the visual tree
    /// swaps the subscription immediately (unsubscribes the old feed,
    /// subscribes the new one); setting it while detached only takes
    /// effect on the next attach.
    /// </summary>
    public IWorkspaceChanges? Feed
    {
        get => _feed;
        set
        {
            if (ReferenceEquals(_feed, value))
                return;

            Unsubscribe();
            _feed = value;

            if (_owner.IsAttachedToVisualTree())
                Subscribe();
        }
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e) => Subscribe();

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e) => Unsubscribe();

    private void Subscribe()
    {
        if (_subscribed || _feed is null)
            return;

        _feed.Changed += _handler;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || _feed is null)
            return;

        _feed.Changed -= _handler;
        _subscribed = false;
    }

    /// <summary>Unsubscribes and forgets the feed — equivalent to setting <see cref="Feed"/> to <see langword="null"/>, kept as its own method as the direct one-line replacement for the old <c>WorkspaceChanges = null</c> line.</summary>
    public void Clear() => Feed = null;

    /// <summary>Unsubscribes and stops reacting to the owner's own attach/detach lifecycle.</summary>
    public void Dispose()
    {
        Unsubscribe();
        _owner.AttachedToVisualTree -= OnAttachedToVisualTree;
        _owner.DetachedFromVisualTree -= OnDetachedFromVisualTree;
        _feed = null;
    }
}
