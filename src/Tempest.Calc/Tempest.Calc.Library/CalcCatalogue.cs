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
    private readonly object _gate = new();

    public CalcCatalogue() { }

    public CalcCatalogue(IEnumerable<ICalcCalculator> calculators)
    {
        ArgumentNullException.ThrowIfNull(calculators);

        foreach (var c in calculators)
            Register(c);
    }

    /// <summary>Registers a calculator. A second registration of the same id@version is refused; a newer version becomes current.</summary>
    public CalcCatalogueEntry Register(ICalcCalculator calculator)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        var template = calculator.Template;
        ValidateTemplate(template);

        lock (_gate)
        {
            if (_byReference.ContainsKey(template.Reference))
                throw new InvalidOperationException($"Calculator '{template.Reference}' is already registered.");

            var entry = new CalcCatalogueEntry(calculator, CalcTemplateHasher.Hash(calculator));
            _byReference[template.Reference] = entry;

            if (!_current.TryGetValue(template.Id, out var current) || CompareVersions(template.Version, current.Template.Version) > 0)
                _current[template.Id] = entry;

            return entry;
        }
    }

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

    public CalcCatalogueEntry Get(string reference) => Find(reference) ?? throw new KeyNotFoundException($"No calculator '{reference}' is registered.");

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
