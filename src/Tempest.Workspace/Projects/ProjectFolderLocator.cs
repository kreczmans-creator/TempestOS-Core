using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Projects;

namespace Tempest.Workspace.Projects;

/// <summary>
/// Resolves a project into the customer and project names
/// <see cref="ProjectFolderService"/> files it under, and asks the service
/// for its folder (PO decision 2026-10-01: "Folders should be generated in
/// D:\01 Projects on this computer. It should first search for the
/// customer, if none then make new, likewise project ref").
/// </summary>
/// <remarks>
/// <para>
/// <b>Reads the project and its customer as they are now.</b> The project
/// comes from <see cref="IProjectDirectory"/> (never a cached summary), and
/// its customer from the Organisation catalogue through
/// <see cref="ProjectSummary.ClientOrganisationId"/> (`WP 19.0A`,
/// `ADR-0150`). A project with no client, or whose client record has
/// gone, is still filed — under <see cref="ProjectFolderService.NoCustomerFolderName"/>,
/// or under the raw client id respectively.
/// </para>
/// <para>
/// <b>The customer code is <see cref="Organisation.Reference"/> today</b> —
/// the short reference the organisation is known by. <see cref="CustomerCodeOf"/>
/// is the one place to change if a dedicated customer code is introduced.
/// </para>
/// <para>
/// <b>Never throws for an unreadable catalogue or a refusing file
/// system</b> — every failure is folded into the returned
/// <see cref="ProjectFolderOutcome"/>, so a caller on the UI thread can
/// report it in the status bar and carry on.
/// </para>
/// </remarks>
public sealed class ProjectFolderLocator
{
    private readonly ProjectFolderService _service;
    private readonly IProjectDirectory _projects;
    private readonly IOrganisationCatalog _organisations;

    /// <summary>Initialises a new instance of the <see cref="ProjectFolderLocator"/> class.</summary>
    /// <param name="service">The folder service that touches the disk.</param>
    /// <param name="projects">Where each project is read from.</param>
    /// <param name="organisations">Where each project's customer is read from.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public ProjectFolderLocator(ProjectFolderService service, IProjectDirectory projects, IOrganisationCatalog organisations)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(organisations);

        _service = service;
        _projects = projects;
        _organisations = organisations;
    }

    /// <summary>The folder service this locator asks.</summary>
    public ProjectFolderService Service => _service;

    /// <summary>Ensures <paramref name="projectId"/>'s own folder tree exists (see <see cref="ProjectFolderService.Ensure"/>).</summary>
    /// <param name="projectId">The project to file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task<ProjectFolderOutcome> EnsureAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var request = await RequestForAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (request is null)
            return new ProjectFolderOutcome(ProjectFolderStatus.Failed, "Project folder: the project could not be found.", null, []);

        return await Task.Run(() => _service.Ensure(request), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The folder a quote PDF export for <paramref name="projectId"/>
    /// should start in (see <see cref="ProjectFolderService.QuoteFolderFor"/>),
    /// or <see langword="null"/> when there is none to offer — the caller
    /// then behaves exactly as it did before project folders existed.
    /// </summary>
    /// <param name="projectId">The project the quote belongs to.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task<string?> QuoteFolderForAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var request = await RequestForAsync(projectId, cancellationToken).ConfigureAwait(false);
        return request is null ? null : await Task.Run(() => _service.QuoteFolderFor(request), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The customer code a project's folder is matched and named on — <see cref="Organisation.Reference"/> today (see this class's own remarks).</summary>
    /// <param name="organisation">The customer.</param>
    public static string? CustomerCodeOf(Organisation organisation)
    {
        ArgumentNullException.ThrowIfNull(organisation);
        return organisation.Reference;
    }

    private async Task<ProjectFolderRequest?> RequestForAsync(Guid projectId, CancellationToken cancellationToken)
    {
        ProjectSummary? project;
        try
        {
            project = await _projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }

        if (project is null)
            return null;

        string? customerCode = null;
        string? customerName = null;

        if (!string.IsNullOrWhiteSpace(project.ClientOrganisationId))
        {
            customerName = project.ClientOrganisationId;
            try
            {
                if (await _organisations.FindAsync(project.ClientOrganisationId, cancellationToken).ConfigureAwait(false) is { } record)
                {
                    customerCode = CustomerCodeOf(record.Definition);
                    customerName = record.Definition.Name;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // An unreadable catalogue still files the project — under
                // the raw client id — rather than refusing the folder.
            }
        }

        return new ProjectFolderRequest(project.Identifier ?? string.Empty, project.DisplayName, customerCode, customerName);
    }
}
