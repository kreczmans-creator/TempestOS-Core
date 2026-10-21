using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Disciplines;

/// <summary>Creates a <see cref="DisciplineObject"/> of one of <see cref="DisciplineCatalogue"/>'s Kinds.</summary>
public sealed class CreateDisciplineObjectCommand : ICommand
{
    /// <summary>Initialises a new instance of the <see cref="CreateDisciplineObjectCommand"/> class.</summary>
    public CreateDisciplineObjectCommand(string kind, string displayName, Guid? parentId = null, string? identifier = null, string? initialContent = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Kind = kind;
        DisplayName = displayName;
        ParentId = parentId;
        Identifier = identifier;
        InitialContent = initialContent ?? $"{displayName} — created in TempestOS.";
    }

    /// <summary>The Kind to create.</summary>
    public string Kind { get; }

    /// <summary>The new object's name.</summary>
    public string DisplayName { get; }

    /// <summary>The project or discipline object to create it under, if any.</summary>
    public Guid? ParentId { get; }

    /// <summary>An optional engineering identifier.</summary>
    public string? Identifier { get; }

    /// <summary>The first revision's content.</summary>
    public string InitialContent { get; }
}

/// <summary>Handles <see cref="CreateDisciplineObjectCommand"/>.</summary>
public sealed class CreateDisciplineObjectCommandHandler : ICommandHandler<CreateDisciplineObjectCommand>
{
    private readonly EngineeringDomainContext _context;
    private readonly ICommandDispatcher? _dispatcher;

