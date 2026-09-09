using Tempest.Core.Manufacturing;
using Tempest.Core.Materials;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// A seed set of manufacturing processes carrying one supplier's published
/// capability envelope.
/// </summary>
/// <remarks>
/// <para>
/// <b>These are a supplier's capabilities, not a process's limits.</b>
/// "CNC milling can hold ±0.127 mm" is false as a statement about milling
/// and true as a statement about one service bureau's unmarked-dimension
/// policy. The distinction matters enormously to anything reasoning over
/// this data, so each record names the supplier in
/// <see cref="ProcessDefinition.Variant"/>, attributes every figure to that
/// supplier's own catalogue through
/// <see cref="ReferenceValueOrigin.ManufacturerCatalogue"/>, and says in
/// its notes that a different supplier will publish different numbers.
/// Nothing here was generalised into a claim about the process itself.
/// </para>
/// <para>
/// <b>Three processes chosen to disagree.</b> Machining, sheet metal
/// fabrication and injection moulding differ on the axes a real
/// manufacturing decision turns on — tooling cost, viable volume,
/// achievable tolerance, geometry freedom and material family. A decision
/// tree offered only variants of machining has nothing to decide.
/// </para>
/// <para>
/// <b>Material compatibility is recorded at family level, because that is
/// what the source states.</b> The supplier lists "Aluminum" and
/// "Stainless Steel", not grade designations, so each entry names a
/// <see cref="MaterialFamily"/> and leaves
/// <see cref="ProcessMaterialCompatibility.MaterialId"/> unset. Pointing
/// these at the specific registered grades would assert something the
/// supplier never said — that it machines 6082-T6 in particular — and the
/// family is the true and still-resolvable link.
/// </para>
/// </remarks>
public sealed class ProcessSeed : IReferenceSeed<ProcessDefinition>
{
    /// <summary>The identity of the CNC milling capability record.</summary>
    public const string CncMilling = "prc-cnc-milling";

    /// <summary>The identity of the CNC turning capability record.</summary>
    public const string CncTurning = "prc-cnc-turning";

    /// <summary>The identity of the sheet metal fabrication capability record.</summary>
    public const string SheetMetalFabrication = "prc-sheet-metal-fabrication";

    /// <summary>The identity of the injection moulding capability record.</summary>
    public const string InjectionMoulding = "prc-injection-moulding";

    private const string Supplier = "Proto Labs, Inc.";

    private const string SupplierCaveat =
        "Every figure on this record is one supplier's published capability, not a limit of the process. "
        + "Another supplier will publish different numbers for the same process, and a shop with different "
        + "machines may do better or worse. Treat this as evidence about a route to manufacture, never as a "
        + "physical constraint.";

    /// <summary>The single instance of this dataset.</summary>
    public static ProcessSeed Instance { get; } = new();

    private ProcessSeed()
    {
    }

    /// <inheritdoc />
    public string DatasetName => "Manufacturing capability envelopes — Proto Labs published service data";

    /// <inheritdoc />
    public int DatasetRevision => 1;

