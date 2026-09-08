using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Verification;

/// <summary>
/// Creates a new Verification Activity Domain object. Plain
/// <see cref="ICommand"/>, not <see cref="IWorkspaceCommand"/> — there is
/// no pre-existing target object/view to refresh, only a new one to be
/// navigated to once created, mirroring
/// <see cref="Calculations.CreateCalculationObjectCommand"/>'s own
/// identical reasoning exactly.
/// </summary>
public sealed class CreateVerificationActivityCommand : ICommand
{
    public CreateVerificationActivityCommand(
        string displayName, Guid subjectId, string method, Guid? parentId = null, string? initialContent = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);

        DisplayName = displayName;
        SubjectId = subjectId;
        Method = method;
        ParentId = parentId;
        InitialContent = initialContent ?? $"{displayName} — created via the Verification module.";
    }

    /// <summary>Gets the new activity's own display name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the engineering object this activity verifies.</summary>
    public Guid SubjectId { get; }

    /// <summary>Gets the verification method.</summary>
    public string Method { get; }

    /// <summary>Gets the new object's own parent, or <see langword="null"/> for a top-level object.</summary>
    public Guid? ParentId { get; }

    /// <summary>Gets the new object's own initial revision content.</summary>
    public string InitialContent { get; }
}

/// <summary>Handles <see cref="CreateVerificationActivityCommand"/>.</summary>
public sealed class CreateVerificationActivityCommandHandler : ICommandHandler<CreateVerificationActivityCommand>
{
    private readonly VerificationActivityFactoryRegistry _registry;

    public CreateVerificationActivityCommandHandler(VerificationActivityFactoryRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        _registry = registry;
    }

    public async Task<CommandResult> HandleAsync(CreateVerificationActivityCommand command, CancellationToken cancellationToken)
    {
        IEngineeringObject created;

        try
        {
            created = await _registry.CreateAsync(
                command.DisplayName, command.InitialContent, command.SubjectId, command.Method, command.ParentId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        return CommandResult.Success($"Created VerificationActivity '{created.Id}'.");
    }
}
