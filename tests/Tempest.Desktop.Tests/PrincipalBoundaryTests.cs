using Tempest.App.Projects;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Verification;
using Avalonia.Headless.XUnit;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `TD-103`. The running product knows who is using it, it learns that
/// through one boundary, and nothing about it is an authentication system.
/// </summary>
/// <remarks>
/// <para>
/// Before this, only sample modules ever called
/// <see cref="IIdentityService.EstablishCurrentPrincipal"/>, so a real
/// launch's principal — and therefore its authorship, its audit
/// attribution and every permission check — depended on which sample
/// happened to initialise last, and on the samples shipping at all. A
/// product built without them ran as nobody.
/// </para>
/// <para>
/// These tests drive the real <see cref="WorkspaceHost"/>, not a
/// hand-assembled one. What they assert is the shape the boundary is for:
/// <c>desktop session → ISessionPrincipalSource → ICurrentPrincipalAccessor
/// → services → domain</c>.
/// </para>
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class PrincipalBoundaryTests
{
    // ================================================================
    // The shell establishes a principal
    // ================================================================

    [AvaloniaFact]
    public async Task TheRealShell_EstablishesAPrincipal_AndPublishesItToEveryConsumer()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        await host.StartAsync();

        // The shell resolved one.
        Assert.NotNull(host.SessionPrincipal);
        Assert.False(string.IsNullOrWhiteSpace(host.SessionPrincipal!.Identity.Id));

        // And it is the one every consumer reads. The accessor is what the
        // domain, audit and permission checks actually consult — the host
        // property is only a window onto it.
        var accessor = (ICurrentPrincipalAccessor)host.Services!.GetService(typeof(ICurrentPrincipalAccessor));

        Assert.NotNull(accessor.Current);
        Assert.Same(host.SessionPrincipal, accessor.Current);
    }

    [AvaloniaFact]
    public async Task WorkTheUserCreates_IsAttributedToThatPrincipal_NotToUnknown()
    {
        // The consumer-side proof. Authorship is not something the shell
        // hands to a project; it is resolved from the accessor deep inside
        // the domain, which is exactly why the boundary had to be set
        // before any work happens.
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        await host.StartAsync();

        var project = await host.ProjectDirectory!.CreateAsync("P-0103", "Principal Boundary");

        var stored = (IHasRevisions)(await DomainOf(host).Repository.FindAsync(project.Id))!;

        Assert.Equal(host.SessionPrincipal!.Identity.Id, stored.AuthorPrincipalId);
        Assert.NotEqual(EngineeringDocumentStore.UnknownAuthorPrincipalId, stored.AuthorPrincipalId);
    }

    [AvaloniaFact]
    public async Task ThePrincipalHoldsWhatFirstPartySurfacesNeed_SoTheyReadRatherThanRefuse()
    {
        // The Requirements register reports verification as Unknown when
        // the session cannot read verification history. That honest
        // degradation was the *only* behaviour available before this work,
        // because a real launch had no principal at all.
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        await host.StartAsync();

        Assert.Contains(VerificationService.ReadPermission, host.SessionPrincipal!.Permissions);

        var verification = (IVerificationService)host.Services!.GetService(typeof(IVerificationService));

        // Reads, rather than throwing PermissionDeniedException.
        var records = await verification.GetVerificationHistoryAsync(Guid.NewGuid());
        Assert.NotNull(records);
    }

    // ================================================================
    // The boundary is a seam, not a hard-coded name
    // ================================================================

    [AvaloniaFact]
    public async Task ADifferentSource_ReplacesThePrincipal_WithoutTheDomainKnowing()
    {
        // What Administration will do one day: supply principals through
        // this one interface. Nothing in the engineering domain changes,
        // and no engineering object grows a user field.
        var supplied = new PlatformPrincipal(
            new PlatformIdentity("admin-supplied", "Supplied By Administration"),
            ApplicationPermissions.LocalSession);

        var host = new WorkspaceHost(
            WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath(),
            new StubSessionPrincipalSource(supplied));

        await host.StartAsync();

        Assert.Same(supplied, host.SessionPrincipal);

        var project = await host.ProjectDirectory!.CreateAsync("P-0104", "Supplied Principal");
        var stored = (IHasRevisions)(await DomainOf(host).Repository.FindAsync(project.Id))!;

        Assert.Equal("admin-supplied", stored.AuthorPrincipalId);
    }

    [AvaloniaFact]
    public async Task WhenNoPrincipalCanBeEstablished_TheProductSaysSo_RatherThanInventingOne()
    {
        // The `Unknown` behaviour is preserved deliberately. A source that
        // genuinely cannot answer must not be papered over with a
        // fabricated user: "unknown" is a true statement and a fake name
        // is not.
        var host = new WorkspaceHost(
            WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath(),
            new StubSessionPrincipalSource(null));

        await host.StartAsync();

        Assert.Null(host.SessionPrincipal);

        var project = await host.ProjectDirectory!.CreateAsync("P-0105", "No Principal");
        var stored = (IHasRevisions)(await DomainOf(host).Repository.FindAsync(project.Id))!;

        Assert.Equal(EngineeringDocumentStore.UnknownAuthorPrincipalId, stored.AuthorPrincipalId);
    }

    [Fact]
    public void TheLocalSource_UsesTheOperatingSystemAccount_AndFallsBackWithoutInventingAPerson()
    {
        Assert.Equal("ada", new LocalSessionPrincipalSource("ada").Resolve()!.Identity.Id);
        Assert.Equal("ada", new LocalSessionPrincipalSource("  ada  ").Resolve()!.Identity.Id);

        foreach (var empty in new[] { null, string.Empty, "   " })
        {
            var principal = new LocalSessionPrincipalSource(empty).Resolve();

            Assert.Equal(LocalSessionPrincipalSource.FallbackIdentityId, principal!.Identity.Id);
            Assert.Equal(LocalSessionPrincipalSource.FallbackDisplayName, principal.Identity.DisplayName);
        }

        // The parameterless production form answers, whatever the host is.
        Assert.NotNull(new LocalSessionPrincipalSource().Resolve());
    }

    // ================================================================
    // No authentication system was invented
    // ================================================================

    [Fact]
    public void TheBoundaryIsNotAuthentication()
    {
        // A guard, not a formality. The requirement was a *deliberate*
        // principal boundary for a single-user desktop product — the way
        // this goes wrong is by quietly growing into a login system, so
        // the absence is asserted rather than assumed.
        var members = typeof(ISessionPrincipalSource)
            .GetMembers(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Select(m => m.Name)
            .ToList();

        Assert.Equal(["Resolve"], members);

        var identityTypes = typeof(ISessionPrincipalSource).Assembly
            .GetTypes()
            .Where(t => t.IsPublic && t.Namespace == "Tempest.Core.Identity")
            .SelectMany(t => t.GetMembers(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .ToList();

        // Credentials, sessions-you-sign-into and bearer tokens are what
        // "authentication" means here, and none of them exist. Roles are
        // deliberately *not* on this list: IRoleProvider is pre-existing
        // configuration-driven permission grouping (`ADR-0043`), older than
        // this boundary and untouched by it — banning it would be a claim
        // about the platform's history rather than about this work.
        foreach (var forbidden in new[] { "Password", "Credential", "SignIn", "LogIn", "Login", "Logout", "Authenticate", "Token" })
        {
            var offenders = identityTypes.Where(n => n.Contains(forbidden, StringComparison.OrdinalIgnoreCase)).ToList();

            Assert.True(
                offenders.Count == 0,
                $"Identity now exposes '{forbidden}': {string.Join(", ", offenders)}. TD-103 is a principal boundary, not authentication.");
        }

        // The session's permissions are the two first-party surfaces need,
        // and specifically not a permissions *model*. Plugin capability
        // permissions gate registrants, never the person at the keyboard,
        // so none may leak into a session principal.
        // And the boundary itself grants a flat, fixed list rather than
        // resolving anything: no role lookup, no provider, no policy.
        Assert.DoesNotContain(
            typeof(ApplicationPermissions).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly),
            m => !m.IsSpecialName);

        Assert.Equal(2, ApplicationPermissions.LocalSession.Count);
        Assert.All(
            ApplicationPermissions.LocalSession,
            p => Assert.DoesNotContain("plugin.", p.Key, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NoEngineeringObject_CarriesAUserFieldOfItsOwn()
    {
        // The architecture requirement stated negatively: the domain
        // acquires a principal by *asking*, never by storing one. The one
        // legitimate exception is a business assignment — who a task is
        // assigned to is engineering data, not session identity.
        var offenders = typeof(EngineeringObjectBase).Assembly
            .GetTypes()
            .Where(t => t is { IsPublic: true, IsAbstract: false } && t.IsSubclassOf(typeof(EngineeringObjectBase)))
            .SelectMany(t => t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
            .Where(p => p.PropertyType == typeof(IPrincipal) || p.PropertyType == typeof(IIdentity))
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Engineering objects hold a principal directly: " + string.Join(", ", offenders));
    }

    // ================================================================
    // Fixtures
    // ================================================================

    private sealed class StubSessionPrincipalSource(IPrincipal? principal) : ISessionPrincipalSource
    {
        public IPrincipal? Resolve() => principal;
    }

    private static EngineeringDomainContext DomainOf(WorkspaceHost host) =>
        (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
}
