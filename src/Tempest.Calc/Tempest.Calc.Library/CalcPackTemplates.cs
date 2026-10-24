namespace Tempest.Calc.Library;

/// <summary>
/// Addendum A, §A7: the linked chains every build ships as pack templates. Steps name calculators by template id
/// (the Library does not reference the calculator assemblies); <see cref="CalcInstanceService.ValidatePackTemplate"/>
/// refuses a template whose calculators are not composed in.
/// </summary>
public static class CalcPackTemplates
{
    /// <summary>Drive train: motor (E1) → shaft (M4) → bearing (M5) → bearing seat fit (M2).</summary>
    public static CalcPackTemplate DriveTrain { get; } = new(
        "pack.drive-train", "Drive train",
        "Motor sizing gives the torque and speed the shaft carries; the shaft gives the bearing load and the seat diameter; the bearing life uses the shaft reaction and the motor speed; the seat fit uses the shaft diameter.",
        [
            new CalcPackStep("e1", "elec.motor-sizing", "Motor sizing"),
            new CalcPackStep("m4", "mech.shaft", "Shaft"),
            new CalcPackStep("m5", "mech.bearing-life", "Bearing A life"),
            new CalcPackStep("m2", "mech.iso286-fit", "Bearing A seat fit"),
        ],
        [
            new CalcPackBinding("e1", "T_rms", "m4", "T"),
            new CalcPackBinding("e1", "n_pk", "m4", "n"),
            new CalcPackBinding("m4", "R_A", "m5", "F_r"),
            new CalcPackBinding("e1", "n_pk", "m5", "n"),
            new CalcPackBinding("m4", "d_A", "m2", "D"),
        ]);

    /// <summary>Every built-in chain.</summary>
    public static IReadOnlyList<CalcPackTemplate> All { get; } = [DriveTrain];
}
