namespace Tempest.Core.Fasteners;

/// <summary>
/// Which parts of a fastener record are meaningful for a given
/// <see cref="FastenerFamily"/> — this library's own type-aware modelling
/// rule, and the single place it is stated.
/// </summary>
/// <remarks>
/// The same discipline <see cref="Materials.MaterialFamilyTraits"/> applies
/// to material families. Reading applicability from here lets a missing
/// value be reported as
/// <see cref="ReferenceData.ReferencePropertyAvailability.NotApplicable"/>
/// — a washer has no thread, and recording that is entirely different from
/// failing to record one.
/// </remarks>
public static class FastenerFamilyTraits
{
    /// <summary>Whether the family carries a thread on its outside.</summary>
    public static bool IsExternallyThreaded(FastenerFamily family) =>
        family is FastenerFamily.Bolt or FastenerFamily.Screw or FastenerFamily.SetScrew or FastenerFamily.Stud;

    /// <summary>Whether the family carries a thread on its inside.</summary>
    public static bool IsInternallyThreaded(FastenerFamily family) =>
        family is FastenerFamily.Nut or FastenerFamily.ThreadedInsert;

    /// <summary>Whether a thread specification is a meaningful part of this family's own description.</summary>
    public static bool IsThreaded(FastenerFamily family) =>
        IsExternallyThreaded(family) || IsInternallyThreaded(family);

    /// <summary>Whether the family has a head. A stud and a set screw do not, which is what distinguishes them from a bolt and a screw.</summary>
    public static bool HasHead(FastenerFamily family) =>
        family is FastenerFamily.Bolt or FastenerFamily.Screw;

    /// <summary>Whether the family has a driving feature the installer engages.</summary>
    public static bool HasDriveFeature(FastenerFamily family) =>
        family is FastenerFamily.Bolt or FastenerFamily.Screw or FastenerFamily.SetScrew or FastenerFamily.Nut;

    /// <summary>Whether an overall length is a meaningful dimension for the family. A washer records a thickness instead.</summary>
    public static bool HasNominalLength(FastenerFamily family) =>
        family is not (FastenerFamily.Washer or FastenerFamily.RetainingRing or FastenerFamily.Nut);

    /// <summary>
    /// Whether a property class or grade designation applies. Threaded
    /// fasteners carry one; a plain washer is specified by hardness and
    /// material instead.
    /// </summary>
    public static bool TakesPropertyClass(FastenerFamily family) => IsThreaded(family);

    /// <summary>
    /// Whether a proof load or proof strength is a meaningful published
    /// figure for the family — the load-carrying threaded fasteners, and
    /// nothing else.
    /// </summary>
    public static bool TakesProofLoad(FastenerFamily family) =>
        family is FastenerFamily.Bolt or FastenerFamily.Screw or FastenerFamily.Stud or FastenerFamily.Nut;

    /// <summary>
    /// Whether a published tightening torque is a meaningful thing for the
    /// family to record. Only a fastener that is tightened has one.
    /// </summary>
    public static bool TakesTighteningTorque(FastenerFamily family) =>
        family is FastenerFamily.Bolt or FastenerFamily.Screw or FastenerFamily.SetScrew or FastenerFamily.Nut;

    /// <summary>
    /// Whether this table can speak for <paramref name="family"/> at all.
    /// <see cref="FastenerFamily.Unspecified"/> and
    /// <see cref="FastenerFamily.Other"/> are unclassified by construction:
    /// every answer above is conservative for them and must be read as "not
    /// known to apply", never "known not to apply".
    /// </summary>
    public static bool IsApplicabilityKnown(FastenerFamily family) =>
        family is not (FastenerFamily.Unspecified or FastenerFamily.Other);
}
