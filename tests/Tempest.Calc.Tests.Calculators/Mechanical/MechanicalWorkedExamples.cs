using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

public static class MechanicalWorkedExamples
{
    private static CalcProvenance Book(string where) => CalcProvenance.Specification(where);

    public static IReadOnlyList<WorkedExample> All { get; } =
    [
        new(ToleranceStackCalculator.Id, "shouldered-screw-gap", "Shouldered-screw assembly gap (Shigley)",
            new CalcReferenceSource("Shigley's Mechanical Engineering Design", "9th ed.", "Ch. 1 tolerance stack-up example"),
            new CalcInputSet(
            [
                ToleranceStackCalculator.Table("in",
                    ("a shoulder length", "adds", 1.750, 0.003, -0.003),
                    ("b sleeve", "subtracts", 0.750, 0.001, -0.001),
                    ("c washer", "subtracts", 0.120, 0.005, -0.005),
                    ("d sleeve", "subtracts", 0.875, 0.001, -0.001)) with { Provenance = Book("Shigley 9e Ch. 1 example, part dimensions") },
                CalcInputValue.Quantity("minimum", 0.003, "in", Book("Shigley 9e Ch. 1 example, required gap")),
                CalcInputValue.Of("k", 3, CalcProvenance.Assumed("Each part at ±3σ within its band.")),
                CalcInputValue.Of("f_rss", 1.5, CalcProvenance.Assumed("Bender factor 1.5.")),
                CalcInputValue.Of("cpk_target", 1.33, CalcProvenance.Assumed("Usual capability target.")),
                CalcInputValue.Of("n_mc", 10_000, CalcProvenance.Assumed("Default sample count.")),
                CalcInputValue.Of("seed", 1, CalcProvenance.Assumed("Fixed seed for reproducibility.")),
                CalcInputValue.Choice("notation", "ASME Y14.5", CalcProvenance.Assumed("Inch drawing.")),
            ]),
            [new CalcExpectedValue("x_nom", 0.005, "in", 1e-9), new CalcExpectedValue("t_wc", 0.010, "in", 1e-9), new CalcExpectedValue("t_rss", 0.006, "in", 1e-6), new CalcExpectedValue("x_wc_min", -0.005, "in", 1e-6)],
            "Four inch-dimensioned parts on a shouldered screw. The worst-case gap can go negative (−0.005 in), so the stack fails the 0.003 in minimum; the RSS band ±0.006 in predicts most assemblies within 0.005 ± 0.006 in.",
            CalcReferenceStatus.Unconfirmed),

        new(ToleranceStackCalculator.Id, "bearing-housing-gap", "Bearing housing axial clearance (analytic)",
            new CalcReferenceSource("Analytic check", null, "RSS = √(0.05² + 0.06² + 0.02²) mm; C_pk from the nearer limit"),
            new CalcInputSet(
            [
                ToleranceStackCalculator.Table("mm",
                    ("Housing bore depth", "adds", 40, 0.10, 0),
                    ("Bearing width", "subtracts", 20, 0, -0.12),
                    ("Spacer", "subtracts", 19.5, 0.02, -0.02)) with { Provenance = CalcProvenance.Drawing("DRG-HSG-001", "B") },
                CalcInputValue.Quantity("minimum", 0.40, "mm", CalcProvenance.Specification("SPEC-BRG-7 §4.2")),
                CalcInputValue.Quantity("maximum", 0.80, "mm", CalcProvenance.Specification("SPEC-BRG-7 §4.2")),
                CalcInputValue.Of("k", 3, CalcProvenance.Assumed("Each part at ±3σ within its band.")),
                CalcInputValue.Of("f_rss", 1.5, CalcProvenance.Assumed("Bender factor 1.5.")),
                CalcInputValue.Of("cpk_target", 1.33, CalcProvenance.Assumed("Usual capability target.")),
                CalcInputValue.Of("n_mc", 10_000, CalcProvenance.Assumed("Default sample count.")),
                CalcInputValue.Of("seed", 1, CalcProvenance.Assumed("Fixed seed for reproducibility.")),
                CalcInputValue.Choice("notation", "BS 8888", CalcProvenance.Assumed("Metric drawing.")),
            ]),
            [new CalcExpectedValue("x_mean", 0.61, "mm", 1e-9), new CalcExpectedValue("t_wc", 0.13, "mm", 1e-9), new CalcExpectedValue("t_rss", 0.080622577, "mm", 1e-6), new CalcExpectedValue("cpk", 2.35666, "", 1e-5)],
            "Three metric parts with asymmetric deviations. Worst case 0.48–0.74 mm sits inside the 0.40–0.80 mm requirement with margins 0.08 and 0.06 mm; C_pk 2.36 against the 1.33 target."),

        new(Iso286FitCalculator.Id, "h7-g6-25mm", "25 mm H7/g6 sliding fit",
            new CalcReferenceSource("Machinery's Handbook", "30th ed.", "Preferred metric fits, 25 mm H7/g6: hole +0.021/0, shaft −0.007/−0.020, clearance 0.007…0.041 mm"),
            new CalcInputSet(
            [
                CalcInputValue.Quantity("D", 25, "mm", CalcProvenance.Drawing("DRG-SHAFT-12", "A")),
                CalcInputValue.Choice("hole_letter", "H", CalcProvenance.Specification("DRG-SHAFT-12 note 3: H7/g6")),
                CalcInputValue.Of("hole_grade", 7, CalcProvenance.Specification("DRG-SHAFT-12 note 3")),
                CalcInputValue.Choice("shaft_letter", "g", CalcProvenance.Specification("DRG-SHAFT-12 note 3")),
                CalcInputValue.Of("shaft_grade", 6, CalcProvenance.Specification("DRG-SHAFT-12 note 3")),
            ]),
            [new CalcExpectedValue("IT_hole", 21, "µm", 0.03), new CalcExpectedValue("es", -7, "µm", 0.06), new CalcExpectedValue("c_min", 7, "µm", 0.06), new CalcExpectedValue("c_max", 41, "µm", 0.03)],
            "A close-running fit from the formulae: IT7 = 20.9 µm, IT6 = 13.1 µm, g = −7.3 µm; clearance 7.3–41.3 µm against the tabulated 7–41 µm.",
            CalcReferenceStatus.Unconfirmed),

        new(Iso286FitCalculator.Id, "h7-p6-50mm", "50 mm H7/p6 interference (press) fit",
            new CalcReferenceSource("ISO 286-2:2010 (public excerpt)", null, "50 mm H7: +25/0 µm; p6: +42/+26 µm"),
            new CalcInputSet(
            [
                CalcInputValue.Quantity("D", 50, "mm", CalcProvenance.Drawing("DRG-HUB-04", "C")),
                CalcInputValue.Choice("hole_letter", "H", CalcProvenance.Specification("DRG-HUB-04: H7/p6")),
                CalcInputValue.Of("hole_grade", 7, CalcProvenance.Specification("DRG-HUB-04: H7/p6")),
                CalcInputValue.Choice("shaft_letter", "p", CalcProvenance.Specification("DRG-HUB-04: H7/p6")),
                CalcInputValue.Of("shaft_grade", 6, CalcProvenance.Specification("DRG-HUB-04: H7/p6")),
            ]),
            [new CalcExpectedValue("IT_hole", 25, "µm", 0.03), new CalcExpectedValue("i_max", 42, "µm", 0.05)],
            "An interference fit: the formula value p = IT7 = 25.0 µm sits 1 µm under the tabulated +26 µm, so the minimum interference reads 0 rather than 1 µm — the documented rounding difference; enter overrides from a released reference record where the table governs.",
            CalcReferenceStatus.Unconfirmed),

        new(PressFitCalculator.Id, "solid-steel-shaft", "50 mm solid steel shaft in a 100 mm steel hub",
            new CalcReferenceSource("Analytic (Shigley 9th ed. eq. 3-56 to 3-58)", null, "p = 62.1 MPa, hub hoop 103.5 MPa, F = 87.8 kN, T = 2195 N·m"),
            new CalcInputSet(
            [
                CalcInputValue.Quantity("d", 50, "mm", CalcProvenance.Drawing("DRG-HUB-04", "C")), CalcInputValue.Quantity("delta", 40, "µm", CalcProvenance.Drawing("DRG-HUB-04 (H7/p6 mean)", "C")),
                CalcInputValue.Quantity("d_o", 100, "mm", CalcProvenance.Drawing("DRG-HUB-04", "C")), CalcInputValue.Quantity("d_i", 0, "mm", CalcProvenance.Drawing("DRG-SHAFT-12", "A")),
                CalcInputValue.Quantity("L", 60, "mm", CalcProvenance.Drawing("DRG-HUB-04", "C")),
                CalcInputValue.Quantity("E_h", 207, "GPa", CalcProvenance.Specification("EN 10025 S355, E per textbook")), CalcInputValue.Of("nu_h", 0.292, CalcProvenance.Specification("steel")),
                CalcInputValue.Quantity("E_s", 207, "GPa", CalcProvenance.Specification("steel")), CalcInputValue.Of("nu_s", 0.292, CalcProvenance.Specification("steel")),
                CalcInputValue.Of("mu", 0.15, CalcProvenance.Assumed("Dry steel on steel, pressed; Shigley Table 7-? range 0.1–0.3.")),
                CalcInputValue.Quantity("S_y_h", 355, "MPa", CalcProvenance.Specification("EN 10025 S355")), CalcInputValue.Quantity("S_y_s", 355, "MPa", CalcProvenance.Specification("EN 10025 S355")),
                CalcInputValue.Quantity("alpha_h", 12e-6, "1/K", CalcProvenance.Specification("steel")), CalcInputValue.Quantity("c_a", 20, "µm", CalcProvenance.Assumed("Assembly clearance for a shrink fit.")),
            ]),
            [new CalcExpectedValue("p", 62.1, "MPa", 1e-3), new CalcExpectedValue("sigma_t_hub", 103.5, "MPa", 1e-3), new CalcExpectedValue("F_a", 87.80, "kN", 2e-3), new CalcExpectedValue("T", 2195, "N*m", 2e-3), new CalcExpectedValue("dT", 100, "K", 1e-6)],
            "Equal-stiffness steel parts: the interface pressure is 62 MPa, the hub bore hoop stress 104 MPa (von Mises 145 MPa, 41 % of yield), the press force 88 kN and the friction torque 2.2 kN·m. Shrinking the hub needs a 100 K rise for 20 µm of assembly clearance."),

        new(PressFitCalculator.Id, "hollow-shaft", "Hollow 50 mm shaft (25 mm bore) in a 100 mm hub",
            new CalcReferenceSource("Analytic (Lamé)", null, "p = 49.6 MPa; shaft surface hoop −82.7 MPa"),
            new CalcInputSet(
            [
                CalcInputValue.Quantity("d", 50, "mm", CalcProvenance.Drawing("DRG-HUB-04", "C")), CalcInputValue.Quantity("delta", 40, "µm", CalcProvenance.Drawing("DRG-HUB-04", "C")),
                CalcInputValue.Quantity("d_o", 100, "mm", CalcProvenance.Drawing("DRG-HUB-04", "C")), CalcInputValue.Quantity("d_i", 25, "mm", CalcProvenance.Drawing("DRG-SHAFT-13", "A")),
                CalcInputValue.Quantity("L", 60, "mm", CalcProvenance.Drawing("DRG-HUB-04", "C")),
                CalcInputValue.Quantity("E_h", 207, "GPa", CalcProvenance.Specification("steel")), CalcInputValue.Of("nu_h", 0.292, CalcProvenance.Specification("steel")),
                CalcInputValue.Quantity("E_s", 207, "GPa", CalcProvenance.Specification("steel")), CalcInputValue.Of("nu_s", 0.292, CalcProvenance.Specification("steel")),
                CalcInputValue.Of("mu", 0.15, CalcProvenance.Assumed("Dry steel on steel.")),
                CalcInputValue.Quantity("S_y_h", 355, "MPa", CalcProvenance.Specification("EN 10025 S355")), CalcInputValue.Quantity("S_y_s", 355, "MPa", CalcProvenance.Specification("EN 10025 S355")),
                CalcInputValue.Quantity("alpha_h", 12e-6, "1/K", CalcProvenance.Specification("steel")), CalcInputValue.Quantity("c_a", 20, "µm", CalcProvenance.Assumed("Assembly clearance.")),
            ]),
            [new CalcExpectedValue("p", 49.63, "MPa", 2e-3), new CalcExpectedValue("sigma_t_shaft", -82.7, "MPa", 2e-3)],
            "The bore makes the shaft more compliant, so the same interference gives 50 MPa instead of 62 MPa and the shaft surface sees −83 MPa hoop compression."),

        new(ThickCylinderCalculator.Id, "internal-pressure-closed", "Internal pressure, closed ends",
            new CalcReferenceSource("Analytic (Lamé), v0.16 calc.thick-walled-cylinder vector 1", null, "a = 50, b = 100 mm, p_i = 50 MPa"),
            new CalcInputSet(
            [
                CalcInputValue.Quantity("a", 50, "mm", CalcProvenance.Drawing("DRG-CYL-01", "A")), CalcInputValue.Quantity("b", 100, "mm", CalcProvenance.Drawing("DRG-CYL-01", "A")),
                CalcInputValue.Quantity("p_i", 50, "MPa", CalcProvenance.Specification("SPEC-PV-2 §3 design pressure")), CalcInputValue.Quantity("p_o", 0, "MPa", CalcProvenance.Assumed("Atmospheric outside.")),
                CalcInputValue.Switch("closed", true, CalcProvenance.Drawing("DRG-CYL-01 end caps", "A")), CalcInputValue.Quantity("sigma_allow", 200, "MPa", CalcProvenance.Specification("SPEC-PV-2 §5")),
            ]),
            [new CalcExpectedValue("sigma_t_a", 83.333, "MPa", 1e-4), new CalcExpectedValue("sigma_vm_a", 115.47, "MPa", 1e-4), new CalcExpectedValue("u", 0.57735, "", 1e-4)],
            "A 2:1 wall ratio under 50 MPa: bore hoop stress 83 MPa against a thin-wall estimate of 75 MPa; von Mises 115 MPa, 58 % of the allowable."),

        new(ThickCylinderCalculator.Id, "external-pressure-closed", "External pressure, closed ends",
            new CalcReferenceSource("Analytic (Lamé), v0.16 vector 2", null, "a = 40, b = 60 mm, p_o = 20 MPa"),
            new CalcInputSet(
            [
                CalcInputValue.Quantity("a", 40, "mm", CalcProvenance.Drawing("DRG-CYL-02", "B")), CalcInputValue.Quantity("b", 60, "mm", CalcProvenance.Drawing("DRG-CYL-02", "B")),
                CalcInputValue.Quantity("p_i", 0, "MPa", CalcProvenance.Assumed("Vented bore.")), CalcInputValue.Quantity("p_o", 20, "MPa", CalcProvenance.Specification("SPEC-SUB-1 §2 external pressure")),
                CalcInputValue.Switch("closed", true, CalcProvenance.Drawing("DRG-CYL-02", "B")), CalcInputValue.Quantity("sigma_allow", 100, "MPa", CalcProvenance.Specification("SPEC-SUB-1 §4")),
            ]),
            [new CalcExpectedValue("sigma_t_a", -72, "MPa", 1e-6), new CalcExpectedValue("sigma_vm_a", 62.354, "MPa", 1e-4), new CalcExpectedValue("u", 0.62354, "", 1e-4)],
            "External pressure puts the whole wall in compression: bore hoop −72 MPa, outer −52 MPa; von Mises 62 MPa at the bore, 62 % of the allowable."),

        new(BoltedJointCalculator.Id, "m12-88-flange", "M12 class 8.8 flange bolt, 80 N·m, 10 kN working load",
            new CalcReferenceSource("Analytic (VDI 2230 Part 1 steps R0–R9; Shigley eqs 8-17, 8-20)", null, "F_M = 40.69 kN; Φ = 0.0720; F_KR = 25.4 − 9.3 − 3.0 ≈ 13 kN"),
            JointInputs(80, "friction", 10),
            [new CalcExpectedValue("F_M", 40.69, "kN", 2e-3), new CalcExpectedValue("Phi", 0.07205, "", 3e-3)],
            "A steel flange clamped 40 mm thick by an M12 class 8.8 bolt torqued to 80 N·m with μ = 0.12: the bolt takes 7 % of the 10 kN working load, keeps about 13 kN of clamp after scatter and embedding, and sits well inside yield and the 50 MPa endurance amplitude."),

        new(BoltedJointCalculator.Id, "m12-88-overloaded", "The same bolt under a 60 kN working load (separation)",
            new CalcReferenceSource("Analytic (VDI 2230 R7)", null, "F_KR = F_M,min − (1 − Φ) F_A − F_Z < 0"),
            JointInputs(80, "friction", 60),
            [new CalcExpectedValue("F_M", 40.69, "kN", 2e-3)],
            "With a 60 kN working load the relieved member load exceeds the minimum preload: the residual clamp goes negative, the joint separates and the sheet fails the separation and clamp checks."),

        new(BoltGroupCalculator.Id, "four-bolt-bracket", "Four-bolt bracket, 50 kN at 150 mm eccentricity",
            new CalcReferenceSource("Analytic (elastic method; AISC Manual Part 7 procedure)", null, "governing bolt resultant 36.44 kN"),
            GroupInputs([(0, 0), (100, 0), (100, 100), (0, 100)], fy: -50, ex: 200, ey: 50, mx: 0, vr: 40, tr: 45),
            [new CalcExpectedValue("v_max", 36.443, "kN", 1e-3), new CalcExpectedValue("u_max", 0.91109, "", 1e-3)],
            "A 100 mm square pattern carrying 50 kN downward 150 mm from its centroid: 12.5 kN direct plus 26.5 kN torsional shear per bolt; the two bolts on the far side govern at 36.4 kN, 91 % of a 40 kN shear resistance."),

        new(BoltGroupCalculator.Id, "end-plate-moment", "End-plate with two bolt rows under 20 kN·m",
            new CalcReferenceSource("Analytic (elastic method)", null, "t = M_x y / Σ y² = 20e6 × 100 / 40000 per unit area = 50 kN per bolt"),
            GroupInputs([(0, -100), (100, -100), (0, 100), (100, 100)], fy: 0, ex: 0, ey: 0, mx: 20, vr: 40, tr: 60),
            [new CalcExpectedValue("t_max", 50, "kN", 1e-6), new CalcExpectedValue("u_max", 0.83333, "", 1e-4)],
            "Four bolts in two rows 200 mm apart under a 20 kN·m moment: the tension row sees 50 kN per bolt, 83 % of a 60 kN tension resistance; no shear."),
        new(BearingLifeCalculator.Id, "skf-6206-pump", "Pump shaft bearing, SKF 6206 at 1500 rpm, 95 % reliability",
            new CalcReferenceSource("SKF Rolling bearings catalogue (PUB BU/P1 17000)", "2018", "6206 ratings C = 20.3 kN, C_0 = 11.2 kN (distributor listing; check against SKF's own product page)"),
            new CalcInputSet(
            [
                CalcInputValue.Choice("type", BearingLifeCalculator.Ball, CalcProvenance.Assumed("Deep groove ball bearing.")),
                CalcInputValue.Quantity("F_r", 2.5, "kN", CalcProvenance.Assumed("Radial reaction from the shaft calculation.")),
                CalcInputValue.Quantity("F_a", 0, "kN", CalcProvenance.Assumed("Locating bearing elsewhere; no axial load here.")),
                CalcInputValue.Quantity("n", 1500, "rpm", CalcProvenance.Specification("Pump datasheet, rated speed")),
                CalcInputValue.Quantity("C", 20.3, "kN", Book("SKF catalogue, 6206")),
                CalcInputValue.Quantity("C_0", 11.2, "kN", Book("SKF catalogue, 6206")),
                CalcInputValue.Of("e_lim", 0.22, Book("Manufacturer factor for single-row deep groove bearings")),
                CalcInputValue.Of("X", 0.56, Book("Manufacturer factor for single-row deep groove bearings")),
                CalcInputValue.Of("Y", 2, Book("Manufacturer factor for single-row deep groove bearings")),
                CalcInputValue.Of("X_0", 0.6, Book("Manufacturer static factor")),
                CalcInputValue.Of("Y_0", 0.5, Book("Manufacturer static factor")),
                CalcInputValue.Quantity("R", 95, "%", CalcProvenance.Assumed("Client reliability target.")),
                CalcInputValue.Of("a_iso", 2, CalcProvenance.Assumed("Read from the manufacturer's life tool for clean ISO VG 68 oil.")),
                CalcInputValue.Quantity("L_req", 5000, "h", CalcProvenance.Specification("Pump specification, minimum bearing life")),
                CalcInputValue.Of("s_0_req", 1, CalcProvenance.Assumed("Normal running, smooth loads.")),
            ]),
            [new CalcExpectedValue("L_10", 535.3873, "", 1e-6), new CalcExpectedValue("L_10h", 5948.748, "h", 1e-6), new CalcExpectedValue("a_1", 0.637912, "", 1e-5), new CalcExpectedValue("L_nmh", 7589.6, "h", 1e-4), new CalcExpectedValue("s_0", 4.48, "", 1e-6)],
            "A 6206 deep groove ball bearing carries 2.5 kN radial at 1500 rpm. The basic life is 5949 h; at 95 % reliability with a_ISO = 2 the modified life is 7590 h, above the 5000 h required (utilisation 0.659). The ratings and factors would normally come from reference records; here they cite the catalogue directly.",
            CalcReferenceStatus.Unconfirmed),
    ];

