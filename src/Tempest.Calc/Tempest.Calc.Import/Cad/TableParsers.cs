using System.Globalization;
using System.Text.Json;

namespace Tempest.Calc.Import.Cad;

/// <summary>Shared helpers for the workbook and CSV readers.</summary>
internal static class CadTables
{
    public static XlsxSheet PickSheet(XlsxWorkbook workbook, string? wanted)
    {
        if (wanted is { Length: > 0 })
        {
            return workbook.Sheets.FirstOrDefault(s => string.Equals(s.Name, wanted, StringComparison.OrdinalIgnoreCase))
                ?? throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"The workbook has no sheet named '{CadValueParser.Clip(wanted)}'. It has: {string.Join(", ", workbook.Sheets.Select(s => s.Name).Take(10))}.");
        }

        return workbook.Sheets.FirstOrDefault(s => !s.Hidden) ?? workbook.Sheets[0];
    }

    public static string Lower(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>The value an xlsx cell holds as a parsed CAD value: numbers are stored with a decimal point, texts follow the declared number format.</summary>
    public static CadValue? FromCell(XlsxCell? cell, NumberProfile np, string? unitText, CadNonLiteral nonLiteral)
    {
        if (cell is null || cell.Kind == XlsxCellKind.Empty)
            return null;

        switch (cell.Kind)
        {
            case XlsxCellKind.Number:
            {
                var number = double.Parse(cell.Text, NumberStyles.Float, CultureInfo.InvariantCulture);
                return CadValueParser.WithUnit(number, unitText);
            }

            case XlsxCellKind.Boolean:
                return new CadValue(CadParameterKind.Boolean, Flag: cell.Text == "true");

            case XlsxCellKind.Text:
                return CadValueParser.Classify(cell.Text, np, unitText, allowBoolean: true, quotedIsText: false, nonLiteral: nonLiteral);

            default:
                return null;
        }
    }
}

/// <summary>NX expressions exported to a workbook: a header row (Name, Formula, Value, Unit, Type, Comment) and one expression per row.</summary>
internal static class NxXlsxParser
{
    private static readonly string[] NameHeaders = ["name", "expression name", "parameter", "parameter name", "variable"];
    private static readonly string[] FormulaHeaders = ["formula", "expression", "equation", "definition", "rhs", "expr"];
    private static readonly string[] ValueHeaders = ["value", "result", "current value"];
    private static readonly string[] UnitHeaders = ["unit", "units"];
    private static readonly string[] TypeHeaders = ["type"];
    private static readonly string[] CommentHeaders = ["comment", "comments", "description", "notes"];

    public static CadParseResult Parse(CadParseContext c)
    {
        var workbook = XlsxReader.Read(c.File, c.Budget);

        foreach (var n in workbook.Notices)
            c.Warn(n);

        var sheet = CadTables.PickSheet(workbook, c.Settings.SheetName);
        var np = c.Settings.Number;
        int nameCol = 1, valueCol = 2, unitCol = 3, formulaCol = 0, typeCol = 0, commentCol = 0;
        var firstRow = 0;

        foreach (var row in sheet.Rows.Take(30))
        {
            var byName = row.Cells.Where(x => x.Kind == XlsxCellKind.Text).ToDictionary(x => CadTables.Lower(x.Text), x => x.Column, StringComparer.Ordinal);
            var found = NameHeaders.Where(byName.ContainsKey).Select(h => byName[h]).ToList();

            if (found.Count == 0)
                continue;

            nameCol = found[0];
            formulaCol = FirstColumn(byName, FormulaHeaders);
            valueCol = FirstColumn(byName, ValueHeaders);
            unitCol = FirstColumn(byName, UnitHeaders);
            typeCol = FirstColumn(byName, TypeHeaders);
            commentCol = FirstColumn(byName, CommentHeaders);
            firstRow = row.Number + 1;

            if (valueCol == 0 && formulaCol == 0)
                throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, "The sheet has a Name column but no Value or Formula column.");

            break;
        }

        if (firstRow == 0)
        {
            // No header: columns are name, value, unit in that order.
            formulaCol = 0;
            c.Warn("The sheet has no header row; columns were read as name, value, unit.");
        }

        var any = false;

