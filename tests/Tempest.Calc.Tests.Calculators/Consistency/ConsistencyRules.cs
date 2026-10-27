using System.Text.RegularExpressions;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Consistency;

/// <summary>One enforceable rule of <c>docs/calcs/sheet-standard.md</c>: its id, the audit row it comes from and the check that finds what breaks it in one template.</summary>
internal sealed record ConsistencyRule(string Id, string Category, string Rows, string Summary, Func<TemplateFacts, IEnumerable<string>> Offences);

/// <summary>
/// C-2: the standard, as checks. Each rule returns what is wrong in one template (empty when it conforms). The rules that every template
/// already meets hold without exception; the others are held by <c>ConsistencyAllowList.json</c>, which names the templates that do not meet them
/// yet and which phases B and C shrink to empty (<see cref="ConsistencyContractTests"/>).
/// </summary>
internal static partial class ConsistencyRules
{
    // ---- text helpers -------------------------------------------------

    [GeneratedRegex(@"(?i)modulus of elasticity|elastic modulus|youngs? modulus|young.s modulus")]
    private static partial Regex ModulusWords();

    [GeneratedRegex(@"(?i)\bultimate (?!tensile\b)(strength|stress)\b|\byield point\b|\ballowable strength\b")]
    private static partial Regex StrengthWords();

    [GeneratedRegex(@"(?i)\bmargins?\b")]
    private static partial Regex MarginWord();

    [GeneratedRegex(@"\(([^()\s]{1,10})\)\s*$")]
    private static partial Regex TrailingParenthesis();

    [GeneratedRegex(@"(?i)\bdegC\b|\bohm\b|[kMm]?N\*m\b|\bm\^[2-6]|\bmm\^[2-6]|\*K\)|\^-?\d")]
    private static partial Regex AsciiUnit();

    [GeneratedRegex(@"(?i)per cent|percent")]
    private static partial Regex PercentWord();

    [GeneratedRegex(@"\b\d{1,3},\d{3}\b")]
    private static partial Regex CommaThousands();

    [GeneratedRegex(@"(?i)\b(needs?|is needed|are needed|give|requires?|must be given)\b")]
    private static partial Regex MissingPhrasing();

    [GeneratedRegex(@"(?i)\b(held at|is treated as|treated as|is capped|capped at|falls back|is used instead|is taken as)\b")]
    private static partial Regex FallbackLanguage();

    [GeneratedRegex(@"(?i)\b(gives? no result|no result is|cannot be solved|cannot be computed|is refused)\b")]
    private static partial Regex NoResultLanguage();

    [GeneratedRegex(@"\b[MESBFW]\d{1,2}[abc]?\b|§A\d")]
    private static partial Regex InternalCode();

    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceBreak();

    private static IEnumerable<string> Texts(TemplateFacts f)
    {
        foreach (var i in f.Template.Inputs)
        {
            yield return i.Description;

            foreach (var c in i.Columns ?? [])
                yield return c.Label;
        }

        foreach (var o in f.DefaultRun.Outputs)
            yield return o.Description;

        foreach (var p in f.Template.OutputPorts ?? [])
            yield return p.Description;
    }

    /// <summary>Every message the template can show: the literals in its source and what its runs raised.</summary>
    private static IEnumerable<string> Messages(TemplateFacts f) =>
        f.Sites.Where(s => s.Message is not null && Regex.Matches(s.Message, "[A-Za-z]{3,}").Count > 1).Select(s => s.Message!)
            .Concat(f.Notices.Where(n => !n.RuleId.StartsWith("input.", StringComparison.Ordinal) && !n.RuleId.StartsWith("record", StringComparison.Ordinal) && n.RuleId != "engine").Select(n => n.Message))
            .Distinct(StringComparer.Ordinal);

    [GeneratedRegex(@"^[A-Za-z_0-9]+ = -?[\d.]+$")]
    private static partial Regex CheckRatioOnly();

    private static IEnumerable<CalcCheck> Checks(TemplateFacts f) => f.CaseRuns.Prepend(f.DefaultRun).SelectMany(r => r.Checks);

