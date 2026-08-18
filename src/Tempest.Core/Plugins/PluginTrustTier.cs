namespace Tempest.Core.Plugins;

/// <summary>
/// The trust tier assigned to a loaded plugin, once, at Plugin Loading
/// (Phase 3.2), and immutable for the remainder of that process run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Exactly these three values — a <see cref="PluginManifest"/> never
/// carries a fourth, "rejected"/"untrusted" tier value.</b> ADR-0112's own
/// tier-assignment table has five rows, not three:
/// </para>
/// <para>
/// | Outcome | Tier |<br/>
/// |---|---|<br/>
/// | No <c>Signature</c> field, <c>Plugins:AllowUnsignedLoad</c> is <c>true</c> | Unsigned-Local |<br/>
/// | No <c>Signature</c> field, <c>Plugins:AllowUnsignedLoad</c> is <c>false</c> (default) | Rejected — new category 16 |<br/>
/// | <c>Signature</c> verifies, matched certificate is TempestOS's own | First-Party |<br/>
/// | <c>Signature</c> verifies, matched certificate is any other trusted entry | Verified-Signed |<br/>
/// | <c>Signature</c> present but fails to verify | Rejected — new category 15, never downgraded to Unsigned-Local |
/// </para>
/// <para>
/// The two "Rejected" rows are not a fourth tier a plugin ever holds — they
/// are Plugin Discovery/Loading *isolation outcomes* (ADR-0025 categories
/// 15 and 16, thrown as <see cref="PluginSignatureVerificationFailedException"/>
/// and <see cref="PluginUnsignedLoadNotAllowedException"/> respectively). A
/// plugin that reaches either outcome never produces a
/// <see cref="PluginManifest"/>/component principal that carries a trust
/// tier at all — it is isolated before Module Discovery ever sees it,
/// exactly as every other Plugin Discovery/Loading failure is (ADR-0025,
/// "What 'Isolated' Guarantees, Uniformly"). "Rejected" therefore has no
/// corresponding member here: it is not a state this enum needs to
/// represent, because no <see cref="PluginManifest"/> instance is ever
/// constructed for a plugin that reached it. A future contributor must
/// resist the temptation to "helpfully" add a fourth
/// <c>Untrusted</c>/<c>Rejected</c> value to this enum — doing so would
/// misrepresent an isolation outcome as a runtime tier a process could
/// observe running, which ADR-0112 explicitly states is never the case.
/// </para>
/// </remarks>
public enum PluginTrustTier
{
    /// <summary>
    /// No <c>Signature</c> field, loaded only because
    /// <c>Plugins:AllowUnsignedLoad</c> is explicitly <c>true</c>. Capability
    /// grants are clamped to a fixed, low ceiling regardless of what the
    /// manifest requests (ADR-0111).
    /// </summary>
    UnsignedLocal,

    /// <summary>
    /// The manifest's <c>Signature</c> verifies against a trust store entry
    /// other than TempestOS's own first-party publisher certificate. May be
    /// granted exactly the capabilities its manifest declares, subject to
    /// eligibility and constructor-conformance checks (ADR-0111).
    /// </summary>
    VerifiedSigned,

    /// <summary>
    /// Either a project-referenced/compiled-in module (never touches Plugin
    /// Discovery/Loading at all), or a plugin whose manifest's
    /// <c>Signature</c> verifies against TempestOS's own first-party
    /// publisher certificate. Unrestricted — not subject to any capability
    /// check.
    /// </summary>
    FirstParty,
}
