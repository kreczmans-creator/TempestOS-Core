using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Tempest.Calc.Import.Cad;

/// <summary>NX expression files: <c>[unit]name=rhs</c>, optional <c>(Type)</c>, <c>//</c> comments.</summary>
internal static class NxExpressionParser
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(50);
    private static readonly Regex VersionPattern = new(@"\bNX\s*([0-9][0-9A-Za-z.\-]{0,20})", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, RegexTimeout);
    private static readonly Regex PartPattern = new(@"^\s*(?:part(?:\s*name)?)\s*[:=]\s*(.{1,120})$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, RegexTimeout);

    private static readonly Regex RevisionPattern = new(@"^\s*rev(?:ision)?\s*[:=]?\s*([A-Za-z0-9.\-]{1,10})\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, RegexTimeout);

    private static readonly HashSet<string> NonScalarTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "point", "vector", "list", "array", "function", "matrix", "plane", "curve", "csys", "datum", "string_list",
    };

    public static CadParseResult Parse(CadParseContext c)
    {
        var text = BoundedInputReader.DecodeText(c.File);
        var np = c.Settings.Number;
        var header = true;
        var headerLines = 0;

        foreach (var (number, raw) in CadLines.Of(text))
        {
            c.Budget.Tick();
            var line = raw.Trim();

            if (line.Length == 0)
                continue;

            if (line.StartsWith("//", StringComparison.Ordinal))
            {
                if (header && ++headerLines <= 20)
                    ReadHeader(c, line[2..].Trim());

                continue;
            }

            header = false;
            line = StripComment(line);
            var location = $"line {number}";

            string? declaredType = null;
            string? declaredUnit = null;

            if (line.StartsWith('(') && line.IndexOf(')', StringComparison.Ordinal) is var close and > 0)
            {
                declaredType = line[1..close].Trim();
                line = line[(close + 1)..].TrimStart();
            }

            if (line.StartsWith('[') && line.IndexOf(']', StringComparison.Ordinal) is var closeUnit and > 0)
            {
                declaredUnit = line[1..closeUnit].Trim();
                line = line[(closeUnit + 1)..].TrimStart();
            }

            var equals = line.IndexOf('=', StringComparison.Ordinal);

            if (equals <= 0)
            {
                c.Warn($"{location} was not understood (expected [unit]name=value) and was skipped.");
                continue;
            }

            var rawName = line[..equals].Trim();
            var rhs = line[(equals + 1)..].Trim();

            if (rhs.Length == 0 || !IsName(rawName))
            {
                c.Warn($"{location} was not understood (expected [unit]name=value) and was skipped.");
                continue;
            }

            var name = c.CleanName(rawName, location);
            c.Add(Build(c, name, rhs, declaredType, declaredUnit, location, np));
        }

        if (c.Parameters.Count == 0)
            throw new ImportRefusedException(ImportRefusalCode.Empty, "The file holds no expressions.");

        return c.Finish(CadSystem.Nx, CadSourceFormat.NxExpressions);
    }

    internal static CadParameter Build(CadParseContext c, string name, string rhs, string? declaredType, string? declaredUnit, string location, NumberProfile np)
    {
        if (declaredType is not null && NonScalarTypes.Contains(declaredType))
            return new CadParameter(name, CadParameterKind.Expression, location, Text: CadValueParser.CleanText(rhs, c.Limits), Comment: $"({declaredType}) is not a scalar");

        var isString = string.Equals(declaredType, "string", StringComparison.OrdinalIgnoreCase);
        var value = isString
            ? new CadValue(CadParameterKind.Text, Text: rhs.Length >= 2 && rhs[0] == '"' && rhs[^1] == '"' ? rhs[1..^1] : rhs)
            : CadValueParser.Classify(rhs, np, declaredUnit);

        c.Hint(value, name);
        return ToParameter(c, name, value, location, declaredUnit);
    }

    internal static CadParameter ToParameter(CadParseContext c, string name, CadValue value, string location, string? declaredUnit = null, bool fromFormula = false, string? reported = null, string? comment = null) =>
        new(
            name, value.Kind, location,
            Number: value.Number,
            Unit: value.Unit,
            RawUnit: value.RawUnit ?? (value.Kind == CadParameterKind.Expression ? declaredUnit : null),
            UnitProblem: value.UnitProblem,
            Text: value.Text is null ? null : CadValueParser.CleanText(value.Text, c.Limits),
            Flag: value.Flag,
            Comment: comment is null ? null : CadValueParser.CleanText(comment, c.Limits),
            FromFormulaCell: fromFormula,
            Reported: reported is null ? null : CadValueParser.CleanText(reported, c.Limits));

    private static void ReadHeader(CadParseContext c, string comment)
    {
        try
        {
            if (c.FileCadVersion is null && VersionPattern.Match(comment) is { Success: true } v)
                c.FileCadVersion = "NX " + v.Groups[1].Value.TrimEnd('.', '-');

            if (c.FilePartName is null && PartPattern.Match(comment) is { Success: true } p)
                c.FilePartName = p.Groups[1].Value.Trim();

            if (c.FileRevision is null && RevisionPattern.Match(comment) is { Success: true } r)
                c.FileRevision = r.Groups[1].Value;
        }
        catch (RegexMatchTimeoutException)
        {
            // A header comment that defeats a simple pattern is not worth a refusal; the person can enter the facts.
        }
    }

    /// <summary>A name: a letter or underscore, then letters, digits, underscore, dot or colon (NX names, Creo symbols).</summary>
    internal static bool IsName(string s)
    {
        if (s.Length == 0 || s.Length > 128)
            return false;

        if (!(char.IsLetter(s[0]) || s[0] == '_'))
            return false;

        foreach (var ch in s)
        {
            if (!(char.IsLetterOrDigit(ch) || ch is '_' or '.' or ':' or '-' || CharUnicodeInfo.GetUnicodeCategory(ch) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark))
                return false;
        }

        return true;
    }

    /// <summary>Removes a trailing <c>// comment</c> that is not inside double quotes.</summary>
    internal static string StripComment(string line)
    {
        var inQuotes = false;

        for (var i = 0; i < line.Length - 1; i++)
        {
            if (line[i] == '"')
                inQuotes = !inQuotes;
            else if (!inQuotes && line[i] == '/' && line[i + 1] == '/')
                return line[..i].TrimEnd();
        }

        return line;
    }
}

