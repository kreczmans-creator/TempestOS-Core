using static Tempest.Calc.Library.RecordSeeds.RecordSeedLicence;

namespace Tempest.Calc.Library.RecordSeeds;

/// <summary>
/// Every bundled reference-record set (owner comment 5 and its refinement 5a, 2026-10-29): what each CSV in <c>RecordSeeds/Data</c> holds, where
/// its values were read, the licence status, the paid standard a secondary source reproduces, and how the values were cross-checked. No value is
/// transcribed from a paid standard document: each comes from an openly published web page, a manufacturer's datasheet, an open dataset or a
/// formula. <c>docs/testing/v0.26.0-record-sources.md</c> is the owner's list of the same sets.
/// </summary>
public static class RecordSeedCatalogue
{
    private const string Read = RecordSeedSet.ReadDate;

    private static RecordSeedSource S(string title, string url, RecordSeedLicence licence) => new(title, url, Read, licence);

    /// <summary>Every set, in the order the owner list gives them.</summary>
    public static IReadOnlyList<RecordSeedSet> All { get; } = Build();

    /// <summary>
    /// Every record kind a calculator declares that no bundled set fills, with the reason, in plain words. A test holds this list to the calculators:
    /// each declared kind is either filled by a set or named here.
    /// </summary>
    public static IReadOnlyDictionary<string, string> NotBundled { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["material.conductivity"] = "The record is a construction's layer build-up (thickness, conductivity and vapour resistance of each layer), not reference data. Each project enters its own.",
        ["material.properties"] = "The material library already ships four generic starting materials; a design takes the grade's own data.",
        ["cable.correction-factors"] = "No open page states the ambient, grouping or insulation factor tables, or the formula behind them, well enough to cite.",
        ["cable.impedance"] = "The datasheets found give conductor resistance but no reactance, and the template needs both.",
        ["cable.pulling-limit"] = "Pulling limits belong to one cable maker's installation data; none was found published openly.",
        ["containment.friction"] = "Pulling friction depends on the sheath, the containment and the lubricant; no open table was found.",
        ["containment.weight-correction"] = "The factor depends on the ratio of cable to conduit diameter; there is no single value to ship.",
        ["device.characteristic"] = "A time–current curve belongs to one exact device; read it from that device's datasheet.",
        ["motor.rating"] = "Candidate motor data belong to one motor's datasheet.",
        ["motor.start"] = "Starting data belong to one motor's datasheet.",
        ["starter.limits"] = "Current limits belong to one soft-starter or drive and its settings.",
        ["transformer.data"] = "Impedance belongs to one transformer's rating plate or test certificate.",
        ["psu.derating"] = "A derating curve belongs to one power supply's datasheet.",
        ["battery.factors"] = "Battery factors belong to one battery's datasheet and chemistry.",
        ["bearing.aiso"] = "a_ISO comes from a maker's life tool for the lubricant and contamination; it is not a table.",
        ["bellows.data"] = "Bellows data belong to one bellows' datasheet.",
        ["climate.humidity-class"] = "Only part of the humidity-class table was found on an open page (classes 3 and 4); a partial set could mislead.",
        ["criteria.insulation"] = "The limits are the project specification's, or BS 5422's; no open source was found.",
        ["surface.coefficient"] = "h_se depends on the surface, the air speed and the temperatures; the calculator's correlation works it out.",
        ["ground.u-values"] = "Ground floor U-values depend on the floor's size, soil and insulation; the method is in a paid standard.",
        ["shaft.limits"] = "Open sources disagree widely on slope limits; the bearing maker's figure for the bearing in use is needed.",
        ["shaft.marin"] = "The calculator works the surface factor out from the finish and the strength when it is left blank.",
        ["shaft.material"] = "The strengths belong to the bar's grade, size and certificate.",
        ["snapfit.annular-factor"] = "The factor depends on the ring shape; no open table was found.",
        ["snapfit.annular-force-factor"] = "The factor depends on the arrangement; no open table was found.",
        ["snapfit.secant-modulus"] = "The secant modulus belongs to one plastic grade at the design strain and temperature.",
        ["spring.permissible"] = "The permissible-stress fractions are in a paid standard's diagrams; the calculator opens on generic textbook fractions.",
        ["oring.extrusion"] = "The extrusion chart belongs to one seal maker's compound; it is read as points from that chart.",
        ["plate.deflection-limit"] = "The limit is the project specification's; the calculator opens on span/250.",
        ["cylinder.buckling-safety"] = "No open source gave a safety factor; the calculator's default is a placeholder.",
        ["lifting.design-factors"] = "The lifting calculator is behind a feature flag and its records are never shipped (owner rule for that calculator).",
        ["lifting.allowable-stress"] = "The lifting calculator is behind a feature flag and its records are never shipped (owner rule for that calculator).",
        ["lifting.limits"] = "The lifting calculator is behind a feature flag and its records are never shipped (owner rule for that calculator).",
    };

