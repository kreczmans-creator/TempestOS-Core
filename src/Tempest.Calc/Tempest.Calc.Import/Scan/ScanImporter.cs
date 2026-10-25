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
        sink.Budget = budget;
        sink.AbsTolerance = Engine.Units.UnitCatalog.TryParse("mm", out var mmUnit, out _) && mmUnit.Dimension == unit.Dimension ? 0.0025 * mmUnit.Factor / unit.Factor : 1e-6;
        var fileInfos = new List<ScanFileInfo>();

        var seenHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        long totalBytes = 0;

        foreach (var f in files)
        {
            if (f is null)
                throw new ImportRefusedException(ImportRefusalCode.Empty, "A file entry is empty.");

            totalBytes += f.File.Bytes.Length;

            if (seenHashes.TryGetValue(f.File.Sha256, out var firstName))
                throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"'{f.File.FileName}' is the same file as '{firstName}' (identical SHA-256). Adding a table twice would count every part twice and hide that there are fewer parts than there seem to be; remove one.");

            seenHashes[f.File.Sha256] = f.File.FileName;
        }

        if (totalBytes > settings.MaxTotalBytes)
            throw new ImportRefusedException(ImportRefusalCode.TooLarge, $"The tables together are {totalBytes / (1024.0 * 1024.0):0.#} MB; at most {settings.MaxTotalBytes / (1024.0 * 1024.0):0.#} MB are imported at once.");

        for (var i = 0; i < files.Count; i++)
        {
            budget.Check();
            var input = files[i];

            if (input.File.Bytes.Length > settings.Limits.MaxBytes)
                throw new ImportRefusedException(ImportRefusalCode.TooLarge, $"A file is larger than {settings.Limits.MaxBytes / (1024.0 * 1024.0):0.#} MB and was not read.");

            var before = sink.Observations.Count;
            var rows = ReadFile(i, input, profile, settings, np, budget, sink);
            fileInfos.Add(new ScanFileInfo(input.File.FileName, input.File.Sha256, Clean(input.Stage, MaxStageChars), Clean(input.Serial, MaxSerialChars), rows, sink.Observations.Count - before));
        }

        // Group by stage and feature, in the order they first appear.
        var groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var order = new List<(string Key, string Feature, string Stage)>();

        for (var index = 0; index < sink.Observations.Count; index++)
        {
            var o = sink.Observations[index];
            var key = ScanProfile.Key(o.Feature, o.Stage, profile.IgnoreCase);

            if (!groups.TryGetValue(key, out var list))
            {
                if (groups.Count >= settings.Limits.MaxParameters)
                    throw new ImportRefusedException(ImportRefusalCode.TooManyParameters, $"The tables hold more than {settings.Limits.MaxParameters} features (counting each stage).");

                list = [];
                groups[key] = list;
                order.Add((key, o.Feature, o.Stage));
            }

            list.Add(index);
        }

        var stats = new List<ScanFeatureStats>(order.Count);

        foreach (var (key, feature, stage) in order)
        {
            budget.Check();
            stats.Add(ScanStatistics.Compute(feature, stage, new ObservationView(sink.Observations, groups[key]), fileInfos, settings, budget));
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

        /// <summary>The rounding allowance (0.0025 mm, in the profile's unit) when actual, nominal and deviation are compared.</summary>
        public double AbsTolerance { get; set; } = 1e-6;

        public ImportBudget Budget { get; set; } = null!;

        /// <summary>First spelling seen for each feature (compared as the profile says), and the other spellings already reported as merged into it.</summary>
        public Dictionary<string, string> Canonical { get; } = new(StringComparer.Ordinal);

        public HashSet<string> MergedSpellings { get; } = new(StringComparer.Ordinal);

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

    private sealed class FileRun
    {
        public required int Index { get; init; }

        public required ScanFileInput Input { get; init; }

        public required NumberProfile Number { get; init; }

        public required string Stage0 { get; init; }

        public required string Serial0 { get; init; }

        public int RowIndex { get; set; }

        public int HeaderWidth { get; set; }

        /// <summary>Columns whose header is blank: nothing is mapped there, and data under one is a field that was split off a neighbour.</summary>
        public List<int> BlankHeaders { get; } = [];

        public Roles? Roles { get; set; }

        public int Width { get; set; }

        public bool CommaSeparated { get; set; }

        /// <summary>The number format was stated (by the profile or the person), so the guesses about a split decimal comma are off.</summary>
        public bool Declared { get; init; }

        public HashSet<int> Mapped { get; } = [];

        /// <summary>Some row writes a number with a decimal point (the file uses the point as its decimal mark at least somewhere).</summary>
        public bool SawPoint { get; set; }

        /// <summary>The first row whose number is a whole number followed, in a column nothing is mapped to, by bare digits (a fraction without a leading zero, "1,5" split in two).</summary>
        public string? SplitSuspect { get; set; }

        public long Values { get; set; }

        public long WholeValues { get; set; }
    }

    private static int ReadFile(int fileIndex, ScanFileInput input, ScanProfile profile, ScanSettings settings, NumberProfile np, ImportBudget budget, Sink sink)
    {
        var file = input.File;
        var run = new FileRun
        {
            Index = fileIndex,
            Input = input,
            Number = np,
            Declared = settings.Number is not null || profile.Number is not null,
            Stage0 = Clean(input.Stage, MaxStageChars) ?? Clean(profile.FixedStage, MaxStageChars) ?? string.Empty,
            Serial0 = Clean(input.Serial, MaxSerialChars) ?? Clean(profile.FixedSerial, MaxSerialChars) ?? string.Empty,
        };

        // Rows are handled as they are read; the table is never held as a list of rows.
        void OnRow(IReadOnlyList<string> row, int line)
        {
            budget.Tick();
            run.RowIndex++;
            sink.RowsRead++;

            if (run.RowIndex < profile.HeaderRow)
                return;

            if (run.RowIndex == profile.HeaderRow)
            {
                var header = row.Select(h => ScanText.Clean(h, 128, out _)).ToList();
                run.HeaderWidth = header.Count;

                for (var h = 0; h < header.Count; h++)
                {
                    if (header[h].Length == 0)
                        run.BlankHeaders.Add(h);
                }

                run.Roles = ResolveColumns(profile, header, file.FileName);
                var r = run.Roles;
                run.Width = new[] { r.Feature, r.Nominal, r.Actual, r.Deviation, r.Upper, r.Lower, r.Serial, r.Stage }.Max() + 1;

                foreach (var index in new[] { r.Feature, r.Nominal, r.Actual, r.Deviation, r.Upper, r.Lower, r.Serial, r.Stage })
                {
                    if (index >= 0)
                        run.Mapped.Add(index);
                }
                return;
            }

            ProcessRow(run, row, line, profile, settings, sink);
        }

        if (IsWorkbook(file))
        {
            // A workbook stores numbers with a decimal point whatever the profile says; text cells are read the declared way by the same rule, so a
            // comma-decimal text cell in a workbook is reported, not guessed.
            var wb = new FileRun { Index = run.Index, Input = run.Input, Number = NumberProfile.Point, Declared = true, Stage0 = run.Stage0, Serial0 = run.Serial0 };
            run = wb;
            ReadWorkbook(file, budget, sink, OnRow);
        }
        else
        {
            var text = BoundedInputReader.DecodeText(file);
            var delimiter = profile.Delimiter;

            if (delimiter == ScanDelimiter.Whitespace)
            {
                SplitWhitespace(text, budget, OnRow);
            }
            else
            {
                var d = DelimiterChar(delimiter, text, np);
                run.CommaSeparated = d == ',';
                CsvReader.ReadRows(text, d, budget, OnRow);
            }
        }

        if (run.RowIndex == 0)
            throw new ImportRefusedException(ImportRefusalCode.Empty, $"{file.FileName}: the table is empty.");

        if (profile.HeaderRow > run.RowIndex)
            throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"{file.FileName}: the profile '{profile.Label}' expects the header on row {profile.HeaderRow}, but the table has only {run.RowIndex} row(s).");

        // A file that writes a point decimal in one row and, in another, a whole number followed by bare digits in an unmapped column mixes decimal marks: the second
        // is a decimal comma split in two ("1,5"). The leading-zero form is refused row by row; this one needs the whole file, so it is decided here.
        if (run.SplitSuspect is not null && run.SawPoint)
            throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"{file.FileName}: {run.SplitSuspect}, yet other rows write a decimal point, so the file mixes decimal marks. That looks like a decimal comma split by the comma delimiter. State the number format in the profile (decimal comma with a semicolon or tab delimiter, or decimal point to confirm) to import this table. Nothing was imported.");

        // A comma-separated table whose numbers are declared with a decimal point and are all whole numbers is what a decimal-comma export looks like once its
        // commas have been taken as delimiters; the column-count check catches most of these, this catches the rest.
        // Unless the number format was stated this is a block, not a hint: a person lifts it by declaring the decimal format in the profile.
        if (run.CommaSeparated && !run.Declared && np.Decimal == DecimalMark.Point && run.Values >= 2 && run.Values == run.WholeValues)
            sink.Notices.Add(new CalcNotice("scan-import", CalcNoticeSeverity.Block, $"{file.FileName}: every one of the {run.Values} values is a whole number, the delimiter is a comma and the number format was not declared (a decimal point is assumed). A decimal-comma export read this way loses its decimals. State the number format in the profile (number: decimal point, or decimal comma) to confirm what the file uses; nothing is imported until you do."));

        return run.RowIndex - profile.HeaderRow;
    }

    private static void ProcessRow(FileRun run, IReadOnlyList<string> row, int line, ScanProfile profile, ScanSettings settings, Sink sink)
    {
        var roles = run.Roles!;
        var fileIndex = run.Index;
        var file = run.Input.File;
        var limits = settings.Limits;

        if (sink.Observations.Count >= limits.MaxRows)
            throw new ImportRefusedException(ImportRefusalCode.TooManyRows, $"The tables hold more than {limits.MaxRows} data rows.");

        // A row wider than the header is refused whether or not the extra fields are blank: a decimal comma or a thousands separator splits a number into
        // extra fields, and a header that ends in a delimiter would otherwise hide it.
        if (row.Count > run.HeaderWidth)
            throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"{file.FileName} line {line}: the row has {row.Count} fields but the header row has {run.HeaderWidth}. A number written with the other decimal mark, or a delimiter inside a value, splits a field and would shift the values; check the delimiter and the number format, or quote the field. Nothing was imported.");

        foreach (var h in run.BlankHeaders)
        {
            if (h < row.Count && !string.IsNullOrWhiteSpace(row[h]))
                throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"{file.FileName} line {line}: column {h + 1} has no header but holds a value. A number written with the other decimal mark, or a delimiter inside a value, splits a field and spills the rest into an unnamed column; check the delimiter and the number format. Nothing was imported.");
        }

        // "1,012" in a decimal-comma export is "1" and "012" once the comma has been taken as the delimiter. With the number format undeclared, a whole number whose
        // right-hand neighbour is a column nothing is mapped to and that holds digits with a leading zero is that pattern, row by row: one row with a decimal point
        // elsewhere does not clear it.
        if (run.CommaSeparated && !run.Declared)
        {
            foreach (var index in new[] { roles.Nominal, roles.Actual, roles.Deviation, roles.Upper, roles.Lower })
            {
                if (index < 0 || index >= row.Count)
                    continue;

                if (row[index].Contains('.', StringComparison.Ordinal))
                    run.SawPoint = true;

                if (index + 1 < row.Count && !run.Mapped.Contains(index + 1) && IsWholeNumberText(row[index]))
                {
                    if (IsLeadingZeroDigits(row[index + 1]))
                        throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"{file.FileName} line {line}: '{row[index].Trim()}' is followed by '{row[index + 1].Trim()}' in a column nothing is mapped to. That looks like a decimal comma ('{row[index].Trim()},{row[index + 1].Trim()}') split by the comma delimiter. State the number format in the profile (decimal comma with a semicolon or tab delimiter, or decimal point to confirm) to import this table. Nothing was imported.");

                    if (run.SplitSuspect is null && IsBareDigits(row[index + 1]))
                        run.SplitSuspect = $"line {line} has '{row[index].Trim()}' followed by '{row[index + 1].Trim()}' in a column nothing is mapped to";
                }
            }
        }

        if (row.All(static f => f.Length == 0 || string.IsNullOrWhiteSpace(f)))
        {
            sink.Log.Add(ScanProblemKind.NoFeature, "(row)", fileIndex, line, null, "an empty row");
            return;
        }

        if (row.Count < run.Width)
            sink.Log.Add(ScanProblemKind.ShortRow, "(row)", fileIndex, line, null, $"the row has {row.Count} column(s) and the profile maps up to column {run.Width}; the missing cells are blank");

        var feature = ScanText.Clean(Cell(row, roles.Feature), limits.MaxNameChars + 1, out var featureChanged);

        if (feature.Length > limits.MaxNameChars)
            throw new ImportRefusedException(ImportRefusalCode.FieldTooLong, $"{file.FileName} line {line}: a feature name is longer than {limits.MaxNameChars} characters.");

        if (featureChanged)
            sink.Log.Add(ScanProblemKind.Text, "feature", fileIndex, line, feature, "markup, a link or an invisible character was removed from the name");

        if (feature.Length == 0)
        {
            sink.Log.Add(ScanProblemKind.NoFeature, "feature", fileIndex, line, null, "the row has no feature name and was not used");
            return;
        }

        var stage = run.Stage0;
        var serial = run.Serial0;

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

        var np = run.Number;
        var nominal = Number(row, roles.Nominal, "nominal", np, fileIndex, line, feature, sink);
        var actual = Number(row, roles.Actual, "actual", np, fileIndex, line, feature, sink);
        var deviation = Number(row, roles.Deviation, "deviation", np, fileIndex, line, feature, sink);
        var upper = Number(row, roles.Upper, "upper tolerance", np, fileIndex, line, feature, sink);
        var lower = Number(row, roles.Lower, "lower tolerance", np, fileIndex, line, feature, sink);

        double value;
        double? nominalEff = nominal;
        var deviationOnly = false;

        if (actual is { } a)
        {
            value = a;

            if (deviation is { } d && nominal is { } n)
            {
                var diff = Math.Abs(a - n - d);
                var allowed = 0.05 * Math.Max(Math.Abs(d), Math.Abs(a - n)) + sink.AbsTolerance;

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
            return;
        }

        if (!double.IsFinite(value) || Math.Abs(value) > MaxPlausible)
        {
            sink.Log.Add(ScanProblemKind.OutOfRange, "actual", fileIndex, line, feature, "the value is beyond any plausible scan value; the row was not used");
            return;
        }

        run.Values++;

        // "Whole" is how the value is written (no decimal mark at all), not what it equals: 50.0 shows the file does use a decimal point.
        if (IsWholeNumberText(Cell(row, actual is not null ? roles.Actual : roles.Deviation)))
            run.WholeValues++;

        var deviationMode = profile.ToleranceMode == ScanToleranceMode.DeviationsFromNominal;
        double? lsl = null, usl = null;

        if (upper is not null || lower is not null)
        {
            if (deviationOnly && !deviationMode)
            {
                sink.Log.Add(ScanProblemKind.Tolerance, "upper tolerance", fileIndex, line, feature, "the table gives only deviations, but the tolerance columns are absolute limits; the deviation cannot be judged against them, so the row is used without limits");
            }
            else if (deviationMode)
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

        if (!sink.Canonical.TryGetValue(casing, out var name))
        {
            name = sink.Intern(feature);
            sink.Canonical[casing] = name;
        }
        else if (!string.Equals(name, feature, StringComparison.Ordinal) && sink.MergedSpellings.Add(feature))
        {
            sink.Log.Add(ScanProblemKind.Text, "feature", fileIndex, line, feature, $"'{feature}' was read as the same feature as '{name}': this profile compares names without regard to case, so a hole 'D' and a shaft 'd' would be merged (set ignoreCase to false in the profile if they are different features)");
        }

        sink.Observations.Add(new ScanObservation(fileIndex, line, name, sink.Intern(stage), sink.Intern(serial), nominalEff, value, lsl, usl, deviationOnly));
    }

    private static bool IsWholeNumberText(string text)
    {
        var t = text.AsSpan().Trim();

        if (t.Length > 0 && t[0] is '+' or '-')
            t = t[1..];

        return t.Length > 0 && t.Length <= 15 && !t.ContainsAnyExceptInRange('0', '9');
    }

    private static bool IsBareDigits(string text)
    {
        var t = text.AsSpan().Trim();
        return t.Length > 0 && t.Length <= 15 && !t.ContainsAnyExceptInRange('0', '9');
    }

    private static bool IsLeadingZeroDigits(string text)
    {
        var t = text.AsSpan().Trim();
        return t.Length >= 2 && t.Length <= 15 && t[0] == '0' && !t.ContainsAnyExceptInRange('0', '9');
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

        if (!LocaleNumber.TryParse(text, np, out var value, out var problem, out var grouped))
        {
            sink.Log.Add(ScanProblemKind.NotANumber, column, file, line, feature, problem + " It is treated as missing.");
            return null;
        }

        if (grouped)
            sink.Log.Add(ScanProblemKind.Grouping, column, file, line, feature, $"'{(text.Length > 24 ? text[..24] + "…" : text)}' was read as {value.ToString("R", CultureInfo.InvariantCulture)} because the profile declares '{np.Grouping}' as the grouping mark; check that this is a thousands separator and not a decimal mark");

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

    private static void ReadWorkbook(ImportedFile file, ImportBudget budget, Sink sink, Action<IReadOnlyList<string>, int> onRow)
    {
        var workbook = XlsxReader.Read(file, budget);

        foreach (var n in workbook.Notices)
            sink.Notices.Add(new CalcNotice("scan-import", CalcNoticeSeverity.Warning, n));

        if (workbook.FormulaCellCount > 0)
            sink.Notices.Add(new CalcNotice("scan-import", CalcNoticeSeverity.Warning, $"{workbook.FormulaCellCount} formula cell(s) in the workbook were read as the values Excel saved with them; no formula is evaluated."));

        var sheet = CadTables.PickSheet(workbook, null);
        var limits = sink.Settings.Limits;
        var count = 0;

        foreach (var row in sheet.Rows)
        {
            budget.Tick();
            var cells = row.Cells.Where(static c => c.Kind != XlsxCellKind.Empty && !(c.Kind == XlsxCellKind.Text && c.Text.Length == 0)).ToList();
            var width = cells.Count == 0 ? 0 : cells.Max(c => c.Column);

            if (width > limits.MaxColumns)
                throw new ImportRefusedException(ImportRefusalCode.TooManyColumns, $"Row {row.Number} reaches column {width}; at most {limits.MaxColumns} columns are read.");

            if (cells.Count == 0)
                continue;

            if (++count > limits.MaxRows)
                throw new ImportRefusedException(ImportRefusalCode.TooManyRows, $"The sheet has more than {limits.MaxRows} rows.");

            var fields = new string[width];
            Array.Fill(fields, string.Empty);

            foreach (var cell in cells)
                fields[cell.Column - 1] = cell.Kind == XlsxCellKind.Error ? string.Empty : cell.Text;

            onRow(fields, row.Number);
        }
    }

    private static void SplitWhitespace(string text, ImportBudget budget, Action<IReadOnlyList<string>, int> onRow)
    {
        var limits = budget.Limits;
        var count = 0;

        foreach (var (number, line) in CadLines.Of(text))
        {
            budget.Tick();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (++count > limits.MaxRows)
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

            onRow(fields, number);
        }
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

    /// <summary>The observations of one feature at one stage, as indexes into the table's list: no second copy of them is made.</summary>
    private sealed class ObservationView(List<ScanObservation> all, List<int> indexes) : IReadOnlyList<ScanObservation>
    {
        public int Count => indexes.Count;

        public ScanObservation this[int index] => all[indexes[index]];

        public IEnumerator<ScanObservation> GetEnumerator()
        {
            foreach (var i in indexes)
                yield return all[i];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
