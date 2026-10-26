namespace Tempest.Calc.Sheets;

/// <summary>
/// C-2: reads an input by a symbol it had before it was renamed. A template that renames an input lists the old symbol in
/// <see cref="CalcInputSpec.Aliases"/>; an inputs file, a CSV, a saved draft, a pack binding or a link that still names the old
/// symbol is then read as the input it has become, with the same value, unit and provenance. Nothing writes an alias: every
/// value that leaves the system carries the current symbol.
/// </summary>
public static class CalcInputAliases
{
    /// <summary>Whether any input of <paramref name="template"/> has an alias.</summary>
    public static bool Any(CalcTemplate template) => template.Inputs.Any(i => i.Aliases is { Count: > 0 });

    /// <summary>
    /// The current symbol for <paramref name="symbolOrAlias"/>: the symbol itself when it is one of the template's inputs, the input
    /// it is an alias of, or the text unchanged when it names neither (an unknown symbol stays unknown, for the caller to report).
    /// </summary>
    public static string Canonical(CalcTemplate template, string symbolOrAlias)
    {
        ArgumentNullException.ThrowIfNull(template);
        return template.ResolveInput(symbolOrAlias)?.Symbol ?? symbolOrAlias;
    }

    /// <summary>
    /// The values with every alias replaced by the current symbol. A value under the current symbol wins over one under an alias
    /// (the input was given twice; the new name is the one the user can see), and the alias entry is dropped. A set with no alias in
    /// it is returned as it is.
    /// </summary>
    public static IReadOnlyList<CalcInputValue> Canonicalise(CalcTemplate template, IReadOnlyList<CalcInputValue> values)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);

        if (!Any(template) || values.All(v => template.FindInput(v.Symbol) is not null || template.ResolveInput(v.Symbol) is null))
            return values;

        var current = values.Where(v => template.FindInput(v.Symbol) is not null).Select(v => v.Symbol).ToHashSet(StringComparer.Ordinal);
        var result = new List<CalcInputValue>(values.Count);

        foreach (var v in values)
        {
            if (template.FindInput(v.Symbol) is not null || template.ResolveInput(v.Symbol) is not { } spec)
            {
                result.Add(v);
                continue;
            }

            if (current.Add(spec.Symbol))
                result.Add(v with { Symbol = spec.Symbol });
        }

        return result;
    }

    /// <summary><see cref="Canonicalise(CalcTemplate, IReadOnlyList{CalcInputValue})"/> for an input set.</summary>
    public static CalcInputSet Canonicalise(CalcTemplate template, CalcInputSet inputs)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(inputs);

        if (!Any(template))
            return inputs;

        var values = inputs.Values.ToList();
        var canonical = Canonicalise(template, values);
        return ReferenceEquals(canonical, values) ? inputs : new CalcInputSet(canonical);
    }

    /// <summary>The aliases the values use, as (alias, current symbol) pairs, for a log line; empty when none is used.</summary>
    public static IReadOnlyList<(string Alias, string Symbol)> Renamed(CalcTemplate template, IEnumerable<string> symbols)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(symbols);
        return [.. symbols.Where(s => template.FindInput(s) is null && template.ResolveInput(s) is not null).Select(s => (s, template.ResolveInput(s)!.Symbol))];
    }
}
