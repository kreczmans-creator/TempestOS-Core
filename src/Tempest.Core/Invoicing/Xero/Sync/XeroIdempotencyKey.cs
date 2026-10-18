using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Tempest.Core.Invoicing.Xero.Sync;

/// <summary>
/// Builds the <c>Idempotency-Key</c> header value an outbox entry carries
/// on every attempt (`v0.24.0` B2, `ADR-0162` decision 4; design §6.4
/// item 2).
/// </summary>
/// <remarks>
/// <para>
/// <b>Shape.</b> <c>tos:{kind}:{key}:{operation}:{hash}</c> — for example
/// <c>tos:Quote:3f2b…:PushQuote:9a1c…</c> — where <c>{hash}</c> is the
/// entry's content hash verbatim for the first entry of that content with
/// no argument, and otherwise a SHA-256 (lower-case hex) over the content
/// hash, the argument and the occurrence. When that form would be longer
/// than <see cref="XeroOutboxEntry.MaximumIdempotencyKeyLength"/> (128)
/// characters, or the TempestOS key or content hash holds a character
/// outside <c>[A-Za-z0-9._-]</c>, the key is shortened to
/// <c>tos:{kind}:{operation}:{sha256(full form)}</c> — at most 101
/// characters, never ambiguous with the long form (four segments, not
/// five).
/// </para>
/// <para>
/// <b>Deterministic.</b> The same inputs always give the same key, in any
/// process, on any machine — no clock, no randomness, no culture. Xero
/// replays its cached response for a repeated key and answers 400 when a
/// key is reused with a different body (S7); embedding the content hash
/// makes the second impossible by construction.
/// </para>
/// <para>
/// <b>Argument and occurrence.</b> Two entries for the same document and
/// operation that differ only in their argument (a quote's
/// <c>SetQuoteStatus</c> to <c>SENT</c>, then to <c>ACCEPTED</c>, with the
/// same content) must not share a key, or Xero would replay the first
/// response for the second. Likewise content pushed, changed, and then
/// changed back is a new write — <paramref name="occurrence"/> (how many
/// earlier entries the outbox already holds for the same document,
/// operation, argument and content) keeps it from replaying the first
/// push's cached response.
/// </para>
/// </remarks>
public static class XeroIdempotencyKey
{
    /// <summary>Every key TempestOS sends starts with this prefix, so a key seen in Xero's logs is recognisably ours.</summary>
    public const string Prefix = "tos:";

    /// <summary>
    /// The key for one outbox entry.
    /// </summary>
    /// <param name="document">The TempestOS record the entry is about.</param>
    /// <param name="operation">What the entry does.</param>
    /// <param name="contentHash">The hash of the content the entry pushes (<see cref="XeroOutboxEntry.ContentHash"/>).</param>
    /// <param name="argument">The entry's argument (<see cref="XeroOutboxEntry.Argument"/>), or <see langword="null"/>.</param>
    /// <param name="occurrence">How many earlier entries for the same document, operation, argument and content exist; <c>0</c> for the first.</param>
    /// <returns>A key of at most <see cref="XeroOutboxEntry.MaximumIdempotencyKeyLength"/> ASCII characters.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="contentHash"/> or the document's key is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="occurrence"/> is negative.</exception>
    public static string Create(
        XeroDocumentRef document, XeroOperation operation, string contentHash, string? argument = null, int occurrence = 0)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.TempestKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        ArgumentOutOfRangeException.ThrowIfNegative(occurrence);

        var kind = document.Kind.ToString();
        var op = operation.ToString();

        var hashSegment = argument is null && occurrence == 0
            ? contentHash
            : Sha256Hex(string.Join(
                '\u001f',
                contentHash,
                argument is null ? "\u0000" : "=" + argument,
                occurrence.ToString(CultureInfo.InvariantCulture)));

        var full = $"{Prefix}{kind}:{document.TempestKey}:{op}:{hashSegment}";

        if (full.Length <= XeroOutboxEntry.MaximumIdempotencyKeyLength && IsSafe(document.TempestKey) && IsSafe(hashSegment))
            return full;

        return $"{Prefix}{kind}:{op}:{Sha256Hex(full)}";
    }

    /// <summary>
    /// Whether <paramref name="key"/> is a well-formed key this builder could
    /// have produced: starts with <see cref="Prefix"/>, at most
    /// <see cref="XeroOutboxEntry.MaximumIdempotencyKeyLength"/> characters,
    /// printable ASCII only.
    /// </summary>
    /// <param name="key">The key to check.</param>
    public static bool IsWellFormed(string? key) =>
        key is not null
        && key.StartsWith(Prefix, StringComparison.Ordinal)
        && key.Length <= XeroOutboxEntry.MaximumIdempotencyKeyLength
        && key.All(c => c is > ' ' and <= '~');

    private static bool IsSafe(string segment)
    {
        foreach (var c in segment)
        {
            if (c is not ((>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-' or '_' or '.'))
                return false;
        }

        return true;
    }

    private static string Sha256Hex(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