        foreach (var row in sheet.Rows.Where(r => r.Number >= Math.Max(firstRow, 1)))
        {
            c.Budget.Tick();

            if (row.At(nameCol) is not { Kind: XlsxCellKind.Text } nameCell || nameCell.Text.Trim().Length == 0)
                continue;

            var location = $"cell {nameCell.Reference(row.Number)}";
            var name = c.CleanName(nameCell.Text, location);
            var unitText = unitCol > 0 && row.At(unitCol) is { Kind: XlsxCellKind.Text } uc ? uc.Text.Trim() : null;
            var comment = commentCol > 0 && row.At(commentCol) is { Kind: XlsxCellKind.Text } cc ? cc.Text : null;
            var typeText = typeCol > 0 && row.At(typeCol) is { Kind: XlsxCellKind.Text } tc ? tc.Text : null;
            var formulaCell = formulaCol > 0 ? row.At(formulaCol) : null;
            var valueCell = valueCol > 0 ? row.At(valueCol) : null;

            CadValue? value;
            string? reported = null;

            if (formulaCell is { Kind: XlsxCellKind.Text or XlsxCellKind.Number } && formulaCell.Text.Trim().Length > 0)
            {
                value = CadTables.FromCell(formulaCell, np, unitText, CadNonLiteral.Expression);

                if (value is { Kind: CadParameterKind.Expression } && valueCell is { Kind: not XlsxCellKind.Empty })
                    reported = valueCell.Text;
            }
            else
            {
                value = CadTables.FromCell(valueCell, np, unitText, CadNonLiteral.Text);
            }

            if (value is null)
            {
                c.Warn($"{location}: '{CadValueParser.Clip(name)}' has no value or formula and was skipped.");
                continue;
            }

            if (typeText is not null && string.Equals(typeText.Trim(), "string", StringComparison.OrdinalIgnoreCase) && value.Kind == CadParameterKind.Expression)
            {
                var literal = (reported ?? value.Text ?? string.Empty).Trim();
                value = new CadValue(CadParameterKind.Text, Text: literal.Length >= 2 && literal[0] == '"' && literal[^1] == '"' ? literal[1..^1] : literal);
                reported = null;
            }

            c.Hint(value, name);
            var fromFormula = (valueCell?.IsFormula ?? false) || (formulaCell?.IsFormula ?? false);
            c.Add(NxExpressionParser.ToParameter(c, name, value, location, unitText, fromFormula, reported, comment));
            any = true;
        }

        if (!any)
            throw new ImportRefusedException(ImportRefusalCode.Empty, "The sheet holds no expressions.");

        if (workbook.FormulaCellCount > 0)
            c.Warn($"{workbook.FormulaCellCount} cell(s) are spreadsheet formulas. Their saved results were read; no formula was evaluated.");

        return c.Finish(CadSystem.Nx, CadSourceFormat.NxXlsx);
    }

    private static int FirstColumn(Dictionary<string, int> byName, string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (byName.TryGetValue(alias, out var col))
                return col;
        }

        return 0;
    }
}

/// <summary>SolidWorks design tables: a title cell, a header row of parameter names, and one row (or column) per configuration.</summary>
internal static class SolidWorksDesignTableParser
{
    public static CadParseResult Parse(CadParseContext c)
    {
        var workbook = XlsxReader.Read(c.File, c.Budget);

        foreach (var n in workbook.Notices)
            c.Warn(n);

        var sheet = CadTables.PickSheet(workbook, c.Settings.SheetName);
        var np = c.Settings.Number;
        var rows = sheet.Rows.ToList();

        if (rows.Count == 0)
            throw new ImportRefusedException(ImportRefusalCode.Empty, "The design table is empty.");

        var headerIndex = 0;
        const string TitlePrefix = "design table for";

        if (rows[0].At(1) is { Kind: XlsxCellKind.Text } title && CadTables.Lower(title.Text).StartsWith(TitlePrefix, StringComparison.Ordinal))
        {
            var part = title.Text[TitlePrefix.Length..].TrimStart(':', ' ').Trim();

            if (part.Length > 0)
                c.FilePartName = part;

            headerIndex = 1;
        }

        if (headerIndex >= rows.Count)
            throw new ImportRefusedException(ImportRefusalCode.Empty, "The design table has a title but no header row.");

        var inRows = c.Settings.Orientation == DesignTableOrientation.ConfigurationsInRows;
        var header = rows[headerIndex];
        var body = rows.Skip(headerIndex + 1).ToList();

        // (parameter header or name, cell) pairs for the selected configuration.
        var pairs = new List<(string Name, XlsxCell Cell, int Row)>();
        List<string> configurations;
        string selected;

        if (inRows)
        {
            var configRows = body.Where(r => r.At(1) is { Kind: XlsxCellKind.Text or XlsxCellKind.Number } cell && cell.Text.Trim().Length > 0).ToList();
            configurations = configRows.Select(r => r.At(1)!.Text.Trim()).ToList();

            if (configRows.Count == 0)
                throw new ImportRefusedException(ImportRefusalCode.Empty, "The design table has no configurations.");

            var chosen = c.Settings.Configuration is { Length: > 0 } want
                ? configRows.FirstOrDefault(r => string.Equals(r.At(1)!.Text.Trim(), want.Trim(), StringComparison.OrdinalIgnoreCase))
                : configRows[0];

            if (chosen is null)
                throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"The design table has no configuration '{CadValueParser.Clip(c.Settings.Configuration ?? string.Empty)}'. It has: {string.Join(", ", configurations.Take(10))}.");

            selected = chosen.At(1)!.Text.Trim();

            foreach (var cell in header.Cells.Where(x => x.Column >= 2 && x.Kind == XlsxCellKind.Text && x.Text.Trim().Length > 0))
            {
                if (chosen.At(cell.Column) is { } valueCell)
                    pairs.Add((cell.Text, valueCell, chosen.Number));
            }
        }
        else
        {
            var configCells = header.Cells.Where(x => x.Column >= 2 && x.Kind is XlsxCellKind.Text or XlsxCellKind.Number && x.Text.Trim().Length > 0).ToList();
            configurations = configCells.Select(x => x.Text.Trim()).ToList();

            if (configCells.Count == 0)
                throw new ImportRefusedException(ImportRefusalCode.Empty, "The design table has no configurations.");

            var chosen = c.Settings.Configuration is { Length: > 0 } want
                ? configCells.FirstOrDefault(x => string.Equals(x.Text.Trim(), want.Trim(), StringComparison.OrdinalIgnoreCase))
                : configCells[0];

            if (chosen is null)
                throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"The design table has no configuration '{CadValueParser.Clip(c.Settings.Configuration ?? string.Empty)}'. It has: {string.Join(", ", configurations.Take(10))}.");

