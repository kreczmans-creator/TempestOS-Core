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
/// <b>Trust-gated <see cref="EstablishCurrentPrincipal"/> (WP 13.10B,
/// TD-52).</b> Establishing the current principal reassigns the ambient,
/// process-wide, deliberately non-call-chain-scoped value
/// <see cref="ICurrentPrincipalAccessor"/> exposes (ADR-0044) — a plugin
/// able to call this method for an arbitrary <c>identityId</c> could
/// hijack that value for every later, unrelated caller. Mirroring
/// <see cref="Navigation.NavigationService.Register"/>'s own ADR-0111
/// gate: if the caller's own ambient component principal
/// (<see cref="ICurrentComponentAccessor.Current"/>) is non-null and not
/// First-Party (<see cref="Plugins.PluginTrustPermission.IsFirstParty"/>),
/// it must hold <c>plugin.identity.establish</c>
/// (<see cref="IPermissionEvaluator.RequirePermission"/> — throws
/// <see cref="PermissionDeniedException"/> if not). The check is
/// <i>skipped</i>, not merely satisfied, when the ambient component
/// principal is <see langword="null"/> or First-Party, so every
/// first-party caller (e.g. a genuine login/session-establishment flow)
/// observes zero behavioural change.
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
    private readonly ICurrentComponentAccessor? _currentComponentAccessor;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    /// <summary>
    /// Initialises a new instance of the <see cref="IdentityService"/> class.
    /// </summary>
    /// <param name="configuration">The configuration to read principal definitions from.</param>
    /// <param name="roleProvider">The role provider used to resolve a principal's configured roles.</param>
    /// <param name="currentPrincipalAccessor">The accessor this service establishes the current principal on.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <param name="currentComponentAccessor">
    /// An optional accessor resolving which loaded component's own code is
    /// currently calling <see cref="EstablishCurrentPrincipal"/> (WP 13.10B,
    /// TD-52). <see langword="null"/> — the default — reproduces today's
    /// exact unconditional behaviour: every check that reads it treats a
    /// <see langword="null"/> accessor identically to a <see langword="null"/>
    /// current component (first-party).
    /// </param>
    /// <param name="permissionEvaluator">
    /// An optional evaluator used to enforce <c>plugin.identity.establish</c>
    /// (WP 13.10B, TD-52). <see langword="null"/> — the default — no-ops
    /// the check this collaborator would otherwise perform.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Any of <paramref name="configuration"/>, <paramref name="roleProvider"/>,
    /// or <paramref name="currentPrincipalAccessor"/> is <see langword="null"/>.
    /// </exception>
    public IdentityService(
        IConfigurationProvider configuration,
        IRoleProvider roleProvider,
        CurrentPrincipalAccessor currentPrincipalAccessor,
        ILogger? logger = null,
        ICurrentComponentAccessor? currentComponentAccessor = null,
        IPermissionEvaluator? permissionEvaluator = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(roleProvider);
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);

        _configuration = configuration;
        _roleProvider = roleProvider;
        _currentPrincipalAccessor = currentPrincipalAccessor;
        _logger = logger;
        _currentComponentAccessor = currentComponentAccessor;
        _permissionEvaluator = permissionEvaluator;
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
    /// <remarks>
    /// <b>Trust-gated (WP 13.10B, TD-52).</b> If the caller's own ambient
    /// component principal (<see cref="ICurrentComponentAccessor.Current"/>)
    /// is non-null and not First-Party
    /// (<see cref="Plugins.PluginTrustPermission.IsFirstParty"/>), it must
    /// hold <c>plugin.identity.establish</c>
    /// (<see cref="IPermissionEvaluator.RequirePermission"/> — throws
    /// <see cref="PermissionDeniedException"/> if not, propagating
    /// uncaught). The check is skipped entirely — not merely satisfied —
    /// when the ambient component principal is <see langword="null"/> or
    /// First-Party, so every existing first-party caller (e.g. a genuine
    /// login/session-establishment flow) observes zero behavioural change;
    /// only a call originating from a plugin's own component scope is ever
    /// checked. See this type's own remarks.
    /// </remarks>
    /// <inheritdoc />
    public IPrincipal EstablishCurrentPrincipal(string identityId)
    {
        var registrant = _currentComponentAccessor?.Current;

        if (!Plugins.PluginTrustPermission.IsFirstParty(registrant))
            _permissionEvaluator?.RequirePermission(registrant!, new Permission(Plugins.PluginCapability.IdentityEstablish));

        var principal = GetPrincipal(identityId);

        _currentPrincipalAccessor.SetCurrent(principal);
        _logger?.Information($"Principal '{identityId}' established as current.");

        return principal;
    }
}
