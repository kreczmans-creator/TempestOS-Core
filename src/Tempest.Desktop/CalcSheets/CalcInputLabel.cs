using System.Text;
using Tempest.Calc.Sheets;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// The label a worksheet input row shows (owner, 2026-10-29): "symbol — name" only, for example "L — Span" or
/// "w — Distributed load". The rest of the description (the direction, "optional; blank = …", a note on where the
/// value comes from) moves to the tooltip on the label and the field, which keeps the full description. Presentation
/// only: the template, its hash, the sheet and the PDF keep the description exactly as written.
/// </summary>
public static class CalcInputLabel
{
    /// <summary>The longest name a label shows; a longer one is cut to a curated short name (<see cref="ShortNames"/>).</summary>
    public const int MaxNameLength = 40;

    /// <summary>"symbol — name", as the row shows it.</summary>
    public static string Visible(CalcInputSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return $"{spec.Symbol} — {Name(spec.Description)}";
    }

    /// <summary>The tooltip on the label and the field: the symbol, the full description, the valid range, and whether it is optional.</summary>
    public static string Tooltip(CalcInputSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return $"{spec.Symbol}: {spec.Description}\nValid range: {spec.RangeText}{(spec.IsOptional ? "\nOptional" : string.Empty)}";
    }

    /// <summary>
    /// The short name of a description: a curated name where one is listed, otherwise the description without its
    /// bracketed asides, cut at the first clause break (", ", "; ", ": ", " — ", or a full stop before a capital).
    /// </summary>
    public static string Name(string? description) => Name(description, clauses: 1);

    /// <summary>
    /// The short name of every input of <paramref name="template"/>. Two inputs that would read the same
    /// ("Groove root diameter" for D_g, D_g_u and D_g_l) keep their next clause too ("Groove root diameter, upper
    /// deviation"), where it still fits.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Names(CalcTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var names = template.Inputs.ToDictionary(i => i.Symbol, i => Name(i.Description), StringComparer.Ordinal);

        foreach (var group in template.Inputs.GroupBy(i => names[i.Symbol], StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            foreach (var spec in group)
            {
                var longer = Name(spec.Description, clauses: 2);

                if (longer.Length <= MaxNameLength && !string.Equals(longer, names[spec.Symbol], StringComparison.Ordinal))
                    names[spec.Symbol] = longer;
            }
        }

        return names;
    }

    private static string Name(string? description, int clauses)
    {
        var text = (description ?? string.Empty).Trim();

        if (text.Length == 0)
            return string.Empty;

        if (ShortNames.TryGetValue(text, out var curated))
            return curated;

        var name = WithoutAsides(text);
        var cut = -1;

        for (var clause = 0; clause < clauses; clause++)
        {
            var next = Break(name, cut < 0 ? 0 : cut + 1);

            if (next < 0)
            {
                cut = name.Length;
                break;
            }

            cut = next;
        }

        name = name[..Math.Min(cut < 0 ? name.Length : cut, name.Length)].Trim().TrimEnd('.', ',', ';', ':').Trim();
        return name.Length == 0 ? text : name;
    }

    /// <summary>The index of the first clause break at or after <paramref name="from"/>, or -1.</summary>
    private static int Break(string text, int from)
    {
        var cut = -1;

        foreach (var separator in Separators)
        {
            var at = text.IndexOf(separator, from, StringComparison.Ordinal);

            if (at > 0 && (cut < 0 || at < cut))
                cut = at;
        }

        // A sentence ends at a full stop before a capital ("Temperature of use. Recorded …"); "cl. 6.3.2.3" does not.
        for (var i = Math.Max(from, 1); i + 2 < text.Length && (cut < 0 || i < cut); i++)
        {
            if (text[i] == '.' && text[i + 1] == ' ' && char.IsUpper(text[i + 2]))
            {
                cut = i;
                break;
            }
        }

        return cut;
    }

    private static readonly string[] Separators = [", ", "; ", ": ", " — ", " – "];

