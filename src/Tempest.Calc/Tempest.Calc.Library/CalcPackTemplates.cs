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

    /// <summary>
    /// Motor circuit: motor (E1) → load budget (E6) → cable (E4) → voltage drop (E3) and fault protection (E5) →
    /// enclosure heat (E2). E1's peak power enters E6 as a linked load; E6's bus current sizes the cable; the cable's
    /// size, length and device feed E3 and E5; E6's dissipated heat enters E2.
    /// </summary>
    public static CalcPackTemplate MotorCircuit { get; } = new(
        "pack.motor-circuit", "Motor circuit",
        "Sizes a motor's supply from the motor duty to the enclosure: demand, cable and device, voltage drop, earth-fault disconnection and the heat the panel must lose.",
        [
            new CalcPackStep("e1", "elec.motor-sizing", "Motor sizing"),
            new CalcPackStep("e6", "elec.load-budget", "Load budget"),
            new CalcPackStep("e4", "elec.conductor-sizing", "Cable and device"),
            new CalcPackStep("e3", "elec.voltage-drop", "Voltage drop"),
            new CalcPackStep("e5", "elec.fault-protection", "Fault protection"),
            new CalcPackStep("e2", "elec.enclosure-heat", "Enclosure heat"),
        ],
        [
            new CalcPackBinding("e1", "P_pk", "e6", "P_add"),
            new CalcPackBinding("e6", "I_3ph", "e4", "I_b_in"),
            new CalcPackBinding("e4", "I_b", "e3", "I"),
            new CalcPackBinding("e4", "L", "e3", "L"),
            new CalcPackBinding("e4", "CSA", "e3", "A_c"),
            new CalcPackBinding("e4", "CSA", "e5", "S_line"),
            new CalcPackBinding("e4", "L", "e5", "L"),
            new CalcPackBinding("e4", "I_n", "e5", "I_n"),
            new CalcPackBinding("e6", "Phi", "e2", "P_add"),
        ]);

    /// <summary>Every built-in chain.</summary>
    public static IReadOnlyList<CalcPackTemplate> All { get; } = [DriveTrain, MotorCircuit];
}
