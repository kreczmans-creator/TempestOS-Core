using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Documents;

/// <summary>
/// Creates a new Document Domain object. Plain <see cref="ICommand"/>, not
/// <see cref="IWorkspaceCommand"/> — there is no pre-existing target
/// object/view to refresh, only a new one to be navigated to once created,
/// mirroring <see cref="Calculations.CreateCalculationObjectCommand"/>'s own
/// identical reasoning exactly.
/// </summary>
public sealed class CreateDocumentObjectCommand : ICommand
{
    public CreateDocumentObjectCommand(
        string kind, string displayName, string? identifier = null, Guid? parentId = null, string? initialContent = null,
        string? classification = null, string? drawingNumber = null, string? modelFormat = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Kind = kind;
        DisplayName = displayName;
        Identifier = identifier;
        ParentId = parentId;
        InitialContent = initialContent ?? $"{displayName} — created via the Documents module.";
        Classification = classification;
        DrawingNumber = drawingNumber;
        ModelFormat = modelFormat;
    }

    /// <summary>Gets the Kind to create — one of <see cref="DocumentObjectFactoryRegistry.SupportedKinds"/>.</summary>
    public string Kind { get; }

    /// <summary>Gets the new object's own display name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the new object's own business identifier (its own "document number"), or <see langword="null"/> to leave it unset.</summary>
    public string? Identifier { get; }

    /// <summary>Gets the new object's own parent, or <see langword="null"/> for a top-level object.</summary>
    public Guid? ParentId { get; }

    /// <summary>Gets the new object's own initial revision content.</summary>
    public string InitialContent { get; }

    /// <summary>Gets the new object's own document classification (`ADR-0088`) — only meaningful for <c>"Document"</c>.</summary>
    public string? Classification { get; }

    /// <summary>Gets the new object's own drawing number — only meaningful for <c>"Drawing"</c>.</summary>
    public string? DrawingNumber { get; }

    /// <summary>Gets the new object's own CAD model format — only meaningful for <c>"CadModel"</c>.</summary>
    public string? ModelFormat { get; }
}

/// <summary>Handles <see cref="CreateDocumentObjectCommand"/>.</summary>
public sealed class CreateDocumentObjectCommandHandler : ICommandHandler<CreateDocumentObjectCommand>
{
    private readonly DocumentObjectFactoryRegistry _registry;
    private readonly EngineeringDomainContext? _context;
    private readonly ICommandDispatcher? _dispatcher;

    /// <param name="registry">Creates the new object.</param>
    /// <param name="context">
    /// Used to dispatch this create's own compensation (`WP 21.1A`) —
    /// optional, mirroring <see cref="Mechanical.CreateMechanicalObjectCommandHandler"/>'s
    /// own already-optional <c>domainContext</c>.
    /// </param>
    /// <param name="dispatcher">Dispatches the compensation — optional; <see langword="null"/> (either this or <paramref name="context"/>) means no <see cref="CommandResult.Compensation"/> is attached.</param>
    public CreateDocumentObjectCommandHandler(DocumentObjectFactoryRegistry registry, EngineeringDomainContext? context = null, ICommandDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(registry);

        _registry = registry;
        _context = context;
        _dispatcher = dispatcher;
    }

    public async Task<CommandResult> HandleAsync(CreateDocumentObjectCommand command, CancellationToken cancellationToken)
    {
        IEngineeringObject created;

        var identifier = command.Identifier
            ?? await ProjectCentricIdentifierAsync(command.Kind, command.ParentId, cancellationToken).ConfigureAwait(false);

        try
        {
            created = await _registry.CreateAsync(
                command.Kind, identifier, command.DisplayName, command.InitialContent, command.ParentId,
                command.Classification, command.DrawingNumber, command.ModelFormat, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
        catch (DuplicateBusinessIdentifierException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        var displayName = (created as IHasBusinessIdentifier)?.DisplayName ?? created.Id.ToString();
        var compensation = WorkspaceCommandBindings.CreationCompensation(
            _context, _dispatcher, created.Id, command.Kind, $"Create '{displayName}'",
            buildDelete: () => new DeleteDocumentObjectCommand(created.Id, command.Kind),
            buildUndelete: () => new UndeleteDocumentObjectCommand(created.Id, command.Kind));

        return CommandResult.Success($"Created {command.Kind} '{displayName}'.", created.Id, command.Kind, compensation);
    }

    /// <summary>
    /// The document number a new Document, Drawing or CAD model created
    /// with no identifier of its own receives (Product Owner decision
    /// 2026-10-01 §3, `ADR-0156`): <c>CUSTOMER-PROJECTREF-DOC-NNN</c>
    /// (<c>DWG</c> for a drawing, <c>CAD</c> for a CAD model) when its
    /// parent resolves — through <see cref="Tempest.Workspace.Projects.ProjectMembership"/> —
    /// to a project whose own identifier is project-centric; one past the
    /// highest suffix any object of that Kind already uses under that
    /// prefix. <see langword="null"/> otherwise — standalone, an older
    /// <c>P-NNNN</c> project, no domain context composed, or a Kind outside
    /// the table — leaving the identifier unset exactly as before.
    /// </summary>
    private async Task<string?> ProjectCentricIdentifierAsync(string kind, Guid? parentId, CancellationToken cancellationToken)
    {
        if (_context is null || parentId is not { } parent)
            return null;

        var documentType = kind switch
        {
            DocumentObjectFactoryRegistry.Document => Tempest.Core.Projects.ProjectNumbering.Document,
            DocumentObjectFactoryRegistry.Drawing => Tempest.Core.Projects.ProjectNumbering.Drawing,
            DocumentObjectFactoryRegistry.CadModel => Tempest.Core.Projects.ProjectNumbering.CadModel,
            _ => null,
        };
        if (documentType is null)
            return null;

        var projectId = await Tempest.Workspace.Projects.ProjectMembership
            .ResolveOwningProjectAsync(_context.Repository, parent, cancellationToken).ConfigureAwait(false);
        if (projectId is not { } id
            || await _context.Repository.FindAsync(id, cancellationToken).ConfigureAwait(false) is not IProject project
            || !Tempest.Core.Projects.ProjectNumbering.TryGetDocumentPrefix(project.Identifier, documentType, out var prefix))
        {
            return null;
        }

        var entries = await _context.Repository.ListByKindAsync(kind, cancellationToken).ConfigureAwait(false);
        var existing = await _context.Repository.MaterialiseAsync<IEngineeringObject>(entries, cancellationToken).ConfigureAwait(false);

        return Tempest.Core.Projects.ProjectNumbering.NextNumber(
            prefix, existing.Select(o => (o as IHasBusinessIdentifier)?.Identifier));
    }
}
