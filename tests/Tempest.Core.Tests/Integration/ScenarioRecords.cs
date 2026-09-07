using Tempest.Core.ReferenceData.Seeding.Datasets;

namespace Tempest.Core.Tests.Integration;

/// <summary>
/// The record identities the bracket scenario uses, named once.
/// </summary>
/// <remarks>
/// The scenario was chosen from the data that exists rather than the data
/// being chosen to fit a scenario: these are the records the population
/// phase actually seeded, and the bracket is what they support. A shaft
/// scenario would have needed fatigue data nobody has; a bearing-supported
/// assembly would have needed more than one bearing family.
/// </remarks>
internal static class ScenarioRecords
{
    /// <summary>The requirement the whole scenario hangs from.</summary>
    public const string RequirementIdentifier = "REQ-BRACKET-001";

    /// <summary>
    /// The materials offered to selection. All six seeded grades, because a
    /// selection that is only shown the answer is not a selection.
    /// </summary>
    public static IReadOnlyList<string> CandidateMaterials { get; } =
    [
        MaterialSeed.S355J2,
        MaterialSeed.Stainless1Point4301,
        MaterialSeed.Stainless1Point4404,
        MaterialSeed.Aluminium6082T6,
        MaterialSeed.Aluminium5083OH111,
        MaterialSeed.CopperCw004A,
    ];

    /// <summary>The manufacturing routes offered to the decision.</summary>
    public static IReadOnlyList<string> CandidateProcesses { get; } =
    [
        ProcessSeed.CncMilling,
        ProcessSeed.CncTurning,
        ProcessSeed.SheetMetalFabrication,
        ProcessSeed.InjectionMoulding,
    ];

    /// <summary>The rules that bear on the scenario.</summary>
    public static IReadOnlyList<string> ScenarioRules { get; } =
    [
        RuleSeed.MillingEnvelope,
        RuleSeed.MachiningGeneralTolerance,
        RuleSeed.MaterialMustCiteAStandard,
        RuleSeed.StrengthNeedsACondition,
    ];
}
