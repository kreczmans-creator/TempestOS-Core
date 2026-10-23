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

        new(PsychrometricsCalculator.Id, "cooling-coil-summer", "Summer cooling coil: 28 °C / 60 % to 12 °C / 95 %",
            new CalcReferenceSource("ASHRAE Handbook — Fundamentals", "2017", "Ch. 1, cooling and dehumidification process (energy and mass balance form)"),
            new CalcInputSet(
            [
                CalcInputValue.Quantity("altitude", 0, "m", CalcProvenance.Assumed("Sea-level site.")),
                CalcInputValue.Quantity("t_db", 28, "degC", CalcProvenance.Specification("CIBSE Guide A summer design condition for the site, assumed")),
                CalcInputValue.Choice("known", PsychrometricsCalculator.KnownRh, CalcProvenance.Assumed("RH given.")),
                CalcInputValue.Of("RH", 60, CalcProvenance.Specification("Design condition")),
                CalcInputValue.Quantity("m_da", 2.4, "kg/s", CalcProvenance.Specification("AHU schedule AHU-01: 2.0 m³/s ≈ 2.4 kg/s dry air")),
                CalcInputValue.Choice("process", PsychrometricsCalculator.ProcessCoolingDehum, CalcProvenance.Assumed("Chilled-water coil.")),
                CalcInputValue.Quantity("t_2", 12, "degC", CalcProvenance.Specification("AHU schedule AHU-01 off-coil 12 °C")),
                CalcInputValue.Of("RH_2", 95, CalcProvenance.Assumed("Off-coil 95 % RH (typical contact factor).")),
            ]),
            [new CalcExpectedValue("t_2_out", 12, "degC", 1e-9), new CalcExpectedValue("RH_2", 95, "", 1e-9)],
            "Entering 28 °C / 60 % (W ≈ 14.3 g/kg) leaves the coil at 12 °C / 95 % (W ≈ 8.3 g/kg): the sheet gives the condensate rate, total, sensible and latent loads and the sensible heat ratio for the coil selection."),

        new(PsychrometricsCalculator.Id, "winter-mixing-and-steam", "Winter: mix return and outdoor air, then steam-humidify",
            new CalcReferenceSource("Analytic mass and energy balance (ASHRAE Ch. 1 mixing and humidification relations)", null, "W₃ and h₃ mass-weighted; steam adds (W₂ − W₁) h_steam"),
            new CalcInputSet(
            [
                CalcInputValue.Quantity("altitude", 120, "m", CalcProvenance.Drawing("Site survey SRV-01: 120 m AOD")),
                CalcInputValue.Quantity("t_db", 22, "degC", CalcProvenance.Specification("Return air 22 °C / 40 %")),
                CalcInputValue.Choice("known", PsychrometricsCalculator.KnownRh, CalcProvenance.Assumed("RH given.")),
                CalcInputValue.Of("RH", 40, CalcProvenance.Specification("Return air 22 °C / 40 %")),
                CalcInputValue.Quantity("m_da", 3, "kg/s", CalcProvenance.Specification("AHU-02 return 3 kg/s")),
                CalcInputValue.Choice("process", PsychrometricsCalculator.ProcessMixing, CalcProvenance.Assumed("Mixing box.")),
                CalcInputValue.Quantity("t_db_b", -3, "degC", CalcProvenance.Specification("CIBSE Guide A winter design −3 °C / 90 %, assumed")),
                CalcInputValue.Of("RH_b", 90, CalcProvenance.Specification("Winter design condition")),
                CalcInputValue.Quantity("m_da_b", 1, "kg/s", CalcProvenance.Specification("AHU-02 minimum outdoor air 1 kg/s")),
            ]),
            [new CalcExpectedValue("m_da_3", 4, "kg/s", 1e-9)],
            "Three parts return air at 22 °C / 40 % mixed with one part outdoor air at −3 °C / 90 % (over ice): the mixed state follows the mass-weighted W and h. A second run of the sheet with the steam process takes the mixed state to the supply humidity."),

        new(DuctSizingCalculator.Id, "office-supply-equal-friction", "Office supply ductwork by equal friction at 1 Pa/m",
            new CalcReferenceSource("ASHRAE Handbook — Fundamentals", "2017", "Ch. 21 equal-friction method; equivalent diameters by the Ch. 21 relations"),
            new CalcInputSet(
            [
                CalcInputValue.Choice("method", DuctSizingCalculator.MethodEqualFriction, CalcProvenance.Assumed("Equal friction for a low-pressure system.")),
                CalcInputValue.Quantity("dp_target", 1.0, "Pa/m", CalcProvenance.Specification("SPEC-M-07 §5: 1 Pa/m design friction rate")),
                CalcInputValue.Quantity("T", 20, "degC", CalcProvenance.Assumed("Supply air 20 °C.")),
                CalcInputValue.Quantity("p_atm", 101.325, "kPa", CalcProvenance.Assumed("Sea level.")),
                CalcInputValue.Quantity("eps", 0.09, "mm", CalcProvenance.Specification("Galvanised sheet, medium roughness (ASHRAE Ch. 21 category)")),
                DuctSizingCalculator.Sections(("AHU to riser", 2.0, 15, DuctSizingCalculator.ShapeRectangular, 800, 0, 0.8, 0, true), ("riser", 2.0, 12, DuctSizingCalculator.ShapeRound, 0, 0, 0.3, 0, true), ("floor main", 1.2, 25, DuctSizingCalculator.ShapeRectangular, 500, 0, 1.1, 0, true), ("branch to VAV", 0.4, 10, DuctSizingCalculator.ShapeRound, 0, 0, 0.9, 60, true), ("other branch", 0.8, 18, DuctSizingCalculator.ShapeRound, 0, 0, 1.0, 60, false)) with { Provenance = CalcProvenance.Drawing("DRG-M-205", "B") },
                CalcInputValue.Quantity("v_limit", 8, "m/s", CalcProvenance.Specification("SPEC-M-07 §5: 8 m/s in risers")),
                CalcInputValue.Quantity("dp_limit", 250, "Pa", CalcProvenance.Specification("Fan external static allowance 250 Pa")),
                CalcInputValue.Quantity("round_to", 50, "mm", CalcProvenance.Specification("BS EN 1505 / 1506 preferred sizes, 50 mm steps")),
            ]),
            [new CalcExpectedValue("dp_fit_s4", 0, "Pa", 1e9)],
            "Five sections sized at 1 Pa/m with the AHU main and floor main rectangular on fixed depths. The index run (four sections) adds friction, fittings (ΣC from the manufacturer's data) and the 60 Pa VAV terminal; the total is checked against the fan's external static allowance and every section against the 8 m/s limit."),

        new(DuctSizingCalculator.Id, "extract-velocity-method", "Workshop extract by the velocity method",
            new CalcReferenceSource("Analytic", null, "D = √(4Q/πv) rounded up to the next 50 mm; Δp/L by Darcy–Weisbach / Colebrook"),
            new CalcInputSet(
            [
                CalcInputValue.Choice("method", DuctSizingCalculator.MethodVelocity, CalcProvenance.Assumed("Velocity method for an extract system.")),
                CalcInputValue.Quantity("v_target", 10, "m/s", CalcProvenance.Specification("Dust-carrying extract: 10 m/s minimum transport velocity, SPEC-M-09")),
                CalcInputValue.Quantity("T", 25, "degC", CalcProvenance.Assumed("Workshop air 25 °C.")),
                CalcInputValue.Quantity("p_atm", 100.5, "kPa", CalcProvenance.Assumed("Site 70 m AOD, ≈ 100.5 kPa.")),
                CalcInputValue.Quantity("eps", 0.15, "mm", CalcProvenance.Assumed("Spiral-wound galvanised duct with joints.")),
                DuctSizingCalculator.Sections(("hood branch", 0.35, 6, DuctSizingCalculator.ShapeRound, 0, 0, 1.5, 120, true), ("main", 1.05, 20, DuctSizingCalculator.ShapeRound, 0, 0, 0.6, 0, true), ("stack", 1.05, 8, DuctSizingCalculator.ShapeRound, 0, 0, 1.0, 30, true)) with { Provenance = CalcProvenance.Drawing("DRG-M-310", "A") },
                CalcInputValue.Quantity("v_limit", 15, "m/s", CalcProvenance.Specification("SPEC-M-09 noise limit")),
                CalcInputValue.Quantity("dp_limit", 600, "Pa", CalcProvenance.Specification("Extract fan external static 600 Pa")),
                CalcInputValue.Quantity("round_to", 50, "mm", CalcProvenance.Specification("BS EN 1506 preferred sizes")),
            ]),
            [new CalcExpectedValue("D_s1", 250, "mm", 1e-9), new CalcExpectedValue("D_s2", 400, "mm", 1e-9)],
            "Sizing at 10 m/s transport velocity rounds the hood branch to 250 mm and the main and stack to 400 mm; the actual velocities, friction rates and the index-run total (with the 120 Pa hood entry and 30 Pa cowl) follow, against the fan's 600 Pa."),
    ];
}
