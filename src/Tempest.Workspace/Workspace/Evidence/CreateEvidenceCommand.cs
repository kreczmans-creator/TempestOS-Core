using Tempest.Core.Commands;
using Tempest.Core.Evidence;

namespace Tempest.Workspace.Evidence;

/// <summary>
/// Creates a new, empty piece of Evidence. Plain <see cref="ICommand"/>,
/// not <see cref="IWorkspaceCommand"/> — there is no pre-existing target to
/// refresh, only a new object to be revealed and opened right up once
/// created, mirroring <c>Verification.CreateVerificationActivityCommand</c>'s
/// own identical reasoning.
/// </summary>
public sealed class CreateEvidenceCommand : ICommand
{
    /// <summary>Initialises a new instance of the <see cref="CreateEvidenceCommand"/> class.</summary>
    public CreateEvidenceCommand(string title, EvidenceClassification classification, Guid? parentId = null, Guid? subjectId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Title = title;
        Classification = classification;
        ParentId = parentId;
        SubjectId = subjectId;
    }

    /// <summary>Gets the new evidence's own display title.</summary>
    public string Title { get; }

    /// <summary>Gets what kind of engineering record this is.</summary>
    public EvidenceClassification Classification { get; }

    /// <summary>Gets where the new evidence goes — the open project, or a chosen container. <see langword="null"/> for standalone.</summary>
    public Guid? ParentId { get; }

    /// <summary>Gets the Part, Assembly, Requirement or Deliverable this evidence is about, by id. Optional.</summary>
    public Guid? SubjectId { get; }
}

/// <summary>Handles <see cref="CreateEvidenceCommand"/>.</summary>
public sealed class CreateEvidenceCommandHandler : ICommandHandler<CreateEvidenceCommand>
{
    private readonly IEvidenceService _service;

    /// <summary>Initialises a new instance of the <see cref="CreateEvidenceCommandHandler"/> class.</summary>
    public CreateEvidenceCommandHandler(IEvidenceService service)
    {
        ArgumentNullException.ThrowIfNull(service);

        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(CreateEvidenceCommand command, CancellationToken cancellationToken)
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

        // The shell reveals and opens whatever a create command names here
        // (Product Owner guard, `WP 17.9.4`).
        return CommandResult.Success($"Created Evidence '{created.DisplayName}'.", created.Id, Core.Evidence.Evidence.CanonicalKind);
    }
}
