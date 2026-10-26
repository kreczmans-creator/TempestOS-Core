using System.Text.RegularExpressions;

namespace Tempest.Calc.Sheets;

/// <summary>
/// C-2, row 43: the name of an output port, <c>&lt;area&gt;.&lt;quantity&gt;</c>, and the area each template draws its port names from. The
/// area is one lower-case word naming what the template is about (<c>bearing</c>, <c>shaft</c>, <c>exchanger</c>); the quantity is lower-case
/// words joined by underscores that say what the value is, most general word first (<c>life_modified</c>, <c>temperature_hot_out</c>). The
/// unit is never in the name: it is in the port's unit field. A port is listed under this name; the output symbol it had stays as its
/// symbol and as an alias, so a link or a pack binding saved before the rename still resolves (<see cref="CalcOutputPort"/>).
/// </summary>
public static partial class CalcPortNames
{
    [GeneratedRegex(@"^(?<area>[a-z][a-z0-9]*)\.(?<quantity>[a-z][a-z0-9]*(_[a-z0-9]+)*)$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    /// <summary>Whether <paramref name="name"/> is written <c>&lt;area&gt;.&lt;quantity&gt;</c>.</summary>
    public static bool IsWellFormed(string? name) => name is not null && NamePattern().IsMatch(name);

    /// <summary>The area of a well-formed port name, or <see langword="null"/>.</summary>
    public static string? AreaOf(string? name) => name is not null && NamePattern().Match(name) is { Success: true } m ? m.Groups["area"].Value : null;

    /// <summary>The areas each template may name its ports under (the area vocabulary). A template not listed here has no area yet and so no port names.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Areas { get; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["mech.tolerance-stack"] = ["tolerance"],
        ["mech.iso286-fit"] = ["fit"],
        ["mech.press-fit"] = ["press"],
        ["mech.thick-cylinder"] = ["cylinder"],
        ["mech.lifting-lug"] = ["lug"],
        ["mech.bearing-life-basic"] = ["bearing"],
        ["mech.fatigue-miner"] = ["fatigue"],
        ["mech.shaft-stress"] = ["shaft"],
        ["mech.thermal-expansion"] = ["expansion"],
        ["mech.thin-vessel"] = ["vessel"],
        ["mech.material-margin"] = ["material"],
        ["mech.bolted-joint"] = ["joint"],
        ["mech.bolt-group"] = ["boltgroup"],
        ["mech.bearing-life"] = ["bearing"],
        ["mech.weld-group"] = ["weld"],
        ["mech.shaft"] = ["shaft"],
        ["mech.spring"] = ["spring"],
        ["mech.snap-fit"] = ["snapfit"],
        ["mech.mechanism"] = ["mechanism"],
        ["struct.section-properties"] = ["section"],
        ["struct.frame-2d"] = ["frame"],
        ["struct.beam-formulas"] = ["beam"],
        ["struct.en1993-member"] = ["member"],
        ["struct.load-combinations"] = ["combination"],
        ["struct.connection-ec3"] = ["bolt", "weld"],
        ["struct.base-plate"] = ["baseplate"],
        ["struct.ltb-interaction"] = ["ltb"],
        ["struct.plate-bending"] = ["plate"],
        ["struct.lifting"] = ["lifting"],
        ["struct.column-buckling"] = ["column"],
        ["struct.fillet-weld"] = ["weld"],
        ["bs.pipe-pump"] = ["pipe", "pump"],
        ["bs.psychrometrics"] = ["air"],
        ["bs.heat-exchanger"] = ["exchanger"],
        ["bs.sealed-system"] = ["heating"],
        ["bs.room-heat-loss"] = ["room"],
        ["bs.duct-sizing"] = ["duct"],
        ["bs.glaser"] = ["condensation"],
        ["bs.pipe-expansion"] = ["piping"],
        ["bs.insulation"] = ["insulation"],
        ["bs.plane-wall"] = ["wall"],
        ["elec.motor-sizing"] = ["motor"],
        ["elec.enclosure-heat"] = ["enclosure"],
        ["elec.voltage-drop"] = ["circuit"],
        ["elec.load-budget"] = ["load", "battery"],
        ["elec.fault-protection"] = ["fault", "conductor"],
        ["elec.conductor-sizing"] = ["conductor"],
        ["elec.pcb-trace"] = ["trace"],
        ["elec.containment"] = ["containment"],
        ["elec.motor-start"] = ["start"],
        ["elec.thermal-chain"] = ["heatsink"],
        ["fp.cylinder"] = ["actuator"],
        ["fp.accumulator"] = ["accumulator"],
        ["fp.oring-gland"] = ["gland"],
    };

    /// <summary>The problem with a port's public name for the template <paramref name="templateId"/>, or <see langword="null"/> when it conforms.</summary>
    public static string? Problem(string templateId, string? name)
    {
        if (string.IsNullOrEmpty(name))
            return "has no public name";

        if (!IsWellFormed(name))
            return $"'{name}' is not written <area>.<quantity> in lower case";

        var area = AreaOf(name)!;
        return Areas.TryGetValue(templateId, out var areas) && areas.Contains(area, StringComparer.Ordinal) ? null : $"'{area}' is not an area of {templateId} ({(Areas.TryGetValue(templateId, out var a) ? string.Join(", ", a) : "none")})";
    }
}
