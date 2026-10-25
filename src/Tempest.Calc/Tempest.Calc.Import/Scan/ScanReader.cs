using System.Globalization;
using System.Text;
using Tempest.Calc.Import.Cad;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Import.Scan;

/// <summary>The outcome of reading and analysing scan tables: what was read, and the statistics of every feature at every stage.</summary>
public sealed record ScanAnalysis(ScanData Data, IReadOnlyList<ScanFeatureStats> Stats, ScanProfile Profile, ScanSettings Settings);

/// <summary>
/// The entry point for scan deviation tables. A table is read through the bounded reader (5 MB, strict UTF-8, no control character) and the
/// W1 CSV reader (or the safe workbook reader for .xlsx), under the profile's own column mapping. A blank or non-numeric cell is
/// reported and never read as a number (NaN and Infinity are not numbers here); text from the file is reduced to plain text; at most
/// <c>ImportLimits.Scan</c> rows are read. Nothing but <see cref="ImportRefusedException"/> (or the caller's cancellation) leaves it.
/// </summary>
public static class ScanImporter
{
    /// <summary>The most files one import takes.</summary>
    public const int MaxFiles = 64;

    /// <summary>A value beyond this (in the profile's unit) is not a plausible scan value and is reported, not used. It also keeps the squares in the statistics from overflowing.</summary>
    public const double MaxPlausible = 1e9;

    private const int MaxStageChars = 40;
    private const int MaxSerialChars = 60;

    /// <summary>Reads <paramref name="files"/> through <paramref name="profile"/> and computes the statistics of every feature at every stage.</summary>
    public static ScanAnalysis Analyse(IReadOnlyList<ScanFileInput> files, ScanProfile profile, ScanSettings? settings = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(profile);
        settings ??= new ScanSettings();

        try
        {
            return AnalyseCore(files, profile, settings, cancellationToken);
        }
        catch (Exception e) when (e is not (ImportRefusedException or OperationCanceledException))
        {
            // The reader is written to refuse, not to fail; anything else is a defect in it, and is reported as one.
            throw new ImportRefusedException(ImportRefusalCode.Internal, $"The scan table could not be read ({e.GetType().Name}). This is a fault in the importer, not in your file.");
        }
    }

    /// <summary>Reads a single table with no per-file stage or serial.</summary>
    public static ScanAnalysis Analyse(ImportedFile file, ScanProfile profile, ScanSettings? settings = null, CancellationToken cancellationToken = default) =>
        Analyse([new ScanFileInput(file)], profile, settings, cancellationToken);

    // ------------------------------------------------------------------

    private static ScanAnalysis AnalyseCore(IReadOnlyList<ScanFileInput> files, ScanProfile profile, ScanSettings settings, CancellationToken cancellationToken)
    {
        if (profile.Validate() is { Count: > 0 } problems)
            throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, "The profile cannot be used: " + string.Join(" ", problems.Take(4)));

        if (files.Count == 0)
            throw new ImportRefusedException(ImportRefusalCode.Empty, "No scan table was given.");

        if (files.Count > MaxFiles)
            throw new ImportRefusedException(ImportRefusalCode.TooManyRows, $"At most {MaxFiles} files are imported at once.");

