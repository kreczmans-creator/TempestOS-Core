using Tempest.Core.Commands;
using Tempest.Core.Identity;

namespace Tempest.Samples;

/// <summary>
/// Handles <see cref="CheckSamplePermissionCommand"/> by reading
/// <see cref="ICurrentPrincipalAccessor"/> and <see cref="IPermissionEvaluator"/>
/// and reporting whether the current principal holds
/// <see cref="IdentitySampleModule.SamplePermissionKey"/>.
/// </summary>
/// <remarks>
/// Depends on <see cref="ICurrentPrincipalAccessor"/> and
/// <see cref="IPermissionEvaluator"/> directly, as ordinary, explicit peer
/// dependencies of this command's own application logic — the Command
/// Framework itself never depends on Identity &amp; Permissions, and
/// Identity &amp; Permissions never depends on the Command Framework.
/// Uses the non-throwing <see cref="IPermissionEvaluator.HasPermission"/>
/// so a denied check is reported as an ordinary
/// <see cref="CommandResult.Failure(string)"/>, not an unhandled
/// <see cref="PermissionDeniedException"/> — a command handler's own
/// choice, not a Command Framework or Identity requirement.
/// </remarks>
public sealed class CheckSamplePermissionCommandHandler : ICommandHandler<CheckSamplePermissionCommand>
{
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IPermissionEvaluator _permissionEvaluator;

    /// <summary>
    /// Initialises a new instance of the <see cref="CheckSamplePermissionCommandHandler"/> class.
    /// </summary>
    /// <param name="currentPrincipalAccessor">The service this handler reads the current principal from.</param>
    /// <param name="permissionEvaluator">The service this handler checks the permission against.</param>
    public CheckSamplePermissionCommandHandler(
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IPermissionEvaluator permissionEvaluator)
    {
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);

        _currentPrincipalAccessor = currentPrincipalAccessor;
        _permissionEvaluator = permissionEvaluator;
    }

    /// <inheritdoc />
    public Task<CommandResult> HandleAsync(CheckSamplePermissionCommand command, CancellationToken cancellationToken)
    {
        var principal = _currentPrincipalAccessor.Current;

        if (principal is null)
            return Task.FromResult(CommandResult.Failure("No current principal is established."));

        var permission = new Permission(IdentitySampleModule.SamplePermissionKey);

        return Task.FromResult(_permissionEvaluator.HasPermission(principal, permission)
            ? CommandResult.Success($"Principal '{principal.Identity.Id}' holds '{permission.Key}'.")
            : CommandResult.Failure($"Principal '{principal.Identity.Id}' does not hold '{permission.Key}'."));
    }
}
