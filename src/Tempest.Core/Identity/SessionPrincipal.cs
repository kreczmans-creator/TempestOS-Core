using System.Runtime.Versioning;
using Tempest.Core.Configuration;

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

        // `WP 17.9.3`: verifying and releasing reference data are permissions
        // (ADR-0143, amended), held by both session roles in a single-user
        // consultancy desktop. The gate exists so that a configuration, or a
        // later role model, can withdraw them without touching the service.
        ReferenceData.Review.ReferenceReviewService.VerifyPermission,
        ReferenceData.Review.ReferenceReviewService.ReleasePermission,
    ];
}

/// <summary>
/// The role a session principal holds, kept separate from its identity
/// (`WP 17.2A`, ADR-0146).
/// </summary>
/// <remarks>
/// A role governs which commands a session is offered; it carries no
/// bearing on the identity id an audit row, authorship field or check
/// record stores. Configuration may set it (<c>Identity:Role</c>); the
/// identity id is never derived from it.
/// </remarks>
public enum SessionRole
{
    /// <summary>Authors and runs engineering work. The default role.</summary>
    Engineer,

    /// <summary>Independently checks work authored by an Engineer.</summary>
    Checker,
}

/// <summary>
/// A session's own principal: a stable, OS-derived identity id, a
/// configurable display name, and a role kept separate from both
/// (`WP 17.2A`, ADR-0146).
/// </summary>
/// <remarks>
/// <para>
/// Extends <see cref="IPrincipal"/> rather than replacing it: every
/// existing consumer of <see cref="ICurrentPrincipalAccessor"/>,
/// <see cref="IPrincipal.Identity"/> and <see cref="IPrincipal.Permissions"/>
/// keeps working unchanged. <see cref="IdentityId"/> is always exactly
/// <see cref="IPrincipal.Identity"/>'s own <see cref="IIdentity.Id"/> —
/// restated here as a same-named, directly-typed property because that is
/// what an audit row, an authorship field and a check record are specified
/// to store, and spelling that out at every call site as
/// <c>principal.Identity.Id</c> is exactly the kind of indirection this
/// collapse (ADR-0146, Decision B) exists to remove.
/// </para>
/// </remarks>
public interface ISessionPrincipal : IPrincipal
{
    /// <summary>
    /// Gets the stable identity id this session's every audit row,
    /// authorship field and check record stores. Read from the OS at each
    /// launch (<see cref="SessionPrincipalSource"/>) and never from
    /// configuration.
    /// </summary>
    string IdentityId { get; }

    /// <summary>
    /// Gets the human-readable display name — <c>Identity:DisplayName</c>
    /// if configured, otherwise the OS account name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the role this session holds — <c>Identity:Role</c> if
    /// configured, otherwise <see cref="SessionRole.Engineer"/>.
    /// </summary>
    SessionRole Role { get; }
}

/// <summary>
/// The concrete, immutable <see cref="ISessionPrincipal"/> implementation.
/// </summary>
public sealed class SessionPrincipal : ISessionPrincipal
{
    /// <summary>Initialises a new instance of the <see cref="SessionPrincipal"/> class.</summary>
    /// <param name="identityId">The stable, OS-derived identity id.</param>
    /// <param name="displayName">The human-readable display name.</param>
    /// <param name="role">The role this session holds.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="identityId"/> or <paramref name="displayName"/> is
    /// <see langword="null"/>, empty, or whitespace.
    /// </exception>
    public SessionPrincipal(string identityId, string displayName, SessionRole role)
    {
        if (string.IsNullOrWhiteSpace(identityId))
            throw new ArgumentException("Identity id must not be null, empty, or whitespace.", nameof(identityId));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name must not be null, empty, or whitespace.", nameof(displayName));

        IdentityId = identityId;
        DisplayName = displayName;
        Role = role;
        Identity = new PlatformIdentity(identityId, displayName);
        Permissions = ApplicationPermissions.LocalSession;
    }

    /// <inheritdoc />
    public string IdentityId { get; }

    /// <inheritdoc />
    public string DisplayName { get; }

    /// <inheritdoc />
    public SessionRole Role { get; }

    /// <inheritdoc />
    public IIdentity Identity { get; }

    /// <inheritdoc />
    public IReadOnlyList<Permission> Permissions { get; }
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
/// modules ever established one, so what a running session could do
/// depended on which sample happened to initialise last (`TD-103`).
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
    ISessionPrincipal? Resolve();
}

