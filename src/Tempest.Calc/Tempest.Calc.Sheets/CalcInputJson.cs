using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempest.Calc.Sheets;

/// <summary>
/// JSON exchange of an input set (worked examples, import). The document
/// is a plain list of <see cref="CalcInputValue"/>; it is read with a size
/// cap, a depth cap, and no polymorphism, so nothing in it can name a type.
/// Validation against the template happens afterwards as for any input.
/// </summary>
public static class CalcInputJson
{
    public const int MaxBytes = 2_000_000;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        MaxDepth = 8,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Write(CalcInputSet inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        return JsonSerializer.Serialize(inputs.Values.OrderBy(v => v.Symbol, StringComparer.Ordinal).ToList(), Options);
    }

    /// <summary>Reads an input set; a malformed or oversized document becomes a block notice, never an exception.</summary>
    public static (CalcInputSet Inputs, IReadOnlyList<CalcNotice> Notices) Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxBytes)
            return (new CalcInputSet(), [new CalcNotice("json", CalcNoticeSeverity.Block, $"The file is larger than {MaxBytes / 1_000_000} MB and was not read.")]);

        try
        {
            var values = JsonSerializer.Deserialize<List<CalcInputValue?>>(json, Options) ?? [];

            if (values.Count > 1_000)
                return (new CalcInputSet(), [new CalcNotice("json", CalcNoticeSeverity.Block, "The file holds more than 1000 inputs.")]);

            if (values.Any(v => v is null))
                return (new CalcInputSet(), [new CalcNotice("json", CalcNoticeSeverity.Block, "The file holds an empty (null) input entry.")]);

            if (values.Any(v => string.IsNullOrWhiteSpace(v!.Symbol) || v.Symbol.Length > 64))
                return (new CalcInputSet(), [new CalcNotice("json", CalcNoticeSeverity.Block, "Every input needs a symbol of at most 64 characters.")]);

            if (values.Any(v => v!.Rows is { } rows && rows.Any(r => r is null)))
                return (new CalcInputSet(), [new CalcNotice("json", CalcNoticeSeverity.Block, "A table holds an empty (null) row.")]);

            var duplicate = values.GroupBy(v => v!.Symbol, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);

            if (duplicate is not null)
                return (new CalcInputSet(), [new CalcNotice("json", CalcNoticeSeverity.Block, $"'{duplicate.Key}' appears {duplicate.Count()} times ({string.Join(", ", duplicate.Select(v => v!.ValueText))}); keep one.")]);

            var textOnly = values.FirstOrDefault(v => v!.Number is null && v.Text is null && v.Flag is null && v.Vector is null && v.Rows is null && !string.IsNullOrWhiteSpace(v.ValueText));

            if (textOnly is not null)
                return (new CalcInputSet(), [new CalcNotice("json", CalcNoticeSeverity.Block, $"'{textOnly.Symbol}' has only a ValueText ('{Truncate(textOnly.ValueText)}'); give the value as \"Number\" (with \"Unit\"), \"Text\", \"Flag\", \"Vector\" or \"Rows\".")]);

            return (new CalcInputSet(values.Select(v => v!).ToList()), []);
        }
        catch (JsonException e)
        {
            var reason = e.Message.Contains("could not be converted", StringComparison.Ordinal)
                ? "the file must be a JSON list of inputs, each an object with \"Symbol\" and a \"Number\" (with \"Unit\"), \"Text\", \"Flag\", \"Vector\" or \"Rows\"" + (e.Path is { Length: > 0 } ? $" (at {e.Path})" : string.Empty)
                : Truncate(e.Message);
            return (new CalcInputSet(), [new CalcNotice("json", CalcNoticeSeverity.Block, $"The file is not a valid inputs document: {reason}.")]);
        }
    }

    private static string Truncate(string s) => s.Length > 120 ? s[..120] + "…" : s;
}
