namespace Tempest.Core.Plugins;

/// <summary>
/// The tier-marker <see cref="Identity.Permission"/> keys carried in a
/// plugin's component <see cref="Identity.IPrincipal.Permissions"/> set,
/// identifying which <see cref="PluginTrustTier"/> it was assigned at
/// Plugin Loading.
/// </summary>
/// <remarks>
/// No new authorization type family is introduced — a plugin's assigned
/// trust tier is represented as an ordinary <see cref="Identity.Permission"/>
/// entry in its component principal's own <see cref="Identity.IPrincipal.Permissions"/>
/// list, exactly as every other <c>plugin.*</c> capability is (ADR-0111,
/// "Capability keys reuse <c>Permission</c> directly"; Consequences,
/// "Reuses <c>Permission</c>/<c>IPrincipal</c>/<c>IIdentity</c>/
/// <c>IPermissionEvaluator</c> exactly as they exist — no new authorization
/// type family"). This lets the trust-ordered registration rule's own
/// priority comparison (<see cref="Rank"/>) and ownership-override check
/// (<see cref="IsFirstParty"/>) — used by <c>NavigationService</c> and the
/// Command Framework's registration path — read a principal's tier through
/// the exact same <see cref="Identity.IPrincipal.Permissions"/> shape every
/// other capability check already uses, rather than a parallel field.
/// </remarks>
public static class PluginTrustPermission
{
    /// <summary>
    /// The permission key marking a component principal as First-Party
    /// tier.
    /// </summary>
    public const string FirstParty = "plugin.trust.first-party";

    /// <summary>
    /// The permission key marking a component principal as Verified-Signed
    /// tier.
    /// </summary>
    public const string VerifiedSigned = "plugin.trust.verified-signed";

    /// <summary>
    /// The permission key marking a component principal as Unsigned-Local
    /// tier.
    /// </summary>
    public const string UnsignedLocal = "plugin.trust.unsigned-local";

    /// <summary>
    /// Maps a <see cref="PluginTrustTier"/> to its corresponding
    /// tier-marker permission key.
    /// </summary>
    /// <param name="tier">The trust tier to map.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="tier"/> is not a recognised <see cref="PluginTrustTier"/>
    /// value.
    /// </exception>
    public static string ForTier(PluginTrustTier tier) => tier switch
    {
        PluginTrustTier.FirstParty => FirstParty,
        PluginTrustTier.VerifiedSigned => VerifiedSigned,
        PluginTrustTier.UnsignedLocal => UnsignedLocal,
        _ => throw new ArgumentOutOfRangeException(nameof(tier)),
    };

    /// <summary>
    /// Ranks a principal's trust tier for the priority-eviction rule
    /// (ADR-0111, "a higher-trust-tier registration always wins... regardless
    /// of order"). A null principal is a genuine first-party module (the Host
    /// never pushes a component scope for one) and ranks identically to a
    /// plugin that itself achieved FirstParty tier. Higher return value =
    /// higher trust.
    /// </summary>
    /// <param name="principal">
    /// The registering component's own principal, or <see langword="null"/>
    /// for first-party code (no component scope pushed).
    /// </param>
    /// <returns>
    /// 3 for First-Party (or <see langword="null"/>); 2 for Verified-Signed;
    /// 1 for Unsigned-Local, or any principal carrying no recognised tier
    /// marker (defensive — treated as lowest).
    /// </returns>
    public static int Rank(Identity.IPrincipal? principal)
    {
        if (principal is null) return 3;
        if (principal.Permissions.Contains(new Identity.Permission(FirstParty))) return 3;
        if (principal.Permissions.Contains(new Identity.Permission(VerifiedSigned))) return 2;
        return 1; // UnsignedLocal, or any principal carrying no recognised tier marker (defensive, treated as lowest)
    }

    /// <summary>
    /// True for a genuine first-party module (null principal) or a plugin
    /// that itself achieved FirstParty tier — the only principals holding an
    /// ownership-override permission "by construction" (ADR-0111).
    /// </summary>
    /// <param name="principal">
    /// The component principal to check, or <see langword="null"/> for
    /// first-party code (no component scope pushed).
    /// </param>
    public static bool IsFirstParty(Identity.IPrincipal? principal) =>
        principal is null || principal.Permissions.Contains(new Identity.Permission(FirstParty));
}
