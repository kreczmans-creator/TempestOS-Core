using Tempest.Core.Knowledge;
using Tempest.Core.Knowledge.Academy;
using Tempest.Core.Knowledge.Challenges;
using Tempest.Core.Knowledge.Prompts;
using Tempest.Core.Knowledge.WorkedExamples;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// A seed knowledge corpus built on the engineering data this repository
/// actually holds.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every item teaches something the corpus can demonstrate.</b> The
/// lesson, the challenge and the worked example are all about the same
/// thing — that a material strength is meaningless without the product
/// form and size band it was published for — because that is the lesson
/// the real datasheets in this repository teach the moment you read them.
/// 6082-T6 has four different published proof stresses; a student can be
/// shown the record rather than told a story.
/// </para>
/// <para>
/// <b>The failure and lessons library is deliberately left empty.</b>
/// Populating it would mean either writing up a real company's failure,
/// which is somebody else's story to tell and often somebody's litigation,
/// or inventing one, which would put fiction into the library engineers
/// would most expect to be true. Neither is acceptable, so it stays empty
/// and the deferred-datasets record says why.
/// </para>
/// <para>
/// <b>The prompts do not run.</b> They are text with declared inputs,
/// declared outputs and declared failure modes, held as records. Nothing
/// here executes a prompt, calls a model or orchestrates an agent.
/// </para>
/// </remarks>
public sealed class KnowledgeSeed
{
    /// <summary>The identity of the material-basis review prompt.</summary>
    public const string ReviewPromptRecordId = "prm-material-basis-review";

    /// <summary>The identity of the datasheet extraction prompt.</summary>
    public const string ExtractionPromptRecordId = "prm-datasheet-extraction";

    /// <summary>The identity of the material properties lesson.</summary>
    public const string LessonNodeRecordId = "acd-material-property-conditions";

    /// <summary>The identity of the size-band challenge.</summary>
    public const string ChallengeRecordId = "chl-bar-size-band";

    /// <summary>The identity of the worked example.</summary>
    public const string WorkedExampleRecordId = "wex-bracket-proof-stress-margin";

    private const string LessonReference = "ACD-MAT-COND-001";

    private static readonly KnowledgeProvenance Authored = new()
    {
        Origin = KnowledgeOrigin.Authored,
        ReviewState = KnowledgeReviewState.Unreviewed,
    };

    /// <summary>The prompt dataset.</summary>
    public static IReferenceSeed<PromptRecord> Prompts { get; } = new Seed<PromptRecord>("Prompts",
    [
        new(ReviewPromptRecordId,
            new PromptRecord
            {
                Reference = "PRM-REV-001",
                Name = "Check a calculation's material basis",
                Instruction = "You are reviewing an engineering calculation. Read the material record it cites, "
                    + "including the conditions attached to each property. Report, for each material property "
                    + "the calculation relies on: the value used, the value the record states, the conditions "
                    + "the record attaches to it, and whether those conditions match the product form and size "
                    + "the design actually specifies. Do not judge whether the calculation is correct. Do not "
                    + "supply a value the record does not state.",
                Purpose = PromptPurpose.Checking,
                TaskDescription = "Comparing a calculation's stated material basis against the reference record "
                    + "it pins.",
                HumanReviewGuidance = "Treat the output as a list of things to look at, never as a verdict. A "
                    + "clean report means nothing was found by this check, not that the calculation is right.",
                KnownFailureModes =
                [
                    "Reads the headline property value and ignores the conditions attached to it, which is "
                    + "precisely the error the prompt exists to catch.",
                    "Fills a gap with a remembered typical value when the record states none.",
                    "Treats a specification minimum as though it were a measured property of the delivered "
                    + "material.",
                ],
                Provenance = Authored,
                Notes = "AUTHORED. Held as text. Nothing in this platform executes it.",
            },
            SeedSources.TempestAuthored("Prompt library", "Written against the material records this corpus holds.")),

        new(ExtractionPromptRecordId,
            new PromptRecord
            {
                Reference = "PRM-EXT-001",
                Name = "Extract properties from a supplier datasheet",
                Instruction = "Read the supplied datasheet. For every mechanical and physical property, report "
                    + "the value exactly as printed, its unit exactly as printed, the product form and size "
                    + "band it applies to, and whether the source labels it a minimum, a maximum, a range or a "
                    + "typical value. Where the datasheet gives no value for a property, say so. Never convert "
                    + "a unit. Never supply a value the datasheet does not print, and never correct one that "
                    + "looks wrong — report it as printed and flag it.",
                Purpose = PromptPurpose.Extraction,
                TaskDescription = "First-pass transcription of a supplier datasheet into structured properties, "
                    + "for a person to check.",
                HumanReviewGuidance = "Every extracted value must be checked against the source before the "
                    + "record leaves Draft. Extraction is not verification.",
                KnownFailureModes =
                [
                    "Silently normalises units, destroying the faithful transcription.",
                    "Corrects an implausible printed value instead of flagging it — the 5083 record in this "
                    + "corpus exists because a datasheet prints a density of 265 g/cm3, and an extractor that "
                    + "helpfully wrote 2.65 would have hidden the source's error.",
                    "Collapses a size-banded table into a single row.",
                ],
                Provenance = Authored,
                Notes = "AUTHORED. Held as text. Nothing in this platform executes it.",
            },
            SeedSources.TempestAuthored("Prompt library", "Written from the failure modes actually met while transcribing this corpus.")),
    ]);

