using Tempest.Core.Configuration;
using Tempest.Core.Identity;
using Tempest.Core.Plugins;

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
    // EstablishCurrentPrincipal: trust gate (WP 13.10B, TD-52). Mirrors
    // NavigationServiceTrustTests' own established pattern for testing this
    // exact kind of dynamic capability gate - a real CurrentComponentAccessor
    // and real PermissionEvaluator, a non-First-Party component principal
    // pushed via BeginScope, asserting PermissionDeniedException.
    // ----------------------------------------------------------------

    [Fact]
    public void EstablishCurrentPrincipal_ComponentWithoutIdentityEstablishCapability_ThrowsPermissionDeniedException()
    {
        var (service, componentAccessor, _) = CreateServiceWithTrustGate();
        var noCapability = CreatePrincipal("plugin.no-cap", PluginTrustPermission.UnsignedLocal);

        using (componentAccessor.BeginScope(noCapability))
        {
            Assert.Throws<PermissionDeniedException>(() => service.EstablishCurrentPrincipal("local.user"));
        }
    }

    [Fact]
    public void EstablishCurrentPrincipal_ComponentWithIdentityEstablishCapability_Succeeds()
    {
        var (service, componentAccessor, principalAccessor) = CreateServiceWithTrustGate();
        var withCapability = CreatePrincipal("plugin.a", PluginTrustPermission.UnsignedLocal, PluginCapability.IdentityEstablish);

        IPrincipal established;
        using (componentAccessor.BeginScope(withCapability))
        {
            established = service.EstablishCurrentPrincipal("local.user");
        }

        Assert.Same(established, principalAccessor.Current);
    }

    [Fact]
    public void EstablishCurrentPrincipal_FirstPartyTierComponentPrincipal_SkipsCapabilityCheck_Succeeds()
    {
        // A genuine FirstParty-tier plugin principal (the tier marker
        // permission, not merely a null/unwired component) holding no
        // explicit plugin.identity.establish grant - proves the IsFirstParty
        // skip-check itself, not merely the absence of a wired evaluator.
        var (service, componentAccessor, principalAccessor) = CreateServiceWithTrustGate();
        var firstParty = CreatePrincipal("plugin.firstparty", PluginTrustPermission.FirstParty);

        IPrincipal established;
        using (componentAccessor.BeginScope(firstParty))
        {
            established = service.EstablishCurrentPrincipal("local.user");
        }

        Assert.Same(established, principalAccessor.Current);
    }

    [Fact]
    public void EstablishCurrentPrincipal_NullCurrentComponentAccessor_SkipsCapabilityCheck_ReproducesTodaysBehaviour()
    {
        // No currentComponentAccessor/permissionEvaluator supplied - the
        // fully-backward-compatible path every pre-existing test in this
        // file already exercises (e.g. the test immediately above this
        // section). Explicit here via Record.Exception, mirroring
        // NavigationServiceTrustTests.Register_NullCurrentComponentAccessor_SkipsCapabilityCheck_ReproducesTodaysBehaviour.
        var configuration = BuildConfiguration();
        var accessor = new CurrentPrincipalAccessor();
        var service = new IdentityService(configuration, new RoleProvider(configuration), accessor);

        var exception = Record.Exception(() => service.EstablishCurrentPrincipal("local.user"));

        Assert.Null(exception);
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

    // ----------------------------------------------------------------
    // Helpers (mirrors NavigationServiceTrustTests' own CreateService/
    // CreatePrincipal helpers)
    // ----------------------------------------------------------------

    private static (IdentityService Service, CurrentComponentAccessor ComponentAccessor, CurrentPrincipalAccessor PrincipalAccessor) CreateServiceWithTrustGate()
    {
        var configuration = BuildConfiguration();
        var roleProvider = new RoleProvider(configuration);
        var principalAccessor = new CurrentPrincipalAccessor();
        var componentAccessor = new CurrentComponentAccessor();
        var evaluator = new PermissionEvaluator();
        var service = new IdentityService(
            configuration,
            roleProvider,
            principalAccessor,
            currentComponentAccessor: componentAccessor,
            permissionEvaluator: evaluator);
        return (service, componentAccessor, principalAccessor);
    }

    private static PlatformPrincipal CreatePrincipal(string id, string tierPermissionKey, params string[] additionalPermissionKeys)
    {
        var permissions = new List<Permission> { new(tierPermissionKey) };
        permissions.AddRange(additionalPermissionKeys.Select(key => new Permission(key)));
        return new PlatformPrincipal(new PlatformIdentity(id, id), permissions);
    }
}
