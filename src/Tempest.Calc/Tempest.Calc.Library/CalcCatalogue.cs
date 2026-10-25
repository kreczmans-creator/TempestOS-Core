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

        lock (_gate)
        {
            if (_byReference.ContainsKey(template.Reference) || _withheldByReference.ContainsKey(template.Reference))
                throw new InvalidOperationException($"Calculator '{template.Reference}' is already registered.");

            var entry = new CalcCatalogueEntry(calculator, CalcTemplateHasher.Hash(calculator));

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
            return $"Calculator '{withheld.Template.Title}' ({withheld.Template.Id}) is switched off: its '{flag}' feature is not enabled in this installation " +
                   $"(configuration key {CalcFeatureFlags.ConfigurationKey(flag)}). It stays off until the owner has completed its review checklist; nothing can be created, checked or issued with it while it is.";
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
