using Tempest.Core.Bearings;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// The 6000, 6200 and 6300 deep groove ball bearing series, sizes 00 to 12,
/// from RHD Bearings' own product specification pages (PO decision
/// 2026-10-01).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why one manufacturer for the whole set.</b> Load ratings and speed
/// limits are each maker's own product data. A selection routine comparing
/// a 6206 with a 6306 must compare like with like; mixing makers' figures
/// across a series would make the comparison partly a comparison of
/// catalogues. The first acquisition already used RHD for 6205 and 6305,
/// and SKF's and Schaeffler's catalogues still serve their tables only to a
/// scripted browser.
/// </para>
/// <para>
/// <b>Recorded as published.</b> Every figure below is the page's own.
/// One oddity is kept rather than smoothed: the page gives the 6301 a mass
/// of 0.051 kg, lighter than the smaller 6300 at 0.053 kg; it is recorded
/// as published and flagged in that record's notes.
/// </para>
/// </remarks>
public sealed partial class BearingSeed
{
    // Designation, series, d, D, B (mm), r min (mm), Cr (kN), Cor (kN),
    // grease and oil speed limits (rpm), mass (kg) — each row exactly as
    // https://rhdbearings.com/specs/{series}-series/{designation}/ states it.
    private static (string Designation, string Series, double Bore, double Outside, double Width, double Chamfer,
        double Cr, double Cor, double Grease, double Oil, double Mass)[] SeriesRows =>
    [
        ("6000", "6000", 10, 26, 8, 0.3, 4.58, 1.98, 22000, 30000, 0.019),
        ("6001", "6000", 12, 28, 8, 0.3, 5.1, 2.38, 20000, 26000, 0.022),
        ("6002", "6000", 15, 32, 9, 0.3, 5.58, 2.85, 19000, 24000, 0.031),
        ("6003", "6000", 17, 35, 10, 0.3, 6.0, 3.25, 17000, 21000, 0.04),
        ("6004", "6000", 20, 42, 12, 0.6, 9.38, 5.02, 16000, 19000, 0.068),
        ("6005", "6000", 25, 47, 12, 0.6, 10.0, 5.85, 13000, 17000, 0.078),
        ("6006", "6000", 30, 55, 13, 1.0, 13.2, 8.3, 11000, 14000, 0.113),
        ("6007", "6000", 35, 62, 14, 1.0, 16.2, 10.5, 9500, 12000, 0.148),
        ("6008", "6000", 40, 68, 15, 1.0, 17.0, 11.8, 9000, 11000, 0.185),
        ("6009", "6000", 45, 75, 16, 1.0, 21.0, 14.8, 8000, 10000, 0.23),
        ("6010", "6000", 50, 80, 16, 1.0, 22.0, 16.2, 7000, 9000, 0.25),
        ("6011", "6000", 55, 90, 18, 1.1, 30.2, 21.8, 7000, 8500, 0.362),
        ("6012", "6000", 60, 95, 18, 1.1, 31.5, 24.2, 6300, 7500, 0.385),
        ("6200", "6200", 10, 30, 9, 0.6, 5.1, 2.38, 20000, 26000, 0.032),
        ("6201", "6200", 12, 32, 10, 0.6, 6.82, 3.05, 19000, 24000, 0.035),
        ("6202", "6200", 15, 35, 11, 0.6, 7.65, 3.72, 18000, 22000, 0.045),
        ("6203", "6200", 17, 40, 12, 0.6, 9.58, 4.78, 16000, 20000, 0.064),
        ("6204", "6200", 20, 47, 14, 1.0, 12.8, 6.65, 14000, 18000, 0.103),
        ("6206", "6200", 30, 62, 16, 1.0, 19.5, 11.5, 9500, 13000, 0.2),
        ("6207", "6200", 35, 72, 17, 1.1, 25.5, 15.2, 8500, 11000, 0.288),
        ("6208", "6200", 40, 80, 18, 1.1, 29.5, 18.0, 8000, 10000, 0.368),
        ("6209", "6200", 45, 85, 19, 1.1, 31.5, 20.5, 7000, 9000, 0.416),
        ("6210", "6200", 50, 90, 20, 1.1, 35.0, 23.2, 6700, 8500, 0.463),
        ("6211", "6200", 55, 100, 21, 1.5, 43.2, 29.2, 6000, 7500, 0.603),
        ("6212", "6200", 60, 110, 22, 1.5, 47.8, 32.8, 5600, 7000, 0.789),
        ("6300", "6300", 10, 35, 11, 0.6, 7.65, 3.48, 18000, 24000, 0.053),
        ("6301", "6300", 12, 37, 12, 1.0, 9.72, 5.08, 17000, 22000, 0.051),
        ("6302", "6300", 15, 42, 13, 1.0, 11.5, 5.42, 16000, 20000, 0.08),
        ("6303", "6300", 17, 47, 14, 1.0, 13.5, 6.58, 15000, 18000, 0.109),
        ("6304", "6300", 20, 52, 15, 1.1, 15.8, 7.88, 13000, 16000, 0.142),
        ("6306", "6300", 30, 72, 19, 1.1, 27.0, 15.2, 9000, 11000, 0.349),
        ("6307", "6300", 35, 80, 21, 1.5, 33.4, 19.2, 8000, 9500, 0.455),
        ("6308", "6300", 40, 90, 23, 1.5, 40.8, 24.0, 7000, 8500, 0.639),
        ("6309", "6300", 45, 100, 25, 1.5, 52.8, 31.8, 6300, 7500, 0.837),
        ("6310", "6300", 50, 110, 27, 2.0, 61.8, 38.0, 6000, 7000, 1.082),
        ("6311", "6300", 55, 120, 29, 2.0, 71.5, 44.8, 5600, 6700, 1.367),
        ("6312", "6300", 60, 130, 31, 2.1, 81.8, 51.8, 5000, 6000, 1.71),
    ];

