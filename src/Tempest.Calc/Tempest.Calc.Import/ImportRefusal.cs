using System.Diagnostics;

namespace Tempest.Calc.Import;

/// <summary>Why an untrusted file was refused. The code is stable; the message is for the person.</summary>
public enum ImportRefusalCode
{
    /// <summary>Larger than the byte cap, or an archive that expands past its caps.</summary>
    TooLarge,

    /// <summary>Reading or parsing ran past the time limit.</summary>
    Timeout,

    /// <summary>Not valid UTF-8 (or a UTF-16/32 file, which is not read).</summary>
    InvalidEncoding,

    /// <summary>A control character other than tab, carriage return or line feed.</summary>
    ControlCharacter,

    /// <summary>The file is not the structure its format requires.</summary>
    MalformedStructure,

    /// <summary>More rows than the row cap.</summary>
    TooManyRows,

    /// <summary>More columns than the column cap.</summary>
    TooManyColumns,

    /// <summary>A field, name or text longer than its cap.</summary>
    FieldTooLong,

    /// <summary>A path that traverses, reaches a share or device, or leaves its root.</summary>
    UnsafePath,

    /// <summary>A format or feature this reader does not read (legacy .xls, encrypted workbook, UTF-16).</summary>
    Unsupported,

    /// <summary>An archive whose entries expand far beyond their compressed size.</summary>
    ZipBomb,

    /// <summary>A workbook that carries macros.</summary>
    Macros,

    /// <summary>A number that is not written the way the declared number format writes numbers.</summary>
    NumberFormat,

    /// <summary>More parameters than the parameter cap.</summary>
    TooManyParameters,

    /// <summary>Nothing to import.</summary>
    Empty,

    /// <summary>The file does not exist.</summary>
    NotFound,

    /// <summary>A defect in the importer, not in the file. The fuzz tests assert this never happens.</summary>
    Internal,
}

/// <summary>
/// The one exception the import primitives and parsers throw for a file they will not read. Anything else escaping
/// them is a defect; the fuzz tests hold that line. The caller's own cancellation is not a refusal and surfaces as
/// <see cref="OperationCanceledException"/>.
/// </summary>
public sealed class ImportRefusedException : Exception
{
    public ImportRefusedException(ImportRefusalCode code, string message)
        : base(message) => Code = code;

    public ImportRefusedException() => Code = ImportRefusalCode.MalformedStructure;

    public ImportRefusedException(string message)
        : base(message) => Code = ImportRefusalCode.MalformedStructure;

    public ImportRefusedException(string message, Exception innerException)
        : base(message, innerException) => Code = ImportRefusalCode.MalformedStructure;

    public ImportRefusalCode Code { get; }
}

/// <summary>The caps every import runs under. The defaults are the W1 values; W2 raises the row cap for scan tables.</summary>
public sealed record ImportLimits
{
    /// <summary>The input byte cap (Addendum B §B6 W1: 5 MB).</summary>
    public const int DefaultMaxBytes = 5 * 1024 * 1024;

    /// <summary>The W1 limits.</summary>
    public static ImportLimits Default { get; } = new();

    /// <summary>The scan-import limits (Addendum B §B6 W2: row cap 1e6). Still 5 MB of input.</summary>
    public static ImportLimits Scan { get; } = new() { MaxRows = 1_000_000 };

    public int MaxBytes { get; init; } = DefaultMaxBytes;

    public int MaxRows { get; init; } = 100_000;

    public int MaxColumns { get; init; } = 256;

    /// <summary>The longest cell, field, name or text value in characters.</summary>
    public int MaxFieldChars { get; init; } = 4096;

    public int MaxNameChars { get; init; } = 128;

    public int MaxParameters { get; init; } = 5000;

    /// <summary>Wall-clock budget for one read and parse.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

    public int MaxZipEntries { get; init; } = 256;

    /// <summary>Total expanded size of every entry of a workbook.</summary>
    public long MaxZipUncompressedBytes { get; init; } = 64L * 1024 * 1024;

    /// <summary>Expanded size of one entry.</summary>
    public long MaxZipEntryBytes { get; init; } = 32L * 1024 * 1024;

    /// <summary>The largest expanded:compressed ratio an entry (or the whole archive) may have once it expands past <see cref="ZipRatioFloorBytes"/>.</summary>
    public double MaxZipRatio { get; init; } = 100;

    /// <summary>Entries smaller than this expanded size are exempt from the ratio test: they cannot hurt, and small repetitive sheets compress well.</summary>
    public long ZipRatioFloorBytes { get; init; } = 1024 * 1024;

    public int MaxSharedStrings { get; init; } = 200_000;

    public int MaxCells { get; init; } = 1_000_000;
}

/// <summary>
/// A running time and cancellation budget for one import. Parsers call <see cref="Check"/> in their loops; it throws the
/// caller's <see cref="OperationCanceledException"/> when the caller cancelled and <see cref="ImportRefusedException"/>
/// (<see cref="ImportRefusalCode.Timeout"/>) when the time limit passed.
/// </summary>
public sealed class ImportBudget
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly CancellationToken _token;
    private int _ticks;

    public ImportBudget(ImportLimits? limits = null, CancellationToken cancellationToken = default)
    {
        Limits = limits ?? ImportLimits.Default;
        _token = cancellationToken;
    }

    public ImportLimits Limits { get; }

    /// <summary>Throws when cancelled or out of time.</summary>
    public void Check()
    {
        _token.ThrowIfCancellationRequested();

        if (_clock.Elapsed > Limits.Timeout)
            throw new ImportRefusedException(ImportRefusalCode.Timeout, $"Reading the file took longer than {Limits.Timeout.TotalSeconds:0.#} seconds, so it was stopped.");
    }

    /// <summary>Cheap form for tight loops: checks once every 256 calls.</summary>
    public void Tick()
    {
        if ((++_ticks & 0xFF) == 0)
            Check();
    }
}
