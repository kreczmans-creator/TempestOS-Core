using Tempest.Core.Commands;
using Tempest.Core.Evidence;
using Tempest.Workspace.Files;

namespace Tempest.Workspace.Evidence;

/// <summary>
/// Creates a new piece of Evidence and attaches every picked file's bytes
/// to it, in one user-facing operation (`WP 18.2A`). The Evidence
/// workspace's own real Create act: the file picker (or a drag/drop)
/// supplies the files, a prompt collects classification and an optional
/// subject, and this command does the rest — mirroring
/// <see cref="CreateEvidenceCommand"/>'s own identical shape, plain
/// <see cref="ICommand"/>, not <see cref="IWorkspaceCommand"/>, since there
/// is no pre-existing target to refresh.
/// </summary>
public sealed class CreateEvidenceFromFilesCommand : ICommand
{
    /// <summary>Initialises a new instance of the <see cref="CreateEvidenceFromFilesCommand"/> class.</summary>
    /// <param name="title">The new evidence's own display title — defaults to the first file's own name, without its extension, when the caller has nothing more specific to offer.</param>
    /// <param name="classification">What kind of engineering record this is.</param>
    /// <param name="files">Every file to attach, read and stored in the order given. May be empty — a record with no attachment yet is still a legitimate Draft.</param>
    /// <param name="parentId">Where the evidence goes — the open project, or a container the shell picked via <c>CreationPlacement</c>. <see langword="null"/> for a standalone, top-level record.</param>
    /// <param name="subjectId">The Part, Assembly, Requirement or Deliverable this evidence is about, by id. Optional.</param>
    public CreateEvidenceFromFilesCommand(
        string title, EvidenceClassification classification, IReadOnlyList<PickedFile> files, Guid? parentId = null, Guid? subjectId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(files);

        Title = title;
        Classification = classification;
        Files = files;
        ParentId = parentId;
        SubjectId = subjectId;
    }

    /// <summary>Gets the new evidence's own display title.</summary>
    public string Title { get; }

    /// <summary>Gets what kind of engineering record this is.</summary>
    public EvidenceClassification Classification { get; }

    /// <summary>Gets every file to attach.</summary>
    public IReadOnlyList<PickedFile> Files { get; }

    /// <summary>Gets where the new evidence goes. <see langword="null"/> for standalone.</summary>
    public Guid? ParentId { get; }

    /// <summary>Gets the Part, Assembly, Requirement or Deliverable this evidence is about, by id. Optional.</summary>
    public Guid? SubjectId { get; }
}

/// <summary>Handles <see cref="CreateEvidenceFromFilesCommand"/>.</summary>
public sealed class CreateEvidenceFromFilesCommandHandler : ICommandHandler<CreateEvidenceFromFilesCommand>
{
    private readonly IEvidenceService _service;

    /// <summary>Initialises a new instance of the <see cref="CreateEvidenceFromFilesCommandHandler"/> class.</summary>
    public CreateEvidenceFromFilesCommandHandler(IEvidenceService service)
    {
        ArgumentNullException.ThrowIfNull(service);

        _service = service;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Create is one transaction (`IEvidenceService.CreateAsync`); each
    /// attach is a second, own transaction with its own audit row
    /// (`AttachContentAsync`'s own established discipline, `TD-31`) —
    /// content is written before metadata inside each attach, and each
    /// attach is independently, durably recorded, exactly as attaching
    /// several files through the Object Editor one at a time already is.
    /// A file that fails to read or attach fails the whole command: a
    /// record that silently dropped one of the files the user picked
    /// would misreport what it actually holds.
    /// </remarks>
    public async Task<CommandResult> HandleAsync(CreateEvidenceFromFilesCommand command, CancellationToken cancellationToken)
    {
        Core.Evidence.Evidence created;

        try
        {
            created = await _service.CreateAsync(command.ParentId, command.Title, command.Classification, command.SubjectId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        foreach (var file in command.Files)
        {
            var content = await file.ReadAsync().ConfigureAwait(false);
            await created.AttachContentAsync(file.Name, file.ContentType, content, cancellationToken).ConfigureAwait(false);
        }

        var fileWord = command.Files.Count == 1 ? "file" : "files";
        return CommandResult.Success(
            $"Created Evidence '{created.DisplayName}' with {command.Files.Count} {fileWord}.",
            created.Id,
            Core.Evidence.Evidence.CanonicalKind);
    }
}
