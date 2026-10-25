using System.Globalization;
using Tempest.Calc.Import.Cad;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Import.Mapping;

/// <summary>What the provenance of an imported value says, read back.</summary>
public sealed record CadImportProvenanceInfo(string SystemAndVersion, string? Part, string? PartRevision, string FileSha256, DateTimeOffset ImportedAt, string CadName, string ProfileId, int ProfileVersion);

/// <summary>
/// The provenance every imported value carries (<see cref="CalcProvenanceKind.CadImport"/>): the CAD system and version, the
/// part and its revision, the SHA-256 of the file, the import time and the CAD parameter's name in the reference, and the
/// mapping profile id and version in the revision. The reference is capped at
/// <see cref="CalcProvenance.MaxReferenceLength"/>; a long part or parameter name is shortened there (the full facts are in
/// the instance's <c>CadImportRecord</c>), the hash is never shortened.
/// </summary>
public static class CadImportProvenance
{
    public const string Prefix = "cad:";
    private const string TimeFormat = "yyyy-MM-ddTHH:mmZ";

    public static CalcProvenance Create(CadSourceInfo source, CadMappingProfile profile, string cadName, DateTimeOffset importedAt)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(profile);

        var label = SystemLabel(source.System);
        var version = Field(source.CadVersion, 28);
        var systemAndVersion = version.Length == 0 ? label : version.StartsWith(label, StringComparison.OrdinalIgnoreCase) ? version : $"{label} {version}";
        var part = Field(source.PartName, 32);
        var revision = Field(source.PartRevision, 12);
        var time = importedAt.UtcDateTime.ToString(TimeFormat, CultureInfo.InvariantCulture);
        var name = Field(cadName, 60);

        string Build() => $"{Prefix}{systemAndVersion}|{part}|{revision}|{source.Sha256}|{time}|{name}";
        var reference = Build();

        // Shorten the free-text pieces, never the hash or the time, until the reference fits.
        while (reference.Length > CalcProvenance.MaxReferenceLength && (name.Length > 8 || part.Length > 8))
        {
            if (name.Length >= part.Length && name.Length > 8)
                name = name[..^4].TrimEnd() + "…";
            else if (part.Length > 8)
                part = part[..^4].TrimEnd() + "…";

            reference = Build();
        }

        return new CalcProvenance(CalcProvenanceKind.CadImport, reference, profile.Label);
    }

    /// <summary>Reads a provenance written by <see cref="Create"/>, or <see langword="null"/> when it is not one.</summary>
    public static CadImportProvenanceInfo? TryParse(CalcProvenance? provenance)
    {
        if (provenance is not { Kind: CalcProvenanceKind.CadImport, Reference: { } reference } || !reference.StartsWith(Prefix, StringComparison.Ordinal))
            return null;

        var parts = reference[Prefix.Length..].Split('|');

        if (parts.Length != 6 || parts[3].Length != 64 || !DateTimeOffset.TryParseExact(parts[4], TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at))
            return null;

        var revision = provenance.Revision ?? string.Empty;
        var atSign = revision.LastIndexOf('@');

        if (atSign <= 0 || !int.TryParse(revision[(atSign + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var version))
            return null;

        return new CadImportProvenanceInfo(parts[0], parts[1].Length == 0 ? null : parts[1], parts[2].Length == 0 ? null : parts[2], parts[3], at, parts[5], revision[..atSign], version);
    }

    public static string SystemLabel(CadSystem system) => system switch
    {
        CadSystem.Nx => "NX",
        CadSystem.Creo => "Creo",
        CadSystem.SolidWorks => "SolidWorks",
        CadSystem.Onshape => "Onshape",
        _ => "CAD",
    };

    private static string Field(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var clean = value.Replace('|', '/').Trim();
        return clean.Length > max ? clean[..max].TrimEnd() + "…" : clean;
    }
}
