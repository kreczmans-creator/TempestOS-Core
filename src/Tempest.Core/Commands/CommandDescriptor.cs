namespace Tempest.Core.Commands;

/// <summary>
/// Describes one invokable command for a caller that has only a string
/// Id — a menu, a toolbar, a keyboard shortcut, automation, or a future AI
/// service — without that caller ever needing the command's own concrete
/// type at compile time.
/// </summary>
/// <remarks>
/// An immutable snapshot, mirroring <see cref="Navigation.NavigationItem"/>
/// directly: the platform's own Registry pattern, applied a third time. See
/// <c>Command Framework Architecture.md</c> for the complete design.
/// </remarks>
public sealed class CommandDescriptor
{
    /// <summary>
    /// Initialises a new instance of the <see cref="CommandDescriptor"/> class.
    /// </summary>
    /// <param name="id">The command's unique, stable identifier.</param>
    /// <param name="displayName">The command's human-readable display name.</param>
    /// <param name="category">An optional grouping label (for example, a menu section).</param>
    /// <param name="description">An optional, longer description (for example, for a tooltip or an AI service).</param>
    /// <param name="icon">An optional, symbolic icon key — never a rendered image or UI framework resource.</param>
    /// <param name="canExecute">
    /// An optional predicate, evaluated by the caller at query time;
    /// <see langword="null"/> means always available.
    /// </param>
    /// <param name="createDefault">
    /// An optional factory constructing a default, parameterless instance of
    /// this command, used by <see cref="ICommandRegistry.InvokeAsync"/>.
    /// <see langword="null"/> means this command cannot be invoked by Id —
    /// only through <see cref="ICommandDispatcher.DispatchAsync{TCommand}"/>
    /// by a caller that already has the data it needs.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> or <paramref name="displayName"/> is
    /// <see langword="null"/>, empty, or whitespace.
    /// </exception>
    public CommandDescriptor(
        string id,
        string displayName,
        string? category = null,
        string? description = null,
        string? icon = null,
        Func<bool>? canExecute = null,
        Func<ICommand>? createDefault = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Id must not be null, empty, or whitespace.", nameof(id));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("DisplayName must not be null, empty, or whitespace.", nameof(displayName));

        Id = id;
        DisplayName = displayName;
        Category = category;
        Description = description;
        Icon = icon;
        CanExecute = canExecute;
        CreateDefault = createDefault;
    }

    /// <summary>Gets the command's unique, stable identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the command's human-readable display name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the command's optional grouping label.</summary>
    public string? Category { get; }

    /// <summary>Gets the command's optional, longer description.</summary>
    public string? Description { get; }

    /// <summary>Gets the command's optional, symbolic icon key.</summary>
    public string? Icon { get; }

    /// <summary>
    /// Gets the optional predicate reporting whether this command is
    /// currently available. Evaluated by the caller, not by
    /// <see cref="ICommandRegistry"/> itself — see
    /// <c>Command Framework Architecture.md</c>'s "Command Availability and
    /// Enable/Disable Behaviour."
    /// </summary>
    public Func<bool>? CanExecute { get; }

    /// <summary>
    /// Gets the optional factory constructing a default, parameterless
    /// instance of this command. <see langword="null"/> if this command
    /// cannot be invoked by Id.
    /// </summary>
    public Func<ICommand>? CreateDefault { get; }

    /// <summary>
    /// Gets how this command is constructed from the application's current
    /// <see cref="CommandContext"/>, or <see langword="null"/> if it
    /// declares no binding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The additive extension <c>Command Framework Architecture.md</c>
    /// already anticipated: <see cref="CreateDefault"/> serves a command
    /// needing no caller-supplied data, and was never widened to
    /// <c>Func&lt;object?, ICommand&gt;</c> because no consumer needed it
    /// yet. A binding is that widening, done as the document said it would
    /// be — on this type alone, with <see cref="CreateDefault"/> untouched
    /// and every existing descriptor unaffected.
    /// </para>
    /// <para>
    /// A descriptor may carry a binding, a <see cref="CreateDefault"/>, or
    /// neither. Neither means the command is reachable only through
    /// <see cref="ICommandDispatcher.DispatchAsync{TCommand}"/>; a binding
    /// built by <see cref="CommandBinding.Unavailable"/> means the same
    /// thing but says why.
    /// </para>
    /// <para>
    /// <b>Set through an object initialiser, not a constructor parameter —
    /// deliberately.</b> Appending an optional parameter to the constructor
    /// would compile every existing call site unchanged while breaking any
    /// <i>already-compiled</i> assembly bound to its exact signature, and
    /// this platform loads plugin assemblies it did not compile
    /// (<c>ADR-0111</c>), one of whose sanctioned acts is registering a
    /// command descriptor. Adding a second overload instead is ambiguous at
    /// any named-argument call site, because the two would differ only by a
    /// trailing optional parameter. An <see langword="init"/> accessor has
    /// neither problem: the constructor is untouched, so every compiled
    /// caller keeps working, and the descriptor is still immutable once
    /// built.
    /// </para>
    /// </remarks>
    public CommandBinding? Binding { get; init; }
}