/// <summary>Creo relations (<c>name = rhs</c>, <c>/* … */</c> comments, IF/ELSE/ENDIF blocks) and parameter lines (<c>NAME TYPE VALUE [unit]</c>).</summary>
internal static class CreoParser
{
    private static readonly HashSet<string> RealTypes = new(StringComparer.OrdinalIgnoreCase) { "REAL", "REAL_NUMBER", "DOUBLE", "FLOAT", "INTEGER", "INT", "NUMBER" };
    private static readonly HashSet<string> TextTypes = new(StringComparer.OrdinalIgnoreCase) { "STRING", "TEXT" };
    private static readonly HashSet<string> BoolTypes = new(StringComparer.OrdinalIgnoreCase) { "YES_NO", "BOOLEAN", "BOOL", "YESNO" };

    public static CadParseResult Parse(CadParseContext c)
    {
        var text = BoundedInputReader.DecodeText(c.File);
        var np = c.Settings.Number;
        var inBlockComment = false;
        var conditionalDepth = 0;

        foreach (var (number, raw) in CadLines.Of(text))
        {
            c.Budget.Tick();
            var line = RemoveBlockComments(raw, ref inBlockComment).Trim();
            var location = $"line {number}";

            if (line.Length == 0)
                continue;

            var upper = line.ToUpperInvariant();

            if (upper.StartsWith("IF ", StringComparison.Ordinal) || upper == "IF" || upper.StartsWith("IF(", StringComparison.Ordinal))
            {
                conditionalDepth++;
                continue;
            }

            if (upper is "ENDIF" or "END_IF")
            {
                conditionalDepth = Math.Max(0, conditionalDepth - 1);
                continue;
            }

            if (upper is "ELSE" or "ELSEIF" or "RELATIONS" or "PARAMETERS" or "END RELATIONS" or "END PARAMETERS" || upper.StartsWith("ELSE ", StringComparison.Ordinal))
                continue;

            if (upper.StartsWith("NAME ", StringComparison.Ordinal) && upper.Contains("TYPE", StringComparison.Ordinal) && upper.Contains("VALUE", StringComparison.Ordinal))
                continue;

            if (upper.StartsWith("PARAMETER NAME", StringComparison.Ordinal))
                continue;

            var equals = AssignmentIndex(line);

            if (equals > 0)
            {
                var rawName = line[..equals].Trim();
                var rhs = line[(equals + 1)..].Trim();

                if (rhs.Length == 0 || !NxExpressionParser.IsName(rawName))
                {
                    c.Warn($"{location} was not understood (expected name = value) and was skipped.");
                    continue;
                }

                var name = c.CleanName(rawName, location);

                if (conditionalDepth > 0)
                {
                    c.Add(new CadParameter(name, CadParameterKind.Expression, location, Text: CadValueParser.CleanText(rhs, c.Limits), Comment: "assigned inside an IF block, so not a fixed value"));
                    continue;
                }

                var value = CadValueParser.Classify(rhs, np);
                c.Hint(value, name);
                c.Add(NxExpressionParser.ToParameter(c, name, value, location));
                continue;
            }

            ReadParameterLine(c, line, location, np);
        }

        if (c.Parameters.Count == 0)
            throw new ImportRefusedException(ImportRefusalCode.Empty, "The file holds no relations or parameters.");

        return c.Finish(CadSystem.Creo, CadSourceFormat.CreoRelations);
    }