    /// <inheritdoc />
    public IReadOnlyList<ReferenceSeedRecord<ProcessDefinition>> Records { get; } =
    [
        new(CncMilling,
            new ProcessDefinition
            {
                Family = ProcessFamily.Milling,
                Name = "CNC milling",
                Variant = $"{Supplier} factory service capability",
                Description = "Subtractive machining of a part from solid stock on a multi-axis milling centre.",
                Capabilities = new ProcessCapabilities(
                    AchievableTolerance: new ReferenceRange<Length>(
                        Minimum: new Quantity<Length>(0.0127, LengthUnits.Millimetre),
                        Maximum: new Quantity<Length>(0.127, LengthUnits.Millimetre),
                        Origin: ReferenceValueOrigin.ManufacturerCatalogue,
                        Conditions: "The upper end is the general linear tolerance applied to a part supplied "
                            + "without a technical drawing (+/- 0.005 in.); the lower end is the tighter "
                            + "hole-and-bore tolerance available on individually specified features "
                            + "(+/- 0.0005 in.). Unmarked dimensions are held to ISO 2768-1-1989-f.",
                        SourceDesignation: "Linear tolerance"),
                    PartSize: new ReferenceRange<Length>(
                        Minimum: new Quantity<Length>(1.016, LengthUnits.Millimetre),
                        Maximum: new Quantity<Length>(559.0, LengthUnits.Millimetre),
                        Origin: ReferenceValueOrigin.ManufacturerCatalogue,
                        Conditions: "Largest and smallest single dimension of the factory milling envelope. "
                            + "The full envelope is 559 mm x 356 mm x 95.3 mm at the maximum and "
                            + "6.35 mm x 6.35 mm x 1.016 mm at the minimum, so a part fitting within the "
                            + "extremes quoted here is not thereby guaranteed to fit.",
                        SourceDesignation: "Maximum / minimum part size")),
                MaterialCompatibility =
                [
                    Compatible(MaterialFamily.Aluminium, "Aluminum"),
                    Compatible(MaterialFamily.StainlessSteel, "Stainless Steel"),
                    Compatible(MaterialFamily.Steel, "Steel Alloy; Steel Mild Low Carbon"),
                    Compatible(MaterialFamily.CopperAlloy, "Brass; Copper"),
                    Compatible(MaterialFamily.Titanium, "Titanium"),
                    Compatible(MaterialFamily.Thermoplastic,
                        "ABS; Acetal; CPVC; Delrin; HDPE; Nylon; PEEK; PEI; PET; PMMA; Polycarbonate; "
                        + "Polypropylene; PPSU; PSU; PTFE; PVC"),
                ],
                ProductionScales = [ProductionScale.Prototype, ProductionScale.LowVolume],
                Constraints =
                [
                    new ProcessConstraint(
                        "Quoted lead time is as fast as 1 day for machined prototypes and production parts, and "
                        + "as fast as 4 days through the expanded automated milling route.",
                        ProcessConstraintKind.Economic,
                        ReferenceValueOrigin.ManufacturerCatalogue),
                ],
                Standards =
                [
                    new StandardReference("ISO 2768-1", StandardSeed.Iso2768Part1, "ISO", "1989",
                        "General tolerance class f, applied to dimensions without individual tolerances"),
                ],
                TypicalApplications = "Prototype and low-volume machined parts, brackets, housings and fixtures.",
                SourceClassification = "CNC machining service",
                Notes = SupplierCaveat,
            },
            SeedSources.Protolabs("Online CNC Machining Service | Get a Quote", "CNC milling capability tables")),

        new(CncTurning,
            new ProcessDefinition
            {
                Family = ProcessFamily.Turning,
                Name = "CNC turning",
                Variant = $"{Supplier} factory service capability",
                Description = "Subtractive machining of a rotationally symmetric part from bar stock on a lathe.",
                Capabilities = new ProcessCapabilities(
                    AchievableTolerance: new ReferenceRange<Length>(
                        Minimum: new Quantity<Length>(0.020, LengthUnits.Millimetre),
                        Maximum: new Quantity<Length>(0.13, LengthUnits.Millimetre),
                        Origin: ReferenceValueOrigin.ManufacturerCatalogue,
                        Conditions: "The upper end is the factory turning tolerance (+/- 0.005 in.); the lower "
                            + "end is the tighter tolerance quoted for the network turning route "
                            + "(+/- 0.001 in.). The two are different services, so the range spans routes "
                            + "rather than describing one machine.",
                        SourceDesignation: "Turning tolerance"),
                    PartSize: new ReferenceRange<Length>(
                        Minimum: new Quantity<Length>(1.27, LengthUnits.Millimetre),
                        Maximum: new Quantity<Length>(228.6, LengthUnits.Millimetre),
                        Origin: ReferenceValueOrigin.ManufacturerCatalogue,
                        Conditions: "Factory turning length limits. Diameter is separately limited to between "
                            + "4.07 mm and 100.33 mm, which is the binding constraint on most turned parts.",
                        SourceDesignation: "Part length")),
                MaterialCompatibility =
                [
                    Compatible(MaterialFamily.Aluminium, "Aluminum"),
                    Compatible(MaterialFamily.StainlessSteel, "Stainless Steel"),
                    Compatible(MaterialFamily.Steel, "Steel Alloy; Steel Mild Low Carbon"),
                    Compatible(MaterialFamily.CopperAlloy, "Brass; Copper"),
                ],
                ProductionScales = [ProductionScale.Prototype, ProductionScale.LowVolume],
                Constraints =
                [
                    new ProcessConstraint(
                        "Minimum wall thickness 0.51 mm for factory turning, and 0.5 mm through the network "
                        + "route. Recorded as a constraint rather than as a wall thickness capability because "
                        + "this library's own rules hold that turning does not produce a wall thickness "
                        + "(TEMPEST-MFG-005). The data was moved to fit the rule rather than the rule relaxed "
                        + "to fit the data, but the disagreement is real — a turned tube plainly has a wall — "
                        + "and is left recorded here for the foundation to settle rather than silently "
                        + "resolved by a population run.",
                        ProcessConstraintKind.Geometric,
                        ReferenceValueOrigin.ManufacturerCatalogue),
                ],
                Standards =
                [
                    new StandardReference("ISO 2768-1", StandardSeed.Iso2768Part1, "ISO", "1989",
                        "General tolerance class f, applied to dimensions without individual tolerances"),
                ],
                TypicalApplications = "Shafts, spacers, bushes, pins and other rotationally symmetric parts.",
                SourceClassification = "CNC machining service",
                Notes = SupplierCaveat,
            },
            SeedSources.Protolabs("Online CNC Machining Service | Get a Quote", "CNC turning capability tables")),

        new(SheetMetalFabrication,
            new ProcessDefinition
            {
                Family = ProcessFamily.Bending,
                Name = "Sheet metal fabrication",
                Variant = $"{Supplier} service capability",
                Description = "Cutting and press-brake forming of flat sheet into folded components and assemblies.",
                Capabilities = new ProcessCapabilities(
                    WallThickness: new ReferenceRange<Length>(
                        Minimum: new Quantity<Length>(0.61, LengthUnits.Millimetre),
                        Maximum: new Quantity<Length>(6.35, LengthUnits.Millimetre),
                        Origin: ReferenceValueOrigin.ManufacturerCatalogue,
                        Conditions: "Material thickness range for the five-day route (0.024 in. to 0.250 in.). "
                            + "The faster three-day route is limited to 3.175 mm and under, so choosing the "
                            + "shorter lead time narrows the thickness available.",
                        SourceDesignation: "Material thickness")),
                MaterialCompatibility =
                [
                    Compatible(MaterialFamily.Aluminium, "Aluminum"),
                    Compatible(MaterialFamily.StainlessSteel, "Stainless Steel"),
                    Compatible(MaterialFamily.Steel, "Steel: CR Non-treated; CR Galvanneal; CR Galvanized"),
                    Compatible(MaterialFamily.CopperAlloy, "Brass; Copper"),
                ],
                ProductionScales = [ProductionScale.Prototype, ProductionScale.LowVolume, ProductionScale.MediumVolume],
                Constraints =
                [
                    new ProcessConstraint(
                        "The three-day route accepts up to 12 bends; more than 12 bends requires the five-day "
                        + "route with its more complex forming sequence.",
                        ProcessConstraintKind.Geometric,
                        ReferenceValueOrigin.ManufacturerCatalogue),
                    new ProcessConstraint(
                        "The source states no dimensional or bend tolerance, no maximum part size and no hole "
                        + "size limits. Their absence here is the source's silence, not a claim that the "
                        + "process is unconstrained in those respects.",
                        ProcessConstraintKind.Dimensional,
                        ReferenceValueOrigin.ManufacturerCatalogue),
                ],
                TypicalApplications = "Enclosures, chassis, brackets and panels.",
                SourceClassification = "Sheet metal fabrication service",
                Notes = SupplierCaveat,
            },
            SeedSources.Protolabs("Online Custom Sheet Metal Fabrication Service", "Capability and lead time tables")),

        new(InjectionMoulding,
            new ProcessDefinition
            {
                Family = ProcessFamily.InjectionMoulding,
                Name = "Plastic injection moulding",
                Variant = $"{Supplier} service capability",
                Description = "Injection of molten thermoplastic or thermoset into a machined tool.",
                Capabilities = new ProcessCapabilities(
                    AchievableTolerance: new ReferenceRange<Length>(
                        Minimum: null,
                        Maximum: new Quantity<Length>(0.08, LengthUnits.Millimetre),
                        Origin: ReferenceValueOrigin.ManufacturerCatalogue,
                        Conditions: "Machining tolerance of the tool (+/- 0.003 in.). The moulded part also "
                            + "carries a resin shrinkage tolerance the source states as no better than "
                            + "+/- 0.002 in./in., which is proportional to size and is therefore not a length "
                            + "and not recorded here. A large moulding is governed by that term, not by this "
                            + "one.",
                        SourceDesignation: "Machining tolerance"),
                    PartSize: new ReferenceRange<Length>(
                        Minimum: null,
                        Maximum: new Quantity<Length>(751.0, LengthUnits.Millimetre),
                        Origin: ReferenceValueOrigin.ManufacturerCatalogue,
                        Conditions: "Largest single dimension of the maximum part envelope "
                            + "480 mm x 751 mm x 203 mm. The source states no minimum.",
                        SourceDesignation: "Maximum part size")),
                MaterialCompatibility =
                [
                    Compatible(MaterialFamily.Thermoplastic, "More than 100 thermoplastic materials"),
                    Compatible(MaterialFamily.Thermoset, "More than 100 thermoplastic and thermoset materials"),
                ],
                ProductionScales =
                [
                    ProductionScale.Prototype,
                    ProductionScale.LowVolume,
                    ProductionScale.MediumVolume,
                    ProductionScale.HighVolume,
                ],
                Constraints =
                [
                    new ProcessConstraint(
                        "Requires a tool. The source quotes a mould cost starting at USD 1,495, which is a "
                        + "fixed cost incurred before the first part exists and is what makes this process a "
                        + "volume decision rather than a geometry one.",
                        ProcessConstraintKind.Tooling,
                        ReferenceValueOrigin.ManufacturerCatalogue),
                    new ProcessConstraint(
                        "No minimum order quantity is imposed by this supplier, so the tooling cost is the only "
                        + "barrier to a small run.",
                        ProcessConstraintKind.Economic,
                        ReferenceValueOrigin.ManufacturerCatalogue),
                ],
                TypicalApplications = "Volume plastic components, housings, clips and enclosures.",
                SourceClassification = "Injection moulding service",
                Notes = SupplierCaveat + " The mould cost figure is a price quoted at the time of retrieval and "
                    + "will not stay current; it is recorded as evidence of the shape of the cost, not as a "
                    + "price to quote from.",
            },
            SeedSources.Protolabs("Injection Molding Services for Custom Parts", "Capability, tolerance and cost statements")),
    ];

    private static ProcessMaterialCompatibility Compatible(MaterialFamily family, string designationsAsListed) =>
        new(Family: family,
            Suitability: ProcessMaterialSuitability.Suitable,
            MaterialDesignation: designationsAsListed,
            Origin: ReferenceValueOrigin.ManufacturerCatalogue,
            Conditions: "Listed by the supplier as a material it offers for this service.",
            Notes: "Recorded at family level because the supplier lists material families rather than grades. "
                + "No specific registered grade is asserted to be available.");
}
