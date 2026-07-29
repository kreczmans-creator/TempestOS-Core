using Tempest.Core.ExportImport;

namespace Tempest.Core.Tests.ExportImport;

/// <summary>
/// A configurable <see cref="IExportable"/>/<see cref="IExportableKind"/>
/// that records whether it was invoked, and either writes fixed bytes or
/// throws a caller-supplied exception.
/// </summary>
internal sealed class RecordingExportable : IExportable, IExportableKind
{
    private readonly byte[] _data;
    private readonly Exception? _throwOnExport;

    public RecordingExportable(string kind, int schemaVersion, byte[] data, Exception? throwOnExport = null)
    {
        Kind = kind;
        SchemaVersion = schemaVersion;
        _data = data;
        _throwOnExport = throwOnExport;
    }

    public string Kind { get; }
    public int SchemaVersion { get; }
    public bool ExportCalled { get; private set; }

    public Task ExportAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ExportCalled = true;

        if (_throwOnExport is not null)
            throw _throwOnExport;

        return destination.WriteAsync(_data, cancellationToken).AsTask();
    }
}

/// <summary>
/// An <see cref="IExportable"/> deliberately not implementing
/// <see cref="IExportableKind"/>, used to prove <c>ExportService</c> falls
/// back to a source's own runtime type name as its section's kind.
/// </summary>
internal sealed class UnkeyedRecordingExportable : IExportable
{
    private readonly byte[] _data;

    public UnkeyedRecordingExportable(int schemaVersion, byte[] data)
    {
        SchemaVersion = schemaVersion;
        _data = data;
    }

    public int SchemaVersion { get; }

    public Task ExportAsync(Stream destination, CancellationToken cancellationToken = default) =>
        destination.WriteAsync(_data, cancellationToken).AsTask();
}

/// <summary>
/// A configurable <see cref="IImportable"/> that records every payload it
/// receives, and either returns normally or throws a caller-supplied
/// exception.
/// </summary>
internal sealed class RecordingImportable : IImportable
{
    private readonly Exception? _throwOnImport;

    public RecordingImportable(string kind, int schemaVersion, Exception? throwOnImport = null)
    {
        Kind = kind;
        SchemaVersion = schemaVersion;
        _throwOnImport = throwOnImport;
    }

    public string Kind { get; }
    public int SchemaVersion { get; }
    public List<byte[]> ReceivedPayloads { get; } = [];

    public async Task ImportAsync(Stream payload, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await payload.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        ReceivedPayloads.Add(buffer.ToArray());

        if (_throwOnImport is not null)
            throw _throwOnImport;
    }
}

/// <summary>
/// A <see cref="Stream"/> wrapper that throws a caller-supplied
/// <see cref="IOException"/> on every write or read, used to prove a
/// caller-supplied stream's own failure propagates unmodified.
/// </summary>
internal sealed class ThrowingStream : Stream
{
    private readonly bool _throwOnWrite;
    private readonly bool _throwOnRead;

    public ThrowingStream(bool throwOnWrite = false, bool throwOnRead = false)
    {
        _throwOnWrite = throwOnWrite;
        _throwOnRead = throwOnRead;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_throwOnRead)
            throw new IOException("Simulated read failure.");

        return 0;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (_throwOnWrite)
            throw new IOException("Simulated write failure.");
    }
}
