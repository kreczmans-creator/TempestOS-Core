using Tempest.Core.Configuration;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.Identity;

public class RoleProviderTests
{
    private static IConfigurationProvider BuildConfiguration(params KeyValuePair<string, string>[] entries) =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(entries)).Build();

    // ----------------------------------------------------------------
    // Parsing a single, well-formed role definition
    // ----------------------------------------------------------------

    [Fact]
    public void Constructor_SingleRoleDefinition_ParsesNameAndPermissions()
    {
        var configuration = BuildConfiguration(
            new KeyValuePair<string, string>("Identity:Roles:Admin:Permissions", "settings.write,audit.query"));

        var provider = new RoleProvider(configuration);

        var role = provider.FindRole("Admin");
        Assert.NotNull(role);
        Assert.Equal("Admin", role!.Name);
        Assert.Equal(2, role.Permissions.Count);
        Assert.Contains(new Permission("settings.write"), role.Permissions);
        Assert.Contains(new Permission("audit.query"), role.Permissions);
    }

    [Fact]
    public void Constructor_PermissionListWithWhitespace_TrimsEachEntry()
    {
        var configuration = BuildConfiguration(
            new KeyValuePair<string, string>("Identity:Roles:Admin:Permissions", " settings.write , audit.query "));

        var provider = new RoleProvider(configuration);

        var role = provider.FindRole("Admin");
        Assert.Equal(2, role!.Permissions.Count);
        Assert.Contains(new Permission("settings.write"), role.Permissions);
        Assert.Contains(new Permission("audit.query"), role.Permissions);
    }

    [Fact]
    public void Constructor_EmptyPermissionsValue_ProducesRoleWithZeroPermissions()
    {
        var configuration = BuildConfiguration(
            new KeyValuePair<string, string>("Identity:Roles:Guest:Permissions", ""));

        var provider = new RoleProvider(configuration);

        var role = provider.FindRole("Guest");
        Assert.NotNull(role);
        Assert.Empty(role!.Permissions);
    }

    // ----------------------------------------------------------------
    // Multiple roles
    // ----------------------------------------------------------------

    [Fact]
    public void Constructor_MultipleRoleDefinitions_ParsesEachIndependently()
    {
        var configuration = BuildConfiguration(
            new KeyValuePair<string, string>("Identity:Roles:Admin:Permissions", "settings.write"),
            new KeyValuePair<string, string>("Identity:Roles:Auditor:Permissions", "audit.query"));

        var provider = new RoleProvider(configuration);

        Assert.Equal(2, provider.Roles.Count);
        Assert.NotNull(provider.FindRole("Admin"));
        Assert.NotNull(provider.FindRole("Auditor"));
    }

    // ----------------------------------------------------------------
    // Configuration validation: keys outside the Identity:Roles:*:Permissions
    // shape are ignored, not misparsed
    // ----------------------------------------------------------------

    [Fact]
    public void Constructor_UnrelatedConfigurationKeys_AreIgnored()
    {
        var configuration = BuildConfiguration(
            new KeyValuePair<string, string>("Runtime:Logging:MinimumLevel", "Information"),
            new KeyValuePair<string, string>("Identity:Principals:sample.local-user:Roles", "Admin"),
            new KeyValuePair<string, string>("Identity:Roles:Admin:Permissions", "settings.write"));

        var provider = new RoleProvider(configuration);

        var role = Assert.Single(provider.Roles);
        Assert.Equal("Admin", role.Name);
    }

    [Fact]
    public void Constructor_KeyWithRolesPrefixButWrongSuffix_IsIgnored()
    {
        var configuration = BuildConfiguration(
            new KeyValuePair<string, string>("Identity:Roles:Admin:DisplayName", "Administrator"));

        var provider = new RoleProvider(configuration);

        Assert.Empty(provider.Roles);
    }

    [Fact]
    public void Constructor_NoConfiguration_ProducesNoRoles()
    {
        var configuration = BuildConfiguration();

        var provider = new RoleProvider(configuration);

        Assert.Empty(provider.Roles);
    }

    [Fact]
    public void Constructor_RoleNameIsCaseInsensitiveForLookup()
    {
        var configuration = BuildConfiguration(
            new KeyValuePair<string, string>("Identity:Roles:Admin:Permissions", "settings.write"));

        var provider = new RoleProvider(configuration);

        Assert.NotNull(provider.FindRole("admin"));
        Assert.NotNull(provider.FindRole("ADMIN"));
    }

    // ----------------------------------------------------------------
    // FindRole for an undefined role
    // ----------------------------------------------------------------

    [Fact]
    public void FindRole_UndefinedRole_ReturnsNull()
    {
        var provider = new RoleProvider(BuildConfiguration());

        Assert.Null(provider.FindRole("Nonexistent"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FindRole_NullEmptyOrWhitespaceName_ThrowsArgumentException(string? name)
    {
        var provider = new RoleProvider(BuildConfiguration());

        Assert.Throws<ArgumentException>(() => provider.FindRole(name!));
    }

    // ----------------------------------------------------------------
    // Failure injection
    // ----------------------------------------------------------------

    [Fact]
    public void Constructor_NullConfiguration_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RoleProvider(null!));
    }
}
