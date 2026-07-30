using System.Text.Json;

namespace Tempest.Core.ExportImport;

/// <summary>
/// The platform's own ready-to-use <see cref="IExportFormat"/>: frames every
/// section as a JSON array of <c>{ kind, schemaVersion, payload }</c>
/// objects, with each section's own opaque bytes carried as base64 —
/// matching this codebase's existing <see cref="System.Text.Json"/>
/// convention (<see cref="Repositories.JsonProjectRepository"/>).
/// </summary>
public sealed class JsonExportFormat : IExportFormat
{
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

        await JsonSerializer.SerializeAsync(destination, envelopes, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExportSection>> ReadAsync(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        List<Envelope>? envelopes;

        try
        {
            envelopes = await JsonSerializer.DeserializeAsync<List<Envelope>>(source, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new CorruptedExportArtifactException(ex.Message);
        }

        if (envelopes is null)
            throw new CorruptedExportArtifactException("the artifact deserialized to no content.");

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
