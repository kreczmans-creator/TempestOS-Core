using System.Text.Json;
using System.Text.Json.Serialization;
using Tempest.Calc.Import.Cad;

namespace Tempest.Calc.Import.Scan;

/// <summary>
/// One line of a scan profile: the feature (the name the table gives it), where its fitted distribution goes, which stage it is
/// read at when the table has several, and whether the table must hold it. The target is one of
/// <list type="bullet">
/// <item><description><c>D</c> or <c>D:sigma</c> — a single input takes a statistic (mean by default; sigma, nominal, min, max);</description></item>
/// <item><description><c>contributors[2]</c> — an existing row of a tolerance-stack table (zero-based) takes the fitted normal: nominal, mean shift, ± kσ band and the normal distribution;</description></item>
/// <item><description><c>measured{D_g}</c> — the row of a measured-dimension table whose <c>dim</c> is D_g takes mean and σ (the row is added if there is none).</description></item>
/// </list>
/// </summary>
public sealed record ScanFeatureEntry(string Feature, string Target, string? Stage = null, bool Required = false);

/// <summary>
/// A named, versioned map from a scan table's columns to quantities, and from its features to inputs of a calculation. The columns are
/// always the profile's own (a scan software's export is user-configurable, so no layout is assumed). A profile is immutable once
/// stored; a change is a new version, and every fitted value records the profile id and version it was made with.
/// </summary>
public sealed record ScanProfile(
    string Id,
    int Version,
    string Title,
    ScanColumns Columns,
    IReadOnlyList<ScanFeatureEntry> Features,
    ScanSoftware Software = ScanSoftware.Generic,
    string Unit = "mm",
    ScanDelimiter Delimiter = ScanDelimiter.Auto,
    NumberProfile? Number = null,
    int HeaderRow = 1,
    ScanToleranceMode ToleranceMode = ScanToleranceMode.DeviationsFromNominal,
    bool IgnoreCase = true,
    string? FixedStage = null,
    string? FixedSerial = null,
    string? TemplateId = null,
    string? Notes = null)
{
    public const int MaxFeatures = 500;
    public const int MaxIdChars = 30;
    public const int MaxHeaderRow = 1000;
    public const int MaxColumnIndex = 256;

    /// <summary>"id@version", as recorded in provenance.</summary>
    [JsonIgnore]
    public string Label => $"{Id}@{Version}";

    /// <summary>The software as named in provenance.</summary>
    [JsonIgnore]
    public string SoftwareLabel => SoftwareName(Software);

    public static string SoftwareName(ScanSoftware software) => software switch
    {
        ScanSoftware.ZeissInspect => "ZEISS Inspect",
        ScanSoftware.GomInspect => "GOM Inspect",
        ScanSoftware.PolyWorks => "PolyWorks",
        ScanSoftware.GeomagicControlX => "Geomagic Control X",
        ScanSoftware.MultiStageCsv => "multi-stage CSV",
        _ => "scan table",
    };

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

        if (!Enum.IsDefined(Software) || !Enum.IsDefined(Delimiter) || !Enum.IsDefined(ToleranceMode))
            problems.Add("The profile has a software, delimiter or tolerance mode that is not one of the listed values.");

        if (Number is { IsValid: false })
            problems.Add("The profile's number format is not valid (the grouping character cannot be the decimal mark).");

        if (HeaderRow is < 1 or > MaxHeaderRow)
            problems.Add($"The header row must be a line number from 1 to {MaxHeaderRow}.");

        if (FixedStage is { Length: > 40 } || FixedSerial is { Length: > 60 })
            problems.Add("A fixed stage is at most 40 characters and a fixed serial at most 60.");

        if (TemplateId is { Length: > 100 })
            problems.Add("The template id is at most 100 characters.");

        if (!CadUnits.TryResolve(Unit, out var unitSymbol, out var unit) || unitSymbol.Length == 0)
            problems.Add($"The unit '{Clip(Unit ?? string.Empty)}' is not a unit the engine knows.");
        else if (unit.IsAffine || unit.Dimension.IsDimensionless && !CadUnits.IsAngle(unitSymbol))
            problems.Add($"The unit '{Clip(Unit ?? string.Empty)}' is not a plain scale unit (a length, an angle, a force …); a temperature scale or a bare number cannot carry a deviation.");

        if (new[] { Id, Title, Notes?.Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' '), FixedStage, FixedSerial, TemplateId, Unit }.Concat(Columns?.Set().Select(c => c.Reference) ?? []).Concat((Features ?? []).SelectMany(f => new[] { f?.Feature, f?.Target, f?.Stage })).Any(HasInvisible))
            problems.Add("The profile holds a control, formatting or invisible character (such as a zero-width space or a right-to-left override) in a text; remove it.");

        if (Columns is null)
        {
            problems.Add("The profile maps no columns.");
            return problems;
        }

        if (string.IsNullOrWhiteSpace(Columns.Feature))
            problems.Add("The profile must say which column holds the feature name.");

        if (string.IsNullOrWhiteSpace(Columns.Actual) && string.IsNullOrWhiteSpace(Columns.Deviation))
            problems.Add("The profile must say which column holds the actual value, or the deviation.");

        var seenColumns = new HashSet<string>(IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        foreach (var (role, reference) in Columns.Set())
        {
            if (string.IsNullOrWhiteSpace(reference) || reference.Length > 128)
            {
                problems.Add($"The {role} column needs a header text or a position like #3 (at most 128 characters).");
                continue;
            }

            if (IsPosition(reference, out var position) && position is < 1 or > MaxColumnIndex)
                problems.Add($"The {role} column position must be from #1 to #{MaxColumnIndex}.");

            if (!seenColumns.Add(ScanProfile.Normalise(reference)))
                problems.Add($"The {role} column '{Clip(reference)}' is also used for another quantity.");
        }

        if (Features is null)
        {
            problems.Add("The profile has no feature list (an empty list is allowed).");
            return problems;
        }

        if (Features.Count > MaxFeatures)
            problems.Add($"The profile maps more than {MaxFeatures} features.");

        var names = new HashSet<string>(StringComparer.Ordinal);
        var targets = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in Features)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.Feature) || entry.Feature.Length > 128)
            {
                problems.Add("Every feature needs a name of at most 128 characters.");
                continue;
            }

            if (entry.Stage is { Length: > 40 })
                problems.Add($"'{Clip(entry.Feature)}': a stage is at most 40 characters.");

            var key = Key(entry.Feature, entry.Stage, IgnoreCase);

            if (!names.Add(key))
                problems.Add($"'{Clip(entry.Feature)}' appears twice{(entry.Stage is null ? string.Empty : " for the stage '" + Clip(entry.Stage) + "'")}.");

            if (!ScanTarget.TryParse(entry.Target, out var target))
            {
                problems.Add($"'{Clip(entry.Feature)}': the target '{Clip(entry.Target ?? string.Empty)}' is not an input symbol (D, D:sigma), a stack row (contributors[2]) or a measured dimension (measured{{D_g}}).");
                continue;
            }

            if (!targets.Add(target.ToString()))
                problems.Add($"More than one feature is mapped to '{target}'; an input takes its value from one feature.");
        }

        return problems;
    }

    private static bool HasInvisible(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        foreach (var c in text)
        {
            if (char.IsControl(c))
                return true;

            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);

            if (category is System.Globalization.UnicodeCategory.Format or System.Globalization.UnicodeCategory.LineSeparator or System.Globalization.UnicodeCategory.ParagraphSeparator or System.Globalization.UnicodeCategory.PrivateUse or System.Globalization.UnicodeCategory.OtherNotAssigned)
                return true;
        }

        return false;
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

    internal static string Key(string feature, string? stage, bool ignoreCase)
    {
        var f = Normalise(feature);
        var s = stage is null ? string.Empty : Normalise(stage);
        return (ignoreCase ? f.ToUpperInvariant() : f) + "\u0001" + (ignoreCase ? s.ToUpperInvariant() : s);
    }

    /// <summary>Whether a column reference is a position like <c>#3</c>.</summary>
    public static bool IsPosition(string reference, out int position)
    {
        position = 0;
        return reference.Length is >= 2 and <= 4 && reference[0] == '#' && reference[1] != '0' && int.TryParse(reference.AsSpan(1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out position);
    }

    private static string Clip(string s) => s.Length > 40 ? s[..40] + "…" : s;
}