    private static void ReadParameterLine(CadParseContext c, string line, string location, NumberProfile np)
    {
        var tokens = Tokenise(line);

        if (tokens.Count < 3 || !NxExpressionParser.IsName(tokens[0]))
        {
            c.Warn($"{location} was not understood (expected NAME TYPE VALUE) and was skipped.");
            return;
        }

        var name = c.CleanName(tokens[0], location);
        var type = tokens[1];
        var next = 2;

        if (string.Equals(type, "REAL", StringComparison.OrdinalIgnoreCase) && next < tokens.Count && string.Equals(tokens[next], "NUMBER", StringComparison.OrdinalIgnoreCase))
            next++;

        if (next >= tokens.Count)
        {
            c.Warn($"{location} has no value and was skipped.");
            return;
        }

        var valueText = tokens[next];

        if (TextTypes.Contains(type))
        {
            c.Add(new CadParameter(name, CadParameterKind.Text, location, Text: CadValueParser.CleanText(valueText.Trim('"'), c.Limits)));
        }
        else if (BoolTypes.Contains(type))
        {
            if (CadValueParser.TryBoolean(valueText.Trim('"')) is { } flag)
                c.Add(new CadParameter(name, CadParameterKind.Boolean, location, Flag: flag));
            else
                c.Warn($"{location}: '{CadValueParser.Clip(valueText)}' is not a yes/no value; skipped.");
        }
        else if (RealTypes.Contains(type) || string.Equals(type, "REAL_NUMBER", StringComparison.OrdinalIgnoreCase))
        {
            var value = CadValueParser.Classify(valueText, np, allowBoolean: false, quotedIsText: false);

            if (value.Kind == CadParameterKind.Number && value.Unit is null && value.UnitProblem is null && next + 1 < tokens.Count && CadUnits.TryResolve(tokens[next + 1], out var symbol, out _) && symbol.Length > 0)
                value = value with { Unit = symbol };

            c.Hint(value, name);
            c.Add(NxExpressionParser.ToParameter(c, name, value, location));
        }
        else
        {
            c.Warn($"{location}: the parameter type '{CadValueParser.Clip(type)}' is not read; skipped.");
        }
    }