        if (settings.SubgroupSize is < 1 or > 1000 || settings.MinParts is < 2 or > 100_000 || !(settings.Alpha > 0 && settings.Alpha < 1))
            throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, "The subgroup size must be 1 to 1000, the minimum parts 2 to 100000 and the significance level between 0 and 1.");

        var np = settings.Number ?? profile.Number ?? NumberProfile.Point;

        if (!np.IsValid)
            throw new ImportRefusedException(ImportRefusalCode.NumberFormat, "The number format is not valid (the grouping character cannot be the decimal mark).");

        CadUnits.TryResolve(profile.Unit, out var unitSymbol, out var unit);
        var budget = new ImportBudget(settings.Limits, cancellationToken);
        var sink = new Sink(profile, settings, unit, unitSymbol);
        var fileInfos = new List<ScanFileInfo>();

        for (var i = 0; i < files.Count; i++)
        {
            budget.Check();
            var input = files[i] ?? throw new ImportRefusedException(ImportRefusalCode.Empty, "A file entry is empty.");

            if (input.File.Bytes.Length > settings.Limits.MaxBytes)
                throw new ImportRefusedException(ImportRefusalCode.TooLarge, $"A file is larger than {settings.Limits.MaxBytes / (1024.0 * 1024.0):0.#} MB and was not read.");

            var before = sink.Observations.Count;
            var rows = ReadFile(i, input, profile, settings, np, budget, sink);
            fileInfos.Add(new ScanFileInfo(input.File.FileName, input.File.Sha256, Clean(input.Stage, MaxStageChars), Clean(input.Serial, MaxSerialChars), rows, sink.Observations.Count - before));
        }

        // Group by stage and feature, in the order they first appear.
        var groups = new Dictionary<string, List<ScanObservation>>(StringComparer.Ordinal);
        var order = new List<(string Key, string Feature, string Stage)>();

        foreach (var o in sink.Observations)
        {
            var key = ScanProfile.Key(o.Feature, o.Stage, profile.IgnoreCase);

            if (!groups.TryGetValue(key, out var list))
            {
                if (groups.Count >= settings.Limits.MaxParameters)
                    throw new ImportRefusedException(ImportRefusalCode.TooManyParameters, $"The tables hold more than {settings.Limits.MaxParameters} features (counting each stage).");

                list = [];
                groups[key] = list;
                order.Add((key, o.Feature, o.Stage));
            }

            list.Add(o);
        }

        var stats = new List<ScanFeatureStats>(order.Count);

        foreach (var (key, feature, stage) in order)
        {
            budget.Check();
            stats.Add(ScanStatistics.Compute(feature, stage, groups[key], fileInfos, settings));
        }

        var notices = new List<CalcNotice>(sink.Notices);

        if (sink.Observations.Count == 0)
            notices.Add(new CalcNotice("scan-import", CalcNoticeSeverity.Block, "No usable row was found: every row was reported (see the problems) or the table has no data rows."));

        var data = new ScanData(fileInfos, sink.Observations, sink.Log.ToGroups(), notices, sink.RowsRead);
        return new ScanAnalysis(data, stats, profile, settings);
    }

    // ------------------------------------------------------------------

    private sealed class Sink
    {
        private readonly Dictionary<string, string> _interned = new(StringComparer.Ordinal);

        public Sink(ScanProfile profile, ScanSettings settings, Engine.Units.Unit unit, string unitSymbol)
        {
            Profile = profile;
            Settings = settings;
            Unit = unit;
            UnitSymbol = unitSymbol;
        }

        public ScanProfile Profile { get; }

        public ScanSettings Settings { get; }

        public Engine.Units.Unit Unit { get; }

        public string UnitSymbol { get; }

        public List<ScanObservation> Observations { get; } = [];

        public List<CalcNotice> Notices { get; } = [];

        public ProblemLog Log { get; } = new();

        public int RowsRead { get; set; }

        public string Intern(string s)
        {
            if (_interned.TryGetValue(s, out var existing))
                return existing;

            _interned[s] = s;
            return s;
        }
    }

    private sealed class ProblemLog
    {
        private const int ExamplesPerKind = 5;
        private readonly Dictionary<(ScanProblemKind, string), (int Count, List<string> Examples)> _groups = [];

        public void Add(ScanProblemKind kind, string column, int file, int line, string? feature, string message)
        {
            var key = (kind, column);

            if (!_groups.TryGetValue(key, out var group))
            {
                group = (0, []);
            }

            if (group.Examples.Count < ExamplesPerKind)
                group.Examples.Add(string.Create(CultureInfo.InvariantCulture, $"file {file + 1} line {line}{(string.IsNullOrEmpty(feature) ? string.Empty : " (" + Clip(feature) + ")")}: {message}"));

            _groups[key] = (group.Count + 1, group.Examples);
        }

        public IReadOnlyList<ScanProblemGroup> ToGroups() =>
            _groups.OrderBy(g => g.Key.Item1).ThenBy(g => g.Key.Item2, StringComparer.Ordinal)
                .Select(g => new ScanProblemGroup(g.Key.Item1, g.Key.Item2, g.Value.Count, g.Value.Examples)).ToList();
    }

    private sealed record Roles(int Feature, int Nominal, int Actual, int Deviation, int Upper, int Lower, int Serial, int Stage);

    private static int ReadFile(int fileIndex, ScanFileInput input, ScanProfile profile, ScanSettings settings, NumberProfile np, ImportBudget budget, Sink sink)
    {
        var file = input.File;
        var fileNumber = np;
        IReadOnlyList<IReadOnlyList<string>> rows;
        IReadOnlyList<int> lines;

        if (IsWorkbook(file))
        {
            // A workbook stores numbers with a decimal point whatever the profile says; text cells are read the declared way by the same rule, so a
            // comma-decimal text cell in a workbook is reported, not guessed.
            fileNumber = NumberProfile.Point;
            (rows, lines) = ReadWorkbook(file, budget, sink);
        }
        else
        {
            var text = BoundedInputReader.DecodeText(file);
            var delimiter = profile.Delimiter;

            if (delimiter == ScanDelimiter.Whitespace)
            {
                (rows, lines) = SplitWhitespace(text, budget);
            }
            else
            {
                var table = CsvReader.Read(text, DelimiterChar(delimiter, text, np), budget);
                rows = table.Rows;
                lines = table.Lines;
            }
        }

        sink.RowsRead += rows.Count;

        if (rows.Count == 0)
            throw new ImportRefusedException(ImportRefusalCode.Empty, $"{file.FileName}: the table is empty.");

        if (profile.HeaderRow > rows.Count)
            throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"{file.FileName}: the profile '{profile.Label}' expects the header on row {profile.HeaderRow}, but the table has only {rows.Count} row(s).");

        var header = rows[profile.HeaderRow - 1].Select(h => ScanText.Clean(h, 128, out _)).ToList();
        var headerWidth = header.Count;
        var roles = ResolveColumns(profile, header, file.FileName);
        var stage0 = Clean(input.Stage, MaxStageChars) ?? Clean(profile.FixedStage, MaxStageChars) ?? string.Empty;
        var serial0 = Clean(input.Serial, MaxSerialChars) ?? Clean(profile.FixedSerial, MaxSerialChars) ?? string.Empty;
        var limits = settings.Limits;
        var mm = Engine.Units.UnitCatalog.TryParse("mm", out var mmUnit, out _) ? mmUnit : null;
        var absTolerance = mm is not null && mm.Dimension == sink.Unit.Dimension ? 0.0025 * mm.Factor / sink.Unit.Factor : 1e-6;
        var deviationMode = profile.ToleranceMode == ScanToleranceMode.DeviationsFromNominal;
        var width = new[] { roles.Feature, roles.Nominal, roles.Actual, roles.Deviation, roles.Upper, roles.Lower, roles.Serial, roles.Stage }.Max() + 1;
        var canonical = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var r = profile.HeaderRow; r < rows.Count; r++)
        {
            budget.Tick();
            var row = rows[r];
            var line = lines[r];

            if (sink.Observations.Count >= limits.MaxRows)
                throw new ImportRefusedException(ImportRefusalCode.TooManyRows, $"The tables hold more than {limits.MaxRows} data rows.");

            if (row.All(static f => f.Length == 0 || string.IsNullOrWhiteSpace(f)))
            {
                sink.Log.Add(ScanProblemKind.NoFeature, "(row)", fileIndex, line, null, "an empty row");
                continue;
            }

            if (row.Count > headerWidth && row.Skip(headerWidth).Any(static f => !string.IsNullOrWhiteSpace(f)))
                throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"{file.FileName} line {line}: the row has {row.Count} fields but the header row has {headerWidth}. A number written with the other decimal mark, or a delimiter inside a value, splits a field and would shift the values; check the delimiter and the number format, or quote the field. Nothing was imported.");

            if (row.Count < width)
                sink.Log.Add(ScanProblemKind.ShortRow, "(row)", fileIndex, line, null, $"the row has {row.Count} column(s) and the profile maps up to column {width}; the missing cells are blank");

            var rawFeature = Cell(row, roles.Feature);
            var feature = ScanText.Clean(rawFeature, limits.MaxNameChars + 1, out var featureChanged);

            if (feature.Length > limits.MaxNameChars)
                throw new ImportRefusedException(ImportRefusalCode.FieldTooLong, $"{file.FileName} line {line}: a feature name is longer than {limits.MaxNameChars} characters.");

            if (featureChanged)
                sink.Log.Add(ScanProblemKind.Text, "feature", fileIndex, line, feature, "markup, a link or an invisible character was removed from the name");

            if (feature.Length == 0)
            {
                sink.Log.Add(ScanProblemKind.NoFeature, "feature", fileIndex, line, null, "the row has no feature name and was not used");
                continue;
            }

            var stage = stage0;
            var serial = serial0;

            if (roles.Stage >= 0)
            {
                var s = ScanText.Clean(Cell(row, roles.Stage), MaxStageChars + 1, out var changed);

                if (changed)
                    sink.Log.Add(ScanProblemKind.Text, "stage", fileIndex, line, feature, "markup, a link or an invisible character was removed from the stage");

                if (s.Length > MaxStageChars)
                    throw new ImportRefusedException(ImportRefusalCode.FieldTooLong, $"{file.FileName} line {line}: a stage name is longer than {MaxStageChars} characters.");

                if (s.Length > 0)
                    stage = s;
            }

            if (roles.Serial >= 0)
            {
                var s = ScanText.Clean(Cell(row, roles.Serial), MaxSerialChars + 1, out var changed);

                if (changed)
                    sink.Log.Add(ScanProblemKind.Text, "serial", fileIndex, line, feature, "markup, a link or an invisible character was removed from the serial");

                if (s.Length > MaxSerialChars)
                    throw new ImportRefusedException(ImportRefusalCode.FieldTooLong, $"{file.FileName} line {line}: a serial is longer than {MaxSerialChars} characters.");

                if (s.Length > 0)
                    serial = s;
            }

            var nominal = Number(row, roles.Nominal, "nominal", fileNumber, fileIndex, line, feature, sink);
            var actual = Number(row, roles.Actual, "actual", fileNumber, fileIndex, line, feature, sink);
            var deviation = Number(row, roles.Deviation, "deviation", fileNumber, fileIndex, line, feature, sink);
            var upper = Number(row, roles.Upper, "upper tolerance", fileNumber, fileIndex, line, feature, sink);
            var lower = Number(row, roles.Lower, "lower tolerance", fileNumber, fileIndex, line, feature, sink);

            double value;
            double? nominalEff = nominal;
            var deviationOnly = false;

            if (actual is { } a)
            {
                value = a;

                if (deviation is { } d && nominal is { } n)
                {
                    var diff = Math.Abs(a - n - d);
                    var allowed = 0.05 * Math.Max(Math.Abs(d), Math.Abs(a - n)) + absTolerance;

                    if (diff > allowed)
                        sink.Log.Add(ScanProblemKind.Inconsistent, "deviation", fileIndex, line, feature, string.Create(CultureInfo.InvariantCulture, $"actual {a:0.######} − nominal {n:0.######} = {a - n:0.######}, but the deviation column says {d:0.######}; the actual value is used"));
                }
            }
            else if (nominal is { } n2 && deviation is { } d2)
            {
                value = n2 + d2;
            }
            else if (deviation is { } d3)
            {
                value = d3;
                nominalEff = 0;
                deviationOnly = true;
            }
            else
            {
                sink.Log.Add(ScanProblemKind.NoValue, "actual", fileIndex, line, feature, "no actual value and no nominal with a deviation; the row was not used");
                continue;
            }

            if (!double.IsFinite(value) || Math.Abs(value) > MaxPlausible)
            {
                sink.Log.Add(ScanProblemKind.OutOfRange, "actual", fileIndex, line, feature, "the value is beyond any plausible scan value; the row was not used");
                continue;
            }

            double? lsl = null, usl = null;

            if (upper is not null || lower is not null)
            {
                if (deviationMode)
                {
                    if (nominalEff is null)
                    {
                        sink.Log.Add(ScanProblemKind.Tolerance, "upper tolerance", fileIndex, line, feature, "the tolerance is given as deviations from the nominal, but the row has no nominal; the row is used without limits");
                    }
                    else if (upper is < 0 || lower is > 0)
                    {
                        sink.Log.Add(ScanProblemKind.Tolerance, "lower tolerance", fileIndex, line, feature, "as deviations the upper tolerance must be 0 or more and the lower 0 or less; the row is used without limits (use absolute limits in the profile if the columns are limits)");
                    }
                    else
                    {
                        usl = upper is { } up ? nominalEff + up : null;
                        lsl = lower is { } lo ? nominalEff + lo : null;
                    }
                }
                else
                {
                    usl = upper;
                    lsl = lower;
                }

                if (lsl is { } l && usl is { } u && !(u > l))
                {
                    sink.Log.Add(ScanProblemKind.Tolerance, "upper tolerance", fileIndex, line, feature, "the upper limit is not above the lower limit; the row is used without limits");
                    lsl = usl = null;
                }
            }

            var key = ScanProfile.Normalise(feature);
            var casing = profile.IgnoreCase ? key.ToUpperInvariant() : key;

            if (!canonical.TryGetValue(casing, out var name))
            {
                name = sink.Intern(feature);
                canonical[casing] = name;
            }

            sink.Observations.Add(new ScanObservation(fileIndex, line, name, sink.Intern(stage), sink.Intern(serial), nominalEff, value, lsl, usl, deviationOnly));
        }

        return rows.Count - profile.HeaderRow;
    }

    private static double? Number(IReadOnlyList<string> row, int index, string column, NumberProfile np, int file, int line, string feature, Sink sink)
    {
        if (index < 0)
            return null;

        var text = Cell(row, index).Trim();

        if (text.Length == 0)
        {
            sink.Log.Add(ScanProblemKind.Blank, column, file, line, feature, "the cell is blank (missing, not zero)");
            return null;
        }

        if (!LocaleNumber.TryParse(text, np, out var value, out var problem))
        {
            sink.Log.Add(ScanProblemKind.NotANumber, column, file, line, feature, problem + " It is treated as missing.");
            return null;
        }

        if (Math.Abs(value) > MaxPlausible)
        {
            sink.Log.Add(ScanProblemKind.OutOfRange, column, file, line, feature, "the number is beyond any plausible scan value; it is treated as missing");
            return null;
        }

        return value;
    }

    private static string Cell(IReadOnlyList<string> row, int index) => index >= 0 && index < row.Count ? row[index] : string.Empty;

    private static string? Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var clean = ScanText.Clean(value, max + 1, out _);
        return clean.Length == 0 ? null : clean.Length > max ? clean[..max] : clean;
    }

    private static Roles ResolveColumns(ScanProfile profile, IReadOnlyList<string> header, string fileName)
    {
        var c = profile.Columns;

        int Find(string? reference, string role)
        {
            if (reference is null)
                return -1;

            if (ScanProfile.IsPosition(reference, out var position))
            {
                if (position > header.Count)
                    throw Mismatch(profile, fileName, header, $"the {role} column {reference} is beyond the {header.Count} column(s) of the header row");

                return position - 1;
            }

            var wanted = ScanProfile.Normalise(reference);
            var comparison = profile.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var found = new List<int>();

            for (var i = 0; i < header.Count; i++)
            {
                if (string.Equals(ScanProfile.Normalise(header[i]), wanted, comparison))
                    found.Add(i);
            }

            if (found.Count == 0)
                throw Mismatch(profile, fileName, header, $"it has no column '{Clip(reference)}' for the {role}");

            if (found.Count > 1)
                throw Mismatch(profile, fileName, header, $"the header '{Clip(reference)}' for the {role} appears {found.Count} times; name it by position (#{found[0] + 1}) instead");

            return found[0];
        }

        return new Roles(
            Find(c.Feature, "feature"),
            Find(c.Nominal, "nominal"),
            Find(c.Actual, "actual value"),
            Find(c.Deviation, "deviation"),
            Find(c.UpperTolerance, "upper tolerance"),
            Find(c.LowerTolerance, "lower tolerance"),
            Find(c.Serial, "serial"),
            Find(c.Stage, "stage"));
    }

    private static ImportRefusedException Mismatch(ScanProfile profile, string fileName, IReadOnlyList<string> header, string reason)
    {
        var shown = string.Join(", ", header.Take(12).Select(h => "'" + Clip(h) + "'"));
        var more = header.Count > 12 ? $" and {header.Count - 12} more" : string.Empty;
        return new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"{fileName} does not match the profile '{profile.Label}': {reason}. The header row has {shown}{more}. Nothing was imported.");
    }

    private static bool IsWorkbook(ImportedFile file) =>
        file.Bytes.Length >= 4 && file.Bytes[0] == (byte)'P' && file.Bytes[1] == (byte)'K' && file.Bytes[2] is 3 or 5 or 7 && file.Bytes[3] is 4 or 6 or 8;

    private static (IReadOnlyList<IReadOnlyList<string>> Rows, IReadOnlyList<int> Lines) ReadWorkbook(ImportedFile file, ImportBudget budget, Sink sink)
    {
        var workbook = XlsxReader.Read(file, budget);

        foreach (var n in workbook.Notices)
            sink.Notices.Add(new CalcNotice("scan-import", CalcNoticeSeverity.Warning, n));

        if (workbook.FormulaCellCount > 0)
            sink.Notices.Add(new CalcNotice("scan-import", CalcNoticeSeverity.Warning, $"{workbook.FormulaCellCount} formula cell(s) in the workbook were read as the values Excel saved with them; no formula is evaluated."));

        var sheet = CadTables.PickSheet(workbook, null);
        var limits = sink.Settings.Limits;
        var rows = new List<IReadOnlyList<string>>();
        var lines = new List<int>();

        foreach (var row in sheet.Rows)
        {
            budget.Tick();
            var width = row.Cells.Count == 0 ? 0 : row.Cells.Max(c => c.Column);

            if (width > limits.MaxColumns)
                throw new ImportRefusedException(ImportRefusalCode.TooManyColumns, $"Row {row.Number} reaches column {width}; at most {limits.MaxColumns} columns are read.");

            if (rows.Count >= limits.MaxRows)
                throw new ImportRefusedException(ImportRefusalCode.TooManyRows, $"The sheet has more than {limits.MaxRows} rows.");

            var fields = new string[width];
            Array.Fill(fields, string.Empty);

            foreach (var cell in row.Cells)
            {
                if (cell.Kind is XlsxCellKind.Empty)
                    continue;

                fields[cell.Column - 1] = cell.Kind == XlsxCellKind.Error ? string.Empty : cell.Text;
            }

            if (fields.All(static f => f.Length == 0))
                continue;

            rows.Add(fields);
            lines.Add(row.Number);
        }

        return (rows, lines);
    }

    private static (IReadOnlyList<IReadOnlyList<string>> Rows, IReadOnlyList<int> Lines) SplitWhitespace(string text, ImportBudget budget)
    {
        var limits = budget.Limits;
        var rows = new List<IReadOnlyList<string>>();
        var lines = new List<int>();

        foreach (var (number, line) in CadLines.Of(text))
        {
            budget.Tick();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (rows.Count >= limits.MaxRows)
                throw new ImportRefusedException(ImportRefusalCode.TooManyRows, $"The file has more than {limits.MaxRows} rows.");

            var fields = new List<string>();
            var sb = new StringBuilder();

            foreach (var ch in line)
            {
                if (ch is ' ' or '\t')
                {
                    if (sb.Length > 0)
                    {
                        fields.Add(sb.ToString());
                        sb.Clear();
                    }

                    continue;
                }

                if (sb.Length >= limits.MaxFieldChars)
                    throw new ImportRefusedException(ImportRefusalCode.FieldTooLong, $"A field on line {number} is longer than {limits.MaxFieldChars} characters.");

                sb.Append(ch);
            }

            if (sb.Length > 0)
                fields.Add(sb.ToString());

            if (fields.Count > limits.MaxColumns)
                throw new ImportRefusedException(ImportRefusalCode.TooManyColumns, $"Line {number} has more than {limits.MaxColumns} columns.");

            rows.Add(fields);
            lines.Add(number);
        }

        return (rows, lines);
    }

    private static char DelimiterChar(ScanDelimiter delimiter, string text, NumberProfile np) => delimiter switch
    {
        ScanDelimiter.Comma => ',',
        ScanDelimiter.Semicolon => ';',
        ScanDelimiter.Tab => '\t',
        _ => AutoDelimiter(text, np),
    };

    private static char AutoDelimiter(string text, NumberProfile np)
    {
        var found = CsvReader.DetectDelimiter(text);

        // With a decimal comma the comma cannot be the separator.
        if (found == ',' && np.Decimal == DecimalMark.Comma)
            return text.Contains('\t', StringComparison.Ordinal) && !text.Contains(';', StringComparison.Ordinal) ? '\t' : ';';

        return found;
    }

    private static string Clip(string s) => s.Length > 30 ? s[..30] + "…" : s;
}
