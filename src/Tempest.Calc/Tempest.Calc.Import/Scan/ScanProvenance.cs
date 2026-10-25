using System.Globalization;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Import.Scan;

/// <summary>The facts of a fitted feature that go into its provenance.</summary>
public sealed record ScanProvenanceSource(string Software, string? SoftwareVersion, string? Stage, string Serials, string? Alignment, string Sha256);

/// <summary>What the provenance of a fitted value says, read back.</summary>
public sealed record ScanImportProvenanceInfo(string SoftwareAndVersion, string? Stage, string Serials, string? Alignment, string Sha256, DateTimeOffset ImportedAt, string Feature, string ProfileId, int ProfileVersion);

/// <summary>
/// The provenance every value fitted from a scan table carries (<see cref="CalcProvenanceKind.ScanImport"/>), in the same shape as W1's
/// <c>CadImportProvenance</c>: the reference names the software and its version, the stage, the serials, the alignment (as the person entered it), the
/// SHA-256 of the file (of the set of files, when a feature's parts came from several), the import time and the feature; the revision names the
/// scan profile as <c>id@version</c>. The reference is held to <see cref="CalcProvenance.MaxReferenceLength"/> by shortening the free-text pieces,
/// never the hash or the time.
/// </summary>
public static class ScanImportProvenance
{
    public const string Prefix = "scan:";
    private const string TimeFormat = "yyyy-MM-ddTHH:mmZ";

    public static CalcProvenance Create(ScanProvenanceSource source, ScanProfile profile, string feature, DateTimeOffset importedAt)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(profile);

        var software = Field(string.IsNullOrWhiteSpace(source.SoftwareVersion) ? source.Software : $"{source.Software} {source.SoftwareVersion}", 30);
        var stage = Field(source.Stage, 18);
        var serials = Field(source.Serials, 24);
        var alignment = Field(source.Alignment, 22);
        var time = importedAt.UtcDateTime.ToString(TimeFormat, CultureInfo.InvariantCulture);
        var name = Field(feature, 30);

        string Build() => $"{Prefix}{software}|{stage}|{serials}|{alignment}|{source.Sha256}|{time}|{name}";
        var reference = Build();

        while (reference.Length > CalcProvenance.MaxReferenceLength)
        {
            var longest = new[] { ("name", name.Length), ("software", software.Length), ("serials", serials.Length), ("alignment", alignment.Length), ("stage", stage.Length) }
                .OrderByDescending(x => x.Item2).First();

            if (longest.Item2 <= 8)
                break;

            switch (longest.Item1)
            {
                case "name":
                    name = Shorten(name);
                    break;
                case "software":
                    software = Shorten(software);
                    break;
                case "serials":
                    serials = Shorten(serials);
                    break;
                case "alignment":
                    alignment = Shorten(alignment);
                    break;
                default:
                    stage = Shorten(stage);
                    break;
            }

            reference = Build();
        }

        return new CalcProvenance(CalcProvenanceKind.ScanImport, reference, profile.Label);
    }

    /// <summary>Reads a provenance written by <see cref="Create"/>, or <see langword="null"/> when it is not one.</summary>
    public static ScanImportProvenanceInfo? TryParse(CalcProvenance? provenance)
    {
        if (provenance is not { Kind: CalcProvenanceKind.ScanImport, Reference: { } reference } || !reference.StartsWith(Prefix, StringComparison.Ordinal))
            return null;

        var parts = reference[Prefix.Length..].Split('|');

        if (parts.Length != 7 || parts[4].Length != 64 || !DateTimeOffset.TryParseExact(parts[5], TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at))
            return null;

        var revision = provenance.Revision ?? string.Empty;
        var atSign = revision.LastIndexOf('@');

        if (atSign <= 0 || !int.TryParse(revision[(atSign + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var version))
            return null;

        return new ScanImportProvenanceInfo(parts[0], parts[1].Length == 0 ? null : parts[1], parts[2], parts[3].Length == 0 ? null : parts[3], parts[4], at, parts[6], revision[..atSign], version);
    }

    private static string Shorten(string s) => s.Length <= 8 ? s : s[..^4].TrimEnd() + "…";

    private static string Field(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var clean = value.Replace('|', '/').Trim();
        return clean.Length > max ? clean[..max].TrimEnd() + "…" : clean;
    }
}