    /// <summary>The academy dataset.</summary>
    public static IReferenceSeed<AcademyNode> AcademyNodes { get; } = new Seed<AcademyNode>("Academy",
    [
        new(LessonNodeRecordId,
            new AcademyNode
            {
                Reference = LessonReference,
                Title = "A material property without its conditions is not a property",
                Kind = AcademyNodeKind.Lesson,
                Summary = "Published strengths are rows in a table, not constants. The same alloy in the same "
                    + "temper has different specified minima depending on product form and section size, and "
                    + "using the wrong row is one of the easiest ways to design something that fails a check "
                    + "it should have passed — or passes one it should have failed.",
                Outcomes =
                [
                    new LearningOutcome("LO-1",
                        "Given a material record, identify the product form, size band and condition each "
                        + "strength value applies to."),
                    new LearningOutcome("LO-2",
                        "Explain why a single quoted strength for an alloy is insufficient for a design "
                        + "calculation."),
                    new LearningOutcome("LO-3",
                        "Recognise when a published range spans tempers rather than uncertainty."),
                ],
                Activities =
                [
                    new AcademyActivity("ACT-1",
                        "Read the 6082-T6 record in the material library and list every size band its source "
                        + "publishes a different proof stress for.",
                        AcademyActivityKind.Reading),
                    new AcademyActivity("ACT-2",
                        "Work through the bracket proof-stress margin example.",
                        AcademyActivityKind.WorkedExample,
                        WorkedExampleReference: "WEX-MAT-001"),
                    new AcademyActivity("ACT-3",
                        "Attempt the bar size band challenge.",
                        AcademyActivityKind.Problem,
                        ChallengeReference: "CHL-MAT-001"),
                ],
                Provenance = Authored,
            },
            SeedSources.TempestAuthored("Engineering Academy",
                "The first lesson, written from what the seeded material records demonstrate.")),
    ]);

    /// <summary>The challenge dataset.</summary>
    public static IReferenceSeed<EngineeringChallenge> Challenges { get; } = new Seed<EngineeringChallenge>("Challenges",
    [
        new(ChallengeRecordId,
            new EngineeringChallenge
            {
                Reference = "CHL-MAT-001",
                Title = "The bar that got thicker",
                Scenario = "A bracket was designed in 6082-T6, sized against a minimum 0.2% proof stress of "
                    + "260 MPa, and passed its stress check with a margin of 8%. Late in the programme the "
                    + "design changed and the bracket is now machined from 220 mm across-flats bar instead of "
                    + "the 80 mm bar originally assumed. Nobody revisited the calculation, because the material "
                    + "did not change.",
                Question = "Does the bracket still pass? What exactly went wrong in the process, and at what "
                    + "point should it have been caught?",
                Kind = ChallengeKind.WhatIf,
                Difficulty = ChallengeDifficulty.Straightforward,
                GivenAssumptions =
                [
                    "The load, the geometry of the minimum section and the acceptance criterion are unchanged.",
                    "The material grade and temper are unchanged.",
                ],
                DeliberateOmissions =
                [
                    "The actual load and section area are not given, and are not needed: the question is about "
                    + "the basis, not the arithmetic.",
                ],
                PrerequisiteNodeReferences = [LessonReference],
                Guidance = new ChallengeGuidance(
                    "The material record's own conditions state 260 MPa for 20 to 150 mm bar and 200 MPa above "
                    + "200 mm. At 220 mm the specified minimum has fallen by 23%, which more than consumes an "
                    + "8% margin: the bracket no longer passes. Nothing about the material changed; the row of "
                    + "the table that applies changed. A calculation pack that pins the material record and "
                    + "states the size band its value came from makes this visible at the moment the stock "
                    + "size changes.",
                    CommonMistakes:
                    [
                        "Assuming the calculation still holds because the material specification did not change.",
                        "Looking up 6082-T6 and taking the first published proof stress found.",
                    ],
                    WorkedExampleReference: "WEX-MAT-001"),
                Provenance = Authored,
                Notes = "AUTHORED, and fictional as a scenario — no such bracket and no such programme exists. "
                    + "The numbers it turns on are real: they are the published minima on the 6082-T6 record "
                    + "in this repository.",
            },
            SeedSources.TempestAuthored("Challenge library",
                "Written around the size-banded minima on the seeded 6082-T6 record.")),
    ]);

