using System.Globalization;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Import.Scan;

/// <summary>One value an import wrote: where (<c>D</c>, <c>contributors[0].upper</c>, <c>measured{D_s}.mean</c>), the number and unit or the text. What a claim of scan provenance is checked against.</summary>
public sealed record ScanPushedValue(string Path, double? Number, string? Unit, string? Text, CalcProvenance? Provenance = null);

/// <summary>One fitted feature as it was when imported: what the next import is compared against. Values are in the profile's unit.</summary>
public sealed record ScanImportedFeature(
    string Feature,
    string Stage,
    string? Target,
    int Count,
    double Mean,
    double Sigma,
    double? Cpk,
    double? Ppk,
    double? Nominal,
    string SetSha256,
    string Unit,
    IReadOnlyList<ScanPushedValue>? Pushed = null);

/// <summary>A file of an import: its name and SHA-256, and the stage and serial the person entered for it.</summary>
public sealed record ScanImportFile(string FileName, string Sha256, string? Stage, string? Serial);

/// <summary>
/// What a calculation remembers about a scan import: the files (name and SHA-256), the software and version, the alignment the person entered,
/// the profile, who imported it and when, and every feature fitted. It is kept on the instance (and so frozen with an issued revision); a later import
/// through the same profile is compared with it.
/// </summary>
public sealed record ScanImportRecord(
    string Key,
    string ProfileId,
    int ProfileVersion,
    string Software,
    string? SoftwareVersion,
    string? Alignment,
    IReadOnlyList<ScanImportFile> Files,
    DateTimeOffset ImportedAt,
    string ImportedBy,
    IReadOnlyList<ScanImportedFeature> Features)
{
    /// <summary>The identity of an import source: the same scan profile.</summary>
    public static string KeyFor(string profileId) => "scan|" + profileId;
}
