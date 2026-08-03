using Tempest.Core.Commands;
using Tempest.Core.Identity;
using Tempest.Core.Requirements;

namespace Tempest.Samples;

/// <summary>
/// Handles <see cref="GetSampleRequirementEvidenceCommand"/> by checking
/// Identity's own permission gate explicitly, then retrieving
/// <see cref="RequirementsSampleModule.SampleRequirementId"/>'s own
/// aggregated evidence through <see cref="IRequirementsService.GetEvidenceAsync"/>.
/// </summary>
/// <remarks>
/// <see cref="IRequirementsService"/> performs no internal permission
/// gating of its own (`ADR-0061`) — this handler is the enforcement
/// point, mirroring <see cref="ExportSampleDataCommandHandler"/>'s own
/// convention. Reports denial explicitly as an ordinary
/// <see cref="CommandResult.Failure(string)"/>, mirroring
/// <see cref="GetSampleVerificationHistoryCommandHandler"/>'s own
/// identical convention. With no <c>Identity:Roles:*:Permissions</c>
/// configuration supplied, <see cref="RequirementsSampleModule.ReadPermissionKey"/>
/// is not granted — denied by default.
/// </remarks>
public sealed class GetSampleRequirementEvidenceCommandHandler : ICommandHandler<GetSampleRequirementEvidenceCommand>
{
    private static readonly Permission ReadPermission = new(RequirementsSampleModule.ReadPermissionKey);

    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IPermissionEvaluator _permissionEvaluator;
    private readonly IRequirementsService _requirementsService;
    private readonly RequirementsSampleModule _module;

    /// <summary>Initialises a new instance of the <see cref="GetSampleRequirementEvidenceCommandHandler"/> class.</summary>
    public GetSampleRequirementEvidenceCommandHandler(
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IPermissionEvaluator permissionEvaluator,
        IRequirementsService requirementsService,
        RequirementsSampleModule module)
    {
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);
        ArgumentNullException.ThrowIfNull(requirementsService);
        ArgumentNullException.ThrowIfNull(module);

        _currentPrincipalAccessor = currentPrincipalAccessor;
        _permissionEvaluator = permissionEvaluator;
        _requirementsService = requirementsService;
        _module = module;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(GetSampleRequirementEvidenceCommand command, CancellationToken cancellationToken)
    {
        var principal = _currentPrincipalAccessor.Current
            ?? new PlatformPrincipal(new PlatformIdentity("unknown", "Unauthenticated"), []);

        if (!_permissionEvaluator.HasPermission(principal, ReadPermission))
            return CommandResult.Failure("Denied: current principal does not hold the requirements-read permission.");

        var evidence = await _requirementsService.GetEvidenceAsync(_module.SampleRequirementId!.Value, cancellationToken).ConfigureAwait(false);

        return CommandResult.Success(
            $"Found {evidence.VerificationHistory.Count} verification record(s) and {evidence.LinkedReferences.Count} linked reference(s).");
    }
}