            selected = chosen.Text.Trim();

            foreach (var row in body)
            {
                if (row.At(1) is { Kind: XlsxCellKind.Text } nameCell && nameCell.Text.Trim().Length > 0 && row.At(chosen.Column) is { } valueCell)
                    pairs.Add((nameCell.Text, valueCell, row.Number));
            }
        }

        c.Configuration = selected;
        c.Configurations = configurations.Take(100).ToList();

        foreach (var (rawName, cell, rowNumber) in pairs)
        {
            c.Budget.Tick();
            var location = $"cell {cell.Reference(rowNumber)}";
            var name = c.CleanName(rawName, location);

            if (cell.Kind == XlsxCellKind.Empty)
                continue;

            if (cell.Kind == XlsxCellKind.Error)
            {
                c.Warn($"{location}: '{CadValueParser.Clip(name)}' holds a spreadsheet error ({cell.Text}) and was skipped.");
                continue;
            }

            CadValue? value;

            if (name.StartsWith('$'))
            {
                value = new CadValue(CadParameterKind.Text, Text: cell.Text);
            }
            else if (cell.Kind == XlsxCellKind.Text && cell.Text.TrimStart().StartsWith('='))
            {
                value = new CadValue(CadParameterKind.Expression, Text: cell.Text.Trim());
            }
            else
            {
                value = CadTables.FromCell(cell, np, null, CadNonLiteral.Text);
            }

            if (value is null)
                continue;

            c.Hint(value, name);
            c.Add(NxExpressionParser.ToParameter(c, name, value, location, fromFormula: cell.IsFormula));
        }

        if (c.Parameters.Count == 0)
            throw new ImportRefusedException(ImportRefusalCode.Empty, "The design table holds no values for that configuration.");

        if (workbook.FormulaCellCount > 0)
            c.Warn($"{workbook.FormulaCellCount} cell(s) are spreadsheet formulas. Their saved results were read; no formula was evaluated.");

        return c.Finish(CadSystem.SolidWorks, CadSourceFormat.SolidWorksDesignTable);
    }
}

/// <summary>The generic CSV and JSON schema and the Onshape variable exports, which use the same tabular shape.</summary>
internal static class TabularParametersParser
{
    private static readonly string[] NameHeaders = ["name", "parameter", "variable", "param", "symbol"];
    private static readonly string[] ValueHeaders = ["value", "val"];
    private static readonly string[] UnitHeaders = ["unit", "units"];
    private static readonly string[] ExpressionHeaders = ["expression", "formula", "expr"];
    private static readonly string[] CommentHeaders = ["description", "comment", "notes"];