    private static IEnumerable<string> Sentences(string message) => SentenceBreak().Split(message.Trim()).Where(s => s.Length > 0);

    private static int Words(string sentence) => sentence.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;

    public static IReadOnlyDictionary<string, string> GroupSynonyms { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Load"] = "Loads", ["Actions"] = "Loads", ["Forces"] = "Loads", ["Heat load"] = "Loads", ["Limits"] = "Requirements", ["Spacing limits"] = "Requirements",
        ["Conductor material"] = "Material", ["Conductor"] = "Geometry", ["Plate"] = "Geometry", ["Pressure"] = "Conditions", ["Pressures"] = "Conditions",
    };

    private static readonly string[] GroupOrderList = ["Setup", "Geometry", "Material", "Loads", "Conditions", "Factors", "Requirements"];

    // ---- the rules ----------------------------------------------------

    public static IReadOnlyList<ConsistencyRule> All { get; } =
    [
        // A. Layout (the renderers apply rows 1, 2 and 4 to every template; row 3 names checks, which a template writes)
        new("A03-check-names", "A", "3", "A check is named '<quantity> against <limit>'.", f =>
            Checks(f).Select(c => c.Name).Distinct(StringComparer.Ordinal).Where(n => !n.Contains(" against ", StringComparison.Ordinal)).Select(n => $"'{n}'")),
        new("A03-check-shows-physical-values", "A", "3", "A check shows its demand and capacity as physical values with units, not only as a utilisation.", f =>
            Checks(f).Where(c => CheckRatioOnly().IsMatch(c.DemandText) || c.CapacityText.Contains(" ≤ ", StringComparison.Ordinal) && Regex.IsMatch(c.CapacityText, @"^[A-Za-z_0-9]+ ≤ ")).Select(c => $"'{c.Name}': {c.DemandText} vs {c.CapacityText}").Distinct()),

        // B. Terminology
        new("B05-youngs-modulus", "B", "5", "The elastic modulus is called Young's modulus.", f =>
            Texts(f).Where(t => ModulusWords().IsMatch(t) && !t.Contains("Young's modulus", StringComparison.Ordinal)).Select(t => $"'{t}'")),
        new("B06-strength-words", "B", "6", "Strength is yield strength and ultimate tensile strength; a code value is a design strength.", f =>
            Texts(f).Where(t => StrengthWords().IsMatch(t)).Select(t => $"'{Cut(t)}'")),
        new("B07-requirement-words", "B", "7", "No 'margin'; 'Required X' is _req and 'Target X' is _target.", f =>
            f.Template.Inputs.SelectMany(i => new[]
            {
                MarginWord().IsMatch(i.Description) ? $"{i.Symbol}: '{i.Description}' says margin" : null,
                i.Description.StartsWith("Required ", StringComparison.Ordinal) && !i.Symbol.Contains("req", StringComparison.OrdinalIgnoreCase) ? $"{i.Symbol}: '{i.Description}' is not _req" : null,
                i.Description.StartsWith("Target ", StringComparison.Ordinal) && !i.Symbol.Contains("target", StringComparison.OrdinalIgnoreCase) ? $"{i.Symbol}: '{i.Description}' is not _target" : null,
            }).Where(x => x is not null).Select(x => x!)),
        new("B08-description-pattern", "B", "8", "A description is plain words: it does not repeat its symbol or carry a unit.", f =>
            f.Template.Inputs.Select(i => (i.Symbol, i.Description)).Concat(f.DefaultRun.Outputs.Select(o => (o.Symbol, o.Description))).Where(x =>
            {
                var (symbol, description) = x;

                // A symbol that is an ordinary English word (mode, layers, fluid) may be described by it.
                var isWord = Regex.IsMatch(symbol, "^[a-z]{3,}$");

                if (!isWord && string.Equals(symbol, description, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (!isWord && symbol.Length >= 1 && Regex.IsMatch(description, $@"(^|[\s(,]){Regex.Escape(symbol)}($|[\s),.])"))
                    return true;

                return TrailingParenthesis().Match(description) is { Success: true } m && (m.Groups[1].Value == "Mrev" || UnitCatalog.TryParse(m.Groups[1].Value, out _, out _) && m.Groups[1].Value.Any(char.IsLetter) && m.Groups[1].Value.Length <= 6 && !Regex.IsMatch(m.Groups[1].Value, "^[a-z]{5,}$"));
            }).Select(x => $"{x.Symbol}: '{x.Description}'")),
        new("B09-optional-hint", "B", "9", "An optional input says '(optional; blank = what happens)'.", f =>
            f.Template.Inputs.Where(i => i.Description.Contains("blank", StringComparison.OrdinalIgnoreCase) && !i.Description.Contains("(optional; blank = ", StringComparison.Ordinal)).Select(i => $"{i.Symbol}: '{Cut(i.Description)}'")),

        // C. Symbols
        new("C10-temperature-symbols", "C", "10", "A temperature is T or T_<qualifier>; a difference is dT.", f => Symbols(f, "10")),
        new("C11-theta-is-an-angle", "C", "11", "theta is an angle, never a temperature.", f => Symbols(f, "11")),
        new("C12-torque-and-time", "C", "12", "Bare T is a torque and bare t is a time.", f => Symbols(f, "12")),
        new("C13-no-case-only-pairs", "C", "13", "Two symbols of one template never differ only by letter case.", f =>
            f.Template.Inputs.Select(i => i.Symbol).Concat(f.DefaultRun.Outputs.Select(o => o.Symbol)).Distinct(StringComparer.Ordinal)
                .Where(s => s.Length > 1).GroupBy(s => s.ToLowerInvariant()).Where(g => g.Count() > 1).Select(g => string.Join(" / ", g))),
        new("C14-quantity-letters", "C", "14", "U is voltage, V is volume, Q is flow, P is power, F is force, Phi is heat flow.", f => Symbols(f, "14")),
        new("C15-no-bare-p", "C", "15", "A pressure is p_<qualifier>, never a bare p.", f => Symbols(f, "15")),
        new("C16-greek-letters", "C", "16", "mu, rho, nu and eta each have one meaning.", f => Symbols(f, "16")),
        new("C17-single-letters", "C", "17", "eps, h, n, R, D and L each have one meaning.", f => Symbols(f, "17")),
        new("C18-sibling-symbols", "C", "18", "One symbol for one quantity across sibling sheets.", f => Symbols(f, "18")),
        new("C19-iso-axes", "C", "19", "Section properties use the EN 1993 axes, y-y major and z-z minor.", f =>
            f.Template.Id == "struct.section-properties"
                ? f.QuantityFacts.Where(q => q.Origin is "output" or "input" && Regex.IsMatch(q.Symbol, @"^(I|W_el|W_pl|i|alpha_pl)_(x|xx|xy|yy)$")).Select(q => $"{q.Symbol} uses the x-x axes of the sheet or a doubled subscript instead of the EN 1993 y-y and z-z").Distinct()
                : []),

        // D. Units
        new("D20-units-per-quantity-type", "D", "20 to 26", "A quantity type is entered and shown in its one unit.", f =>
            f.QuantityFacts.Select(q => (q, p: CalcUnitStandard.Problem(f.Id, q.Dimension, q.Unit))).Where(x => x.p is not null).Select(x => $"{x.q.Symbol} ({x.q.Origin}): {x.p}").Distinct()),
        new("D27-unit-spelling-in-text", "D", "27", "Units in a description, notice or message are written as symbols (°C, Ω, kN·m, m²).", f =>
            Texts(f).Concat(Messages(f)).Where(t => AsciiUnit().IsMatch(t)).Select(t => $"'{Cut(t)}'")),
        new("D28-percent", "D", "28", "A percentage is a quantity in %, written '%' and never 'percent'.", f =>
            f.Template.Inputs.Where(i => i.Kind == CalcInputKind.Number && (i.Description.Contains('%', StringComparison.Ordinal) || PercentWord().IsMatch(i.Description))).Select(i => $"{i.Symbol}: '{Cut(i.Description)}' is a plain number")
                .Concat(Texts(f).Concat(Messages(f)).Where(t => PercentWord().IsMatch(t)).Select(t => $"'{Cut(t)}' spells percent"))),

        // E. Decimals
        new("E29-output-unit-has-a-precision", "E", "29", "Every output unit has a display rule in the precision table.", f =>
            f.DefaultRun.Outputs.Where(o => o.Value is ScalarValue { Dimension.IsDimensionless: false } s && CalcDisplay.DecimalsFor(s.Dimension, s.DisplayUnit.Symbol) is null).Select(o => $"{o.Symbol} in {CalcUnitText.Pretty(((ScalarValue)o.Value).DisplayUnit.Symbol)}")),
        new("E30-utilisation-two-decimals", "E", "30", "Every utilisation output is shown to two decimals.", f =>
            f.DefaultRun.Outputs.Where(o => CalcSheetStandard.IsUtilisation(o) && !Regex.IsMatch(o.ValueText, @"^-?\d+\.\d\d$|^∞$|^−∞$")).Select(o => $"{o.Symbol} = {o.ValueText}")),

        // F. Messages and severity
        new("F31-message-shape", "F", "31", "A message is '<symbol>: <one sentence>.'", f =>
            Messages(f).Select(m => (m, p: CalcMessageShape.Problems(m))).Where(x => x.p.Count > 0).Select(x => $"'{Cut(x.m)}' {x.p[0]}")),
        new("F32-missing-input-phrasing", "F", "32", "A missing input reads '<symbol>: enter <thing>.'", f =>
            f.Sites.Where(s => s.Message is { } m && !s.HasHoles && MissingPhrasing().IsMatch(m) && !m.Contains(": enter ", StringComparison.Ordinal) && !m.Contains(": choose ", StringComparison.Ordinal)).Select(s => $"'{Cut(s.Message!)}'")),
        new("F33-one-idea-per-message", "F", "33", "A message has no semicolon, at most two sentences, each of at most 20 words.", f =>
            Messages(f).Where(m => m.Contains(';', StringComparison.Ordinal) || Sentences(m).Count() > 2 || Sentences(m).Any(s => Words(s) > 20)).Select(m => $"'{Cut(m)}'")),
        new("F34-severity-fits-the-condition", "F", "34", "A block is for a method that gives no result and a warning for a documented fallback.", f =>
            f.Template.ApplicabilityRules.Where(r => (r.Severity == CalcNoticeSeverity.Block && FallbackLanguage().IsMatch(r.Description)) || (r.Severity == CalcNoticeSeverity.Warning && NoResultLanguage().IsMatch(r.Description))).Select(r => $"'{r.Id}' is {r.Severity}: {Cut(r.Description)}")),
        new("F36-emitted-ids-declared", "F", "36", "Every emitted rule id is declared once.", f =>
            f.Sites.Where(s => s.RuleId is not null).Select(s => s.RuleId!).Distinct(StringComparer.Ordinal).Where(id => f.Template.ApplicabilityRules.All(r => r.Id != id)).Select(id => $"'{id}' is emitted and not declared")
                .Concat(f.Template.ApplicabilityRules.GroupBy(r => r.Id).Where(g => g.Count() > 1).Select(g => $"'{g.Key}' is declared twice"))),
        new("F35-emitted-severity-equals-declared", "F", "35", "A rule's emitted severity is its declared severity.", f =>
            f.Sites.Where(s => s.RuleId is not null).Select(s => (s, r: f.Template.ApplicabilityRules.FirstOrDefault(r => r.Id == s.RuleId))).Where(x => x.r is not null && x.r.Severity != x.s.Severity)
                .Select(x => $"'{x.s.RuleId}' is declared {x.r!.Severity} and emitted as {x.s.Severity}")
                .Concat(f.Notices.Select(n => (n, r: f.Template.ApplicabilityRules.FirstOrDefault(r => r.Id == n.RuleId))).Where(x => x.r is not null && x.r.Severity != x.n.Severity).Select(x => $"'{x.n.RuleId}' is declared {x.r!.Severity} and a run emitted it as {x.n.Severity}"))
                .Distinct()),
        new("F36-no-input-prefix", "F", "36", "A rule id never carries the input. prefix the engine adds.", f =>
            f.Template.ApplicabilityRules.Where(r => r.Id.StartsWith("input.", StringComparison.Ordinal)).Select(r => r.Id)
                .Concat(f.Sites.Where(s => s.RuleId is { } id && id.StartsWith("input.", StringComparison.Ordinal)).Select(s => s.RuleId!)).Distinct()),
        new("F37-numbers-in-text", "F", "37", "No thousands separator by comma in a message.", f => Messages(f).Where(m => CommaThousands().IsMatch(m)).Select(m => $"'{Cut(m)}'")),

        // G. Input grouping
        new("G38-groups-when-more-than-five-inputs", "G", "38", "A sheet with more than five inputs groups every input.", f =>
            f.Template.Inputs.Count > 5 ? f.Template.Inputs.Where(i => string.IsNullOrEmpty(i.Group)).Select(i => $"{i.Symbol} has no group") : []),
        new("G39-setup-group-first", "G", "39", "Choice and table inputs that define the problem sit in a first group called Setup.", f =>
            f.Template.Inputs.Count > 5 && f.Template.Inputs.FirstOrDefault() is { Kind: CalcInputKind.Choice or CalcInputKind.Table } first && first.Group != "Setup" ? [$"{first.Symbol} is first and is not in Setup"] : []),
        new("G40-group-names", "G", "40", "Groups use the standard names; none is numbered or uses a retired synonym.", f =>
            f.Template.Inputs.Select(i => i.Group).OfType<string>().Distinct(StringComparer.Ordinal)
                .Where(g => Regex.IsMatch(g, @"\d") || GroupSynonyms.ContainsKey(g)).Select(g => GroupSynonyms.TryGetValue(g, out var use) ? $"'{g}' should be '{use}'" : $"'{g}' is numbered")),
        new("G41-group-order", "G", "41", "Groups run Setup, Geometry, Material, Loads, Conditions, Factors, Requirements; optional inputs come last in a group.", GroupOrder),
        new("G42-soft-limits", "G", "42", "Every numeric input has a usual range inside its hard range.", f =>
            f.Template.Inputs.Where(i => i.Kind is CalcInputKind.Quantity or CalcInputKind.Number && ((i.Min is null && i.Max is null) || (i.Min == i.HardMin && i.Max == i.HardMax && i.HardMin is not null && i.HardMax is not null))).Select(i => $"{i.Symbol} has {(i.Min is null && i.Max is null ? "no soft limit" : "a soft limit equal to its hard limit")}")),

        // H. Ports
        new("H43-port-names", "H", "43", "A port is named <area>.<quantity> from the template's area.", f =>
            (f.Template.OutputPorts ?? []).Select(p => (p, problem: CalcPortNames.Problem(f.Id, p.Name))).Where(x => x.problem is not null).Select(x => $"{x.p.Symbol} {x.problem}")),
        new("H44-ports-declared", "H", "44", "A template declares its output ports.", f => f.Template.OutputPorts is { Count: > 0 } ? [] : ["declares no output ports"]),
        new("H46-record-kinds", "H", "46", "A record kind's title repeats the noun of its id.", f =>
            (f.Template.RecordKinds ?? []).Select(k => (k, problem: RecordKindProblem(k))).Where(x => x.problem is not null).Select(x => $"{x.k.Kind} ('{x.k.Title}') {x.problem}")),
        new("H45-no-legacy-codes", "H", "45", "A port description names its target by template id and input, never by a legacy code.", f =>
            (f.Template.OutputPorts ?? []).Where(p => InternalCode().IsMatch(p.Description)).Select(p => $"{p.Symbol}: '{p.Description}'")),

        // I. Catalogue
        new("I47-title-form", "I", "47", "A title is <Subject> (<standard or method>), without a year or a version.", f => CalcCatalogueStandard.TitleProblems(f.Template.Title).Select(p => $"'{f.Template.Title}' {p}")),
        new("I48-category-vocabulary", "I", "48", "A category is one word from the discipline's list.", f => CalcCatalogueStandard.CategoryProblem(f.Template.Discipline, f.Template.Category) is { } p ? [p] : []),
        new("I49-scope-length", "I", "49", "A scope is at most 40 words in two sentences and begins 'Calculates'.", f =>
        {
            var sentences = Sentences(f.Template.Scope).ToList();
            var words = Words(f.Template.Scope);
            var problems = new List<string>();

            if (words > 40 || sentences.Count > 2)
                problems.Add($"{words} words in {sentences.Count} sentence(s)");

            if (!f.Template.Scope.StartsWith("Calculates ", StringComparison.Ordinal))
                problems.Add("does not begin 'Calculates'");

            return problems;
        }),
        new("I50-method-layout", "I", "50", "A method is one relation per line, label first, at most 8 lines.", f =>
        {
            var lines = f.Template.Method.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var problems = new List<string>();

            if (lines.Length > 8)
                problems.Add($"{lines.Length} lines");

            if (lines.Any(l => !l.Contains(": ", StringComparison.Ordinal)))
                problems.Add("a line has no 'label: relation' form");

            return problems;
        }),

        // J. Clause references
        new("J51-standard-designation", "J", "51", "A standard is written in full with its year, and the same everywhere.", f =>
            f.Template.ClauseRefs.Where(c => IsStandard(c.Standard) && !Designation().IsMatch(c.Standard) || Regex.IsMatch(c.Standard, @"^EN ")).Select(c => $"'{c.Standard}'")
                .Concat(f.Template.ClauseRefs.Where(c => IsStandard(c.Standard) && UsualSpelling(c.Standard) is { } usual && !string.Equals(usual, c.Standard, StringComparison.Ordinal)).Select(c => $"'{c.Standard}' is written '{UsualSpelling(c.Standard)}' elsewhere in the catalogue"))
                .Distinct()),
        new("J52-clause-locator", "J", "52", "A locator is 'cl. x', 'reg. x' (BS 7671 only), 'Ch. n', '§x' or 'Table n', with the edition before or after it, never a bare number.", f =>
            f.Template.ClauseRefs.Where(c => c.Clause.Length > 0 && c.Clause != "—" && (Locator().Match(c.Clause) is not { Success: true } m || m.Groups["kind"].Value == "reg." && !c.Standard.StartsWith("BS 7671", StringComparison.Ordinal))).Select(c => $"{c.Standard} '{c.Clause}'")),
        new("J53-book-citations", "J", "53", "A book is '<Author>, <Title>' with its edition in the locator.", f =>
            f.Template.ClauseRefs.Where(c => !IsStandard(c.Standard) && !(Regex.IsMatch(c.Standard, @"^[^,]+, .+") && Regex.IsMatch(c.Clause, @"\d+(st|nd|rd|th) ed\."))).Select(c => $"'{c.Standard}' / '{c.Clause}'")),
        new("J54-no-notes-in-reference-titles", "J", "54", "A reference title holds no licence note and no brand name.", f =>
            f.Template.ClauseRefs.Where(c => Regex.IsMatch(c.Title + " " + c.Standard, @"(?i)\((cited|not reproduced|not copied)|cited, not copied|cited by number only|\b(Kollmorgen|Parker|Rittal|nVent|SKF|Schaeffler)\b")).Select(c => $"'{c.Standard}' — '{Cut(c.Title)}'")),

        // K. Assumptions
        new("K55-no-internal-codes", "K", "55", "An assumption names no internal code or section number.", f =>
            f.Template.Assumptions.Where(a => InternalCode().IsMatch(a.Statement) || (a.Basis is not null && InternalCode().IsMatch(a.Basis))).Select(a => $"'{Cut(a.Statement)}'")),
        new("K56-one-short-sentence", "K", "56", "An assumption is one full sentence of at most 20 words, with no semicolon.", f =>
            f.Template.Assumptions.Where(a => a.Statement.Contains(';', StringComparison.Ordinal) || Sentences(a.Statement).Count() > 1 || Words(a.Statement) > 20 || !a.Statement.EndsWith('.')).Select(a => $"'{Cut(a.Statement)}'")),
        new("K57-exclusion-stated-once", "K", "57", "A limitation is stated once, in the scope.", f =>
            Regex.IsMatch(f.Template.Scope, @"(?i)outside this sheet|not checked|not covered") ? f.Template.Assumptions.Where(a => Regex.IsMatch(a.Statement, @"(?i)is not (checked|assessed|computed|covered)|are not (checked|assessed|computed|covered)")).Select(a => $"'{Cut(a.Statement)}'") : []),

        // L. Spec docs
        new("L58-spec-headings", "L", "58", "A spec doc has ## headings in the standard order.", f => DocProblems(f, "58")),
        new("L59-spec-title-line", "L", "59", "A spec doc's title line is '# <template title> (`id@version`)'.", f => DocProblems(f, "59")),
        new("L60-spec-scope-is-the-template-scope", "L", "60", "A spec doc's scope is the template's scope.", f => DocProblems(f, "60")),
    ];

    /// <summary>
    /// The title of a record kind repeats the words of its id's noun. The id itself is not judged: it is stored on every record of that kind, so renaming
    /// it needs a migration, and the audit's plural-noun form is left to a later release (decision C2-15).
    /// </summary>
    private static string? RecordKindProblem(CalcRecordKind kind)
    {
        var m = Regex.Match(kind.Kind, @"^(?<area>[a-z0-9]+)\.(?<noun>[a-z0-9]+(-[a-z0-9]+)*)$");

        if (!m.Success)
            return "is not written <area>.<noun>";

        var words = m.Groups["noun"].Value.Split('-').Select(w => w.TrimEnd('s')).Where(w => w.Length > 2);
        var title = kind.Title.ToLowerInvariant();
        return words.All(w => title.Contains(w, StringComparison.Ordinal)) ? null : "has a title that does not repeat the noun";
    }

    private static string Cut(string text) => text.Length <= 90 ? text : text[..90] + "…";

    private static bool IsStandard(string standard) => Regex.IsMatch(standard, @"^(BS |DIN )?(EN|ISO|IEC|IPC|ASME|VDI|JEDEC|API)\b|^(BS|DIN) \d");

    /// <summary>
    /// A designation written in full: the prefixes <see cref="IsStandard"/> admits (after an optional BS or DIN adoption), the number (with a part, a letter,
    /// a "/TS" or a "Part n"), then a colon-or-dash year and any amendments ("BS EN 1993-1-1:2005+A1:2014", "ASME B31.3-2022", "IPC-2221B:2012",
    /// "IEC TR 60890:2022", "VDI 2230 Part 1:2015"); or a national standard by number ("BS 7671:2018").
    /// </summary>
    [GeneratedRegex(@"^(BS |DIN )?(EN|ISO|IEC( TR)?|IPC|ASME|VDI|JEDEC|API)[ \-/]?\S.*[:\-]\d{4}|^(BS|DIN) \d+(-\d+)?:\d{4}")]
    private static partial Regex Designation();

    /// <summary>"cl. 6.2.3", "11th ed., Ch. 11", "Table 8.1, 8th ed.", "reg. 525": an optional edition, then a locator of a kind the standard allows.</summary>
    [GeneratedRegex(@"^(\d+(st|nd|rd|th) ed\.(, )?)?(?<kind>cl\.|reg\.|Ch\.|§|Table |Fig\.|Annex |App\.)")]
    private static partial Regex Locator();

    /// <summary>The standard's number without its adoption prefix, year, amendments or part ("EN 1993-1-1", "7671"): what two spellings of one standard share.</summary>
    private static string StandardKey(string standard)
    {
        var key = Regex.Replace(standard, @"^(BS |DIN )", string.Empty);
        key = Regex.Replace(key, @"(:\d{4}.*|-\d{4}.*| Part .*)$", string.Empty);
        return key.Trim();
    }

    /// <summary>Every standard designation of the catalogue, by number: its spellings, the most used first.</summary>
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>>> Spellings = new(() =>
        AllCalculators.All.SelectMany(c => c.Template.ClauseRefs.Select(r => r.Standard)).Where(IsStandard)
            .GroupBy(StandardKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)[.. g.GroupBy(x => x, StringComparer.Ordinal).OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key)], StringComparer.Ordinal));

