using Tempest.Calc.BuildingServices;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.BuildingServices;

public static class BuildingServicesWorkedExamples
{
    public static IReadOnlyList<WorkedExample> All { get; } =
    [
        new(PipeFlowCalculator.Id, "chilled-water-riser-duty", "Chilled-water riser: pressure drop, duty point, speed turn-down",
            new CalcReferenceSource("Analytic: parabolic pump curve H = 40 − 0.01 Q² against the system curve", null, "Duty where H_pump = H_sys; affinity shift at n₂/n₁ = 0.9"),
            new CalcInputSet(
            [
                CalcInputValue.Choice("fluid", PipeFlowCalculator.FluidWater, CalcProvenance.Specification("SPEC-M-04 §3: chilled water, no glycol")),
                CalcInputValue.Quantity("T", 6, "degC", CalcProvenance.Specification("SPEC-M-04 §3: 6 °C flow")),
                CalcInputValue.Quantity("Q", 30, "m^3/h", CalcProvenance.Specification("Schedule CHW-01 design flow")),
                PipeFlowCalculator.Segments(("plantroom suction", 8, 100, 0.045, 3, 0, 0, "suction"), ("riser DN80", 60, 80, 0.045, 8, 0, 10, "discharge"), ("index run DN65", 45, 65, 0.045, 12, 6, 2, "discharge")) with { Provenance = CalcProvenance.Drawing("DRG-M-110", "C") },
                CalcInputValue.Quantity("h_out", 3, "m", CalcProvenance.Specification("Terminal unit: 30 kPa ≈ 3 m")),
                CalcInputValue.Quantity("v_max", 2.5, "m/s", CalcProvenance.Specification("CIBSE Guide C practice limit, assumed")),
                CalcInputValue.VectorOf("Q_pump", [0, 20, 40, 60], "m^3/h", CalcProvenance.Specification("Pump datasheet P-01 rev 2, curve points")),
                CalcInputValue.VectorOf("H_pump", [40, 36, 24, 4], "m", CalcProvenance.Specification("Pump datasheet P-01 rev 2, curve points")),
                CalcInputValue.Of("n_ratio", 0.9, CalcProvenance.Assumed("Inverter turn-down to 90 % speed.")),
                CalcInputValue.Of("D_ratio", 1, CalcProvenance.Assumed("Impeller unchanged.")),
                CalcInputValue.Of("eta_pump", 0.68, CalcProvenance.Specification("Pump datasheet P-01 rev 2")),
                CalcInputValue.Quantity("p_atm", 101.325, "kPa", CalcProvenance.Assumed("Sea level.")),
                CalcInputValue.Quantity("z_s", 2, "m", CalcProvenance.Drawing("DRG-M-110", "C")),
                CalcInputValue.Quantity("NPSH_r", 3, "m", CalcProvenance.Specification("Pump datasheet P-01 rev 2")),
            ]),
            [new CalcExpectedValue("H_static", 15, "m", 1e-9)],
            "Three series segments with K fittings, a 12 m rise and 3 m terminal head. The sheet reports each segment's velocity, Re, Colebrook f (with the Swamee–Jain cross-check) and loss, the system curve, the duty point on the datasheet curve, the duty after a 90 % speed turn-down, shaft power and NPSH available against the pump's requirement."),

        new(PipeFlowCalculator.Id, "oil-transfer-laminar", "Lube-oil transfer line, laminar flow",
            new CalcReferenceSource("Çengel & Cimbala, Fluid Mechanics", "4th ed.", "Ch. 8 laminar pipe flow: Δp = 32 μ L v / D²"),
            new CalcInputSet(
            [
                CalcInputValue.Choice("fluid", PipeFlowCalculator.FluidCustom, CalcProvenance.Specification("Oil datasheet ISO VG 680 at 20 °C")),
                CalcInputValue.Quantity("T", 20, "degC", CalcProvenance.Assumed("Ambient 20 °C.")),
                CalcInputValue.Quantity("rho_custom", 888, "kg/m^3", CalcProvenance.Specification("Oil datasheet ISO VG 680")),
                CalcInputValue.Quantity("mu_custom", 800, "mPa*s", CalcProvenance.Specification("Oil datasheet ISO VG 680 at 20 °C")),
                CalcInputValue.Quantity("Q", 1, "m^3/h", CalcProvenance.Specification("Transfer pump duty")),
                PipeFlowCalculator.Segments(("transfer line", 40, 50, 0.0, 0, 0, 0, "discharge")) with { Provenance = CalcProvenance.Drawing("DRG-L-020", "A") },
                CalcInputValue.Quantity("h_out", 0, "m", CalcProvenance.Assumed("Open discharge.")),
                CalcInputValue.Quantity("v_max", 3, "m/s", CalcProvenance.Assumed("Practice limit.")),
            ]),
            [new CalcExpectedValue("Re_s1", 7.852, "", 1e-3), new CalcExpectedValue("dp_total", 57.946, "kPa", 1e-3)],
            "Viscous oil at Re ≈ 8: the sheet flags laminar flow, uses f = 64/Re and reproduces Hagen–Poiseuille (Δp = 32 μ L v / D² = 57.9 kPa) exactly. Roughness plays no part in laminar flow."),
    ];
}
