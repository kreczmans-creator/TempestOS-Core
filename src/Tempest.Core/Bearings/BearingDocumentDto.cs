namespace Tempest.Core.Bearings;

/// <summary>
/// The plain, JSON-serialisable shape a bearing record is stored as —
/// this is the <see cref="EngineeringData.IDocumentRevision.Content"/> of
/// its own backing <see cref="EngineeringData.IEngineeringDocument"/>.
/// </summary>
/// <remarks>
/// <para>
/// A thin envelope around <see cref="BearingDefinition"/> itself, rather
/// than a parallel DTO graph mirroring every one of its ~15 nested types
/// (`ADR-0124`). Unlike <see cref="Materials.MaterialSpecificationDto"/>,
/// which needs <see cref="Materials.MaterialPropertyValueCodec"/> because
/// a material property's own value is a boxed
/// <c>Quantity&lt;TDimension&gt;</c> that <c>System.Text.Json</c> cannot
/// recover the closed generic of, every dimensioned value on a bearing is
/// declared at a statically-known dimension. That makes the canonical
/// types directly serialisable, and a hand-written parallel graph would
/// add only the opportunity for the two shapes to drift apart.
/// </para>
/// <para>
/// Enums are written as strings (see
/// <see cref="BearingSerialisation.Options"/>), so adding or reordering an
/// enum member can never silently reinterpret already-stored records.
/// </para>
/// </remarks>
internal sealed record BearingDocumentDto(
    string BearingId,
    BearingDefinition Definition,
    BearingValidationState ValidationState,
    string? SupersededByBearingId);
