namespace Tempest.Core.BusinessGovernance;

/// <summary>
/// How sensitive a business record is, and therefore how it must be
/// handled.
/// </summary>
/// <remarks>
/// <para>
/// <b>A classification is a label, not an access-control mechanism.</b>
/// TempestOS already has an access-control system —
/// <see cref="Tempest.Core.Identity.IPermissionEvaluator"/>, roles and
/// permissions — and `P07` does not build a second one. What `P07`
/// records is the handling requirement that a person or a future
/// permission policy acts on; what it never does is decide, on its own,
/// who may read something.
/// </para>
/// <para>
/// Applying a classification is therefore a statement about the content,
/// not a guarantee about the storage. A record marked
/// <see cref="ClientConfidential"/> is not thereby encrypted, restricted
/// or audited differently; it is marked, so that the people and policies
/// that do those things know that they must.
/// </para>
/// </remarks>
public enum ConfidentialityClassification
{
    /// <summary>Not classified. Treated as the most restrictive until somebody classifies it, not the least.</summary>
    Unclassified,

    /// <summary>May be published — marketing material, a public rate card, a published policy statement.</summary>
    Public,

    /// <summary>For the organisation, not for outside it. The ordinary default for internal business records.</summary>
    Internal,

    /// <summary>Sensitive within the organisation; not for general internal circulation.</summary>
    Confidential,

    /// <summary>Would damage the organisation's commercial position if disclosed — margins, negotiated rates, pipeline value.</summary>
    CommerciallySensitive,

    /// <summary>Belongs to a client, or was disclosed under an obligation of confidence to one. Not the organisation's to share.</summary>
    ClientConfidential,

    /// <summary>Restricted to named individuals — legal advice, personnel matters, an unannounced transaction.</summary>
    Restricted
}

/// <summary>Reasoning over <see cref="ConfidentialityClassification"/>.</summary>
public static class ConfidentialityClassifications
{
    /// <summary>Every classification, from least to most restrictive.</summary>
    public static IReadOnlyList<ConfidentialityClassification> All { get; } =
    [
        ConfidentialityClassification.Public,
        ConfidentialityClassification.Internal,
        ConfidentialityClassification.Confidential,
        ConfidentialityClassification.CommerciallySensitive,
        ConfidentialityClassification.ClientConfidential,
        ConfidentialityClassification.Restricted,
        ConfidentialityClassification.Unclassified,
    ];

    /// <summary>
    /// How restrictive a classification is, so that a record derived from
    /// several sources can take the most restrictive of them.
    /// </summary>
    /// <remarks>
    /// <see cref="ConfidentialityClassification.Unclassified"/> ranks
    /// alongside <see cref="ConfidentialityClassification.Confidential"/>
    /// rather than at zero. An unclassified record is one nobody has
    /// assessed, and treating "not yet looked at" as "safe to publish" is
    /// the mistake this ranking exists to prevent.
    /// </remarks>
    public static int Restrictiveness(ConfidentialityClassification classification) => classification switch
    {
        ConfidentialityClassification.Public => 0,
        ConfidentialityClassification.Internal => 1,
        ConfidentialityClassification.Confidential => 2,
        ConfidentialityClassification.Unclassified => 2,
        ConfidentialityClassification.CommerciallySensitive => 3,
        ConfidentialityClassification.ClientConfidential => 4,
        ConfidentialityClassification.Restricted => 5,
        _ => 2,
    };

    /// <summary>
    /// The classification a record combining <paramref name="classifications"/>
    /// must carry — the most restrictive of them.
    /// </summary>
    /// <remarks>
    /// A forecast built from a client-confidential contract value is
    /// client-confidential. Derived records inherit their sources'
    /// sensitivity; they never dilute it.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="classifications"/> is <see langword="null"/>.</exception>
    public static ConfidentialityClassification MostRestrictive(IEnumerable<ConfidentialityClassification> classifications)
    {
        ArgumentNullException.ThrowIfNull(classifications);

        var seen = false;
        var highest = ConfidentialityClassification.Public;

        foreach (var classification in classifications)
        {
            seen = true;

            if (Restrictiveness(classification) > Restrictiveness(highest))
                highest = classification;
        }

        return seen ? highest : ConfidentialityClassification.Unclassified;
    }
}
