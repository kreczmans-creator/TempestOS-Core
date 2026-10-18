using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Tempest.Core.Tests.Invoicing.Xero.Simulator;

/// <summary>
/// Wire-format helpers for <see cref="XeroApiSimulator"/>: query strings,
/// Microsoft JSON dates (<c>/Date(1767225600000+0000)/</c>), JSON field
/// access, and the subset of Xero's <c>where=</c> grammar TempestOS uses.
/// </summary>
internal static partial class XeroWire
{
    /// <summary>Options for every JSON object the simulator builds: property names case-insensitive, as Xero reads them.</summary>
    public static readonly JsonNodeOptions NodeOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>A new, case-insensitive <see cref="JsonObject"/>.</summary>
    public static JsonObject NewObject() => new(NodeOptions);

    /// <summary>Decodes a query string (leading <c>?</c> optional; <c>+</c> is a space) into a case-insensitive dictionary; a repeated name keeps its last value.</summary>
    public static IReadOnlyDictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = pair.IndexOf('=', StringComparison.Ordinal);
            var name = Decode(equals < 0 ? pair : pair[..equals]);
            var value = equals < 0 ? string.Empty : Decode(pair[(equals + 1)..]);
            result[name] = value;
        }

        return result;

        static string Decode(string part) => Uri.UnescapeDataString(part.Replace('+', ' '));
    }

    /// <summary>A UTC instant as Xero writes it: <c>/Date(ms+0000)/</c>.</summary>
    public static string MsDate(DateTimeOffset at) => $"/Date({at.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}+0000)/";

    /// <summary>A calendar date as Xero writes it: midnight UTC as <c>/Date(ms+0000)/</c>.</summary>
    public static string MsDate(DateOnly date) => MsDate(new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

    /// <summary>A calendar date as Xero's companion <c>…String</c> field writes it.</summary>
    public static string DateString(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00:00";

    /// <summary>Parses a date Xero accepts: ISO (<c>2026-10-02</c>, <c>2026-10-02T00:00:00</c>) or <c>/Date(ms±zzzz)/</c>.</summary>
    public static bool TryParseDate(string? raw, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var match = MsDateRegex().Match(raw);
        if (match.Success)
        {
            if (!long.TryParse(match.Groups["ms"].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var ms))
                return false;
            date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime);
            return true;
        }

        // Exact ISO shapes only: no culture-dependent forms (10/02/2026) and
        // no offsets, which would silently move the calendar date.
        if (DateTime.TryParseExact(raw, IsoDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            date = DateOnly.FromDateTime(parsed);
            return true;
        }

        return false;
    }

    private static readonly string[] IsoDateFormats = ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss"];

    /// <summary>Parses an instant written as <c>/Date(ms±zzzz)/</c>.</summary>
    public static DateTimeOffset? ParseMsInstant(string? raw)
    {
        var match = raw is null ? null : MsDateRegex().Match(raw);
        return match is { Success: true } && long.TryParse(match.Groups["ms"].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var ms)
            ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
            : null;
    }

    /// <summary>The string value of <paramref name="name"/> on <paramref name="obj"/>; <see langword="null"/> when absent, null or not a string.</summary>
    public static string? Str(JsonObject? obj, string name) =>
        obj is not null && obj.TryGetPropertyValue(name, out var node) && node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>The object value of <paramref name="name"/>; <see langword="null"/> when absent or not an object.</summary>
    public static JsonObject? Obj(JsonObject? obj, string name) =>
        obj is not null && obj.TryGetPropertyValue(name, out var node) ? node as JsonObject : null;

    /// <summary>Whether <paramref name="obj"/> has a non-null <paramref name="name"/>.</summary>
    public static bool Has(JsonObject obj, string name) => obj.TryGetPropertyValue(name, out var node) && node is not null;

    /// <summary>Reads a number that may be sent as a JSON number (or, leniently, a numeric string).</summary>
    public static bool TryNumber(JsonNode? node, out decimal value)
    {
        value = 0m;
        if (node is not JsonValue v)
            return false;
        if (v.TryGetValue<decimal>(out value))
            return true;
        if (v.TryGetValue<double>(out var d))
        {
            value = (decimal)d;
            return true;
        }

        return v.TryGetValue<string>(out var s) && decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>The value at a dotted path (<c>Contact.ContactID</c>) as comparable text; <see langword="null"/> when absent.</summary>
    public static string? TextAt(JsonObject obj, string path)
    {
        JsonNode? node = obj;
        foreach (var part in path.Split('.'))
        {
            if (node is not JsonObject current || !current.TryGetPropertyValue(part, out node))
                return null;
        }

        return node switch
        {
            null => null,
            JsonValue v when v.TryGetValue<string>(out var s) => s,
            JsonValue v when v.TryGetValue<bool>(out var b) => b ? "true" : "false",
            JsonValue v when v.TryGetValue<decimal>(out var m) => m.ToString(CultureInfo.InvariantCulture),
            _ => node.ToJsonString(),
        };
    }

    /// <summary>
    /// Compiles a <c>where=</c> clause into a predicate: clauses joined by
    /// <c>&amp;&amp;</c> or <c>AND</c>, each <c>Path==value</c>,
    /// <c>Path!=value</c>, or <c>Path.Contains("x")</c> /
    /// <c>.StartsWith("x")</c> / <c>.EndsWith("x")</c>, where a value is
    /// <c>"text"</c>, <c>Guid("…")</c>, <c>true</c>/<c>false</c>, a number,
    /// or <c>null</c>. Comparison is case-insensitive. Anything else
    /// (<c>||</c>, <c>&lt;</c>, <c>DateTime(…)</c>) is refused with
    /// <paramref name="error"/>.
    /// </summary>
    public static Func<JsonObject, bool>? CompileWhere(string where, out string? error)
    {
        error = null;
        var clauses = SplitWhere(where, out var hasOr);
        if (hasOr)
        {
            error = "OR is not supported by the simulator";
            return null;
        }

        var predicates = new List<Func<JsonObject, bool>>();
        foreach (var rawClause in clauses)
        {
            var clause = rawClause.Trim();
            while (clause.StartsWith('(') && clause.EndsWith(')'))
                clause = clause[1..^1].Trim();

            var method = MethodClauseRegex().Match(clause);
            if (method.Success)
            {
                var path = method.Groups["path"].Value;
                var arg = Unescape(method.Groups["arg"].Value);
                var fn = method.Groups["fn"].Value;
                predicates.Add(o => TextAt(o, path) is { } text && fn switch
                {
                    "Contains" => text.Contains(arg, StringComparison.OrdinalIgnoreCase),
                    "StartsWith" => text.StartsWith(arg, StringComparison.OrdinalIgnoreCase),
                    _ => text.EndsWith(arg, StringComparison.OrdinalIgnoreCase),
                });
                continue;
            }

            var comparison = ComparisonClauseRegex().Match(clause);
            if (!comparison.Success || !TryLiteral(comparison.Groups["value"].Value.Trim(), out var literal))
            {
                error = $"the clause '{clause}' is not understood";
                return null;
            }

            var fieldPath = comparison.Groups["path"].Value;
            var equal = comparison.Groups["op"].Value == "==";
            predicates.Add(o => string.Equals(TextAt(o, fieldPath), literal, StringComparison.OrdinalIgnoreCase) == equal);
        }

        return o => predicates.All(p => p(o));
    }

    /// <summary>
    /// Splits a <c>where=</c> clause on <c>&amp;&amp;</c> / <c>AND</c>
    /// outside double-quoted strings, and reports whether an <c>||</c> /
    /// <c>OR</c> appears outside them. Text inside quotes (where <c>\"</c>
    /// escapes a quote and <c>\\</c> a backslash; the literal is unescaped
    /// when the clause is read) is never split, so
    /// <c>Name=="Barnes AND Noble"</c> is one clause.
    /// </summary>
    internal static List<string> SplitWhere(string where, out bool hasOr)
    {
        hasOr = false;
        var clauses = new List<string>();
        var start = 0;
        var inQuotes = false;
        for (var i = 0; i < where.Length; i++)
        {
            var c = where[i];
            if (inQuotes)
            {
                if (c == '\\')
                    i++;
                else if (c == '"')
                    inQuotes = false;
                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
                continue;
            }

            if (c == '|' && i + 1 < where.Length && where[i + 1] == '|')
            {
                hasOr = true;
                i++;
                continue;
            }

            if (c == '&' && i + 1 < where.Length && where[i + 1] == '&')
            {
                clauses.Add(where[start..i]);
                i++;
                start = i + 1;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                var wordStart = i + 1;
                var wordEnd = wordStart;
                while (wordEnd < where.Length && char.IsAsciiLetter(where[wordEnd]))
                    wordEnd++;
                if (wordEnd < where.Length && char.IsWhiteSpace(where[wordEnd]))
                {
                    var word = where[wordStart..wordEnd];
                    if (string.Equals(word, "OR", StringComparison.OrdinalIgnoreCase))
                    {
                        hasOr = true;
                    }
                    else if (string.Equals(word, "AND", StringComparison.OrdinalIgnoreCase))
                    {
                        clauses.Add(where[start..i]);
                        start = wordEnd + 1;
                        i = wordEnd;
                    }
                }
            }
        }

        clauses.Add(where[start..]);
        return clauses;
    }

    private static bool TryLiteral(string raw, out string? literal)
    {
        literal = null;
        var guid = GuidLiteralRegex().Match(raw);
        if (guid.Success)
        {
            literal = guid.Groups["v"].Value;
            return true;
        }

        var quoted = QuotedLiteralRegex().Match(raw);
        if (quoted.Success)
        {
            literal = Unescape(quoted.Groups["v"].Value);
            return true;
        }

        if (raw is "null")
            return true;

        if (raw is "true" or "false" || decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
        {
            literal = raw;
            return true;
        }

        return false;
    }

    /// <summary>Undoes the escapes a quoted <c>where=</c> string may carry: <c>\"</c> is <c>"</c> and <c>\\</c> is <c>\</c>.</summary>
    internal static string Unescape(string quoted) => EscapeRegex().Replace(quoted, "$1");

    [GeneratedRegex(@"\\([""\\])")]
    private static partial Regex EscapeRegex();

    [GeneratedRegex(@"^""(?<v>(?:[^""\\]|\\.)*)""$")]
    private static partial Regex QuotedLiteralRegex();

    [GeneratedRegex(@"^/Date\((?<ms>-?\d+)(?<offset>[+-]\d{4})?\)/$")]
    private static partial Regex MsDateRegex();

    [GeneratedRegex(@"^(?<path>[A-Za-z][A-Za-z0-9]*(\.[A-Za-z][A-Za-z0-9]*)*)\.(?<fn>Contains|StartsWith|EndsWith)\(""(?<arg>(?:[^""\\]|\\.)*)""\)$")]
    private static partial Regex MethodClauseRegex();

    [GeneratedRegex(@"^(?<path>[A-Za-z][A-Za-z0-9]*(\.[A-Za-z][A-Za-z0-9]*)*)\s*(?<op>==|!=)\s*(?<value>.+)$")]
    private static partial Regex ComparisonClauseRegex();

    [GeneratedRegex(@"^Guid\(""(?<v>[0-9A-Fa-f-]+)""\)$")]
    private static partial Regex GuidLiteralRegex();
}
