using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>One catalogue entry: a calculator, its template and its content hash, computed once.</summary>
public sealed record CalcCatalogueEntry(ICalcCalculator Calculator, string ContentHash)
{
    public CalcTemplate Template => Calculator.Template;

    public string Reference => Template.Reference;

    public string ShortHash => CalcTemplateHasher.Short(ContentHash);
}

/// <summary>
/// The registry of every calculator the application composes in, by id
/// and version. Static registration only: the calculator assemblies hand
/// their instances to the composer, nothing is loaded from a path.
/// </summary>
public sealed class CalcCatalogue
{
    private readonly Dictionary<string, CalcCatalogueEntry> _byReference = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CalcCatalogueEntry> _current = new(StringComparer.Ordinal);

    // Calculators gated behind a feature flag that is off (ADR-0173): validated and kept, never listed, found, created or issued.
    private readonly Dictionary<string, CalcCatalogueEntry> _withheldByReference = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CalcCatalogueEntry> _withheldCurrent = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public CalcCatalogue() : this((CalcFeatureFlags?)null) { }

    public CalcCatalogue(CalcFeatureFlags? features) => Features = features ?? CalcFeatureFlags.None;

    public CalcCatalogue(IEnumerable<ICalcCalculator> calculators, CalcFeatureFlags? features = null) : this(features)
    {
        ArgumentNullException.ThrowIfNull(calculators);

        foreach (var c in calculators)
            Register(c);
    }

    /// <summary>The optional features this catalogue was built with. Fixed for its lifetime.</summary>
    public CalcFeatureFlags Features { get; }

    /// <summary>Registers a calculator. A second registration of the same id@version is refused; a newer version becomes current.</summary>
    public CalcCatalogueEntry Register(ICalcCalculator calculator)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        var template = calculator.Template;
        ValidateTemplate(template);

        // One run on the defaults gives the formulas the content hash covers and the output symbols a port alias must not take.
        var defaultRun = CalcRunner.Run(calculator, CalcTemplateHasher.DefaultInputs(template));
        ValidatePortAliases(template, defaultRun);
        var contentHash = CalcTemplateHasher.Hash(template, CalcTemplateHasher.Formulas(defaultRun));

