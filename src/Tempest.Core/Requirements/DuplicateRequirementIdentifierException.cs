namespace Tempest.Core.Requirements;

/// <summary>Thrown when <see cref="IRequirementsService.CreateAsync"/> is called with an <c>identifier</c> already registered.</summary>
public sealed class DuplicateRequirementIdentifierException : RequirementsException
{
    /// <summary>The identifier that was already registered.</summary>
    public string Identifier { get; }

    /// <summary>Initialises a new instance of the <see cref="DuplicateRequirementIdentifierException"/> class.</summary>
    public DuplicateRequirementIdentifierException(string identifier)
        : base($"A requirement is already registered under identifier '{identifier}'.")
    {
        Identifier = identifier;
    }
}
