using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Projects;

/// <summary>Why an <see cref="IProjectLifecycleService"/> act was refused, or <see cref="None"/> if it was not.</summary>
/// <remarks>
/// A refusal is a first-class answer here, exactly as
/// <c>Tempest.Core.Quotations.QuotationRefusal</c> is (`WP 19.5C`).
/// </remarks>
public enum ProjectLifecycleRefusal
{
    /// <summary>Nothing was refused.</summary>
    None,

    /// <summary>No project is registered under the requested id.</summary>
    ProjectNotFound,

    /// <summary>The project is already on hold.</summary>
    AlreadyHeld,

    /// <summary>The project is not on hold, so it cannot be resumed.</summary>
    NotHeld,

    /// <summary>The project is closed, so it cannot be put on hold or resumed.</summary>
    ProjectClosed,

    /// <summary>The project is already closed, so it cannot be signed off again.</summary>
    AlreadyClosed,

    /// <summary>The project is not closed, so it cannot be reopened.</summary>
    NotClosed,

    /// <summary>The project closed 90 days or more ago — it is Archive, read-only, and cannot be reopened.</summary>
    ReopenWindowElapsed,
}

/// <summary>The outcome of an <see cref="IProjectLifecycleService"/> act: either it happened, or a refusal that says why it did not.</summary>
/// <param name="Refusal">Why the act was refused, or <see cref="ProjectLifecycleRefusal.None"/>.</param>
/// <param name="Reason">The refusal in a sentence an engineer can read. <see langword="null"/> when nothing was refused.</param>
/// <param name="Project">The project acted on, when it could be resolved.</param>
public sealed record ProjectLifecycleResult(ProjectLifecycleRefusal Refusal, string? Reason, Project? Project)
{
    /// <summary>Whether the act actually happened.</summary>
    public bool Succeeded => Refusal == ProjectLifecycleRefusal.None;
}

/// <summary>
/// A project's own lifecycle, distinct from the generic, unused
/// <see cref="LifecycleState"/> every <c>EngineeringObjectBase</c> carries
/// (`WP 19.5C`, Product Owner comment item 6): hold and resume (a pause,
/// not a close), sign off (closes the project, recording who, when and a
/// statement), and reopen (Closed only, within the 90-day window before a
/// project becomes Archive — see <see cref="ProjectArchival"/>). Every act
/// is one transaction with an audit row; whether an act is
/// <em>permitted</em> is decided here, before <see cref="Project"/>'s own
/// mutator ever runs, and reported back as a refusal result rather than an
/// exception, mirroring <c>Tempest.Core.Quotations.IQuotationService</c>.
/// </summary>
public interface IProjectLifecycleService
{
    /// <summary>Puts <paramref name="projectId"/> on hold, recording <paramref name="reason"/>. Refused, as a result, when the project is closed or already on hold.</summary>
    Task<ProjectLifecycleResult> HoldAsync(Guid projectId, string reason, CancellationToken cancellationToken = default);

    /// <summary>Resumes <paramref name="projectId"/> from hold. Refused, as a result, when the project is not on hold.</summary>
    Task<ProjectLifecycleResult> ResumeAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs off <paramref name="projectId"/>: records who (the current
    /// principal), when (today) and <paramref name="statement"/> as a
    /// <see cref="ProjectSignOff"/>, and closes the project.
    /// </summary>
    /// <remarks>Refused, as a result, when the project is already closed.</remarks>
    Task<ProjectLifecycleResult> SignOffAsync(Guid projectId, string statement, CancellationToken cancellationToken = default);

    /// <summary>Reopens <paramref name="projectId"/> — Closed only, and only within <see cref="ProjectArchival.ArchiveAfterDays"/> days of closing.</summary>
    /// <remarks>Refused, as a result, when the project is not closed, or has already become Archive.</remarks>
    Task<ProjectLifecycleResult> ReopenAsync(Guid projectId, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IProjectLifecycleService"/> implementation (`WP 19.5C`).</summary>
public sealed class ProjectLifecycleService : IProjectLifecycleService
{
    private readonly EngineeringDomainContext _context;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="ProjectLifecycleService"/> class.</summary>
    /// <param name="context">The domain context every write commits through.</param>
    /// <param name="timeProvider">The clock "today" and the 90-day archival window are read from. <see langword="null"/> — the default — is <see cref="TimeProvider.System"/>; a test supplies a controllable one.</param>
    public ProjectLifecycleService(EngineeringDomainContext context, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<ProjectLifecycleResult> HoldAsync(Guid projectId, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var project = await FindProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
            return NotFound(projectId);

        if (ProjectArchival.IsClosed(project))
            return new ProjectLifecycleResult(ProjectLifecycleRefusal.ProjectClosed, $"Project '{projectId}' is closed; it cannot be put on hold.", project);

        if (project.Held)
            return new ProjectLifecycleResult(ProjectLifecycleRefusal.AlreadyHeld, $"Project '{projectId}' is already on hold ({project.HoldReason}).", project);

        await project.HoldAsync(reason.Trim(), cancellationToken).ConfigureAwait(false);

        return new ProjectLifecycleResult(ProjectLifecycleRefusal.None, null, project);
    }

    /// <inheritdoc />
    public async Task<ProjectLifecycleResult> ResumeAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await FindProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
            return NotFound(projectId);

        if (!project.Held)
            return new ProjectLifecycleResult(ProjectLifecycleRefusal.NotHeld, $"Project '{projectId}' is not on hold.", project);

        await project.ResumeAsync(cancellationToken).ConfigureAwait(false);

        return new ProjectLifecycleResult(ProjectLifecycleRefusal.None, null, project);
    }

    /// <inheritdoc />
    public async Task<ProjectLifecycleResult> SignOffAsync(Guid projectId, string statement, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statement);

        var project = await FindProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
            return NotFound(projectId);

        if (ProjectArchival.IsClosed(project))
            return new ProjectLifecycleResult(ProjectLifecycleRefusal.AlreadyClosed, $"Project '{projectId}' is already closed (signed off {project.SignOff?.SignedOn:O}).", project);

        var today = Today();
        var signOff = new ProjectSignOff(_context.ResolveCurrentPrincipalId(), today, statement.Trim());

        await project.SignOffAsync(signOff, today, cancellationToken).ConfigureAwait(false);

        return new ProjectLifecycleResult(ProjectLifecycleRefusal.None, null, project);
    }

    /// <inheritdoc />
    public async Task<ProjectLifecycleResult> ReopenAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await FindProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
            return NotFound(projectId);

        if (!ProjectArchival.IsClosed(project))
            return new ProjectLifecycleResult(ProjectLifecycleRefusal.NotClosed, $"Project '{projectId}' is not closed.", project);

        if (ProjectArchival.IsArchived(project, _time.GetUtcNow()))
        {
            return new ProjectLifecycleResult(
                ProjectLifecycleRefusal.ReopenWindowElapsed,
                $"Project '{projectId}' closed on {project.ClosedOn:O}, {ProjectArchival.ArchiveAfterDays} days or more ago; it is Archive and read-only. It cannot be reopened.",
                project);
        }

        await project.ReopenAsync(cancellationToken).ConfigureAwait(false);

        return new ProjectLifecycleResult(ProjectLifecycleRefusal.None, null, project);
    }

    private DateOnly Today() => DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

    private async Task<Project?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) as Project;

    private static ProjectLifecycleResult NotFound(Guid projectId) =>
        new(ProjectLifecycleRefusal.ProjectNotFound, $"No project '{projectId}' is registered.", null);
}
