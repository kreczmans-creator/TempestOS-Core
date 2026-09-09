namespace Tempest.Workspace.Files;

/// <summary>
/// Maps a file's own extension to a MIME content type — the one place
/// every <see cref="IFilePicker"/> implementation (the real Avalonia
/// picker, the headless test stub) derives a picked file's
/// <see cref="PickedFile.ContentType"/> from, so the two never drift apart
/// (`WP 18.2A`).
/// </summary>
public static class FileContentTypes
{
    /// <summary>The content type reported when a file's own extension is not one this platform recognises.</summary>
    public const string Unknown = "application/octet-stream";

    private static readonly IReadOnlyDictionary<string, string> ByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ["pdf"] = "application/pdf",
        ["docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ["dwg"] = "application/acad",
        ["png"] = "image/png",
        ["jpg"] = "image/jpeg",
        ["jpeg"] = "image/jpeg",
        ["txt"] = "text/plain",
        ["csv"] = "text/csv",
    };

    /// <summary>The content type for <paramref name="fileName"/>'s own extension, or <see cref="Unknown"/> when it is not recognised.</summary>
    public static string ForFileName(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var extension = Path.GetExtension(fileName).TrimStart('.');
        return extension.Length > 0 && ByExtension.TryGetValue(extension, out var contentType) ? contentType : Unknown;
    }
}
