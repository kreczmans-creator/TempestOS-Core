using Tempest.Core.EngineeringIntelligence;
using Tempest.Core.Materials;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// A small seed set of engineering rules, split cleanly between rules
/// derived from a source and rules Tempest Design Engineering authored.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rules are the most dangerous thing to populate carelessly.</b> A
/// wrong material property is a wrong number a reviewer can catch against
/// its own source. A wrong rule is a piece of engineering judgement the
/// platform will apply to every design that passes through it, and it will
/// read as authority. So this dataset holds only two kinds of rule:
/// restatements of a limit a named source published, and rules Tempest
/// itself wrote and owns. There is no third category of remembered
/// engineering wisdom, because there is no way to review one.
/// </para>
/// <para>
/// <b>The supplier-derived rules are scoped to their supplier.</b>
/// "Machining cannot exceed 559 mm" is false; "this supplier's factory
/// milling envelope does not exceed 559 mm" is true, and the applicability
/// conditions say which is meant. A rule that quietly generalises a
/// commercial limit into a physical one is how a platform starts giving
/// confidently wrong advice.
/// </para>
/// <para>
/// <b>Every rule here needs a person before it can bind anything.</b>
/// <see cref="RuleCatalog"/> reads only released rules, and nothing seeded
/// reaches <see cref="ReferenceValidationState.Released"/> without a named
/// reviewer. That is the intended shape: the platform may hold engineering
/// knowledge without a person, but it may not act on it without one.
/// </para>
/// </remarks>
public sealed class RuleSeed : IReferenceSeed<RuleDefinition>
{
    /// <summary>The identity of the rule bounding the milling envelope.</summary>
    public const string MillingEnvelope = "rule-mfg-milling-envelope";

    /// <summary>The identity of the rule bounding unmarked machining tolerance.</summary>
    public const string MachiningGeneralTolerance = "rule-mfg-general-tolerance";

    /// <summary>The identity of the rule bounding sheet metal thickness.</summary>
    public const string SheetMetalThickness = "rule-mfg-sheet-thickness";

    /// <summary>The identity of the rule requiring a cited standard on a material.</summary>
    public const string MaterialMustCiteAStandard = "rule-tde-material-standard";

    /// <summary>The identity of the rule requiring a stated condition on a strength value.</summary>
    public const string StrengthNeedsACondition = "rule-tde-strength-condition";

    /// <summary>The single instance of this dataset.</summary>
    public static RuleSeed Instance { get; } = new();

    private RuleSeed()
    {
    }

    /// <inheritdoc />
    public string DatasetName => "Seed engineering rules — supplier-derived limits and Tempest-authored practice";

    /// <inheritdoc />
    public int DatasetRevision => 1;

