using Tempest.Core.Identity;

namespace Tempest.Core.Tests.Identity;

/// <summary>
/// `WP 17.9.1`: stored identity ids are shown as names. The first Windows
/// review of `v0.17.0` saw a raw SID under "Last Revised By".
/// </summary>
public class PrincipalDirectoryTests
{
    private static CurrentPrincipalAccessor AccessorWith(string id, string displayName)
    {
        var accessor = new CurrentPrincipalAccessor();
        accessor.SetCurrent(new PlatformPrincipal(new PlatformIdentity(id, displayName), []));
        return accessor;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unknown")]
    [InlineData("Unknown")]
    public void Describe_AbsentOrUnknownId_SaysUnknown(string? id)
    {
        var directory = new PrincipalDirectory(new CurrentPrincipalAccessor());

        Assert.Equal(PrincipalDirectory.UnknownDisplayName, directory.Describe(id));
    }

    [Fact]
    public void Describe_TheSessionPrincipalsOwnId_ReturnsItsDisplayName()
    {
        var directory = new PrincipalDirectory(AccessorWith("S-1-5-21-1-2-3-1001", "Steven"));

        Assert.Equal("Steven", directory.Describe("S-1-5-21-1-2-3-1001"));
    }

    [Fact]
    public void Describe_AnIdTheOsCannotResolve_ReturnsTheIdVerbatim_NeverAnInventedName()
    {
        var directory = new PrincipalDirectory(AccessorWith("someone.else", "Someone Else"));

        // Not a SID, not the session principal: nothing can resolve it and
        // the directory must say so by echoing the id, not by guessing.
        Assert.Equal("ci-runner-42", directory.Describe("ci-runner-42"));
    }

    [Fact]
    public void Describe_AMalformedSid_ReturnsTheIdVerbatim()
    {
        var directory = new PrincipalDirectory(new CurrentPrincipalAccessor());

        // Looks like a SID, is not one. Translation throws on every
        // platform; the directory absorbs it rather than surfacing it to a
        // property grid.
        Assert.Equal("S-1-not-a-sid", directory.Describe("S-1-not-a-sid"));
    }

    [Fact]
    public void Describe_IsStableAcrossCalls()
    {
        var directory = new PrincipalDirectory(new CurrentPrincipalAccessor());

        var first = directory.Describe("S-1-5-21-9-9-9-9999");
        var second = directory.Describe("S-1-5-21-9-9-9-9999");

        Assert.Equal(first, second);
    }
}
