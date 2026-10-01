using Tempest.Core.Fasteners;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// The shipped fasteners library: seven geometry-only ISO metric
/// coarse-thread records from the first acquisition, and — from the day-one
/// acquisition (PO decision 2026-10-01) — one record per size and property
/// class for ISO 898-1 classes 4.6 to 12.9 (M3 to M36) and ISO 3506-1
/// A2-70, A4-70 and A4-80 (M3 to M24). See
/// <c>FastenerSeed.PropertyClasses.cs</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The geometry-only records are kept as they were.</b> They name a
/// thread, not a fastener: <see cref="FastenerMechanicalProperties.IsRecorded"/>
/// answers <see langword="false"/> for them, so a consumer that needs
/// strength can detect its absence rather than reading a zero. The first
/// acquisition found no readable restatement of ISO 898-1; the day-one
/// acquisition did (Würth's technical handbook extracts), and the property
/// class records carry those values.
/// </para>
/// </remarks>
public sealed partial class FastenerSeed : IReferenceSeed<FastenerDefinition>
{
    /// <summary>The single instance of this dataset.</summary>
    public static FastenerSeed Instance { get; } = new();

    private FastenerSeed()
    {
    }

    /// <inheritdoc />
    /// <remarks>Shipped reference data under the PO decision of 2026-10-01: released at seed by the host's seeder.</remarks>
    public bool ReleaseAtSeed => true;

    /// <inheritdoc />
    public string DatasetName => "ISO metric coarse thread fasteners — geometry and ISO 898-1 / ISO 3506-1 property classes";

    /// <inheritdoc />
    /// <remarks>Revision 2 adds the property-class records (PO decision 2026-10-01); revision 1 was geometry only.</remarks>
    public int DatasetRevision => 2;

    /// <inheritdoc />
    public IReadOnlyList<ReferenceSeedRecord<FastenerDefinition>> Records { get; } =
    [
        .. GeometryOnly(),
        .. CarbonSteelPropertyClasses(),
        .. StainlessPropertyClasses(),
    ];

    private static IEnumerable<ReferenceSeedRecord<FastenerDefinition>> GeometryOnly() =>
    [
        Bolt("fst-m5-coarse", 5.0, 0.8),
        Bolt("fst-m6-coarse", 6.0, 1.0),
        Bolt("fst-m8-coarse", 8.0, 1.25),
        Bolt("fst-m10-coarse", 10.0, 1.5),
        Bolt("fst-m12-coarse", 12.0, 1.75),
        Bolt("fst-m16-coarse", 16.0, 2.0),
        Bolt("fst-m20-coarse", 20.0, 2.5),
    ];

    private static ReferenceSeedRecord<FastenerDefinition> Bolt(string recordId, double diameterMillimetres, double pitchMillimetres)
    {
        var designation = $"M{Format(diameterMillimetres)}";
        var threadDesignation = $"{designation} x {Format(pitchMillimetres)}";

        return new ReferenceSeedRecord<FastenerDefinition>(
            recordId,
            new FastenerDefinition
            {
                Family = FastenerFamily.Bolt,
                Designation = threadDesignation,
                HeadType = FastenerHeadType.Hexagon,
                Thread = new ThreadSpecification(
                    threadDesignation,
                    ThreadSystem.MetricCoarse,
                    NominalDiameter: new ReferenceValue<Length>(
                        new Quantity<Length>(diameterMillimetres, LengthUnits.Millimetre),
                        ReferenceValueOrigin.Standard,
                        "Nominal (major) diameter of the selected coarse-thread size.",
                        "d"),
                    Pitch: new ReferenceValue<Length>(
                        new Quantity<Length>(pitchMillimetres, LengthUnits.Millimetre),
                        ReferenceValueOrigin.Standard,
                        "Coarse pitch for this diameter, from the selected-sizes table.",
                        "P"),
                    Handedness: ThreadHandedness.RightHand),
                Standards =
                [
                    new StandardReference("ISO 262", StandardSeed.Iso262, "ISO", "1998",
                        "Selected diameter and coarse-pitch combination"),
                    new StandardReference("ISO 68-1", StandardSeed.Iso68Part1, "ISO", "1998",
                        "Basic thread profile the dimensions derive from"),
                ],
                SourceClassification = "ISO general purpose metric screw thread, coarse pitch series",
                Notes = "Geometry only. No property class, material or mechanical property is recorded, because "
                    + "no readable source for ISO 898-1's tables was obtainable. A joint calculation must not "
                    + "treat this record as sufficient; it names a thread, it does not qualify a fastener.",
            },
            SeedSources.TertiaryReference(
                "ISO metric screw thread",
                $"selected sizes table, {designation} row",
                "ISO 262:1998"),
            new SourceCitation("Wikimedia Foundation", "Wikipedia — ISO metric screw thread",
                TableOrFigure: "Selected sizes table", RowOrEntry: designation));
    }

    private static string Format(double millimetres) =>
        millimetres == Math.Floor(millimetres)
            ? millimetres.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
            : millimetres.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}
