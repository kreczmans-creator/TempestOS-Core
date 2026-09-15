using System.Text.Json;

namespace Tempest.Core.ExportImport;

/// <summary>
/// The platform's own ready-to-use <see cref="IExportFormat"/>: frames every
/// section as a JSON array of <c>{ kind, schemaVersion, payload }</c>
/// objects, with each section's own opaque bytes carried as base64 —
/// matching this codebase's existing <see cref="System.Text.Json"/>
/// convention.
/// </summary>
public sealed class JsonExportFormat : IExportFormat
{
    /// <summary>
    /// The largest artifact <see cref="ReadAsync"/> will attempt to parse at
    /// all, checked before deserialization when the stream can report its
    /// own length.
    /// </summary>
    /// <remarks>
    /// `WP 21.5F` Offensive Security Audit, OSA-05: neither this nor
    /// <see cref="MaxSectionCount"/> existed before this fix — a hostile
    /// "export" file with an enormous base64 payload or millions of array
    /// entries was read entirely into memory with nothing to stop it. Not a
    /// classic *compressed* zip bomb (this format is a flat JSON array, not
    /// an archive — see the audit report's own note that no zip/archive
    /// extraction code exists anywhere in this codebase to have the
    /// path-traversal/entry-count problem the brief's item 5 names), but
    /// the same class of unbounded-resource-consumption risk. 500&#160;MB is
    /// generous for any real export this platform would ever produce.
    /// </remarks>
    private const long MaxArtifactBytes = 500_000_000;

    /// <summary>The most sections one artifact may declare — see <see cref="MaxArtifactBytes"/>'s own remarks.</summary>
    private const int MaxSectionCount = 10_000;

    /// <summary>
    /// Explicit rather than relying on <see cref="JsonSerializerOptions"/>'s
    /// own implicit default (`WP 21.5F` OSA-05, brief item 5: "<c>MaxDepth</c>
    /// on every <c>JsonSerializer</c>") — 64 matches that default exactly,
    /// so this changes nothing about what a legitimate artifact can carry;
    /// it makes the limit a reviewable, intentional decision rather than
    /// whatever the framework happens to default to.
    /// </summary>
    private static readonly JsonSerializerOptions Options = new() { MaxDepth = 64 };

    private sealed class Envelope
    {
        public string Kind { get; set; } = string.Empty;
        public int SchemaVersion { get; set; }
        public string Payload { get; set; } = string.Empty;
    }

    /// <inheritdoc />
    public async Task WriteAsync(IReadOnlyList<ExportSection> sections, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sections);
        ArgumentNullException.ThrowIfNull(destination);

        var envelopes = sections
            .Select(section => new Envelope
            {
                Kind = section.Kind,
                SchemaVersion = section.SchemaVersion,
                Payload = Convert.ToBase64String(section.Payload),
            })
            .ToList();

        await JsonSerializer.SerializeAsync(destination, envelopes, Options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExportSection>> ReadAsync(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.CanSeek && source.Length > MaxArtifactBytes)
        {
            throw new CorruptedExportArtifactException(
                $"the artifact is {source.Length:N0} bytes, more than this platform will import ({MaxArtifactBytes:N0} bytes).");
        }

        List<Envelope>? envelopes;

        try
        {
            envelopes = await JsonSerializer.DeserializeAsync<List<Envelope>>(source, Options, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new CorruptedExportArtifactException(ex.Message);
        }

        if (envelopes is null)
            throw new CorruptedExportArtifactException("the artifact deserialized to no content.");

        if (envelopes.Count > MaxSectionCount)
        {
            throw new CorruptedExportArtifactException(
                $"the artifact declares {envelopes.Count:N0} sections, more than this platform will import ({MaxSectionCount:N0}).");
        }

        try
        {
            return envelopes
                .Select(envelope => new ExportSection(envelope.Kind, envelope.SchemaVersion, Convert.FromBase64String(envelope.Payload)))
                .ToList();
        }
        catch (FormatException ex)
        {
            throw new CorruptedExportArtifactException($"a section's own payload is not valid base64 ({ex.Message}).");
        }
    }
}
