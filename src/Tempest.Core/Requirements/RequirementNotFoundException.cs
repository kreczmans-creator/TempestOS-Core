namespace Tempest.Core.Requirements;

/// <summary>Thrown when an operation references a requirement Id that does not exist.</summary>
public sealed class RequirementNotFoundException : RequirementsException
{
    /// <summary>The requirement Id that does not exist.</summary>
    public Guid RequirementId { get; }

    /// <summary>Initialises a new instance of the <see cref="RequirementNotFoundException"/> class.</summary>
    public RequirementNotFoundException(Guid requirementId)
        : base($"No requirement exists with Id '{requirementId}'.")
    {
        RequirementId = requirementId;
    }
}
