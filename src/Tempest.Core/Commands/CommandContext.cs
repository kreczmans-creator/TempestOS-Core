namespace Tempest.Core.Commands;

/// <summary>
/// One engineering object the application currently has selected, as the
/// Command Framework sees it — an identity and a <c>Kind</c>, nothing more.
/// </summary>
/// <param name="ObjectId">The selected object's own Id.</param>
/// <param name="Kind">The selected object's own <c>Kind</c> — for example, <c>"Requirement"</c>.</param>
public sealed record CommandContextObject(Guid ObjectId, string Kind);

/// <summary>
/// Everything the application knows at the moment a command is invoked by
/// Id — the data a <see cref="CommandBinding"/> is allowed to read when it
/// constructs its own <see cref="ICommand"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately, and permanently, just the selection.</b> This carries a
/// selection and nothing else: no <c>IServiceProvider</c>, no active view,
/// no project, no shell or UI object, no property bag, no ambient state.
/// Every field here is one an existing production command handler actually
/// reads. A context that could carry anything would be a service locator
/// wearing a record's clothes, and <c>ADR-0037</c> rejected service
/// location for this framework explicitly.
/// </para>
/// <para>
/// <b>A project is deliberately absent.</b> The Work Package that
/// introduced this type audited all 74 production discipline commands and
/// their handlers for any read of project scope
/// (<c>ProjectId</c>/<c>IShellNavigator</c>/<c>IEngineeringScope</c>/
/// <c>ProjectMembership</c>) and found none — so no project field was
/// carried speculatively. Note that <c>"Project"</c> is also a Mechanical
/// Product Structure <c>Kind</c>; that is an assembly-tree root and is
/// entirely unrelated to the shell's own project scope.
/// </para>
/// <para>
/// <b>Core-only, by construction.</b> This mirrors the shape of
/// <c>Tempest.App.Workspace.WorkspaceSelection</c> without depending on it:
/// <see cref="CommandDescriptor"/> lives in <c>Tempest.Core</c>, which
/// cannot reference <c>Tempest.App</c>. An application-side adapter
/// populates this from whatever it uses to track selection.
/// </para>
/// </remarks>
public sealed class CommandContext
{
    /// <summary>
    /// Initialises a new instance of the <see cref="CommandContext"/> class.
    /// </summary>
    /// <param name="selection">
    /// The currently selected objects, in selection order. The first entry
    /// is <see cref="Primary"/>. Empty means nothing is selected.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="selection"/>, or any entry in it, is <see langword="null"/>.</exception>
    public CommandContext(IReadOnlyList<CommandContextObject> selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        // Copied rather than aliased: a context handed to a binding must
        // describe the moment it was built, not a live view a caller can
        // still mutate underneath the command being constructed from it.
        var copy = new CommandContextObject[selection.Count];

        for (var i = 0; i < selection.Count; i++)
        {
            copy[i] = selection[i]
                ?? throw new ArgumentNullException(nameof(selection), $"Selection entry {i} is null.");
        }

        Selection = copy;
    }

    /// <summary>An empty context — nothing selected.</summary>
    public static CommandContext Empty { get; } = new([]);

    /// <summary>Creates a context with exactly one selected object.</summary>
    /// <param name="objectId">The selected object's own Id.</param>
    /// <param name="kind">The selected object's own <c>Kind</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public static CommandContext For(Guid objectId, string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        return new([new CommandContextObject(objectId, kind)]);
    }

    /// <summary>
    /// Gets the currently selected objects, in selection order. Never
    /// <see langword="null"/>; empty if nothing is selected.
    /// </summary>
    public IReadOnlyList<CommandContextObject> Selection { get; }

    /// <summary>
    /// Gets the first selected object, or <see langword="null"/> if nothing
    /// is selected — what a single-target command acts on.
    /// </summary>
    public CommandContextObject? Primary => Selection.Count > 0 ? Selection[0] : null;
}
