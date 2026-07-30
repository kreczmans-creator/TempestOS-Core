using Tempest.Core.Configuration;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.Identity;

public class IdentityServiceTests
{
    private static IConfigurationProvider BuildConfiguration(params KeyValuePair<string, string>[] entries) =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(entries)).Build();

    // ----------------------------------------------------------------
    // GetPrincipal: known identity
    // ----------------------------------------------------------------

    [Fact]
    public void GetPrincipal_ConfiguredIdentityWithDisplayNameAndRole_ResolvesCorrectly()
    {
        var configuration = BuildConfiguration(
            new KeyValuePair<string, string>("Identity:Roles:Admin:Permissions", "settings.write,audit.query"),
            new KeyValuePair<string, string>("Identity:Principals:local.user:DisplayName", "Local Administrator"),
            new KeyValuePair<string, string>("Identity:Principals:local.user:Roles", "Admin"));
        var roleProvider = new RoleProvider(configuration);
        var service = new IdentityService(configuration, roleProvider, new CurrentPrincipalAccessor());

        var principal = service.GetPrincipal("local.user");

        Assert.Equal("local.user", principal.Identity.Id);
        Assert.Equal("Local Administrator", principal.Identity.DisplayName);
        Assert.Equal(2, principal.Permissions.Count);
        Assert.Contains(new Permission("settings.write"), principal.Permissions);
        Assert.Contains(new Permission("audit.query"), principal.Permissions);
    }

    [Fact]
    public void GetPrincipal_NoDisplayNameConfigured_DefaultsDisplayNameToIdentityId()
    {
        var configuration = BuildConfiguration();
        var service = new IdentityService(configuration, new RoleProvider(configuration), new CurrentPrincipalAccessor());

        var principal = service.GetPrincipal("local.user");

        Assert.Equal("local.user", principal.Identity.DisplayName);
    }

    [Fact]
    public void GetPrincipal_MultipleRoles_FlattensPermissionsAcrossAll()
    {
        var configuration = BuildConfiguration(
            new KeyValuePair<string, string>("Identity:Roles:Reader:Permissions", "reports.read"),
            new KeyValuePair<string, string>("Identity:Roles:Writer:Permissions", "reports.write"),
            new KeyValuePair<string, string>("Identity:Principals:local.user:Roles", "Reader,Writer"));
        var roleProvider = new RoleProvider(configuration);
        var service = new IdentityService(configuration, roleProvider, new CurrentPrincipalAccessor());

        var principal = service.GetPrincipal("local.user");

        Assert.Equal(2, principal.Permissions.Count);
        Assert.Contains(new Permission("reports.read"), principal.Permissions);
        Assert.Contains(new Permission("reports.write"), principal.Permissions);
    }

    [Fact]
    public void GetPrincipal_OverlappingRolesGrantingTheSamePermission_DoesNotDuplicateIt()
    {
        var configuration = BuildConfiguration(
            new KeyValuePair<string, string>("Identity:Roles:Reader:Permissions", "reports.read"),
            new KeyValuePair<string, string>("Identity:Roles:SuperReader:Permissions", "reports.read,reports.export"),
            new KeyValuePair<string, string>("Identity:Principals:local.user:Roles", "Reader,SuperReader"));
        var roleProvider = new RoleProvider(configuration);
        var service = new IdentityService(configuration, roleProvider, new CurrentPrincipalAccessor());

        var principal = service.GetPrincipal("local.user");

        Assert.Equal(2, principal.Permissions.Count);
    }

    // ----------------------------------------------------------------
    // GetPrincipal: unknown identity - fail-closed, not an error
    // ----------------------------------------------------------------

    [Fact]
    public void GetPrincipal_UnknownIdentity_ResolvesWithZeroPermissions()
    {
        var configuration = BuildConfiguration();
        var service = new IdentityService(configuration, new RoleProvider(configuration), new CurrentPrincipalAccessor());

        var principal = service.GetPrincipal("nobody.configured");

        Assert.Empty(principal.Permissions);
        Assert.Equal("nobody.configured", principal.Identity.Id);
    }

    // ----------------------------------------------------------------
    // Configuration validation: a principal referencing an undefined role
    // ----------------------------------------------------------------

    [Fact]
    public void GetPrincipal_PrincipalReferencesUndefinedRole_ThrowsRoleNotFoundException()
    {
        var configuration = BuildConfiguration(
            new KeyValuePair<string, string>("Identity:Principals:local.user:Roles", "NoSuchRole"));
        var roleProvider = new RoleProvider(configuration);
        var service = new IdentityService(configuration, roleProvider, new CurrentPrincipalAccessor());

        var exception = Assert.Throws<RoleNotFoundException>(() => service.GetPrincipal("local.user"));

        Assert.Equal("NoSuchRole", exception.RoleName);
    }

    [Fact]
    public void GetPrincipal_OneOfSeveralReferencedRolesIsUndefined_ThrowsRoleNotFoundException()
    {
        var configuration = BuildConfiguration(
            new KeyValuePair<string, string>("Identity:Roles:Reader:Permissions", "reports.read"),
            new KeyValuePair<string, string>("Identity:Principals:local.user:Roles", "Reader,Nonexistent"));
        var roleProvider = new RoleProvider(configuration);
        var service = new IdentityService(configuration, roleProvider, new CurrentPrincipalAccessor());

        Assert.Throws<RoleNotFoundException>(() => service.GetPrincipal("local.user"));
    }

    // ----------------------------------------------------------------
    // EstablishCurrentPrincipal
    // ----------------------------------------------------------------

    [Fact]
    public void EstablishCurrentPrincipal_SetsAccessorAndReturnsTheSamePrincipal()
    {
        var configuration = BuildConfiguration();
        var accessor = new CurrentPrincipalAccessor();
        var service = new IdentityService(configuration, new RoleProvider(configuration), accessor);

        var principal = service.EstablishCurrentPrincipal("local.user");

        Assert.Same(principal, accessor.Current);
    }

    // ----------------------------------------------------------------
    // Failure injection: argument validation
    // ----------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetPrincipal_NullEmptyOrWhitespaceIdentityId_ThrowsArgumentException(string? identityId)
    {
        var configuration = BuildConfiguration();
        var service = new IdentityService(configuration, new RoleProvider(configuration), new CurrentPrincipalAccessor());

        Assert.Throws<ArgumentException>(() => service.GetPrincipal(identityId!));
    }

    [Fact]
    public void Constructor_NullConfiguration_ThrowsArgumentNullException()
    {
        var configuration = BuildConfiguration();

        Assert.Throws<ArgumentNullException>(
            () => new IdentityService(null!, new RoleProvider(configuration), new CurrentPrincipalAccessor()));
    }

    [Fact]
    public void Constructor_NullRoleProvider_ThrowsArgumentNullException()
    {
        var configuration = BuildConfiguration();

        Assert.Throws<ArgumentNullException>(
            () => new IdentityService(configuration, null!, new CurrentPrincipalAccessor()));
    }

    [Fact]
    public void Constructor_NullCurrentPrincipalAccessor_ThrowsArgumentNullException()
    {
        var configuration = BuildConfiguration();

        Assert.Throws<ArgumentNullException>(
            () => new IdentityService(configuration, new RoleProvider(configuration), null!));
    }
}
