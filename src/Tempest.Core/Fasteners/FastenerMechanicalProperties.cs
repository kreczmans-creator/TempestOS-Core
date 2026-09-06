using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Fasteners;

/// <summary>
/// The published mechanical properties of a fastener.
/// </summary>
/// <remarks>
/// <para>
/// <b>Published figures, never derived ones.</b> Every value here is what a
/// source stated. A3 computes nothing: it does not derive a proof load
/// from a proof strength and a stress area, does not infer a class from a
/// strength, and does not fill a missing figure from a related one. Doing
/// any of those would turn reference data into calculation output wearing
/// reference data's clothes.
/// </para>
/// <para>
/// <see cref="PropertyClass"/> is the source's own designation kept
/// verbatim, not a parsed value: a class designation encodes strengths by
/// convention, and reconstructing those numbers here would be deriving
/// data that the source may not have intended.
/// </para>
/// </remarks>
/// <param name="PropertyClass">The property class, grade or strength designation as the source writes it. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="ProofStrength">The published proof strength. <see langword="null"/> if not recorded.</param>
/// <param name="TensileStrength">The published minimum tensile strength. <see langword="null"/> if not recorded.</param>
/// <param name="YieldStrength">The published minimum yield or 0.2% proof stress. <see langword="null"/> if not recorded.</param>
/// <param name="ProofLoad">The published proof load — a force, not a stress. <see langword="null"/> if not recorded.</param>
/// <param name="MinimumBreakingLoad">The published minimum breaking or ultimate tensile load. <see langword="null"/> if not recorded.</param>
/// <param name="Hardness">The published hardness band, on its own scale.</param>
/// <param name="ElongationAfterFracture">The published minimum elongation, as a ratio. <see langword="null"/> if not recorded.</param>
/// <param name="StressArea">The tensile stress area the source published for the thread. <see langword="null"/> if not recorded — never computed from the thread.</param>
public sealed record FastenerMechanicalProperties(
    string? PropertyClass = null,
    ReferenceValue<Pressure>? ProofStrength = null,
    ReferenceValue<Pressure>? TensileStrength = null,
    ReferenceValue<Pressure>? YieldStrength = null,
    ReferenceValue<Force>? ProofLoad = null,
    ReferenceValue<Force>? MinimumBreakingLoad = null,
    FastenerHardness? Hardness = null,
    ReferenceValue<Dimensionless>? ElongationAfterFracture = null,
    ReferenceValue<Area>? StressArea = null)
{
    /// <summary>Whether any mechanical property at all is recorded.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsRecorded =>
        PropertyClass is not null || ProofStrength is not null || TensileStrength is not null
        || YieldStrength is not null || ProofLoad is not null || MinimumBreakingLoad is not null
        || Hardness is not null || ElongationAfterFracture is not null || StressArea is not null;
}
