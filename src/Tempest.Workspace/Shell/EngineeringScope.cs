using Tempest.App.Projects;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Shell;

/// <summary>
/// The concrete <see cref="IEngineeringScope"/> — a read-only projection
/// of navigation state onto the engineering object graph.
/// </summary>
/// <remarks>
/// Holds no state of its own and persists nothing. Every answer is
/// computed from <see cref="IShellNavigator.Current"/> and the live
/// repository at the moment it is asked, so the scope can never drift out
/// of step with where the user actually is — the failure mode a cached
/// "current project id" field would reintroduce.
/// </remarks>
public sealed class EngineeringScope : IEngineeringScope
{
    private readonly IShellNavigator _navigator;
    private readonly IProjectContext _projectContext;
    private readonly EngineeringDomainContext _domain;

    /// <summary>Initialises a new instance of the <see cref="EngineeringScope"/> class.</summary>
    /// <exception cref="ArgumentNullException">Any parameter is <see langword="null"/>.</exception>
    public EngineeringScope(IShellNavigator navigator, IProjectContext projectContext, EngineeringDomainContext domain)
    {
        ArgumentNullException.ThrowIfNull(navigator);
        ArgumentNullException.ThrowIfNull(projectContext);
        ArgumentNullException.ThrowIfNull(domain);

        _navigator = navigator;
        _projectContext = projectContext;
        _domain = domain;
    }

    /// <inheritdoc />
    public EngineeringScopeDescriptor Current
    {
        get
        {
            // The location is authoritative: it is what the user navigated
            // to. The context supplies the label for it.
            if (_navigator.Current.ProjectId is not { } projectId)
                return EngineeringScopeDescriptor.Standalone;

            var label = _projectContext.Current is { Id: var openId } open && openId == projectId
                ? open.Label
                : projectId.ToString();

            return new EngineeringScopeDescriptor(EngineeringScopeKind.Project, projectId, label);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IEngineeringObject>> ListObjectsAsync(CancellationToken cancellationToken = default) =>
        Current.ProjectId is { } projectId
            ? ProjectMembership.ListProjectMembersAsync(_domain.Repository, projectId, cancellationToken)
            : ProjectMembership.ListStandaloneAsync(_domain.Repository, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> ContainsAsync(Guid objectId, CancellationToken cancellationToken = default)
    {
        var owner = await ProjectMembership
            .ResolveOwningProjectAsync(_domain.Repository, objectId, cancellationToken)
            .ConfigureAwait(false);

        return owner == Current.ProjectId;
    }
}
