using static Tempest.Calc.Library.Rule;

namespace Tempest.Calc.Library;

/// <summary>
/// Track P's 82 record templates (<c>docs/verification/record-templates</c>), keyed by their file number, each tied to the record kind a built
/// calculator declares, with the checks its notes ask for (Addendum C, C-3). The headers are the template files' own header rows; a test reads
/// the files and fails if either side changes. Hard limits (the ranges a calculator refuses) are not repeated here: the importer takes them from
/// the calculator's own input specification, so a record is never accepted that the calculator would then refuse. What is listed here is what only
/// the notes know: required cells, duplicates, orderings within a table, and the warning ranges.
/// </summary>
/// <remarks>
/// A template whose kind no calculator declares yet (<see cref="RecordTemplateDisposition.Proposed"/>) cannot be imported: there is no record to
/// save it as. Its checks arrive with its kind. A superseded template (<see cref="RecordTemplateDisposition.Superseded"/>) is not read by any
/// calculator and is refused with the notes' reason.
/// </remarks>
public static class RecordTemplates
{
    private const string Conductor = "conductor.material";

    /// <summary>Every template, in file order.</summary>
    public static IReadOnlyList<RecordTemplate> All { get; } = Build();

    /// <summary>The template with this file number (<c>03</c>) or file name (<c>03-bs7671.max-zs</c>), or <see langword="null"/>.</summary>
    public static RecordTemplate? Find(string? idOrFileName)
    {
        if (string.IsNullOrWhiteSpace(idOrFileName))
            return null;

        var key = idOrFileName.Trim();
        return All.FirstOrDefault(t => string.Equals(t.Id, key, StringComparison.Ordinal) || string.Equals(t.FileName, key, StringComparison.Ordinal));
    }

    /// <summary>The templates that fill a record kind.</summary>
    public static IReadOnlyList<RecordTemplate> ForKind(string kind) =>
        [.. All.Where(t => t.Disposition == RecordTemplateDisposition.Kind && string.Equals(t.Kind, kind, StringComparison.Ordinal))];

    private static RecordTemplate T(string id, string name, string title, string kind, string header, string note, params RecordCheck[] checks) =>
        new(id, name, title, kind, RecordTemplateDisposition.Kind, header, note, checks);

    private static RecordTemplate Proposed(string id, string name, string title, string kind, string header, string note) =>
        new(id, name, title, kind, RecordTemplateDisposition.Proposed, header, note);

    private static RecordTemplate Superseded(string id, string name, string title, string header, string note) =>
        new(id, name, title, string.Empty, RecordTemplateDisposition.Superseded, header, note);

