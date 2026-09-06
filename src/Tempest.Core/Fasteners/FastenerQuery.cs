using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Fasteners;

/// <summary>A deterministic reference-data filter over the fastener catalogue.</summary>
/// <remarks>
/// Every criterion is a predicate, every unset criterion matches
/// everything, criteria combine with AND, and results come back in
/// ascending record-Id order — the same contract every Group A library
/// offers. Dimensional bounds compare in the base unit, and a fastener
/// that does not record the value a bound filters on does not match it: an
/// unrecorded value is never read as zero.
/// <para>
/// <b>A filter, not a selector.</b> Narrowing candidates by what they
/// record is not choosing a fastener for a joint, and this query offers no
/// criterion that would require it to be.
/// </para>
/// </remarks>
public sealed record FastenerQuery
{
    /// <summary>Matches any fastener whose designation contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? DesignationContains { get; init; }

    /// <summary>Matches <see cref="FastenerDefinition.Manufacturer"/> exactly, ignoring case. <see langword="null"/> to match any.</summary>
    public string? Manufacturer { get; init; }

    /// <summary>Matches any of these families. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<FastenerFamily> Families { get; init; } = [];

    /// <summary>Matches any of these head types. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<FastenerHeadType> HeadTypes { get; init; } = [];

    /// <summary>Matches any of these drive types. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<FastenerDriveType> DriveTypes { get; init; } = [];

    /// <summary>Matches any of these thread systems. An unthreaded fastener never matches. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ThreadSystem> ThreadSystems { get; init; } = [];

    /// <summary>Matches the thread designation exactly, ignoring case. An unthreaded fastener never matches. <see langword="null"/> to match any.</summary>
    public string? ThreadDesignation { get; init; }

    /// <summary>Matches <see cref="ThreadSpecification.Handedness"/>. <see langword="null"/> to match any.</summary>
    public ThreadHandedness? Handedness { get; init; }

    /// <summary>Matches <see cref="FastenerMechanicalProperties.PropertyClass"/> exactly, ignoring case. <see langword="null"/> to match any.</summary>
    public string? PropertyClass { get; init; }

    /// <summary>Matches <see cref="FastenerFinish.Designation"/> exactly, ignoring case. A fastener with no recorded finish never matches. <see langword="null"/> to match any.</summary>
    public string? FinishDesignation { get; init; }

    /// <summary>Matches any fastener made from this registered material. <see langword="null"/> to match any.</summary>
    public string? MaterialId { get; init; }

    /// <summary>Matches any of these validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];

    /// <summary>Matches any fastener citing a standard whose designation contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? CitesStandardContaining { get; init; }

    /// <summary>Inclusive lower bound on the nominal thread diameter. <see langword="null"/> for no lower bound.</summary>
    public Quantity<Length>? NominalDiameterMinimum { get; init; }

    /// <summary>Inclusive upper bound on the nominal thread diameter. <see langword="null"/> for no upper bound.</summary>
    public Quantity<Length>? NominalDiameterMaximum { get; init; }

    /// <summary>Inclusive lower bound on the nominal length. <see langword="null"/> for no lower bound.</summary>
    public Quantity<Length>? NominalLengthMinimum { get; init; }

    /// <summary>Inclusive upper bound on the nominal length. <see langword="null"/> for no upper bound.</summary>
    public Quantity<Length>? NominalLengthMaximum { get; init; }

    /// <summary>Inclusive lower bound on the published width across flats. <see langword="null"/> for no lower bound.</summary>
    public Quantity<Length>? WidthAcrossFlatsMinimum { get; init; }

    /// <summary>Inclusive upper bound on the published width across flats. <see langword="null"/> for no upper bound.</summary>
    public Quantity<Length>? WidthAcrossFlatsMaximum { get; init; }

    /// <summary>Inclusive lower bound on the published proof strength. <see langword="null"/> for no lower bound.</summary>
    public Quantity<Pressure>? ProofStrengthMinimum { get; init; }

    /// <summary>Inclusive lower bound on the published minimum tensile strength. <see langword="null"/> for no lower bound.</summary>
    public Quantity<Pressure>? TensileStrengthMinimum { get; init; }

    /// <summary>Matches any fastener recording at least one published tightening torque. <see langword="null"/> to match any.</summary>
    public bool? RecordsTighteningTorque { get; init; }
}
