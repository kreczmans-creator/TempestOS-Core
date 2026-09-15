using System.Reflection;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.Identity;

/// <summary>
/// `WP 21.6A`, OSA-12/OSA-14: proves the principal seam — a component
/// holding only <see cref="ICurrentPrincipalAccessor"/> cannot set or
/// clear the current principal, and <see cref="PrincipalSession"/> is the
/// one place that can, reachable only by already holding the exact
/// instance <c>TempestHost</c> constructed (never mintable around an
/// accessor obtained some other way).
/// </summary>
public class PrincipalSessionTests
{
    private static IPrincipal BuildPrincipal(string id) =>
        new PlatformPrincipal(new PlatformIdentity(id, id), []);

    private static PrincipalSession BuildSession(out CurrentPrincipalAccessor accessor)
    {
        accessor = new CurrentPrincipalAccessor();
        // Legal only because this test assembly carries [InternalsVisibleTo]
        // from Tempest.Core — the exact narrowing this file's own facts
        // below prove does not extend to any other assembly.
        return new PrincipalSession(accessor);
    }

    // ------------------------------------------------------------
    // The seam itself does what it says
    // ------------------------------------------------------------

    [Fact]
    public void Establish_SetsCurrentOnTheWrappedAccessor()
    {
        var session = BuildSession(out var accessor);
        var principal = BuildPrincipal("local.user");

        session.Establish(principal);

        Assert.Same(principal, accessor.Current);
    }

    [Fact]
    public void Establish_Null_ClearsAnAlreadyEstablishedPrincipal()
    {
        var session = BuildSession(out var accessor);
        session.Establish(BuildPrincipal("local.user"));

        session.Establish(null);

        Assert.Null(accessor.Current);
    }

    [Fact]
    public void Constructor_NullAccessor_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new PrincipalSession(null!));

    // ------------------------------------------------------------
    // OSA-12: a component holding only ICurrentPrincipalAccessor cannot
    // set or clear — proved at the compiled metadata level (the same
    // level that stops every other assembly's compiler, which is what
    // actually enforces this, not a runtime check). Deliberately
    // reflection-only for *inspection*, never for *bypassing* access —
    // no test here calls SetCurrent or constructs a PrincipalSession via
    // reflection; every reflection call below only reads what member
    // modifiers the compiled types carry.
    // ------------------------------------------------------------

    [Fact]
    public void ICurrentPrincipalAccessor_ExposesExactlyOneMember_TheReadOnlyCurrentProperty()
    {
        // The interface itself is the proof: a caller holding only this
        // type has literally nothing to call except the getter. This is a
        // compile-time fact for every consumer outside Tempest.Core, not
        // merely an unexercised capability — the interface declares no
        // SetCurrent overload for anything to resolve.
        var members = typeof(ICurrentPrincipalAccessor)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToList();

        Assert.Equal(["Current", "get_Current"], members.OrderBy(n => n, StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void CurrentPrincipalAccessor_SetCurrent_IsNotPublic()
    {
        // `WP 21.6A` closes OSA-12's demonstrated reach: before this, the
        // concrete type was both public-surfaced (SetCurrent) and
        // registered in the DI container under its own key, so any
        // component resolving it by concrete type — not only
        // WorkspaceHost — could call this method directly
        // (`Tempest.Samples.SamplePrincipalFactory` did, with an arbitrary
        // caller-supplied identity and no credential check). SetCurrent
        // itself still exists (PrincipalSession, in the same assembly,
        // calls it) — it is internal, not gone.
        var setCurrent = typeof(CurrentPrincipalAccessor).GetMethod(
            "SetCurrent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(setCurrent);
        Assert.False(setCurrent!.IsPublic, "CurrentPrincipalAccessor.SetCurrent must not be public (OSA-12).");
        Assert.True(setCurrent.IsAssembly, "CurrentPrincipalAccessor.SetCurrent must be internal, reachable only from within Tempest.Core.");
    }

    [Fact]
    public void PrincipalSession_Constructor_IsNotPublic()
    {
        // The single internal seam's own construction is as restricted as
        // the capability it carries — only TempestHost (Tempest.Core) can
        // mint one; nothing else can wrap an accessor it obtained some
        // other way (an `is CurrentPrincipalAccessor` cast against a
        // resolved ICurrentPrincipalAccessor, for instance) into a second,
        // unauthorised session.
        var constructors = typeof(PrincipalSession).GetConstructors(BindingFlags.Instance | BindingFlags.Public);

        Assert.Empty(constructors);

        var internalConstructor = typeof(PrincipalSession).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(CurrentPrincipalAccessor)], null);

        Assert.NotNull(internalConstructor);
        Assert.True(internalConstructor!.IsAssembly, "PrincipalSession's constructor must be internal, reachable only from within Tempest.Core.");
    }

    [Fact]
    public void PrincipalSession_Establish_IsPublic()
    {
        // The one capability the seam exists to expose, and the only
        // member on this type at all beyond its (internal) constructor —
        // confirming WP 21.6A's own design intent: a single-purpose type,
        // not a second, differently-named copy of the accessor's full
        // surface.
        var members = typeof(PrincipalSession)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToList();

        Assert.Equal(["Establish"], members);
    }
}
