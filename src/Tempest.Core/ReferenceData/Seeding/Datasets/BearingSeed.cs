using Tempest.Core.Bearings;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// A seed set of deep groove ball bearings, transcribed from one
/// manufacturer's published product specifications.
/// </summary>
/// <remarks>
/// <para>
/// <b>One manufacturer, and the record says so on its face.</b> Boundary
/// dimensions for a 6205 are fixed by ISO 15 and are the same whoever
/// makes it; load ratings and speed limits are not, and differ between
/// manufacturers for the same envelope because they follow from the
/// internal geometry, steel and cage each maker chooses. That is why
/// <see cref="BearingIdentity"/> makes the manufacturer mandatory, and why
/// nothing here presents a load rating as a property of the size.
/// </para>
/// <para>
/// <b>One family only, which is a real gap.</b> Deep groove ball bearings
/// are all that was obtainable: the two large manufacturers' catalogues
/// serve their data only to a scripted browser, and the tapered roller
/// pages of the manufacturer that does publish static specifications were
/// not present. Rolling-element selection genuinely turns on the choice
/// between families, so this dataset cannot yet support that decision, and
/// the deferred-datasets record says so rather than leaving a reader to
/// infer coverage from a count of records.
/// </para>
/// </remarks>
public sealed class BearingSeed : IReferenceSeed<BearingDefinition>
{
    /// <summary>The identity of the 6205 deep groove ball bearing record.</summary>
    public const string Rhd6205 = "brg-rhd-6205";

    /// <summary>The identity of the 6305 deep groove ball bearing record.</summary>
    public const string Rhd6305 = "brg-rhd-6305";

    private const string Manufacturer = "RHD Bearings";

    /// <summary>The single instance of this dataset.</summary>
    public static BearingSeed Instance { get; } = new();

    private BearingSeed()
    {
    }

    /// <inheritdoc />
    public string DatasetName => "Deep groove ball bearings — RHD Bearings published specifications";

    /// <inheritdoc />
    public int DatasetRevision => 1;

