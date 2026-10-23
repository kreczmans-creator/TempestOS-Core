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
            var values = JsonSerializer.Deserialize<List<CalcInputValue>>(json, Options) ?? [];

            if (values.Count > 1_000)
                return (new CalcInputSet(), [new CalcNotice("json", CalcNoticeSeverity.Block, "The file holds more than 1000 inputs.")]);

            if (values.Any(v => string.IsNullOrWhiteSpace(v.Symbol) || v.Symbol.Length > 64))
                return (new CalcInputSet(), [new CalcNotice("json", CalcNoticeSeverity.Block, "Every input needs a symbol of at most 64 characters.")]);

            return (new CalcInputSet(values), []);
        }
        catch (JsonException e)
        {
            return (new CalcInputSet(), [new CalcNotice("json", CalcNoticeSeverity.Block, $"The file is not a valid inputs document: {Truncate(e.Message)}")]);
        }
    }

    private static string Truncate(string s) => s.Length > 120 ? s[..120] + "…" : s;
}