    /// <inheritdoc />
    public IReadOnlyList<ReferenceSeedRecord<RuleDefinition>> Records { get; } =
    [
        new(MillingEnvelope,
            new RuleDefinition
            {
                Code = "MFG-CAP-001",
                Name = "Factory CNC milling envelope",
                Statement = "A part whose largest dimension exceeds 559 mm cannot be produced by Proto Labs' "
                    + "factory CNC milling service and must be routed elsewhere.",
                Severity = RuleSeverity.Constraint,
                Domain = RuleDomain.Manufacturing,
                Applicability = new RuleApplicability(
                    SubjectKinds: [AssessmentSubjectKinds.Process],
                    Conditions: "Applies only to Proto Labs' factory CNC milling service. Another supplier, and "
                        + "this supplier's own network route, have different envelopes; the network milling "
                        + "route reaches 650 mm."),
                Condition = new QuantityComparisonExpression(
                    SubjectPropertyNames.MaximumPartSize,
                    QuantityComparator.AtMost,
                    RuleThreshold.FromValue(new ReferenceQuantityValue(
                        new Quantity<Length>(559.0, LengthUnits.Millimetre),
                        ReferenceValueOrigin.ManufacturerCatalogue,
                        "Largest single dimension of the published factory milling envelope."))),
                Rationale = "The machine envelope is a hard physical limit of the equipment the service runs. "
                    + "Discovering it at quotation costs a design iteration.",
                Consequence = "The part cannot be quoted through this route and the enquiry is returned.",
                SourceClassification = "Supplier capability limit",
                Notes = "Restates one supplier's published capability. It is not a statement about CNC milling.",
            },
            SeedSources.Protolabs("Online CNC Machining Service | Get a Quote", "Maximum part size, CNC milling factory")),

        new(MachiningGeneralTolerance,
            new RuleDefinition
            {
                Code = "MFG-CAP-002",
                Name = "Unmarked machined dimensions carry the general tolerance",
                Statement = "A machined dimension not individually toleranced on the drawing is held only to "
                    + "ISO 2768-1-1989-f, which for this supplier means +/- 0.127 mm. A feature needing better "
                    + "than that must be toleranced explicitly.",
                Severity = RuleSeverity.Requirement,
                Domain = RuleDomain.Tolerances,
                Applicability = new RuleApplicability(
                    SubjectKinds: [AssessmentSubjectKinds.Process],
                    Conditions: "Applies to Proto Labs' factory CNC machining services. The tolerance figure is "
                        + "this supplier's; the principle that unmarked dimensions fall to a general tolerance "
                        + "class is general."),
                Condition = new QuantityComparisonExpression(
                    SubjectPropertyNames.CoarsestAchievableTolerance,
                    QuantityComparator.AtMost,
                    RuleThreshold.FromValue(new ReferenceQuantityValue(
                        new Quantity<Length>(0.127, LengthUnits.Millimetre),
                        ReferenceValueOrigin.ManufacturerCatalogue,
                        "General linear tolerance applied where no drawing tolerance is given."))),
                Rationale = "A designer who assumes a tighter default gets parts that measure correctly against "
                    + "the supplier's contract and wrongly against the intent.",
                Consequence = "Features silently manufactured to a looser tolerance than the design assumed.",
                Standards =
                [
                    new StandardReference("ISO 2768-1", StandardSeed.Iso2768Part1, "ISO", "1989",
                        "The general tolerance class the supplier applies"),
                ],
                SourceClassification = "Supplier capability limit",
            },
            SeedSources.Protolabs("Online CNC Machining Service | Get a Quote", "Dimensional tolerances, CNC milling")),

        new(SheetMetalThickness,
            new RuleDefinition
            {
                Code = "MFG-CAP-003",
                Name = "Sheet metal fabrication thickness range",
                Statement = "Sheet metal fabricated by this supplier must be between 0.61 mm and 6.35 mm thick. "
                    + "The faster three-day route is further limited to 3.175 mm and under.",
                Severity = RuleSeverity.Constraint,
                Domain = RuleDomain.Manufacturing,
                Applicability = new RuleApplicability(
                    SubjectKinds: [AssessmentSubjectKinds.Process],
                    Conditions: "Applies only to Proto Labs' sheet metal fabrication service."),
                Condition = new QuantityComparisonExpression(
                    SubjectPropertyNames.MinimumWallThickness,
                    QuantityComparator.AtLeast,
                    RuleThreshold.FromValue(new ReferenceQuantityValue(
                        new Quantity<Length>(0.61, LengthUnits.Millimetre),
                        ReferenceValueOrigin.ManufacturerCatalogue,
                        "Thinnest material the five-day fabrication route accepts."))),
                Rationale = "Material outside the range cannot be formed on the supplier's press brakes.",
                Consequence = "The part cannot be fabricated by this route.",
                SourceClassification = "Supplier capability limit",
                Notes = "The upper bound of 6.35 mm is stated by the source but is not expressed in the machine "
                    + "condition: the condition tests one comparison, and the lower bound is the one a thin "
                    + "sheet design trips. The full range is in the statement, where a reader sees it.",
            },
            SeedSources.Protolabs("Online Custom Sheet Metal Fabrication Service", "Material thickness range")),

        // --- Rules Tempest Design Engineering authored and owns ---
        new(MaterialMustCiteAStandard,
            new RuleDefinition
            {
                Code = "TDE-DES-001",
                Name = "A material used in a released design cites the standard its properties come from",
                Statement = "Every material selected for a released design must cite the standard or "
                    + "specification its mechanical properties are stated against, so that a later reader can "
                    + "check the figures the design relied on.",
                Severity = RuleSeverity.Requirement,
                Domain = RuleDomain.Materials,
                Applicability = new RuleApplicability(SubjectKinds: [AssessmentSubjectKinds.Material]),
                Condition = new PropertyRecordedExpression(MaterialPropertyNames.YieldStrength),
                Rationale = "A property with no cited source cannot be checked, cannot be re-derived when the "
                    + "standard is revised, and cannot be defended if the design is ever questioned.",
                Consequence = "A design whose material basis cannot be reconstructed after the fact.",
                RequiresHumanReview = true,
                SourceClassification = "Tempest Design Engineering practice",
                Notes = "AUTHORED by Tempest Design Engineering. Not a restatement of any external document. "
                    + "The machine condition tests only that a yield strength is recorded, which is weaker than "
                    + "the statement: whether a citation is adequate is a judgement, which is why the rule also "
                    + "demands human review rather than pretending the check is automatable.",
            },
            SeedSources.TempestAuthored(
                "Reference data practice",
                "Adopted during the population phase as the first authored rule in the library.")),

        new(StrengthNeedsACondition,
            new RuleDefinition
            {
                Code = "TDE-DES-002",
                Name = "A strength value is meaningless without the condition it holds under",
                Statement = "A material strength relied on in a calculation must carry the product form, size "
                    + "band, temper or temperature it was published for. A figure quoted without its condition "
                    + "must not be used.",
                Severity = RuleSeverity.Requirement,
                Domain = RuleDomain.Materials,
                Applicability = new RuleApplicability(SubjectKinds: [AssessmentSubjectKinds.Material]),
                Condition = new PropertyRecordedExpression(MaterialPropertyNames.UltimateTensileStrength),
                Rationale = "The seed corpus itself demonstrates the problem: 6082-T6 has four different "
                    + "published proof stresses depending on bar diameter, and CW004A's tensile strength spans "
                    + "200 to 360 MPa across tempers. A single number carries no warning that it was one row of "
                    + "a table.",
                Consequence = "A calculation performed against a strength the delivered material does not have.",
                RequiresHumanReview = true,
                SourceClassification = "Tempest Design Engineering practice",
                Notes = "AUTHORED by Tempest Design Engineering. As with TDE-DES-001, the machine condition is "
                    + "only a presence check; judging whether a stated condition matches the application is a "
                    + "person's job and the rule says so.",
            },
            SeedSources.TempestAuthored(
                "Reference data practice",
                "Adopted during the population phase, prompted by the size-banded and temper-banded values "
                + "found while transcribing the material datasheets.")),
    ];
}
