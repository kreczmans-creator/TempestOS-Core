namespace Tempest.Core.Requirements;

/// <summary>The abstract base for every exception this namespace throws.</summary>
public abstract class RequirementsException : Exception
{
    /// <summary>Initialises a new instance of the <see cref="RequirementsException"/> class.</summary>
    protected RequirementsException(string message) : base(message)
    {
    }
}
