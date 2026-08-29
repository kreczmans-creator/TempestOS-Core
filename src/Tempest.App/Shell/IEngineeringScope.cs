using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Shell;

/// <summary>Which of the two valid engineering scopes the user is working in.</summary>
public enum EngineeringScopeKind
{
    /// <summary>Engineering work inside a project — the project-centric workflow.</summary>
    Project,

    /// <summary>Engineering work belonging to no project — quick calculations and calculation sets (`TD-89`).</summary>
    Standalone,
}

/// <summary>The scope the Engineering Workspace is currently operating in, as a value.</summary>
/// <param name="Kind">Project or standalone.</param>
/// <param name="ProjectId">The project, or <see langword="null"/> when standalone.</param>
/// <param name="Label">A human-readable name for the scope, suitable for a status bar or header.</param>
public sealed record EngineeringScopeDescriptor(EngineeringScopeKind Kind, Guid? ProjectId, string Label)
{
    /// <summary>The standalone scope — the one every session starts in until a project is opened.</summary>
    public static readonly EngineeringScopeDescriptor Standalone =
        new(EngineeringScopeKind.Standalone, null, "Standalone engineering");
}

/// <summary>
/// What the Engineering Workspace is allowed to see, and why.
/// </summary>
/// <remarks>
/// <para>
/// The Engineering Workspace does not decide its own scope and does not
/// infer it from what happens to be on screen. It reads it here, derived
/// from the two pieces of real application state that already own the
/// answer: <see cref="IShellNavigator.Current"/> (which carries the
/// project the user navigated into, or none) and
/// <see cref="Projects.IProjectContext"/> (the open project itself).
/// </para>
/// <para>
/// Both scopes are first-class. A project scope lists that project's own
/// engineering objects, transitively; the standalone scope lists the
/// engineering objects that belong to no project. Neither is a filtered
/// view of the other, and neither is a fallback for the other failing.
/// </para>
/// </remarks>
public interface IEngineeringScope
{
    /// <summary>Gets the scope implied by where the user currently is.</summary>
    EngineeringScopeDescriptor Current { get; }

    /// <summary>Every live engineering object in the current scope.</summary>
    Task<IReadOnlyList<IEngineeringObject>> ListObjectsAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether <paramref name="objectId"/> belongs to the current scope — the question a surface asks before showing an object as "in this project".</summary>
    Task<bool> ContainsAsync(Guid objectId, CancellationToken cancellationToken = default);
}
