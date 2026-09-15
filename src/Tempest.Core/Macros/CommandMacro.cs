namespace Tempest.Core.Macros;

/// <summary>The one, concrete <see cref="ICommandMacro"/> implementation — an immutable snapshot, mirroring <see cref="Commands.CommandDescriptor"/>'s own established shape.</summary>
public sealed class CommandMacro : ICommandMacro
{
    /// <summary>Initialises a new instance of the <see cref="CommandMacro"/> class.</summary>
    /// <param name="id">The macro's own unique, stable Id.</param>
    /// <param name="name">The macro's own human-readable display name.</param>
    /// <param name="steps">The ordered steps this macro invokes when run.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is <see langword="null"/>, empty, or whitespace, or <paramref name="steps"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="steps"/> is <see langword="null"/>.</exception>
    public CommandMacro(Guid id, string name, IReadOnlyList<MacroStep> steps)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name must not be null, empty, or whitespace.", nameof(name));

        ArgumentNullException.ThrowIfNull(steps);

        if (steps.Count == 0)
            throw new ArgumentException("A macro must have at least one step.", nameof(steps));

        Id = id;
        Name = name;
        Steps = steps;
    }

    /// <inheritdoc />
    public Guid Id { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public IReadOnlyList<MacroStep> Steps { get; }
}