    /// <summary>The set with this id, or <see langword="null"/>.</summary>
    public static RecordSeedSet? Find(string id) => All.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));

    private static List<RecordSeedSet> Build()
    {
        var bayer = S("Bayer MaterialScience, Snap-Fit Joints for Plastics – A Design Guide", "https://cdn.sparkfun.com/assets/home_page_posts/1/4/1/0/Plastic_Snap_fit_design.pdf", ManufacturerToConfirm);
        var roymech = S("Roymech, Steel to EN 10025", "https://roymech.org/Useful_Tables/Matter/Steel_Data/Steel-10025", SecondaryToConfirm);
        var eland = S("Eland Cables, BS 6004 624-Y Twin and Earth PVC Cable datasheet (566633)", "https://static.rapidonline.com/pdf/566633_v1.pdf", ManufacturerToConfirm);
        var proPower = S("Pro Power (Farnell), BS 6004 624-Y Twin and Earth PVC Cable datasheet", "https://www.farnell.com/datasheets/2693671.pdf", ManufacturerToConfirm);
        var mcsConditions = S("MCS Heat Load Calculator, Design Conditions", "https://heatloadcalculator.mcscertified.com/docs/reference-sources/design-conditions", SecondaryToConfirm);
        var nasa = S("NASA POWER climatology API v2.10.1 (T2M, RH2M; 2001–2025, MERRA-2)", "https://power.larc.nasa.gov/api/temporal/climatology/point", OpenData);
        var mycable = S("myCableEngineering, The adiabatic equation", "https://mycableengineering.com/the-adiabatic-equation/", SecondaryToConfirm);
        var ies = S("IES VE help, Table 12: Winter design temperatures and air changes", "https://help.iesve.com/ve2024/table_12_winter_design_temperatures_and_air_changes.htm", SecondaryToConfirm);
        var steelcalcK = S("steelcalculator, K-factor for steel columns", "https://steelcalculator.pages.dev/reference/column-k-factor/", SecondaryToConfirm);

        return
        [
            // ---------------------------------------------------------------- Required kinds
            new("snapfit-friction-bayer", "69", "Snap-fit friction, plastics (Bayer guide)", ManufacturerToConfirm,
                [bayer, S("SABIC/GE snap-fit design manual excerpt (same-material values)", "https://www.scribd.com/doc/297616980/snap-fit-joints", SecondaryToConfirm)],
                "Table 3 (friction of plastics on steel)", "Bayer design guide, web copy", null,
                "Low and high ends of each range on steel as printed. Same-plastic pairs are derived as the high end times the factor the table gives in brackets (Table 3 note; its worked example uses PC on PC = 0.50 x 1.2 = 0.6). Cross-check: the SABIC excerpt gives PBT 0.35–0.40 and POM 0.20–0.35, as here. Neither end is conservative for every check: use the high end for the assembly force and the low end for the retention."),

            new("spring-wire-a228-gibbs", "39", "Music wire ASTM A228 tensile strength (Gibbs Wire)", ManufacturerToConfirm,
                [S("Gibbs Wire & Steel, Phosphate coated music wire technical data sheet No. 1004 01/14", "https://www.gibbswire.com/assets/PMW -phos coated music wire.pdf", ManufacturerToConfirm),
                 S("ASTM A228/A228M-16 public preview pages (cross-check only)", "https://cdn.standards.iteh.ai/samples/96199/8436585562f54449b3966f931f7865d3/ASTM-A228-A228M-16.pdf", SecondaryToConfirm)],
                "Tensile Strength Table (ASTM-A228 spec)", "Data sheet No. 1004, 01/14", "ASTM A228/A228M Table 1",
                "The minimum tensile strength of each listed diameter (the conservative end of the band), converted from psi (x 0.00689476 MPa) and from inches (x 25.4 mm). Cross-check: the preview pages give 1.0 mm at 2150 MPa minimum and 2.0 mm at 1950 MPa; the sheet gives 2172 MPa at 1.016 mm and 1944 MPa at 2.032 mm."),

            new("spring-endurance-zimmerli", "53", "Spring wire endurance (Zimmerli data)", Derived,
                [S("AMES Web, Compression spring fatigue design (Zimmerli 1957 data)", "https://amesweb.info/MechanicalSprings/CompressionSpringFatigueDesign.aspx", SecondaryToConfirm),
                 S("AMES Web, Compression spring fatigue calculator", "https://amesweb.info/MechanicalSprings/compression-spring-fatigue-calculator.aspx", SecondaryToConfirm)],
                "Zimmerli torsional endurance, wire under 10 mm", "Zimmerli, The Mainspring, 1957", null,
                "Zimmerli's pulsating test points: unpeened S_sa = 241 MPa at S_sm = 379 MPa, shot-peened S_sa = 398 MPa at S_sm = 534 MPa, for spring steels under 10 mm whatever their strength. Derived: S_e = S_sa. A Goodman line from S_e to any S_su passes on or below Zimmerli's point, so this S_e is conservative for every wire."),

            new("containment-space-factor-uk", "38", "Containment space factor (IET On-Site Guide method)", SecondaryToConfirm,
                [S("Marshall-Tufflex, Spacing factors: a brief guide (Voltimum)", "https://www.voltimum.co.uk/articles/spacing-factors-brief-guide", ManufacturerToConfirm),
                 S("TLC Direct, Electrician's Guide 4.5.3 Cable capacity of conduits and trunking", "https://tlc-direct.co.uk/Book/4.5.3.htm", SecondaryToConfirm),
                 S("IET EngX forum, Space factor for single cables inside trunking", "https://engx.theiet.org/f/wiring-and-regulations/25458/space-factor-for-single-cables-inside-trunking", SecondaryToConfirm)],
                "45 % space factor", "web pages", "IET On-Site Guide (BS 7671) containment capacity method",
                "Three sources give 45 % for conduit and trunking. One blog quotes 40 % for trunking against a regulation that holds no such figure, and its two versions disagree; it was not taken as a source."),

            new("containment-space-factor-nec", "38", "Conduit fill limit (NEC Chapter 9 Table 1)", SecondaryToConfirm,
                [S("ExpertCE, NEC Chapter 9 Table 1 conduit fill", "https://expertce.com/learn-articles/nec-chapter-9-table-1-conduit-fill/", SecondaryToConfirm),
                 S("Nassau National Cable, NEC conduit fill chart", "https://www.nassaunationalcable.com/blogs/blog/nec-conduit-fill-chart", SecondaryToConfirm)],
                "Chapter 9 Table 1", "NFPA 70 (NEC), as the pages give it", "NFPA 70 (NEC) Chapter 9 Table 1",
                "53 % for one cable, 31 % for two and 40 % for three or more; the sources agree."),

            new("device-ratings-mcb", "47", "Circuit-breaker preferred ratings 6 A to 125 A", SecondaryToConfirm,
                [S("Alibaba seller blog, Circuit breaker rated current selection", "https://seller.alibaba.com/blogs/2026/southeast-asia/electrical/mcb-breaker-current-rating-16a-25a-32a-63a-alibaba-guide", SecondaryToConfirm),
                 S("Endless Sphere forum, Fuses again (IEC 60898 preferred values)", "https://endless-sphere.com/sphere/threads/fuses-again.58804/post-878337", SecondaryToConfirm),
                 S("Hager MTN single-pole type B range 6 A to 63 A (yesss.co.uk listing)", "https://www.yesss.co.uk/hager-63a-single-pole-mcbs-6ka-b-type-mtn163", ManufacturerToConfirm)],
                "Clause 5.3.2 preferred values of rated current", "IEC 60898-1, as the pages give it", "IEC 60898-1 clause 5.3.2",
                "Two pages list the preferred values; one adds 8 A, the other does not. 8 A is left out: without it the calculator picks the next rating up, which then needs the larger cable. Hager's MTN range spans 6 A to 63 A."),

            new("max-zs-mcb-derived", "03", "Maximum Z_s for circuit-breakers, 230 V (derived)", Derived,
                [S("IET Wiring Matters, March 2023: Determining the maximum earth fault loop impedance for protective devices", "https://electrical.theiet.org/wiring-matters/years/2023/94-march-2023/determining-the-maximum-earth-fault-loop-impedance-for-protective-devices-to-bs-en-60898-bs-en-60947-2/", SecondaryToConfirm),
                 S("NICEIC, Determining the maximum earth fault loop impedance", "https://niceic.com/newsletter/determining-the-maximum-earth-fault-loop-impedance", SecondaryToConfirm),
                 S("Open Exam Prep, City & Guilds 2365 study guide 13.4 (B32 1.37 ohm)", "https://open-exam-prep.com/study-guides/uk-city-guilds-2365-l2/supply-systems-earthing-protection/ads-shock-protection-zs", SecondaryToConfirm)],
                "Z_s,max = C_min·U_0/I_a with C_min 0.95 and U_0 230 V", "derived", "BS 7671 Regulation 411.4.5 (the formula and C_min)",
                "Derived for each rating with C_min = 0.95 (CLC/TR 50480, as IET and NICEIC give it) and I_a = 5 I_n (type B), 10 I_n (type C), 20 I_n (type D); the same value serves 0.4 s and 5 s for a circuit-breaker. Cross-check: the derived B32 value 1.366 ohm rounds to the 1.37 ohm the study guide quotes. These are the values for calculation; a measured Z_s is compared with 0.8 of them. The calculator always checks its own derived Z_s,max too."),

            new("climate-monthly-london", "45", "Monthly climate, London (NASA POWER)", OpenData,
                [nasa with { Url = "https://power.larc.nasa.gov/api/temporal/climatology/point?parameters=T2M,RH2M&community=RE&longitude=-0.1278&latitude=51.5074&format=JSON" },
                 S("Wikipedia, Climate of London (NOAA Gatwick 1961–1990 means, cross-check)", "https://en.wikipedia.org/wiki/Climate_of_London", SecondaryToConfirm)],
                "Monthly mean T2M and RH2M at 51.507 N, 0.128 W", "climatology 2001–2025", null,
                "The grid cell's monthly mean air temperature and relative humidity at 2 m, as the API returns them. Cross-check: NOAA's Gatwick 1961–1990 means are 3.5 °C in January and 16.5 °C in July (here 4.14 and 17.63); the later period and the city cell explain the gap. A grid cell is not your site: use the site's own data for a design."),
            Climate("birmingham", "Birmingham", "52.4862", "-1.8904", "52.486 N, 1.890 W", nasa),
            Climate("manchester", "Manchester", "53.4808", "-2.2426", "53.481 N, 2.243 W", nasa),
            Climate("edinburgh", "Edinburgh", "55.9533", "-3.1883", "55.953 N, 3.188 W", nasa),
            Climate("glasgow", "Glasgow", "55.8642", "-4.2518", "55.864 N, 4.252 W", nasa),
            Climate("cardiff", "Cardiff", "51.4816", "-3.1791", "51.482 N, 3.179 W", nasa),
            Climate("belfast", "Belfast", "54.5973", "-5.9301", "54.597 N, 5.930 W", nasa),
            Climate("newcastle", "Newcastle", "54.9783", "-1.6178", "54.978 N, 1.618 W", nasa),

            new("pipe-sa-a106b", "34", "Allowable stress range Sa, A106 Grade B (derived)", SecondaryToConfirm,
                [S("Project Materials, What is ASTM A106 Grade B (allowable stress by temperature)", "https://www.blog.projectmaterials.com/quick-answers/pipes/what-is-astm-a106-grade-b/", SecondaryToConfirm),
                 S("API 570 exam study guide, ASME B31.3 module 9 part 5", "https://hareeshkv.blogspot.com/2021/03/asme-b-31.3-api-570-study-guide-module9-part5.html", SecondaryToConfirm)],
                "Sa = f(1.25 Sc + 0.25 Sh), f = 1", "derived from the pages' S values", "ASME B31.3 Table A-1M (S values) and equation 1a",
                "S = 138 MPa up to 100 °C and 134 MPa at 200 °C. At 300 °C one page gives 118 MPa and the other 126 MPa: 118 MPa, the lower, is used. Sc = 138 MPa at the cold end. Sa = 1.25 Sc + 0.25 Sh with f = 1 (7000 cycles or fewer): 207.0, 206.0 and 202.0 MPa. The liberal form with (Sh − SL) added is not used."),

            new("pipe-sch40", "74", "Steel pipe Schedule 40 dimensions (NPS 1/2 to 12)", SecondaryToConfirm,
                [S("Engineers Edge, Schedule 40 steel pipe sizes and dimensions ASME/ANSI B36.10/19", "https://engineersedge.com/fluid_flow/steel-pipe-schedule-40.htm", SecondaryToConfirm),
                 S("Engineering ToolBox, ANSI steel pipes dimensions", "https://www.engineeringtoolbox.com/ansi-steel-pipes-d_305.html", SecondaryToConfirm)],
                "Schedule 40 outside diameter and wall", "ASME B36.10M, as the pages give it", "ASME B36.10M dimension tables",
                "The inch values converted (x 25.4 mm, two decimals): NPS 1/2 is 21.34 mm by 2.77 mm. Engineering ToolBox gives the same diameters and the walls rounded to 0.01 in. Nominal wall: deduct your mill tolerance and corrosion allowance."),

            new("pipe-k1-b31-3", "75", "Screening limit K1, ASME B31.3 (before 2016)", SecondaryToConfirm,
                [S("Piping Analysis blog, Flexibility analysis based on ASME B31.3", "https://pipinganalysis.blogspot.com/2011/07/flexibility-analysis-based-on-asme-b313.html", SecondaryToConfirm),
                 S("Colorado DNR record scan of ASME B31.3 319.4.1", "https://dnrweblink.state.co.us/DRMSSearch/0/doc/1336347/Page94.aspx", SecondaryToConfirm)],
                "319.4.1 equation 16", "editions before 2016", "ASME B31.3 paragraph 319.4.1, equation 16",
                "K1 = 208.3 in the SI form D·y/(L − U)² ≤ K1 (D and y in mm, L and U in m). Editions from 2016 give K1 = 208 000·SA/Ea instead; for carbon steel at ambient that is about 211, so 208.3 is the lower and more conservative figure."),

            // ---------------------------------------------------------------- Structural
            new("steel-en10025-2", "12", "Structural steel strengths by thickness (EN 10025-2)", SecondaryToConfirm,
                [roymech,
                 S("Metinvest, Steel grade S355J2", "https://www.metinvestholding.com/en/products/steel-grades/s355j2", ManufacturerToConfirm),
                 S("Arvedi, Non-alloyed structural steels as per EN 10025-2", "https://www.arvedi.it/en/acciaieria/products/hot-rolled-steels/non-alloyed-structural-steels-as-per-en-10025-2/", ManufacturerToConfirm),
                 S("Parker Steel, Declaration of performance S355J2 plate", "https://www.parkersteel.co.uk/webshared/media/pdf/dop/PS-DOP013-S355J2-PLATE.pdf", ManufacturerToConfirm)],
                "Minimum yield by thickness band; minimum tensile 3 mm to 100 mm", "EN 10025-2, as the pages give it", "EN 10025-2 Tables 7",
                "f_y by band (16, 40, 63, 80, 100 mm) and the minimum of the tensile range. Metinvest and Arvedi agree with Roymech. For S355 at 40 to 63 mm Roymech and Metinvest give 335 MPa and Parker Steel 325 MPa (its 40 to 80 mm band): 325 MPa, the lower, is used. The first band starts at 0 mm; below 3 mm the standard's tensile range is higher, so f_u is conservative there. S235 over 16 mm (225 and 215 MPa) is left out: the connection and base plate calculators take f_y from 235 MPa only."),

            new("concrete-en1992-classes", "19", "Concrete strength classes C20/25 to C50/60", Derived,
                [S("Structural Basics, Concrete and reinforcement properties for structural design", "https://structuralbasics.com/concrete-and-reinforcement-properties", SecondaryToConfirm),
                 S("Eurocode Applied, Concrete design properties", "https://cdn.eurocodeapplied.com/design/en1992/concrete-design-properties", SecondaryToConfirm)],
                "Strength class designation C f_ck/f_ck,cube", "derived from the class name", null,
                "A class name Cx/y states f_ck = x MPa (cylinder) and f_ck,cube = y MPa; both pages list the same pairs. C12/15, C16/20 and the classes above C50/60 are left out because the base plate calculator takes f_ck from 20 to 50 MPa only."),

            new("ec3-1-1-gamma-m0-uk", "13", "UK NA to EN 1993-1-1, gamma_M0", SecondaryToConfirm,
                [S("Bentley STAAD.Pro help, D5.B.4.1 Clause 6.1(1) partial safety factors (UK NA)", "https://docs.bentley.com/LiveContent/web/STAAD.Pro%20Help-v20/en/GUID-CFD528D8-8B3B-4F6E-8E24-95EF5297FB5B.html", SecondaryToConfirm),
                 S("SCI Advisory Desk note AD 388 (steelconstruction.info)", "https://www.SteelConstruction.info/images/c/c0/AD-388.pdf", SecondaryToConfirm)],
                "Clause 6.1(1)", "NA to BS EN 1993-1-1, as the pages give it", "NA to BS EN 1993-1-1 clause 6.1(1)",
                "gamma_M0 = 1.00, the UK value and the recommended value alike."),

            new("ec3-1-8-uk-factors", "14", "UK NA to EN 1993-1-8 and Table 3.3 spacings", SecondaryToConfirm,
                [S("Bentley STAAD.Pro help, D5.B.4.1 Clause 6.1(1) partial safety factors (UK NA)", "https://docs.bentley.com/LiveContent/web/STAAD.Pro%20Help-v20/en/GUID-CFD528D8-8B3B-4F6E-8E24-95EF5297FB5B.html", SecondaryToConfirm),
                 S("Eurocode Applied, Bolt design properties (EN 1993-1-8 Table 3.3)", "https://eurocodeapplied.com/design/en1993/bolt-design-properties", SecondaryToConfirm),
                 S("Hilti, steel connection handout (minimum spacings)", "https://files-ask.hilti.com/original/ld/ldhrjqf0nt.pdf", ManufacturerToConfirm)],
                "gamma_M0, gamma_M1, gamma_M2; Table 3.3 minimum e1, e2, p1, p2", "NA to BS EN 1993-1-1 and -1-8, as the pages give them", "NA to BS EN 1993-1-8 and EN 1993-1-8 Table 3.3",
                "gamma_M0 = gamma_M1 = 1.00 and gamma_M2 = 1.25 for bolts, welds and plates in bearing (the UK NA gives 1.10 for tension fracture of a cross-section; that is not this record). Minimum e1 = e2 = 1.2 d0, p1 = 2.2 d0, p2 = 2.4 d0; the two pages agree."),

            new("ec2-uk-na", "18", "UK NA to EN 1992-1-1, gamma_c and alpha_cc", SecondaryToConfirm,
                [S("Fine GEO5 help, Standard values of coefficients (national annexes)", "https://finesoftware.eu/help/geo5/en/standard-values-of-coefficients-01", SecondaryToConfirm),
                 S("Concise Eurocode 2 (scribd excerpt, UK NA values)", "https://www.scribd.com/doc/300666969/Concise-Eurocode-2-Concrete", SecondaryToConfirm)],
                "Table 2.1N and clause 3.1.6", "NA to BS EN 1992-1-1, as the pages give it", "NA to BS EN 1992-1-1 Table 2.1N and 3.1.6",
                "gamma_c = 1.5 for persistent and transient situations; alpha_cc = 0.85 in the UK (the recommended value is 1.0)."),

            new("ec3-ltb-recommended", "20", "Imperfection factors and LTB parameters (recommended)", SecondaryToConfirm,
                [S("Dlubal knowledge base, Imperfection coefficients according to EN 1993-1-1 Table 6.1", "https://www.dlubal.com/en/support-and-learning/support/knowledge-base/001622", SecondaryToConfirm),
                 S("Cyprus National Annex to EN 1993-1-1 (Ministry of Interior)", "https://moi.gov.cy/MOI/moiup/moi.nsf/all/05FE06BB537676ACC22585A100430DF8/$file/Cyprus_National_Annex_EN_1993-1-1.pdf", SecondaryToConfirm),
                 S("Steel Buildings in Europe, multi-storey worked example (lambda_LT,0 0.4, beta 0.75)", "https://infosteel.be/images/Steel-Buildings-in-Europe/MSB04/96", SecondaryToConfirm)],
                "Table 6.1 and 6.3.2.3", "EN 1993-1-1 recommended values, as the pages give them", "EN 1993-1-1 Table 6.1 and clause 6.3.2.3",
                "alpha = 0.13, 0.21, 0.34, 0.49, 0.76 for curves a0, a, b, c, d; lambda_LT,0 = 0.4 and beta = 0.75 (the recommended values, which a UK-focused page says the UK NA adopts for rolled sections)."),

            new("joint-friction-mortar", "51", "Base plate friction on sand-cement mortar", SecondaryToConfirm,
                [S("IDEA StatiCa, Shear force transfer in base plate by friction and anchors", "https://www.ideastatica.com/support-center/shear-force-transfer-in-base-plate-by-friction-and-anchors", SecondaryToConfirm),
                 S("LBV Brandenburg, Bautechnik Tipp 23-01", "https://lbv.brandenburg.de/download/Bautechnik/bautechnik_Tipp_23-01.pdf", SecondaryToConfirm)],
                "Clause 6.2.2(6)", "EN 1993-1-8, as the pages give it", "EN 1993-1-8 clause 6.2.2(6)",
                "C_f,d = 0.20 for sand-cement mortar; both pages agree. A draft revision raises it to 0.30; the lower value is kept."),

            new("en1990-recommended", "15", "EN 1990 Annex A1 recommended factors (buildings)", SecondaryToConfirm,
                [S("steelcalculator, UK load combinations (EN 1990 Table A1.1 psi factors)", "https://steelcalculator.pages.dev/reference/uk-load-combinations/", SecondaryToConfirm),
                 S("SCIA Engineer, Theory: national annexes to EN 1990 (Table A1.1)", "https://help.scia.net/19.1/en/pvt/attachments/theory_na_en_1990_enu.pdf", SecondaryToConfirm),
                 S("Cyprus National Annex to EN 1990 (Cyprus Organisation for Standardisation)", "https://www.cys.org.cy/images/stories/Cyprus_National_Annex_EN_1990.pdf", SecondaryToConfirm)],
                "Tables A1.1 and A1.2(B)", "EN 1990 Annex A1 recommended values", "EN 1990 Annex A1 Tables A1.1 and A1.2(B)",
                "The same set as the calculator's built-in 'EN recommended' choice (DC-07): gamma_G,sup 1.35, gamma_G,inf 1.00, gamma_Q 1.50, xi 0.85 and the psi factors of Table A1.1 for buildings. The pages confirm categories A to C, snow, wind and thermal; E (1.0, 0.9, 0.8) and H (0) are the values the calculator already ships under DC-07."),

            new("en1990-uk-na", "15", "UK NA to EN 1990, partial and psi factors (buildings)", SecondaryToConfirm,
                [S("steelcalculator, UK load combinations — EN 1990 + UK NA guide", "https://steelcalculator.pages.dev/reference/uk-load-combinations/", SecondaryToConfirm)],
                "Tables NA.A1.1 and NA.A1.2(B)", "NA to BS EN 1990:2002+A1:2005, as the page gives it", "NA to BS EN 1990 Tables NA.A1.1 and NA.A1.2(B)",
                "gamma_G,sup 1.35, gamma_G,inf 1.00, gamma_Q 1.50, xi 0.925. Only the categories the page lists are here (B, C, E, H, snow up to 1000 m, wind, thermal); A, D, F and G are left out because no second page confirmed them. Roofs take psi_0 = 0.7 as the page gives (the EN recommended value is 0), the higher and more conservative. The page also prints 1.35 x 0.925 as 1.228; the right product is 1.249, which its own worked example uses. One source only: check it against your copy."),

            // ---------------------------------------------------------------- Electrical
            new("conductor-material-iec60949", "01", "Conductor material constants (Cu, Al)", SecondaryToConfirm,
                [mycable,
                 S("myElectrical, The dc resistance of conductors", "https://myelectrical.com/notes/entryid/112/the-dc-resistance-of-conductors", SecondaryToConfirm)],
                "Material constants and typical k values", "IEC 60949 / IEC 60364-5-54 constants, as the page gives them", "IEC 60949 material constants",
                "beta, Q_c and rho_20 as the page gives them; alpha is derived as 1/(beta + 20). For aluminium the page gives rho_20 = 28.267e-6 ohm·mm and IEC 60889 gives 28.264e-6: the higher is used. Check: k = sqrt(Q_c(beta + 20)/rho_20 · ln((beta + theta_f)/(beta + theta_i))) gives 114.8, 102.7, 142.9, 76.1 and 94.5, the page's 115, 103, 143, 76 and 94."),

            new("ampacity-6242y-method-c", "04", "6242Y twin and earth, clipped direct (method C)", ManufacturerToConfirm,
                [eland, proPower],
                "Current carrying capacity, reference method C (clipped direct)", "datasheets 566633 V1 and 25/10/18 V1.0", "BS 7671 Appendix 4 Table 4D5",
                "Both datasheets give 16, 20, 27, 37, 47 and 64 A for 1 to 10 mm²; Pro Power adds 85 A at 16 mm². 30 °C ambient, no grouping; apply your correction factors."),

            new("ampacity-6242y-method-a", "04", "6242Y twin and earth, in conduit in an insulated wall (method A)", ManufacturerToConfirm,
                [eland, proPower],
                "Current carrying capacity, reference method A (in conduit in a thermally insulating wall)", "datasheets 566633 V1 and 25/10/18 V1.0", "BS 7671 Appendix 4 Table 4D5",
                "Both datasheets give 11.5, 14.5, 20, 26, 32 and 44 A for 1 to 10 mm²; Pro Power adds 57 A at 16 mm²."),

            new("voltage-drop-6242y", "05", "6242Y twin and earth voltage drop (mV/A/m)", ManufacturerToConfirm,
                [eland, proPower],
                "Voltage drop column", "datasheets 566633 V1 and 25/10/18 V1.0", "BS 7671 Appendix 4 Table 4D5",
                "The single figure each datasheet gives (44, 29, 18, 11, 7.3, 4.4 and 2.8 mV/A/m) goes in the z column, as the template asks for a table with one figure. The two sheets agree."),

            new("bend-radius-6242y", "79", "6242Y twin and earth minimum bend radius", ManufacturerToConfirm,
                [eland, proPower],
                "Minimum bending radius, fixed", "datasheets 566633 V1 and 25/10/18 V1.0", null,
                "Both datasheets give 6 x overall diameter for a fixed installation."),

            // ---------------------------------------------------------------- Mechanical and fluid power
            new("bearing-ratings-rhd", "24", "Deep groove ball bearing ratings (RHD Bearings)", ManufacturerToConfirm,
                [S("RHD Bearings, 6000, 6200 and 6300 series specification pages", "https://rhdbearings.com/specs/6200-series/6205/", ManufacturerToConfirm),
                 S("TempestOS Core bearing seed (same pages, PO decision 2026-10-01)", "https://github.com/kreczmans-creator/TempestOS-Core/blob/main/src/Tempest.Core/ReferenceData/Seeding/Datasets/BearingSeed.Series.cs", ManufacturerToConfirm)],
                "Load ratings Cr and Cor per designation", "rhdbearings.com specification pages", null,
                "The same 39 bearings the reference library already ships, as RHD publishes them. The 6205 page was read again on 2026-10-29: 14.0 kN and 7.88 kN, as stored. Ratings belong to this maker; another maker's 6205 differs."),

            new("bearing-factors-dgbb", "25", "Deep groove ball bearing X, Y and e factors", ManufacturerToConfirm,
                [S("NSK, Rolling bearings catalogue E1281 (dynamic equivalent load table)", "https://www.NSK.com/content/dam/nsk/common/catalogs/ctrgPdf/bearings/e1281f.pdf", ManufacturerToConfirm),
                 S("NACHI bearings catalogue pages 77 to 80 (AIMS Industrial)", "https://aimsindustrial.com.au/cdn/shop/files/NACHI-Bearings-Catalogue-Pages-77-78-79-80.pdf", ManufacturerToConfirm)],
                "Dynamic and static equivalent load factors", "manufacturer catalogues", "ISO 281 / ISO 76 factor tables",
                "e and Y for Fa/Fr > e with X = 0.56; static X0 = 0.6, Y0 = 0.5. The two catalogues agree. Fa_C0 is stored for reference and never applied: the catalogues tabulate f0·Fa/C0, whose values are about 12 times these."),

            new("oring-as568-gtweed", "32", "O-ring sizes AS568 (Greene Tweed)", ManufacturerToConfirm,
                [S("Greene Tweed, AS568 standard o-ring sizing", "https://www.gtweed.com/wp-content/uploads/AS568-standard-o-ring-sizing.pdf", ManufacturerToConfirm),
                 S("Global O-Ring and Seal, AS568 guide", "https://www.globaloring.com/wp-content/uploads/2018/10/Global_O-Ring_AS568_Guide.pdf", ManufacturerToConfirm)],
                "AS568 size table (ID and cross-section with tolerances)", "manufacturer size tables", "SAE AS568 size table",
                "Inch values converted (x 25.4 mm), sizes rounded to 0.01 mm and tolerances rounded up. The two tables agree on 343 of the 345 sizes both list. For -102 the larger ID tolerance (0.005 in) is used; -391 is left out because the tables give different IDs."),

            new("accumulator-rules-hydac", "54", "Bladder accumulator pre-charge rules (HYDAC)", ManufacturerToConfirm,
                [S("HYDAC, Bladder accumulators catalogue section 2.1.9 (scribd copy)", "https://www.scribd.com/document/571484604/Standard-Bladder-Accumulator", ManufacturerToConfirm),
                 S("HYDAC, Bladder accumulators (second scribd copy)", "https://www.scribd.com/doc/194865301/Bladder-Accumulators", ManufacturerToConfirm)],
                "Section 2.1.9 limits for gas pre-charge pressure", "HYDAC accumulator catalogue", null,
                "p0 ≤ 0.9 p1 and p2 : p0 ≤ 4 : 1, stated on gauge pressures. The sheet applies them to absolute pressures: 0.9 is then stricter and 4 slightly looser (by about 1 bar in p2)."),

            new("accumulator-sizes-hydac", "55", "Bladder accumulator nominal volumes (HYDAC SB330)", ManufacturerToConfirm,
                [S("HYDAC online shop, SB330 bladder accumulator listings", "https://hydac.com/shop/en-tr/standard-bladder-accumulators-1000187974?p=2", ManufacturerToConfirm),
                 S("HYDAC SB330 data sheet excerpt (scribd)", "https://www.scribd.com/document/686056954/Standard-Model-Datasheet", ManufacturerToConfirm)],
                "SB330 nominal volumes", "hydac.com product listings", null,
                "1, 2.5, 4, 10, 20, 24 and 32 L from the product listings; 0.5 and 6 L from the 0.5 to 6 L range. 13 L and 50 L were not confirmed and are left out."),

            new("cylinder-line-limits", "17", "Hydraulic line velocity limits", ManufacturerToConfirm,
                [S("Parker techConnect, Sizing tube to maximize system efficiency", "https://www.parker.com/content/dam/Parker-com/Literature/Tube-Fittings-Division/Parker-techConnect-Sizing-Tube-to-Maximize-System-Efficiency.pdf", ManufacturerToConfirm),
                 S("Kuriyama, Hydraulic connections catalog 296", "https://products.kuriyama.com/asset/Kuriyama-Hydraulic-Connections-catalog_296.pdf", ManufacturerToConfirm)],
                "Recommended flow velocities", "manufacturer guides", null,
                "Parker: suction 1.22, pressure 7.62, return 3.05 m/s. Kuriyama: suction up to 1.2, pressure up to 7.5, return up to 3 m/s. The lower limit of each is used: 1.2, 7.5 and 3.0 m/s."),

            new("hose-dash-sizes", "68", "Hydraulic hose bores by dash size", Derived,
                [S("Triad Technologies, Guide to sizing hydraulic hose (dash size = ID in sixteenths of an inch)", "https://www.triadtechnologies.com/blog/post/guide-to-sizing-hydraulic-hose", SecondaryToConfirm),
                 S("Zoro, Hydraulic hose size chart", "https://www.zoro.com/resourcehub/hydraulic-hose-size-chart/", SecondaryToConfirm)],
                "Dash number -4 to -32", "derived", null,
                "ID = dash/16 inch x 25.4 mm for -4, -5, -6, -8, -10, -12, -16, -24 and -32. One distributor lists a -20 fitting at 1 1/8 in (28.58 mm), not 1 1/4 in: the smaller bore is used."),

            new("cylinder-mounting-euler", "66", "Effective-length factor K (Euler theory)", Derived,
                [S("Wikipedia, Euler's critical load (CC BY-SA)", "https://en.wikipedia.org/wiki/Euler%27s_critical_load", SecondaryToConfirm), steelcalcK],
                "Theoretical K for the four ideal end conditions", "derived", null,
                "K = 0.5, 0.7, 1 and 2 follow from the buckled shape of an ideal column. Real fixings are less stiff: the recommended design values (set cylinder-mounting-aisc) are higher and safer."),

            new("cylinder-mounting-aisc", "66", "Effective-length factor K (recommended design values)", SecondaryToConfirm,
                [steelcalcK,
                 S("ideCAD, Effective length and effective length factor per AISC 360-16", "https://idecad.atlassian.net/wiki/spaces/IKP/pages/1128662114", SecondaryToConfirm)],
                "Recommended design K for six end conditions", "AISC 360 Commentary, as the pages give it", "AISC 360 Commentary Table C-A-7.1",
                "0.65, 0.80, 1.2, 1.0, 2.1 and 2.0; the two pages agree. AISC publishes its specification free of charge, so the licence may be simple to settle."),

            new("spring-seating-euler", "52", "Spring end-support coefficient (Euler theory)", Derived,
                [S("Wikipedia, Euler's critical load (CC BY-SA)", "https://en.wikipedia.org/wiki/Euler%27s_critical_load", SecondaryToConfirm), steelcalcK],
                "nu for the four ideal end conditions", "derived", null,
                "nu = 0.5, 0.7, 1 and 2, the theoretical effective-length factors, as the calculator's own guidance gives them."),

            // ---------------------------------------------------------------- Building services
            new("climate-design-temperature-uk", "41", "UK external design temperatures (99.6 %)", SecondaryToConfirm,
                [mcsConditions,
                 S("NOAA, Engineering weather data: London Heathrow, Edinburgh, Brize Norton (99.6 % heating dry bulb, 1973–1996)", "https://www.ncei.noaa.gov/pub/data/EngineeringWeatherData_CDROM/engwx/", OpenData),
                 S("MEP Desk, Design temperatures (London Heathrow −4.2 °C)", "https://mepdesk-website.pages.dev/design-temps", SecondaryToConfirm)],
                "99.6 % dry bulb by reference station", "CIBSE Guide A (2015) Table 2.5, as MCS gives it", "CIBSE Guide A (2015) Table 2.5",
                "MCS values, except where another source is colder: London −4.2 °C (MCS −3.0, NOAA −3.3, MEP Desk −4.2), Edinburgh −6.1 °C (MCS −5.4, NOAA −6.1) and Swindon −5.0 °C (MCS −4.6, NOAA Brize Norton −5.0). Lower the value by 0.6 K for each 100 m the site is above the station."),

            new("room-temperatures-dwellings", "42", "Dwelling internal design temperatures", SecondaryToConfirm,
                [mcsConditions,
                 S("MEP Desk, Internal design temperatures (CIBSE Guide A Table 1.5)", "https://mepdesk-website.pages.dev/design-temps", SecondaryToConfirm)],
                "Design room temperatures by age band", "CIBSE Domestic Heating Design Guide (2026) Table 2-2, as MCS gives it", "CIBSE Domestic Heating Design Guide Table 2-2 and CIBSE Guide A Table 1.5",
                "MCS values by age band. Where CIBSE Guide A (via MEP Desk) is higher, the higher is used: living rooms 22 °C and bathrooms and shower rooms 23 °C."),

            new("room-temperatures-non-domestic", "42", "Non-domestic internal design temperatures", SecondaryToConfirm,
                [ies],
                "Table 12 design temperatures", "older CIBSE Guide A table, as IES gives it", "CIBSE Guide A (older edition) winter design temperatures",
                "Values as the table gives them; spaces below 16 °C (stores, warehouses for storage) are left out because the room heat loss calculator takes 16 to 24 °C. One source only."),

            new("room-air-changes", "43", "Design air change rates", SecondaryToConfirm,
                [ies,
                 S("MCS Heat Load Calculator, Ventilation rates (minimum 0.5 per hour)", "https://heatloadcalculator.mcscertified.com/docs/reference-sources/ventilation-rates", SecondaryToConfirm)],
                "Table 12 air changes per hour", "older CIBSE Guide A table, as IES gives it", "CIBSE Guide A (older edition) infiltration rates",
                "The older CIBSE design rates. MCS now gives a minimum of 0.5 per hour for most rooms, with infiltration worked out apart; the older rates are higher and are used. Police cells (5 per hour) are left out: the calculator takes up to 3."),

            new("thermal-bridging-rdsap", "50", "Thermal bridging allowance y (RdSAP 10)", SecondaryToConfirm,
                [S("MCS Heat Load Calculator, Thermal bridging (RdSAP 10 June 2025 Table 21)", "https://heatloadcalculator.mcscertified.com/docs/reference-sources/thermal-bridging", SecondaryToConfirm),
                 S("UK Government, RdSAP 10 specification (Open Government Licence)", "https://www.gov.uk/guidance/standard-assessment-procedure", OpenData)],
                "Table 21", "RdSAP 10, June 2025", null,
                "y = 0.15, 0.11 and 0.08 W/(m²K) for age bands A to I, J and K onwards, added to the external elements. RdSAP is published by the UK Government under the Open Government Licence; the values were read on the MCS page, so the set stays marked to confirm until the RdSAP table itself is checked."),

            // ---------------------------------------------------------------- Second batch
            new("fluid-cp-water-nist", "16", "Water specific heat at 1 atm (NIST)", SecondaryToConfirm,
                [S("NIST Chemistry WebBook, Thermophysical properties of fluid systems: water, isobaric at 0.101325 MPa", "https://webbook.nist.gov/cgi/fluid.cgi?Action=Data&Wide=on&ID=C7732185&Type=IsoBar&Digits=5&P=0.101325&THigh=95&TLow=5&TInc=5&RefState=DEF&TUnit=C&PUnit=MPa&DUnit=kg%2Fm3", SecondaryToConfirm),
                 S("Engineering ToolBox, Water specific heat capacity", "https://www.engineeringtoolbox.com/specific-heat-capacity-water-d_660.html", SecondaryToConfirm)],
                "c_p of liquid water at each stream's mean temperature", "NIST Standard Reference Database 69", null,
                "c_p as NIST lists it (J/g·K x 1000). Engineering ToolBox agrees to four figures at 10, 20, 40, 60, 70 and 80 °C. NIST Standard Reference Data are free to read; their reuse terms are for the owner to confirm. The heat exchanger calculator needs c_p for a pair of streams, so the record holds common water pairs."),

            new("fluid-expansion-water-nist", "27", "Water expansion from fill to maximum temperature (NIST, derived)", SecondaryToConfirm,
                [S("NIST Chemistry WebBook, Thermophysical properties of fluid systems: water, isobaric at 0.101325 MPa", "https://webbook.nist.gov/cgi/fluid.cgi?Action=Data&Wide=on&ID=C7732185&Type=IsoBar&Digits=5&P=0.101325&THigh=95&TLow=5&TInc=5&RefState=DEF&TUnit=C&PUnit=MPa&DUnit=kg%2Fm3", SecondaryToConfirm)],
                "e = rho(fill)/rho(max) − 1 from NIST densities", "derived from NIST Standard Reference Database 69", null,
                "Derived: e = rho(t_fill)/rho(t_max) − 1, with rho at 5, 10, 70, 80 and 90 °C from NIST (999.97, 999.70, 977.76, 971.79, 965.31 kg/m³). The sealed system calculator already assumes water when e is blank; this record cites the figure. Glycol needs the supplier's own data."),

            new("surface-resistance-iso6946", "57", "Surface resistances for the condensation check", SecondaryToConfirm,
                [S("Data Driven AEC, U-value calculator for wall assemblies (ISO 6946 surface resistances)", "https://datadrivenaec.com/tools/uvalue-calculator-wall-assembly", SecondaryToConfirm),
                 S("Ravago Building Solutions, Heat transfer coefficient knowledge base", "https://ravagobuildingsolutions.com/hu/en/knowledgebase/heat-transfer-coefficient", ManufacturerToConfirm),
                 S("Universidade do Algarve, condensation calculation examples (R_si = 0.25 for f_Rsi)", "https://sapientia.ualg.pt/bitstreams/1efee868-7db6-4302-816b-c26ec78c76b9/download", SecondaryToConfirm)],
                "R_si by heat-flow direction, R_se, and R_si for the f_Rsi check", "ISO 6946 and ISO 13788 values, as the pages give them", "ISO 6946 Table 7 and ISO 13788 clause 6",
                "R_si = 0.13 (horizontal), 0.10 (upward) and 0.17 (downward) and R_se = 0.04 m²K/W; the pages agree. R_si,f = 0.25 m²K/W for an opaque element, as the calculation examples and the calculator's own guidance use."),

            new("condensation-criteria-mould", "59", "Critical surface humidity for mould (80 %)", SecondaryToConfirm,
                [S("Universidade do Algarve, condensation calculation examples (critical surface humidity 0.8)", "https://sapientia.ualg.pt/bitstreams/1efee868-7db6-4302-816b-c26ec78c76b9/download", SecondaryToConfirm),
                 S("ISO-Chemie, What is the temperature factor fRSI?", "https://www.iso-chemie.eu/en/isopedia/building-physics/temperature-factor-frsi", ManufacturerToConfirm)],
                "Critical surface relative humidity", "ISO 13788 mould criterion, as the pages give it", "ISO 13788 clause 6",
                "80 % relative humidity at the inside surface, the mould criterion both pages use. No condensate limit is given: the sheet reports the condensate without judging it."),

            new("surface-emissivity-etb", "78", "Surface emissivity of common finishes", SecondaryToConfirm,
                [S("Engineering ToolBox, Emissivity coefficients of common materials (at 300 K)", "https://www.engineeringtoolbox.com/emissivity-coefficients-d_447.html", SecondaryToConfirm)],
                "Emissivity at 300 K", "web page", null,
                "Where the page gives a range, the low end is used: a lower emissivity gives a lower outer coefficient, so a hotter surface on a hot pipe and a colder one on a cold pipe, the safe side of both surface checks. One source only: a second table (Omega) refused the request."),

            new("emitter-n-stelrad", "28", "Radiator exponent n (Stelrad, EN 442 data)", ManufacturerToConfirm,
                [S("Stelrad, Softline Plan data sheet (EN 442 certification data, CETIAT)", "https://www.stelradprofessional.com/wp-content/uploads/2015/10/28968_Softline-Plan-Web.pdf", ManufacturerToConfirm),
                 S("Stelrad, Softline Deco data sheet (EN 442 certification data, CETIAT)", "https://www.stelradprofessional.com/wp-content/uploads/2019/06/28968_Softline-Deco-Web.pdf", ManufacturerToConfirm)],
                "EN 442 certification data, n-coefficients", "Stelrad data sheets 28968", null,
                "The n-coefficient for each type and height, as each sheet prints it. Two models of one maker; another maker's radiators differ."),

            new("oring-criteria-parker", "31", "O-ring squeeze, fill and stretch limits (Parker)", ManufacturerToConfirm,
                [S("Parker Hannifin, O-Ring Handbook ORD 5700, sections 3.5 to 3.7 (University of Arizona copy)", "https://mfc.arizona.edu/sites/default/files/2022-02/parker_o-ring_handbook.pdf", ManufacturerToConfirm),
                 S("Global O-Ring and Seal, O-ring groove design calculator (squeeze ranges)", "https://www.globaloring.com/blog/o-ring-groove-design-calculator-engineering-the-right-gland-geometry-the-first-time/", ManufacturerToConfirm)],
                "Sections 3.5 Stretch, 3.6 Squeeze, 3.7 Gland fill", "ORD 5700", null,
                "Parker: static squeeze at most 30 %, dynamic about 16 %, gland fill 60 % to 85 %, assembled stretch at most 5 %, and at least 0.2 mm of squeeze in any seal. Global O-Ring: static 15 % to 25 %, dynamic 10 % to 20 %. The tighter bound of each is used: static 15 % to 25 %, dynamic 10 % to 16 %. The lower stretch and outside-diameter limits are not given; the sheet keeps its own."),

            new("pcb-ipc2221-constants", "60", "IPC-2221 trace curve-fit constants", SecondaryToConfirm,
                [S("DigiKey, PCB trace width conversion calculator", "https://www.digikey.com/en/resources/conversion-calculators/conversion-calculator-pcb-trace-width", ManufacturerToConfirm),
                 S("NextPCB, PCB trace width calculator: IPC-2221 guide", "https://www.nextpcb.com/blog/pcb-trace-width-calculator-ipc-2221-guide", ManufacturerToConfirm)],
                "I = k·dT^0.44·A^0.725", "IPC-2221, as the pages give it", "IPC-2221 trace current curve fit",
                "k = 0.048 (external) and 0.024 (internal), exponents 0.44 and 0.725, 1.378 mil of copper per oz/ft²; the pages agree and match the calculator's own defaults."),

            new("ltb-c1-sn003", "21", "C1 and C2 for lateral-torsional buckling (NCCI SN003)", SecondaryToConfirm,
                [S("Access Steel NCCI SN003a, Elastic critical moment for lateral torsional buckling", "https://dan.ct.upt.ro/fire/files/NCCI_SN003a-EN-EU.pdf", SecondaryToConfirm),
                 S("Bentley STAAD.Pro help, clause 6.3.2.2 elastic critical moment (C1, C2 table)", "https://docs.bentley.com/LiveContent/web/STAAD.Pro%20Help-v13/ja/GUID-894AAB66-3487-452A-93EE-2E41AB9765FA.html", SecondaryToConfirm)],
                "Tables 3.1 and 3.2, k = 1", "NCCI SN003a", null,
                "C1 = 1.127, C2 = 0.454 (uniform load) and C1 = 1.348, C2 = 0.630 (central point load); STAAD rounds them to 1.13/0.45 and 1.35/0.63, and the smaller C1 is used. End moments: C1 = 1.0 at psi = 1 and 1.77 at psi = 0. k_c is not given: enter it from your source."),
        ];
    }

    private static RecordSeedSet Climate(string id, string city, string lat, string lon, string where, RecordSeedSource nasa) =>
        new($"climate-monthly-{id}", "45", $"Monthly climate, {city} (NASA POWER)", OpenData,
            [nasa with { Url = $"https://power.larc.nasa.gov/api/temporal/climatology/point?parameters=T2M,RH2M&community=RE&longitude={lon}&latitude={lat}&format=JSON" }],
            $"Monthly mean T2M and RH2M at {where}", "climatology 2001–2025", null,
            "The grid cell's monthly mean air temperature and relative humidity at 2 m, as the API returns them. NASA POWER data are free to use with credit to NASA. A grid cell is not your site: use the site's own data for a design.");
}
