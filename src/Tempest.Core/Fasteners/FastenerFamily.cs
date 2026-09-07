namespace Tempest.Core.Fasteners;

/// <summary>The controlled classification of fasteners this library recognises.</summary>
/// <remarks>
/// <para>
/// Classifies <em>what the item is</em>, not how it is driven or what head
/// it carries: those are <see cref="FastenerDriveType"/> and
/// <see cref="FastenerHeadType"/>, and are orthogonal to this. A closed
/// enum, because the family determines which parts of a fastener record
/// are meaningful at all — a washer has no thread and a stud has no head —
/// and <see cref="FastenerFamilyTraits"/> would have nothing to stand on
/// if the family were free text.
/// </para>
/// <para>
/// The source's own wording is never lost:
/// <see cref="FastenerDefinition.SourceClassification"/> keeps it verbatim.
/// </para>
/// </remarks>
public enum FastenerFamily
{
    /// <summary>Not recorded. The honest default — never a claim the item has no family.</summary>
    Unspecified,

    /// <summary>An externally threaded fastener with a head, intended to be used with a nut.</summary>
    Bolt,

    /// <summary>An externally threaded fastener with a head, intended to engage a threaded hole.</summary>
    Screw,

    /// <summary>A headless externally threaded fastener tightened against a surface by its own end.</summary>
    SetScrew,

    /// <summary>A headless externally threaded rod, threaded at one or both ends.</summary>
    Stud,

    /// <summary>An internally threaded fastener used with a bolt or stud.</summary>
    Nut,

    /// <summary>An unthreaded bearing or spacing disc used under a head or nut.</summary>
    Washer,

    /// <summary>An internally threaded insert installed into a host material.</summary>
    ThreadedInsert,

    /// <summary>A ring seated in a groove to retain a component axially.</summary>
    RetainingRing,

    /// <summary>A fastener installed by permanent deformation.</summary>
    Rivet,

    /// <summary>An unthreaded pin used for location, retention or shear transfer.</summary>
    Pin,

    /// <summary>A fastener this taxonomy does not classify. <see cref="FastenerDefinition.SourceClassification"/> must then record the source's own wording.</summary>
    Other
}