/// <summary>
/// The production source for the current product: one local desktop
/// session, no authentication (`WP 17.2A`, ADR-0146).
/// </summary>
/// <remarks>
/// <para>
/// <b>Replaces <c>LocalSessionPrincipalSource</c>.</b> The identity id is a
/// stable id read from the operating system at each launch — on Windows,
/// the current Windows account's own security identifier
/// (<see cref="System.Security.Principal.WindowsIdentity.User"/>); on every
/// other platform, <see cref="Environment.UserName"/> — and is <b>never</b>
/// read from configuration. The display name and role are the two fields
/// configuration may change: <c>Identity:DisplayName</c> overrides the
/// display name (falling back to the OS account name), and
/// <c>Identity:Role</c> overrides the role (falling back to
/// <see cref="SessionRole.Engineer"/>). This is the same non-negotiable-
/// identity principle <c>ADR-0043</c> already established, narrowed from
/// "local, extensible" to "one session, OS-derived" (ADR-0146).
/// </para>
/// <para>
/// Where the OS gives no usable name the identity falls back to a stable,
/// clearly-named local id rather than an empty string or a fabricated
/// person. <b>No username is hard-coded anywhere else in the
/// application</b>: this class is the only place a session identity is
/// constructed.
/// </para>
/// </remarks>
public sealed class SessionPrincipalSource : ISessionPrincipalSource
{
    /// <summary>The configuration key an operator overrides the display name with.</summary>
    public const string DisplayNameConfigurationKey = "Identity:DisplayName";

    /// <summary>The configuration key an operator overrides the role with.</summary>
    public const string RoleConfigurationKey = "Identity:Role";

    /// <summary>The identity id used when the operating system reports no usable account name or SID.</summary>
    public const string FallbackIdentityId = "local-user";

    private readonly ISessionPrincipal _principal;

    /// <summary>
    /// Initialises a new instance of the <see cref="SessionPrincipalSource"/>
    /// class, reading the identity id from the operating system and the
    /// display name and role from <paramref name="configuration"/>, if
    /// supplied.
    /// </summary>
    /// <param name="configuration">
    /// The configuration <c>Identity:DisplayName</c> and <c>Identity:Role</c>
    /// are read from, or <see langword="null"/> to take every default (the
    /// OS account name as display name, <see cref="SessionRole.Engineer"/>
    /// as role).
    /// </param>
    /// <exception cref="ConfigurationException">
    /// <c>Identity:Role</c> is configured but is not a valid
    /// <see cref="SessionRole"/> name.
    /// </exception>
    public SessionPrincipalSource(IConfigurationProvider? configuration = null)
        : this(ResolveOsIdentityId(), configuration)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="SessionPrincipalSource"/>
    /// class for an explicit identity id.
    /// </summary>
    /// <remarks>
    /// Internal test seam — lets a test state the OS identity id
    /// deterministically rather than depend on whatever account the build
    /// agent happens to run as, mirroring <c>LocalSessionPrincipalSource</c>'s
    /// own former public seam. The identity id is still never taken from
    /// <paramref name="configuration"/>: only <see cref="DisplayNameConfigurationKey"/>
    /// and <see cref="RoleConfigurationKey"/> are read from it.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="identityId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ConfigurationException">
    /// <c>Identity:Role</c> is configured but is not a valid <see cref="SessionRole"/> name.
    /// </exception>
    internal SessionPrincipalSource(string identityId, IConfigurationProvider? configuration)
    {
        if (string.IsNullOrWhiteSpace(identityId))
            throw new ArgumentException("Identity id must not be null, empty, or whitespace.", nameof(identityId));

        var displayName = ResolveDisplayName(identityId, configuration);
        var role = ResolveRole(configuration);

        _principal = new SessionPrincipal(identityId, displayName, role);
    }

    /// <inheritdoc />
    public ISessionPrincipal? Resolve() => _principal;

    [SupportedOSPlatformGuard("windows")]
    private static bool IsWindows() => OperatingSystem.IsWindows();

    private static string ResolveOsIdentityId()
    {
        if (IsWindows())
        {
            var sid = SafeWindowsSid();

            if (!string.IsNullOrWhiteSpace(sid))
                return sid;
        }

        var userName = SafeUserName();

        return string.IsNullOrWhiteSpace(userName) ? FallbackIdentityId : userName!.Trim();
    }

    [SupportedOSPlatform("windows")]
    private static string? SafeWindowsSid()
    {
        try
        {
            return System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Security.SecurityException or UnauthorizedAccessException)
        {
            // Some hosts (a locked-down service account, a container with no
            // usable token) genuinely cannot answer. Falling through to the
            // OS user name, and ultimately FallbackIdentityId, is a better
            // answer than a crash on startup.
            return null;
        }
    }

    private static string? SafeUserName()
    {
        try
        {
            return Environment.UserName;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string ResolveDisplayName(string identityId, IConfigurationProvider? configuration)
    {
        if (configuration is not null
            && configuration.TryGetValue(DisplayNameConfigurationKey, out var configured)
            && !string.IsNullOrWhiteSpace(configured))
        {
            return configured!.Trim();
        }

        var userName = SafeUserName();

        return string.IsNullOrWhiteSpace(userName) ? identityId : userName!.Trim();
    }

    private static SessionRole ResolveRole(IConfigurationProvider? configuration)
    {
        if (configuration is null || !configuration.TryGetValue(RoleConfigurationKey, out var configured) || string.IsNullOrWhiteSpace(configured))
            return SessionRole.Engineer;

        if (!Enum.TryParse<SessionRole>(configured, ignoreCase: true, out var role))
        {
            throw new ConfigurationException(
                $"Configuration value '{configured}' for key '{RoleConfigurationKey}' is not a valid SessionRole.");
        }

        return role;
    }
}