    /// <summary>Removes every bracketed aside that starts after a space ("proof (yield) strength" reads "proof strength"); a bracket that is part of a formula (√(A_c1/A_c0)) stays.</summary>
    private static string WithoutAsides(string text)
    {
        var builder = new StringBuilder(text.Length);
        var i = 0;

        while (i < text.Length)
        {
            if (text[i] == '(' && i > 0 && text[i - 1] == ' ')
            {
                var depth = 0;
                var j = i;

                for (; j < text.Length; j++)
                {
                    if (text[j] == '(')
                        depth++;
                    else if (text[j] == ')' && --depth == 0)
                        break;
                }

                // An unclosed bracket ends the name where it opens.
                if (j >= text.Length)
                    break;

                // Drop the space before the aside as well.
                if (builder.Length > 0 && builder[^1] == ' ')
                    builder.Length--;

                i = j + 1;
                continue;
            }

            builder.Append(text[i]);
            i++;
        }

        return builder.ToString().Trim();
    }

    /// <summary>
    /// Curated short names for descriptions that have no short name before their first break, keyed by the full
    /// description. A description that changes falls back to the rule, and the label test then flags it if it is too long.
    /// </summary>
    private static readonly Dictionary<string, string> ShortNames = new(StringComparer.Ordinal)
    {
        ["Assembly clearance wanted for shrink fitting"] = "Shrink-fit assembly clearance", // mech.press-fit c_a
        ["Ratio of the ultimate to the yield safety factor required"] = "Ultimate-to-yield safety factor ratio", // mech.material-margin k_u
        ["Nut / internal-thread material shear strength"] = "Nut thread shear strength", // mech.bolted-joint tau_B
        ["Additional in-plane torque about the centroid"] = "Additional in-plane torque", // mech.bolt-group M_z
        ["Alternating torque amplitude about the mean (optional; blank = 0, a steady torque; a reversing or cyclic drive needs it)"] = "Alternating torque amplitude", // mech.shaft T_a
        ["Peak torque for the yield check and the key (optional; blank = T + T_a)"] = "Peak torque", // mech.shaft T_pk
        ["Add the weight of the carried masses and of the shaft itself as downward vertical loads (optional; blank = refused when a mass is carried; off: the loads table is the whole load, the weights are entered as forces and the masses enter the critical speed only)"] = "Add the weight of masses and shaft", // mech.shaft w_masses
        ["Smallest ratio of the first critical speed to the operating speed"] = "Least critical-speed ratio", // mech.shaft r_c_min
        ["Stress correction factor k_c for the shear stress"] = "Stress correction factor k_c", // mech.spring corr
        ["Where R_m(d) comes from: typed (no default: the wire certificate or your wire standard) or the curve R_m = A/d^m of a material record"] = "Source of the wire tensile strength", // mech.spring rm_source
        ["Endurance strength for the stress kind (shear; bending in torsion) at N_c cycles, alternating (no default: from the wire maker or your fatigue reference)"] = "Endurance strength", // mech.spring S_e
        ["Tensile strength of the wire at this diameter, typed (no default)"] = "Wire tensile strength", // mech.spring Rm
        ["Curve coefficient A of the material record (R_m = A/d^m, A in MPa for d in mm)"] = "Curve coefficient A", // mech.spring Rm_A
        ["Bending endurance strength of the hook (extension) at N_c cycles, alternating (optional; blank = hook bending fatigue is not assessed)"] = "Hook bending endurance strength", // mech.spring S_e_b
        ["Permissible shear stress as a fraction of R_m (generic textbook default; replace with the licensed EN 13906 value)"] = "Permissible shear stress fraction", // mech.spring f_tau
        ["Ultimate shear strength as a fraction of R_m (Goodman, shear; generic textbook default, replace with the licensed EN 13906 value)"] = "Ultimate shear strength fraction", // mech.spring f_su
        ["Permissible bending stress as a fraction of R_m (torsion, hooks; generic textbook default, replace with the licensed EN 13906 value)"] = "Permissible bending stress fraction", // mech.spring f_sigma
        ["Number of cycles the endurance strength belongs to"] = "Cycles for the endurance strength", // mech.spring N_c
        ["Deflection at the tip (cantilever) or at the end of the lever (torsion): the undercut to be cleared"] = "Deflection to clear the undercut", // mech.snap-fit y
        ["Strain factor of the annular snap on the nominal hoop strain ΔD/D (record snapfit.annular-factor; 1 for a plain free ring; no default: from your design reference)"] = "Annular strain factor", // mech.snap-fit K_ann
        ["Force factor of the annular snap on the hoop force (optional; blank = 1, the free ring; record snapfit.annular-force-factor, for example the stiffening of a tube behind the ring)"] = "Annular force factor", // mech.snap-fit K_F
        ["Secant modulus at the design strain and temperature (annular: at the nominal hoop strain ΔD/D) (typed; no default: the plastic's stress–strain data)"] = "Secant modulus", // mech.snap-fit Es
        ["Entry (assembly) angle α of the inclined face to the joining direction"] = "Entry angle", // mech.snap-fit alpha
        ["Coefficient of friction between the two parts (record snapfit.friction; no default: the pair of materials, from your reference or a W3 friction pair)"] = "Coefficient of friction", // mech.snap-fit mu
        ["Number of identical latches that share a holding load (a whole number) (optional; blank = the holding force is not worked out)"] = "Number of latches", // mech.snap-fit n_latch
        ["Coupler length b (four-bar) or connecting-rod length l (slider-crank)"] = "Coupler or connecting-rod length", // mech.mechanism L3
        ["Offset e of the slide line above the crank centre, or of the cam follower axis at +x: positive e reduces the pressure angle where ds/dθ exceeds e (optional; blank = 0)"] = "Offset of the slide line", // mech.mechanism e_off
        ["Input angle at the first row of the table"] = "Start input angle", // mech.mechanism theta_start
        ["Moment of inertia of the output link about its pivot (four-bar)"] = "Output link moment of inertia", // mech.mechanism J_out
        ["Inner corner radius of a hollow rectangle (optional; blank = 1.0 t_w, at most the outer radius)"] = "Inner corner radius", // struct.section-properties r_i
        ["Wall, web or leg thickness"] = "Wall, web or leg thickness", // struct.section-properties t_w
        ["Extreme-fibre distance from the neutral axis (optional; blank = no stress check)"] = "Extreme-fibre distance", // struct.beam-formulas y_c
        ["Compression flange against lateral-torsional buckling (optional; blank = blocks when M_y_Ed is above 0)"] = "Compression flange restraint", // struct.en1993-member ltb
        ["Partial factor for cross-section resistance"] = "Partial factor γ_M0", // struct.en1993-member gamma_M0
        ["Interaction factor for bending about y-y in the member check"] = "Interaction factor about y-y", // struct.en1993-member k_yy
        ["Interaction factor for bending about z-z in the member check"] = "Interaction factor about z-z", // struct.en1993-member k_zz
        ["Across-corners dimension of the smaller of bolt head and nut (optional; blank = punching takes the across-flats dimension, which is conservative)"] = "Across-corners width of head or nut", // struct.connection-ec3 e_head
        ["Partial factor for bolt and weld resistance"] = "Partial factor γ_M2", // struct.connection-ec3 gamma_M2
        ["Directional method: normal stress perpendicular to the throat (optional; blank = no directional check)"] = "Normal stress across the throat", // struct.connection-ec3 sigma_perp
        ["Directional method: shear perpendicular to the weld axis (optional; blank = no directional check)"] = "Shear across the weld axis", // struct.connection-ec3 tau_perp
        ["Directional method: shear parallel to the weld axis (optional; blank = no directional check)"] = "Shear along the weld axis", // struct.connection-ec3 tau_par
        ["Compression resultant distance from the column axis (optional; blank = from the section)"] = "Compression resultant distance", // struct.base-plate z_C_in
        ["Concrete characteristic cylinder strength"] = "Concrete cylinder strength", // struct.base-plate f_ck
        ["Coefficient for long-term effects on the concrete strength"] = "Long-term concrete strength factor", // struct.base-plate alpha_cc
        ["Concentration factor √(A_c1/A_c0) of the foundation (1 when the foundation geometry is not known)"] = "Concentration factor", // struct.base-plate alpha
        ["Foundation geometry that the concentration factor rests on (state it when the factor is above 1)"] = "Basis of the concentration factor", // struct.base-plate alpha_basis
        ["Governing utilisation reported by the PROFIS anchorage design, from the attached report (optional; blank = the anchorage is not assessed here)"] = "PROFIS anchorage utilisation", // struct.base-plate u_profis
        ["Method for the lateral-torsional buckling reduction factor"] = "LTB reduction factor method", // struct.ltb-interaction ltb_method
        ["Buckling curve for lateral-torsional buckling"] = "LTB buckling curve", // struct.ltb-interaction ltb_curve
        ["Apply the modification factor f of cl. 6.3.2.3(2) to the reduction factor"] = "Apply the modification factor f", // struct.ltb-interaction use_f
        ["Moment at end A about y-y as an ordinate of the bending-moment diagram, with the same sign at both ends for single curvature, unlike the end actions of a stiffness analysis (optional; blank = the sheet is blocked in the end-moment mode)"] = "Moment at end A about y-y", // struct.ltb-interaction M_y_A
        ["Effective length factor for end rotation about the minor axis"] = "Effective length factor", // struct.ltb-interaction k
        ["Slenderness correction factor for cl. 6.3.2.3 (recommended 0.75)"] = "Slenderness correction factor", // struct.ltb-interaction beta_LT
        ["Equivalent uniform moment factor about y-y in Annex B (optional; blank = 0.6 + 0.4ψ_y, at least 0.4)"] = "Equivalent moment factor about y-y", // struct.ltb-interaction C_m_y
        ["Equivalent uniform moment factor about z-z in Annex B (optional; blank = 0.6 + 0.4ψ_z, at least 0.4)"] = "Equivalent moment factor about z-z", // struct.ltb-interaction C_m_z
        ["Equivalent uniform moment factor for lateral-torsional buckling in Annex B (optional; blank = the value of C_my)"] = "Equivalent moment factor for LTB", // struct.ltb-interaction C_m_LT
        ["Plate radius (circular) or length in the x direction (rectangular)"] = "Plate radius or x length", // struct.plate-bending a
        ["Partial factor on the resistance of the cross-section"] = "Partial factor γ_M0", // struct.plate-bending gamma_M0
        ["The lugs are on a horizontal spreader or lifting beam (adds the automatic 6° pitch and roll tilt cases)"] = "Lugs on a spreader or lifting beam", // struct.lifting beam
        ["Edge distance from the hole edge to the plate edge on each side, across the load line (limited to r_lug − D_h/2; the effective width for tension is reduced further by 4t_p and by f_u/f_y)"] = "Edge distance across the load line", // struct.lifting be
        ["End distance from the hole edge to the plate edge along the load line (optional; blank = r_lug − D_h/2, the round end)"] = "End distance along the load line", // struct.lifting a
        ["Distance from the hole centre to the weld or base, for the out-of-plane moment (optional; blank = no moment given)"] = "Hole centre to weld or base", // struct.lifting h
        ["Gross cross-section area of the spreader or lifting beam, for its axial utilisation (optional; blank = lug utilisation only)"] = "Spreader beam gross area", // struct.lifting A_beam
        ["Yield strength of the lug plate for its thickness"] = "Lug plate yield strength", // struct.lifting f_y
        ["Ultimate tensile strength of the lug plate"] = "Lug plate ultimate tensile strength", // struct.lifting f_u
        ["Out-of-plane angle β of the sling out of the lug plane"] = "Out-of-plane sling angle", // struct.lifting beta
        ["Design Category the design factor belongs to (optional; blank = no label printed)"] = "Design Category", // struct.lifting design_category
        ["Service Class the service factor belongs to (optional; blank = no label printed)"] = "Service Class", // struct.lifting service_class
        ["Divisor on the yield strength in the allowable stress (optional; blank = BTH-1 mode is blocked; user record)"] = "Divisor on the yield strength", // struct.lifting n_y
        ["Divisor on the ultimate tensile strength in the allowable stress (optional; blank = BTH-1 mode is blocked; user record)"] = "Divisor on the ultimate strength", // struct.lifting n_u
        ["Bearing multiple of the allowable stress on the projected pin area (optional; blank = BTH-1 mode is blocked; user record)"] = "Bearing multiple on the pin area", // struct.lifting k_b
        ["Dishing-ratio limit on the hole diameter over the thickness (optional; blank = the dishing check is not made; user record)"] = "Dishing-ratio limit", // struct.lifting dish_max
        ["Second moment of area about the weaker axis"] = "Second moment of area, weaker axis", // struct.column-buckling I
        ["Ultimate tensile strength of the weaker part joined"] = "Ultimate strength of the weaker part", // struct.fillet-weld f_u
        ["Flow-to-return temperature difference across the load"] = "Flow-to-return temperature difference", // bs.pipe-pump dT_load
        ["Specific heat of the fluid in the heat-load flow (optional; blank = 4186 J/(kg·K) assumed for water, and required for a custom fluid)"] = "Specific heat of the fluid", // bs.pipe-pump cp_load
        ["Design temperature of the line for the insulation step: the coldest (cold line) or hottest (hot line) fluid temperature in it, for example the flow temperature of a 6/12 °C chilled line whose mean is 9 °C (optional; blank = the fluid temperature; offered as a port only when it or the fluid temperature is entered; not used for the fluid properties)"] = "Line design temperature", // bs.pipe-pump T_line
        ["Vapour pressure of the custom fluid at the fluid temperature, absolute (optional; blank = NPSH_a is not computed; needed for NPSH_a of a custom fluid)"] = "Vapour pressure of the custom fluid", // bs.pipe-pump p_v_custom
        ["Saturation efficiency for evaporative humidification (optional; blank = use the leaving relative humidity)"] = "Saturation efficiency", // bs.psychrometrics eff
        ["Expansion coefficient from a glycol record (optional; blank = water)"] = "Glycol expansion coefficient", // bs.sealed-system e_fluid
        ["MVHR supply air temperature after heat recovery (optional; blank = natural ventilation, outside air)"] = "MVHR supply air temperature", // bs.room-heat-loss T_supply
        ["Thermal-bridge allowance on the area facing outside"] = "Thermal-bridge allowance", // bs.room-heat-loss y_tb
        ["Target U for built-up elements without their own target (optional; blank = no global target)"] = "Target U for built-up elements", // bs.room-heat-loss U_target
        ["Infiltration air change rate at outside temperature, with MVHR only (optional; blank = no infiltration loss, with a warning; 0 = airtight; n is then the MVHR rate)"] = "Infiltration air change rate", // bs.room-heat-loss n_inf
        ["Element whose rows of the layers table to use (optional; blank = all rows)"] = "Element to use", // bs.glaser element
        ["Internal surface resistance R_si,f for the f_Rsi check (0.25 m²K/W is the conventional value for an opaque element and 0.13 for glazing; a record to check against the owner's copy of ISO 13788)"] = "Internal surface resistance R_si,f", // bs.glaser Rsi_f
        ["Safety factor on the vapour-pressure difference Δp = Δv·R_v·(T_i + T_e)/2 in the moisture-excess mode (1.10 by convention; a record, wording to be checked against the owner's copy of ISO 13788)"] = "Safety factor on the vapour pressure", // bs.glaser dp_f
        ["U-value of this wall from the room heat loss calculation, for the consistency check against 1/R_T (optional; blank = not checked)"] = "U-value from the room heat loss", // bs.glaser U_ref
        ["Young's modulus of the pipe material at the installation (reference) temperature, for the stress check, in GPa (steel 210, copper 120, PE and PP about 1; not MPa)"] = "Young's modulus at installation", // bs.pipe-expansion E
        ["Mean coefficient of thermal expansion between the install and operating temperatures"] = "Mean thermal expansion coefficient", // bs.pipe-expansion alpha
        ["Young's modulus at the operating temperature, for the reactions on the anchors, in GPa (optional; blank = E); do not use it for the stress check"] = "Young's modulus when hot", // bs.pipe-expansion E_hot
        ["Allowable displacement-stress range for the legs and runs (your piping code and its formula; optional for an axial bellows; no default)"] = "Allowable displacement-stress range", // bs.pipe-expansion Sa
        ["Screening limit K1 for OD·Y/(L − U)², in mm²/m² (or in²/ft², …) from your piping code (optional; blank = screening not evaluated, so the sheet does not pass; no default)"] = "Screening limit K1", // bs.pipe-expansion K_scr
        ["Friction coefficient μ of the slides and guides between the anchor and the loop (optional; blank = friction is not in the anchor force)"] = "Slide and guide friction coefficient", // bs.pipe-expansion mu
        ["Weight of the pipe per metre with its contents, in kg/m (enter with μ)"] = "Pipe weight per metre, full", // bs.pipe-expansion w_pipe
        ["Length of pipe on sliding supports between the anchor and the loop (optional; blank = the anchor spacing L)"] = "Length on sliding supports", // bs.pipe-expansion L_slide
        ["Where the outer surface coefficient comes from"] = "Source of the outer surface coefficient", // bs.insulation h_mode
        ["Height or flow length of the flat surface (correlation, flat)"] = "Flat surface height or flow length", // bs.insulation L_surf
        ["Heat-loss limit per square metre of flat surface (optional; blank = not a criterion)"] = "Heat-loss limit per square metre", // bs.insulation q_max_A
        ["Allowance of the surface above the dew point, for a cold surface (optional; blank = not a criterion)"] = "Margin above the dew point", // bs.insulation dew_margin
        ["Design torque basis for downstream couplings (T_design): the load's peak (the larger of the accelerating and the braking torque, in magnitude), the motor's typed peak torque, a service factor on the rated torque, or a protective limit such as a torque limiter"] = "Design torque basis", // elec.motor-sizing T_basis
        ["A brake or a self-locking drive holds a linear axis during the dwell (no holding torque); unticked, a vertical or inclined axis is held by the motor"] = "Brake or self-locking drive holds", // elec.motor-sizing brake
        ["Transmission inertia referred to the motor shaft (screw, pulleys, gearbox, coupling)"] = "Transmission inertia at the motor", // elec.motor-sizing J_mech
        ["Service factor on the rated torque for the design torque (needed for the service-factor basis)"] = "Service factor on the rated torque", // elec.motor-sizing SF_c
        ["Protective torque limit at the motor shaft, e.g. a torque limiter or a drive torque setting (needed for the protective-limit basis)"] = "Protective torque limit", // elec.motor-sizing T_lim
        ["Overall heat-transfer coefficient of the enclosure surface (editable reference value)"] = "Surface heat-transfer coefficient", // elec.enclosure-heat h_conv
        ["Temperature at which the enclosure was closed with no breather or vent, for the pressure difference across its walls when the air warms or cools (optional; blank = vented, so no pressure difference)"] = "Temperature when sealed", // elec.enclosure-heat T_seal
        ["Cracking pressure of the breather or relief element a sealed enclosure has (optional; blank = none, so the full gas-law difference acts; needs T_seal)"] = "Breather cracking pressure", // elec.enclosure-heat dp_relief
        ["Area of the cover or door the pressure difference acts on, for the force on its fixings (optional; blank = not worked out; needs T_seal)"] = "Cover or door area", // elec.enclosure-heat A_cover
        ["System resistance (filters, grilles) at the reference airflow"] = "System resistance", // elec.enclosure-heat p_sys
        ["Reference airflow for the system resistance"] = "Reference airflow", // elec.enclosure-heat V_ref
        ["Voltage drop figures by cross-section from your licensed table, read on the mV/A/m route at csa = A_c (optional; blank = the figure is typed as mVAm)"] = "Voltage drop table", // elec.voltage-drop mVAm_table
        ["Conductor operating temperature for the resistance"] = "Conductor operating temperature", // elec.voltage-drop T_c
        ["Tabulated current-carrying capacity for the installed conditions (temperature estimate)"] = "Tabulated current-carrying capacity", // elec.voltage-drop I_z
        ["Share of the additional load's input power released inside the enclosure (a linked machine is outside it: 0)"] = "Heat share of the additional load", // elec.load-budget heat_add
        ["Rated (nameplate) shaft power of the additional machine (linked from the motor-sizing P_rated); the supply carries the larger of this and the duty power P_add (optional; blank = the machine is sized on its duty only)"] = "Rated power of the additional machine", // elec.load-budget P_rated_add
        ["PSU derating factor at the ambient and altitude"] = "PSU derating factor", // elec.load-budget f_derate
        ["Transformer short-time overload allowed for inrush"] = "Transformer inrush overload factor", // elec.load-budget k_T
        ["Device type code carried from the cable step (1 MCB type B, 2 type C, 3 type D, 4 fuse, MCCB or MPCB); the device chosen here must match it"] = "Device type code", // elec.fault-protection device_code
        ["Maximum earth fault loop impedances from your licensed table, checked against Z_s at the device rating and the required disconnection time (optional; blank = no check against a table)"] = "Maximum loop impedance table", // elec.fault-protection Z_s_max_table
        ["Conductor resistance factor on ρ₂₀ for R₁ + R₂ (1 = the resistivity itself; the default of 1.125 takes pure copper to the cable standard's maximum conductor resistances, so Z_s is not understated; k is not changed by it)"] = "Conductor resistance factor", // elec.fault-protection k_R
        ["Initial temperature for the adiabatic check"] = "Initial temperature", // elec.fault-protection theta_i
        ["Final temperature for the adiabatic check"] = "Final temperature", // elec.fault-protection theta_f
        ["Device let-through energy I²t at the maximum prospective fault (disconnection below 0.1 s)"] = "Device let-through energy I²t", // elec.fault-protection I2t_let
        ["Current-carrying capacities for the installation method"] = "Current-carrying capacities", // elec.conductor-sizing ampacity
        ["Conductor operating temperature for 'user' insulation"] = "Operating temperature, user insulation", // elec.conductor-sizing T_user
        ["Reference ambient temperature for the derived C_a"] = "Reference ambient temperature", // elec.conductor-sizing T_ref
        ["Derive C_a from the temperatures instead of the entered factor"] = "Derive C_a from the temperatures", // elec.conductor-sizing derive_Ca
        ["Temperature coefficient of resistance of copper"] = "Temperature coefficient of copper", // elec.pcb-trace alpha
        ["Cables in the containment and pulled together"] = "Cables pulled together", // elec.containment cables
        ["Weight-correction factor on the cable weight and, by the same factor, the bend friction (optional; blank = 1, full weight on the containment; above 1 for cradled or triangular arrangements, from your record)"] = "Weight-correction factor", // elec.containment w_c
        ["Derating of the cable limits for more than 3 cables on one grip (from your record)"] = "Derating for more than 3 cables", // elec.containment k_n
        ["Minimum bend radius as a multiple of the largest cable diameter (from your record)"] = "Minimum bend radius multiple", // elec.containment n_bend
        ["Grip or pulling-eye limit of the whole bundle (from the pulling equipment or your record)"] = "Grip or pulling-eye limit", // elec.containment T_grip
        ["Three-phase fault level at the point of supply (use the minimum fault level, which gives the deepest dip)"] = "Three-phase fault level", // elec.motor-start S_k
        ["Three-phase fault current at the point of supply (links fault-protection I_k3)"] = "Three-phase fault current", // elec.motor-start I_k
        ["Voltage factor c for the fault-level route, linked from the fault-protection c_k3 (optional; blank = 1.1 for the I_k basis, as fault-protection I_k3 is computed with, and 1.0 for a declared S_k)"] = "Voltage factor c", // elec.motor-start c_src
        ["Upstream network fault level at the transformer primary, in series with the transformer, as the minimum fault level (optional; blank = infinite bus)"] = "Upstream network fault level", // elec.motor-start S_kQ
        ["Soft-starter current limit as a multiple of the rated current"] = "Soft-starter current limit", // elec.motor-start k_soft
        ["Drive line-current limit as a multiple of the rated current"] = "Drive line-current limit", // elec.motor-start k_vfd
        ["Drive line displacement power factor while starting"] = "Drive power factor when starting", // elec.motor-start cos_phi_vfd
        ["Dip limit at the point of common coupling (when the network operator sets one)"] = "Dip limit at the coupling point", // elec.motor-start dip_limit_pcc
        ["Maximum permitted temperature of the hottest node (junction or case)"] = "Maximum hottest-node temperature", // elec.thermal-chain T_max
        ["Supply pressure at the cylinder and the pump, gauge"] = "Supply pressure", // fp.cylinder p_sys
        ["Back pressure on the discharge side of the standard connection, gauge"] = "Back pressure", // fp.cylinder p_back
        ["Effective-length factor K of the mounting (L_eff = K·L)"] = "Effective-length factor K", // fp.cylinder K_m
        ["Available hose or pipe inside diameters from your supplier's range (optional; blank = no size is recommended)"] = "Available hose or pipe sizes", // fp.cylinder hoses
        ["Length of the closed cylinder beyond its stroke (body, end caps and mounting eyes), added to the stroke when the closed length is not entered"] = "Closed length beyond the stroke", // fp.cylinder L_add
        ["Hydraulic motor displacement per revolution (optional; blank = no motor)"] = "Hydraulic motor displacement", // fp.cylinder V_gm
        ["Available accumulator sizes from the manufacturer's range (optional; blank = no size is selected)"] = "Available accumulator sizes", // fp.accumulator sizes
        ["Pre-charge rule: p0 (at T_max) at most this fraction of p1, on absolute pressures"] = "Pre-charge rule, lower limit", // fp.accumulator r_0
        ["Pre-charge rule: p2/p0 (p0 at T_min) at most this ratio, on absolute pressures"] = "Pre-charge rule, upper limit", // fp.accumulator r_p
        ["Ring linear thermal expansion coefficient α (an indicative elastomer figure; take yours from the compound or the material library)"] = "Ring thermal expansion coefficient", // fp.oring-gland alpha
        ["Groove metal linear thermal expansion coefficient α_m (the part that carries the groove; one material record per sheet, so this one is entered from the metal's data sheet)"] = "Groove metal expansion coefficient", // fp.oring-gland alpha_m
        ["Smallest volume change of the ring in the fluid (negative for shrinkage; 0 if none)"] = "Smallest ring volume change", // fp.oring-gland s_min
        ["Largest volume swell of the ring in the fluid"] = "Largest ring volume swell", // fp.oring-gland s_max
        ["Linear thermal expansion coefficient of the part that carries the sealing surface, the bore of a piston seal or the rod of a rod seal, which sets the extrusion gap over temperature (optional; blank = the groove metal's α_m)"] = "Sealing-surface expansion coefficient", // fp.oring-gland alpha_s
        ["Diametral growth under the operating pressure of the bore (piston seal) or of the housing bore round the rod (rod seal), from your cylinder or housing calculation; added to the extrusion gap (0 if not known)"] = "Diametral growth under pressure", // fp.oring-gland dD_p
        ["Largest permitted extrusion gap at this pressure and hardness, if you have it as one value: diametral (the clearance D_s − D_c) for a radial seal, axial for a face seal; a radial chart value is half of it"] = "Largest permitted extrusion gap", // fp.oring-gland gap_allow
        ["Permitted extrusion gap against hardness and pressure (your own record)"] = "Permitted extrusion gap record", // fp.oring-gland ext
        ["Distribution of the toleranced dimensions"] = "Tolerance distribution", // fp.oring-gland dist
        ["Sealing-surface diameter, upper deviation"] = "Sealing diameter, upper deviation", // fp.oring-gland D_s_u
        ["Sealing-surface diameter, lower deviation"] = "Sealing diameter, lower deviation", // fp.oring-gland D_s_l
        ["Hole: an ISO 286 class, or a rolling-bearing bore (ES_hole and EI_hole from the bearing record, ISO 492)"] = "Hole class or bearing bore", // mech.iso286-fit hole_kind
        ["External load: constant direction, or always opposing the output motion"] = "External load direction", // mech.mechanism load_dir
        ["γ_G,sup (unfavourable permanent)"] = "Factor on unfavourable permanent actions", // struct.load-combinations gamma_G_sup
        ["γ_G,inf (favourable permanent)"] = "Factor on favourable permanent actions", // struct.load-combinations gamma_G_inf
        ["γ_Q (variable)"] = "Factor on variable actions", // struct.load-combinations gamma_Q
        ["Temperature of use. Recorded: Es and the permissible strain must be the values at this temperature"] = "Temperature of use", // mech.snap-fit T_use
        ["Groove root diameter: on the piston, in the housing of a rod seal, or for a face seal the diameter the ring seats on (the groove inner wall for external pressure, the groove outer wall for internal pressure)"] = "Groove root diameter", // fp.oring-gland D_g
    };
}