    /// <summary>
    /// The worked example dataset, pinned to the material revision it uses.
    /// </summary>
    /// <param name="aluminiumPin">The pin identifying the 6082-T6 record and revision the example reads.</param>
    /// <returns>The dataset.</returns>
    public static IReferenceSeed<WorkedExample> WorkedExamples(ReferencePin aluminiumPin) =>
        new Seed<WorkedExample>("Worked examples",
        [
            new(WorkedExampleRecordId,
                new WorkedExample
                {
                    Reference = "WEX-MAT-001",
                    Title = "Margin against proof stress, and why the size band decides it",
                    ProblemStatement = "A bracket carries 12 kN in direct tension across a minimum section of "
                        + "60 mm2, machined from 6082-T6 bar. Establish the margin against the material's "
                        + "specified minimum 0.2% proof stress, first for 100 mm bar and then for 220 mm bar.",
                    Inputs =
                    [
                        new WorkedValue("F", "Design load", "12", "kN"),
                        new WorkedValue("A", "Minimum cross-sectional area", "60", "mm2"),
                        new WorkedValue("Rp0.2(100)", "Specified minimum proof stress, 20 to 150 mm bar", "260", "MPa"),
                        new WorkedValue("Rp0.2(220)", "Specified minimum proof stress, 200 to 250 mm bar", "200", "MPa"),
                    ],
                    Assumptions =
                    [
                        "Static, purely axial loading on the minimum section.",
                        "The delivered bar meets the specified minimum for its own size band.",
                    ],
                    MethodSummary = "Direct stress, then margin against the specified minimum proof stress for "
                        + "the size band the bar actually comes from.",
                    Steps =
                    [
                        new WorkedStep("S-1", WorkedStepKind.Calculation, "Direct stress on the minimum section.",
                            Reasoning: "Load divided by area, with the units carried through rather than "
                                + "assumed: 12 kN over 60 mm2 is 12 000 N over 60 mm2.",
                            Expression: "sigma = F / A = 12000 N / 60 mm2",
                            Result: "200 MPa"),
                        new WorkedStep("S-2", WorkedStepKind.Lookup, "Margin for 100 mm bar.",
                            Reasoning: "The 20 to 150 mm band applies, so the specified minimum is 260 MPa.",
                            Expression: "margin = (260 / 200) - 1",
                            Result: "0.30, or 30%",
                            SourcePin: aluminiumPin),
                        new WorkedStep("S-3", WorkedStepKind.Lookup, "Margin for 220 mm bar.",
                            Reasoning: "The 200 to 250 mm band applies, so the specified minimum is 200 MPa — "
                                + "the same alloy, the same temper, a different row of the same table.",
                            Expression: "margin = (200 / 200) - 1",
                            Result: "0.00, or zero margin",
                            SourcePin: aluminiumPin),
                    ],
                    Result = new WorkedValue("margin", "Margin against proof stress", "0.30 at 100 mm bar, 0.00 at 220 mm bar", null),
                    Interpretation = "Changing the stock size, with no change to the material specification, "
                        + "takes the design from a comfortable 30% margin to none at all. The bracket is not "
                        + "borderline because the engineer was careless with the arithmetic; it is borderline "
                        + "because the applicable row of the specification changed underneath a number that "
                        + "was written down once.",
                    Verification = "Not independently checked. The arithmetic is simple enough to repeat by "
                        + "hand, and a reader should.",
                    TeachingPoints =
                    [
                        "The size band is part of the property, not an annotation on it.",
                        "A specified minimum is a floor the supplier must meet, not the strength of the bar in "
                        + "your hand.",
                        "Pinning the reference record and its revision is what lets a later reader see which "
                        + "row was used.",
                    ],
                    CommonMistakes =
                    [
                        "Quoting one proof stress for '6082-T6' as though the temper fully determined it.",
                        "Carrying a margin forward through a stock size change without rechecking the basis.",
                        "Reading the highest published figure because it is the one most often quoted.",
                    ],
                    CalculationPackReference = "TDE-CPK-001",
                    Provenance = Authored,
                    Notes = "AUTHORED. The load and area are illustrative and chosen to make the arithmetic "
                        + "clean; the two proof stresses are real, and are the figures on the 6082-T6 record "
                        + "pinned by the steps that use them.",
                },
                SeedSources.TempestAuthored("Worked example library",
                    "Written to demonstrate a worked example standing on pinned reference data.")),
        ]);

    private sealed class Seed<TDefinition>(string datasetName, IReadOnlyList<ReferenceSeedRecord<TDefinition>> records)
        : IReferenceSeed<TDefinition>
    {
        public string DatasetName { get; } = $"Tempest-authored knowledge seed — {datasetName}";

        public int DatasetRevision => 1;

        public IReadOnlyList<ReferenceSeedRecord<TDefinition>> Records { get; } = records;
    }
}
