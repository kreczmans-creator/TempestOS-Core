using Tempest.Workspace.Files;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The headless test double for <see cref="IFilePicker"/> (`WP 18.2A`,
/// Execution Plan §3 decision 6) — returns bytes from real files on disk
/// (typically under a per-test temp directory), so a journey test attaches
/// a real workbook with no OS dialog on screen, exactly as
/// <c>EvidenceTestHost</c> stubs the session principal. Every picker call
/// this stub receives is recorded, so a test can assert what the caller
/// asked for (title, extensions, multiplicity) without a headless window
/// ever existing to click through.
/// </summary>
public sealed class StubFilePicker : IFilePicker
{
    private readonly Queue<IReadOnlyList<string>> _pickResults = new();
    private string? _nextSavePath;
    private string? _nextFolder;

    /// <summary>Every folder pick this stub received, as (title, start folder), in call order.</summary>
    public List<(string Title, string? StartFolder)> FolderRequests { get; } = [];

    /// <summary>Sets the folder <see cref="PickFolderAsync"/> returns next. <see langword="null"/> (the default) simulates a cancelled pick.</summary>
    public void SetNextFolder(string? folder) => _nextFolder = folder;

    /// <inheritdoc />
    public Task<string?> PickFolderAsync(string title, string? startFolder = null, CancellationToken cancellationToken = default)
    {
        FolderRequests.Add((title, startFolder));
        return Task.FromResult(_nextFolder);
    }

    /// <summary>Every <see cref="FilePickerRequest"/> this stub received, in call order.</summary>
    public List<FilePickerRequest> PickRequests { get; } = [];

    /// <summary>Every <see cref="SavePickerRequest"/> this stub received, in call order — so a test can assert the start folder a save was offered (PO decision 2026-10-01).</summary>
    public List<SavePickerRequest> SaveRequests { get; } = [];

    /// <summary>Queues the paths of real files on disk to return from the next <see cref="PickFilesAsync"/> call.</summary>
    public void EnqueuePick(params string[] filePaths) => _pickResults.Enqueue(filePaths);

    /// <summary>Sets the path <see cref="PickSavePathAsync"/> returns next. <see langword="null"/> (the default) simulates a cancelled save.</summary>
    public void SetNextSavePath(string? path) => _nextSavePath = path;

    /// <inheritdoc />
    public Task<IReadOnlyList<PickedFile>> PickFilesAsync(FilePickerRequest request, CancellationToken cancellationToken = default)
    {
        PickRequests.Add(request);

        if (!_pickResults.TryDequeue(out var paths))
            return Task.FromResult<IReadOnlyList<PickedFile>>([]);

        IReadOnlyList<PickedFile> picked = [.. paths.Select(ToPickedFile)];
        return Task.FromResult(picked);
    }

    /// <inheritdoc />
    public Task<string?> PickSavePathAsync(SavePickerRequest request, CancellationToken cancellationToken = default)
    {
        SaveRequests.Add(request);
        return Task.FromResult(_nextSavePath);
    }

    private static PickedFile ToPickedFile(string path)
    {
        var name = Path.GetFileName(path);
        return new PickedFile(name, FileContentTypes.ForFileName(name), () => Task.FromResult<ReadOnlyMemory<byte>>(File.ReadAllBytes(path)));
    }
}
