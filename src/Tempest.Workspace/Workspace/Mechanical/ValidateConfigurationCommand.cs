using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Mechanical;

/// <summary>
/// Checks one <c>Baseline</c>/<c>Release</c> object's own member/revision
/// consistency — wires the already-existing
/// <see cref="IReferenceIntegrityChecker.CheckBaselineMembersAsync"/>
/// (`WP 8.2C`) into the Workspace for the first time; no new Domain
/// capability is introduced (`WP 9.0B`). Scoped to <see cref="IBaseline"/>
/// specifically, not the broader <see cref="IConfiguration"/> — a plain,
/// working <c>Configuration</c> does not itself satisfy <see cref="IBaseline"/>
/// (`Baseline : Configuration`, `WP8.2C`, a frozen shape this Work Package
/// does not reopen), so this command reports failure for one, exactly
/// as <see cref="IReferenceIntegrityChecker.CheckBaselineMembersAsync"/>'s
/// own signature already requires.
/// </summary>
public sealed class ValidateConfigurationCommand : IWorkspaceCommand
{
    public ValidateConfigurationCommand(Guid targetObjectId, string targetKind)
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

/// <summary>Handles <see cref="ValidateConfigurationCommand"/>.</summary>
public sealed class ValidateConfigurationCommandHandler : ICommandHandler<ValidateConfigurationCommand>
{
    private readonly EngineeringDomainContext _context;
    private readonly IReferenceIntegrityChecker _referenceIntegrityChecker;

    public ValidateConfigurationCommandHandler(EngineeringDomainContext context, IReferenceIntegrityChecker referenceIntegrityChecker)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(referenceIntegrityChecker);

        _context = context;
        _referenceIntegrityChecker = referenceIntegrityChecker;
    }

    public async Task<CommandResult> HandleAsync(ValidateConfigurationCommand command, CancellationToken cancellationToken)
    {
        var target = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (target is not IBaseline baseline)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or is not a Baseline/Release (a plain, working Configuration has no member-consistency check of its own).");

        var result = await _referenceIntegrityChecker.CheckBaselineMembersAsync(baseline, cancellationToken).ConfigureAwait(false);

        return result.IsValid
            ? CommandResult.Success($"'{command.TargetObjectId}' is consistent — all {baseline.MemberRevisions.Count} member(s) exist at the referenced revision.")
            : CommandResult.Failure($"'{command.TargetObjectId}' has {result.Errors.Count} inconsistenc(y/ies): {string.Join("; ", result.Errors.Select(e => e.Message))}");
    }
}
