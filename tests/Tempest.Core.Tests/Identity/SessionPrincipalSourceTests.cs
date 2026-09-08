using Tempest.Core.Configuration;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.Identity;

/// <summary>
/// Proves <see cref="SessionPrincipalSource"/>'s own non-negotiable
/// identity id and configuration-overridable display name/role
/// (`WP 17.2A`, ADR-0146).
/// </summary>
public class SessionPrincipalSourceTests
{
    private static IConfigurationProvider ConfigurationWith(params (string Key, string Value)[] entries) =>
        new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource(entries.Select(e => new KeyValuePair<string, string>(e.Key, e.Value))))
            .Build();

    [Fact]
    public void Resolve_WithNoConfiguration_UsesTheOsIdentityIdAsDisplayNameToo_AndDefaultsToEngineer()
    {
        var source = new SessionPrincipalSource("ada", configuration: null);

        var principal = source.Resolve()!;

        Assert.Equal("ada", principal.IdentityId);
        Assert.Equal(SessionRole.Engineer, principal.Role);
        Assert.Equal("ada", principal.Identity.Id);
    }

    [Fact]
    public void Resolve_ConfigurationCannotChangeTheIdentityId_OnlyDisplayNameAndRole()
    {
        var configuration = ConfigurationWith(
            (SessionPrincipalSource.DisplayNameConfigurationKey, "Ada Lovelace"),
            (SessionPrincipalSource.RoleConfigurationKey, "Checker"),
            // Neither of these is ever consulted for the identity id -
            // there is no configuration key that could change it.
            ("Identity:IdentityId", "somebody-else"));

        var source = new SessionPrincipalSource("ada", configuration);

        var principal = source.Resolve()!;

        Assert.Equal("ada", principal.IdentityId);
        Assert.Equal("ada", principal.Identity.Id);
        Assert.Equal("Ada Lovelace", principal.DisplayName);
        Assert.Equal(SessionRole.Checker, principal.Role);
    }

    [Fact]
    public void Resolve_DisplayNameConfigured_OverridesTheDisplayName()
    {
        var configuration = ConfigurationWith((SessionPrincipalSource.DisplayNameConfigurationKey, "Ada Lovelace"));

        var principal = new SessionPrincipalSource("ada", configuration).Resolve()!;

        Assert.Equal("Ada Lovelace", principal.DisplayName);
        Assert.Equal("Ada Lovelace", principal.Identity.DisplayName);
        Assert.Equal("ada", principal.IdentityId);
    }

    [Fact]
    public void Resolve_RoleConfigured_OverridesTheRole()
    {
        var configuration = ConfigurationWith((SessionPrincipalSource.RoleConfigurationKey, "Checker"));

        var principal = new SessionPrincipalSource("ada", configuration).Resolve()!;

        Assert.Equal(SessionRole.Checker, principal.Role);
    }

    [Fact]
    public void Resolve_RoleConfigured_IsCaseInsensitive()
    {
        var configuration = ConfigurationWith((SessionPrincipalSource.RoleConfigurationKey, "checker"));

        var principal = new SessionPrincipalSource("ada", configuration).Resolve()!;

        Assert.Equal(SessionRole.Checker, principal.Role);
    }

    [Fact]
    public void Resolve_InvalidRoleConfigured_ThrowsConfigurationException()
    {
        var configuration = ConfigurationWith((SessionPrincipalSource.RoleConfigurationKey, "Administrator"));

        Assert.Throws<ConfigurationException>(() => new SessionPrincipalSource("ada", configuration));
    }

    [Fact]
    public void Resolve_NoRoleConfigured_DefaultsToEngineer()
    {
        var principal = new SessionPrincipalSource("ada", configuration: null).Resolve()!;

        Assert.Equal(SessionRole.Engineer, principal.Role);
    }

    [Fact]
    public void Resolve_PermissionsAreTheFixedLocalSessionSet()
    {
        var principal = new SessionPrincipalSource("ada", configuration: null).Resolve()!;

        Assert.Equal(ApplicationPermissions.LocalSession, principal.Permissions);
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenIdentityIdIsNullEmptyOrWhitespace()
    {
        Assert.ThrowsAny<ArgumentException>(() => new SessionPrincipalSource(null!, null));
        Assert.Throws<ArgumentException>(() => new SessionPrincipalSource("", null));
        Assert.Throws<ArgumentException>(() => new SessionPrincipalSource("   ", null));
    }

    [Fact]
    public void ParameterlessConstructor_ProducesAResolvablePrincipal_OnThisHost()
    {
        // The production, OS-derived form: whatever the account is, it
        // must always answer with a non-empty identity id, never throw.
        var principal = new SessionPrincipalSource().Resolve();

        Assert.NotNull(principal);
        Assert.False(string.IsNullOrWhiteSpace(principal!.IdentityId));
    }
}
