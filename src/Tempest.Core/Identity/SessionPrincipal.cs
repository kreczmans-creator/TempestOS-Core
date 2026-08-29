namespace Tempest.Core.Identity;

/// <summary>
/// The permissions the running application needs in order to operate as
/// the product it currently is.
/// </summary>
/// <remarks>
/// <para>
/// Declared once, here, rather than spelled out at each place a principal
/// is built. A permission belongs on this list when a first-party product
/// surface would otherwise be unusable without it — not because some code
/// path happens to check for it.
/// </para>
/// <para>
/// <b>This is not a roles or authorisation model.</b> It is the flat set a
/// single-user local session holds. When Administration becomes the
/// authority for identity, roles and permissions, it supplies principals
/// through <see cref="ISessionPrincipalSource"/> and this list stops being
/// consulted — nothing else has to change, because nothing else knows
/// about it.
/// </para>
/// </remarks>
public static class ApplicationPermissions
{
    /// <summary>
    /// The permissions a local single-user desktop session holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>verification.read</c> — the Requirements and Verification
    /// surfaces read verification history; without it a user sees their
    /// own project's requirements with the verification column reporting
    /// that it cannot be read.
    /// </para>
    /// <para>
    /// <c>audit.query</c> — the audit trail is a first-party surface over
    /// the user's own actions on their own machine.
    /// </para>
    /// <para>
    /// Deliberately short, and deliberately not "everything": plugin
    /// capability permissions (<c>plugin.*</c>) gate what *components*
    /// registering into the platform may do and are checked against the
    /// registrant, never the person using the product, so granting them to
    /// a session principal would be meaningless at best.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<Permission> LocalSession { get; } =
    [
        Verification.VerificationService.ReadPermission,
        Audit.AuditQuery.QueryPermission,
    ];
}

/// <summary>
/// The one place the running application decides who is using it.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the principal boundary, and it is deliberately small.</b>
/// Everything downstream — the engineering domain, audit attribution,
/// ownership, assignment, permission checks — already reads
/// <see cref="ICurrentPrincipalAccessor"/> and needs no knowledge of where
/// that principal came from. What was missing was anything at all on the
/// *other* side of that accessor in a real product launch: only sample
/// modules ever called <see cref="IIdentityService.EstablishCurrentPrincipal"/>,
/// so what a running session could do depended on which sample happened to
/// initialise last (`TD-103`).
/// </para>
/// <para>
/// The shape is:
/// </para>
/// <code>
/// desktop session → ISessionPrincipalSource → ICurrentPrincipalAccessor → services → domain
/// </code>
/// <para>
/// and specifically <b>not</b> a user field invented on engineering
/// objects, or a username threaded through call sites. An engineering
/// object is never responsible for knowing who is signed in.
/// </para>
/// <para>
/// <b>This is not authentication.</b> There are no credentials, no login,
/// no external identity provider and no roles model here, and none is
/// implied. TempestOS today is a local single-user desktop application and
/// this boundary says so honestly. It exists so that when Administration
/// becomes the authority for identity, roles and permissions, it can
/// implement this one interface and everything downstream keeps working
/// unchanged — the engineering domain does not get redesigned to acquire a
/// user.
/// </para>
/// </remarks>
public interface ISessionPrincipalSource
{
    /// <summary>
    /// The principal for this session, or <see langword="null"/> when one
    /// genuinely cannot be established.
    /// </summary>
    /// <remarks>
    /// Nullable on purpose. "No principal" is a real state — a headless
    /// process, a test rig, or a future Administration implementation with
    /// nobody signed in — and consumers already handle it honestly:
    /// authorship falls back to
    /// <c>EngineeringDocumentStore.UnknownAuthorPrincipalId</c> and the
    /// project requirements register reports verification as
    /// <c>Unknown</c> rather than claiming nothing was recorded. Returning
    /// an invented principal to avoid the null would destroy both of those
    /// truths.
    /// </remarks>
    IPrincipal? Resolve();
}

/// <summary>
/// The production source for the current product: one local desktop user,
/// no authentication.
/// </summary>
/// <remarks>
/// <para>
/// The identity is the operating system's own account name, because that
/// is the only true statement available about who is using a local
/// single-user application. It is read once, at construction, and is not a
/// claim of having authenticated anyone — it is a label for attribution,
/// so that audit records, authorship and ownership say something more
/// useful than <c>"unknown"</c>.
/// </para>
/// <para>
/// Where the OS gives no usable name the identity falls back to a stable,
/// clearly-named local id rather than an empty string or a fabricated
/// person. <b>No username is hard-coded anywhere else in the
/// application</b>: this class is the only place a session identity is
/// constructed.
/// </para>
/// </remarks>
public sealed class LocalSessionPrincipalSource : ISessionPrincipalSource
{
    /// <summary>The identity id used when the operating system reports no usable account name.</summary>
    public const string FallbackIdentityId = "local-user";

    /// <summary>The display name used when the operating system reports no usable account name.</summary>
    public const string FallbackDisplayName = "Local User";

    private readonly IPrincipal _principal;

    /// <summary>Initialises a new instance of the <see cref="LocalSessionPrincipalSource"/> class from the operating system's own account name.</summary>
    public LocalSessionPrincipalSource()
        : this(SafeUserName())
    {
    }

    /// <summary>Initialises a new instance of the <see cref="LocalSessionPrincipalSource"/> class for <paramref name="userName"/>.</summary>
    /// <remarks>The explicit form exists so a test can state the account name rather than inherit whatever the build agent happens to run as.</remarks>
    public LocalSessionPrincipalSource(string? userName)
    {
        var name = string.IsNullOrWhiteSpace(userName) ? null : userName.Trim();

        _principal = new PlatformPrincipal(
            new PlatformIdentity(name ?? FallbackIdentityId, name ?? FallbackDisplayName),
            ApplicationPermissions.LocalSession);
    }

    /// <inheritdoc />
    public IPrincipal? Resolve() => _principal;

    private static string? SafeUserName()
    {
        try
        {
            return Environment.UserName;
        }
        catch (InvalidOperationException)
        {
            // Some hosts genuinely cannot answer. The fallback identity is
            // a better answer than a crash on startup, and a far better
            // one than inventing a person.
            return null;
        }
    }
}
