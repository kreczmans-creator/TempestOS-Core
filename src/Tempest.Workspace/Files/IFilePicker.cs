namespace Tempest.Workspace.Files;

/// <summary>
/// One file the user picked (or dropped), lazily readable — the shell
/// never reads every picked file's bytes just to offer a choice; only the
/// files actually attached are read (`WP 18.2A`).
/// </summary>
/// <param name="Name">The file's own display name, including its extension.</param>
/// <param name="ContentType">The file's MIME type, derived from its extension (`IFilePicker`'s own convention) or as the source (a drag/drop payload, the OS picker) reports it.</param>
/// <param name="ReadAsync">Reads the file's full content. Called at most once per attach; a caller that needs the bytes twice should cache the result itself.</param>
public sealed record PickedFile(string Name, string ContentType, Func<Task<ReadOnlyMemory<byte>>> ReadAsync);

/// <summary>What kind of files <see cref="IFilePicker.PickFilesAsync"/> is asking for, and whether more than one may be chosen.</summary>
/// <param name="Title">The picker dialog's own title.</param>
/// <param name="AllowMultiple">Whether the user may choose more than one file in a single pick.</param>
/// <param name="FileTypeDescription">A human-readable label for the offered file-type filter (e.g. <c>"Evidence files"</c>). <see langword="null"/> offers every file.</param>
/// <param name="Extensions">The file extensions offered (without the leading dot, e.g. <c>"xlsx"</c>). Empty or <see langword="null"/> offers every file.</param>
public sealed record FilePickerRequest(
    string Title,
    bool AllowMultiple = true,
    string? FileTypeDescription = null,
    IReadOnlyList<string>? Extensions = null);

/// <summary>What <see cref="IFilePicker.PickSavePathAsync"/> is asking the user to choose a destination for.</summary>
/// <param name="Title">The picker dialog's own title.</param>
/// <param name="SuggestedFileName">The file name offered as a starting point.</param>
public sealed record SavePickerRequest(string Title, string? SuggestedFileName = null);

/// <summary>
/// A real file picker, behind an interface (`WP 18.2A`, Execution Plan §3
/// decision 6): no file picker existed anywhere in the shell before this —
/// attachments were typed metadata a person filled in by hand
/// (<c>AttachDocumentCommand</c>'s metadata-only constructor). The Desktop
/// implementation wraps Avalonia's own <c>IStorageProvider</c>; the
/// headless test suite registers a stub that returns bytes from a temp
/// file, so a journey test can attach a real workbook with no dialog on
/// screen.
/// </summary>
public interface IFilePicker
{
    /// <summary>Asks the user to pick one or more files. An empty list means the user cancelled — never a refusal, never an exception.</summary>
    Task<IReadOnlyList<PickedFile>> PickFilesAsync(FilePickerRequest request, CancellationToken cancellationToken = default);

    /// <summary>Asks the user to choose a destination path to save to. <see langword="null"/> means the user cancelled.</summary>
    Task<string?> PickSavePathAsync(SavePickerRequest request, CancellationToken cancellationToken = default);
}
