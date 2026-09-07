using Tempest.Core.ReferenceData;

namespace Tempest.Core.Manufacturing;

/// <summary>
/// The canonical engineering description of one manufacturing process:
/// what it is, what sources say it can achieve, what it works on, and what
/// limits it.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is deliberately absent.</b> No process planning, no route
/// generation, no process selection, no cost model, no cycle-time
/// estimation, and no supplier capability of any kind. A7 records what
/// processes exist and what sources published about them; choosing a
/// process for a part is a judgement resting on geometry, volume, cost,
/// lead time and available suppliers that this library holds none of, and
/// does not become able to make by holding more capability bands.
/// </para>
/// <para>
/// <see cref="TypicalApplications"/> deserves the same warning it carries
/// on its own field: it is what a source said the process is used for, not
/// what TempestOS thinks it should be used for.
/// </para>
/// <para>
/// Carries no TempestOS identity, no provenance, no validation state and
/// no revision number — those belong to the registered record
/// (<see cref="IReferenceRecord{TDefinition}"/>).
/// </para>
/// </remarks>
public sealed record ProcessDefinition
{
    /// <summary>The process's own family. Required — this is what makes the rest of the record interpretable.</summary>
    public required ProcessFamily Family { get; init; }

    /// <summary>The process's own name, as the source writes it. Required.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// A qualifier distinguishing this record from another describing the
    /// same process. <see langword="null"/> where none is needed.
    /// </summary>
    /// <remarks>
    /// Two sources legitimately publish different capability bands for the
    /// same named process, and both are real reference data. The variant is
    /// part of the uniqueness key so both can be held, rather than one
    /// silently displacing the other.
    /// </remarks>
    public string? Variant { get; init; }

    /// <summary>
    /// A short summary of the process, in the recorder's own words.
    /// <see langword="null"/> if none was written.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>What sources published that the process can achieve. Never <see langword="null"/>; an all-absent instance means nothing was recorded.</summary>
    public ProcessCapabilities Capabilities { get; init; } = new();

    /// <summary>The materials sources associated with the process, and what they said. Never <see langword="null"/>; empty if none is recorded.</summary>
    public IReadOnlyList<ProcessMaterialCompatibility> MaterialCompatibility { get; init; } = [];

    /// <summary>The production scales sources associated with the process. Never <see langword="null"/>; empty means none was recorded, never that the process suits no scale.</summary>
    public IReadOnlyList<ProductionScale> ProductionScales { get; init; } = [];

    /// <summary>The limitations sources stated. Never <see langword="null"/>; empty if none is recorded.</summary>
    public IReadOnlyList<ProcessConstraint> Constraints { get; init; } = [];

    /// <summary>
    /// What sources say the process is typically used for, verbatim.
    /// <b>What a source said, never TempestOS's own recommendation.</b>
    /// <see langword="null"/> if none was given.
    /// </summary>
    public string? TypicalApplications { get; init; }

    /// <summary>Every standard this record's own information is stated against. Never <see langword="null"/>; empty if none is cited.</summary>
    public IReadOnlyList<StandardReference> Standards { get; init; } = [];

    /// <summary>
    /// The source's own classification wording, verbatim — the honest home
    /// for a process this taxonomy classifies as
    /// <see cref="ProcessFamily.Other"/>. <see langword="null"/> if the
    /// source gave none.
    /// </summary>
    public string? SourceClassification { get; init; }

    /// <summary>Free-text notes not captured by any other field. <see langword="null"/> if none.</summary>
    public string? Notes { get; init; }

    /// <summary>The date from which this record's own values are effective, where the source states one. <see langword="null"/> if it does not.</summary>
    public DateOnly? EffectiveDate { get; init; }

    /// <summary>The broad group this process's own family belongs to.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public ProcessGroup Group => ProcessFamilyTraits.GroupOf(Family);

    /// <summary>The key process identity uniqueness is enforced on.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string IdentityKey => IdentityKeyFor(Family, Name, Variant);

    /// <summary>Builds the uniqueness key from values that are not (yet) a record — the lookup path.</summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty, or whitespace.</exception>
    public static string IdentityKeyFor(ProcessFamily family, string name, string? variant = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return $"{family} {name.Trim()}:{variant?.Trim() ?? string.Empty}".ToUpperInvariant();
    }
}
