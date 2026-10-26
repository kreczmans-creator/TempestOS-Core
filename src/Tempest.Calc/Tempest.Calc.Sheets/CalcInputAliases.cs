namespace Tempest.Calc.Sheets;

/// <summary>
/// An input was given under both an old symbol and the symbol it has become, with different values. Neither is dropped: the caller refuses the set
/// and says which two to reconcile.
/// </summary>
public sealed class CalcInputAliasConflictException(string alias, string symbol) : ArgumentException($"{symbol}: given as '{symbol}' and as its old symbol '{alias}' with different values; keep one.")
{
    /// <summary>The old symbol.</summary>
    public string Alias { get; } = alias;

    /// <summary>The current symbol of the input.</summary>
    public string Symbol { get; } = symbol;
}

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
    /// The values with every alias replaced by the current symbol. An input given under an alias and again under its current symbol (or under two of
    /// its aliases) with the same value is kept once; with different values the set is refused: <see cref="CalcInputAliasConflictException"/> names the two
    /// (nothing is dropped silently). A set with no alias in it is returned as it is.
    /// </summary>
    /// <exception cref="CalcInputAliasConflictException">An input is given under two of its names with different values.</exception>
    public static IReadOnlyList<CalcInputValue> Canonicalise(CalcTemplate template, IReadOnlyList<CalcInputValue> values)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);

        if (!Any(template) || values.All(v => template.FindInput(v.Symbol) is not null || template.ResolveInput(v.Symbol) is null))
            return values;

        var kept = new Dictionary<string, CalcInputValue>(StringComparer.Ordinal);

        foreach (var v in values.Where(v => template.FindInput(v.Symbol) is not null))
            kept.TryAdd(v.Symbol, v);

        var result = new List<CalcInputValue>(values.Count);
        var added = new HashSet<string>(StringComparer.Ordinal);

        foreach (var v in values)
        {
            if (template.FindInput(v.Symbol) is not null || template.ResolveInput(v.Symbol) is not { } spec)
            {
                result.Add(v);
                continue;
            }

            if (kept.TryGetValue(spec.Symbol, out var existing))
            {
                if (!SameValue(existing, v))
                    throw new CalcInputAliasConflictException(v.Symbol, spec.Symbol);

                continue;
            }

            kept[spec.Symbol] = v with { Symbol = spec.Symbol };

            if (added.Add(spec.Symbol))
                result.Add(kept[spec.Symbol]);
        }

        return result;
    }

    /// <summary>
    /// As <see cref="Canonicalise(CalcTemplate, IReadOnlyList{CalcInputValue})"/>, for a caller that reports instead of throwing: returns
    /// <see langword="false"/> and the message when an input is given under two of its names with different values, with the values unchanged.
    /// </summary>
    public static bool TryCanonicalise(CalcTemplate template, IReadOnlyList<CalcInputValue> values, out IReadOnlyList<CalcInputValue> canonical, out string? conflict)
    {
        try
        {
            canonical = Canonicalise(template, values);
            conflict = null;
            return true;
        }
        catch (CalcInputAliasConflictException e)
        {
            canonical = values;
            conflict = e.Message;
            return false;
        }
    }

    /// <summary>Whether two inputs hold the same value (number, unit, text, flag, vector, table); the symbol and the provenance are not compared.</summary>
    public static bool SameValue(CalcInputValue a, CalcInputValue b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        return a.Number == b.Number
            && string.Equals(a.Unit, b.Unit, StringComparison.Ordinal)
            && string.Equals(a.Text, b.Text, StringComparison.Ordinal)
            && a.Flag == b.Flag
            && ((a.Vector is null && b.Vector is null) || (a.Vector is not null && b.Vector is not null && a.Vector.SequenceEqual(b.Vector)))
            && ((a.Rows is null && b.Rows is null) || (a.Rows is not null && b.Rows is not null && a.Rows.Count == b.Rows.Count && a.Rows.Zip(b.Rows).All(p => SameRow(p.First, p.Second))));
    }

    private static bool SameRow(CalcRow x, CalcRow y) =>
        x.Cells.Count == y.Cells.Count
        && x.Cells.All(p => y.Cells.TryGetValue(p.Key, out var o) && p.Value.Number == o.Number && string.Equals(p.Value.Unit, o.Unit, StringComparison.Ordinal) && string.Equals(p.Value.Text, o.Text, StringComparison.Ordinal) && p.Value.Flag == o.Flag);

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

    /// <summary>As <see cref="Canonicalise(CalcTemplate, CalcInputSet)"/>, returning <see langword="false"/> and the message on a conflict instead of throwing.</summary>
    public static bool TryCanonicalise(CalcTemplate template, CalcInputSet inputs, out CalcInputSet canonical, out string? conflict)
    {
        try
        {
            canonical = Canonicalise(template, inputs);
            conflict = null;
            return true;
        }
        catch (CalcInputAliasConflictException e)
        {
            canonical = inputs;
            conflict = e.Message;
            return false;
        }
    }

    /// <summary>The aliases the values use, as (alias, current symbol) pairs, for a log line; empty when none is used.</summary>
    public static IReadOnlyList<(string Alias, string Symbol)> Renamed(CalcTemplate template, IEnumerable<string> symbols)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(symbols);
        return [.. symbols.Where(s => template.FindInput(s) is null && template.ResolveInput(s) is not null).Select(s => (s, template.ResolveInput(s)!.Symbol))];
    }
}
