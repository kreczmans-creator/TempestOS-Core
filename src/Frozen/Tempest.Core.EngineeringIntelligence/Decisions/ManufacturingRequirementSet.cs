using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.EngineeringIntelligence.Decisions;

/// <summary>
/// What a part needs made, stated as the reasoning inputs a manufacturing
/// decision actually turns on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Typed, not a property bag.</b> Every field here is a real
/// engineering quantity in the platform's own units framework, or a
/// reference into `A1`. A dictionary of strings would make the whole
/// decision untypecheckable and would let a caller pass a tolerance in the
/// wrong unit without anything noticing.
/// </para>
/// <para>
/// Every field is optional, and an absent one means the engineer has not
/// stated it. Screening treats an unstated requirement as one that cannot
/// eliminate anything, and reports which requirements were and were not
/// stated — rather than assuming a default that would silently narrow the
/// candidate set.
/// </para>
/// </remarks>
public sealed record ManufacturingRequirementSet
{
    /// <summary>What is being made, in the engineer's own words. Required.</summary>
    public required string PartDescription { get; init; }

    /// <summary>The `A1` material the part is to be made from, where one has been chosen. <see langword="null"/> if not yet chosen.</summary>
    public string? MaterialId { get; init; }

    /// <summary>The material's own family, where the material has been chosen or the family has been. <see langword="null"/> if neither.</summary>
    public MaterialFamily? MaterialFamily { get; init; }

    /// <summary>The tightest tolerance the part needs held. <see langword="null"/> if not stated.</summary>
    public Quantity<Length>? RequiredTolerance { get; init; }

    /// <summary>The smoothest surface the part needs. <see langword="null"/> if not stated.</summary>
    public Quantity<Length>? RequiredSurfaceRoughness { get; init; }

    /// <summary>The part's largest dimension. <see langword="null"/> if not stated.</summary>
    public Quantity<Length>? LargestDimension { get; init; }

    /// <summary>The part's thinnest wall, where it has one. <see langword="null"/> if not stated or not applicable.</summary>
    public Quantity<Length>? ThinnestWall { get; init; }

    /// <summary>The part's mass. <see langword="null"/> if not stated.</summary>
    public Quantity<Mass>? PartMass { get; init; }

    /// <summary>The smallest feature the part carries. <see langword="null"/> if not stated.</summary>
    public Quantity<Length>? SmallestFeature { get; init; }

    /// <summary>The production scale the part is to be made at. <see langword="null"/> if not stated.</summary>
    public Manufacturing.ProductionScale? ProductionScale { get; init; }

    /// <summary>Further requirements the structured fields cannot express, in the engineer's own words. <see langword="null"/> if none.</summary>
    public string? AdditionalRequirements { get; init; }

    /// <summary>
    /// Whether a candidate must be a Released `A7` record. Defaults to
    /// <see langword="true"/>, for the same reason material selection does:
    /// screening against unverified capability bands produces a conclusion
    /// nobody can stand behind.
    /// </summary>
    public bool RequireReleasedProcesses { get; init; } = true;

    /// <summary>Which requirements were actually stated — what the screening could and could not test against.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<string> StatedRequirements
    {
        get
        {
            var stated = new List<string>();

            if (MaterialFamily is { } family) stated.Add($"material family {family}");
            if (RequiredTolerance is not null) stated.Add("tolerance");
            if (RequiredSurfaceRoughness is not null) stated.Add("surface roughness");
            if (LargestDimension is not null) stated.Add("largest dimension");
            if (ThinnestWall is not null) stated.Add("wall thickness");
            if (PartMass is not null) stated.Add("part mass");
            if (SmallestFeature is not null) stated.Add("smallest feature");
            if (ProductionScale is not null) stated.Add("production scale");

            return stated;
        }
    }
}
