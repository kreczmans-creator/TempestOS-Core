using System.Globalization;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Structural;

/// <summary>
/// S5 — EN 1990 load combinations for buildings (Annex A1): ULS 6.10 or 6.10a/6.10b, SLS characteristic, frequent and
/// quasi-permanent, accidental 6.11b. Every leading variable action is tried; each permanent action takes γ_G,sup when
/// it acts in the direction sought and γ_G,inf when it opposes it, so the maximum and minimum envelopes cover the
/// favourable/unfavourable permutations of a scalar effect.
/// </summary>
public sealed class LoadCombinationsCalculator : ICalcCalculator
{
    public const string Id = "struct.load-combinations";
    public const string Method610 = "6.10";
    public const string Method610ab = "6.10a and 6.10b";
    public const string Permanent = "G permanent";
    public const string Accidental = "accidental";
    public const int MaxActions = 30;

    /// <summary>The units an effect may be given in: one per action table, so every row shares one dimension.</summary>
    public static IReadOnlyList<string> EffectUnits { get; } = ["kN", "kN/m", "kN/m^2", "kN*m", "kN*m/m"];

    /// <summary>EN 1990 Annex A1 recommended ψ factors for buildings (a small factor set, cited by Table A1.1); a UK NA record replaces them.</summary>
    public static IReadOnlyList<(string Category, double Psi0, double Psi1, double Psi2)> Recommended { get; } =
    [
        ("A domestic", 0.7, 0.5, 0.3),
        ("B office", 0.7, 0.5, 0.3),
        ("C congregation", 0.7, 0.7, 0.6),
        ("D shopping", 0.7, 0.7, 0.6),
        ("E storage", 1.0, 0.9, 0.8),
        ("F traffic ≤ 30 kN", 0.7, 0.7, 0.6),
        ("G traffic 30–160 kN", 0.7, 0.5, 0.3),
        ("H roofs", 0, 0, 0),
        ("snow ≤ 1000 m", 0.5, 0.2, 0),
        ("snow > 1000 m", 0.7, 0.5, 0.2),
        ("wind", 0.6, 0.2, 0),
        ("thermal", 0.6, 0.5, 0),
    ];

    public static IReadOnlyList<string> Categories { get; } = [Permanent, .. Recommended.Select(r => r.Category), Accidental];

    private static readonly IReadOnlyList<CalcColumnSpec> ActionColumns =
    [
        new("name", "Action", CalcInputKind.Text),
        new("category", "Category", CalcInputKind.Choice, Choices: Categories),
        new("F_k", "Characteristic value (signed, in the effect unit)", CalcInputKind.Number, HardMin: -1e7, HardMax: 1e7),
    ];

