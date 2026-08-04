namespace Tempest.Core.EngineeringDomain;

public class EngineeringDomainException : Exception
{
    public EngineeringDomainException(string message) : base(message)
    {
    }

    public EngineeringDomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public sealed class EngineeringObjectNotFoundException : EngineeringDomainException
{
    public Guid ObjectId { get; }

    public EngineeringObjectNotFoundException(Guid objectId)
        : base($"No Engineering Object with Id '{objectId}' was found.")
    {
        ObjectId = objectId;
    }
}

public sealed class InvalidLifecycleTransitionException : EngineeringDomainException
{
    public LifecycleState From { get; }
    public LifecycleState To { get; }

    public InvalidLifecycleTransitionException(LifecycleState from, LifecycleState to)
        : base($"The lifecycle transition from '{from}' to '{to}' is not permitted.")
    {
        From = from;
        To = to;
    }
}

public sealed class SelfReferentialRelationshipException : EngineeringDomainException
{
    public Guid ObjectId { get; }

    public SelfReferentialRelationshipException(Guid objectId)
        : base($"An Engineering Object cannot hold a relationship to itself ('{objectId}') — {StructuralValidationRules.NoSelfReference}.")
    {
        ObjectId = objectId;
    }
}
