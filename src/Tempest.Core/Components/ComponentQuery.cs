using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Components;

/// <summary>A deterministic reference-data filter over the component catalogue.</summary>
/// <remarks>
/// Every criterion is a predicate, every unset criterion matches
/// everything, criteria combine with AND, and results come back in
/// ascending record-Id order — the same contract every Group A library
/// offers. A component that does not record the value a bound filters on
/// does not match it: an unrecorded value is never read as zero.
/// <para>
/// <b>A filter, not a selector.</b> Narrowing candidates by what they
/// record is not choosing a spring for a load or a gear for a ratio, and
/// this query offers no criterion that would require it to be.
/// </para>
/// </remarks>
public sealed record ComponentQuery
{
    /// <summary>Matches any component whose designation contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? DesignationContains { get; init; }

    /// <summary>Matches <see cref="ComponentDefinition.Manufacturer"/> exactly, ignoring case. <see langword="null"/> to match any.</summary>
    public string? Manufacturer { get; init; }

    /// <summary>Matches any of these families. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ComponentFamily> Families { get; init; } = [];

    /// <summary>Matches any component whose family is in one of these groups — "every spring", without enumerating six families. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ComponentGroup> Groups { get; init; } = [];

    /// <summary>Matches any of these validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];

    /// <summary>Matches any component made from this registered material. <see langword="null"/> to match any.</summary>
    public string? MaterialId { get; init; }

    /// <summary>Matches any component citing a standard whose designation contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? CitesStandardContaining { get; init; }

    /// <summary>Inclusive lower bound on the bore diameter. <see langword="null"/> for no lower bound.</summary>
    public Quantity<Length>? BoreDiameterMinimum { get; init; }

    /// <summary>Inclusive upper bound on the bore diameter. <see langword="null"/> for no upper bound.</summary>
    public Quantity<Length>? BoreDiameterMaximum { get; init; }

    /// <summary>Inclusive lower bound on the outside diameter. <see langword="null"/> for no lower bound.</summary>
    public Quantity<Length>? OutsideDiameterMinimum { get; init; }

    /// <summary>Inclusive upper bound on the outside diameter. <see langword="null"/> for no upper bound.</summary>
    public Quantity<Length>? OutsideDiameterMaximum { get; init; }

    /// <summary>Inclusive lower bound on a spring's own translational rate. A component with no such rate never matches. <see langword="null"/> for no lower bound.</summary>
    public Quantity<Stiffness>? SpringRateMinimum { get; init; }

    /// <summary>Inclusive upper bound on a spring's own translational rate. A component with no such rate never matches. <see langword="null"/> for no upper bound.</summary>
    public Quantity<Stiffness>? SpringRateMaximum { get; init; }

    /// <summary>Inclusive lower bound on a spring's own free length. <see langword="null"/> for no lower bound.</summary>
    public Quantity<Length>? FreeLengthMinimum { get; init; }

    /// <summary>Inclusive upper bound on a spring's own free length. <see langword="null"/> for no upper bound.</summary>
    public Quantity<Length>? FreeLengthMaximum { get; init; }

    /// <summary>Matches a gear's own tooth count exactly. A component with no gear detail never matches. <see langword="null"/> to match any.</summary>
    public int? NumberOfTeeth { get; init; }

    /// <summary>Inclusive lower bound on a gear's own module. <see langword="null"/> for no lower bound.</summary>
    public Quantity<Length>? ModuleMinimum { get; init; }

    /// <summary>Inclusive upper bound on a gear's own module. <see langword="null"/> for no upper bound.</summary>
    public Quantity<Length>? ModuleMaximum { get; init; }

    /// <summary>Matches <see cref="GearDetail.HelixHand"/>. A component with no gear detail never matches. <see langword="null"/> to match any.</summary>
    public GearHelixHand? HelixHand { get; init; }

    /// <summary>Matches <see cref="DriveElementDetail.ProfileDesignation"/> exactly, ignoring case. <see langword="null"/> to match any.</summary>
    public string? DriveProfileDesignation { get; init; }

    /// <summary>Inclusive lower bound on the rated torque. <see langword="null"/> for no lower bound.</summary>
    public Quantity<Torque>? RatedTorqueMinimum { get; init; }

    /// <summary>Inclusive lower bound on the maximum rotational speed. <see langword="null"/> for no lower bound.</summary>
    public Quantity<RotationalSpeed>? MaximumSpeedMinimum { get; init; }
}