    private static readonly IReadOnlyList<CalcColumnSpec> PsiColumns =
    [
        new("category", "Category", CalcInputKind.Choice, Choices: [.. Recommended.Select(r => r.Category)]),
        new("psi_0", "ψ₀", CalcInputKind.Number, HardMin: 0, HardMax: 1),
        new("psi_1", "ψ₁", CalcInputKind.Number, HardMin: 0, HardMax: 1),
        new("psi_2", "ψ₂", CalcInputKind.Number, HardMin: 0, HardMax: 1),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.0.0", CalcDiscipline.Structural, "Load combinations (EN 1990)",
        "Design values of a single action effect (a force, a moment or a load intensity) under every EN 1990 Annex A1 combination for buildings: ULS persistent/transient, SLS characteristic, frequent and quasi-permanent, and accidental. Every variable action is tried as the leading one; the maximum and minimum envelopes and their governing combinations are reported.",
        "ULS 6.10: Σ γ_G·G + γ_Q·K_FI·Q_1 + Σ γ_Q·K_FI·ψ_0·Q_i (K_FI also on γ_G,sup)\n" +
        "ULS 6.10a: Σ γ_G·G + Σ γ_Q·K_FI·ψ_0·Q_i; 6.10b: Σ ξ·γ_G·G + γ_Q·K_FI·Q_1 + Σ γ_Q·K_FI·ψ_0·Q_i\n" +
        "Permanent factor: γ_G,sup when the action adds to the effect sought, γ_G,inf when it opposes it\n" +
        "Variable actions: included only when they add to the effect sought (otherwise zero)\n" +
        "SLS characteristic 6.14b: Σ G + Q_1 + Σ ψ_0·Q_i; frequent 6.15b: Σ G + ψ_1·Q_1 + Σ ψ_2·Q_i; quasi-permanent 6.16b: Σ G + Σ ψ_2·Q_i\n" +
        "Accidental 6.11b: Σ G + A_d + ψ_1·Q_1 + Σ ψ_2·Q_i",
        [
            new CalcInputSpec("actions", "Actions (one effect, signed)", CalcInputKind.Table, Columns: ActionColumns, MaxRows: MaxActions),
            new CalcInputSpec("unit", "Unit of the effect", CalcInputKind.Choice, Choices: EffectUnits, DefaultChoice: "kN"),
            new CalcInputSpec("method", "ULS expression", CalcInputKind.Choice, Choices: [Method610, Method610ab], DefaultChoice: Method610, Group: "Factors"),
            new CalcInputSpec("gamma_G_sup", "γ_G,sup (unfavourable permanent)", CalcInputKind.Number, Default: 1.35, Min: 1, Max: 2, HardMin: 0.5, HardMax: 3, Group: "Factors"),
            new CalcInputSpec("gamma_G_inf", "γ_G,inf (favourable permanent)", CalcInputKind.Number, Default: 1.0, Min: 0.8, Max: 1.2, HardMin: 0, HardMax: 2, Group: "Factors"),
            new CalcInputSpec("gamma_Q", "γ_Q (variable)", CalcInputKind.Number, Default: 1.5, Min: 1, Max: 2, HardMin: 0.5, HardMax: 3, Group: "Factors"),
            new CalcInputSpec("xi", "ξ reduction for 6.10b", CalcInputKind.Number, Default: 0.85, Min: 0.8, Max: 1, HardMin: 0.5, HardMax: 1, Group: "Factors"),
            new CalcInputSpec("K_FI", "K_FI consequence-class factor", CalcInputKind.Number, Default: 1.0, Min: 0.9, Max: 1.1, HardMin: 0.9, HardMax: 1.1, Group: "Factors"),
            new CalcInputSpec("psi", "ψ factors by category (replaces the recommended set)", CalcInputKind.Table, Columns: PsiColumns, MaxRows: 20, IsOptional: true, Group: "Factors"),
        ],
        [
            new CalcAssumption("One scalar action effect; every action's characteristic value is its contribution to that effect, with sign.", "Linear superposition (EN 1990 cl. 6.3.1 representative values)."),
            new CalcAssumption("Buildings only (Annex A1); no bridges, fatigue or seismic combinations.", "EN 1990 Annex A1 scope."),
            new CalcAssumption("Without a ψ record the EN 1990 Annex A1 recommended values are used, labelled on the sheet; the UK National Annex (NA.A1.2(A)–(C)) values come from a user record.", "§A5: no NA table ships."),
            new CalcAssumption("Each permanent action takes γ_G,sup or γ_G,inf by whether it adds to or opposes the effect sought; for EQU single-source checks, enter a source's permanent actions as one row.", "EN 1990 Table A1.2(B) note on favourable and unfavourable permanent actions."),
        ],
        [
            new CalcClauseRef("EN 1990:2002+A1:2005", "cl. 6.4.3.2", "Combinations for persistent or transient design situations (6.10, 6.10a, 6.10b)"),
            new CalcClauseRef("EN 1990:2002+A1:2005", "cl. 6.4.3.3", "Combinations for accidental design situations (6.11b)"),
            new CalcClauseRef("EN 1990:2002+A1:2005", "cl. 6.5.3", "Serviceability combinations (6.14b, 6.15b, 6.16b)"),
            new CalcClauseRef("EN 1990:2002+A1:2005", "Annex A1, Tables A1.1, A1.2(B)", "Application for buildings: ψ and γ factors"),
            new CalcClauseRef("UK NA to BS EN 1990:2002+A1:2005", "NA.A1.2(A)–(C)", "UK values of partial factors"),
        ],
        [
            new CalcApplicabilityRule("no-actions", "No actions: nothing to combine.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("psi-missing", "A variable action's category has no ψ factors in the ψ record.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("no-permanent", "No permanent action: check the combinations are what you intend.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-610-single", "Permanent plus office imposed, 6.10",
                new CalcReferenceSource("Analytic (EN 1990 6.10, recommended factors)", "EN 1990:2002+A1:2005", "G = 10 kN, Q (B office) = 5 kN: 1.35×10 + 1.5×5 = 21 kN; SLS char 15, frequent 10 + 0.5×5 = 12.5, quasi-permanent 10 + 0.3×5 = 11.5 kN"),
                [Actions(("Dead load", Permanent, 10), ("Office imposed", "B office", 5))],
                [new CalcExpectedValue("E_d_max", 21, "kN", 1e-9), new CalcExpectedValue("E_d_min", 10, "kN", 1e-9), new CalcExpectedValue("E_k_max", 15, "kN", 1e-9), new CalcExpectedValue("E_fr_max", 12.5, "kN", 1e-9), new CalcExpectedValue("E_qp_max", 11.5, "kN", 1e-9)]),
            new CalcTestCase("ka-610ab-single", "Permanent plus office imposed, 6.10a and 6.10b",
                new CalcReferenceSource("Analytic (EN 1990 6.10a/6.10b, recommended factors)", "EN 1990:2002+A1:2005", "6.10a: 1.35×10 + 1.5×0.7×5 = 18.75; 6.10b: 0.85×1.35×10 + 1.5×5 = 18.975 kN governs"),
                [Actions(("Dead load", Permanent, 10), ("Office imposed", "B office", 5)), CalcInputValue.Choice("method", Method610ab)],
                [new CalcExpectedValue("E_d_max", 18.975, "kN", 1e-9)]),
            new CalcTestCase("ka-610-two-variables", "Imposed and wind, each leading in turn",
                new CalcReferenceSource("Analytic (EN 1990 6.10, recommended factors)", "EN 1990:2002+A1:2005", "G 10, Q_B 5, W 4 kN: imposed leading 13.5 + 7.5 + 1.5×0.6×4 = 24.6; wind leading 13.5 + 6 + 1.5×0.7×5 = 24.75 kN governs"),
                [Actions(("Dead load", Permanent, 10), ("Office imposed", "B office", 5), ("Wind", "wind", 4))],
                [new CalcExpectedValue("E_d_max", 24.75, "kN", 1e-9), new CalcExpectedValue("E_k_max", 17.5, "kN", 1e-9)]),
            new CalcTestCase("ka-uplift", "Wind uplift against favourable permanent load",
                new CalcReferenceSource("Analytic (EN 1990 6.10, recommended factors)", "EN 1990:2002+A1:2005", "G = 10 kN down, W = −20 kN uplift: minimum 1.0×10 + 1.5×(−20) = −20 kN; maximum 1.35×10 = 13.5 kN"),
                [Actions(("Roof dead load", Permanent, 10), ("Wind uplift", "wind", -20))],
                [new CalcExpectedValue("E_d_min", -20, "kN", 1e-9), new CalcExpectedValue("E_d_max", 13.5, "kN", 1e-9)]),
            new CalcTestCase("ka-designers-guide", "Floor beam: permanent, imposed and snow (Designers' Guide pattern)",
                new CalcReferenceSource("Gulvanessian, Calgaro & Holický, Designers' Guide to EN 1990", "2002", "Ch. 6 combination examples — the pattern only; the numbers here are analytic and await the owner's check against the book: G 20, Q_A 8, S 3 kN/m; 6.10a 1.35×20 + 1.5×0.7×8 + 1.5×0.5×3 = 37.65 kN/m governs (6.10b imposed leading 37.2)"),
                [Actions(("Permanent", Permanent, 20), ("Domestic imposed", "A domestic", 8), ("Snow", "snow ≤ 1000 m", 3)), CalcInputValue.Choice("unit", "kN/m"), CalcInputValue.Choice("method", Method610ab)],
                [new CalcExpectedValue("E_d_max", 37.65, "kN/m", 1e-9)],
                CalcReferenceStatus.Unconfirmed),
        ],
        Category: "Design",
        OutputPorts:
        [
            new CalcOutputPort("E_d_max", "", "ULS design effect, maximum (unit of the actions)"),
            new CalcOutputPort("E_d_min", "", "ULS design effect, minimum"),
            new CalcOutputPort("E_k_max", "", "SLS characteristic effect, maximum"),
            new CalcOutputPort("E_qp_max", "", "SLS quasi-permanent effect, maximum"),
        ],
        RecordKinds:
        [
            new CalcRecordKind("en1990.factors", "EN 1990 partial and ψ factors", ["gamma_G_sup", "gamma_G_inf", "gamma_Q", "xi", "psi"], "From the National Annex in use (UK: NA.A1.2(A)–(C)) or the EN 1990 Annex A1 recommended set, with its edition."),
        ]);

