namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// Gives an Engineering Object a Bill of Materials line — the Quantity,
/// Unit of Measure, Find Number, Item Number, and Reference Designator
/// describing this object's own usage under its current
/// <see cref="IHasParent.ParentId"/> — a genuine, disclosed `WP 9.0B`
/// extension to the Domain, additive over <see cref="IHasParent"/>
/// exactly as <see cref="IHasParent"/> itself was additive over
/// <see cref="IHasBusinessIdentifier"/> (`ADR-0080`, `ADR-0083`).
/// </summary>
/// <remarks>
/// <para>
/// A BOM line is structural metadata, not document content —
/// <see cref="SetBomLineAsync"/> mutates it in place (mirrors
/// <see cref="IRenamable.RenameAsync"/>'s own identical reasoning) and
/// never creates a new <see cref="IHasRevisions"/> revision.
/// </para>
/// <para>
/// <see cref="UnitOfMeasure"/> is a plain string (<c>"EA"</c>, <c>"M"</c>,
/// <c>"KG"</c>, ...), deliberately never
/// <see cref="Tempest.Core.UnitsAndQuantities.Quantity{TDimension}"/> —
/// that system exists for compile-time-safe calculation dimensional
/// analysis (`ADR-0054`); a BOM count like <c>"EA"</c> is not a physical
/// dimension, and forcing it through an <c>IDimension</c> type family
/// built for Length/Mass/Area would be a category mismatch. See
/// `ADR-0083`.
/// </para>
/// </remarks>
public interface IHasBomLine : IEngineeringObject
{
    /// <summary>Gets how many of this object are used under its current parent. Defaults to <c>1</c> until explicitly set.</summary>
    decimal Quantity { get; }

    /// <summary>Gets the unit <see cref="Quantity"/> is expressed in (for example <c>"EA"</c>, <c>"M"</c>, <c>"KG"</c>), or <see langword="null"/> if not yet set.</summary>
    string? UnitOfMeasure { get; }

    /// <summary>Gets the drawing-callout find number correlating this object to an assembly drawing balloon, or <see langword="null"/> if not yet set.</summary>
    string? FindNumber { get; }

    /// <summary>Gets this object's own BOM line sequence number (for example <c>"10"</c>, <c>"20"</c>), or <see langword="null"/> if not yet set.</summary>
    string? ItemNumber { get; }

    /// <summary>Gets the reference designator(s) (for example <c>"R1, R2"</c>), or <see langword="null"/> if not yet set — chiefly meaningful for electrical/PCB assemblies.</summary>
    string? ReferenceDesignator { get; }

    /// <summary>Sets this object's own BOM line data in place. Never creates a new revision.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quantity"/> is not positive.</exception>
    Task SetBomLineAsync(
        decimal quantity, string? unitOfMeasure = null, string? findNumber = null,
        string? itemNumber = null, string? referenceDesignator = null, CancellationToken cancellationToken = default);
}
