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
        new(HeatExchangerCalculator.Id, "lthw-plate-hx-sizing", "District heating plate exchanger sizing, counterflow",
            new CalcReferenceSource("Analytic (ε-NTU sizing; Incropera 7th ed. Table 11.4)", "7th ed.", "Primary 1.2 kg/s from 80 °C, secondary 1.5 kg/s from 40 °C, 120 kW, U 3000 W/m²K with 0.0001 m²K/W fouling"),
            new CalcInputSet(
            [
                CalcInputValue.Choice("arrangement", HeatExchangerCalculator.Counterflow, CalcProvenance.Assumed("Plate exchanger treated as pure counterflow.")),
                CalcInputValue.Choice("mode", HeatExchangerCalculator.Sizing, CalcProvenance.Assumed("Sizing a new exchanger.")),
                CalcInputValue.Quantity("m_h", 1.2, "kg/s", CalcProvenance.Specification("Network connection agreement, primary flow")),
                CalcInputValue.Quantity("cp_h", 4190, "J/(kg*K)", CalcProvenance.Assumed("Water at about 70 °C.")),
                CalcInputValue.Quantity("T_h_in", 80, "degC", CalcProvenance.Specification("Network supply temperature")),
                CalcInputValue.Quantity("m_c", 1.5, "kg/s", CalcProvenance.Drawing("M-SCH-201", "B")),
                CalcInputValue.Quantity("cp_c", 4180, "J/(kg*K)", CalcProvenance.Assumed("Water at about 50 °C.")),
                CalcInputValue.Quantity("T_c_in", 40, "degC", CalcProvenance.Drawing("M-SCH-201", "B")),
                CalcInputValue.Quantity("U", 3000, "W/(m^2*K)", CalcProvenance.Assumed("Typical clean plate exchanger, water/water.")),
                CalcInputValue.Quantity("R_f", 0.0001, "m^2*K/W", CalcProvenance.Assumed("Treated closed circuits on both sides.")),
                CalcInputValue.Quantity("Q_req", 120, "kW", CalcProvenance.Specification("Building peak heat load")),
                CalcInputValue.Quantity("dT_min", 3, "K", CalcProvenance.Assumed("Practical approach for a plate exchanger.")),
            ]),
            [new CalcExpectedValue("eps", 0.5966587, "", 1e-6), new CalcExpectedValue("NTU", 1.297344, "", 1e-5), new CalcExpectedValue("A_req", 2.826653, "m^2", 1e-5), new CalcExpectedValue("T_h_out", 56.13365, "degC", 1e-6), new CalcExpectedValue("T_c_out", 59.13876, "degC", 1e-6), new CalcExpectedValue("dT_app", 16.13365, "K", 1e-5)],
            "The secondary circuit takes 120 kW from a 1.2 kg/s primary at 80 °C. ε = 0.597 and NTU = 1.30 give 2.83 m² at U_f = 2307.7 W/m²K. The primary leaves at 56.1 °C and the secondary at 59.1 °C; the approach of 16.1 K clears the 3 K minimum.",
            CalcReferenceStatus.Confirmed),
        new(SealedSystemCalculator.Id, "heat-pump-retrofit-55-45", "Heat pump retrofit at 55/45 °C: existing radiators and expansion vessel",
            new CalcReferenceSource("Analytic (characteristic equation; Kell 1975 density; vessel relation)", null, "Five rooms with their existing radiators re-rated from ΔT 50 K to 55/45/21 °C; 12 L vessel at 7 m static"),
            new CalcInputSet(
            [
                SealedSystemCalculator.Rooms(("Lounge", 1800, 3600, 12), ("Kitchen", 1100, 2200, 8), ("Bed 1", 900, 1800, 7), ("Bed 2", 700, 1400, 5), ("Bathroom", 600, 1100, 4)) with { Provenance = CalcProvenance.Specification("Room heat loss calculation (B4) and radiator survey") },
                CalcInputValue.Quantity("t_f", 55, "degC", CalcProvenance.Specification("Heat pump design flow temperature")),
                CalcInputValue.Quantity("t_r", 45, "degC", CalcProvenance.Specification("Heat pump design return temperature")),
                CalcInputValue.Quantity("t_i", 21, "degC", CalcProvenance.Specification("Living-room design temperature")),
                CalcInputValue.Of("n", 1.3, CalcProvenance.Assumed("Panel radiators, manufacturer exponent about 1.3.")),
                CalcInputValue.Choice("dT_method", SealedSystemCalculator.Amtd, CalcProvenance.Assumed("Catalogue ratings use the arithmetic mean.")),
                CalcInputValue.Quantity("V_pipe", 40, "L", CalcProvenance.Assumed("15 mm and 22 mm copper, about 120 m.")),
                CalcInputValue.Quantity("V_plant", 15, "L", CalcProvenance.Specification("Heat pump and plate exchanger content, datasheet")),
                CalcInputValue.Quantity("t_fill", 10, "degC", CalcProvenance.Assumed("Mains water at fill.")),
                CalcInputValue.Quantity("t_max", 60, "degC", CalcProvenance.Specification("Heat pump maximum flow temperature")),
                CalcInputValue.Quantity("h_static", 7, "m", CalcProvenance.Drawing("M-SEC-001", "A")),
                CalcInputValue.Quantity("p_margin", 0.3, "bar", CalcProvenance.Assumed("Usual margin over static head.")),
                CalcInputValue.Quantity("p_f", 2.5, "bar", CalcProvenance.Assumed("0.5 bar below the safety valve.")),
                CalcInputValue.Quantity("p_prv", 3, "bar", CalcProvenance.Specification("Heat pump safety valve")),
                CalcInputValue.Quantity("V_vessel", 12, "L", CalcProvenance.Specification("Site survey: existing 12 L vessel")),
            ]),
            [new CalcExpectedValue("f_dT", 0.4925569, "", 1e-6), new CalcExpectedValue("R1", 3654.400, "W", 1e-6), new CalcExpectedValue("u_emit", 1.107394, "", 1e-6), new CalcExpectedValue("V_s", 91, "L", 1e-9), new CalcExpectedValue("V_e", 3.531184, "L", 1e-6)],
            "At 55/45 °C with rooms at 21 °C the radiators give 49 % of their catalogue output. The lounge, kitchen and bedrooms fall 1.5 % short and the bathroom 11 % short (utilisation 1.107), so the check fails and those radiators need upsizing. The 91 L system needs a 3.5 L vessel, so the existing 12 L vessel is ample.",
            CalcReferenceStatus.Confirmed),
        new(RoomHeatLossCalculator.Id, "semi-detached-living-room", "Semi-detached living room: U-values and design heat loss for the emitter sizing",
            new CalcReferenceSource("Analytic (series resistance; steady-state Φ = ΣA·U·f·Δθ + y_tb·Σ(A·f)_ext·Δθ + 0.34·n·V·Δθ)", null, "Semi-detached living room 4.5 × 3.6 × 2.4 m: front wall with a 2.1 m² window, gable wall, party wall to a neighbour at 18 °C, ground floor with a user-entered U; all material data are invented illustrative inputs, not design data"),
            new CalcInputSet(
            [
                CalcInputValue.Choice("method", RoomHeatLossCalculator.En12831, CalcProvenance.Assumed("Elemental method; the design-team default.")),
                CalcInputValue.Quantity("theta_int", 21, "degC", CalcProvenance.Specification("SPEC-A-02 §3.1: living-room design temperature")),
                CalcInputValue.Quantity("theta_e", -3, "degC", CalcProvenance.Assumed("Illustrative location design temperature; take the project's own from its design-temperature record.")),
                RoomHeatLossCalculator.Layers(("Cavity wall", "Outer brick leaf", 102.5, 0.84), ("Cavity wall", "Insulation", 100, 0.03), ("Cavity wall", "Inner block leaf", 100, 0.2), ("Cavity wall", "Plaster", 13, 0.4), ("Party wall", "Plaster", 13, 0.4), ("Party wall", "Block leaf 1", 100, 0.6), ("Party wall", "Block leaf 2", 100, 0.6), ("Party wall", "Plaster", 13, 0.4)) with { Provenance = CalcProvenance.Drawing("A-210", "C") },
                RoomHeatLossCalculator.WithConstructions(RoomHeatLossCalculator.Elements(("Front wall", 8.7, null, null, null, null, null, null, null), ("Window", 2.1, null, null, null, null, null, 1.4, null), ("Gable wall", 8.64, null, null, null, null, null, null, null), ("Party wall", 8.64, 18, null, null, null, 0.18, null, 1.2), ("Ground floor", 16.2, null, null, null, null, null, 0.25, null)), "Cavity wall", null, "Cavity wall") with { Provenance = CalcProvenance.Drawing("A-101", "C") },
                CalcInputValue.Quantity("y_tb", 0.1, "W/(m^2*K)", CalcProvenance.Assumed("Illustrative thermal-bridge allowance; replace with the project's bridging record.")),
                CalcInputValue.Quantity("U_target", 0.3, "W/(m^2*K)", CalcProvenance.Specification("SPEC-A-02 §4.2: external wall U-value target")),
                CalcInputValue.Quantity("n", 0.8, "1/h", CalcProvenance.Assumed("Natural ventilation allowance for a living room.")),
                CalcInputValue.Quantity("V", 38.88, "m^3", CalcProvenance.Drawing("A-101", "C")),
                CalcInputValue.Quantity("A_floor", 16.2, "m^2", CalcProvenance.Drawing("A-101", "C")),
            ]),
            [new CalcExpectedValue("U_1", 0.2405085, "W/(m^2*K)", 1e-6), new CalcExpectedValue("Phi_1", 50.21818, "W", 1e-6), new CalcExpectedValue("Phi_2", 70.56, "W", 1e-9), new CalcExpectedValue("Phi_3", 49.87184, "W", 1e-6), new CalcExpectedValue("U_4", 1.192843, "W/(m^2*K)", 1e-6), new CalcExpectedValue("Phi_4", 30.91849, "W", 1e-6), new CalcExpectedValue("Phi_5", 97.2, "W", 1e-9), new CalcExpectedValue("Phi_tb", 85.536, "W", 1e-9), new CalcExpectedValue("Phi_T", 384.3045, "W", 1e-6), new CalcExpectedValue("Phi_V", 253.8086, "W", 1e-6), new CalcExpectedValue("Phi_HL", 638.1131, "W", 1e-6), new CalcExpectedValue("HLC", 26.58805, "W/K", 1e-6), new CalcExpectedValue("Phi_A", 39.38970, "W/m^2", 1e-6)],
            "The cavity wall has U = 0.241 W/m²K (R = 4.158 m²K/W) and passes the 0.30 target with utilisation 0.80; the party wall (U = 1.193) loses only to the 18 °C neighbour through a 3 K difference and is held to its own 1.2 target (utilisation 0.99). Fabric loss 384 W includes 85.5 W of thermal-bridge allowance on the 35.64 m² facing outside; ventilation at 0.8 h⁻¹ adds 253.8 W. The room load Φ_HL = 638.1 W (HLC 26.59 W/K, 39.4 W/m²) is the figure to carry to the emitter selection in the sealed-system calculation.",
            CalcReferenceStatus.Confirmed),

        new(GlaserCalculator.Id, "internal-insulation-no-vcl", "Solid brick wall with internal mineral wool and no vapour control layer: interstitial condensation over a year",
            new CalcReferenceSource("Analytic (Glaser method: series resistance, Magnus saturation pressure, tangent construction, g = δ0·Δp/Δs_d; recomputed in python3 with an independent obstacle-problem solve)", null, "Plasterboard 12.5 mm (λ 0.25, μ 8), mineral wool 50 mm (λ 0.035, μ 1) and existing solid brick 215 mm (λ 0.8, μ 10) at 20 °C / 50 % inside; the monthly climate is invented (a maritime year, not ISO 13788 data) and every material value is an invented illustrative input, not design data"),
            GlaserInputs(GlaserCalculator.Layers(("", "Plasterboard", 12.5, 0.25, 8), ("", "Mineral wool", 50, 0.035, 1), ("", "Existing brick", 215, 0.8, 10)) with { Provenance = CalcProvenance.Drawing("A-310", "B") }),
            [new CalcExpectedValue("R_T", 1.9173214285714288, "m^2*K/W", 1e-9), new CalcExpectedValue("S_d", 2.3, "m", 1e-9), new CalcExpectedValue("n_start", 11, "", 1e-9), new CalcExpectedValue("Mc_3", 3022.624174988558, "g/m^2", 1e-9), new CalcExpectedValue("Mc_6", 0, "g/m^2", 1e-9), new CalcExpectedValue("Mc_if_2", 3022.624174988558, "g/m^2", 1e-9), new CalcExpectedValue("Mc_max", 3022.624174988558, "g/m^2", 1e-9), new CalcExpectedValue("Mc_end", 0, "g/m^2", 1e-9), new CalcExpectedValue("u_Mc", 6.045248349977116, "", 1e-9), new CalcExpectedValue("fRsi", 0.8772898588833378, "", 1e-9), new CalcExpectedValue("fRsi_min", 0.5902560168111524, "", 1e-9), new CalcExpectedValue("pass", 0, "", 1e-9)],
            "Condensation starts at the insulation–brick interface in November, builds to 3023 g/m² in March (month lengths are the calendar days) and has dried out by June, so the annual balance passes, but the peak is six times the 500 g/m² project limit and the sheet fails. The surface check passes (f_Rsi 0.877 against f_Rsi,min 0.590, governed by January). The cure is the vapour control layer of the next example.",
            CalcReferenceStatus.Confirmed),

        new(GlaserCalculator.Id, "internal-insulation-with-vcl", "The same wall with a polyethylene vapour control layer behind the plasterboard: no condensation",
            new CalcReferenceSource("Analytic (Glaser method; recomputed in python3)", null, "As the previous example with a 0.3 mm layer (λ 0.2, μ 20000: s_d 6 m) between the plasterboard and the mineral wool; all material values and the climate are invented illustrative inputs, not design data"),
            GlaserInputs(GlaserCalculator.Layers(("", "Plasterboard", 12.5, 0.25, 8), ("", "Vapour control layer", 0.3, 0.2, 20000), ("", "Mineral wool", 50, 0.035, 1), ("", "Existing brick", 215, 0.8, 10)) with { Provenance = CalcProvenance.Drawing("A-310", "C") }),
            [new CalcExpectedValue("R_T", 1.9188214285714285, "m^2*K/W", 1e-9), new CalcExpectedValue("S_d", 8.3, "m", 1e-9), new CalcExpectedValue("n_start", 0, "", 1e-9), new CalcExpectedValue("Mc_max", 0, "g/m^2", 1e-9), new CalcExpectedValue("Mc_end", 0, "g/m^2", 1e-9), new CalcExpectedValue("u_Mc", 0, "", 1e-9), new CalcExpectedValue("R_Tf", 2.0388214285714286, "m^2*K/W", 1e-9), new CalcExpectedValue("fRsi", 0.8773801390859566, "", 1e-9), new CalcExpectedValue("fRsi_min", 0.5902560168111524, "", 1e-9), new CalcExpectedValue("pass", 1, "", 1e-9)],
            "The 6 m diffusion-equivalent layer holds the vapour pressure at the cold interfaces below saturation in every month, so no interface condenses and the sheet passes all three checks. The vapour control layer must be continuous and sealed at laps and penetrations: this calculation does not model air leakage or holes in it.",
            CalcReferenceStatus.Confirmed),
    ];

    /// <summary>The inputs shared by the two Glaser examples, with a layers table supplied by the caller.</summary>
    private static CalcInputSet GlaserInputs(CalcInputValue layers) =>
        new(
        [
            layers,
            CalcInputValue.Quantity("Rsi", 0.13, "m^2*K/W", CalcProvenance.Specification("Project surface-resistance record (internal, horizontal flow), assumed")),
            CalcInputValue.Quantity("Rse", 0.04, "m^2*K/W", CalcProvenance.Specification("Project surface-resistance record (external), assumed")),
            CalcInputValue.Quantity("Rsi_f", 0.25, "m^2*K/W", CalcProvenance.Specification("Conventional R_si,f = 0.25 m²K/W for the surface-condensation check of an opaque element (a record, to be checked against the owner's copy of ISO 13788), assumed")),
            CalcInputValue.Quantity("theta_int", 20, "degC", CalcProvenance.Specification("SPEC-A-02 §3.1: living-room design temperature")),
            CalcInputValue.Choice("humidity_mode", GlaserCalculator.ConstantRh, CalcProvenance.Assumed("Constant internal humidity; no humidity-class record is held for this project.")),
            CalcInputValue.Quantity("RH_int", 50, "%", CalcProvenance.Specification("SPEC-A-02 §3.1: internal design humidity")),
            GlaserCalculator.Climate((2, 90), (3, 88), (6, 84), (9, 80), (13, 76), (16, 74), (18, 74), (18, 76), (15, 82), (11, 87), (6, 90), (3, 91)) with { Provenance = CalcProvenance.Assumed("Invented illustrative maritime year; replace with the project's own monthly climate record (take it from your licensed source).") },
            CalcInputValue.Quantity("RH_crit", 80, "%", CalcProvenance.Specification("SPEC-A-02 §4.4: critical surface humidity")),
            CalcInputValue.Quantity("Mc_limit", 500, "g/m^2", CalcProvenance.Specification("SPEC-A-02 §4.4: limit on the condensate per interface")),
            CalcInputValue.Switch("breathable", false, CalcProvenance.Assumed("Mineral wool and brick: no timber or hygroscopic layers.")),
        ]);
}