        lock (_gate)
        {
            if (_byReference.ContainsKey(template.Reference) || _withheldByReference.ContainsKey(template.Reference))
                throw new InvalidOperationException($"Calculator '{template.Reference}' is already registered.");

            var entry = new CalcCatalogueEntry(calculator, contentHash);

            // A calculator whose feature is off is held aside: no list, no search, no lookup and so no new instance reaches it (ADR-0173).
            var (byReference, current) = Features.IsEnabled(template.FeatureFlag) ? (_byReference, _current) : (_withheldByReference, _withheldCurrent);
            byReference[template.Reference] = entry;

            if (!current.TryGetValue(template.Id, out var latest) || CompareVersions(template.Version, latest.Template.Version) > 0)
                current[template.Id] = entry;

            return entry;
        }
    }

    /// <summary>
    /// Why a calculator that is composed in but switched off cannot be used, or <see langword="null"/> when <paramref name="reference"/>
    /// ("id@version" or id) is not a withheld calculator. The one message every refusal path shows.
    /// </summary>
    public string? FeatureRefusal(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return null;

        lock (_gate)
        {
            var withheld = _withheldByReference.GetValueOrDefault(reference) ?? _withheldCurrent.GetValueOrDefault(reference.Split('@')[0]);

            if (withheld is null || _byReference.ContainsKey(reference))
                return null;

            var flag = withheld.Template.FeatureFlag ?? string.Empty;
            return Tempest.Core.Calculations.CalculationFeatureGate.Refusal(withheld.Template.Title, withheld.Template.Id, flag);
        }
    }

    /// <summary>Whether <paramref name="reference"/> names a calculator that is composed in but switched off.</summary>
    public bool IsWithheld(string? reference) => FeatureRefusal(reference) is not null;

    public IReadOnlyList<CalcCatalogueEntry> All()
    {
        lock (_gate) { return _byReference.Values.OrderBy(e => e.Template.Discipline).ThenBy(e => e.Template.Title, StringComparer.Ordinal).ToList(); }
    }

    /// <summary>The current version of every calculator, by discipline then title.</summary>
    public IReadOnlyList<CalcCatalogueEntry> Current()
    {
        lock (_gate) { return _current.Values.OrderBy(e => e.Template.Discipline).ThenBy(e => e.Template.Title, StringComparer.Ordinal).ToList(); }
    }

    public IReadOnlyList<CalcCatalogueEntry> ByDiscipline(CalcDiscipline discipline) => Current().Where(e => e.Template.Discipline == discipline).ToList();

    /// <summary>Finds by "id@version" or by id alone (current version).</summary>
    public CalcCatalogueEntry? Find(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return null;

        lock (_gate)
        {
            if (_byReference.TryGetValue(reference, out var exact))
                return exact;

            return _current.GetValueOrDefault(reference);
        }
    }

    public CalcCatalogueEntry Get(string reference) =>
        Find(reference) ?? throw (FeatureRefusal(reference) is { } switchedOff ? new CalcFeatureDisabledException(switchedOff) : new KeyNotFoundException($"No calculator '{reference}' is registered."));

    /// <summary>
    /// C-2: a port is also known by its public name and its aliases, so none of them may be the symbol of an output that is not that port: a link by that
    /// symbol would then name two outputs. The outputs are those of a run on the defaults and the symbols the known-answer cases expect.
    /// </summary>
    private static void ValidatePortAliases(CalcTemplate t, CalcRunResult defaultRun)
    {
        if (t.OutputPorts is not { Count: > 0 } ports)
            return;

        var portSymbols = ports.Select(p => p.Symbol).ToHashSet(StringComparer.Ordinal);
        var others = defaultRun.Outputs.Select(o => o.Symbol).Concat(t.TestCases.SelectMany(c => c.Expected).Select(e => e.Symbol))
            .Where(s => !portSymbols.Contains(s)).ToHashSet(StringComparer.Ordinal);

        foreach (var port in ports)
        {
            foreach (var name in (port.Name is { Length: > 0 } n ? [n] : Enumerable.Empty<string>()).Concat(port.Aliases ?? []))
            {
                if (others.Contains(name))
                    throw new ArgumentException($"Template '{t.Id}': the name '{name}' of port '{port.Symbol}' is the symbol of another output of the template; a link by that symbol would name two outputs.");
            }
        }
    }

    private static void ValidateTemplate(CalcTemplate t)
    {
        if (string.IsNullOrWhiteSpace(t.Id) || t.Id.Length > 64 || t.Id.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')))
            throw new ArgumentException($"Template id '{t.Id}' must be 1–64 characters of letters, digits, '.', '-' or '_'.");

        if (string.IsNullOrWhiteSpace(t.Version) || t.Version.Length > 32 || t.Version.Any(c => !(char.IsAsciiDigit(c) || c == '.')))
            throw new ArgumentException($"Template version '{t.Version}' must be dotted digits.");

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var i in t.Inputs)
        {
            if (!seen.Add(i.Symbol))
                throw new ArgumentException($"Template '{t.Id}' declares input '{i.Symbol}' twice.");
        }

        // C-2: an alias is the symbol an input had before it was renamed, so it must name no input of the template and no other input's alias.
        foreach (var i in t.Inputs)
        {
            foreach (var alias in i.Aliases ?? [])
            {
                if (!seen.Add(alias))
                    throw new ArgumentException($"Template '{t.Id}': the alias '{alias}' of input '{i.Symbol}' is already an input or the alias of another input.");
            }
        }

        // C-2: a port is known by its output symbol, its public name and its aliases; no two ports may share one of those names.
        var portNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var port in t.OutputPorts ?? [])
        {
            foreach (var name in new[] { port.Symbol }.Concat(port.Name is { Length: > 0 } n ? [n] : []).Concat(port.Aliases ?? []).Distinct(StringComparer.Ordinal))
            {
                if (!portNames.Add(name))
                    throw new ArgumentException($"Template '{t.Id}': the port name '{name}' of output '{port.Symbol}' is already used by another port.");
            }
        }

        // ADR-0173: the governance a template asks for must be satisfiable, or no instance of it could ever be issued.
        if (t.FeatureFlag is { Length: > 0 } flag && !CalcFeatures.All.Contains(flag, StringComparer.Ordinal))
            throw new ArgumentException($"Template '{t.Id}' names the unknown feature flag '{flag}'.");

        if (t.RequiresAttestation)
        {
            foreach (var symbol in CalcAttestation.Symbols)
            {
                if (t.FindInput(symbol) is not { Kind: CalcInputKind.Text })
                    throw new ArgumentException($"Template '{t.Id}' requires an attestation, so it must declare the text input '{symbol}'.");
            }
        }

        if (t.RequiresSecondCheck && !t.IndependentCheck)
            throw new ArgumentException($"Template '{t.Id}' requires a second check, so its first check must also be independent (IndependentCheck).");
    }

    internal static int CompareVersions(string a, string b)
    {
        var pa = a.Split('.').Select(x => int.TryParse(x, out var n) ? n : 0).ToArray();
        var pb = b.Split('.').Select(x => int.TryParse(x, out var n) ? n : 0).ToArray();

        for (var i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            var x = i < pa.Length ? pa[i] : 0;
            var y = i < pb.Length ? pb[i] : 0;

            if (x != y)
                return x.CompareTo(y);
        }

        return 0;
    }
}
