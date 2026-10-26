using System.Globalization;
using System.Text;
using Tempest.Calc.Import;
using Tempest.Calc.Import.Scan;
using Tempest.Calc.Sheets;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Calc.Library;

/// <summary>
/// One area of the demonstration project: the pack template it is built from, the name it is given and the (invented) specification its figures cite.
/// </summary>
/// <param name="TemplateId">The pack template's id (<see cref="CalcPackTemplate.Id"/>).</param>
/// <param name="Area">What the area is in the press cell; every calculation of the area carries it in its name.</param>
/// <param name="Specification">The invented specification the area's chosen figures cite.</param>
/// <param name="Story">One sentence of the story the area tells.</param>
/// <param name="StepNames">New names for steps whose template names do not fit the story, by step key.</param>
/// <param name="Adjust">Changes the step's example inputs where the story needs another name for a table row; <see langword="null"/> leaves them as the template gives them.</param>
internal sealed record CalcDemoArea(
    string TemplateId, string Area, string Specification, string Story,
    IReadOnlyDictionary<string, string>? StepNames = null,
    Func<string, IReadOnlyList<CalcInputValue>, IReadOnlyList<CalcInputValue>>? Adjust = null);

/// <summary>
/// Addendum C, C-6: the seeded demonstration project "Demo — Hydraulic Press Cell". One invented press cell carries every linked chain this build ships:
/// the press hydraulic power unit, the infeed conveyor drive, a servo transfer axis and its motor circuit, the part ejector, a blank-stack tamper, the
/// support stand and its base plate, a heating-main anchor, the welfare room's heating and wall, the control panel's cooling and its chilled-water feed, the
/// operator pendant box, and the locking cylinder's piston seal (scan to seal, from a synthetic scan table).
/// <para>
/// <b>Its figures are invented, with one exception.</b> No figure is a licensed value: the example inputs are the pack templates' own (each already recorded as
/// an invented illustration), the cable tables are the synthetic test tables, and the scan table is generated from fixed numbers. The exception is the
/// dimensions of the published UC 203×203×46 section in the steel-support and heating-main packs, which are published figures, cited as such and labelled
/// indicative (<see cref="SectionDimensions"/>) rather than as an invented specification's. The project carries a flag (the tag
/// <see cref="Tag"/>) and every sheet made under a flagged project says DEMO in its header (<see cref="Watermark"/>): the flag lives on the project, so no
/// calculation text holds it and no real project carries it unless someone tags one.
/// </para>
/// </summary>
public static class CalcDemoProject
{
    /// <summary>The project's number: five characters of customer code and six of project reference (ADR-0156). The one number the install refuses and the removal looks for.</summary>
    public const string Identifier = "DEMO0-PRESS1";

    /// <summary>The project's name.</summary>
    public const string DisplayName = "Demo — Hydraulic Press Cell";

    /// <summary>The flag on the project (its tag): sheets made under a project that carries it are watermarked.</summary>
    public const string Tag = "demo-project";

    /// <summary>The kind every project's backing document carries (the Mechanical registry's <c>Project</c>; the Library does not reference the Workspace).</summary>
    public const string ProjectKind = "Project";

    /// <summary>The notice printed at the top of every sheet of the demonstration, and (as DEMO) in every page's footer of its PDF.</summary>
    public const string Watermark = "DEMO — invented data for demonstration only; not for design, purchase or construction";

    /// <summary>The start of every calculation's name, so a list of calculations shows what is demonstration at a glance.</summary>
    public const string NamePrefix = "DEMO — ";

    /// <summary>The prefix of the demonstration's scan profile ids; a removal deletes profiles of no other name.</summary>
    public const string ScanProfilePrefix = "demo-press-";

    /// <summary>
    /// The scan profile versions the install writes, and the only ones a removal deletes: the exact keys, not every version under the id, so a later version of
    /// either profile that a person saved after the install is theirs and stays.
    /// </summary>
    internal static IReadOnlyList<(string Id, int Version)> ScanProfiles { get; } = [(StackProfileId, 1), (GlandProfileId, 1)];

