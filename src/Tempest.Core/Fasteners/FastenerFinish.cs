using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Fasteners;

/// <summary>The surface finish, coating or plating a source stated for a fastener.</summary>
/// <remarks>
/// The designation is kept as the source wrote it. A3 does not classify
/// coatings into a taxonomy of its own: coating designations are governed
/// by the standards that define them, and a TempestOS-invented
/// classification laid over them would be a manufacturer-specific or
/// invented vocabulary presented as a universal one.
/// </remarks>
/// <param name="Designation">The coating or finish designation as the source writes it. Required.</param>
/// <param name="Standard">The standard the finish is specified against, where the source cites one. <see langword="null"/> otherwise.</param>
/// <param name="ThicknessRange">The coating thickness range the source stated. <see langword="null"/> if none was given.</param>
/// <param name="Notes">Anything else the source said about the finish, verbatim. <see langword="null"/> if none.</param>
public sealed record FastenerFinish(
    string Designation,
    StandardReference? Standard = null,
    ReferenceRange<Length>? ThicknessRange = null,
    string? Notes = null)
{
    /// <summary>The coating or finish designation as the source writes it.</summary>
    public string Designation { get; } = string.IsNullOrWhiteSpace(Designation)
        ? throw new ArgumentException("A fastener finish must carry a designation.", nameof(Designation))
        : Designation.Trim();
}
