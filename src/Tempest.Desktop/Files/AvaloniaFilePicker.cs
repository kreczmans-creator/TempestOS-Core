using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Tempest.Workspace.Files;

namespace Tempest.Desktop.Files;

/// <summary>
/// The real <see cref="IFilePicker"/> (`WP 18.2A`, Execution Plan §3
/// decision 6) — wraps Avalonia's own <see cref="IStorageProvider"/>,
/// reached from <see cref="TopLevel.GetTopLevel(Avalonia.Visual)"/> so this
/// class needs no reference to <see cref="Window"/> or to which window is
/// currently active.
/// </summary>
public sealed class AvaloniaFilePicker : IFilePicker
{
    private readonly Visual _owner;

    /// <summary>Initialises a new instance of the <see cref="AvaloniaFilePicker"/> class.</summary>
    /// <param name="owner">Any control in the visual tree of the window the picker dialog should be owned by.</param>
    public AvaloniaFilePicker(Visual owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        _owner = owner;
    }

    /// <inheritdoc />
    /// <remarks>No <see cref="TopLevel"/> (the control is not yet attached to a window) is reported as no files picked, never an exception — a picker asked for before the shell is fully up has nothing sensible to show.</remarks>
    public async Task<IReadOnlyList<PickedFile>> PickFilesAsync(FilePickerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (TopLevel.GetTopLevel(_owner) is not { } topLevel)
            return [];

        var options = new FilePickerOpenOptions
        {
            Title = request.Title,
            AllowMultiple = request.AllowMultiple,
        };

        if (request.Extensions is { Count: > 0 })
        {
            options.FileTypeFilter =
            [
                new FilePickerFileType(request.FileTypeDescription ?? "Files")
                {
                    Patterns = [.. request.Extensions.Select(e => $"*.{e}")],
                },
            ];
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(options).ConfigureAwait(true);

        return [.. files.Select(ToPickedFile)];
    }

    /// <inheritdoc />
    public async Task<string?> PickSavePathAsync(SavePickerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (TopLevel.GetTopLevel(_owner) is not { } topLevel)
            return null;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = request.Title,
            SuggestedFileName = request.SuggestedFileName,
        }).ConfigureAwait(true);

        return file?.TryGetLocalPath() ?? file?.Path.ToString();
    }

    private static PickedFile ToPickedFile(IStorageFile file) =>
        new(file.Name, FileContentTypes.ForFileName(file.Name), () => ReadAllBytesAsync(file));

    private static async Task<ReadOnlyMemory<byte>> ReadAllBytesAsync(IStorageFile file)
    {
        await using var stream = await file.OpenReadAsync().ConfigureAwait(false);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer).ConfigureAwait(false);
        return buffer.ToArray();
    }
}
