namespace Tempest.App.Projects;

/// <summary>Thrown when a project is created with an identifier another project already carries.</summary>
public sealed class DuplicateProjectIdentifierException : InvalidOperationException
{
    /// <summary>Initialises a new instance of the <see cref="DuplicateProjectIdentifierException"/> class.</summary>
    /// <param name="identifier">The already-taken project identifier.</param>
    public DuplicateProjectIdentifierException(string identifier)
        : base($"A project with identifier '{identifier}' already exists.")
    {
        Identifier = identifier;
    }

    /// <summary>Gets the already-taken project identifier.</summary>
    public string Identifier { get; }
}