    /// <summary>The profile the install writes at <paramref name="id"/>@<paramref name="version"/>, or <see langword="null"/>: a removal deletes a stored version only when it is this one, word for word.</summary>
    internal static ScanProfile? ShippedProfile(string id, int version) =>
        new[] { StackProfile, GlandProfile }.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal) && p.Version == version);

    /// <summary>The scan profile that reads the synthetic table into the groove-root stack.</summary>
    public const string StackProfileId = ScanProfilePrefix + "seal-stack";

    /// <summary>The scan profile that reads the synthetic table into the gland's measured rows.</summary>
    public const string GlandProfileId = ScanProfilePrefix + "seal-gland";

    /// <summary>The synthetic scan table's file name.</summary>
    public const string ScanFileName = "demo-press-seal-scan.csv";

    /// <summary>The parts in the synthetic scan table: the customary 30 below which the capability figures carry a warning.</summary>
    public const int ScanParts = 30;

    /// <summary>The story, in the words the user page and the project's notes carry.</summary>
    public const string Story =
        "A hydraulic press cell, invented: a 100/70 × 500 mm press cylinder at 160 bar with its pump, emergency-stroke accumulator and motor supply; an infeed conveyor, a servo "
        + "transfer axis, a part ejector and a blank-stack tamper; a support stand for the control desk, a heating-main anchor, a welfare room with its heating and its wall; the "
        + "control panel's cooling and its chilled-water feed; the operator pendant box; and the piston seal of the locking cylinder, its groove measured on 30 synthetic scanned parts.";

    /// <summary>Whether <paramref name="project"/> is flagged as a demonstration: the one question the sheet watermark asks.</summary>
    public static bool IsDemo(IEngineeringObject? project) =>
        project is IHasMetadata metadata && metadata.Tags.Contains(Tag, StringComparer.Ordinal);

    /// <summary>
    /// Whether <paramref name="project"/> is the demonstration project: it carries the flag <b>and</b> the demonstration's number. A real project that holds only
    /// one of the two is not the demonstration and is never removed as one.
    /// </summary>
    public static bool IsTheDemonstration(IEngineeringObject? project) =>
        IsDemo(project) && project is IHasBusinessIdentifier named && string.Equals(named.Identifier, Identifier, StringComparison.OrdinalIgnoreCase);

    // ------------------------------------------------------------------------------------------------------------------------------
    // The areas
    // ------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Every pack template of the build, in the order the story tells them, with the names the story gives. See the user page for why each fits.</summary>
    internal static IReadOnlyList<CalcDemoArea> Areas { get; } =
    [
        new("pack.hydraulic-power-unit", "Press HPU", "DEMO-SPEC-PC-01", "The press cylinder, its pump, accumulator, pressure line, motor, supply cable and start."),
        new("pack.drive-train", "Infeed conveyor drive", "DEMO-SPEC-PC-02", "The infeed conveyor's motor, shaft, bearing, bearing seat and coupling bolts."),
        new("pack.motor-circuit", "Transfer axis circuit", "DEMO-SPEC-PC-03", "The servo transfer axis, its panel load, cable, voltage drop, earth-fault protection and enclosure heat."),
        new("pack.actuated-linkage", "Part ejector", "DEMO-SPEC-PC-04", "The part ejector's crank slider, replaced by a cylinder, and its emergency stroke."),
        new("pack.mechanism-review", "Blank-stack tamper", "DEMO-SPEC-PC-05", "The cam, spring, shaft, bearing, motor and cable of the blank-stack tamper."),
        new("pack.steel-support", "Support stand ES-4", "DEMO-SPEC-PC-06", "The support stand for the control desk: loads, frame, column, joint, bolts and base plate.",
            Adjust: (key, inputs) => key switch
            {
                "s5" => Rename(inputs, "actions", 0, "name", "Control desk and pendant dead load"),
                "s3" or "s6" => PublishedSection(inputs),
                _ => inputs,
            }),
        new("pack.pipework-supports", "Heating main anchor", "DEMO-SPEC-PC-07", "The heating main's growth, the anchor post that holds it and its base plate.",
            Adjust: (key, inputs) => key == "s6" ? PublishedSection(inputs) : inputs),
        new("pack.wet-heating", "Welfare room heating", "DEMO-SPEC-PC-08", "The welfare room and washroom heat losses, the sealed system, the pipework and its circulator.",
            StepNames: new Dictionary<string, string>(StringComparer.Ordinal) { ["b4a"] = "Welfare room heat loss", ["b4b"] = "Washroom heat loss" },
            Adjust: (key, inputs) => key == "b6" ? Rename(Rename(inputs, "rooms", 0, "name", "Welfare room"), "rooms", 1, "name", "Washroom") : inputs),
        new("pack.envelope-moisture", "Welfare room wall", "DEMO-SPEC-PC-09", "The welfare room's internally insulated external wall: heat loss, room air and condensation."),
        new("pack.cooling", "Control panel cooling", "DEMO-SPEC-PC-10",
            "The control panel's heat, the enclosure that cannot lose it (a deliberate FAIL), the cooling coil and its pump.",
            Adjust: (key, inputs) => key == "e6"
                ? Rename(Rename(Rename(inputs, "loads", 0, "name", "Press HPU pump drive"), "loads", 1, "name", "Infeed conveyor drive"), "loads", 2, "name", "Servo axis drives (4)")
                : inputs),
        new("pack.cold-pipe", "Chilled water to panel coil", "DEMO-SPEC-PC-11", "The chilled-water line to the panel's coil, the plant room air and the pipe insulation."),
        new("pack.electronics-enclosure", "Operator pendant box", "DEMO-SPEC-PC-12", "The pendant box's supply trace, board heat, enclosure temperature, cover latches and cover plate."),
    ];

    /// <summary>The name an area's step is given: the demonstration's prefix, the area, a colon and the step's own name.</summary>
    internal static string StepName(CalcDemoArea area, CalcPackStep step) =>
        $"{NamePrefix}{area.Area}: {(area.StepNames is { } names && names.TryGetValue(step.Key, out var renamed) ? renamed : step.Name)}";

    /// <summary>The title an area's pack is given.</summary>
    internal static string PackTitle(CalcDemoArea area) => $"{NamePrefix}{area.Area}";

    /// <summary>The pack template of <paramref name="area"/> with every step named for the story; links and examples are the template's own.</summary>
    internal static CalcPackTemplate Renamed(CalcPackTemplate template, CalcDemoArea area) =>
        template with { Steps = [.. template.Steps.Select(s => s with { Name = StepName(area, s) })] };

    /// <summary>The provenance of every figure a step chooses: the area's invented specification, the step's own clause.</summary>
    internal static CalcProvenance Chosen(CalcDemoArea area, int stepNumber) =>
        CalcProvenance.Specification($"{area.Specification} §{stepNumber} (invented demonstration figure)");

    /// <summary>The provenance of an input left at the sheet's own default.</summary>
    internal static CalcProvenance Default { get; } = CalcProvenance.Assumed("Sheet default value kept for the demonstration; no project figure.");

    /// <summary>The provenance of the synthetic cable tables.</summary>
    internal static CalcProvenance SyntheticTables { get; } =
        CalcProvenance.Specification("DEMO SYNTHETIC cable capacities and device ratings: invented, not a regulation or manufacturer table");

    /// <summary>The provenance of the published UC 203×203×46 dimensions: published figures, not an invented specification's, and indicative until the user's own section is entered.</summary>
    internal static CalcProvenance SectionDimensions { get; } = CalcProvenance.Assumed("published UC 203×203×46 dimensions (indicative)");

    /// <summary>The section dimensions the pack examples state for the published UC 203×203×46 (mm).</summary>
    private static readonly IReadOnlyDictionary<string, double> SectionFigures = new Dictionary<string, double>(StringComparer.Ordinal)
    {
        ["h"] = 203.2, ["b"] = 203.6, ["t_w"] = 7.2, ["t_f"] = 11, ["r"] = 10.2,
    };

    /// <summary>
    /// A step's inputs with the published section's dimensions given their own provenance (<see cref="SectionDimensions"/>) in place of the area's invented
    /// specification clause, which every other figure the step chooses keeps. An input is retagged only when it is one of the section's dimensions at the
    /// section's own value, so a figure the user later changes under the same name is not mislabelled by this class.
    /// </summary>
    private static IReadOnlyList<CalcInputValue> PublishedSection(IReadOnlyList<CalcInputValue> inputs) =>
        [.. inputs.Select(i => SectionFigures.TryGetValue(i.Symbol, out var figure) && i.Number == figure ? i with { Provenance = SectionDimensions } : i)];

    private static IReadOnlyList<CalcInputValue> Rename(IReadOnlyList<CalcInputValue> inputs, string table, int row, string column, string text) =>
        [.. inputs.Select(i => i.Symbol != table || i.Rows is not { } rows ? i : i with
        {
            Rows = [.. rows.Select((r, index) => index != row ? r : new CalcRow(r.Cells.ToDictionary(c => c.Key, c => c.Key == column ? new CalcCell(Text: text) : c.Value, StringComparer.Ordinal)))],
        })];

    // ------------------------------------------------------------------------------------------------------------------------------
    // Scan to seal
    // ------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The area name of the scan-to-seal pair.</summary>
    public const string SealArea = "Locking cylinder seal";

    /// <summary>The specification and drawing the scan-to-seal pair cite.</summary>
    internal static CalcProvenance SealDrawing { get; } = CalcProvenance.Drawing("DEMO-DRG-PC-130", "A");

    /// <summary>The groove-root stack of the locking cylinder's piston seal: the groove root is the scanned contributor, the plating build the other.</summary>
    internal static IReadOnlyList<CalcInputValue> SealStackInputs() =>
    [
        CalcInputValue.Table("contributors",
        [
            CalcRow.Of(("name", new CalcCell(Text: "Groove root (scanned)")), ("direction", new CalcCell(Text: "adds")), ("nominal", new CalcCell(51.5, "mm")), ("upper", new CalcCell(0.05, "mm")), ("lower", new CalcCell(-0.05, "mm"))),
            CalcRow.Of(("name", new CalcCell(Text: "Plating build")), ("direction", new CalcCell(Text: "adds")), ("nominal", new CalcCell(0, "mm")), ("upper", new CalcCell(0.002, "mm")), ("lower", new CalcCell(-0.002, "mm"))),
        ], SealDrawing),
        CalcInputValue.Quantity("minimum", 51.4, "mm", SealDrawing),
        CalcInputValue.Quantity("maximum", 51.6, "mm", SealDrawing),
        CalcInputValue.Of("k", 3, SealDrawing),
        CalcInputValue.Of("f_rss", 1.5, SealDrawing),
        CalcInputValue.Of("cpk_target", 1.33, SealDrawing),
        CalcInputValue.Of("n_mc", 2000, SealDrawing),
        CalcInputValue.Of("seed", 1, SealDrawing),
        CalcInputValue.Choice("notation", "BS 8888", SealDrawing),
    ];

    /// <summary>The scan columns of the synthetic table (a scan software's export is user-configurable, so the profile names them).</summary>
    private static ScanColumns SealColumns { get; } = new("Element", Nominal: "Nominal", Actual: "Actual", Deviation: "Deviation", UpperTolerance: "Upper tol.", LowerTolerance: "Lower tol.", Serial: "Serial");

    /// <summary>The profile that reads the groove root into the stack's first contributor.</summary>
    internal static ScanProfile StackProfile { get; } = new(
        StackProfileId, 1, "DEMO — groove root into the tolerance stack", SealColumns, [new ScanFeatureEntry("Groove root", "contributors[0]", Required: true)],
        TemplateId: "mech.tolerance-stack", Notes: "Demonstration profile for the synthetic scan table; removed with the demonstration project.");

    /// <summary>The profile that reads the bore and the groove width into the gland's measured rows.</summary>
    internal static ScanProfile GlandProfile { get; } = new(
        GlandProfileId, 1, "DEMO — bore and groove width into the O-ring gland", SealColumns,
        [new ScanFeatureEntry("Bore", "measured{D_s}"), new ScanFeatureEntry("Width", "measured{b}")],
        TemplateId: "fp.oring-gland", Notes: "Demonstration profile for the synthetic scan table; removed with the demonstration project.");

    /// <summary>The settings every demonstration import is made with: what the table is, so the provenance says so.</summary>
    internal static ScanSettings ScanSettings { get; } = new() { SoftwareVersion = "synthetic demonstration table", Alignment = "none (invented deviations)" };

    /// <summary>The synthetic scan table as an import file.</summary>
    internal static ScanFileInput ScanFile() =>
        new(BoundedInputReader.FromBytes(new UTF8Encoding(false).GetBytes(SealScanCsv()), ScanFileName));

    /// <summary>
    /// The synthetic scan table: 30 parts, each measured at three features — the groove root (nominal 51.5 mm), the bore (56.9 mm) and the groove width
    /// (4.8 mm) — as a scan software would export them. The deviations are generated from a fixed seed, so the table is the same on every machine; they are
    /// invented, not measurements of anything. The same table is printed in <c>docs/worked-examples/demo/</c> so it can be imported by hand.
    /// </summary>
    public static string SealScanCsv()
    {
        var generator = new Generator(20_261_026);
        var sb = new StringBuilder("Element,Nominal,Actual,Deviation,Upper tol.,Lower tol.,Serial\r\n");

        for (var part = 1; part <= ScanParts; part++)
        {
            var serial = $"DEMO-{part:000}";
            Row(sb, "Groove root", 51.5, 51.485 + generator.Normal() * 0.006, 0.05, -0.05, serial);
            Row(sb, "Bore", 56.9, 56.922 + generator.Normal() * 0.0045, 0.046, 0, serial);
            Row(sb, "Width", 4.8, 4.835 + generator.Normal() * 0.011, 0.1, 0, serial);
        }

        return sb.ToString();
    }

    private static void Row(StringBuilder sb, string feature, double nominal, double raw, double upper, double lower, string serial)
    {
        var actual = Math.Round(raw, 3);
        var deviation = Math.Round(actual - nominal, 3);
        sb.Append(CultureInfo.InvariantCulture, $"{feature},{nominal.ToString("0.0##", CultureInfo.InvariantCulture)},{actual.ToString("0.000", CultureInfo.InvariantCulture)},{deviation.ToString("0.000", CultureInfo.InvariantCulture)},{upper.ToString("0.0##", CultureInfo.InvariantCulture)},{lower.ToString("0.0##", CultureInfo.InvariantCulture)},{serial}\r\n");
    }

    /// <summary>A small deterministic generator: integer arithmetic only, so the same seed gives the same table everywhere. Not for anything but this table.</summary>
    private sealed class Generator(uint seed)
    {
        private uint _state = seed;

        private double Uniform()
        {
            _state = unchecked((_state * 1_664_525u) + 1_013_904_223u);
            return (_state >> 8) / 16_777_216.0;
        }

        /// <summary>An approximately standard normal value: twelve uniforms summed, less six.</summary>
        public double Normal()
        {
            var sum = 0.0;

            for (var i = 0; i < 12; i++)
                sum += Uniform();

            return sum - 6.0;
        }
    }
}
