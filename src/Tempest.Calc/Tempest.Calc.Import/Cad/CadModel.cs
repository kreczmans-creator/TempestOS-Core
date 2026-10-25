using Tempest.Calc.Sheets;

namespace Tempest.Calc.Import.Cad;

/// <summary>The CAD system a file came from. <see cref="Generic"/> is the schema documented in <c>docs/calcs/workflows/w1-cad-parameter-import.md</c>.</summary>
public enum CadSystem
{
    Nx,
    Creo,
    SolidWorks,
    Onshape,
    Generic,
}

/// <summary>The file formats W1 reads.</summary>
public enum CadSourceFormat
{
    /// <summary>NX expression file (.exp): <c>[unit]name=rhs</c> lines, <c>//</c> comments.</summary>
    NxExpressions,

    /// <summary>NX expressions exported to a workbook (.xlsx): name, formula, value, unit columns.</summary>
    NxXlsx,

    /// <summary>Creo relations and parameters as text.</summary>
    CreoRelations,

    /// <summary>SolidWorks equations text: <c>"name"= rhs</c> lines.</summary>
    SolidWorksEquations,

    /// <summary>SolidWorks design table (.xlsx).</summary>
    SolidWorksDesignTable,

    /// <summary>Onshape variables as CSV.</summary>
    OnshapeCsv,

    /// <summary>Onshape variables as JSON.</summary>
    OnshapeJson,

    /// <summary>The generic CSV schema.</summary>
    GenericCsv,

    /// <summary>The generic JSON schema.</summary>
    GenericJson,
}

/// <summary>What a parameter is. An <see cref="Expression"/> is read-only text: it is shown, never evaluated, and never mapped to an input as a number.</summary>
public enum CadParameterKind
{
    Number,
    Text,
    Boolean,
    Expression,
}

/// <summary>Whether a design table lists configurations down the rows (the SolidWorks default) or across the columns.</summary>
public enum DesignTableOrientation
{
    ConfigurationsInRows,
    ConfigurationsInColumns,
}

/// <summary>
/// One parameter as the CAD file states it. A number carries its unit as the engine's unit symbol when the file stated
/// one the engine knows (<see cref="Unit"/>); a unit the file stated that the engine does not know leaves
/// <see cref="UnitProblem"/> set and the parameter unmappable. An expression keeps its source text in <see cref="Text"/>.
/// </summary>
public sealed record CadParameter(
    string Name,
    CadParameterKind Kind,
    string Location,
    double? Number = null,
    string? Unit = null,
    string? RawUnit = null,
    string? UnitProblem = null,
    string? Text = null,
    bool? Flag = null,
    string? Comment = null,
    bool FromFormulaCell = false,
    string? Reported = null);

/// <summary>Where a parse came from: the system and format, the file and its hash, and the identity facts the file or the person supplied.</summary>
public sealed record CadSourceInfo(
    CadSystem System,
    CadSourceFormat Format,
    string FileName,
    string Sha256,
    string? CadVersion,
    string? PartName,
    string? PartRevision,
    string? Configuration = null,
    IReadOnlyList<string>? Configurations = null);

/// <summary>The outcome of parsing one file: its source facts, every parameter in file order, and notices.</summary>
public sealed record CadParseResult(CadSourceInfo Source, IReadOnlyList<CadParameter> Parameters, IReadOnlyList<CalcNotice> Notices)
{
    /// <summary>Names (by exact text) that the file assigns more than once, with the locations.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Duplicates => Parameters
        .GroupBy(p => p.Name, StringComparer.Ordinal)
        .Where(g => g.Count() > 1)
        .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(p => p.Location).ToList(), StringComparer.Ordinal);
}

/// <summary>
/// What the person tells the importer about the file. Anything entered here takes precedence over what the file says
/// about itself (CAD version, part name, revision), so provenance can be completed for formats that carry none.
/// </summary>
public sealed record CadParseSettings
{
    public NumberProfile Number { get; init; } = NumberProfile.Point;

    public string? CadVersion { get; init; }

    public string? PartName { get; init; }

    public string? Revision { get; init; }

    /// <summary>The design-table configuration (row or column label) to read; the first when null.</summary>
    public string? Configuration { get; init; }

    /// <summary>The worksheet to read; the first visible sheet when null.</summary>
    public string? SheetName { get; init; }

    public DesignTableOrientation Orientation { get; init; } = DesignTableOrientation.ConfigurationsInRows;

    public ImportLimits Limits { get; init; } = ImportLimits.Default;
}
