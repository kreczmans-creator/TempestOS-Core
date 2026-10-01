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
/// a folder name that <i>equals</i> the customer's code, or failing that
/// one that starts with the code followed by a space (the
/// <c>&lt;CODE&gt; &lt;Name&gt;</c> form the first build created, so
/// <c>ACME1</c> never matches <c>ACME12 Ltd</c>), or failing that one that
/// <i>equals</i> the customer's name; only when none exists is a folder
/// named just the code (runbook feedback C6: "make the company folder just
/// the 5 letter ID") — or the customer's name when it has no code —
/// created. The project folder is found the same way on the project's own
/// identifier (equal to it, or it followed by a space), and created as
/// just the identifier (C6: "make the project just the project ID") —
/// for example <c>ACME1\ACME1-BRIDG1</c>. This is what lets the service
/// adopt a folder tree that already exists on the PO's own D: drive
/// rather than creating a duplicate beside it. When the customer's
/// folder holds no folder for the project, every other customer folder
/// is searched for its identifier before anything is created, so a
/// project whose customer changed keeps its one tree. A customer filed
/// by name whose name's first word is shaped like a customer code is
/// filed as <c>_</c> + the name, so it never collides with a coded
/// customer's folder.
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

            var customerFolder = FindCustomerFolder(root, request);
            var projectFolder = (customerFolder is null ? null : FindProjectFolder(customerFolder, request))
                                ?? FindProjectFolderUnderAnyCustomer(root, customerFolder, request);
            if (projectFolder is null)
            {
                customerFolder ??= CreateFolder(root, CustomerFolderName(request), created);
                projectFolder = CreateFolder(customerFolder, ProjectFolderName(request), created);
            }

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

        if (code.Length > 0 && MatchToken(root, existing, code) is { } byCode)
            return byCode;

        if (name.Length == 0)
            return null;

        var byName = NameFolderName(name);
        return Match(root, existing, n => string.Equals(n, byName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The project's folder under any customer folder other than
    /// <paramref name="customerFolder"/>, found on the project's own
    /// identifier — so a project whose customer changed (a code edited, or
    /// a client set on a project first filed under
    /// <see cref="NoCustomerFolderName"/>) keeps the one tree it already
    /// has rather than gaining a second. Never matched on the project's
    /// name: two projects may share one, never an identifier.
    /// </summary>
    private static string? FindProjectFolderUnderAnyCustomer(string root, string? customerFolder, ProjectFolderRequest request)
    {
        var identifier = SanitiseSegment(request.ProjectIdentifier);
        if (identifier.Length == 0)
            return null;

        foreach (var name in ChildFolderNames(root))
        {
            var candidate = Path.Combine(root, name);
            if (customerFolder is not null && string.Equals(candidate, customerFolder, StringComparison.OrdinalIgnoreCase))
                continue;

            List<string> projects;
            try
            {
                projects = ChildFolderNames(candidate);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                continue;
            }

            if (MatchToken(candidate, projects, identifier) is { } found)
                return found;
        }

        return null;
    }

    /// <summary>
    /// The folder name a customer is filed under by its name (no code, or
    /// no folder for its code): the name itself, or <c>_</c> + the name
    /// when its first word is shaped like a customer code — so a customer
    /// named "Bravo" never takes, or is taken by, the folder of the
    /// customer whose code is <c>BRAVO</c> (neither the exact nor the
    /// code-and-space match of <see cref="MatchToken"/> can reach a name
    /// that starts with <c>_</c>, which a code never does).
    /// </summary>
    private static string NameFolderName(string name)
    {
        var space = name.IndexOf(' ', StringComparison.Ordinal);
        var firstWord = space < 0 ? name : name[..space];
        return ProjectNumbering.IsValidCustomerCode(ProjectNumbering.Normalise(firstWord)) ? "_" + name : name;
    }

    private static string? FindProjectFolder(string customerFolder, ProjectFolderRequest request)
    {
        var existing = ChildFolderNames(customerFolder);
        var identifier = SanitiseSegment(request.ProjectIdentifier);

        if (identifier.Length > 0)
            return MatchToken(customerFolder, existing, identifier);

        var name = SanitiseSegment(request.ProjectName);
        return name.Length > 0 ? Match(customerFolder, existing, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) : null;
    }

    private static string CustomerFolderName(ProjectFolderRequest request)
    {
        var code = SanitiseSegment(request.CustomerCode);
        var name = SanitiseSegment(request.CustomerName);

        var chosen = code.Length > 0 ? code : name.Length > 0 ? NameFolderName(name) : NoCustomerFolderName;
        return SanitiseSegment(chosen) is { Length: > 0 } safe ? safe : NoCustomerFolderName;
    }

    private static string ProjectFolderName(ProjectFolderRequest request)
    {
        var identifier = SanitiseSegment(request.ProjectIdentifier);
        var name = SanitiseSegment(request.ProjectName);

        var chosen = identifier.Length > 0 ? identifier : name;
        return SanitiseSegment(chosen) is { Length: > 0 } safe ? safe : "_Unnamed project";
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

    /// <summary>
    /// The folder named exactly <paramref name="token"/> (ignoring case), or
    /// failing that the first named <paramref name="token"/> followed by a
    /// space — the <c>&lt;CODE&gt; &lt;Name&gt;</c> and
    /// <c>&lt;identifier&gt; &lt;project name&gt;</c> forms earlier builds
    /// created, reused rather than duplicated.
    /// </summary>
    private static string? MatchToken(string parent, List<string> names, string token) =>
        Match(parent, names, n => string.Equals(n, token, StringComparison.OrdinalIgnoreCase))
        ?? Match(parent, names, n => n.Length > token.Length
                                     && n.StartsWith(token, StringComparison.OrdinalIgnoreCase)
                                     && n[token.Length] == ' ');

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
