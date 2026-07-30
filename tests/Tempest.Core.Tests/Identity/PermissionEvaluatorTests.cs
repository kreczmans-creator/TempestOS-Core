using Tempest.Core.Identity;
using Tempest.Core.Logging;
using Tempest.Core.Tests.Events;

namespace Tempest.Core.Tests.Identity;

public class PermissionEvaluatorTests
{
    private static IPrincipal BuildPrincipal(params string[] permissionKeys) =>
        new PlatformPrincipal(
            new PlatformIdentity("local.user", "Local User"),
            permissionKeys.Select(k => new Permission(k)).ToList());

    // ----------------------------------------------------------------
    // HasPermission
    // ----------------------------------------------------------------

    [Fact]
    public void HasPermission_PrincipalHoldsPermission_ReturnsTrue()
    {
        var evaluator = new PermissionEvaluator();
        var principal = BuildPrincipal("reports.generate");

        Assert.True(evaluator.HasPermission(principal, new Permission("reports.generate")));
    }

    [Fact]
    public void HasPermission_PrincipalDoesNotHoldPermission_ReturnsFalse()
    {
        var evaluator = new PermissionEvaluator();
        var principal = BuildPrincipal("reports.generate");

        Assert.False(evaluator.HasPermission(principal, new Permission("settings.write")));
    }

    [Fact]
    public void HasPermission_PrincipalWithNoPermissions_ReturnsFalse()
    {
        var evaluator = new PermissionEvaluator();
        var principal = BuildPrincipal();

        Assert.False(evaluator.HasPermission(principal, new Permission("anything")));
    }

    // ----------------------------------------------------------------
    // RequirePermission
    // ----------------------------------------------------------------

    [Fact]
    public void RequirePermission_PrincipalHoldsPermission_DoesNotThrow()
    {
        var evaluator = new PermissionEvaluator();
        var principal = BuildPrincipal("reports.generate");

        var exception = Record.Exception(() => evaluator.RequirePermission(principal, new Permission("reports.generate")));

        Assert.Null(exception);
    }

    [Fact]
    public void RequirePermission_PrincipalDoesNotHoldPermission_ThrowsPermissionDeniedException()
    {
        var evaluator = new PermissionEvaluator();
        var principal = BuildPrincipal();
        var permission = new Permission("settings.write");

        var exception = Assert.Throws<PermissionDeniedException>(
            () => evaluator.RequirePermission(principal, permission));

        Assert.Same(principal, exception.Principal);
        Assert.Equal(permission, exception.RequiredPermission);
        Assert.Contains("local.user", exception.Message);
        Assert.Contains("settings.write", exception.Message);
    }

    [Fact]
    public void RequirePermission_Denied_LogsAtWarningWithoutLeakingUnrelatedDetail()
    {
        var logger = new RecordingLevelLogger();
        var evaluator = new PermissionEvaluator(logger);
        var principal = BuildPrincipal();

        Assert.Throws<PermissionDeniedException>(
            () => evaluator.RequirePermission(principal, new Permission("settings.write")));

        Assert.True(logger.HasEntryAt(LogLevel.Warning, "local.user"));
        Assert.True(logger.HasEntryAt(LogLevel.Warning, "settings.write"));
    }

    // ----------------------------------------------------------------
    // Failure injection: argument validation
    // ----------------------------------------------------------------

    [Fact]
    public void HasPermission_NullPrincipal_ThrowsArgumentNullException()
    {
        var evaluator = new PermissionEvaluator();

        Assert.Throws<ArgumentNullException>(() => evaluator.HasPermission(null!, new Permission("x")));
    }

    [Fact]
    public void HasPermission_NullPermission_ThrowsArgumentNullException()
    {
        var evaluator = new PermissionEvaluator();
        var principal = BuildPrincipal();

        Assert.Throws<ArgumentNullException>(() => evaluator.HasPermission(principal, null!));
    }

    [Fact]
    public void RequirePermission_NullPrincipal_ThrowsArgumentNullException()
    {
        var evaluator = new PermissionEvaluator();

        Assert.Throws<ArgumentNullException>(() => evaluator.RequirePermission(null!, new Permission("x")));
    }

    [Fact]
    public void RequirePermission_NullPermission_ThrowsArgumentNullException()
    {
        var evaluator = new PermissionEvaluator();
        var principal = BuildPrincipal();

        Assert.Throws<ArgumentNullException>(() => evaluator.RequirePermission(principal, null!));
    }

    [Fact]
    public void PermissionDeniedException_Constructor_NullPrincipal_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new PermissionDeniedException(null!, new Permission("x")));
    }

    [Fact]
    public void PermissionDeniedException_Constructor_NullPermission_ThrowsArgumentNullException()
    {
        var principal = BuildPrincipal();

        Assert.Throws<ArgumentNullException>(() => new PermissionDeniedException(principal, null!));
    }
}
