using Tempest.Core.Configuration;
using Tempest.Core.Logging;

namespace Tempest.Core.Identity;

/// <summary>
/// The concrete <see cref="IIdentityService"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// A principal's <c>DisplayName</c> and role assignment are read from
/// configuration keys of the shape
/// <c>Identity:Principals:{IdentityId}:DisplayName</c> and
/// <c>Identity:Principals:{IdentityId}:Roles</c> (a comma-separated list
/// of role names, each resolved via <see cref="IRoleProvider"/>) — the
/// same config-sourced approach <see cref="RoleProvider"/> uses for role
/// definitions themselves.
/// </para>
/// <para>
/// Depends on the concrete <see cref="CurrentPrincipalAccessor"/> type,
/// not just <see cref="ICurrentPrincipalAccessor"/> — the only component
/// in this namespace with write access to it, by design. See that type's
/// own remarks.
/// </para>
/// <para>
/// <b>Frozen by ADR-0146 (<c>WP 17.2A</c>).</b>
/// <see cref="EstablishCurrentPrincipal"/> used to be gated on a
/// <c>plugin.identity.establish</c> permission read off an ambient component
/// principal, so that a loaded plugin could not hijack the process-wide
/// current principal. Nothing third-party ever loaded, so there is no
/// component principal to read and the gate is gone with the rest of the
/// trust platform, frozen at <c>src/Frozen/Tempest.Core.Plugins</c>.
/// <see cref="ICurrentPrincipalAccessor"/> and
/// <see cref="IPermissionEvaluator"/> are untouched.
/// </para>
/// </remarks>
public sealed class IdentityService : IIdentityService
{
    /// <summary>
    /// The configuration key prefix every principal definition is nested
    /// under.
    /// </summary>
    public const string PrincipalConfigurationPrefix = "Identity:Principals:";

    private readonly IConfigurationProvider _configuration;
    private readonly IRoleProvider _roleProvider;
    private readonly CurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="IdentityService"/> class.
    /// </summary>
    /// <param name="configuration">The configuration to read principal definitions from.</param>
    /// <param name="roleProvider">The role provider used to resolve a principal's configured roles.</param>
    /// <param name="currentPrincipalAccessor">The accessor this service establishes the current principal on.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">
    /// Any of <paramref name="configuration"/>, <paramref name="roleProvider"/>,
    /// or <paramref name="currentPrincipalAccessor"/> is <see langword="null"/>.
    /// </exception>
    public IdentityService(
        IConfigurationProvider configuration,
        IRoleProvider roleProvider,
        CurrentPrincipalAccessor currentPrincipalAccessor,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(roleProvider);
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);

        _configuration = configuration;
        _roleProvider = roleProvider;
        _currentPrincipalAccessor = currentPrincipalAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public IPrincipal GetPrincipal(string identityId)
    {
        if (string.IsNullOrWhiteSpace(identityId))
            throw new ArgumentException("Identity id must not be null, empty, or whitespace.", nameof(identityId));

        var displayNameKey = $"{PrincipalConfigurationPrefix}{identityId}:DisplayName";
        var displayName = _configuration.TryGetValue(displayNameKey, out var configuredDisplayName)
            ? configuredDisplayName!
            : identityId;

        var identity = new PlatformIdentity(identityId, displayName);

        var rolesKey = $"{PrincipalConfigurationPrefix}{identityId}:Roles";
        var permissions = new List<Permission>();

        if (_configuration.TryGetValue(rolesKey, out var configuredRoles))
        {
            var roleNames = configuredRoles!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var roleName in roleNames)
            {
                var role = _roleProvider.FindRole(roleName) ?? throw new RoleNotFoundException(roleName);

                foreach (var permission in role.Permissions)
                {
                    if (!permissions.Contains(permission))
                        permissions.Add(permission);
                }
            }
        }

        _logger?.Information($"Principal '{identityId}' resolved with {permissions.Count} permission(s).");

        return new PlatformPrincipal(identity, permissions);
    }

    /// <summary>
    /// Resolves <paramref name="identityId"/> and establishes the resulting
    /// principal as the current, ambient, process-wide principal
    /// (<see cref="ICurrentPrincipalAccessor"/>, ADR-0044).
    /// </summary>
    /// <inheritdoc />
    public IPrincipal EstablishCurrentPrincipal(string identityId)
    {
        var principal = GetPrincipal(identityId);

        _currentPrincipalAccessor.SetCurrent(principal);
        _logger?.Information($"Principal '{identityId}' established as current.");

        return principal;
    }
}
