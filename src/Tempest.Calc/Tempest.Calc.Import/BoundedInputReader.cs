using System.Security.Cryptography;
using System.Text;

namespace Tempest.Calc.Import;

/// <summary>An untrusted file as read: its safe display name, its bytes (never more than the cap) and their SHA-256.</summary>
public sealed record ImportedFile(string FileName, byte[] Bytes, string Sha256)
{
    public int Length => Bytes.Length;
}

/// <summary>
/// The only way an untrusted engineering file enters the importers: a bounded, streaming read. The byte cap is enforced
/// while reading (a stream that lies about its length, or never ends, stops at the cap); a time limit and the caller's
/// cancellation token stop a stalled stream; text is decoded as strict UTF-8 (a UTF-8 byte order mark is dropped, UTF-16
/// and UTF-32 are refused with a message, an invalid byte sequence is refused), and every control character other than tab,
/// carriage return and line feed is refused.
/// </summary>
public static class BoundedInputReader
{
    private const int ChunkBytes = 64 * 1024;
    private static readonly UTF8Encoding Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Reads <paramref name="stream"/> to the end or to the cap, whichever comes first; past the cap the file is refused.</summary>
    public static async Task<ImportedFile> ReadAsync(Stream stream, string fileName, ImportLimits? limits = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        limits ??= ImportLimits.Default;

        if (stream.CanSeek)
        {
            long length;

            try
            {
                length = stream.Length;
            }
            catch (NotSupportedException)
            {
                length = -1;
            }

            if (length > limits.MaxBytes)
                throw TooLarge(limits);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(limits.Timeout);
        using var buffer = new MemoryStream();
        var chunk = new byte[ChunkBytes];

        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(chunk.AsMemory(), timeout.Token).ConfigureAwait(false);

                if (read == 0)
                    break;

                if (buffer.Length + read > limits.MaxBytes)
                    throw TooLarge(limits);

                buffer.Write(chunk, 0, read);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ImportRefusedException(ImportRefusalCode.Timeout, $"Reading the file took longer than {limits.Timeout.TotalSeconds:0.#} seconds, so it was stopped.");
        }

        return FromBytes(buffer.ToArray(), fileName, limits);
    }

    /// <summary>Reads a file from disk after canonicalising its path (see <see cref="ImportPath"/>).</summary>
    public static async Task<ImportedFile> ReadFileAsync(string path, string? requiredRoot = null, ImportLimits? limits = null, CancellationToken cancellationToken = default)
    {
        var full = ImportPath.CanonicaliseExistingFile(path, requiredRoot);
        var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, ChunkBytes, FileOptions.Asynchronous | FileOptions.SequentialScan);

        await using (stream.ConfigureAwait(false))
        {
            return await ReadAsync(stream, ImportPath.SafeLeafName(full), limits, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Wraps bytes already in memory (a test, a clipboard): the same cap and the same hash.</summary>
    public static ImportedFile FromBytes(byte[] bytes, string fileName, ImportLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        limits ??= ImportLimits.Default;

        if (bytes.Length > limits.MaxBytes)
            throw TooLarge(limits);

        return new ImportedFile(ImportPath.SafeLeafName(fileName), bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    /// <summary>
    /// Decodes a file as strict UTF-8 text. A UTF-8 byte order mark is dropped; UTF-16 and UTF-32 are refused; an invalid
    /// byte sequence is refused; a control character other than tab, carriage return or line feed is refused with its line.
    /// </summary>
    public static string DecodeText(ImportedFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var bytes = file.Bytes.AsSpan();

        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0 && bytes[3] == 0
            || bytes.Length >= 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xFE && bytes[3] == 0xFF
            || bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE
            || bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            throw new ImportRefusedException(ImportRefusalCode.InvalidEncoding, "The file is UTF-16 or UTF-32 text. Save it as UTF-8 and import it again.");

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            bytes = bytes[3..];

        string text;

        try
        {
            text = Strict.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new ImportRefusedException(ImportRefusalCode.InvalidEncoding, "The file is not valid UTF-8 text. Save it as UTF-8 and import it again.");
        }

        RejectControlCharacters(text);
        return text;
    }

    /// <summary>Refuses a control character other than tab, carriage return and line feed (including DEL and the C1 controls), naming the line.</summary>
    public static void RejectControlCharacters(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var line = 1;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (c == '\n')
            {
                line++;
                continue;
            }

            if (c is '\t' or '\r')
                continue;

            if (char.IsControl(c))
                throw new ImportRefusedException(ImportRefusalCode.ControlCharacter, $"The file holds a control character (U+{(int)c:X4}) on line {line}. Only tab, carriage return and line feed are allowed.");
        }
    }

    private static ImportRefusedException TooLarge(ImportLimits limits) =>
        new(ImportRefusalCode.TooLarge, $"The file is larger than {limits.MaxBytes / (1024.0 * 1024.0):0.#} MB and was not read.");
}
