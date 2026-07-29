using Tempest.Core.Identity;

namespace Tempest.Core.Tests.Identity;

public class ExceptionTests
{
    // ----------------------------------------------------------------
    // IdentityException base
    // ----------------------------------------------------------------

    [Fact]
    public void IdentityException_MessageOnlyConstructor_SetsMessage()
    {
        var exception = new IdentityException("something went wrong");

        Assert.Equal("something went wrong", exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void IdentityException_MessageAndInnerExceptionConstructor_SetsBoth()
    {
        var inner = new InvalidOperationException("inner");

        var exception = new IdentityException("outer", inner);

        Assert.Equal("outer", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void PermissionDeniedException_IsIdentityException()
    {
        var principal = new PlatformPrincipal(new PlatformIdentity("id", "Display"), []);

        var exception = new PermissionDeniedException(principal, new Permission("x"));

        Assert.IsAssignableFrom<IdentityException>(exception);
    }

    [Fact]
    public void RoleNotFoundException_IsIdentityException()
    {
        var exception = new RoleNotFoundException("Admin");

        Assert.IsAssignableFrom<IdentityException>(exception);
        Assert.Equal("Admin", exception.RoleName);
        Assert.Contains("Admin", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RoleNotFoundException_Constructor_NullEmptyOrWhitespaceRoleName_ThrowsArgumentException(string? roleName)
    {
        Assert.Throws<ArgumentException>(() => new RoleNotFoundException(roleName!));
    }
}