/// <summary>What a mapped target is.</summary>
public enum ScanTargetKind
{
    /// <summary>A single input takes a statistic.</summary>
    Scalar,

    /// <summary>An existing row of a tolerance-stack table takes the fitted normal.</summary>
    StackRow,

    /// <summary>The row of a measured-dimension table for a key takes mean and σ.</summary>
    MeasuredKey,
}

/// <summary>A parsed profile target.</summary>
public readonly record struct ScanTarget(string Symbol, ScanTargetKind Kind, int? Row = null, string? Key = null, ScanStat Stat = ScanStat.Mean)
{
    public override string ToString() => Kind switch
    {
        ScanTargetKind.StackRow => $"{Symbol}[{Row}]",
        ScanTargetKind.MeasuredKey => $"{Symbol}{{{Key}}}",
        _ => Stat == ScanStat.Mean ? Symbol : $"{Symbol}:{Stat.ToString().ToLowerInvariant()}",
    };

    public static bool TryParse(string? text, out ScanTarget target)
    {
        target = default;

        if (string.IsNullOrEmpty(text) || text.Length > 140)
            return false;

        var i = 0;

        if (!ReadIdentifier(text, ref i, out var symbol))
            return false;

        if (i == text.Length)
        {
            target = new ScanTarget(symbol, ScanTargetKind.Scalar);
            return true;
        }

        switch (text[i])
        {
            case ':':
            {
                ScanStat? stat = text[(i + 1)..].ToLowerInvariant() switch
                {
                    "mean" => ScanStat.Mean,
                    "sigma" => ScanStat.Sigma,
                    "nominal" => ScanStat.Nominal,
                    "min" => ScanStat.Min,
                    "max" => ScanStat.Max,
                    _ => null,
                };

                if (stat is null)
                    return false;

                target = new ScanTarget(symbol, ScanTargetKind.Scalar, Stat: stat.Value);
                return true;
            }

            case '[':
            {
                i++;
                var digits = i;

                while (i < text.Length && text[i] is >= '0' and <= '9')
                    i++;

                if (i == digits || i - digits > 4 || i != text.Length - 1 || text[i] != ']')
                    return false;

                var row = int.Parse(text.AsSpan(digits, i - digits), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture);
                target = new ScanTarget(symbol, ScanTargetKind.StackRow, Row: row);
                return true;
            }

            case '{':
            {
                i++;
                var close = text.IndexOf('}', i);

                if (close != text.Length - 1 || close == i)
                    return false;

                var key = text[i..close];

                if (key.Length > 64 || !key.All(static c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '-'))
                    return false;

                target = new ScanTarget(symbol, ScanTargetKind.MeasuredKey, Key: key);
                return true;
            }

            default:
                return false;
        }
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
public static class ScanProfileJson
{
    public const int MaxBytes = 200_000;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        MaxDepth = 8,
        AllowDuplicateProperties = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Write(ScanProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return JsonSerializer.Serialize(profile, Options);
    }

    /// <summary>Reads and validates a profile; a malformed or unsound one is refused with the reasons.</summary>
    public static ScanProfile Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (json.Length > 0 && json[0] == '\uFEFF')
            json = json[1..];

        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxBytes)
            throw new ImportRefusedException(ImportRefusalCode.TooLarge, $"A scan profile is at most {MaxBytes / 1000} KB.");

        BoundedInputReader.RejectControlCharacters(json);
        ScanProfile? profile;

        try
        {
            profile = JsonSerializer.Deserialize<ScanProfile>(json, Options);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
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

/// <summary>
/// Header spellings the usual inspection and scan-comparison exports use, offered when a new profile is started so the person does not
/// type every column. <b>Unconfirmed:</b> the spellings are the documented shape of those exports as the builder knows it, not checked against a
/// real export from each product (the owner checks them in the test pack). The profile that results is still the person's own, and a header that
/// is not found is refused, never guessed.
/// </summary>
public static class ScanColumnSuggestions
{
    private static readonly string[] Feature = ["feature", "name", "element", "label", "characteristic", "measurement", "id", "point", "check"];
    private static readonly string[] Nominal = ["nominal", "nom", "nominal value", "target", "reference", "ref", "soll"];
    private static readonly string[] Actual = ["actual", "measured", "meas", "measured value", "act", "actual value", "ist"];
    private static readonly string[] Deviation = ["deviation", "dev", "dev.", "diff", "error", "abweichung"];
    private static readonly string[] Upper = ["upper tol", "upper tol.", "upper tolerance", "tol+", "+tol", "upper", "usl", "upper limit", "tol upper", "max tol"];
    private static readonly string[] Lower = ["lower tol", "lower tol.", "lower tolerance", "tol-", "-tol", "lower", "lsl", "lower limit", "tol lower", "min tol"];
    private static readonly string[] Serial = ["serial", "serial number", "part", "part id", "part number", "sn", "serial no", "specimen", "sample"];
    private static readonly string[] Stage = ["stage", "step", "operation", "op", "process stage", "phase"];

    /// <summary>
    /// Proposes columns from a header row: each role takes the first header whose normalised text equals one of its spellings.
    /// <see langword="null"/> when the feature column or both of actual and deviation are not found.
    /// </summary>
    public static ScanColumns? Suggest(IReadOnlyList<string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        string? Find(string[] spellings)
        {
            foreach (var header in headers)
            {
                var norm = header.Trim().ToLowerInvariant();

                if (spellings.Contains(norm, StringComparer.Ordinal))
                    return header.Trim();
            }

            return null;
        }

        var feature = Find(Feature);
        var actual = Find(Actual);
        var deviation = Find(Deviation);

        if (feature is null || (actual is null && deviation is null))
            return null;

        return new ScanColumns(feature, Find(Nominal), actual, deviation, Find(Upper), Find(Lower), Find(Serial), Find(Stage));
    }
}