    private static CalcInputSet JointInputs(double torque, string method, double workingLoad) => new(
    [
        CalcInputValue.Quantity("d", 12, "mm", CalcProvenance.Drawing("DRG-FLG-02", "A")), CalcInputValue.Quantity("P", 1.75, "mm", CalcProvenance.Specification("ISO 261 coarse pitch")),
        CalcInputValue.Quantity("R_p02", 640, "MPa", CalcProvenance.Specification("ISO 898-1 class 8.8")), CalcInputValue.Quantity("E_S", 205, "GPa", CalcProvenance.Specification("steel")),
        CalcInputValue.Quantity("sigma_A", 50, "MPa", CalcProvenance.Assumed("Rolled-before-heat-treatment M12, VDI 2230 order of magnitude.")),
        CalcInputValue.Quantity("l_k", 40, "mm", CalcProvenance.Drawing("DRG-FLG-02", "A")), CalcInputValue.Quantity("d_h", 13, "mm", CalcProvenance.Drawing("DRG-FLG-02", "A")),
        CalcInputValue.Quantity("d_w", 18, "mm", CalcProvenance.Specification("ISO 4762 head bearing")), CalcInputValue.Quantity("E_P", 205, "GPa", CalcProvenance.Specification("steel")),
        CalcInputValue.Of("n", 0.5, CalcProvenance.Assumed("Load introduced at mid-thickness.")), CalcInputValue.Quantity("f_Z", 11, "µm", CalcProvenance.Assumed("Three interfaces, machined.")),
        CalcInputValue.Choice("method", method, CalcProvenance.Specification("Assembly procedure AP-7")), CalcInputValue.Quantity("M_A", torque, "N*m", CalcProvenance.Specification("Assembly procedure AP-7")),
        CalcInputValue.Of("K", 0.2, CalcProvenance.Assumed("Not used by the friction method.")), CalcInputValue.Of("mu_G", 0.12, CalcProvenance.Assumed("Lightly oiled zinc-plated thread.")),
        CalcInputValue.Of("mu_K", 0.12, CalcProvenance.Assumed("Lightly oiled head bearing.")), CalcInputValue.Of("alpha_A", 1.6, CalcProvenance.Assumed("Torque wrench, VDI 2230 Table A8 order.")),
        CalcInputValue.Quantity("F_A", workingLoad, "kN", CalcProvenance.Specification("LOAD-CASE-3")), CalcInputValue.Quantity("F_KR_req", 5, "kN", CalcProvenance.Specification("SPEC-FLG §6 sealing")),
        CalcInputValue.Quantity("tau_B", 400, "MPa", CalcProvenance.Assumed("Nut material shear strength.")), CalcInputValue.Quantity("m_eff", 12, "mm", CalcProvenance.Drawing("DRG-FLG-02", "A")),
        CalcInputValue.Of("S_strip", 1.25, CalcProvenance.Assumed("Stripping safety factor.")),
    ]);

