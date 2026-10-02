using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Tempest.Core.Persistence;

namespace Tempest.Core.Invoicing.Xero.Sync;

/// <summary>
/// The durable <see cref="IXeroLinkStore"/> (`v0.24.0` B2, `ADR-0162`;
/// design §5): one JSON document per (tenant, document) in the
/// <see cref="IPersistenceStore"/> collection <see cref="Collection"/>,
/// under the key <c>{tenantId}/{kind}/{tempestKey}</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tenant-scoped.</b> Every read and write names the Xero organisation;
/// a link written for the Demo Company is invisible to the live
/// organisation and the other way round (D7).
/// </para>
/// <para>
/// <b>Schema versioning.</b> Every link is written at
/// <see cref="XeroLink.CurrentSchemaVersion"/>. A reader accepts every
/// version up to that one and ignores JSON properties it does not know. A
/// link written by a newer TempestOS (<see cref="IsFromNewerVersion"/>) is
/// returned as read — so the engine still sees "a link exists" and never
/// issues a create — and is never overwritten: <see cref="SaveAsync"/>
/// refuses it. When a newer link cannot be read at all, a placeholder with
/// an empty <see cref="XeroLink.XeroId"/> stands in for it, for the same
/// reason.
/// </para>
/// <para>
/// <b>Unreadable links.</b> A link of a known version that cannot be read
/// (corrupt JSON, a missing required field, a key that does not match its
/// content) fails <see cref="FindAsync"/> with a
/// <see cref="PersistenceException"/> rather than reading as "no link" —
/// which would let the engine create the Xero record a second time — and is
/// left out of <see cref="ListAsync"/>.
/// </para>
/// <para>
/// <b>Concurrency.</b> Writes are serialised per backing store (one gate
/// shared by every instance over the same <see cref="IPersistenceStore"/>),
/// so the "never change a link's Xero id" check and the write it guards are
/// one step.
/// </para>
/// </remarks>
public sealed class PersistenceXeroLinkStore : IXeroLinkStore
{
    /// <summary>The <see cref="IPersistenceStore"/> collection links live in.</summary>
    public const string Collection = "Xero.Links";

    /// <summary>The badge note for a link written by a newer TempestOS than this build.</summary>
    public const string NewerVersionNote = "written by a newer TempestOS";

    private readonly IPersistenceStore _store;
    private readonly SemaphoreSlim _gate;

    /// <summary>Initialises a new instance of the <see cref="PersistenceXeroLinkStore"/> class.</summary>
    /// <param name="store">The platform's persistence store.</param>
    public PersistenceXeroLinkStore(IPersistenceStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
        _gate = XeroStoreSupport.GateFor(store, Collection);
    }

    /// <summary>The persistence key of <paramref name="document"/>'s link in <paramref name="tenantId"/>: <c>{tenantId}/{kind}/{tempestKey}</c>.</summary>
    /// <param name="tenantId">The Xero organisation.</param>
    /// <param name="document">The TempestOS record.</param>
    public static string KeyFor(string tenantId, XeroDocumentRef document)
    {
        ValidateTenant(tenantId);
        ValidateDocument(document);

        return $"{tenantId}/{document.Kind}/{document.TempestKey}";
    }

    /// <summary>Whether <paramref name="link"/> was written by a newer TempestOS than this build — shown, never overwritten.</summary>
    /// <param name="link">The link as read.</param>
    public static bool IsFromNewerVersion(XeroLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        return link.SchemaVersion > XeroLink.CurrentSchemaVersion;
    }

