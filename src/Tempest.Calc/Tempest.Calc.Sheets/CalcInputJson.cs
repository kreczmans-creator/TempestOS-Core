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
        Converters = { new JsonStringEnumConverter(), new LenientFlagConverter() },
    };

    /// <summary>A yes/no may be written as a JSON true or false, or as the text yes, no, true, false, 1 or 0 in any case.</summary>
    private sealed class LenientFlagConverter : JsonConverter<bool?>
    {
        public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.True:
                    return true;
                case JsonTokenType.False:
                    return false;
                case JsonTokenType.Null:
                    return null;
                case JsonTokenType.String when CalcCsv.ParseFlag(reader.GetString() ?? string.Empty) is { } flag:
                    return flag;
                default:
                    throw new JsonException("a yes/no value must be true, false, yes or no");
            }
        }

        public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
        {
            if (value is { } flag)
                writer.WriteBooleanValue(flag);
            else
                writer.WriteNullValue();
        }
    }

    public static string Write(CalcInputSet inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        return JsonSerializer.Serialize(inputs.Values.OrderBy(v => v.Symbol, StringComparer.Ordinal).ToList(), Options);
    }

    /// <summary>Reads an input set; a malformed or oversized document becomes a block notice, never an exception.</summary>
    public static (CalcInputSet Inputs, IReadOnlyList<CalcNotice> Notices) Read(string json) => Read(json, null);

    /// <summary>
    /// Reads an input set for <paramref name="template"/>. A scalar the export withheld (a licensed record) is refused with a
    /// message to apply the record again; it is never read as a missing value that a default would fill in.
    /// </summary>
    public static (CalcInputSet Inputs, IReadOnlyList<CalcNotice> Notices) Read(string json, CalcTemplate? template)
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

            // C-2: a symbol an input had before it was renamed is read as the input it has become (an old inputs.json still loads).
            if (template is not null)
            {
                if (!CalcInputAliases.TryCanonicalise(template, values.Select(v => v!).ToList(), out var canonical, out var aliasConflict))
                    return (new CalcInputSet(), [new CalcNotice("json", CalcNoticeSeverity.Block, aliasConflict!)]);

                values = [.. canonical];
            }

            var duplicate = values.GroupBy(v => v!.Symbol, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);

            if (duplicate is not null)
                return (new CalcInputSet(), [new CalcNotice("json", CalcNoticeSeverity.Block, $"'{duplicate.Key}' appears {duplicate.Count()} times ({string.Join(", ", duplicate.Select(v => v!.ValueText))}); keep one.")]);

            var withheld = template is null
                ? []
                : values.Where(v => v!.Number is null && v.Text is null && v.Flag is null && v.Vector is null && v.Rows is null && CalcCsv.IsWithheld(v.Provenance) && template.FindInput(v.Symbol) is { Kind: not CalcInputKind.Table }).ToList();

            if (withheld.Count > 0)
                return (new CalcInputSet(), withheld.Select(v => new CalcNotice("json", CalcNoticeSeverity.Block, CalcCsv.WithheldMessage(v!.Symbol))).ToList());

            var textOnly = values.FirstOrDefault(v => v!.Number is null && v.Text is null && v.Flag is null && v.Vector is null && v.Rows is null && !CalcCsv.IsWithheld(v.Provenance) && !string.IsNullOrWhiteSpace(v.ValueText));

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