    private static IReadOnlyList<RecordTemplate> Build() =>
    [
        T("01", "conductor.material", "Conductor material constants", Conductor,
            "material[text],rho_20[ohm*mm],alpha[1/K],Q_c[J/(K*mm3)],beta[K],theta_i[degC],theta_f[degC]",
            "One record per row (a material). Used by E5, E3 and E7.",
            Req("rho_20", "alpha", "Q_c", "beta", "theta_i", "theta_f"), Pos("rho_20", "alpha", "Q_c", "beta", "theta_i", "theta_f"),
            Lt("theta_i", "theta_f"), Warn("rho_20", 1e-6, 1e-4), Warn("alpha", 0.001, 0.01), Warn("Q_c", 1e-3, 1e-2), Warn("beta", 100, 400),
            Warn("theta_i", 20, 250), Warn("theta_f", 100, 400)),

        T("02", "device.characteristic", "Device time-current points", "device.characteristic",
            "I2t_let[A2*s],I_cn[kA],t[s],I[A]",
            "One record per file: I2t_let and I_cn are read from the first data row, the other columns are the curve points.",
            Req("t", "I"), Pos("t", "I"), Rows(max: 40), Mono([], "I", "t", RecordDirection.Falling), Warn("I2t_let", 1, 1e9), Warn("I_cn", 1, 150)),

        Proposed("03", "bs7671.max-zs", "Maximum earth fault loop impedance", "bs7671.max-zs",
            "device[text],t_disc[s],I_n[A],Z_s_max[ohm]",
            "Proposed kind: E5 is built but lists no record kind for the maximum Zs table."),

        T("04", "bs7671.ampacity", "Cable current-carrying capacities (installation method)", "cable.ampacity",
            "CSA[mm2],AWG[-],I_t[A]",
            "One record per file: the E4 ampacity table.",
            Req("I_t"), Pos("I_t"), Either("CSA", "AWG"), Uniq("CSA"), Uniq("AWG"), Rows(max: 60), Mono([], "CSA", "I_t", RecordDirection.Rising), Mono([], "AWG", "I_t", RecordDirection.Falling)),

        Proposed("05", "bs7671.voltage-drop", "Voltage drop per ampere per metre", "bs7671.voltage-drop",
            "conductor[text],csa[mm2],r_dc[mV/A/m],r_1ph[mV/A/m],x_1ph[mV/A/m],z_1ph[mV/A/m],r_3ph[mV/A/m],x_3ph[mV/A/m],z_3ph[mV/A/m]",
            "Proposed kind: E3 is built but lists no record kind for the voltage-drop table."),

        T("06", "bs7671.factors-ambient", "Ambient correction factors", "cable.correction-factors",
            "medium[text],insulation[text],theta_a[degC],C_a[-]",
            "One record per row (C_a). The medium, insulation and theta_a cells join the title.",
            Req("theta_a", "C_a"), Pos("C_a"), Uniq("medium", "insulation", "theta_a"), Mono(["medium", "insulation"], "theta_a", "C_a", RecordDirection.Falling), Warn("theta_a", -20, 120)),

        T("07", "bs7671.factors-grouping", "Grouping correction factors", "cable.correction-factors",
            "arrangement[text],n_circuits[-],C_g[-]",
            "One record per row (C_g). The arrangement and n_circuits cells join the title.",
            Req("n_circuits", "C_g"), Pos("C_g"), Int("n_circuits"), AtMost(1, "C_g"), Uniq("arrangement", "n_circuits"),
            Mono(["arrangement"], "n_circuits", "C_g", RecordDirection.NotRising)),

        T("08", "bs7671.factors-insulation", "Thermal-insulation correction factors", "cable.correction-factors",
            "length_in_insulation[mm],C_i[-]",
            "One record per row (C_i). The length cell joins the title.",
            Req("length_in_insulation", "C_i"), Pos("length_in_insulation", "C_i"), AtMost(1, "C_i"), Uniq("length_in_insulation"), Ordered("length_in_insulation"),
            Mono([], "length_in_insulation", "C_i", RecordDirection.NotRising)),

        Superseded("09", "bs7671.factors-protection", "Cc factor", "C_c[-]",
            "Superseded: E4 derives C_c in code, so no calculator reads a record for it."),

        T("10", "en60204.ampacity", "Cable current-carrying capacities (machine wiring)", "cable.ampacity",
            "CSA[mm2],AWG[-],I_t[A]",
            "One record per file: the E4 ampacity table for machine wiring.",
            Req("I_t"), Pos("I_t"), Either("CSA", "AWG"), Uniq("CSA"), Uniq("AWG"), Rows(max: 60), Mono([], "CSA", "I_t", RecordDirection.Rising), Mono([], "AWG", "I_t", RecordDirection.Falling)),

        T("11", "en60204.factors", "Machine wiring correction factors", "cable.correction-factors",
            "factor[text],theta_a[degC],n_circuits[-],C_a[-],C_g[-]",
            "One record per row (C_a or C_g). The factor, theta_a and n_circuits cells join the title.",
            One("C_a", "C_g"), Needs("C_a", "theta_a"), Needs("C_g", "n_circuits"), Pos("C_a", "C_g"), Uniq("factor", "theta_a", "n_circuits"),
            Mono(["factor"], "theta_a", "C_a", RecordDirection.NotRising, RecordSeverity.Warning), Mono(["factor"], "n_circuits", "C_g", RecordDirection.NotRising)),

        T("12", "steel.strengths", "Steel strengths by thickness band", "steel.strengths",
            "grade[text],t_min[mm],t_max[mm],f_y[MPa],f_u[MPa]",
            "One record per row (a grade and thickness band). t_min and t_max are stored and never applied.",
            Req("grade", "t_min", "t_max", "f_y", "f_u"), Pos("f_y", "f_u"), NonNeg("t_min"), Lt("t_min", "t_max"), Lt("f_y", "f_u"), NoOverlap(["grade"], "t_min", "t_max"),
            Mono(["grade"], "t_min", "f_y", RecordDirection.NotRising), Warn("f_u", 340, 720)),

        T("13", "ec3.partial-factors", "EN 1993-1-1 National Annex gamma_M0", "ec3-1-1.national-annex",
            "gamma_M0[-]",
            "One record: a single data row.",
            Req("gamma_M0"), Pos("gamma_M0"), AtLeast(1, "gamma_M0"), Rows(max: 1)),

        T("14", "ec3.national-annex", "EN 1993-1-8 National Annex factors", "ec3.national-annex",
            "gamma_M2[-],gamma_M0[-],k_e1[-],k_e2[-],k_p1[-],k_p2[-],gamma_M1[-]",
            "One record per row. Leave gamma_M1 blank on a row that only serves S4 or S6.",
            Req("gamma_M2", "gamma_M0", "k_e1", "k_e2", "k_p1", "k_p2"), Pos("gamma_M2", "gamma_M0", "k_e1", "k_e2", "k_p1", "k_p2", "gamma_M1"),
            AtLeast(1, "gamma_M2", "gamma_M0", "gamma_M1"), Warn("gamma_M2", 1.1, 1.5), Warn("gamma_M0", 1.0, 1.2), Warn("k_e1", 1, 3), Warn("k_e2", 1, 3),
            Warn("k_p1", 2, 4), Warn("k_p2", 2, 4)),

        T("15", "en1990.factors", "EN 1990 partial and psi factors", "en1990.factors",
            "gamma_G_sup[-],gamma_G_inf[-],gamma_Q[-],xi[-],category[text],psi_0[-],psi_1[-],psi_2[-]",
            "One record per file: the four scalar columns are read from the first data row, the category rows are the psi table.",
            ReqFirst("gamma_G_sup", "gamma_G_inf", "gamma_Q", "xi"), Req("category", "psi_0", "psi_1", "psi_2"), NonNeg("gamma_G_sup", "gamma_G_inf", "gamma_Q", "xi", "psi_0", "psi_1", "psi_2"),
            AtMost(1, "psi_0", "psi_1", "psi_2"), NotRisingAcross("psi_0", "psi_1", "psi_2"), Uniq("category"), Warn("gamma_G_sup", 1, 2), Warn("gamma_G_inf", 0.8, 1.2),
            Warn("gamma_Q", 1, 2), Warn("xi", 0.8, 1)),

        T("16", "fluid.properties", "Fluid specific heats", "fluid.properties",
            "fluid_h[text],t_mean_h[degC],cp_h[J/(kg*K)],fluid_c[text],t_mean_c[degC],cp_c[J/(kg*K)]",
            "One record per row (a pair of streams). The fluid names and mean temperatures join the title.",
            Req("fluid_h", "fluid_c", "t_mean_h", "t_mean_c", "cp_h", "cp_c"), Pos("cp_h", "cp_c"), Warn("cp_h", 500, 10000), Warn("cp_c", 500, 10000),
            Warn("t_mean_h", -40, 200), Warn("t_mean_c", -40, 200)),

        T("17", "hydraulics.velocity-limits", "Line velocity limits", "cylinder.line-limits",
            "basis[text],v_suc_max[m/s],v_prs_max[m/s],v_ret_max[m/s]",
            "One record per row (a basis). The calculator's kind is cylinder.line-limits.",
            Req("basis", "v_suc_max", "v_prs_max", "v_ret_max"), Uniq("basis"), Pos("v_suc_max", "v_prs_max", "v_ret_max"), Lt("v_suc_max", "v_prs_max", orEqual: true, severity: RecordSeverity.Warning)),

        T("18", "en1992.factors", "EN 1992-1-1 National Annex factors", "ec2.national-annex",
            "situation[text],gamma_c[-],alpha_cc[-]",
            "One record per row (a design situation). The calculator's kind is ec2.national-annex.",
            Req("situation", "gamma_c", "alpha_cc"), Pos("gamma_c", "alpha_cc"), AtLeast(1, "gamma_c"), AtMost(1, "alpha_cc"), Uniq("situation")),

        Proposed("19", "concrete.strengths", "Concrete strength classes", "concrete.strengths",
            "class[text],f_ck[MPa],f_ck_cube[MPa]",
            "Proposed kind: S6 is built but types f_ck by hand and lists no record kind for it."),

        T("20", "ec3.ltb-parameters", "Lateral-torsional buckling parameters", "ec3.ltb-factors",
            "alpha_a0[-],alpha_a[-],alpha_b[-],alpha_c[-],alpha_d[-],lambda_LT0[-],beta_LT[-]",
            "One record: a single data row. The calculator's kind is ec3.ltb-factors.",
            Req("alpha_a0", "alpha_a", "alpha_b", "alpha_c", "alpha_d", "lambda_LT0", "beta_LT"), Pos("alpha_a0", "alpha_a", "alpha_b", "alpha_c", "alpha_d", "lambda_LT0", "beta_LT"),
            Rows(max: 1), Rising(RecordSeverity.Warning, "alpha_a0", "alpha_a", "alpha_b", "alpha_c", "alpha_d")),

        T("21", "ltb.moment-factors", "C1, C2 and k_c for the moment diagram", "ec3.mcr-factors",
            "case[text],C_1[-],C_2[-],k_c[-]",
            "One record per row (a load case). The calculator's kind is ec3.mcr-factors.",
            Req("case", "C_1"), Uniq("case"), Pos("C_1"), Warn("C_1", 1, null)),

        T("22", "shaft.material", "Shaft material strengths by diameter band", "shaft.material",
            "material[text],d_min[mm],d_max[mm],S_ut[MPa],S_y[MPa],E[GPa]",
            "One record per row (a material and diameter band). d_min and d_max are stored and never applied.",
            Req("material", "d_min", "d_max", "S_ut", "S_y", "E"), Pos("S_ut", "S_y", "E"), NonNeg("d_min"), Lt("d_min", "d_max"), Lt("S_y", "S_ut"),
            Warn("S_ut", 300, 2000), Warn("S_y", 150, 1900), Warn("E", 50, 250)),

        T("23", "shaft.limits", "Slope and deflection limits", "shaft.limits",
            "component[text],theta_max[rad],y_max[mm]",
            "One record per row (a component).",
            Req("component", "theta_max", "y_max"), Pos("theta_max", "y_max"), Warn("theta_max", 1e-5, 0.1), Warn("y_max", 0.001, 10)),

        T("24", "bearing.ratings", "Bearing load ratings", "bearing.ratings",
            "designation[text],C[kN],C_0[kN]",
            "One record per row (a designation).",
            Req("C", "C_0"), ReqWarn("designation"), Pos("C", "C_0"), Uniq("designation"), Warn("C", 0.1, 10000), Warn("C_0", 0.1, 10000)),

        T("25", "bearing.factors", "Bearing X, Y and e factors", "bearing.factors",
            "bearing_type[text],Fa_C0[-],e_lim[-],X[-],Y[-],X_0[-],Y_0[-]",
            "One record per row (a bearing type and Fa/C0 band). Fa_C0 is stored and never applied.",
            Req("bearing_type", "Fa_C0", "e_lim", "X", "Y", "X_0", "Y_0"), NonNeg("Fa_C0", "e_lim", "X", "Y", "X_0", "Y_0"), Uniq("bearing_type", "Fa_C0"),
            Ordered("Fa_C0", RecordSeverity.Error, "bearing_type"), Warn("e_lim", null, 5), Warn("X", null, 5), Warn("X_0", null, 5), Warn("Y", null, 10), Warn("Y_0", null, 10)),

        T("26", "bearing.aiso", "Bearing a_ISO", "bearing.aiso",
            "designation[text],contamination[text],kappa[-],a_iso[-]",
            "One record per row (a designation and contamination level).",
            Req("designation", "kappa", "a_iso"), Pos("kappa", "a_iso"), Warn("a_iso", 0.1, 50), Warn("kappa", 0.1, 4)),

        T("27", "fluid.expansion", "Heat-transfer fluid expansion", "fluid.expansion",
            "fluid[text],concentration[%],t_fill[degC],t_max[degC],e_fluid[-]",
            "One record per row. t_fill and t_max are stored and never applied.",
            Req("t_fill", "t_max", "e_fluid"), Pos("e_fluid"), Lt("t_fill", "t_max"), Warn("e_fluid", 0.001, 0.2), Warn("concentration", 0, 60)),

        T("28", "emitter.rating", "Emitter exponent", "emitter.rating",
            "emitter[text],n[-]",
            "One record per row (an emitter).",
            Req("n"), Pos("n"), Uniq("emitter"), Warn("n", 1.1, 1.5)),

        T("29", "psu.derating", "PSU derating", "psu.derating",
            "psu[text],theta[degC],altitude[m],f_derate[-]",
            "One record per row (a PSU, ambient and altitude). theta and altitude are stored and never applied.",
            Req("theta", "f_derate"), Pos("f_derate"), AtMost(1, "f_derate"), Warn("f_derate", 0.1, null), Warn("theta", -20, 70),
            Mono(["psu", "altitude"], "theta", "f_derate", RecordDirection.NotRising)),

        T("30", "battery.factors", "Battery factors", "battery.factors",
            "battery[text],theta[degC],DoD[-],eta_b[-],k_temp[-],k_age[-],k_p[-],H_rate[h]",
            "One record per row (a battery and temperature).",
            Req("DoD", "eta_b", "k_temp", "k_age"), Pos("DoD", "eta_b", "k_temp", "k_age"), AllOrNone("k_p", "H_rate"),
            Warn("DoD", 0.1, 1), Warn("eta_b", 0.5, 1), Warn("k_temp", 0.3, 1.2), Warn("k_age", 0.5, 1), Warn("k_p", 1, 1.5), Warn("H_rate", 1, 100)),

        T("31", "oring.gland", "O-ring squeeze, fill and stretch bands", "oring.criteria",
            "basis[text],q_lo_s[%],q_hi_s[%],q_lo_d[%],q_hi_d[%],fill_lo[%],fill_hi[%],x_lo[%],x_hi[%],od_hi[%]",
            "One record per row (a basis). The calculator's kind is oring.criteria.",
            Req("basis"), Uniq("basis"), Lt("q_lo_s", "q_hi_s", orEqual: true), Lt("q_lo_d", "q_hi_d", orEqual: true), Lt("fill_lo", "fill_hi", orEqual: true), Lt("x_lo", "x_hi", orEqual: true),
            Warn("x_hi", 1, 10), Warn("od_hi", 1, 10)),

        T("32", "oring.sizes", "O-ring sizes with tolerances", "oring.ring",
            "size[text],d_r[mm],d_r_tol[mm],W[mm],W_tol[mm]",
            "One record per row (a size). The calculator's kind is oring.ring.",
            Req("size", "d_r", "W"), Uniq("size"), Pos("d_r", "W"), NonNeg("d_r_tol", "W_tol"), Warn("W_tol", null, 0.4)),

        T("33", "oring.extrusion-limits", "Permitted extrusion gap", "oring.extrusion",
            "H[-],p[bar],gap[mm]",
            "One record per file: the extrusion table 'ext'. The single value gap_allow has no template.",
            Req("H", "p", "gap"), NonNeg("H", "p", "gap"), Uniq("H", "p"), Rows(max: 100), Mono(["H"], "p", "gap", RecordDirection.NotRising), Warn("H", 40, 95)),

        T("34", "asme.b31-3.allowable-stress", "Allowable displacement-stress range", "pipe.allowable-range",
            "basis[text],Sa[MPa]",
            "One record per row (a basis). The calculator's kind is pipe.allowable-range.",
            Req("basis", "Sa"), Uniq("basis"), Pos("Sa"), Warn("Sa", 5, 500)),

        Superseded("35", "asme.b31-3.stress-range", "Stress range reduction factor", "N_min[-],N_max[-],f_stress[-]",
            "Superseded: B8 reads the allowable stress range Sa as one value, so the factor f is already inside it."),

        Superseded("36", "asme.b31-3.thermal", "Thermal expansion and modulus tables", "material[text],t_metal[degC],e_expansion[mm/m],E_mod[GPa]",
            "Covered by the material library: B8 takes E and alpha from a material (templates 62 and 63)."),

        Superseded("37", "containment.cable-factors", "Cable factors for conduit and trunking", "cable_type[text],csa[mm2],F_conduit_straight[-],F_conduit_bends[-],F_trunking[-]",
            "Superseded: E8 sizes the fill from cable areas and a space factor, not from cable factors."),

        T("38", "containment.conduit-trunking", "Containment fill limit", "containment.space-factor",
            "arrangement[text],f_max[-]",
            "One record per row (an arrangement). The calculator's kind is containment.space-factor.",
            Req("arrangement", "f_max"), Uniq("arrangement"), Warn("f_max", null, 0.6)),

        T("39", "en10270.spring-wire", "Spring wire tensile strength", "spring.wire-strength",
            "grade[text],d[mm],Rm[MPa]",
            "One record per row (a grade and diameter). The grade and d cells join the title.",
            Req("grade", "d", "Rm"), Pos("d", "Rm"), Uniq("grade", "d"), Warn("Rm", 300, 3500), Mono(["grade"], "d", "Rm", RecordDirection.NotRising, RecordSeverity.Warning)),

        T("40", "en13906.permissible-stress", "Permissible and ultimate stress fractions", "spring.permissible",
            "wire_grade[text],condition[text],d_min[mm],d_max[mm],f_tau[-],f_sigma[-],f_su[-]",
            "One record per row (a wire grade, condition and diameter band). d_min and d_max are stored and never applied.",
            Req("wire_grade", "condition", "d_min", "d_max", "f_tau", "f_su"), Pos("d_min", "d_max", "f_tau", "f_sigma", "f_su"), Lt("d_min", "d_max"), Uniq("wire_grade", "condition", "d_min"),
            Warn("f_tau", 0.2, 0.8), Warn("f_sigma", 0.3, 1), Warn("f_su", 0.4, 0.9)),

        T("41", "climate.design-temperatures", "External design temperature", "climate.design-temperature",
            "location[text],theta_e[degC]",
            "One record per row (a location). The calculator's kind is climate.design-temperature.",
            Req("location", "theta_e"), Uniq("location")),

        Proposed("42", "room.internal-temperatures", "Internal design temperatures", "room.internal-temperatures",
            "room_type[text],theta_i[degC]",
            "Proposed kind: B4 is built but types the room temperature by hand."),

        Proposed("43", "room.air-change-rates", "Air change rates", "room.air-change-rates",
            "room_type[text],n_ach[1/h]",
            "Proposed kind: B4 is built but types the air change rate by hand."),

        Proposed("44", "ground.u-values", "Ground floor U-value inputs", "ground.u-values",
            "construction[text],lambda_g[W/(m*K)],B_prime[m],R_f[m2*K/W],U_g[W/(m2*K)]",
            "Proposed kind: B4 takes the ground floor U as an override."),

        T("45", "climate.monthly", "Monthly outside climate", "climate.monthly",
            "month[text],theta_e[degC],RH_e[%],days[d]",
            "One record per file: the twelve monthly rows (table 'climate').",
            Rows(min: 12, max: 12), Req("theta_e", "RH_e"), Uniq("month"), Warn("theta_e", -30, 40), Warn("RH_e", 20, null), Warn("days", 28, 31)),

        T("46", "insulation.bs5422", "Insulation heat-loss, surface-temperature and dew-point limits", "criteria.insulation",
            "purpose[text],q_max_m[W/m],q_max_A[W/m2],theta_se_max[degC],dew_margin[K]",
            "One record per row (a purpose). The calculator's kind is criteria.insulation.",
            Req("purpose"), Uniq("purpose"), Either("q_max_m", "q_max_A", "theta_se_max", "dew_margin"), Pos("q_max_m", "q_max_A", "theta_se_max"), NonNeg("dew_margin"),
            Warn("q_max_m", 1, 1000), Warn("q_max_A", 1, 500), Warn("theta_se_max", 30, 70), Warn("dew_margin", null, 5)),

        T("47", "device.ratings", "Preferred device ratings", "device.ratings",
            "I_n[A]",
            "One record per file: the E4 preferred-ratings table.",
            Req("I_n"), Pos("I_n"), Uniq("I_n"), Rows(max: 60), Ordered("I_n", RecordSeverity.Warning)),

        Proposed("48", "surface.resistances", "Surface resistances and air-gap resistance", "surface.resistances",
            "Rsi[m2*K/W],Rse[m2*K/W],Rg[m2*K/W]",
            "Proposed kind: B4 no longer lists it; the surface resistances are conventional values chosen by the flow column."),

        T("49", "material.conductivity", "Thermal conductivity of construction layers", "material.conductivity",
            "name[text],lambda[W/(m*K)]",
            "One record per file: only the name and lambda columns of the layers table. Applying it replaces the whole table, so d and element are entered after.",
            Req("name", "lambda"), Uniq("name"), Pos("lambda"), Rows(max: 200)),

        T("50", "thermal.bridging", "Thermal-bridge allowance", "thermal.bridging",
            "construction[text],y_tb[W/(m2*K)]",
            "One record per row (a construction).",
            Req("construction", "y_tb"), Uniq("construction"), NonNeg("y_tb"), Warn("y_tb", null, 0.3)),

        T("51", "joint.friction", "Friction coefficient plate to grout", "joint.friction",
            "grout[text],C_fd[-]",
            "One record per row (a grout).",
            Req("grout", "C_fd"), Uniq("grout"), NonNeg("C_fd")),

        T("52", "spring.seating", "End-support coefficient for spring buckling", "spring.seating",
            "support[text],nu_seat[-]",
            "One record per row (a support condition).",
            Req("support", "nu_seat"), Uniq("support"), Pos("nu_seat"), Warn("nu_seat", 0.5, 2)),

        T("53", "spring.endurance", "Alternating endurance strength of the wire", "spring.endurance",
            "wire[text],stress[text],N_c[-],S_e[MPa]",
            "One record per row (a wire, stress type and life). The wire, stress and N_c cells join the title.",
            Req("wire", "stress", "N_c", "S_e"), Pos("N_c", "S_e"), Uniq("wire", "stress", "N_c"), Warn("S_e", 50, 2000), Warn("N_c", 1e4, 1e10)),

        T("54", "accumulator.rules", "Accumulator pre-charge limits", "accumulator.rules",
            "type[text],basis[text],r_0[-],r_p[-]",
            "One record per row (a type and pressure basis).",
            Req("type", "basis", "r_0", "r_p"), Pos("r_0", "r_p"), Uniq("type", "basis"),
            TextWarn("basis", "gauge", "The basis is gauge: the same numbers read as absolute pressure give a different ratio. Check it before you use the record.")),

        T("55", "accumulator.sizes", "Accumulator nominal gas volumes", "accumulator.sizes",
            "V[L]",
            "One record per file: the F2 size list.",
            Req("V"), Pos("V"), Uniq("V"), Rows(max: 60), Ordered("V", RecordSeverity.Warning)),

        T("56", "glaser.layers", "Glaser layers: conductivity and vapour resistance", "material.conductivity",
            "element[text],name[text],d[mm],lambda[W/(m*K)],mu[-],timber[-]",
            "One record per file: the B7 layers table. Shares the kind with template 49.",
            Req("d", "lambda", "mu"), Pos("d", "lambda", "mu"), OneOf("timber", 0, 1), Rows(max: 30), Warn("d", 1, 500), Warn("lambda", 0.01, 5), Warn("mu", null, 1e5)),

        T("57", "surface.resistance", "Surface resistances for the condensation check", "surface.resistance",
            "case[text],Rsi[m2*K/W],Rse[m2*K/W],Rsi_f[m2*K/W]",
            "One record per row (a case).",
            Req("case", "Rsi", "Rse", "Rsi_f"), Uniq("case"), NonNeg("Rsi", "Rse", "Rsi_f"), Warn("Rsi", 0.04, 0.25), Warn("Rse", 0.04, 0.25), Warn("Rsi_f", 0.04, 0.5)),

        T("58", "climate.humidity-class", "Internal humidity class by moisture excess", "climate.humidity-class",
            "class[text],dv_a[g/m3],theta_a[degC],dv_b[g/m3],theta_b[degC],dp_f[-]",
            "One record per row (a class).",
            Req("class", "dv_a", "theta_a", "dv_b", "theta_b", "dp_f"), Uniq("class"), NonNeg("dv_a", "dv_b"), Lt("theta_a", "theta_b"), Warn("dv_a", null, 20), Warn("dv_b", null, 20), Warn("dp_f", null, 1.5),
            Lt("dv_b", "dv_a", orEqual: true, severity: RecordSeverity.Warning)),

        T("59", "criteria.condensation", "Critical surface humidity and condensate limit", "criteria.condensation",
            "basis[text],RH_crit[%],Mc_limit[g/m2]",
            "One record per row (a basis).",
            Req("basis", "RH_crit"), Uniq("basis"), Pos("RH_crit"), AtMost(100, "RH_crit"), Warn("RH_crit", 50, 100), Warn("Mc_limit", 1, 5000)),

        T("60", "pcb.method", "PCB trace curve-fit constants", "pcb.method",
            "method[text],k_ext[-],k_int[-],n_dT[-],n_A[-],t_oz[mil]",
            "One record per row (a method).",
            Req("method", "k_ext", "k_int", "n_dT", "n_A", "t_oz"), Uniq("method"), Pos("k_ext", "k_int", "n_dT", "n_A", "t_oz"), Lt("k_int", "k_ext", orEqual: true, severity: RecordSeverity.Warning)),

        T("61", "shaft.marin", "Marin surface factor", "shaft.marin",
            "finish[text],k_a[-]",
            "One record per row (a surface finish).",
            Req("finish", "k_a"), Uniq("finish"), Pos("k_a"), Warn("k_a", 0.1, 1)),

        T("62", "material.properties", "Material properties", MaterialLibrary.Kind,
            "material[text],E[GPa],G[GPa],nu[-],rho[kg/m3],alpha[1/K],lambda[W/(m*K)],f_y[MPa],f_u[MPa],Rm_A[MPa],Rm_m[-],eps_perm_once[-],eps_perm_repeated[-]",
            "One material per row. The material cell is the record's title.",
            Req("material"), Uniq("material"), Either("E", "G", "nu", "rho", "alpha", "lambda", "f_y", "f_u", "Rm_A", "Rm_m", "eps_perm_once", "eps_perm_repeated"),
            Pos("E", "G", "rho", "lambda", "f_y", "f_u", "Rm_A")),

        T("63", "material.temperature-tables", "Material property against temperature", MaterialLibrary.Kind,
            "material[text],property[text],T[degC],v[-],v_unit[text]",
            "Adds a temperature table (E(T) and the others) to a material that is already in the library.",
            Req("material", "property", "T", "v")),

        T("64", "material.secant-curve", "Material secant modulus against strain", MaterialLibrary.Kind,
            "material[text],eps[-],Es[MPa]",
            "Adds the Es_curve table to a material that is already in the library.",
            Req("material", "eps", "Es"), Pos("Es"), NonNeg("eps")),

        T("65", "material.friction-pairs", "Material friction coefficient by pair", MaterialLibrary.Kind,
            "material[text],pair[text],mu[-]",
            "Adds the mu_pairs table to a material that is already in the library.",
            Req("material", "pair", "mu"), NonNeg("mu"), AtMost(5, "mu"), Uniq("material", "pair"), Warn("mu", null, 1)),

        T("66", "cylinder.mounting", "Cylinder mounting effective-length factor", "cylinder.mounting",
            "mounting[text],K_m[-]",
            "One record per row (a mounting).",
            Req("mounting", "K_m"), Uniq("mounting")),

        T("67", "cylinder.buckling-safety", "Rod buckling safety factor", "cylinder.buckling-safety",
            "basis[text],S_f[-]",
            "One record per row (a basis).",
            Req("basis", "S_f"), Uniq("basis"), Warn("S_f", 2, null)),

        T("68", "cylinder.hose-sizes", "Hose and pipe inside diameters", "cylinder.hose-sizes",
            "ID[mm]",
            "One record per file: the F1 hose-size list (table 'hoses').",
            Req("ID"), Pos("ID"), Uniq("ID"), Rows(max: 60), Ordered("ID", RecordSeverity.Warning)),

        T("69", "snapfit.friction", "Snap-fit coefficient of friction", "snapfit.friction",
            "pair[text],mu[-]",
            "One record per row (a pair).",
            Req("pair", "mu"), Uniq("pair"), NonNeg("mu"), Warn("mu", 0.05, 1)),

        T("70", "snapfit.annular-factor", "Annular snap strain factor", "snapfit.annular-factor",
            "shape[text],K_ann[-]",
            "One record per row (a shape).",
            Req("shape", "K_ann"), Uniq("shape"), Warn("K_ann", 0.5, 3)),

        T("71", "snapfit.annular-force-factor", "Annular snap force factor", "snapfit.annular-force-factor",
            "arrangement[text],K_F[-]",
            "One record per row (an arrangement).",
            Req("arrangement", "K_F"), Uniq("arrangement"), Warn("K_F", 0.5, 5)),

        T("72", "snapfit.secant-modulus", "Secant modulus at the design strain", "snapfit.secant-modulus",
            "basis[text],Es[MPa]",
            "One record per row (a basis).",
            Req("basis", "Es"), Uniq("basis"), Pos("Es"), Warn("Es", 10, 20000)),

        T("73", "plate.deflection-limit", "Plate deflection limit", "plate.deflection-limit",
            "application[text],defl_ratio[-]",
            "One record per row (an application).",
            Req("application", "defl_ratio"), Uniq("application"), Warn("defl_ratio", 50, 2000)),

        T("74", "pipe.section", "Pipe outside diameter and wall", "pipe.section",
            "pipe[text],OD[mm],t_wall[mm]",
            "One record per row (a pipe).",
            Req("pipe", "OD", "t_wall"), Uniq("pipe"), Pos("OD", "t_wall"), Lt("t_wall", "OD", factor: 0.5), Warn("OD", 10, 1500), Warn("t_wall", 0.5, 60)),

        T("75", "pipe.screening-k1", "Screening limit K1", "pipe.screening-k1",
            "code[text],K_scr[mm2/m2]",
            "One record per row (a piping code).",
            Req("code", "K_scr"), Uniq("code"), Pos("K_scr"), Warn("K_scr", 1, 1000)),

        T("76", "bellows.data", "Axial bellows data", "bellows.data",
            "bellows[text],A_eff[cm2],k_b[N/mm],dL_rated[mm]",
            "One record per row (a bellows).",
            Req("bellows", "A_eff", "k_b", "dL_rated"), Uniq("bellows"), Pos("A_eff", "dL_rated"), NonNeg("k_b"), Warn("A_eff", 1, 5000), Warn("k_b", 1, 5000), Warn("dL_rated", 1, 500)),

        T("77", "surface.coefficient", "Outer surface coefficient", "surface.coefficient",
            "surface[text],h_se[W/(m2*K)]",
            "One record per row (a surface).",
            Req("surface", "h_se"), Uniq("surface"), Pos("h_se"), Warn("h_se", 3, 40)),

        T("78", "surface.emissivity", "Surface emissivity", "surface.emissivity",
            "finish[text],eps[-]",
            "One record per row (a finish).",
            Req("finish", "eps"), Uniq("finish"), Warn("eps", 0.05, null)),

        T("79", "cable.bend-radius", "Minimum bend radius multiple", "cable.bend-radius",
            "cable[text],n_bend[-]",
            "One record per row (a cable).",
            Req("cable", "n_bend"), Uniq("cable"), Warn("n_bend", null, 30)),

        T("80", "containment.friction", "Pulling friction coefficient", "containment.friction",
            "pair[text],mu[-]",
            "One record per row (a pair).",
            Req("pair", "mu"), Uniq("pair"), NonNeg("mu"), Warn("mu", null, 1)),

        T("81", "cable.pulling-limit", "Pulling tension, grip and sidewall limits", "cable.pulling-limit",
            "bundle[text],T_grip[N],P_lim[N/m],k_n[-]",
            "One record per row (a bundle).",
            Req("bundle"), Uniq("bundle"), Pos("T_grip", "P_lim", "k_n"), AtMost(1, "k_n")),

        T("82", "containment.weight-correction", "Cable weight-correction factor", "containment.weight-correction",
            "arrangement[text],w_c[-]",
            "One record per row (an arrangement).",
            Req("arrangement", "w_c"), Uniq("arrangement"), Warn("w_c", null, 3)),
    ];
}