    /// <summary>The record identity of the RHD deep groove ball bearing <paramref name="designation"/>.</summary>
    /// <param name="designation">The bearing designation, e.g. "6206".</param>
    /// <returns>The record identity.</returns>
    public static string RhdRecordId(string designation) => $"brg-rhd-{designation}";

    private static IEnumerable<ReferenceSeedRecord<BearingDefinition>> DeepGrooveSeries() =>
        SeriesRows.Select(row => new ReferenceSeedRecord<BearingDefinition>(
            RhdRecordId(row.Designation),
            new BearingDefinition
            {
                Identity = new BearingIdentity(
                    Manufacturer,
                    row.Designation,
                    Designation: row.Designation,
                    Series: row.Series,
                    FamilyDesignation: "Deep groove ball bearing, single row, open"),
                Family = BearingFamily.DeepGrooveBall,
                Geometry = new BearingGeometry(
                    Bore: new Quantity<Length>(row.Bore, LengthUnits.Millimetre),
                    OutsideDiameter: new Quantity<Length>(row.Outside, LengthUnits.Millimetre),
                    Width: new Quantity<Length>(row.Width, LengthUnits.Millimetre),
                    ChamferMinimum: new Quantity<Length>(row.Chamfer, LengthUnits.Millimetre)),
                LoadRatings = new BearingLoadRatings(
                    BasicDynamicRadial: Rating(row.Cr, "Basic dynamic radial load rating Cr, as this manufacturer "
                        + "publishes it. Not interchangeable with another manufacturer's figure for the same boundary "
                        + "dimensions.", "Cr"),
                    BasicStaticRadial: Rating(row.Cor, "Basic static radial load rating Cor, as this manufacturer "
                        + "publishes it.", "Cor")),
                SpeedRatings =
                [
                    Speed(BearingSpeedRatingKind.GreaseLubricatedSpeed, row.Grease,
                        "Speed limit under grease lubrication, as this manufacturer publishes it, for the open bearing."),
                    Speed(BearingSpeedRatingKind.OilLubricatedSpeed, row.Oil,
                        "Speed limit under oil lubrication, as this manufacturer publishes it."),
                ],
                Mass = new Quantity<Mass>(row.Mass, MassUnits.Kilogram),
                Standards =
                [
                    new StandardReference("ISO 15", StandardSeed.Iso15, "ISO", "2011",
                        "Boundary dimensions. The edition recorded is the one the manufacturer's page cites."),
                ],
                ManufacturerAttributes = new Dictionary<string, string>
                {
                    ["Steel grade"] = "SAE52100",
                },
                Notes = "Open bearing ratings. The page also lists shielded (ZZ/Z) and sealed (2RS) variants with speed "
                    + "factors relative to these limits; those variants are not separate records. Shoulder and recess "
                    + "diameters the page marks approximate are not recorded (no approximation qualifier exists for "
                    + "additional dimensions)."
                    + (row.Designation == "6301"
                        ? " SOURCE ODDITY: the page's mass for 6301 (0.051 kg) is lower than for the smaller 6300 "
                            + "(0.053 kg); recorded as published, not corrected."
                        : string.Empty),
            },
            SeedSources.RhdBearingsPage(row.Designation, row.Series),
            new SourceCitation("RHD Bearings", $"{row.Designation} Deep Groove Ball Bearing",
                TableOrFigure: "Dimensions, load ratings, speed limits and weight")));
}
