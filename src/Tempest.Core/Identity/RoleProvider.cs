using Tempest.Core.Configuration;
using Tempest.Core.Logging;

namespace Tempest.Core.Identity;

/// <summary>
/// The concrete <see cref="IRoleProvider"/> implementation — reads role
/// definitions from <see cref="IConfigurationProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// A role is defined by a configuration key of the shape
/// <c>Identity:Roles:{RoleName}:Permissions</c>, whose value is a
/// comma-separated list of permission keys — for example,
/// <c>Identity:Roles:Admin:Permissions</c> = <c>"settings.write,audit.query"</c>.
/// This is the config-sourced grant mechanism <c>Platform Service
/// Contracts.md</c> named as a decision this Work Package's own
/// implementation phase must make; no administration UI or runtime-mutable
/// role store exists in this release (see <c>Future Extension Points</c> in
/// this Work Package's own retrospective).
/// </para>
/// <para>
/// Parsed once, eagerly, at construction — <see cref="IConfigurationProvider"/>
/// is itself immutable once built (Case Study 05), so there is no
/// possibility of the underlying configuration changing after this
/// provider reads it, and parsing once avoids re-parsing on every
/// <see cref="FindRole"/> call.
/// </para>
/// </remarks>
public sealed class RoleProvider : IRoleProvider
{
    /// <summary>
    /// The configuration key prefix every role definition is nested under.
    /// </summary>
    public const string RoleConfigurationPrefix = "Identity:Roles:";

    /// <summary>
    /// The configuration key suffix a role's permission list is stored
    /// under.
    /// </summary>
    public const string PermissionsConfigurationSuffix = ":Permissions";

    private readonly IReadOnlyDictionary<string, IRole> _rolesByName;

    /// <summary>
    /// Initialises a new instance of the <see cref="RoleProvider"/> class,
    /// parsing every role definition out of <paramref name="configuration"/>.
    /// </summary>
    /// <param name="configuration">The configuration to read role definitions from.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is <see langword="null"/>.</exception>
    public RoleProvider(IConfigurationProvider configuration, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var rolesByName = new Dictionary<string, IRole>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in configuration.GetAll())
        {
            if (!pair.Key.StartsWith(RoleConfigurationPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!pair.Key.EndsWith(PermissionsConfigurationSuffix, StringComparison.OrdinalIgnoreCase))
                continue;

            var roleName = pair.Key[RoleConfigurationPrefix.Length..^PermissionsConfigurationSuffix.Length];

            if (string.IsNullOrWhiteSpace(roleName))
                continue;

            var permissions = pair.Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(key => new Permission(key))
                .ToList();

            rolesByName[roleName] = new Role(roleName, permissions);
            logger?.Information($"Role '{roleName}' defined with {permissions.Count} permission(s).");
        }

        _rolesByName = rolesByName;
    }

    /// <inheritdoc />
    public IRole? FindRole(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Role name must not be null, empty, or whitespace.", nameof(name));

        return _rolesByName.TryGetValue(name, out var role) ? role : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<IRole> Roles => _rolesByName.Values.ToList();
}
