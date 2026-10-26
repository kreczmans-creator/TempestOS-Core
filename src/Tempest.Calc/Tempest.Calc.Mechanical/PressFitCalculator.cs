using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

/// <summary>M2b — press (interference) fit and shrink fit by the Lamé thick-cylinder relations: interface pressure, hub and shaft stresses, assembly force, torque capacity, shrink temperature, utilisation against yield.</summary>
public sealed class PressFitCalculator : ICalcCalculator
{
    public const string Id = "mech.press-fit";

    public CalcTemplate Template { get; } = new(
        Id, "1.2.0", CalcDiscipline.Mechanical, "Press and shrink fit",
        "A shaft (solid or hollow) pressed or shrunk into a hub: the interface pressure for a given diametral interference, the hoop and radial stresses at the interface in hub and shaft, von Mises utilisation against each yield strength, the axial assembly force and the torque the joint can carry by friction, and the temperature rise to shrink-fit the hub.",
        "Radial interference: δ_r = δ/2.\nInterface pressure, the hub and the shaft as thick cylinders: p = δ_r / [ (R/E_h)·((r_o² + R²)/(r_o² − R²) + ν_h) + (R/E_s)·((R² + r_i²)/(R² − r_i²) − ν_s) ].\nStresses at the hub bore: σ_θ = p·(r_o² + R²)/(r_o² − R²); σ_r = −p.\nStresses at the shaft surface: σ_θ = −p·(R² + r_i²)/(R² − r_i²); σ_r = −p.\nSurface smoothing on assembly (when Rz is entered): δ_eff = max(δ − 0.8·(Rz_h + Rz_s), 0), p_eff = p·δ_eff/δ.\nAxial force and torque the fit holds by friction, on p_eff: F = μ·p_eff·2πR·L; T = μ·p_eff·2πR²·L. The stresses stay at the nominal p (the larger interference of the fit).\nTemperature difference to assemble by heating the hub, with assembly clearance c_a: ΔT = (δ + c_a)/(α·2R).",
        [
            new CalcInputSpec("d", "Interface (nominal) diameter", CalcInputKind.Quantity, "mm", Default: 50, Min: 1, Max: 1000, HardMin: 1e-3, HardMax: 1e5),
            new CalcInputSpec("delta", "Diametral interference", CalcInputKind.Quantity, "µm", Default: 40, Min: 1, Max: 500, HardMin: 0, HardMax: 1e5),
            new CalcInputSpec("d_o", "Hub outer diameter", CalcInputKind.Quantity, "mm", Default: 100, Min: 1, Max: 2000, HardMin: 1e-3, HardMax: 1e5),
            new CalcInputSpec("d_i", "Shaft bore diameter (0 for a solid shaft)", CalcInputKind.Quantity, "mm", Default: 0, Min: 0, Max: 1000, HardMin: 0, HardMax: 1e5),
            new CalcInputSpec("L", "Engagement length", CalcInputKind.Quantity, "mm", Default: 60, Min: 1, Max: 2000, HardMin: 1e-3, HardMax: 1e5),
            new CalcInputSpec("E_h", "Hub Young's modulus", CalcInputKind.Quantity, "GPa", ReferenceLibrary: "Materials:hub", ReferenceProperty: "YoungsModulus", ReferenceDimension: "Pressure", Default: 207, Min: 10, Max: 450, HardMin: 0.1, HardMax: 2000),
            new CalcInputSpec("nu_h", "Hub Poisson's ratio", CalcInputKind.Number, Default: 0.292, Min: 0.1, Max: 0.45, HardMin: 0, HardMax: 0.5),
            new CalcInputSpec("E_s", "Shaft Young's modulus", CalcInputKind.Quantity, "GPa", ReferenceLibrary: "Materials:shaft", ReferenceProperty: "YoungsModulus", ReferenceDimension: "Pressure", Default: 207, Min: 10, Max: 450, HardMin: 0.1, HardMax: 2000),
            new CalcInputSpec("nu_s", "Shaft Poisson's ratio", CalcInputKind.Number, Default: 0.292, Min: 0.1, Max: 0.45, HardMin: 0, HardMax: 0.5),
            new CalcInputSpec("mu", "Coefficient of friction at the interface", CalcInputKind.Number, Default: 0.15, Min: 0.05, Max: 0.4, HardMin: 0, HardMax: 2),
            new CalcInputSpec("S_y_h", "Hub yield strength", CalcInputKind.Quantity, "MPa", ReferenceLibrary: "Materials:hub", ReferenceProperty: "YieldStrength", ReferenceDimension: "Pressure", Default: 350, Min: 50, Max: 2000, HardMin: 1e-3, HardMax: 1e5),
            new CalcInputSpec("S_y_s", "Shaft yield strength", CalcInputKind.Quantity, "MPa", ReferenceLibrary: "Materials:shaft", ReferenceProperty: "YieldStrength", ReferenceDimension: "Pressure", Default: 350, Min: 50, Max: 2000, HardMin: 1e-3, HardMax: 1e5),
            new CalcInputSpec("alpha_h", "Hub thermal expansion coefficient", CalcInputKind.Quantity, "1/K", Default: 12e-6, Min: 5e-6, Max: 30e-6, HardMin: 1e-9, HardMax: 1e-3),
            new CalcInputSpec("c_a", "Assembly clearance wanted for shrink fitting", CalcInputKind.Quantity, "µm", Default: 20, Min: 0, Max: 500, HardMin: 0, HardMax: 1e5),
            new CalcInputSpec("Rz_h", "Hub bore roughness Rz (blank: the smoothing is not applied)", CalcInputKind.Quantity, "µm", Min: 0, Max: 100, HardMin: 0, HardMax: 1e4, IsOptional: true),
            new CalcInputSpec("Rz_s", "Shaft surface roughness Rz (blank: the smoothing is not applied)", CalcInputKind.Quantity, "µm", Min: 0, Max: 100, HardMin: 0, HardMax: 1e4, IsOptional: true),
        ],
        [
            new CalcAssumption("Both parts are long cylinders in plane stress; the interface pressure is uniform along the engagement.", "Lamé thick-cylinder relations; end effects neglected."),
            new CalcAssumption("The interference is the nominal one given: the torque and the press force need the minimum interference of the fit, the stresses the maximum, and the surface roughness smooths on assembly and takes about 0.8·(Rz_h + Rz_s) off it unless the roughness is entered.", "DIN 7190-1; enter Rz to apply the smoothing to the torque and the force."),
            new CalcAssumption("The friction coefficient is as entered and the same for assembly and for torque transmission.", "Enter it with its source; it dominates the force and torque results."),
            new CalcAssumption("Yield is checked by the von Mises equivalent stress at the hub bore, at the shaft surface and, for a hollow shaft, at the shaft bore (where the hoop stress is largest: 2·p·R²/(R² − r_i²), with no radial stress), the larger of the two shaft values governing.", "Axial stress taken as zero."),
        ],
        [
            new CalcClauseRef("Shigley's Mechanical Engineering Design", "§3-16, §3-17", "Press and shrink fits; stresses in pressurised cylinders"),
            new CalcClauseRef("Roark's Formulas for Stress and Strain", "Ch. 13, Table 13.5", "Thick-walled cylinders under pressure"),
        ],
        [
            new CalcApplicabilityRule("thin-hub", "Hub wall thinner than 10 % of the interface diameter: the thick-cylinder relation still holds but assembly distortion is likely.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("bore-geometry", "The shaft bore must be smaller than the interface, and the interface smaller than the hub outer diameter.", CalcNoticeSeverity.Block),
        ],
        [
            new CalcTestCase("ka-steel-steel-solid", "50 mm solid steel shaft in a 100 mm steel hub, 40 µm interference",
                new CalcReferenceSource("Analytic (Shigley 9th ed. eq. 3-56 to 3-58, same E and ν)", null, "p = E δ_r (r_o² − R²) / (2 R r_o²) = 207000 × 0.02 × (2500 − 625) / (2 × 25 × 2500) = 62.1 MPa; σ_θ,hub = p (r_o² + R²)/(r_o² − R²) = 103.5 MPa"),
                [CalcInputValue.Quantity("d", 50, "mm"), CalcInputValue.Quantity("delta", 40, "µm"), CalcInputValue.Quantity("d_o", 100, "mm"), CalcInputValue.Quantity("d_i", 0, "mm"), CalcInputValue.Quantity("L", 60, "mm"), CalcInputValue.Quantity("E_h", 207, "GPa"), CalcInputValue.Of("nu_h", 0.292), CalcInputValue.Quantity("E_s", 207, "GPa"), CalcInputValue.Of("nu_s", 0.292), CalcInputValue.Of("mu", 0.15), CalcInputValue.Quantity("S_y_h", 350, "MPa"), CalcInputValue.Quantity("S_y_s", 350, "MPa"), CalcInputValue.Quantity("alpha_h", 12e-6, "1/K"), CalcInputValue.Quantity("c_a", 20, "µm")],
                [new CalcExpectedValue("p", 62.1, "MPa", 1e-4), new CalcExpectedValue("sigma_t_hub", 103.5, "MPa", 1e-4), new CalcExpectedValue("F_a", 87.80, "kN", 1e-3), new CalcExpectedValue("T", 2195.0, "N*m", 1e-3), new CalcExpectedValue("dT", 100, "K", 1e-6)]),
            new CalcTestCase("ka-hollow-shaft", "Hollow shaft (bore 25 mm) in the same hub",
                new CalcReferenceSource("Analytic (Lamé)", null, "Shaft compliance term (R/E)((R² + r_i²)/(R² − r_i²) − ν) with r_i = 12.5 mm: (25/207000)(781.25/468.75 − 0.292) = 0.000166 mm/MPa; hub term (25/207000)(3125/1875 + 0.292) = 0.000237; p = 0.02/0.000403 = 49.6 MPa"),
                [CalcInputValue.Quantity("d", 50, "mm"), CalcInputValue.Quantity("delta", 40, "µm"), CalcInputValue.Quantity("d_o", 100, "mm"), CalcInputValue.Quantity("d_i", 25, "mm"), CalcInputValue.Quantity("L", 60, "mm"), CalcInputValue.Quantity("E_h", 207, "GPa"), CalcInputValue.Of("nu_h", 0.292), CalcInputValue.Quantity("E_s", 207, "GPa"), CalcInputValue.Of("nu_s", 0.292), CalcInputValue.Of("mu", 0.15), CalcInputValue.Quantity("S_y_h", 350, "MPa"), CalcInputValue.Quantity("S_y_s", 350, "MPa"), CalcInputValue.Quantity("alpha_h", 12e-6, "1/K"), CalcInputValue.Quantity("c_a", 20, "µm")],
                [new CalcExpectedValue("p", 49.63, "MPa", 2e-3), new CalcExpectedValue("sigma_t_shaft", -82.7, "MPa", 2e-3)]),
        ],
        Category: "Fits");

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
            c.Block("bore-geometry", "d_i/d/d_o: the shaft bore must be smaller than the interface diameter, and the interface smaller than the hub outer diameter.");
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
        c.Assign("p", "delta_r / (C_h + C_s)", "MPa", "Interface pressure", "Shigley eq. 3-56");
        c.Assign("sigma_t_hub", "p * (r_o^2 + R^2) / (r_o^2 - R^2)", "MPa", "Hub hoop stress at the bore", "Shigley eq. 3-57");
        c.Assign("sigma_r_hub", "-p", "MPa", "Hub radial stress at the bore");
        c.Assign("sigma_t_shaft", "-p * (R^2 + r_i^2) / (R^2 - r_i^2)", "MPa", "Shaft hoop stress at the surface", "Shigley eq. 3-58");
        c.Assign("sigma_r_shaft", "-p", "MPa", "Shaft radial stress at the surface");
        c.Assign("sigma_vm_hub", "sqrt(sigma_t_hub^2 - sigma_t_hub * sigma_r_hub + sigma_r_hub^2)", "MPa", "Hub von Mises stress at the bore");
        c.Assign("sigma_vm_shaft", "sqrt(sigma_t_shaft^2 - sigma_t_shaft * sigma_r_shaft + sigma_r_shaft^2)", "MPa", "Shaft von Mises stress at the surface");
        if (dI > 0)
        {
            c.Assign("sigma_t_shaft_bore", "-2 * p * R^2 / (R^2 - r_i^2)", "MPa", "Shaft hoop stress at the shaft bore (hollow shaft; the radial stress is zero there)", "Lamé, external pressure on a hollow cylinder");
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
            c.Assume("No surface smoothing is applied: the torque and the press force are at the nominal interference. Roughness takes about 0.8·(Rz_h + Rz_s) off it on assembly, so a fine fit grips less than shown; check the torque at the minimum interference of the fit and the stresses at the maximum.", "Enter Rz_h and Rz_s to apply the smoothing (DIN 7190-1).");
        }

        c.Assign("p_eff", c.Si("delta") > 0 ? "p * delta_eff / delta" : "p", "MPa", "Interface pressure that grips, at the effective interference");
        c.Assign("F_a", "mu * p_eff * 2 * pi * R * L", "kN", "Axial assembly (press) force");
        c.Assign("T", "mu * p_eff * 2 * pi * R^2 * L", "N*m", "Torque capacity by friction");
        c.Assign("dT", "(delta + c_a) / (alpha_h * d)", "K", "Hub temperature rise for shrink fitting");

        c.Output("p", "Interface pressure");
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

        c.Check("Hub yield", "sigma_vm_hub", "S_y_h");
        c.Check("Shaft yield, surface and bore", "sigma_vm_shaft_max", "S_y_s");
    }
}