    /// <summary>An actions table from (name, category, value[, unit]) rows.</summary>
    public static CalcInputValue Actions(params (string Name, string Category, double Value)[] rows) =>
        CalcInputValue.Table("actions", rows.Select(r => CalcRow.Of(("name", new CalcCell(Text: r.Name)), ("category", new CalcCell(Text: r.Category)), ("F_k", new CalcCell(r.Value)))).ToList());

    private sealed record Action(int Index, string Name, string Category, double Fk, (double Psi0, double Psi1, double Psi2) Psi)
    {
        public bool IsPermanent => Category == Permanent;
        public bool IsAccidental => Category == Accidental;
        public bool IsVariable => !IsPermanent && !IsAccidental;
    }

    private sealed record Term(Action Action, double Factor, string FactorText);

    private sealed record Combination(string Family, string Name, IReadOnlyList<Term> Terms)
    {
        public double Value => Terms.Sum(t => t.Factor * t.Action.Fk);
    }

    public void Run(CalcRunContext c)
    {
        var rows = c.Rows("actions");
        var method = c.InputText("method");

        foreach (var s in new[] { "gamma_G_sup", "gamma_G_inf", "gamma_Q", "xi", "K_FI" })
            c.Input(s);

        if (rows.Count == 0)
        {
            c.Block("no-actions", "actions: enter at least one action.");
            return;
        }

        var unit = UnitCatalog.Parse(c.InputText("unit"));
        var psi = PsiTable(c);
        var actions = new List<Action>();

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var category = row.Get("category")?.Text ?? Permanent;
            var factors = (0.0, 0.0, 0.0);

            if (category != Permanent && category != Accidental && !psi.TryGetValue(category, out factors))
            {
                c.Block("psi-missing", $"psi: no ψ factors for category '{category}' (row {i + 1}).");
                return;
            }

            var name = row.Get("name")?.Text is { Length: > 0 } n ? CalcSheetTextRenderer.Clean(n) : $"Action {i + 1}";
            actions.Add(new Action(i + 1, name, category, unit.ToSi(row.Get("F_k")?.Number ?? 0), factors));
        }

