namespace Tempest.Core.Bearings;

/// <summary>
/// This library's own common classification of a bearing's own sealing or
/// shielding arrangement — deliberately manufacturer-neutral.
/// </summary>
/// <remarks>
/// No manufacturer's own suffix vocabulary (2RS, 2Z, DDU, LLU, and so on)
/// is encoded here as universal truth. A record keeps the manufacturer's
/// own designation verbatim in
/// <see cref="BearingSealingArrangement.ManufacturerDesignation"/> and maps
/// it to one of these members only where that mapping is genuinely
/// defensible; where it is not, the mapping is left
/// <see cref="Unspecified"/> rather than guessed.
/// </remarks>
public enum BearingSealingType
{
    /// <summary>The arrangement is not recorded, or the manufacturer's own designation could not be defensibly mapped.</summary>
    Unspecified,

    /// <summary>No seal or shield.</summary>
    Open,

    /// <summary>A non-rubbing metal shield.</summary>
    Shielded,

    /// <summary>Sealed, but the source does not distinguish contact from non-contact.</summary>
    Sealed,

    /// <summary>A seal that rubs on the inner ring.</summary>
    ContactSeal,

    /// <summary>A seal that runs with a controlled gap, without rubbing.</summary>
    NonContactSeal
}
