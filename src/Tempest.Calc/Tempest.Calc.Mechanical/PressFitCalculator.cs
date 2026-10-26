using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

/// <summary>M2b — press (interference) fit and shrink fit by the Lamé thick-cylinder relations: interface pressure, hub and shaft stresses, assembly force, torque capacity, shrink temperature, utilisation against yield.</summary>
public sealed class PressFitCalculator : ICalcCalculator
{
    public const string Id = "mech.press-fit";

    public CalcTemplate Template { get; } = new(
        Id, "1.3.0", CalcDiscipline.Mechanical, "Press and shrink fit",
        "Calculates the interface pressure, stresses, assembly force and friction torque of a shaft pressed or shrunk into a hub, and the shrink temperature. It uses the Lamé thick-cylinder relations and ignores end effects.",
        "Radial interference: δ_r = δ/2.\nInterface pressure, the hub and the shaft as thick cylinders: p_int = δ_r / [ (R/E_h)·((r_o² + R²)/(r_o² − R²) + ν_h) + (R/E_s)·((R² + r_i²)/(R² − r_i²) − ν_s) ].\nStresses at the hub bore: σ_θ = p_int·(r_o² + R²)/(r_o² − R²); σ_r = −p_int.\nStresses at the shaft surface: σ_θ = −p_int·(R² + r_i²)/(R² − r_i²); σ_r = −p_int.\nStress at the bore of a hollow shaft: σ_θ = −2·p_int·R²/(R² − r_i²); σ_r = 0.\nSurface smoothing on assembly (when Rz is entered): δ_eff = max(δ − 0.8·(Rz_h + Rz_s), 0), p_eff = p_int·δ_eff/δ.\nAxial force and torque the fit holds by friction, on p_eff: F = μ·p_eff·2πR·L; T = μ·p_eff·2πR²·L. The stresses stay at the nominal p_int (the larger interference of the fit).\nTemperature difference to assemble by heating the hub, with assembly clearance c_a: ΔT = (δ + c_a)/(α·2R).",
        [
            new CalcInputSpec("d", "Interface (nominal) diameter", CalcInputKind.Quantity, "mm", Default: 50, Min: 1, Max: 1000, HardMin: 1e-3, HardMax: 1e5, Group: "Geometry"),
            new CalcInputSpec("delta", "Diametral interference", CalcInputKind.Quantity, "µm", Default: 40, Min: 1, Max: 500, HardMin: 0, HardMax: 1e5, Group: "Geometry"),
            new CalcInputSpec("d_o", "Hub outer diameter", CalcInputKind.Quantity, "mm", Default: 100, Min: 1, Max: 2000, HardMin: 1e-3, HardMax: 1e5, Group: "Geometry"),
            new CalcInputSpec("d_i", "Shaft bore diameter (0 for a solid shaft)", CalcInputKind.Quantity, "mm", Default: 0, Min: 0, Max: 1000, HardMin: 0, HardMax: 1e5, Group: "Geometry"),
            new CalcInputSpec("L", "Engagement length", CalcInputKind.Quantity, "mm", Default: 60, Min: 1, Max: 2000, HardMin: 1e-3, HardMax: 1e5, Group: "Geometry"),
            new CalcInputSpec("Rz_h", "Hub bore roughness Rz (optional; blank = the smoothing is not applied)", CalcInputKind.Quantity, "µm", Min: 0, Max: 100, HardMin: 0, HardMax: 1e4, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("Rz_s", "Shaft surface roughness Rz (optional; blank = the smoothing is not applied)", CalcInputKind.Quantity, "µm", Min: 0, Max: 100, HardMin: 0, HardMax: 1e4, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("E_h", "Hub Young's modulus", CalcInputKind.Quantity, "GPa", ReferenceLibrary: "Materials:hub", ReferenceProperty: "YoungsModulus", ReferenceDimension: "Pressure", Default: 207, Min: 10, Max: 450, HardMin: 0.1, HardMax: 2000, Group: "Material"),
            new CalcInputSpec("nu_h", "Hub Poisson's ratio", CalcInputKind.Number, Default: 0.292, Min: 0.1, Max: 0.45, HardMin: 0, HardMax: 0.5, Group: "Material"),
            new CalcInputSpec("E_s", "Shaft Young's modulus", CalcInputKind.Quantity, "GPa", ReferenceLibrary: "Materials:shaft", ReferenceProperty: "YoungsModulus", ReferenceDimension: "Pressure", Default: 207, Min: 10, Max: 450, HardMin: 0.1, HardMax: 2000, Group: "Material"),
            new CalcInputSpec("nu_s", "Shaft Poisson's ratio", CalcInputKind.Number, Default: 0.292, Min: 0.1, Max: 0.45, HardMin: 0, HardMax: 0.5, Group: "Material"),
            new CalcInputSpec("S_y_h", "Hub yield strength", CalcInputKind.Quantity, "MPa", ReferenceLibrary: "Materials:hub", ReferenceProperty: "YieldStrength", ReferenceDimension: "Pressure", Default: 350, Min: 50, Max: 2000, HardMin: 1e-3, HardMax: 1e5, Group: "Material"),
            new CalcInputSpec("S_y_s", "Shaft yield strength", CalcInputKind.Quantity, "MPa", ReferenceLibrary: "Materials:shaft", ReferenceProperty: "YieldStrength", ReferenceDimension: "Pressure", Default: 350, Min: 50, Max: 2000, HardMin: 1e-3, HardMax: 1e5, Group: "Material"),
            new CalcInputSpec("alpha_h", "Hub thermal expansion coefficient", CalcInputKind.Quantity, "1/K", Default: 12e-6, Min: 5e-6, Max: 30e-6, HardMin: 1e-9, HardMax: 1e-3, Group: "Material"),
            new CalcInputSpec("mu", "Coefficient of friction at the interface", CalcInputKind.Number, Default: 0.15, Min: 0.05, Max: 0.4, HardMin: 0, HardMax: 2, Group: "Factors"),
            new CalcInputSpec("c_a", "Assembly clearance wanted for shrink fitting", CalcInputKind.Quantity, "µm", Default: 20, Min: 0, Max: 500, HardMin: 0, HardMax: 1e5, Group: "Requirements"),
        ],
        [
            new CalcAssumption("Both parts are long cylinders in plane stress, and the interface pressure is uniform along the engagement.", "The Lamé thick-cylinder relations apply, and end effects are neglected."),
            new CalcAssumption("The interference is the nominal value entered.", "Use the minimum interference of the fit for the torque and the force, and the maximum for the stresses."),
            new CalcAssumption("Surface roughness smooths on assembly and takes about 0.8·(Rz_h + Rz_s) off the interference when Rz is entered.", "DIN 7190-1 gives the allowance; enter Rz to apply it to the torque and the force."),
            new CalcAssumption("The friction coefficient is as entered and the same for assembly and for torque transmission.", "Enter it with its source, because it dominates the force and torque results."),
            new CalcAssumption("Yield is checked by von Mises stress at the hub bore, the shaft surface and a hollow shaft's bore.", "The axial stress is zero, and the larger of the two shaft values governs."),
        ],
        [
            new CalcClauseRef("Budynas and Nisbett, Shigley's Mechanical Engineering Design", "§3-16, §3-17, 11th ed.", "Press and shrink fits; stresses in pressurised cylinders"),
            new CalcClauseRef("Young, Budynas and Sadegh, Roark's Formulas for Stress and Strain", "Ch. 13, Table 13.5, 8th ed.", "Thick-walled cylinders under pressure"),
        ],
        [
            new CalcApplicabilityRule("thin-hub", "Hub wall thinner than 10 % of the interface diameter: the thick-cylinder relation still holds but assembly distortion is likely.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("bore-geometry", "The shaft bore must be smaller than the interface, and the interface smaller than the hub outer diameter.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
        ],
        [
            new CalcTestCase("ka-steel-steel-solid", "50 mm solid steel shaft in a 100 mm steel hub, 40 µm interference",
                new CalcReferenceSource("Analytic (Shigley 9th ed. eq. 3-56 to 3-58, same E and ν)", null, "p = E δ_r (r_o² − R²) / (2 R r_o²) = 207000 × 0.02 × (2500 − 625) / (2 × 25 × 2500) = 62.1 MPa; σ_θ,hub = p (r_o² + R²)/(r_o² − R²) = 103.5 MPa"),
                [CalcInputValue.Quantity("d", 50, "mm"), CalcInputValue.Quantity("delta", 40, "µm"), CalcInputValue.Quantity("d_o", 100, "mm"), CalcInputValue.Quantity("d_i", 0, "mm"), CalcInputValue.Quantity("L", 60, "mm"), CalcInputValue.Quantity("E_h", 207, "GPa"), CalcInputValue.Of("nu_h", 0.292), CalcInputValue.Quantity("E_s", 207, "GPa"), CalcInputValue.Of("nu_s", 0.292), CalcInputValue.Of("mu", 0.15), CalcInputValue.Quantity("S_y_h", 350, "MPa"), CalcInputValue.Quantity("S_y_s", 350, "MPa"), CalcInputValue.Quantity("alpha_h", 12e-6, "1/K"), CalcInputValue.Quantity("c_a", 20, "µm")],
                [new CalcExpectedValue("p_int", 62.1, "MPa", 1e-4), new CalcExpectedValue("sigma_t_hub", 103.5, "MPa", 1e-4), new CalcExpectedValue("F_a", 87.80, "kN", 1e-3), new CalcExpectedValue("T", 2195.0, "N*m", 1e-3), new CalcExpectedValue("dT", 100, "K", 1e-6)]),
            new CalcTestCase("ka-hollow-shaft", "Hollow shaft (bore 25 mm) in the same hub",
                new CalcReferenceSource("Analytic (Lamé)", null, "Shaft compliance term (R/E)((R² + r_i²)/(R² − r_i²) − ν) with r_i = 12.5 mm: (25/207000)(781.25/468.75 − 0.292) = 0.000166 mm/MPa; hub term (25/207000)(3125/1875 + 0.292) = 0.000237; p = 0.02/0.000403 = 49.6 MPa"),
                [CalcInputValue.Quantity("d", 50, "mm"), CalcInputValue.Quantity("delta", 40, "µm"), CalcInputValue.Quantity("d_o", 100, "mm"), CalcInputValue.Quantity("d_i", 25, "mm"), CalcInputValue.Quantity("L", 60, "mm"), CalcInputValue.Quantity("E_h", 207, "GPa"), CalcInputValue.Of("nu_h", 0.292), CalcInputValue.Quantity("E_s", 207, "GPa"), CalcInputValue.Of("nu_s", 0.292), CalcInputValue.Of("mu", 0.15), CalcInputValue.Quantity("S_y_h", 350, "MPa"), CalcInputValue.Quantity("S_y_s", 350, "MPa"), CalcInputValue.Quantity("alpha_h", 12e-6, "1/K"), CalcInputValue.Quantity("c_a", 20, "µm")],
                [new CalcExpectedValue("p_int", 49.63, "MPa", 2e-3), new CalcExpectedValue("sigma_t_shaft", -82.7, "MPa", 2e-3)]),
        ],
        Category: "Fits",
        OutputPorts:
        [
            new CalcOutputPort("p_int", "MPa", "Interface pressure from the interference", "press.pressure_interface", ["p"]),
            new CalcOutputPort("T", "kN*m", "Torque the joint carries by friction", "press.torque_capacity"),
            new CalcOutputPort("F_a", "kN", "Axial assembly (press) force", "press.force_assembly"),
            new CalcOutputPort("dT", "K", "Hub temperature rise for shrink fitting", "press.temperature_rise_hub"),
            new CalcOutputPort("sigma_vm_hub", "MPa", "Hub von Mises stress at the bore", "press.stress_von_mises_hub"),
            new CalcOutputPort("sigma_vm_shaft_max", "MPa", "Largest shaft von Mises stress, surface or bore", "press.stress_von_mises_shaft"),
        ]);

    public void Run(CalcRunContext c)
    {
        c.Input("d");
        c.Input("delta");
        c.Input("d_o");
        c.Input("d_i");
        c.Input("L");
        c.Input("E_h");
        c.Input("nu_h");
        c.Input("E_s");
        c.Input("nu_s");
        c.Input("mu");
        c.Input("S_y_h");
        c.Input("S_y_s");
        c.Input("alpha_h");
        c.Input("c_a");

        var d = c.Si("d");
        var dO = c.Si("d_o");
        var dI = c.Si("d_i");

        if (!(dI < d) || !(d < dO))
        {
            c.Block("bore-geometry", dI < d
                ? "d_o: the hub outer diameter must be larger than the interface diameter."
                : "d_i: the shaft bore must be smaller than the interface diameter.");
            return;
        }

        if ((dO - d) / 2 < 0.1 * d)
            c.Warn("thin-hub", "d_o: the hub wall is thinner than 10 % of the interface diameter.");

        c.Assign("R", "d / 2", "mm", "Interface radius");
        c.Assign("r_o", "d_o / 2", "mm", "Hub outer radius");
        c.Assign("r_i", "d_i / 2", "mm", "Shaft bore radius");
        c.Assign("delta_r", "delta / 2", "µm", "Radial interference");
        c.Assign("C_h", "(R / E_h) * ((r_o^2 + R^2) / (r_o^2 - R^2) + nu_h)", "mm/GPa", "Hub compliance", "Shigley §3-16");
        c.Assign("C_s", "(R / E_s) * ((R^2 + r_i^2) / (R^2 - r_i^2) - nu_s)", "mm/GPa", "Shaft compliance", "Shigley §3-16");
        c.Assign("p_int", "delta_r / (C_h + C_s)", "MPa", "Interface pressure", "Shigley eq. 3-56");
        c.Assign("sigma_t_hub", "p_int * (r_o^2 + R^2) / (r_o^2 - R^2)", "MPa", "Hub hoop stress at the bore", "Shigley eq. 3-57");
        c.Assign("sigma_r_hub", "-p_int", "MPa", "Hub radial stress at the bore");
        c.Assign("sigma_t_shaft", "-p_int * (R^2 + r_i^2) / (R^2 - r_i^2)", "MPa", "Shaft hoop stress at the surface", "Shigley eq. 3-58");
        c.Assign("sigma_r_shaft", "-p_int", "MPa", "Shaft radial stress at the surface");
        c.Assign("sigma_vm_hub", "sqrt(sigma_t_hub^2 - sigma_t_hub * sigma_r_hub + sigma_r_hub^2)", "MPa", "Hub von Mises stress at the bore");
        c.Assign("sigma_vm_shaft", "sqrt(sigma_t_shaft^2 - sigma_t_shaft * sigma_r_shaft + sigma_r_shaft^2)", "MPa", "Shaft von Mises stress at the surface");
        if (dI > 0)
        {
            c.Assign("sigma_t_shaft_bore", "-2 * p_int * R^2 / (R^2 - r_i^2)", "MPa", "Shaft hoop stress at the shaft bore (hollow shaft; the radial stress is zero there)", "Lamé, external pressure on a hollow cylinder");
            c.Assign("sigma_vm_shaft_bore", "abs(sigma_t_shaft_bore)", "MPa", "Shaft von Mises stress at the shaft bore (hollow shaft: hoop stress alone)");
        }
        else
        {
            c.Assign("sigma_vm_shaft_bore", "sigma_vm_shaft", "MPa", "Shaft von Mises stress at the centre (solid shaft: equal hoop and radial stress, the same as at the surface)");
        }

        c.Assign("sigma_vm_shaft_max", "max(sigma_vm_shaft, sigma_vm_shaft_bore)", "MPa", "Largest shaft von Mises stress, surface or bore");

        // Surface smoothing (mech.press-fit-R1): the roughness peaks flatten on assembly, so the grip comes from a smaller interference than the nominal one; the stresses stay on the nominal.
        var roughness = new List<string>();

        if (c.Has("Rz_h"))
        {
            c.Input("Rz_h");
            roughness.Add("Rz_h");
        }

        if (c.Has("Rz_s"))
        {
            c.Input("Rz_s");
            roughness.Add("Rz_s");
        }

        if (roughness.Count > 0)
        {
            c.Assign("delta_eff", $"max(delta - 0.8 * ({string.Join(" + ", roughness)}), 0 µm)", "µm", "Effective interference after the surface smoothing", "DIN 7190-1");
        }
        else
        {
            c.Assign("delta_eff", "delta", "µm", "Effective interference (no roughness entered: the nominal interference)");
            c.Assume("No surface smoothing is applied, so the torque needs the minimum interference of the fit and the stresses the maximum.", "Roughness takes about 0.8·(Rz_h + Rz_s) off the interference, so enter Rz_h and Rz_s to apply it (DIN 7190-1).");
        }

        c.Assign("p_eff", c.Si("delta") > 0 ? "p_int * delta_eff / delta" : "p_int", "MPa", "Interface pressure that grips, at the effective interference");
        c.Assign("F_a", "mu * p_eff * 2 * pi * R * L", "kN", "Axial assembly (press) force");
        c.Assign("T", "mu * p_eff * 2 * pi * R^2 * L", "kN*m", "Torque capacity by friction");
        c.Assign("dT", "(delta + c_a) / (alpha_h * d)", "K", "Hub temperature rise for shrink fitting");

        c.Output("p_int", "Interface pressure");
        c.Output("sigma_t_hub", "Hub hoop stress at bore");
        c.Output("sigma_t_shaft", "Shaft hoop stress at surface");
        c.Output("sigma_vm_hub", "Hub von Mises stress");
        c.Output("sigma_vm_shaft", "Shaft von Mises stress at the surface");
        c.Output("sigma_vm_shaft_bore", "Shaft von Mises stress at the shaft bore");
        c.Output("sigma_vm_shaft_max", "Largest shaft von Mises stress (surface or bore)");
        c.Output("delta_eff", "Effective interference after smoothing");
        c.Output("p_eff", "Gripping interface pressure");
        c.Output("F_a", "Assembly force");
        c.Output("T", "Torque capacity");
        c.Output("dT", "Shrink-fit temperature rise");

        c.Check("Hub von Mises stress against yield strength", "sigma_vm_hub", "S_y_h");
        c.Check("Largest shaft von Mises stress against yield strength", "sigma_vm_shaft_max", "S_y_s");
    }
}
