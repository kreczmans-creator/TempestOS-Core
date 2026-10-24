using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>
/// The materials the library starts with (Addendum B, W3, §B5): a few generic textbook values, flagged
/// <see cref="CalcDataOrigin.Indicative"/> and unlicensed. They are rounded engineering-handbook figures for a class of
/// material, good for a first estimate and for trying a calculator; none is a grade of a standard, a supplier's catalogue
/// value or a table from either, and a design takes its values from the grade's own data entered as a material of the user's own.
/// </summary>
public static class MaterialSeeds
{
    public const string SeedPrincipal = "TempestOS (indicative seed)";
    public const string SourceTitle = "Generic textbook values (indicative)";
    public const string Edition = "generic";
    public const string Reference = "Rounded handbook figure for the material class; not from a standard or a catalogue";

    public static readonly Guid CarbonSteel = new("5eed0001-0000-4000-8000-000000000001");
    public static readonly Guid StainlessSteel = new("5eed0001-0000-4000-8000-000000000002");
    public static readonly Guid AluminiumAlloy = new("5eed0001-0000-4000-8000-000000000003");
    public static readonly Guid Copper = new("5eed0001-0000-4000-8000-000000000004");

    /// <summary>The seed materials as records at revision 1, stamped <paramref name="now"/>.</summary>
    public static IReadOnlyList<CalcDataRecord> Build(DateTimeOffset now) =>
    [
        Material(CarbonSteel, "Carbon steel (generic)", now, E: 210, G: 81, nu: 0.3, rho: 7850, alpha: 12e-6, lambda: 50),
        Material(StainlessSteel, "Austenitic stainless steel (generic)", now, E: 195, G: 75, nu: 0.3, rho: 8000, alpha: 16e-6, lambda: 15),
        Material(AluminiumAlloy, "Aluminium alloy (generic)", now, E: 70, G: 26, nu: 0.33, rho: 2700, alpha: 23e-6, lambda: 150),
        Material(Copper, "Copper (generic)", now, E: 120, G: 45, nu: 0.34, rho: 8960, alpha: 17e-6, lambda: 390),
    ];

    private static CalcDataRecord Material(Guid id, string title, DateTimeOffset now, double E, double G, double nu, double rho, double alpha, double lambda)
    {
        var source = new CalcValueSource(SourceTitle, Edition, Reference, CalcLicenceClass.Public, SeedPrincipal, now);

        return new CalcDataRecord(
            id, MaterialLibrary.Kind, title, 1, SourceTitle, Edition, "Generic values", "—", SeedPrincipal, false, CalcDataOrigin.Indicative,
            [
                new CalcDataValue("E", E, "GPa", Source: source),
                new CalcDataValue("G", G, "GPa", Source: source),
                new CalcDataValue("nu", nu, Source: source),
                new CalcDataValue("rho", rho, "kg/m^3", Source: source),
                new CalcDataValue("alpha", alpha, "1/K", Source: source),
                new CalcDataValue("lambda", lambda, "W/(m*K)", Source: source),
            ],
            now,
            "Indicative: a generic figure for a class of material. Enter the grade's own data as a material of your own for a design.");
    }
}