    /// <summary>Initialises a new instance of the <see cref="CreateDisciplineObjectCommandHandler"/> class.</summary>
    public CreateDisciplineObjectCommandHandler(EngineeringDomainContext context, ICommandDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _dispatcher = dispatcher;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(CreateDisciplineObjectCommand command, CancellationToken cancellationToken)
    {
        var definition = DisciplineCatalogue.ForKind(command.Kind);
        if (definition is null)
            return CommandResult.Failure($"'{command.Kind}' is not a supported discipline Kind — expected one of: {string.Join(", ", DisciplineCatalogue.AllKinds)}.");

        IEngineeringObject created;
        try
        {
            var projectScopeId = BusinessIdentifierScope.ResolveProjectId(command.ParentId, _context.Repository);
            var factory = new EngineeringObjectFactory<DisciplineObject>(
                command.Kind, _context,
                (doc, rev) => new DisciplineObject(doc, rev, _context, command.Identifier, command.DisplayName, EngineeringObjectMetadata.Empty));

            created = await factory.CreateAsync(command.InitialContent, projectScopeId, cancellationToken).ConfigureAwait(false);

            if (command.ParentId is { } parentId && created is IHasParent hasParent)
                await hasParent.MoveAsync(parentId, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
        catch (DuplicateBusinessIdentifierException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        var label = definition.Find(command.Kind)!.Label;
        var compensation = WorkspaceCommandBindings.CreationCompensation(
            _context, _dispatcher, created.Id, command.Kind, $"Create '{command.DisplayName}'",
            buildDelete: () => new DeleteDisciplineObjectCommand(created.Id, command.Kind),
            buildUndelete: () => new UndeleteDisciplineObjectCommand(created.Id, command.Kind));

        return CommandResult.Success($"Created {label.ToLowerInvariant()} '{command.DisplayName}'.", created.Id, command.Kind, compensation);
    }
}

/// <summary>Renames a discipline object.</summary>
public sealed class RenameDisciplineObjectCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="RenameDisciplineObjectCommand"/> class.</summary>
    public RenameDisciplineObjectCommand(Guid targetObjectId, string targetKind, string newDisplayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(newDisplayName);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        NewDisplayName = newDisplayName;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>The new name.</summary>
    public string NewDisplayName { get; }
}

/// <summary>Handles <see cref="RenameDisciplineObjectCommand"/>.</summary>
public sealed class RenameDisciplineObjectCommandHandler(EngineeringDomainContext context) : ICommandHandler<RenameDisciplineObjectCommand>
{
    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(RenameDisciplineObjectCommand command, CancellationToken cancellationToken)
    {
        var target = await context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);
        if (target is not IRenamable renamable)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or its own Kind cannot be renamed.");

        try
        {
            await renamable.RenameAsync(command.NewDisplayName, cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateBusinessIdentifierException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
        catch (SupersededEngineeringObjectException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        return CommandResult.Success($"Renamed to '{command.NewDisplayName}'.");
    }
}

/// <summary>Records a new content revision of a discipline object.</summary>
public sealed class ReviseDisciplineObjectCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="ReviseDisciplineObjectCommand"/> class.</summary>
    public ReviseDisciplineObjectCommand(Guid targetObjectId, string targetKind, string newContent, string? changeSummary = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentNullException.ThrowIfNull(newContent);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        NewContent = newContent;
        ChangeSummary = changeSummary;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>The new revision's content.</summary>
    public string NewContent { get; }

    /// <summary>An optional summary of the change.</summary>
    public string? ChangeSummary { get; }
}

/// <summary>Handles <see cref="ReviseDisciplineObjectCommand"/>.</summary>
public sealed class ReviseDisciplineObjectCommandHandler(EngineeringDomainContext context) : ICommandHandler<ReviseDisciplineObjectCommand>
{
    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(ReviseDisciplineObjectCommand command, CancellationToken cancellationToken)
    {
        var target = await context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);
        if (target is not IHasRevisions revisable)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or its own Kind cannot be revised.");

        var revised = await revisable.ReviseAsync(command.NewContent, command.ChangeSummary, cancellationToken).ConfigureAwait(false);
        return CommandResult.Success($"Revised '{command.TargetObjectId}' to revision {(revised as IEngineeringObject)?.CurrentRevisionNumber}.");
    }
}

/// <summary>Soft-deletes a discipline object.</summary>
public sealed class DeleteDisciplineObjectCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="DeleteDisciplineObjectCommand"/> class.</summary>
    public DeleteDisciplineObjectCommand(Guid targetObjectId, string targetKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }
}

/// <summary>Handles <see cref="DeleteDisciplineObjectCommand"/>.</summary>
public sealed class DeleteDisciplineObjectCommandHandler(EngineeringDomainContext context, ICommandDispatcher? dispatcher = null) : ICommandHandler<DeleteDisciplineObjectCommand>
{
    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(DeleteDisciplineObjectCommand command, CancellationToken cancellationToken)
    {
        var target = await context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);
        if (target is not IDeletable deletable)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or its own Kind cannot be deleted.");

        var sourceName = (target as IHasBusinessIdentifier)?.DisplayName ?? command.TargetObjectId.ToString();
        try
        {
            await deletable.DeleteAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (EngineeringObjectHasChildrenException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        var compensation = WorkspaceCommandBindings.DeleteCompensation(
            context, dispatcher, command.TargetObjectId, command.TargetKind, sourceName,
            buildDelete: () => new DeleteDisciplineObjectCommand(command.TargetObjectId, command.TargetKind),
            buildUndelete: () => new UndeleteDisciplineObjectCommand(command.TargetObjectId, command.TargetKind));

        return CommandResult.Success($"Deleted '{sourceName}'.", compensation: compensation);
    }
}

/// <summary>Restores a soft-deleted discipline object — Delete's own undo.</summary>
public sealed class UndeleteDisciplineObjectCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="UndeleteDisciplineObjectCommand"/> class.</summary>
    public UndeleteDisciplineObjectCommand(Guid targetObjectId, string targetKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }
}

/// <summary>Handles <see cref="UndeleteDisciplineObjectCommand"/>.</summary>
public sealed class UndeleteDisciplineObjectCommandHandler(EngineeringDomainContext context) : ICommandHandler<UndeleteDisciplineObjectCommand>
{
    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(UndeleteDisciplineObjectCommand command, CancellationToken cancellationToken)
    {
        var target = await context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);
        if (target is not IDeletable deletable)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or its own Kind cannot be restored.");

        try
        {
            await deletable.UndeleteAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (EngineeringObjectNotDeletedException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
        catch (EngineeringObjectParentDeletedException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        var sourceName = (target as IHasBusinessIdentifier)?.DisplayName ?? command.TargetObjectId.ToString();
        return CommandResult.Success($"Restored '{sourceName}'.", command.TargetObjectId, command.TargetKind);
    }
}

/// <summary>Reparents a discipline object.</summary>
public sealed class MoveDisciplineObjectCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="MoveDisciplineObjectCommand"/> class.</summary>
    public MoveDisciplineObjectCommand(Guid targetObjectId, string targetKind, Guid? newParentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        NewParentId = newParentId;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>The new parent, or <see langword="null"/> for top level.</summary>
    public Guid? NewParentId { get; }
}

/// <summary>Handles <see cref="MoveDisciplineObjectCommand"/>.</summary>
public sealed class MoveDisciplineObjectCommandHandler(EngineeringDomainContext context, ICommandDispatcher? dispatcher = null) : ICommandHandler<MoveDisciplineObjectCommand>
{
    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(MoveDisciplineObjectCommand command, CancellationToken cancellationToken)
    {
        var target = await context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);
        if (target is not IHasParent hasParent)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or its own Kind cannot be moved.");

        return await WorkspaceCommandBindings.MoveResultAsync(
            context, dispatcher, hasParent, command.TargetObjectId, command.TargetKind, command.NewParentId,
            parentId => new MoveDisciplineObjectCommand(command.TargetObjectId, command.TargetKind, parentId),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Transitions a discipline object's lifecycle status.</summary>
public sealed class SetDisciplineObjectStatusCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="SetDisciplineObjectStatusCommand"/> class.</summary>
    public SetDisciplineObjectStatusCommand(Guid targetObjectId, string targetKind, LifecycleState status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        Status = status;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>The status to transition to.</summary>
    public LifecycleState Status { get; }
}

/// <summary>Handles <see cref="SetDisciplineObjectStatusCommand"/>.</summary>
public sealed class SetDisciplineObjectStatusCommandHandler(EngineeringDomainContext context, ICommandDispatcher? dispatcher = null) : ICommandHandler<SetDisciplineObjectStatusCommand>
{
    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(SetDisciplineObjectStatusCommand command, CancellationToken cancellationToken)
    {
        var target = await context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);
        if (target is not IHasLifecycle lifecycle)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or its own Kind has no lifecycle status.");

        var previousStatus = lifecycle.Status;
        var sourceName = (target as IHasBusinessIdentifier)?.DisplayName ?? command.TargetObjectId.ToString();
        try
        {
            await lifecycle.TransitionAsync(command.Status, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidLifecycleTransitionException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        var compensation = WorkspaceCommandBindings.StatusCompensation(
            context, dispatcher, command.TargetObjectId, command.TargetKind, sourceName, previousStatus, command.Status,
            status => new SetDisciplineObjectStatusCommand(command.TargetObjectId, command.TargetKind, status));

        return CommandResult.Success(
            $"Status set to '{command.Status}' for '{sourceName}'.", compensation: compensation,
            undoUnavailableReason: compensation is null ? WorkspaceCommandBindings.UndoUnavailableForStatus(previousStatus, command.Status) : null);
    }
}

/// <summary>Records (or clears) one attribute of a discipline object.</summary>
public sealed class SetDisciplineAttributeCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="SetDisciplineAttributeCommand"/> class.</summary>
    public SetDisciplineAttributeCommand(Guid targetObjectId, string targetKind, string attributeName, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        AttributeName = attributeName;
        Value = value;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>The attribute to record.</summary>
    public string AttributeName { get; }

    /// <summary>The value, or blank to clear it.</summary>
    public string? Value { get; }
}

/// <summary>Handles <see cref="SetDisciplineAttributeCommand"/>; undo restores the previous value.</summary>
public sealed class SetDisciplineAttributeCommandHandler(EngineeringDomainContext context) : ICommandHandler<SetDisciplineAttributeCommand>
{
    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(SetDisciplineAttributeCommand command, CancellationToken cancellationToken)
    {
        var target = await context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);
        if (target is not IDisciplineObject disciplineObject)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or is not a discipline object.");

        var known = DisciplineCatalogue.ForKind(disciplineObject.Kind)?.Find(disciplineObject.Kind)?.Attributes ?? [];
        if (!known.Contains(command.AttributeName, StringComparer.Ordinal))
            return CommandResult.Failure($"A {disciplineObject.Kind} has no '{command.AttributeName}' — expected one of: {string.Join(", ", known)}.");

        await disciplineObject.SetAttributeAsync(command.AttributeName, command.Value, cancellationToken).ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(command.Value)
            ? CommandResult.Success($"Cleared {command.AttributeName} on '{disciplineObject.DisplayName}'.")
            : CommandResult.Success($"{command.AttributeName} set to '{command.Value.Trim()}' on '{disciplineObject.DisplayName}'.");
    }
}

/// <summary>Records several attributes of one discipline object at once — the per-kind <c>Set … attributes</c> command.</summary>
public sealed class SetDisciplineAttributesCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="SetDisciplineAttributesCommand"/> class.</summary>
    public SetDisciplineAttributesCommand(Guid targetObjectId, string targetKind, IReadOnlyDictionary<string, string> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentNullException.ThrowIfNull(values);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        Values = values;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>The attributes to record, by name; an attribute not named here is left as it is.</summary>
    public IReadOnlyDictionary<string, string> Values { get; }
}

/// <summary>Handles <see cref="SetDisciplineAttributesCommand"/>.</summary>
public sealed class SetDisciplineAttributesCommandHandler(EngineeringDomainContext context) : ICommandHandler<SetDisciplineAttributesCommand>
{
    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(SetDisciplineAttributesCommand command, CancellationToken cancellationToken)
    {
        var target = await context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);
        if (target is not IDisciplineObject disciplineObject)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or is not a discipline object.");

        var known = DisciplineCatalogue.ForKind(disciplineObject.Kind)?.Find(disciplineObject.Kind)?.Attributes ?? [];
        var unknown = command.Values.Keys.Where(name => !known.Contains(name, StringComparer.Ordinal)).ToList();
        if (unknown.Count > 0)
            return CommandResult.Failure($"A {disciplineObject.Kind} has no '{string.Join("', '", unknown)}' — expected: {string.Join(", ", known)}.");

        if (command.Values.Count == 0)
            return CommandResult.Success($"Nothing changed on '{disciplineObject.DisplayName}' — every value was left blank.");

        foreach (var name in known.Where(command.Values.ContainsKey))
            await disciplineObject.SetAttributeAsync(name, command.Values[name].Trim(), cancellationToken).ConfigureAwait(false);

        return CommandResult.Success($"Recorded {command.Values.Count} attribute(s) on '{disciplineObject.DisplayName}': {string.Join(", ", command.Values.Keys)}.");
    }
}
