namespace Tempest.Calc.Library;

/// <summary>
/// Which record kinds a calculator cannot do without (Addendum C, C-3: "show on each calculator which required records are missing"). The
/// calculators' record kinds carry no such flag (adding one would change every template's hash), so it is stated here, once, and a test holds it to
/// the catalogue: every entry names a template and a kind that template declares. A kind that is not listed is <see cref="RecordNeed.Recommended"/>:
/// the calculator runs on a default and warns until a record is applied.
/// </summary>
public static class RecordRequirements
{
    private static readonly Dictionary<(string Template, string Kind), (RecordNeed Need, string Reason)> Table = new()
    {
        // Required: the calculator is blocked, or cannot judge, without a record (no default exists).
        [("elec.conductor-sizing", "cable.ampacity")] = (RecordNeed.Required, "E4 cannot size a conductor without the current-carrying capacities for the installation method."),
        [("elec.conductor-sizing", "device.ratings")] = (RecordNeed.Required, "E4 cannot pick a protective device without the preferred ratings of its range."),
        [("elec.containment", "containment.space-factor")] = (RecordNeed.Required, "E8 has no default fill limit and blocks until you enter one."),
        [("struct.load-combinations", "en1990.factors")] = (RecordNeed.Required, "S5 blocks without the psi factors; the partial factors come from the same record."),
        [("bs.room-heat-loss", "material.conductivity")] = (RecordNeed.Required, "B4 blocks until the construction layers are entered, unless you give a U-value override."),
        [("bs.glaser", "material.conductivity")] = (RecordNeed.Required, "B7 reads the thickness, conductivity and vapour resistance of every layer; it has no default."),
        [("bs.glaser", "climate.monthly")] = (RecordNeed.Required, "B7 needs all twelve months and ships no climate data."),
        [("bs.pipe-expansion", "pipe.allowable-range")] = (RecordNeed.Required, "B8 blocks without the allowable displacement-stress range Sa."),
        [("bs.pipe-expansion", "pipe.screening-k1")] = (RecordNeed.Required, "Without K1 the B8 screening check is not judged and the sheet does not pass."),
        [("mech.snap-fit", "snapfit.friction")] = (RecordNeed.Required, "M8 has no default friction coefficient; the assembly and separation forces need one."),
        [("mech.spring", "spring.wire-strength")] = (RecordNeed.Required, "M7 has no default wire strength (or take the curve from a material)."),
        [("mech.spring", "spring.endurance")] = (RecordNeed.Required, "Without the endurance strength the M7 fatigue result stays blocked."),

        // Conditional: needed for some configurations.
        [("elec.fault-protection", "device.characteristic")] = (RecordNeed.Conditional, "Needed for a fuse or an MCCB; MCBs of type B, C and D need no curve."),
        [("bs.glaser", "climate.humidity-class")] = (RecordNeed.Conditional, "Needed when the inside vapour load is taken from a humidity class (moisture-excess mode)."),
        [("mech.snap-fit", "snapfit.annular-factor")] = (RecordNeed.Conditional, "Needed for an annular snap-fit."),
        [("mech.snap-fit", "snapfit.annular-force-factor")] = (RecordNeed.Conditional, "Needed for an annular snap-fit; blank means 1."),
        [("elec.containment", "cable.pulling-limit")] = (RecordNeed.Conditional, "Needed when the pull is checked against the cable's limits."),
        [("elec.containment", "containment.friction")] = (RecordNeed.Conditional, "Needed when the pull tension is worked out."),
        [("elec.containment", "cable.bend-radius")] = (RecordNeed.Conditional, "Needed when the bends of the route are checked."),
        [("elec.containment", "containment.weight-correction")] = (RecordNeed.Conditional, "Needed for a cradled or triangular arrangement; 1 means full weight."),

        // Optional: an alternative or an extra.
        [("mech.snap-fit", "snapfit.secant-modulus")] = (RecordNeed.Optional, "An alternative to the secant curve of a material."),
        [("mech.shaft", "shaft.marin")] = (RecordNeed.Optional, "M4 works the surface factor out when you leave it blank."),
        [("bs.pipe-expansion", "bellows.data")] = (RecordNeed.Optional, "Only for a bellows expansion joint."),
        [("fp.cylinder", "cylinder.hose-sizes")] = (RecordNeed.Optional, "F1 picks the smallest bore from the list when one is given."),
        [("fp.accumulator", "accumulator.sizes")] = (RecordNeed.Optional, "F2 picks the size from the list when one is given."),
        [("elec.motor-sizing", "motor.rating")] = (RecordNeed.Optional, "A candidate motor's data; typed on the sheet when there is no record."),
        [("elec.fault-protection", "bs7671.max-zs")] = (RecordNeed.Optional, "A check of Z_s against the tabulated maximum; the derived Z_s,max is always checked."),
        [("elec.voltage-drop", "bs7671.voltage-drop")] = (RecordNeed.Optional, "An alternative to typing the mV/A/m figure on the mV/A/m route."),
        [("bs.room-heat-loss", "ground.u-values")] = (RecordNeed.Optional, "Only for a ground floor element without layers or a U override."),
    };

    /// <summary>How the calculator <paramref name="templateId"/> stands on the record kind <paramref name="kind"/>, with the reason.</summary>
    public static (RecordNeed Need, string Reason) For(string templateId, string kind) =>
        Table.TryGetValue((templateId, kind), out var entry) ? entry : (RecordNeed.Recommended, "The calculator runs on a default and warns until a record of this kind is applied.");

    /// <summary>Every explicit entry: the template id, the kind, how much it is needed and why.</summary>
    public static IEnumerable<(string Template, string Kind, RecordNeed Need, string Reason)> Entries => Table.Select(e => (e.Key.Template, e.Key.Kind, e.Value.Need, e.Value.Reason));
}