    private static CalcInputSet GroupInputs((double X, double Y)[] points, double fy, double ex, double ey, double mx, double vr, double tr) => new(
    [
        CalcInputValue.Table("bolts", points.Select(p => CalcRow.Of(("x", new CalcCell(p.X, "mm")), ("y", new CalcCell(p.Y, "mm")), ("A", new CalcCell(84.3, "mm^2")))).ToList(), CalcProvenance.Drawing("DRG-BRK-09", "B")),
        CalcInputValue.Quantity("F_x", 0, "kN", CalcProvenance.Specification("LOAD-CASE-1")), CalcInputValue.Quantity("F_y", fy, "kN", CalcProvenance.Specification("LOAD-CASE-1")),
        CalcInputValue.Quantity("e_x", ex, "mm", CalcProvenance.Drawing("DRG-BRK-09", "B")), CalcInputValue.Quantity("e_y", ey, "mm", CalcProvenance.Drawing("DRG-BRK-09", "B")),
        CalcInputValue.Quantity("T_z", 0, "kN*m", CalcProvenance.Specification("LOAD-CASE-1")), CalcInputValue.Quantity("N", 0, "kN", CalcProvenance.Specification("LOAD-CASE-1")),
        CalcInputValue.Quantity("M_x", mx, "kN*m", CalcProvenance.Specification("LOAD-CASE-1")), CalcInputValue.Quantity("M_y", 0, "kN*m", CalcProvenance.Specification("LOAD-CASE-1")),
        CalcInputValue.Quantity("V_R", vr, "kN", CalcProvenance.Specification("M12 8.8 shear resistance, EN 1993-1-8 Table 3.4 per project calc")), CalcInputValue.Quantity("T_R", tr, "kN", CalcProvenance.Specification("M12 8.8 tension resistance per project calc")),
    ]);
}
