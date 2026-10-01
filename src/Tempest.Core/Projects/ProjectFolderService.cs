using System.Text;
using Tempest.Core.Logging;

namespace Tempest.Core.Projects;

/// <summary>
/// Finds or creates a project's own Windows Explorer folder —
/// <c>&lt;root&gt;\&lt;customer folder&gt;\&lt;project folder&gt;\&lt;standard subfolders&gt;</c>
/// — so a project created in TempestOS has a real place on disk its quote
/// PDF and everything else about it is filed under (PO decision
/// 2026-10-01; earlier PO comment: "when we create a project, it generates
/// a standard file system in Windows Explorer and exports direct to the
/// quote section there").
/// </summary>
/// <remarks>
/// <para>
/// <b>Find first, create only what is missing.</b> The PO's own words:
/// "It should first search for the customer, if none then make new,
/// likewise project ref". The customer folder is matched, ignoring case, on
/// a folder name that <i>starts with</i> the customer's code (the code
/// followed by the end of the name or a non-alphanumeric character, so
/// <c>ACME</c> never matches <c>ACMEX Ltd</c>), or failing that one that
/// <i>equals</i> the customer's name; only when neither exists is
/// <c>&lt;CODE&gt; &lt;Name&gt;</c> (or <c>&lt;Name&gt;</c> with no code)
/// created. The project folder is found the same way on the project's own
/// identifier, and created as <c>&lt;identifier&gt; &lt;project name&gt;</c>.
/// This is what lets the service adopt a folder tree that already exists
/// on the PO's own D: drive rather than creating a duplicate beside it.
/// </para>
/// <para>
/// <b>Never throws into the UI.</b> Every refusal — generation switched
/// off, not Windows (no default root), a root whose drive does not exist,
/// a permissions or I/O failure — comes back as a
/// <see cref="ProjectFolderOutcome"/> carrying the reason. The folder is a
/// convenience beside the project, never a precondition of it.
/// </para>
/// <para>
/// <b>Names are sanitised to Windows rules</b> on every platform (the
/// characters <c>&lt;&gt;:"/\|?*</c> and control characters become
/// <c>-</c>, trailing dots and spaces are trimmed, reserved device names
/// such as <c>CON</c> are prefixed with <c>_</c>), so a folder created
/// anywhere is a folder Windows Explorer can open, and a subfolder entry
/// can never climb out of the project folder (<c>..</c> sanitises to
/// nothing and is skipped).
/// </para>
/// </remarks>
public sealed class ProjectFolderService
{
    /// <summary>The customer folder a project with no customer is filed under.</summary>
    public const string NoCustomerFolderName = "_No customer";

    private const int MaxSegmentLength = 100;

    private static readonly char[] WindowsInvalidNameChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private readonly ILogger? _logger;

