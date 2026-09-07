using Tempest.Core.Fasteners;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// A seed set of ISO metric coarse-thread hexagon head bolts, carrying
/// thread geometry only.
/// </summary>
/// <remarks>
/// <para>
/// <b>Dimension-only, deliberately and visibly.</b> Every record here
/// leaves <see cref="FastenerDefinition.Mechanical"/> empty. No property
/// class, no proof load, no tensile strength. Those values live in
/// ISO 898-1, which is paywalled; the two fastener manufacturers whose
/// technical libraries restate it serve the tables only inside downloadable
/// PDFs that were not retrievable, and no other readable source stated
/// them. Writing "8.8" and a proof stress from memory would have produced
/// a library that looks complete and cannot be traced to anything, which
/// is the single most damaging thing a reference dataset can be.
/// </para>
/// <para>
/// <b>What it is good for as it stands.</b> Thread designation, nominal
/// diameter and coarse pitch are enough to reason about hole sizes,
/// clearance, spanner sizes and thread engagement, and enough for a
/// bill of materials to name a fastener unambiguously. It is not enough to
/// size a joint, and nothing here pretends otherwise:
/// <see cref="FastenerMechanicalProperties.IsRecorded"/> answers
/// <see langword="false"/> for every one of these records, so a consumer
/// that needs strength can detect its absence rather than reading a zero.
/// </para>
/// </remarks>
public sealed class FastenerSeed : IReferenceSeed<FastenerDefinition>
{
    /// <summary>The single instance of this dataset.</summary>
    public static FastenerSeed Instance { get; } = new();

    private FastenerSeed()
    {
    }

    /// <inheritdoc />
    public string DatasetName => "ISO metric coarse thread hexagon head bolts — geometry only";

    /// <inheritdoc />
    public int DatasetRevision => 1;

    /// <inheritdoc />
    public IReadOnlyList<ReferenceSeedRecord<FastenerDefinition>> Records { get; } =
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
                "ISO 262:1998"));
    }

    private static string Format(double millimetres) =>
        millimetres == Math.Floor(millimetres)
            ? millimetres.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
            : millimetres.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}