    /// <inheritdoc />
    public IReadOnlyList<ReferenceSeedRecord<BearingDefinition>> Records { get; } =
    [
        new(Rhd6205,
            new BearingDefinition
            {
                Identity = new BearingIdentity(
                    Manufacturer,
                    "6205",
                    Designation: "6205",
                    Series: "6200",
                    FamilyDesignation: "Deep groove ball bearing, single row, open"),
                Family = BearingFamily.DeepGrooveBall,
                Geometry = new BearingGeometry(
                    Bore: new Quantity<Length>(25.0, LengthUnits.Millimetre),
                    OutsideDiameter: new Quantity<Length>(52.0, LengthUnits.Millimetre),
                    Width: new Quantity<Length>(15.0, LengthUnits.Millimetre),
                    ChamferMinimum: new Quantity<Length>(1.0, LengthUnits.Millimetre)),
                LoadRatings = new BearingLoadRatings(
                    BasicDynamicRadial: Rating(14.0, "Basic dynamic radial load rating Cr, as this manufacturer "
                        + "publishes it. Not interchangeable with another manufacturer's figure for the same "
                        + "boundary dimensions.", "Cr"),
                    BasicStaticRadial: Rating(7.88, "Basic static radial load rating Cor, as this manufacturer "
                        + "publishes it.", "Cor")),
                SpeedRatings =
                [
                    Speed(BearingSpeedRatingKind.GreaseLubricatedSpeed, 12_000.0,
                        "Speed limit under grease lubrication, as this manufacturer publishes it."),
                    Speed(BearingSpeedRatingKind.OilLubricatedSpeed, 15_000.0,
                        "Speed limit under oil lubrication, as this manufacturer publishes it."),
                ],
                Mass = new Quantity<Mass>(0.127, MassUnits.Kilogram),
                Standards =
                [
                    new StandardReference("ISO 15", StandardSeed.Iso15, "ISO", "2011",
                        "Boundary dimensions. The edition recorded is the one the manufacturer's page cites; "
                        + "the indexed standard record carries the 2017 edition."),
                ],
                ManufacturerAttributes = new Dictionary<string, string>
                {
                    ["Steel grade"] = "SAE52100",
                    ["Ball grade"] = "G10",
                },
                Notes = "The source also gives a shoulder diameter of about 34.35 mm and a recess diameter of "
                    + "about 46.21 mm, both marked approximate. Neither is recorded: additional dimensions are "
                    + "held as exact quantities with nowhere to carry an approximation qualifier, and storing "
                    + "an approximate figure as an exact one would overstate it. The source's stated 'load "
                    + "capacity 1427 kg' is not recorded either, being a restatement of Cr as a mass.",
            },
            SeedSources.RhdBearings("6205 Deep Groove Ball Bearing", "Dimensions, load ratings, speed limits and weight")),

        new(Rhd6305,
            new BearingDefinition
            {
                Identity = new BearingIdentity(
                    Manufacturer,
                    "6305",
                    Designation: "6305",
                    Series: "6300",
                    FamilyDesignation: "Deep groove ball bearing, single row, open"),
                Family = BearingFamily.DeepGrooveBall,
                Geometry = new BearingGeometry(
                    Bore: new Quantity<Length>(25.0, LengthUnits.Millimetre),
                    OutsideDiameter: new Quantity<Length>(62.0, LengthUnits.Millimetre),
                    Width: new Quantity<Length>(17.0, LengthUnits.Millimetre),
                    ChamferMinimum: new Quantity<Length>(1.1, LengthUnits.Millimetre)),
                LoadRatings = new BearingLoadRatings(
                    BasicDynamicRadial: Rating(22.2, "Basic dynamic radial load rating Cr, as this manufacturer "
                        + "publishes it.", "Cr"),
                    BasicStaticRadial: Rating(11.5, "Basic static radial load rating Cor, as this manufacturer "
                        + "publishes it.", "Cor")),
                SpeedRatings =
                [
                    Speed(BearingSpeedRatingKind.GreaseLubricatedSpeed, 10_000.0,
                        "Speed limit under grease lubrication, as this manufacturer publishes it."),
                    Speed(BearingSpeedRatingKind.OilLubricatedSpeed, 14_000.0,
                        "Speed limit under oil lubrication, as this manufacturer publishes it."),
                ],
                Mass = new Quantity<Mass>(0.219, MassUnits.Kilogram),
                Standards =
                [
                    new StandardReference("ISO 15", StandardSeed.Iso15, "ISO", "2011",
                        "Boundary dimensions. The edition recorded is the one the manufacturer's page cites."),
                ],
                ManufacturerAttributes = new Dictionary<string, string>
                {
                    ["Steel grade"] = "SAE52100",
                    ["Ball grade"] = "G10",
                    ["Carbon content"] = "0.95-1.05%",
                    ["Chromium content"] = "1.40-1.65%",
                },
                Notes = "The same bore as the 6205 in a heavier series: 62 mm outside diameter against 52 mm, "
                    + "and a dynamic rating of 22.2 kN against 14.0 kN. The pair exists in this seed precisely "
                    + "so a selection routine has to weigh capacity against envelope rather than picking the "
                    + "only candidate.",
            },
            SeedSources.RhdBearings("6305 Deep Groove Ball Bearing", "Dimensions, load ratings, speed limits and weight")),
    ];

    private static ReferenceValue<Force> Rating(double kilonewtons, string conditions, string designation) =>
        new(new Quantity<Force>(kilonewtons, ForceUnits.Kilonewton),
            ReferenceValueOrigin.ManufacturerCatalogue,
            conditions,
            designation);

    private static BearingSpeedRating Speed(BearingSpeedRatingKind kind, double revolutionsPerMinute, string conditions) =>
        new(kind,
            new ReferenceValue<RotationalSpeed>(
                new Quantity<RotationalSpeed>(revolutionsPerMinute, RotationalSpeedUnits.RevolutionPerMinute),
                ReferenceValueOrigin.ManufacturerCatalogue,
                conditions));
}
