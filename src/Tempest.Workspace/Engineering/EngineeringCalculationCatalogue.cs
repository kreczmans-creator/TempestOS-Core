using Tempest.Core.Calculations;

namespace Tempest.Workspace.Engineering;

/// <summary>
/// What the product can calculate, and honestly which of it this surface can
/// actually drive today.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read from the definitions themselves.</b> Each entry's name and
/// description come from that definition's own
/// <see cref="CalculationMetadata"/>, reached by constructing the definition
/// — a small, stateless, immutable type — exactly as
/// <c>CalculationsWorkspaceRegistration.RegisterRepresentativeTemplates</c>
/// already does. Nothing is restated here, so a renamed calculation cannot
/// drift out of step with its own catalogue entry.
/// </para>
/// <para>
/// <b>Every entry says whether this surface can run it.</b> Five product
/// calculations are registered with the engine and all five genuinely
/// compute, but only the bracket section check has a governed entry point
/// that resolves its inputs from a released reference
/// (<see cref="GovernedBracketCheckService"/>). The others need an input
/// document this surface has no form for. Offering them as though they were
/// ready would be the defect `TD-159` already produced once — a template
/// offered and an execution that throws — so they are listed, and listed as
/// not yet drivable here.
/// </para>
/// </remarks>
public static class EngineeringCalculationCatalogue
{
    /// <summary>Why a calculation cannot be driven from this surface yet.</summary>
    public const string NotDrivableHere =
        "Registered and computable, but this workspace has no input form for it yet — "
        + "it needs a governed entry point that resolves its inputs from released reference data, "
        + "which only the bracket section check has today.";

    /// <summary>Every calculation the product registers, with the bracket check first.</summary>
    /// <returns>The catalogue entries.</returns>
    public static IReadOnlyList<CalculationCatalogueEntry> All()
    {
        var bracket = new BracketSectionCheckCalculationDefinition();

        var entries = new List<CalculationCatalogueEntry>
        {
            new(BracketSectionCheckCalculationDefinition.Id, bracket.Metadata.Name, bracket.Metadata.Description ?? string.Empty, true, null),
        };

        entries.AddRange(Describe(new BoltShearCapacityCalculationDefinition().Metadata, BoltShearCapacityCalculationDefinition.Id));
        entries.AddRange(Describe(new BeamBendingStressCalculationDefinition().Metadata, BeamBendingStressCalculationDefinition.Id));
        entries.AddRange(Describe(new BearingLoadCapacityCalculationDefinition().Metadata, BearingLoadCapacityCalculationDefinition.Id));
        entries.AddRange(Describe(new PressureVesselWallThicknessCalculationDefinition().Metadata, PressureVesselWallThicknessCalculationDefinition.Id));
        entries.AddRange(Describe(new MaterialSelectionMarginCalculationDefinition().Metadata, MaterialSelectionMarginCalculationDefinition.Id));

        return entries;
    }

    /// <summary>The entry for <paramref name="calculationId"/>, or <see langword="null"/> where the product registers no such calculation.</summary>
    /// <param name="calculationId">The calculation to describe.</param>
    public static CalculationCatalogueEntry? For(string calculationId) =>
        All().FirstOrDefault(e => string.Equals(e.CalculationId, calculationId, StringComparison.Ordinal));

    private static IEnumerable<CalculationCatalogueEntry> Describe(CalculationMetadata metadata, string id) =>
        [new(id, metadata.Name, metadata.Description ?? string.Empty, false, NotDrivableHere)];
}

/// <summary>One calculation the product registers, as a surface should offer it.</summary>
/// <param name="CalculationId">The Id the engine knows it by.</param>
/// <param name="Name">Its own name, from its metadata.</param>
/// <param name="Description">Its own description, from its metadata.</param>
/// <param name="IsDrivableHere">Whether this workspace can actually run it.</param>
/// <param name="WhyNotDrivable">Why not, where it cannot. <see langword="null"/> where it can.</param>
public sealed record CalculationCatalogueEntry(
    string CalculationId,
    string Name,
    string Description,
    bool IsDrivableHere,
    string? WhyNotDrivable)
{
    /// <summary>The label a picker should show.</summary>
    public string Label => IsDrivableHere ? Name : $"{Name} — not available here yet";

    /// <inheritdoc />
    public override string ToString() => Label;
}
