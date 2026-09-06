namespace Tempest.Core.Materials;

/// <summary>The controlled classification of engineering materials this library recognises.</summary>
/// <remarks>
/// <para>
/// A closed enum, and a deliberate change from this library's own original
/// open-string <c>Category</c> (`ADR-0055`, which chose the open string
/// "since no real discipline requirement has yet named a fixed taxonomy to
/// validate one against"). Group A is that requirement: a material's own
/// family determines which properties are meaningful — a polymer has no
/// grain direction, a ceramic no yield strength in the metallic sense — so
/// an unvalidated free-text family would leave the applicability model in
/// <see cref="MaterialFamilyTraits"/> with nothing to stand on.
/// </para>
/// <para>
/// The source's own wording is not lost: <see cref="MaterialDefinition.SourceClassification"/>
/// keeps it verbatim, exactly as the open string used to.
/// </para>
/// </remarks>
public enum MaterialFamily
{
    /// <summary>Not recorded. The honest default — never a claim the material has no family.</summary>
    Unspecified,

    /// <summary>Iron-based alloys other than stainless — carbon and low-alloy steels, tool steels.</summary>
    Steel,

    /// <summary>Corrosion-resistant iron-based alloys.</summary>
    StainlessSteel,

    /// <summary>Grey, ductile, malleable and white cast irons.</summary>
    CastIron,

    /// <summary>Aluminium and its alloys.</summary>
    Aluminium,

    /// <summary>Copper and its alloys — brasses, bronzes, cupronickels.</summary>
    CopperAlloy,

    /// <summary>Titanium and its alloys.</summary>
    Titanium,

    /// <summary>Nickel-based alloys, including the high-temperature superalloys.</summary>
    NickelAlloy,

    /// <summary>Magnesium, zinc and other light or low-melting-point alloys not named above.</summary>
    OtherMetal,

    /// <summary>Thermoplastics.</summary>
    Thermoplastic,

    /// <summary>Thermosetting polymers.</summary>
    Thermoset,

    /// <summary>Elastomers and rubbers.</summary>
    Elastomer,

    /// <summary>Technical ceramics.</summary>
    Ceramic,

    /// <summary>Fibre-reinforced and other engineered composites.</summary>
    Composite,

    /// <summary>Glasses.</summary>
    Glass,

    /// <summary>A recognised material this taxonomy does not yet name. Record the source's own wording in <see cref="MaterialDefinition.SourceClassification"/>.</summary>
    Other
}
