namespace Tempest.App.Projects;

/// <summary>Thrown when a project is opened by an Id no project carries.</summary>
public sealed class ProjectNotFoundException : InvalidOperationException
{
    /// <summary>Initialises a new instance of the <see cref="ProjectNotFoundException"/> class.</summary>
    /// <param name="projectId">The Id that resolved to no project.</param>
    public ProjectNotFoundException(Guid projectId)
        : base($"No project exists with Id '{projectId}'.")
    {
        ProjectId = projectId;
    }

    /// <summary>Gets the Id that resolved to no project.</summary>
    public Guid ProjectId { get; }
}