    /// <summary>The spelling of this standard the catalogue uses most (the first in ordinal order on a tie), or <see langword="null"/> when it is spelled one way.</summary>
    private static string? UsualSpelling(string standard) =>
        Spellings.Value.TryGetValue(StandardKey(standard), out var spellings) && spellings.Count > 1 ? spellings[0] : null;

    private static IEnumerable<string> Symbols(TemplateFacts f, string row) =>
        f.QuantityFacts.Where(q => q.Origin != "port")
            .SelectMany(q => CalcSymbolStandard.Problems(q.Symbol, q.Dimension, q.Unit).Where(p => p.Row == row).Select(p => $"{p.Problem} ({q.Origin})")).Distinct();

    private static IEnumerable<string> GroupOrder(TemplateFacts f)
    {
        var problems = new List<string>();
        var seen = f.Template.Inputs.Where(i => i.Group is not null && GroupOrderList.Contains(i.Group)).Select(i => i.Group!).Distinct().ToList();
        var indexes = seen.Select(g => Array.IndexOf(GroupOrderList, g)).ToList();

        if (indexes.Zip(indexes.Skip(1), (a, b) => a > b).Any(x => x))
            problems.Add($"groups run {string.Join(", ", seen)}");

        foreach (var group in f.Template.Inputs.Where(i => i.Group is not null).GroupBy(i => i.Group!))
        {
            var list = group.ToList();

            for (var k = 0; k < list.Count - 1; k++)
            {
                if (list[k].IsOptional && !list[k + 1].IsOptional)
                {
                    problems.Add($"optional {list[k].Symbol} is before {list[k + 1].Symbol} in {group.Key}");
                    break;
                }
            }
        }

        return problems;
    }

