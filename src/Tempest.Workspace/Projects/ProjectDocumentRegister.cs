using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Projects;

/// <summary>One attachment held against a project document, as the register reports it.</summary>
/// <param name="Id">The attachment's own identity — the handle the `TD-80` viewer opens.</param>
/// <param name="FileName">Its file name.</param>
/// <param name="ContentType">Its declared content type.</param>
/// <param name="SizeInBytes">Its recorded size.</param>
public sealed record ProjectDocumentAttachment(Guid Id, string FileName, string ContentType, long SizeInBytes);

/// <summary>
/// One document or drawing belonging to a project, with the files held
/// against it.
/// </summary>
/// <param name="ObjectId">The engineering object itself.</param>
/// <param name="Kind">Its domain kind — Document, Drawing, CadModel, or whatever kind is carrying the file.</param>
/// <param name="Identifier">Its business identifier, where it has one.</param>
/// <param name="DisplayName">Its name, as the register shows it.</param>
/// <param name="Attachments">The files held against it, in a stable order.</param>
public sealed record ProjectDocumentEntry(
    Guid ObjectId,
    string Kind,
    string? Identifier,
    string DisplayName,
    IReadOnlyList<ProjectDocumentAttachment> Attachments)
{
    /// <summary>Whether this entry has any file behind it at all.</summary>
    /// <remarks>
    /// A document object with no attachment is still a document — it is
    /// listed, and says plainly that nothing is attached yet. That is a
    /// different statement from "this project has no documents", and both
    /// are things a user needs to be able to tell apart.
    /// </remarks>
    public bool HasFiles => Attachments.Count > 0;
}

/// <summary>The documents and drawings belonging to a project.</summary>
public interface IProjectDocumentRegister
{
    /// <summary>Every document, drawing and file-carrying object in <paramref name="projectId"/>.</summary>
    Task<IReadOnlyList<ProjectDocumentEntry>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The project's own document and drawing register — the read model behind
/// the Project Workspace's Documents area.
/// </summary>
/// <remarks>
/// <para>
/// <b>Membership is <see cref="ProjectMembership"/>'s answer, not a second
/// one.</b> The register lists what
/// <see cref="ProjectMembership.ListProjectMembersAsync"/> returns, so a
/// drawing attached to a Part inside a Sub-Assembly inside an Assembly is
/// in the project exactly as the platform already defines it —
/// transitively, over the durable <see cref="IHasParent"/> chain, never
/// direct children only.
/// </para>
/// <para>
/// <b>Two things qualify as a project document</b>, and the distinction is
/// deliberate: anything that <em>is</em> a document
/// (<see cref="IDocument"/> — Document, Drawing and CadModel all are), and
/// anything that <em>carries a file</em>, whatever its kind. A drawing
/// attached directly to a Part is a document of this project by any
/// definition a user would recognise, and a register that listed only
/// document-kind objects would hide it.
/// </para>
/// <para>
/// <b>A read model, not a store.</b> It composes the object repository and
/// each object's own attachment metadata; it holds no state, caches
/// nothing, and creates no persistence of its own. Attachment
/// <em>content</em> is `TD-31`'s, and is read only when a document is
/// actually opened — listing a hundred drawings must not load a hundred
/// files.
/// </para>
/// </remarks>
public sealed class ProjectDocumentRegister : IProjectDocumentRegister
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="ProjectDocumentRegister"/> class.</summary>
    public ProjectDocumentRegister(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectDocumentEntry>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var members = await ProjectMembership
            .ListProjectMembersAsync(_context.Repository, projectId, cancellationToken)
            .ConfigureAwait(false);

        var entries = new List<ProjectDocumentEntry>();

        foreach (var member in members)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var attachments = member is IHasAttachments attachable
                ? await attachable.GetAttachmentsAsync(cancellationToken).ConfigureAwait(false)
                : [];

            if (member is not IDocument && attachments.Count == 0)
                continue;

            entries.Add(new ProjectDocumentEntry(
                member.Id,
                member.Kind ?? string.Empty,
                (member as IHasBusinessIdentifier)?.Identifier,
                (member as IHasBusinessIdentifier)?.DisplayName ?? member.Kind ?? "Document",
                [.. attachments
                    .OrderBy(a => a.FileName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(a => a.Id)
                    .Select(a => new ProjectDocumentAttachment(a.Id, a.FileName, a.ContentType, a.SizeInBytes))]));
        }

        // A stable order the user can rely on across refreshes: the
        // repository's own enumeration order is explicitly not guaranteed
        // (`TD-27`), so a register that inherited it would reshuffle itself
        // for no reason the user could see.
        return
        [
            .. entries
                .OrderBy(e => e.Identifier ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.ObjectId)
        ];
    }
}
