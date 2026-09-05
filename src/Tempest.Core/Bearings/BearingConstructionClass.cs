namespace Tempest.Core.Bearings;

/// <summary>The broad construction class of a bearing, where a source states one.</summary>
public enum BearingConstructionClass
{
    /// <summary>Not recorded.</summary>
    Unspecified,

    /// <summary>Ordinary bearing-steel construction.</summary>
    Standard,

    /// <summary>Corrosion-resistant construction (e.g. stainless rings and elements).</summary>
    CorrosionResistant,

    /// <summary>Hybrid construction — steel rings with ceramic rolling elements.</summary>
    Hybrid,

    /// <summary>Fully ceramic construction.</summary>
    AllCeramic,

    /// <summary>Polymer construction.</summary>
    Polymer,

    /// <summary>A construction this vocabulary does not name; record the source's own wording in <see cref="BearingConstruction.ManufacturerDesignation"/>.</summary>
    Other
}