        if (!actions.Any(a => a.IsPermanent))
            c.Warn("no-permanent", "actions: no permanent action is entered; check the combinations are what you intend.");

        var gSup = c.InputSi("gamma_G_sup");
        var gInf = c.InputSi("gamma_G_inf");
        var gQ = c.InputSi("gamma_Q");
        var xi = c.InputSi("xi");
        var kfi = c.InputSi("K_FI");
        var results = new Dictionary<string, (Combination Max, Combination Min)>(StringComparer.Ordinal);

        foreach (var sign in new[] { 1, -1 })
        {
            var direction = sign > 0 ? "max" : "min";
            var combos = Generate(actions, sign, method, gSup, gInf, gQ, xi, kfi);

            var k = 1;

            foreach (var combo in combos)
            {
                var symbol = $"{(sign > 0 ? "C" : "D")}{k++}";
                c.Record(symbol, Formula(combo), Substitution(combo, unit), ScalarValue.Of(unit.FromSi(combo.Value), unit), $"{combo.Name} ({direction})", Clause(combo.Family));
            }

            foreach (var family in combos.Select(x => x.Family).Distinct(StringComparer.Ordinal))
            {
                var inFamily = combos.Where(x => x.Family == family).ToList();
                var pick = sign > 0 ? inFamily.MaxBy(x => x.Value)! : inFamily.MinBy(x => x.Value)!;
                results[family] = sign > 0 ? (pick, results.GetValueOrDefault(family).Min) : (results.GetValueOrDefault(family).Max, pick);
            }
        }