    // ---- spec docs ----------------------------------------------------

    private static readonly string[] DocHeadings = ["Scope", "Method", "Inputs", "Assumptions", "Outputs", "Ports", "Checks", "Limitations", "References", "Test cases"];

    private static string? DocPath(CalcTemplate t)
    {
        var folder = t.Discipline switch
        {
            CalcDiscipline.BuildingServices => "building-services",
            CalcDiscipline.FluidPower => "fluid-power",
            var d => d.ToString().ToLowerInvariant(),
        };

        var path = Path.Combine(WorkedExamples.RepositoryRoot, "docs", "calcs", folder, t.Id + ".md");
        return File.Exists(path) ? path : null;
    }

    private static IEnumerable<string> DocProblems(TemplateFacts f, string row)
    {
        var path = DocPath(f.Template);

        if (path is null)
            return ["has no spec doc"];

        var text = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        var problems = new List<string>();

        switch (row)
        {
            case "58":
            {
                var headings = Regex.Matches(text, @"^## (?<h>.+?)\s*$", RegexOptions.Multiline).Select(m => m.Groups["h"].Value).ToList();
                var required = new List<string> { "Scope", "Method", "Inputs", "Assumptions", "Outputs", "Test cases" };

                if (f.Template.OutputPorts is { Count: > 0 })
                    required.Add("Ports");

                if (f.Template.ApplicabilityRules.Count > 0 || f.DefaultRun.Checks.Count > 0)
                    required.Add("Checks");

                problems.AddRange(required.Where(h => !headings.Contains(h)).Select(h => $"has no '## {h}' heading"));
                var order = headings.Where(DocHeadings.Contains).Select(h => Array.IndexOf(DocHeadings, h)).ToList();

                if (order.Zip(order.Skip(1), (a, b) => a > b).Any(x => x))
                    problems.Add($"headings run {string.Join(", ", headings.Where(DocHeadings.Contains))}");

                break;
            }

            case "59":
            {
                var first = text.Split('\n')[0];
                var expected = $"# {f.Template.Title} (`{f.Template.Reference}`)";

                if (!string.Equals(first, expected, StringComparison.Ordinal))
                    problems.Add($"title line is '{Cut(first)}', expected '{expected}'");

                break;
            }

            case "60":
            {
                var scope = Regex.Replace(f.Template.Scope, @"\s+", " ");

                if (!Regex.Replace(text, @"\s+", " ").Contains(scope, StringComparison.Ordinal))
                    problems.Add("does not carry the template's scope word for word");

                break;
            }
        }

        return problems;
    }
}
