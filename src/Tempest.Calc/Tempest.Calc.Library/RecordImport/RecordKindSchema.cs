using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>A calculator that declares a record kind: its template id and title, and the symbols it takes from that kind.</summary>
public sealed record RecordKindUser(string TemplateId, string TemplateTitle, string Reference, IReadOnlyList<string> Symbols);

/// <summary>One column of a table symbol of a record kind, with the calculator input specification(s) it comes from.</summary>
public sealed record RecordSchemaColumn(string Key, string Label, CalcInputKind Kind, string Unit, IReadOnlyList<(string TemplateId, CalcColumnSpec Spec)> Sources)
{
    /// <summary>Whether any calculator needs this column filled in every row.</summary>
    public bool IsRequired => Sources.Any(s => !s.Spec.IsOptional);
}

/// <summary>One symbol of a record kind: a scalar the calculator takes (<c>C_a</c>) or a table (<c>ampacity</c>), with the specifications that bound it.</summary>
public sealed record RecordSchemaSymbol(string Symbol, string Description, CalcInputKind Kind, string Unit, IReadOnlyList<(string TemplateId, CalcInputSpec Spec)> Sources, IReadOnlyList<RecordSchemaColumn> Columns)
{
    public bool IsTable => Kind == CalcInputKind.Table;

    public int MaxRows => Sources.Count == 0 ? CalcDataRecord.MaxRows : Sources.Max(s => s.Spec.MaxRows);

    public RecordSchemaColumn? Column(string key) => Columns.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.Ordinal));
}

/// <summary>
/// A record kind as the calculators declare it (Addendum C, C-3): what it is called, which calculators draw on it, and — from their input
/// specifications — the symbols it holds, with the unit, the hard and soft limits and (for tables) the columns of each. It is built from the
/// catalogue, so the importer's idea of a kind can never differ from what the calculators read.
/// </summary>
public sealed class RecordKindSchema
{
    private RecordKindSchema(string kind, string title, string guidance, IReadOnlyList<RecordKindUser> users, IReadOnlyList<RecordSchemaSymbol> symbols)
    {
        Kind = kind;
        Title = title;
        Guidance = guidance;
        Users = users;
        Symbols = symbols;
    }

    public string Kind { get; }

    /// <summary>The kind's title as the first calculator that declares it words it.</summary>
    public string Title { get; }

    public string Guidance { get; }

    public IReadOnlyList<RecordKindUser> Users { get; }

    public IReadOnlyList<RecordSchemaSymbol> Symbols { get; }

    public bool IsMaterial => MaterialLibrary.IsMaterial(Kind);

    public RecordSchemaSymbol? Symbol(string symbol) => Symbols.FirstOrDefault(s => string.Equals(s.Symbol, symbol, StringComparison.Ordinal));

    public IEnumerable<RecordSchemaSymbol> Scalars => Symbols.Where(s => !s.IsTable);

    public IEnumerable<RecordSchemaSymbol> Tables => Symbols.Where(s => s.IsTable);

    /// <summary>The kinds every visible calculator of <paramref name="catalogue"/> declares, by kind.</summary>
    public static IReadOnlyDictionary<string, RecordKindSchema> Build(CalcCatalogue catalogue)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        var byKind = new Dictionary<string, List<(CalcTemplate Template, CalcRecordKind Kind)>>(StringComparer.Ordinal);

        foreach (var entry in catalogue.Current())
        {
            foreach (var kind in entry.Template.RecordKinds ?? [])
            {
                if (!byKind.TryGetValue(kind.Kind, out var list))
                    byKind[kind.Kind] = list = [];

                list.Add((entry.Template, kind));
            }
        }

        var result = new Dictionary<string, RecordKindSchema>(StringComparer.Ordinal);

        foreach (var (kind, declarers) in byKind.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var symbols = new List<RecordSchemaSymbol>();

            foreach (var symbol in declarers.SelectMany(d => d.Kind.Symbols).Distinct(StringComparer.Ordinal))
            {
                var sources = declarers
                    .Where(d => d.Kind.Symbols.Contains(symbol, StringComparer.Ordinal) && d.Template.FindInput(symbol) is not null)
                    .Select(d => (d.Template.Id, Spec: d.Template.FindInput(symbol)!))
                    .ToList();

                if (sources.Count == 0)
                    continue;

                var first = sources[0].Spec;
                var columns = new List<RecordSchemaColumn>();

                foreach (var key in sources.SelectMany(s => s.Spec.Columns ?? []).Select(c => c.Key).Distinct(StringComparer.Ordinal))
                {
                    var colSources = sources.SelectMany(s => (s.Spec.Columns ?? []).Where(c => string.Equals(c.Key, key, StringComparison.Ordinal)).Select(c => (s.Id, c))).ToList();
                    var firstColumn = colSources[0].c;
                    columns.Add(new RecordSchemaColumn(key, firstColumn.Label, firstColumn.Kind, firstColumn.DefaultUnit, colSources));
                }

                symbols.Add(new RecordSchemaSymbol(symbol, first.Description, first.Kind, first.DefaultUnit, sources, columns));
            }

            var users = declarers.Select(d => new RecordKindUser(d.Template.Id, d.Template.Title, d.Template.Reference, d.Kind.Symbols)).ToList();
            result[kind] = new RecordKindSchema(kind, declarers[0].Kind.Title, declarers[0].Kind.Guidance, users, symbols);
        }

        return result;
    }
}
