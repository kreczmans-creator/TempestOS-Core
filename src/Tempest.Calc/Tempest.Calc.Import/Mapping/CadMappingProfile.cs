using System.Text.Json;
using System.Text.Json.Serialization;
using Tempest.Calc.Import.Cad;

namespace Tempest.Calc.Import.Mapping;

/// <summary>
/// One line of a mapping profile: the CAD parameter's name, the template input it feeds, an optional unit for a CAD
/// value the file states without one, and whether the file must contain it. A target is an input symbol (<c>W</c>) or a
/// cell of an existing table row (<c>contributors[2].nominal</c>, zero-based row).
/// </summary>
public sealed record CadMappingEntry(string CadName, string Target, string? AssumedUnit = null, bool Required = false);

/// <summary>
/// A named, versioned map from CAD parameter names to template input symbols. A profile is immutable once stored; a change
/// is a new version, and every imported value records the profile id and version it was mapped with.
/// </summary>
public sealed record CadMappingProfile(
    string Id,
    int Version,
    string Title,
    IReadOnlyList<CadMappingEntry> Entries,
    CadSystem? System = null,
    string? TemplateId = null,
    bool IgnoreCase = false,
    NumberProfile? Number = null,
    string? Notes = null)
{
    public const int MaxEntries = 500;
    public const int MaxIdChars = 30;

    /// <summary>"id@version", as recorded in provenance.</summary>
    [JsonIgnore]
    public string Label => $"{Id}@{Version}";

    /// <summary>Why the profile cannot be used, one reason per line; empty when it is sound.</summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (string.IsNullOrEmpty(Id) || Id.Length > MaxIdChars || !char.IsAsciiLetterOrDigit(Id[0]) || !Id.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-') || Id.Any(char.IsAsciiLetterUpper))
            problems.Add($"The profile id must be 1 to {MaxIdChars} lower-case letters, digits, '.', '_' or '-'.");

        if (Version is < 1 or > 999_999)
            problems.Add("The profile version must be a whole number from 1 to 999999.");

        if (string.IsNullOrWhiteSpace(Title) || Title.Length > 120)
            problems.Add("The profile needs a title of at most 120 characters.");

        if (Notes is { Length: > 2000 })
            problems.Add("The profile notes are at most 2000 characters.");

        if (Entries is null || Entries.Count == 0)
            problems.Add("The profile maps nothing.");
        else if (Entries.Count > MaxEntries)
            problems.Add($"The profile maps more than {MaxEntries} names.");

        if (Number is { IsValid: false })
            problems.Add("The profile's number format is not valid (the grouping character cannot be the decimal mark).");

        if (Entries is null)
            return problems;

        var names = new HashSet<string>(IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var targets = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in Entries)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.CadName) || entry.CadName.Length > 128)
            {
                problems.Add("Every entry needs a CAD name of at most 128 characters.");
                continue;
            }

            var name = Normalise(entry.CadName);

            if (!names.Add(name))
                problems.Add($"'{Clip(entry.CadName)}' appears twice.");

            if (!CadTarget.TryParse(entry.Target, out var target))
            {
                problems.Add($"'{Clip(entry.CadName)}': the target '{Clip(entry.Target ?? string.Empty)}' is not an input symbol or a table cell like contributors[0].nominal.");
                continue;
            }

            if (!targets.Add(target.ToString()))
                problems.Add($"More than one name is mapped to '{target}'; an input takes its value from one CAD parameter.");

            if (entry.AssumedUnit is { Length: > 0 } unit && !CadUnits.TryResolve(unit, out _, out _))
                problems.Add($"'{Clip(entry.CadName)}': the assumed unit '{Clip(unit)}' is not a unit the engine knows.");
        }

        return problems;
    }

    /// <summary>The name in the form profiles and files are compared in: Unicode-normalised, trimmed.</summary>
    public static string Normalise(string name)
    {
        try
        {
            return (name ?? string.Empty).Trim().Normalize(global::System.Text.NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            return (name ?? string.Empty).Trim();
        }
    }

    private static string Clip(string s) => s.Length > 40 ? s[..40] + "…" : s;
}

/// <summary>A parsed mapping target: an input symbol, or a cell of a table input.</summary>
public readonly record struct CadTarget(string Symbol, int? Row = null, string? Column = null)
{
    public bool IsCell => Row is not null;

    public override string ToString() => IsCell ? $"{Symbol}[{Row}].{Column}" : Symbol;

    public static bool TryParse(string? text, out CadTarget target)
    {
        target = default;

        if (string.IsNullOrEmpty(text) || text.Length > 140)
            return false;

        var i = 0;

        if (!ReadIdentifier(text, ref i, out var symbol))
            return false;

        if (i == text.Length)
        {
            target = new CadTarget(symbol);
            return true;
        }

        if (text[i] != '[')
            return false;

        i++;
        var digits = i;

        while (i < text.Length && text[i] is >= '0' and <= '9')
            i++;

        if (i == digits || i - digits > 4 || i >= text.Length || text[i] != ']' || (text[digits] == '0' && i - digits > 1))
            return false;

        var row = int.Parse(text.AsSpan(digits, i - digits), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture);
        i++;

        if (i >= text.Length || text[i] != '.')
            return false;

        i++;

        if (!ReadIdentifier(text, ref i, out var column) || i != text.Length)
            return false;

        target = new CadTarget(symbol, row, column);
        return true;
    }

    private static bool ReadIdentifier(string text, ref int i, out string identifier)
    {
        var start = i;
        identifier = string.Empty;

        if (i >= text.Length || !(char.IsAsciiLetter(text[i]) || text[i] == '_'))
            return false;

        while (i < text.Length && (char.IsAsciiLetterOrDigit(text[i]) || text[i] == '_'))
            i++;

        if (i - start > 64)
            return false;

        identifier = text[start..i];
        return true;
    }
}

/// <summary>Profile JSON: read with a size cap, a depth cap and no polymorphism; validated after reading.</summary>
public static class CadMappingProfileJson
{
    public const int MaxBytes = 200_000;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        MaxDepth = 8,
        AllowDuplicateProperties = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Write(CadMappingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return JsonSerializer.Serialize(profile, Options);
    }

    /// <summary>Reads and validates a profile; a malformed or unsound one is refused with the reasons.</summary>
    public static CadMappingProfile Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxBytes)
            throw new ImportRefusedException(ImportRefusalCode.TooLarge, $"A mapping profile is at most {MaxBytes / 1000} KB.");

        json = json.TrimStart('\uFEFF');
        BoundedInputReader.RejectControlCharacters(json);
        CadMappingProfile? profile;

        try
        {
            profile = JsonSerializer.Deserialize<CadMappingProfile>(json, Options);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            // A lone surrogate in the text surfaces as InvalidOperationException from the serializer, not as JsonException.
            throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, "The profile is not valid: " + (e.Message.Length > 120 ? e.Message[..120] + "…" : e.Message));
        }

        if (profile is null)
            throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, "The profile is empty.");

        var problems = profile.Validate();

        if (problems.Count > 0)
            throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, "The profile cannot be used: " + string.Join(" ", problems));

        return profile;
    }
}
