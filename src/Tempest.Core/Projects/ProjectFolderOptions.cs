using Tempest.Core.Configuration;

namespace Tempest.Core.Projects;

/// <summary>
/// Where <see cref="ProjectFolderService"/> generates each project's own
/// Windows Explorer folder, and what it puts inside it (Product Owner
/// decision 2026-10-01: "Folders should be generated in D:\01 Projects on
/// this computer. It should first search for the customer, if none then
/// make new, likewise project ref, then a standard set of folders within
/// that").
/// </summary>
/// <remarks>
/// <para>
/// Read from the platform's own <see cref="IConfigurationProvider"/>
/// (`ADR-0146`), never a second settings mechanism:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="FolderRootKey"/> — the root folder. Unset on Windows means <see cref="DefaultWindowsRoot"/>; unset anywhere else means none (folder generation is a Windows-desktop act). Set to an empty value, or to <see cref="OffValue"/>, to switch generation off explicitly (an environment variable cannot carry an empty value on Windows, hence the named value).</description></item>
/// <item><description><see cref="StandardSubfoldersKey"/> — the standard subfolder set, either one value separated by <c>;</c> or <c>|</c>, or an indexed list (<c>Projects:StandardSubfolders:0</c>, <c>:1</c>, …, the shape a JSON array in <c>appsettings.json</c> flattens to). Nested folders may use <c>\</c> or <c>/</c>.</description></item>
/// <item><description><see cref="QuoteSubfolderKey"/> — the subfolder (relative to the project folder) a quote PDF export starts in. Unset means the project folder itself.</description></item>
/// </list>
/// <para>
/// <b>TODO (v1.0.0 BLOCKER — PO decision 2026-10-01):</b> the standard
/// subfolder set is still to be defined by the Product Owner ("a standard
/// set of folders within that, TBC"). Until it is, <see cref="DefaultStandardSubfolders"/>
/// is deliberately empty and the service creates only the customer and
/// project folders. Tracked in <c>docs/releases/v1.0.0/WorkPackages.md</c>
/// and <c>BACKLOG.md</c>.
/// </para>
/// </remarks>
/// <param name="Root">The root folder every customer folder lives under. <see langword="null"/> or blank switches folder generation off.</param>
/// <param name="StandardSubfolders">The subfolders created inside every project folder. Never <see langword="null"/>; may be empty.</param>
/// <param name="QuoteSubfolder">The quote export's own subfolder, relative to the project folder. <see langword="null"/> for the project folder itself.</param>
public sealed record ProjectFolderOptions(string? Root, IReadOnlyList<string> StandardSubfolders, string? QuoteSubfolder = null)
{
    /// <summary>The configuration key naming the projects root folder.</summary>
    public const string FolderRootKey = "Projects:FolderRoot";

    /// <summary>The configuration key listing the standard subfolder set.</summary>
    public const string StandardSubfoldersKey = "Projects:StandardSubfolders";

    /// <summary>The configuration key naming the quote export's own subfolder.</summary>
    public const string QuoteSubfolderKey = "Projects:QuoteSubfolder";

    /// <summary>The <see cref="FolderRootKey"/> value that switches folder generation off — what the Desktop test suite sets so a test run never writes to the real <see cref="DefaultWindowsRoot"/>.</summary>
    public const string OffValue = "off";

    /// <summary>The default root on Windows (PO decision 2026-10-01: "D:\01 Projects on this computer").</summary>
    public const string DefaultWindowsRoot = @"D:\01 Projects";

    /// <summary>
    /// The default standard subfolder set — <b>empty on purpose</b>.
    /// TODO (v1.0.0 BLOCKER — PO decision 2026-10-01): replace with the
    /// Product Owner's own defined set before v1.0.0 ships.
    /// </summary>
    public static IReadOnlyList<string> DefaultStandardSubfolders { get; } = [];

    /// <summary>Options with generation switched off — what a caller with no configuration uses.</summary>
    public static ProjectFolderOptions Disabled { get; } = new(null, []);

    /// <summary>Reads the options from <paramref name="configuration"/>.</summary>
    /// <param name="configuration">The platform's configuration, or <see langword="null"/> to use the defaults alone.</param>
    /// <param name="isWindows">Whether this is a Windows desktop; <see langword="null"/> asks the running OS. Decides whether an unset root defaults to <see cref="DefaultWindowsRoot"/>.</param>
    public static ProjectFolderOptions FromConfiguration(IConfigurationProvider? configuration, bool? isWindows = null)
    {
        var windows = isWindows ?? OperatingSystem.IsWindows();

        string? root = windows ? DefaultWindowsRoot : null;
        if (configuration is not null && configuration.TryGetValue(FolderRootKey, out var configuredRoot))
            root = string.IsNullOrWhiteSpace(configuredRoot) || string.Equals(configuredRoot.Trim(), OffValue, StringComparison.OrdinalIgnoreCase)
                ? null
                : configuredRoot.Trim();

        string? quoteSubfolder = null;
        if (configuration is not null && configuration.TryGetValue(QuoteSubfolderKey, out var configuredQuote) && !string.IsNullOrWhiteSpace(configuredQuote))
            quoteSubfolder = configuredQuote.Trim();

        return new ProjectFolderOptions(root, ReadSubfolders(configuration), quoteSubfolder);
    }

    private static IReadOnlyList<string> ReadSubfolders(IConfigurationProvider? configuration)
    {
        if (configuration is null)
            return DefaultStandardSubfolders;

        var found = new List<string>();

        if (configuration.TryGetValue(StandardSubfoldersKey, out var single) && !string.IsNullOrWhiteSpace(single))
            found.AddRange(single.Split([';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var indexedPrefix = StandardSubfoldersKey + ":";
        var indexed = configuration.GetAll()
            .Where(kv => kv.Key.StartsWith(indexedPrefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(kv.Key.AsSpan(indexedPrefix.Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out _))
            .OrderBy(kv => int.Parse(kv.Key.AsSpan(indexedPrefix.Length), System.Globalization.CultureInfo.InvariantCulture))
            .Select(kv => kv.Value.Trim())
            .Where(v => v.Length > 0);
        found.AddRange(indexed);

        return found.Count == 0 ? DefaultStandardSubfolders : found;
    }
}
