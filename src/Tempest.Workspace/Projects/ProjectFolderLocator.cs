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
/// or under the raw client id respectively. A catalogue that cannot be
/// read is reported as <see cref="ProjectFolderStatus.Failed"/> instead,
/// never filed under the raw id.
/// </para>
/// <para>
/// <b>The customer code is the one frozen in the project's identifier</b>
/// (<see cref="ProjectNumbering.TryParseProjectIdentifier"/>) — the
/// five-character code project numbers start with (ADR-0156), and the
/// customer folder's whole name (runbook feedback C6) — and only for an
/// identifier of another shape the client's current
/// <see cref="Organisation.CustomerCode"/>. An organisation recorded
/// before customer codes existed has none, so its folder is named after
/// its own name instead.
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
        var (request, failure) = await RequestForAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (request is null)
            return new ProjectFolderOutcome(ProjectFolderStatus.Failed, failure ?? "Project folder: the project could not be found.", null, []);

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
        var (request, _) = await RequestForAsync(projectId, cancellationToken).ConfigureAwait(false);
        return request is null ? null : await Task.Run(() => _service.QuoteFolderFor(request), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The customer code a project's folder is matched and named on — <see cref="Organisation.CustomerCode"/>, trimmed; <see langword="null"/> when it has none, so the folder falls back to the customer's name (see this class's own remarks).</summary>
    /// <param name="organisation">The customer.</param>
    public static string? CustomerCodeOf(Organisation organisation)
    {
        ArgumentNullException.ThrowIfNull(organisation);
        return string.IsNullOrWhiteSpace(organisation.CustomerCode) ? null : organisation.CustomerCode.Trim();
    }

    private async Task<(ProjectFolderRequest? Request, string? Failure)> RequestForAsync(Guid projectId, CancellationToken cancellationToken)
    {
        ProjectSummary? project;
        try
        {
            project = await _projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (null, null);
        }

        if (project is null)
            return (null, null);

        // The customer code frozen in the project's own identifier comes
        // first (ADR-0156: a document number's prefix is never re-derived
        // from the client's current code, and neither is its folder), so
        // editing a customer's code never starts a second tree.
        string? customerCode = ProjectNumbering.TryParseProjectIdentifier(project.Identifier, out var frozenCode, out _)
            ? frozenCode
            : null;
        string? customerName = null;

        if (!string.IsNullOrWhiteSpace(project.ClientOrganisationId))
        {
            customerName = project.ClientOrganisationId;
            try
            {
                if (await _organisations.FindAsync(project.ClientOrganisationId, cancellationToken).ConfigureAwait(false) is { } record)
                {
                    customerCode ??= CustomerCodeOf(record.Definition);
                    customerName = record.Definition.Name;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Refuse rather than file the project under its raw client
                // id — a folder created there would be orphaned the moment
                // the catalogue reads again.
                return (null, $"Project folder: the customer '{project.ClientOrganisationId}' could not be read ({ex.Message}).");
            }
        }

        return (new ProjectFolderRequest(project.Identifier ?? string.Empty, project.DisplayName, customerCode, customerName), null);
    }
}
