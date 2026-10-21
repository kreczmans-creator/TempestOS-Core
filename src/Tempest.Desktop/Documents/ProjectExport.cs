using Tempest.Core.Projects;
using Tempest.Desktop.Quotations;
using Tempest.Workspace.Projects;

namespace Tempest.Desktop.Documents;

/// <summary>
/// The shared naming and starting folder for a document exported from a
/// project (`v0.25.1` PO request: every export carries its project, as a
/// quote does). The name comes from <see cref="ProjectNumbering.ExportReference"/>;
/// the save dialog starts in the project's own folder, the one quote
/// export already starts in.
/// </summary>
internal static class ProjectExport
{
    /// <summary>Today's date as an export distinguisher: <c>2026-10-21</c>.</summary>
    public static string Today() => DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The folder a project's <paramref name="designator"/> export starts in — its standard subfolder (`v0.25.2`) — or <see langword="null"/> when there is none to offer (no project, folders off, or the folder cannot be reached).</summary>
    public static async Task<string?> StartFolderAsync(ProjectFolderLocator? folders, Guid? projectId, string designator, CancellationToken cancellationToken = default)
    {
        if (folders is null || projectId is not { } id)
            return null;

        try
        {
            return await folders.ExportFolderForAsync(id, designator, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }
}