    /// <summary>Initialises a new instance of the <see cref="ProjectFolderService"/> class.</summary>
    /// <param name="options">Where folders go and what goes in them — see <see cref="ProjectFolderOptions.FromConfiguration"/>.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public ProjectFolderService(ProjectFolderOptions options, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        Options = options;
        _logger = logger;
    }

    /// <summary>The options this instance generates folders with.</summary>
    public ProjectFolderOptions Options { get; }

    /// <summary>
    /// Ensures the project's own folder tree exists — finding the customer
    /// and project folders first, creating only what is missing (see this
    /// class's own remarks).
    /// </summary>
    /// <param name="request">The project to file.</param>
    /// <returns>What happened. Never <see langword="null"/>; never an exception for a file-system refusal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    public ProjectFolderOutcome Ensure(ProjectFolderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Unavailable() is { } unavailable)
            return unavailable;

        var root = Options.Root!;
        var created = new List<string>();

        try
        {
            if (!Directory.Exists(root))
            {
                Directory.CreateDirectory(root);
                created.Add(root);
            }

            var customerFolder = FindCustomerFolder(root, request) ?? CreateFolder(root, CustomerFolderName(request), created);
            var projectFolder = FindProjectFolder(customerFolder, request) ?? CreateFolder(customerFolder, ProjectFolderName(request), created);

            foreach (var subfolder in Options.StandardSubfolders.Append(Options.QuoteSubfolder))
            {
                if (SanitiseRelativePath(subfolder) is { } relative)
                    EnsureFolder(Path.Combine(projectFolder, relative), created);
            }

            var outcome = created.Count == 0
                ? new ProjectFolderOutcome(ProjectFolderStatus.AlreadyExisted, $"Project folder: {projectFolder}", projectFolder, created)
                : new ProjectFolderOutcome(ProjectFolderStatus.Created, $"Project folder created: {projectFolder}", projectFolder, created);

            if (created.Count > 0)
                _logger?.Information($"Project folder ensured for '{request.ProjectIdentifier}': created {string.Join(", ", created)}.");

            return outcome;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or System.Security.SecurityException)
        {
            _logger?.Warning($"Project folder for '{request.ProjectIdentifier}' could not be created under '{root}'.", ex);
            return new ProjectFolderOutcome(
                ProjectFolderStatus.Failed, $"Project folder could not be created under '{root}': {ex.Message}", null, created);
        }
    }

    /// <summary>
    /// The folder a quote PDF export for this project should start in —
    /// the project folder, or its configured <see cref="ProjectFolderOptions.QuoteSubfolder"/>
    /// — ensuring it exists first. <see langword="null"/> when folder
    /// generation is unavailable or failed, so the caller falls back to
    /// the picker's own default exactly as before.
    /// </summary>
    /// <param name="request">The project the quote belongs to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    public string? QuoteFolderFor(ProjectFolderRequest request)
    {
        var outcome = Ensure(request);
        if (outcome.ProjectFolder is not { } projectFolder)
            return null;

        return SanitiseRelativePath(Options.QuoteSubfolder) is { } relative
            ? Path.Combine(projectFolder, relative)
            : projectFolder;
    }

    /// <summary>
    /// Makes <paramref name="value"/> safe as one Windows folder name (see
    /// this class's own remarks). Returns an empty string when nothing
    /// usable is left.
    /// </summary>
    /// <param name="value">The raw name.</param>
    public static string SanitiseSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        var lastWasSpace = false;
        foreach (var c in value)
        {
            var mapped = char.IsControl(c) || Array.IndexOf(WindowsInvalidNameChars, c) >= 0 ? '-' : c;
            if (char.IsWhiteSpace(mapped))
            {
                if (!lastWasSpace)
                    builder.Append(' ');
                lastWasSpace = true;
                continue;
            }

            builder.Append(mapped);
            lastWasSpace = false;
        }

        var result = builder.ToString().Trim().TrimEnd('.', ' ');
        if (result.Length > MaxSegmentLength)
            result = result[..MaxSegmentLength].TrimEnd('.', ' ');

        var stem = result.Split('.')[0].TrimEnd();
        if (ReservedDeviceNames.Contains(stem))
            result = "_" + result;

        return result;
    }

    private ProjectFolderOutcome? Unavailable()
    {
        var root = Options.Root;

        if (string.IsNullOrWhiteSpace(root))
        {
            return Refuse(OperatingSystem.IsWindows()
                ? $"Project folders are switched off ({ProjectFolderOptions.FolderRootKey} is empty or '{ProjectFolderOptions.OffValue}')."
                : $"Project folders are generated on Windows only; set {ProjectFolderOptions.FolderRootKey} to use them here.");
        }

        if (!Path.IsPathFullyQualified(root))
            return Refuse($"Project folders are unavailable: '{root}' ({ProjectFolderOptions.FolderRootKey}) is not a full path.");

        var drive = Path.GetPathRoot(root);
        if (string.IsNullOrEmpty(drive) || !Directory.Exists(drive))
            return Refuse($"Project folders are unavailable: drive '{drive}' for '{root}' was not found on this computer.");

        return null;

        ProjectFolderOutcome Refuse(string message)
        {
            _logger?.Information(message);
            return new ProjectFolderOutcome(ProjectFolderStatus.Unavailable, message, null, []);
        }
    }

    private static string? FindCustomerFolder(string root, ProjectFolderRequest request)
    {
        var existing = ChildFolderNames(root);
        var code = SanitiseSegment(request.CustomerCode);
        var name = SanitiseSegment(request.CustomerName);

        if (code.Length == 0 && name.Length == 0)
            return Match(root, existing, n => string.Equals(n, NoCustomerFolderName, StringComparison.OrdinalIgnoreCase));

        if (code.Length > 0 && Match(root, existing, n => StartsWithToken(n, code)) is { } byCode)
            return byCode;

        return name.Length > 0 ? Match(root, existing, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) : null;
    }

    private static string? FindProjectFolder(string customerFolder, ProjectFolderRequest request)
    {
        var existing = ChildFolderNames(customerFolder);
        var identifier = SanitiseSegment(request.ProjectIdentifier);

        if (identifier.Length > 0)
            return Match(customerFolder, existing, n => StartsWithToken(n, identifier));

        var name = SanitiseSegment(request.ProjectName);
        return name.Length > 0 ? Match(customerFolder, existing, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) : null;
    }

    private static string CustomerFolderName(ProjectFolderRequest request)
    {
        var code = SanitiseSegment(request.CustomerCode);
        var name = SanitiseSegment(request.CustomerName);

        var combined = (code.Length > 0, name.Length > 0) switch
        {
            (true, true) when !StartsWithToken(name, code) => $"{code} {name}",
            (true, true) => name,
            (true, false) => code,
            (false, true) => name,
            _ => NoCustomerFolderName,
        };

        return SanitiseSegment(combined) is { Length: > 0 } safe ? safe : NoCustomerFolderName;
    }

    private static string ProjectFolderName(ProjectFolderRequest request)
    {
        var identifier = SanitiseSegment(request.ProjectIdentifier);
        var name = SanitiseSegment(request.ProjectName);

        var combined = identifier.Length > 0 && name.Length > 0 ? $"{identifier} {name}" : identifier.Length > 0 ? identifier : name;
        return SanitiseSegment(combined) is { Length: > 0 } safe ? safe : "_Unnamed project";
    }

    private static string? SanitiseRelativePath(string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
            return null;

        var segments = relative
            .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)
            .Select(SanitiseSegment)
            .Where(s => s.Length > 0)
            .ToArray();

        return segments.Length == 0 ? null : Path.Combine(segments);
    }

    private static List<string> ChildFolderNames(string folder) =>
        [.. Directory.EnumerateDirectories(folder)
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ThenBy(n => n, StringComparer.Ordinal)];

    private static string? Match(string parent, List<string> names, Func<string, bool> predicate) =>
        names.FirstOrDefault(predicate) is { } found ? Path.Combine(parent, found) : null;

    /// <summary>Whether <paramref name="name"/> starts with <paramref name="token"/> as a whole token — followed by the end of the name or a non-alphanumeric character.</summary>
    private static bool StartsWithToken(string name, string token) =>
        name.StartsWith(token, StringComparison.OrdinalIgnoreCase)
        && (name.Length == token.Length || !char.IsLetterOrDigit(name[token.Length]));

    private static string CreateFolder(string parent, string name, List<string> created)
    {
        var path = Path.Combine(parent, name);
        EnsureFolder(path, created);
        return path;
    }

    private static void EnsureFolder(string path, List<string> created)
    {
        if (Directory.Exists(path))
            return;

        Directory.CreateDirectory(path);
        created.Add(path);
    }
}