    public static CadParseResult ParseCsv(CadParseContext c, CadSystem system, CadSourceFormat format)
    {
        var text = BoundedInputReader.DecodeText(c.File);
        var table = CsvReader.Read(text, CsvReader.DetectDelimiter(text), c.Budget);

        if (table.Rows.Count == 0)
            throw new ImportRefusedException(ImportRefusalCode.Empty, "The CSV is empty.");

        var header = table.Rows[0].Select(h => CadTables.Lower(h)).ToList();

        if (header.Count != header.Distinct(StringComparer.Ordinal).Count(h => h.Length > 0) + header.Count(h => h.Length == 0))
            throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, "The CSV header repeats a column name.");

        var nameCol = Find(header, NameHeaders);
        var valueCol = Find(header, ValueHeaders);
        var unitCol = Find(header, UnitHeaders);
        var exprCol = Find(header, ExpressionHeaders);
        var commentCol = Find(header, CommentHeaders);
        var textCol = Find(header, ["text"]);

        if (nameCol < 0 || (valueCol < 0 && exprCol < 0 && textCol < 0))
            throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, "The CSV needs a header row with a 'name' column and a 'value' (or 'expression') column.");

        var np = c.Settings.Number;

        for (var r = 1; r < table.Rows.Count; r++)
        {
            c.Budget.Tick();
            var row = table.Rows[r];
            var location = $"line {table.Lines[r]}";
            var rawName = Cell(row, nameCol);

            if (rawName.Trim().Length == 0)
                continue;

            var name = c.CleanName(system == CadSystem.Onshape ? rawName.Trim().TrimStart('#') : rawName, location);
            var unitText = Cell(row, unitCol).Trim();
            var comment = Cell(row, commentCol);
            var valueText = Cell(row, valueCol).Trim();
            var exprText = Cell(row, exprCol).Trim();
            var plain = Cell(row, textCol);

            CadValue value;
            string? reported = null;

            if (exprText.Length > 0)
            {
                value = CadValueParser.Classify(exprText, np, unitText.Length > 0 ? unitText : null, allowBoolean: true, quotedIsText: false, nonLiteral: CadNonLiteral.Expression);

                if (value.Kind == CadParameterKind.Expression && valueText.Length > 0)
                    reported = valueText;
            }
            else if (valueText.Length > 0)
            {
                value = CadValueParser.Classify(valueText, np, unitText.Length > 0 ? unitText : null, allowBoolean: true, quotedIsText: false, nonLiteral: CadNonLiteral.Text);
            }
            else if (plain.Length > 0)
            {
                value = new CadValue(CadParameterKind.Text, Text: plain);
            }
            else
            {
                c.Warn($"{location}: '{CadValueParser.Clip(name)}' has no value and was skipped.");
                continue;
            }

            c.Hint(value, name);
            c.Add(NxExpressionParser.ToParameter(c, name, value, location, null, false, reported, comment));
        }

        if (c.Parameters.Count == 0)
            throw new ImportRefusedException(ImportRefusalCode.Empty, "The CSV holds no parameters.");

        return c.Finish(system, format);
    }

    public static CadParseResult ParseJson(CadParseContext c, CadSystem system, CadSourceFormat format)
    {
        var text = BoundedInputReader.DecodeText(c.File);
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 16, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
        }
        catch (JsonException e)
        {
            throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, "The JSON is malformed: " + CadValueParser.Clip(e.Message));
        }

        using (document)
        {
            var root = document.RootElement;
            var np = c.Settings.Number;
            var items = new List<JsonElement>();

            switch (root.ValueKind)
            {
                case JsonValueKind.Array:
                    Collect(root, items, c);
                    break;

                case JsonValueKind.Object:
                    ReadMeta(root, c);

                    if (root.TryGetProperty("parameters", out var parameters) && parameters.ValueKind == JsonValueKind.Array)
                        Collect(parameters, items, c);
                    else if (root.TryGetProperty("variables", out var variables) && variables.ValueKind == JsonValueKind.Array)
                        Collect(variables, items, c);
                    else
                        throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, "The JSON object needs a \"parameters\" (or \"variables\") list.");

                    break;

                default:
                    throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, "The JSON must be a list of parameters or an object with a \"parameters\" list.");
            }

            var index = 0;

            foreach (var item in items)
            {
                c.Budget.Tick();
                index++;
                var location = $"item {index}";

                if (item.ValueKind != JsonValueKind.Object)
                    throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"{location} is not an object.");

                var rawName = Str(item, "name");

                if (rawName is null || rawName.Trim().Length == 0)
                    throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"{location} has no \"name\".");

                var name = c.CleanName(system == CadSystem.Onshape ? rawName.Trim().TrimStart('#') : rawName, location);
                var unitText = Str(item, "unit") ?? Str(item, "units");
                var comment = Str(item, "description") ?? Str(item, "comment");
                var expression = Str(item, "expression");
                CadValue? value = null;
                string? reported = null;

                if (expression is { Length: > 0 })
                {
                    value = CadValueParser.Classify(expression, np, unitText, allowBoolean: true, quotedIsText: false, nonLiteral: CadNonLiteral.Expression);

                    if (value.Kind == CadParameterKind.Expression && item.TryGetProperty("value", out var rep) && rep.ValueKind is JsonValueKind.Number or JsonValueKind.String)
                        reported = rep.ToString();
                }
                else if (item.TryGetProperty("value", out var v))
                {
                    value = JsonValue(c, system, item, v, unitText, np, name);
                }
                else if (Str(item, "text") is { } plain)
                {
                    value = new CadValue(CadParameterKind.Text, Text: plain);
                }
                else if (item.TryGetProperty("flag", out var f) && f.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    value = new CadValue(CadParameterKind.Boolean, Flag: f.GetBoolean());
                }

                if (value is null)
                {
                    c.Warn($"{location}: '{CadValueParser.Clip(name)}' has no value and was skipped.");
                    continue;
                }

                c.Hint(value, name);
                c.Add(NxExpressionParser.ToParameter(c, name, value, location, null, false, reported, comment));
            }
        }

        if (c.Parameters.Count == 0)
            throw new ImportRefusedException(ImportRefusalCode.Empty, "The JSON holds no parameters.");

        return c.Finish(system, format);
    }

    private static CadValue? JsonValue(CadParseContext c, CadSystem system, JsonElement item, JsonElement v, string? unitText, NumberProfile np, string name)
    {
        switch (v.ValueKind)
        {
            case JsonValueKind.Number:
            {
                if (!v.TryGetDouble(out var number) || !double.IsFinite(number))
                    throw new ImportRefusedException(ImportRefusalCode.NumberFormat, $"'{CadValueParser.Clip(name)}' holds a number outside the range of a number.");

                if (unitText is { Length: > 0 })
                    return CadValueParser.WithUnit(number, unitText);

                if (system == CadSystem.Onshape && Str(item, "type") is { } type)
                {
                    // Onshape's variable API reports a value in SI base units (metres, radians) with a type; this is stated, not assumed silently.
                    var implied = type.ToUpperInvariant() switch { "LENGTH" => "m", "ANGLE" => "rad", _ => null };

                    if (implied is not null)
                    {
                        c.Warn($"{name}: the Onshape value {number.ToString("R", CultureInfo.InvariantCulture)} has no unit; it was read as {implied} from its type ({type}). Check it against the part studio.");
                        return CadValueParser.WithUnit(number, implied);
                    }
                }

                return new CadValue(CadParameterKind.Number, Number: number);
            }

            case JsonValueKind.String:
                return CadValueParser.Classify(v.GetString(), np, unitText, allowBoolean: true, quotedIsText: false, nonLiteral: CadNonLiteral.Text);

            case JsonValueKind.True:
                return new CadValue(CadParameterKind.Boolean, Flag: true);

            case JsonValueKind.False:
                return new CadValue(CadParameterKind.Boolean, Flag: false);

            default:
                return null;
        }
    }

    private static void Collect(JsonElement array, List<JsonElement> items, CadParseContext c)
    {
        foreach (var element in array.EnumerateArray())
        {
            // An Onshape export may be a list of variable tables, each with its own "variables" list.
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("variables", out var nested) && nested.ValueKind == JsonValueKind.Array && !element.TryGetProperty("name", out _))
            {
                foreach (var inner in nested.EnumerateArray())
                    Add(items, inner, c);
            }
            else
            {
                Add(items, element, c);
            }
        }
    }

    private static void Add(List<JsonElement> items, JsonElement element, CadParseContext c)
    {
        if (items.Count >= c.Limits.MaxParameters)
            throw new ImportRefusedException(ImportRefusalCode.TooManyParameters, $"The file holds more than {c.Limits.MaxParameters} parameters.");

        items.Add(element);
    }

    private static void ReadMeta(JsonElement root, CadParseContext c)
    {
        c.FileCadVersion ??= Str(root, "cad_version");
        c.FilePartName ??= Str(root, "part");
        c.FileRevision ??= Str(root, "revision");
    }

    private static string? Str(JsonElement obj, string property) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(property, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static int Find(List<string> header, string[] aliases)
    {
        foreach (var alias in aliases)
        {
            var i = header.IndexOf(alias);

            if (i >= 0)
                return i;
        }

        return -1;
    }

    private static string Cell(IReadOnlyList<string> row, int index) => index >= 0 && index < row.Count ? row[index] : string.Empty;
}
