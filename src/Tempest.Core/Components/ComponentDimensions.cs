using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Components;

/// <summary>
/// The envelope and mounting dimensions every component family can carry,
/// whatever its typed detail.
/// </summary>
/// <remarks>
/// Kept apart from the per-family detail records because these are the
/// dimensions a caller asks for without knowing what the component is —
/// what bore does it fit, how much space does it need, what does it weigh.
/// Every field stays <see langword="null"/> where the source supplied
/// nothing.
/// </remarks>
/// <param name="BoreDiameter">The bore fitted to a shaft. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="OutsideDiameter">The overall outside diameter. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="OverallLength">The overall length along the component's own principal axis. <see langword="null"/> if not recorded.</param>
/// <param name="OverallWidth">The overall width. <see langword="null"/> if not recorded.</param>
/// <param name="OverallHeight">The overall height. <see langword="null"/> if not recorded.</param>
/// <param name="Mass">The component's own mass, where the source publishes one. <see langword="null"/> otherwise.</param>
public sealed record ComponentDimensions(
    ReferenceValue<Length>? BoreDiameter = null,
    ReferenceValue<Length>? OutsideDiameter = null,
    ReferenceValue<Length>? OverallLength = null,
    ReferenceValue<Length>? OverallWidth = null,
    ReferenceValue<Length>? OverallHeight = null,
    ReferenceValue<Mass>? Mass = null)
{
    /// <summary>Whether any dimension at all is recorded.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsRecorded =>
        BoreDiameter is not null || OutsideDiameter is not null || OverallLength is not null
        || OverallWidth is not null || OverallHeight is not null || Mass is not null;
}