    /// <inheritdoc />
    /// <exception cref="PersistenceException">A link exists for <paramref name="document"/> but cannot be read.</exception>
    public async Task<XeroLink?> FindAsync(string tenantId, XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        var key = KeyFor(tenantId, document);
        var json = await _store.ReadAsync(Collection, key, cancellationToken).ConfigureAwait(false);

        if (json is null)
            return null;

        return Parse(json, tenantId, document)
            ?? throw new PersistenceException($"The Xero link '{key}' in '{Collection}' cannot be read; it is left untouched. Unlink it to start again.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<XeroLink>> ListAsync(string tenantId, XeroDocumentKind? kind = null, CancellationToken cancellationToken = default)
    {
        ValidateTenant(tenantId);

        var prefix = kind is { } k ? $"{tenantId}/{k}/" : $"{tenantId}/";
        var keys = await _store.ListKeysAsync(Collection, cancellationToken).ConfigureAwait(false);
        var links = new List<XeroLink>();

        foreach (var key in keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal))
        {
            if (!TryParseKey(key, tenantId, out var document))
                continue;

            var json = await _store.ReadAsync(Collection, key, cancellationToken).ConfigureAwait(false);

            if (json is not null && Parse(json, tenantId, document) is { } link)
                links.Add(link);
        }

        return links;
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// The write would change the stored link's <see cref="XeroLink.XeroId"/>;
    /// or the stored link was written by a newer TempestOS, or cannot be
    /// read — neither is ever overwritten.
    /// </exception>
    public async Task SaveAsync(XeroLink link, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentException.ThrowIfNullOrWhiteSpace(link.XeroId);
        ArgumentException.ThrowIfNullOrWhiteSpace(link.LinkedBy);

        if (IsFromNewerVersion(link))
            throw new InvalidOperationException($"A Xero link at schema version {link.SchemaVersion} cannot be written by this build (version {XeroLink.CurrentSchemaVersion}).");

        var key = KeyFor(link.TenantId, link.Document);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var json = await _store.ReadAsync(Collection, key, cancellationToken).ConfigureAwait(false);

            JsonObject? original = null;

            if (json is not null)
            {
                var existing = Parse(json, link.TenantId, link.Document)
                    ?? throw new InvalidOperationException($"The stored Xero link '{key}' cannot be read, so it is not overwritten. Unlink it first.");

                if (IsFromNewerVersion(existing))
                    throw new InvalidOperationException($"The stored Xero link '{key}' was {NewerVersionNote} (schema version {existing.SchemaVersion}); it is not overwritten.");

                if (!string.Equals(existing.XeroId, link.XeroId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"The Xero link '{key}' already names Xero record '{existing.XeroId}'; it cannot be changed to '{link.XeroId}'. Unlink it first.");
                }

                original = JsonNode.Parse(json) as JsonObject;
            }

            // Properties a later build added within this schema version
            // (additive only) survive this build's rewrite.
            var stored = XeroStoreSupport.ToJson(link with { SchemaVersion = XeroLink.CurrentSchemaVersion }, original);
            await _store.WriteAsync(Collection, key, stored.ToJsonString(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task UnlinkAsync(string tenantId, XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        var key = KeyFor(tenantId, document);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _store.DeleteAsync(Collection, key, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Reads one stored link. <see langword="null"/> when it cannot be read
    /// at a version this build knows; a placeholder (empty
    /// <see cref="XeroLink.XeroId"/>) when a newer version cannot be read.
    /// </summary>
    internal static XeroLink? Parse(string json, string tenantId, XeroDocumentRef document)
    {
        if (XeroStoreSupport.ReadSchemaVersion(json) is not { } version || version < 1)
            return null;

        var newer = version > XeroLink.CurrentSchemaVersion;
        XeroLink? link;

        try
        {
            link = JsonSerializer.Deserialize<XeroLink>(json, XeroStoreSupport.JsonOptions);
        }
        catch (JsonException)
        {
            link = null;
        }

        if (link is not null && IsComplete(link) && string.Equals(link.TenantId, tenantId, StringComparison.Ordinal) && link.Document == document)
            return link with { SchemaVersion = version };

        return newer
            ? new XeroLink(version, tenantId, document, string.Empty, null, null, null, null, null, default, null, "unknown")
            : null;
    }

    private static bool IsComplete(XeroLink link) =>
        !string.IsNullOrWhiteSpace(link.TenantId)
        && link.Document is { TempestKey: { Length: > 0 } }
        && !string.IsNullOrWhiteSpace(link.XeroId)
        && !string.IsNullOrWhiteSpace(link.LinkedBy);

    private static bool TryParseKey(string key, string tenantId, out XeroDocumentRef document)
    {
        document = null!;
        var rest = key[(tenantId.Length + 1)..];
        var slash = rest.IndexOf('/', StringComparison.Ordinal);

        if (slash <= 0 || slash == rest.Length - 1)
            return false;

        if (!Enum.TryParse<XeroDocumentKind>(rest[..slash], ignoreCase: false, out var kind) || !Enum.IsDefined(kind))
            return false;

        document = new XeroDocumentRef(kind, rest[(slash + 1)..]);
        return true;
    }

    private static void ValidateTenant(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        if (tenantId.Contains('/', StringComparison.Ordinal))
            throw new ArgumentException("A Xero tenant id never contains '/'.", nameof(tenantId));
    }

    private static void ValidateDocument(XeroDocumentRef document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.TempestKey, nameof(document));
    }
}

/// <summary>What the B2 stores share: the JSON shape and the per-backing-store write gates.</summary>
internal static class XeroStoreSupport
{
    private static readonly ConditionalWeakTable<IPersistenceStore, Dictionary<string, SemaphoreSlim>> Gates = new();

    /// <summary>
    /// Enum values as names (stable if an enum gains a member), every other
    /// property by its C# name; unknown properties are ignored on read,
    /// which is <see cref="JsonSerializer"/>'s default.
    /// </summary>
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    /// <summary>The one write gate for <paramref name="collection"/> over <paramref name="store"/>, shared by every instance over it.</summary>
    internal static SemaphoreSlim GateFor(IPersistenceStore store, string collection)
    {
        var gates = Gates.GetValue(store, _ => new Dictionary<string, SemaphoreSlim>(StringComparer.Ordinal));

        lock (gates)
        {
            if (!gates.TryGetValue(collection, out var gate))
            {
                gate = new SemaphoreSlim(1, 1);
                gates[collection] = gate;
            }

            return gate;
        }
    }

    /// <summary>
    /// <paramref name="value"/> as a JSON object: its own properties over a
    /// copy of <paramref name="original"/>, so a property this build does not
    /// know is kept rather than dropped on rewrite.
    /// </summary>
    internal static JsonObject ToJson<T>(T value, JsonObject? original)
    {
        var node = JsonSerializer.SerializeToNode(value, JsonOptions)!.AsObject();

        if (original is null)
            return node;

        var merged = (JsonObject)original.DeepClone();

        foreach (var (name, property) in node)
            merged[name] = property?.DeepClone();

        return merged;
    }

    /// <summary>The stored document's <c>SchemaVersion</c>; <see langword="null"/> when it is not a JSON object or carries no integer version.</summary>
    internal static int? ReadSchemaVersion(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("SchemaVersion", out var version)
                || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out var value))
            {
                return null;
            }

            return value;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
