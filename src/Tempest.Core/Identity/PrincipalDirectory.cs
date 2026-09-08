using System.Security.Principal;

namespace Tempest.Core.Identity;

/// <summary>
/// Turns a stored identity id into something a person can read
/// (`WP 17.9.1`). Authorship, audit and provenance fields store the
/// stable <see cref="ISessionPrincipal.IdentityId"/> — on Windows the
/// account SID — which is the right thing to store and the wrong thing to
/// show: the first Windows review of `v0.17.0` saw
/// <c>S-1-5-21-…</c> under "Last Revised By".
/// </summary>
public interface IPrincipalDirectory
{
    /// <summary>
    /// The display name for <paramref name="identityId"/>: the session
    /// principal's own name when the id is the session's, the OS account
    /// name when the platform can resolve it, and the id itself when it
    /// cannot — never an invented name.
    /// </summary>
    string Describe(string? identityId);
}

/// <summary>
/// The default <see cref="IPrincipalDirectory"/>: the current principal
/// first, then the operating system, then the id verbatim.
/// </summary>
public sealed class PrincipalDirectory : IPrincipalDirectory
{
    /// <summary>What an absent or unknown executor is shown as.</summary>
    public const string UnknownDisplayName = "Unknown";

    private readonly ICurrentPrincipalAccessor _currentPrincipal;
    private readonly Dictionary<string, string> _resolved = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public PrincipalDirectory(ICurrentPrincipalAccessor currentPrincipal)
    {
        ArgumentNullException.ThrowIfNull(currentPrincipal);
        _currentPrincipal = currentPrincipal;
    }

    /// <inheritdoc />
    public string Describe(string? identityId)
    {
        if (string.IsNullOrWhiteSpace(identityId) || string.Equals(identityId, "unknown", StringComparison.OrdinalIgnoreCase))
            return UnknownDisplayName;

        var current = _currentPrincipal.Current?.Identity;
        if (current is not null && string.Equals(current.Id, identityId, StringComparison.Ordinal))
            return current.DisplayName;

        lock (_gate)
        {
            if (_resolved.TryGetValue(identityId, out var cached))
                return cached;
        }

        var resolved = ResolveFromOperatingSystem(identityId) ?? identityId;

        lock (_gate)
            _resolved[identityId] = resolved;

        return resolved;
    }

    /// <summary>
    /// On Windows a SID resolves to <c>DOMAIN\name</c> through the local
    /// security authority; only the account part is shown. Anything that
    /// is not a SID, or that the OS cannot translate, resolves to
    /// <see langword="null"/> so the caller shows the id as recorded.
    /// </summary>
    private static string? ResolveFromOperatingSystem(string identityId)
    {
        if (!OperatingSystem.IsWindows() || !identityId.StartsWith("S-1-", StringComparison.Ordinal))
            return null;

        try
        {
            var account = (NTAccount)new SecurityIdentifier(identityId).Translate(typeof(NTAccount));
            var name = account.Value;
            var separator = name.LastIndexOf('\\');
            return separator >= 0 && separator < name.Length - 1 ? name[(separator + 1)..] : name;
        }
        catch (Exception ex) when (ex is IdentityNotMappedException or SystemException)
        {
            return null;
        }
    }
}