    /// <summary>The index of the assignment <c>=</c> (not part of <c>==</c>, <c>&lt;=</c>, <c>&gt;=</c>, <c>!=</c>), or -1.</summary>
    private static int AssignmentIndex(string line)
    {
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] != '=')
                continue;

            var before = i > 0 ? line[i - 1] : ' ';
            var after = i + 1 < line.Length ? line[i + 1] : ' ';

            if (before is '<' or '>' or '!' or '=' || after == '=')
                continue;

            return i;
        }

        return -1;
    }

    private static string RemoveBlockComments(string line, ref bool inBlock)
    {
        var sb = new StringBuilder(line.Length);
        var i = 0;

        while (i < line.Length)
        {
            if (inBlock)
            {
                var end = line.IndexOf("*/", i, StringComparison.Ordinal);

                if (end < 0)
                    return sb.ToString();

                inBlock = false;
                i = end + 2;
            }
            else if (i + 1 < line.Length && line[i] == '/' && line[i + 1] == '*')
            {
                inBlock = true;
                i += 2;
            }
            else
            {
                sb.Append(line[i]);
                i++;
            }
        }

        return sb.ToString();
    }

    private static List<string> Tokenise(string line)
    {
        var tokens = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;

        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                sb.Append(ch);
            }
            else if (!inQuotes && char.IsWhiteSpace(ch))
            {
                if (sb.Length > 0)
                {
                    tokens.Add(sb.ToString());
                    sb.Clear();
                }
            }
            else
            {
                sb.Append(ch);
            }
        }

        if (sb.Length > 0)
            tokens.Add(sb.ToString());

        return tokens;
    }
}

/// <summary>SolidWorks equations text: <c>"name"= rhs</c>, comments from an apostrophe.</summary>
internal static class SolidWorksEquationsParser
{
    public static CadParseResult Parse(CadParseContext c)
    {
        var text = BoundedInputReader.DecodeText(c.File);
        var np = c.Settings.Number;

        foreach (var (number, raw) in CadLines.Of(text))
        {
            c.Budget.Tick();
            var line = raw.Trim();
            var location = $"line {number}";

            if (line.Length == 0 || line[0] == '\'')
                continue;

            if (line[0] != '"')
            {
                c.Warn($"{location} was not understood (expected \"name\"= value) and was skipped.");
                continue;
            }

            var close = line.IndexOf('"', 1);

            if (close < 2)
            {
                c.Warn($"{location} was not understood (expected \"name\"= value) and was skipped.");
                continue;
            }

            var rawName = line[1..close];
            var rest = line[(close + 1)..].TrimStart();

            if (!rest.StartsWith('='))
            {
                c.Warn($"{location} was not understood (expected \"name\"= value) and was skipped.");
                continue;
            }

            var rhs = StripApostropheComment(rest[1..]).Trim();

            if (rhs.Length == 0)
            {
                c.Warn($"{location} has no value and was skipped.");
                continue;
            }

            var name = c.CleanName(rawName, location);
            var value = CadValueParser.Classify(rhs, np, allowBoolean: false, quotedIsText: false);
            c.Hint(value, name);
            c.Add(NxExpressionParser.ToParameter(c, name, value, location));
        }

        if (c.Parameters.Count == 0)
            throw new ImportRefusedException(ImportRefusalCode.Empty, "The file holds no equations.");

        return c.Finish(CadSystem.SolidWorks, CadSourceFormat.SolidWorksEquations);
    }

    private static string StripApostropheComment(string s)
    {
        var inQuotes = false;

        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '"')
                inQuotes = !inQuotes;
            else if (!inQuotes && s[i] == '\'')
                return s[..i];
        }

        return s;
    }
}
