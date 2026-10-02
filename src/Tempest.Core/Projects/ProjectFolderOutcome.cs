namespace Tempest.Core.Projects;

/// <summary>Which project <see cref="ProjectFolderService.Ensure"/> should find or create a folder for (PO decision 2026-10-01).</summary>
/// <param name="ProjectIdentifier">The project's own reference/identifier (for example <c>P-0027</c>). Required.</param>
/// <param name="ProjectName">The project's own display name. Required.</param>
/// <param name="CustomerCode">The customer's own short code, if it has one; the customer folder is named after it, and matched on a folder equal to it or starting with it followed by a space. <see langword="null"/> when none.</param>
/// <param name="CustomerName">The customer's own name. <see langword="null"/> (with no code either) files the project under <see cref="ProjectFolderService.NoCustomerFolderName"/>.</param>
public sealed record ProjectFolderRequest(string ProjectIdentifier, string ProjectName, string? CustomerCode = null, string? CustomerName = null);

/// <summary>What <see cref="ProjectFolderService.Ensure"/> did.</summary>
public enum ProjectFolderStatus
{
    /// <summary>The customer and project folders (and every standard subfolder) already existed; nothing was created.</summary>
    AlreadyExisted,

    /// <summary>At least one folder was created.</summary>
    Created,

    /// <summary>Folder generation is not available here (switched off, not Windows, or the root's drive does not exist); nothing was touched.</summary>
    Unavailable,

    /// <summary>The file system refused (permissions, a file in the way, an I/O error); see <see cref="ProjectFolderOutcome.Message"/>.</summary>
    Failed,
}

/// <summary>
/// The result of <see cref="ProjectFolderService.Ensure"/> — never an
/// exception: folder generation is a convenience beside the real domain
/// write, so a refusal here is reported, never thrown into the UI
/// (PO decision 2026-10-01).
/// </summary>
/// <param name="Status">What happened.</param>
/// <param name="Message">A one-line, user-facing account of what happened, suitable for the status bar.</param>
/// <param name="ProjectFolder">The project's own folder, when it exists afterwards; otherwise <see langword="null"/>.</param>
/// <param name="CreatedFolders">Every folder this call created, outermost first. Never <see langword="null"/>.</param>
public sealed record ProjectFolderOutcome(ProjectFolderStatus Status, string Message, string? ProjectFolder, IReadOnlyList<string> CreatedFolders)
{
    /// <summary>Whether the project folder exists afterwards.</summary>
    public bool IsAvailable => ProjectFolder is not null;
}