        Envelope(c, results, "ULS", "E_d", "ULS design effect", unit);
        Envelope(c, results, "SLS characteristic", "E_k", "SLS characteristic effect", unit);
        Envelope(c, results, "SLS frequent", "E_fr", "SLS frequent effect", unit);
        Envelope(c, results, "SLS quasi-permanent", "E_qp", "SLS quasi-permanent effect", unit);

        if (results.ContainsKey("Accidental"))
            Envelope(c, results, "Accidental", "E_acc", "Accidental effect", unit);
    }

    /// <summary>Every combination for one direction of the effect (sign +1 maximum, −1 minimum).</summary>
    private static List<Combination> Generate(List<Action> actions, int sign, string method, double gSup, double gInf, double gQ, double xi, double kfi)
    {
        var variables = actions.Where(a => a.IsVariable && Math.Sign(a.Fk) == sign).ToList();
        var accidentals = actions.Where(a => a.IsAccidental && Math.Sign(a.Fk) == sign).Cast<Action?>().ToList();

        if (accidentals.Count == 0 && actions.Any(a => a.IsAccidental))
            accidentals.Add(null);

        var leaders = variables.Count == 0 ? [null] : variables.Cast<Action?>().ToList();
        var combos = new List<Combination>();

        foreach (var lead in leaders)
        {
            var leadText = lead is null ? "no variable action" : $"{lead.Name} leading";
            var others = variables.Where(v => v != lead).ToList();

            if (method == Method610)
                combos.Add(new("ULS", $"ULS 6.10, {leadText}", [.. Permanents(actions, sign, gSup * kfi, "γ_G,sup·K_FI", gInf), .. Lead(lead, gQ * kfi, "γ_Q·K_FI"), .. others.Select(o => new Term(o, gQ * kfi * o.Psi.Psi0, "γ_Q·K_FI·ψ₀"))]));
            else
                combos.Add(new("ULS", $"ULS 6.10b, {leadText}", [.. Permanents(actions, sign, xi * gSup * kfi, "ξ·γ_G,sup·K_FI", gInf), .. Lead(lead, gQ * kfi, "γ_Q·K_FI"), .. others.Select(o => new Term(o, gQ * kfi * o.Psi.Psi0, "γ_Q·K_FI·ψ₀"))]));

            combos.Add(new("SLS characteristic", $"SLS characteristic 6.14b, {leadText}", [.. Permanents(actions, sign, 1, "1", 1), .. Lead(lead, 1, "1"), .. others.Select(o => new Term(o, o.Psi.Psi0, "ψ₀"))]));
            combos.Add(new("SLS frequent", $"SLS frequent 6.15b, {leadText}", [.. Permanents(actions, sign, 1, "1", 1), .. Lead(lead, lead?.Psi.Psi1 ?? 0, "ψ₁"), .. others.Select(o => new Term(o, o.Psi.Psi2, "ψ₂"))]));

            foreach (var accident in accidentals)
                combos.Add(new("Accidental", $"Accidental 6.11b, {accident?.Name ?? "no accidental action this way"}, {leadText}", [.. Permanents(actions, sign, 1, "1", 1), .. Lead(accident, 1, "1"), .. Lead(lead, lead?.Psi.Psi1 ?? 0, "ψ₁"), .. others.Select(o => new Term(o, o.Psi.Psi2, "ψ₂"))]));
        }

        if (method == Method610ab)
            combos.Insert(0, new("ULS", "ULS 6.10a", [.. Permanents(actions, sign, gSup * kfi, "γ_G,sup·K_FI", gInf), .. variables.Select(o => new Term(o, gQ * kfi * o.Psi.Psi0, "γ_Q·K_FI·ψ₀"))]));

        combos.Add(new("SLS quasi-permanent", "SLS quasi-permanent 6.16b", [.. Permanents(actions, sign, 1, "1", 1), .. variables.Select(o => new Term(o, o.Psi.Psi2, "ψ₂"))]));

        return combos;
    }

    /// <summary>
    /// S5 → S2 (Addendum A §A6 S5, Opus review finding 8): the ULS and SLS combinations as rows of the S2 frame's
    /// combinations table, action row n entering as S2 load case n, named as on the S5 sheet (C1…, D1…).
    /// </summary>
    /// <remarks>
    /// Notes say what the caller must know: categories without ψ factors (taken as 0), combinations left out to stay
    /// within S2's limit, and that γ_G,sup/γ_G,inf were chosen for the sign of the effect as entered.
    /// </remarks>
    public static (CalcInputValue Combinations, IReadOnlyList<string> Notes) FrameCombinations(CalcInputSet inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var template = new LoadCombinationsCalculator().Template;
        double Factor(string symbol) => inputs.Find(symbol)?.Number ?? template.FindInput(symbol)!.Default!.Value;
        var psi = ParsePsi(inputs.Find("psi")?.Rows ?? []);
        var method = inputs.Find("method")?.Text ?? Method610;
        var notes = new List<string> { "γ_G,sup and γ_G,inf were chosen for the sign of the effect as entered; for an effect of the other sign run S5 with that effect's values." };
        var actions = (inputs.Find("actions")?.Rows ?? []).Select((row, i) =>
        {
            var category = row.Get("category")?.Text ?? Permanent;

            if (category is not (Permanent or Accidental) && !psi.ContainsKey(category))
                notes.Add($"Action row {i + 1}: no ψ factors for '{category}'; ψ taken as 0 — enter them, as the S5 sheet requires.");

            var factors = category is Permanent or Accidental ? (0.0, 0.0, 0.0) : psi.GetValueOrDefault(category);
            return new Action(i + 1, row.Get("name")?.Text ?? $"Action {i + 1}", category, row.Get("F_k")?.Number ?? 0, factors);
        }).ToList();
        var named = new List<(string Name, Combination Combo)>();

        foreach (var sign in new[] { 1, -1 })
        {
            var k = 1;

            foreach (var combo in Generate(actions, sign, method, Factor("gamma_G_sup"), Factor("gamma_G_inf"), Factor("gamma_Q"), Factor("xi"), Factor("K_FI")))
                named.Add(($"{(sign > 0 ? "C" : "D")}{k++}", combo));
        }

        // ULS first, then SLS characteristic, within S2's limit on combinations.
        var candidates = named.Where(n => n.Combo.Family == "ULS").Concat(named.Where(n => n.Combo.Family == "SLS characteristic"))
            .Where(n => n.Combo.Terms.Any(t => t.Factor != 0)).ToList();
        var chosen = candidates.Take(Frame.FrameLimits.MaxCombinations).ToList();

        if (candidates.Count > chosen.Count)
            notes.Add($"{candidates.Count - chosen.Count} combination(s) left out to stay within S2's limit of {Frame.FrameLimits.MaxCombinations}: {string.Join(", ", candidates.Skip(chosen.Count).Select(n => n.Name))}.");

        return (FrameCalculator.Combinations([.. chosen.SelectMany(n => n.Combo.Terms.Where(t => t.Factor != 0).Select(t => (n.Name, t.Action.Index, t.Factor)))]), notes);
    }

    private static IEnumerable<Term> Lead(Action? lead, double factor, string text) => lead is null ? [] : [new Term(lead, factor, text)];

    private static IEnumerable<Term> Permanents(IEnumerable<Action> actions, int sign, double unfavourable, string unfavourableText, double favourable) =>
        actions.Where(a => a.IsPermanent).Select(a => Math.Sign(a.Fk) == sign || a.Fk == 0
            ? new Term(a, unfavourable, unfavourableText)
            : new Term(a, favourable, favourable == 1 && unfavourable == 1 ? "1" : "γ_G,inf"));

    private static string Formula(Combination combo) =>
        combo.Terms.Count == 0 ? "0" : string.Join(" + ", combo.Terms.Select(t => $"{t.FactorText}·{Short(t.Action)}"));

    private static string Substitution(Combination combo, Unit unit) =>
        combo.Terms.Count == 0 ? "0" : string.Join(" + ", combo.Terms.Select(t => $"{F(t.Factor)}×{F(unit.FromSi(t.Action.Fk))}"));

    private static string Short(Action a) => a.IsPermanent ? $"G{a.Index}" : a.IsAccidental ? $"A{a.Index}" : $"Q{a.Index}";

    private static string Clause(string family) => family switch
    {
        "ULS" => "EN 1990 cl. 6.4.3.2",
        "Accidental" => "EN 1990 cl. 6.4.3.3",
        _ => "EN 1990 cl. 6.5.3",
    };

    private static void Envelope(CalcRunContext c, Dictionary<string, (Combination Max, Combination Min)> results, string family, string symbol, string text, Unit unit)
    {
        if (!results.TryGetValue(family, out var r) || r.Max is null || r.Min is null)
            return;

        c.Record(symbol + "_max", "maximum over the combinations", r.Max.Name, ScalarValue.Of(unit.FromSi(r.Max.Value), unit), text + ", maximum");
        c.Record(symbol + "_min", "minimum over the combinations", r.Min.Name, ScalarValue.Of(unit.FromSi(r.Min.Value), unit), text + ", minimum");
        c.Output(symbol + "_max", text + ", maximum");
        c.Output(symbol + "_min", text + ", minimum");
        c.Output(symbol + "_gov", text + ", governing (max)", new TextValue(r.Max.Name));
    }

    private static Dictionary<string, (double Psi0, double Psi1, double Psi2)> PsiTable(CalcRunContext c)
    {
        var rows = c.Rows("psi");
        c.Assume(rows.Count == 0 ? "ψ factors: EN 1990 Annex A1 recommended values (Table A1.1)." : "ψ factors: from the ψ table entered (its record and edition are cited against the input).", rows.Count == 0 ? "No ψ record entered." : "User record.");
        return ParsePsi(rows);
    }

    /// <summary>The ψ table entered, or the recommended set when none is.</summary>
    private static Dictionary<string, (double Psi0, double Psi1, double Psi2)> ParsePsi(IReadOnlyList<CalcRow> rows)
    {
        var table = Recommended.ToDictionary(r => r.Category, r => (r.Psi0, r.Psi1, r.Psi2), StringComparer.Ordinal);

        if (rows.Count == 0)
            return table;

        table.Clear();

        foreach (var row in rows)
        {
            if (row.Get("category")?.Text is { Length: > 0 } category)
                table[category] = (row.Get("psi_0")?.Number ?? 0, row.Get("psi_1")?.Number ?? 0, row.Get("psi_2")?.Number ?? 0);
        }

        return table;
    }

    private static string F(double v) => CalcValue.FormatNumber(v, 5).ToString(CultureInfo.InvariantCulture);
}
