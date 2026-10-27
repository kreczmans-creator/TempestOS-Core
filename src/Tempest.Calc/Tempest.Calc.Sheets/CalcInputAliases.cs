namespace Tempest.Calc.Sheets;

/// <summary>
/// An input was given under two of its names (the symbol it has now, or one of its old symbols) with different values. Neither is dropped: the caller refuses the set
/// and says which two names to reconcile. The message names the two names that were actually given, never the current symbol when only old ones were.
/// </summary>
public sealed class CalcInputAliasConflictException(string firstGiven, string secondGiven, string symbol)
    : ArgumentException($"{(string.Equals(firstGiven, symbol, StringComparison.Ordinal) || string.Equals(secondGiven, symbol, StringComparison.Ordinal) ? symbol : firstGiven)}: the input is given as '{firstGiven}' and as '{secondGiven}' with different values. Keep one.")
{
    /// <summary>The two names the input was given under, in the order they were met.</summary>
    public IReadOnlyList<string> Given { get; } = [firstGiven, secondGiven];

    /// <summary>An old symbol among the two names given (the second when neither is the current symbol).</summary>
    public string Alias { get; } = string.Equals(secondGiven, symbol, StringComparison.Ordinal) ? firstGiven : secondGiven;

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
        var givenAs = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var v in values.Where(v => template.FindInput(v.Symbol) is not null))
        {
            if (kept.TryAdd(v.Symbol, v))
                givenAs[v.Symbol] = v.Symbol;
        }

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
                if (!SameValue(spec, existing, v))
                    throw new CalcInputAliasConflictException(givenAs.GetValueOrDefault(spec.Symbol, spec.Symbol), v.Symbol, spec.Symbol);

                continue;
            }

            kept[spec.Symbol] = v with { Symbol = spec.Symbol };
            givenAs[spec.Symbol] = v.Symbol;

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

    /// <summary>Whether two inputs hold the same value (number, unit, text, flag, vector, table); the symbol and the provenance are not compared. A quantity is compared in SI.</summary>
    public static bool SameValue(CalcInputValue a, CalcInputValue b) => SameValue(null, a, b);

    /// <summary>
    /// Whether two inputs hold the same value, a quantity compared in SI: 12000 N and 12 kN are the same force, 0.012 m and 12 mm the same length. An input with no unit of its own
    /// is in the default unit of <paramref name="spec"/>. Text, flags and the shape of a vector or table are compared as they are.
    /// </summary>
    public static bool SameValue(CalcInputSpec? spec, CalcInputValue a, CalcInputValue b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var defaultUnit = spec?.DefaultUnit;

        return SameQuantity(a.Number, a.Unit, b.Number, b.Unit, defaultUnit)
            && string.Equals(a.Text, b.Text, StringComparison.Ordinal)
            && a.Flag == b.Flag
            && ((a.Vector is null && b.Vector is null) || (a.Vector is not null && b.Vector is not null && a.Vector.Count == b.Vector.Count && a.Vector.Zip(b.Vector).All(p => SameQuantity(p.First, a.Unit, p.Second, b.Unit, defaultUnit))))
            && ((a.Rows is null && b.Rows is null) || (a.Rows is not null && b.Rows is not null && a.Rows.Count == b.Rows.Count && a.Rows.Zip(b.Rows).All(p => SameRow(p.First, p.Second))));
    }

    private static bool SameRow(CalcRow x, CalcRow y) =>
        x.Cells.Count == y.Cells.Count
        && x.Cells.All(p => y.Cells.TryGetValue(p.Key, out var o) && SameQuantity(p.Value.Number, p.Value.Unit, o.Number, o.Unit, null) && string.Equals(p.Value.Text, o.Text, StringComparison.Ordinal) && p.Value.Flag == o.Flag);

    /// <summary>Two numbers in their units are the same quantity when they are equal in SI (to a part in 10^12); with no unit parsed on either side they are equal only when the numbers and the unit texts are.</summary>
    private static bool SameQuantity(double? x, string? xUnit, double? y, string? yUnit, string? defaultUnit)
    {
        if (x is null || y is null)
            return x is null && y is null && string.Equals(xUnit, yUnit, StringComparison.Ordinal);

        var xText = string.IsNullOrEmpty(xUnit) ? defaultUnit ?? string.Empty : xUnit;
        var yText = string.IsNullOrEmpty(yUnit) ? defaultUnit ?? string.Empty : yUnit;

        if (xText.Length == 0 && yText.Length == 0)
            return x == y;

        if (!Engine.Units.UnitCatalog.TryParse(xText, out var xParsed, out _) || !Engine.Units.UnitCatalog.TryParse(yText, out var yParsed, out _))
            return x == y && string.Equals(xText, yText, StringComparison.Ordinal);

        if (xParsed.Dimension != yParsed.Dimension)
            return false;

        var xSi = xParsed.ToSi(x.Value);
        var ySi = yParsed.ToSi(y.Value);
        return xSi == ySi || Math.Abs(xSi - ySi) <= 1e-12 * Math.Max(Math.Abs(xSi), Math.Abs(ySi));
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
        return ReferenceEquals(canonical, values) ? inputs : new CalcInputSet(canonical, inputs.MadeOn);
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
