using System.Globalization;
using System.Text.Json;

namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// The durable, canonical persisted form of one engineering object's own
/// state (`TD-85`).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is not a second object model.</b> It is the serialisation of
/// the one canonical model — the same relationship
/// <c>EngineeringDocumentDto</c> has to <see cref="IEngineeringObject"/>'s
/// own backing document. A rehydrated object is a real
/// <see cref="IEngineeringObject"/> constructed through the normal factory
/// architecture; this record carries only what the constructor and the
/// object's own mutable fields need in order to come back as the same
/// object.
/// </para>
/// <para>
/// <b>Why it exists.</b> Before `TD-85`, <see cref="EngineeringObjectFactory{T}"/>
/// persisted only the document's own <c>Kind</c> and its first revision's
/// prose content. Everything that makes an object <i>that</i> object —
/// identifier, display name, metadata, lifecycle state, structural parent,
/// deletion, BOM line, transition history, attachments, and every
/// type-specific field — lived in constructor closures and in-memory
/// fields, and was lost on restart (`ADR-0077` disclosed exactly this
/// gap). A Kind-to-constructor map alone could never have closed it: the
/// constructor arguments themselves were never persisted.
/// </para>
/// <para>
/// <see cref="TypeState"/> is the concrete type's own contribution,
/// written by that type's <c>CaptureTypeState</c> override and read back
/// by that Kind's own rehydrator — so each type owns its own state,
/// rather than one central switch knowing every type's fields.
/// </para>
/// </remarks>
/// <param name="SchemaVersion">
/// The shape of this record, versioned (`TD-87`, `ADR-0120`). A record
/// written before this field existed has no such property; the store's
/// own read path normalises the resulting CLR default (<c>0</c>) to
/// <c>1</c> explicitly — this component is never relied upon to default
/// itself correctly. Always <see cref="Tempest.Core.EngineeringDomain.EngineeringObjectStateStore.CurrentSchemaVersion"/>
/// for a record captured by this build; a lower value only appears
/// transiently, before <see cref="Tempest.Core.EngineeringDomain.EngineeringObjectStateStore"/>'s
/// migration step runs.
/// </param>
/// <param name="Id">The object's own identity — the same <see cref="IEngineeringObject.Id"/> it had before restart.</param>
/// <param name="Kind">The object's own Kind, used to resolve its rehydrator.</param>
/// <param name="Identifier">The business identifier.</param>
/// <param name="DisplayName">The current display name — a rename is a state change, so this is the renamed value.</param>
/// <param name="Metadata">The object's own metadata facet.</param>
/// <param name="Status">The current lifecycle state.</param>
/// <param name="ParentId">The structural parent — the edge that makes an object belong to a project.</param>
/// <param name="IsDeleted">Whether the object has been soft-deleted.</param>
/// <param name="BomLine">The BOM line facet's own current values.</param>
/// <param name="History">Every recorded lifecycle transition, in order.</param>
/// <param name="Attachments">Every recorded attachment's own metadata.</param>
/// <param name="TypeState">The concrete type's own state, written and read by that type.</param>
public sealed record EngineeringObjectState(
    int SchemaVersion,
    Guid Id,
    string Kind,
    string? Identifier,
    string DisplayName,
    EngineeringObjectMetadata Metadata,
    LifecycleState Status,
    Guid? ParentId,
    bool IsDeleted,
    EngineeringObjectBomLineState BomLine,
    IReadOnlyList<EngineeringObjectTransitionState> History,
    IReadOnlyList<EngineeringObjectAttachmentState> Attachments,
    IReadOnlyDictionary<string, string?> TypeState)
{
    /// <summary>Reads one type-specific value, or <see langword="null"/> when that type never wrote it.</summary>
    public string? Type(string key) => TypeState.TryGetValue(key, out var value) ? value : null;

    /// <summary>Reads one type-specific <see cref="Guid"/>, or <see langword="null"/> when absent or unparseable.</summary>
    public Guid? TypeGuid(string key) => Guid.TryParse(Type(key), out var value) ? value : null;

    /// <summary>
    /// Reads one type-specific <see cref="Guid"/> a constructor requires,
    /// falling back to <see cref="Guid.Empty"/> — rehydration never fails
    /// to construct an object because one field is missing from an older
    /// record; it comes back with a visibly empty reference instead.
    /// </summary>
    public Guid TypeGuidOrEmpty(string key) => TypeGuid(key) ?? Guid.Empty;

    /// <summary>Reads one type-specific <see cref="DateTimeOffset"/>, or <see langword="null"/> when absent or unparseable.</summary>
    public DateTimeOffset? TypeDate(string key) =>
        DateTimeOffset.TryParse(Type(key), CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>Reads one type-specific string list, as written by <c>WriteList</c>.</summary>
    public IReadOnlyList<string> TypeList(string key)
    {
        var raw = Type(key);
        if (string.IsNullOrEmpty(raw))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(raw) ?? [];
        }
        catch (JsonException)
        {
            // A malformed list degrades to empty rather than failing the
            // whole rehydration (`TD-60`'s own established discipline).
            return [];
        }
    }

    /// <summary>Reads one type-specific <see cref="Guid"/> list.</summary>
    public IReadOnlyList<Guid> TypeGuidList(string key) =>
        TypeList(key).Select(v => Guid.TryParse(v, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList();

    /// <summary>Reads one type-specific value written by <c>WriteJson</c>, or <see langword="null"/> when absent or unreadable.</summary>
    public TValue? TypeJson<TValue>(string key)
    {
        var raw = Type(key);
        if (string.IsNullOrEmpty(raw))
            return default;

        try
        {
            return JsonSerializer.Deserialize<TValue>(raw);
        }
        catch (JsonException)
        {
            // Degrades to absent rather than failing the whole rehydration
            // (`TD-60`'s own established discipline for passive reads).
            return default;
        }
    }
}

/// <summary>The BOM line facet's own durable values (`TD-85`).</summary>
public sealed record EngineeringObjectBomLineState(
    decimal Quantity,
    string? UnitOfMeasure,
    string? FindNumber,
    string? ItemNumber,
    string? ReferenceDesignator)
{
    /// <summary>The values a never-configured BOM line carries.</summary>
    public static readonly EngineeringObjectBomLineState Default = new(1m, null, null, null, null);
}

/// <summary>One recorded lifecycle transition, durably (`TD-85`).</summary>
public sealed record EngineeringObjectTransitionState(
    LifecycleState From,
    LifecycleState To,
    string ActorPrincipalId,
    DateTimeOffset OccurredAt,
    Guid? ApprovalId);

/// <summary>One recorded attachment's own metadata, durably (`TD-85`).</summary>
/// <param name="Id">The attachment's identity, stable across restarts.</param>
/// <param name="FileName">The file's own name.</param>
/// <param name="ContentType">The file's MIME type.</param>
/// <param name="SizeInBytes">The size of the content this attachment describes.</param>
/// <param name="ContentHash">
/// The SHA-256 of the bytes held for this attachment, or
/// <see langword="null"/> when no content is stored (`TD-31`).
/// </param>
/// <remarks>
/// <paramref name="ContentHash"/> is optional with a <see langword="null"/>
/// default so a state record written before `TD-31` still deserialises:
/// the absent property reads back as <see langword="null"/>, which is
/// exactly what it means — an attachment whose content this platform never
/// held and whose bytes therefore cannot be verified. That the schema
/// carries no version of its own remains `TD-87`; this field was chosen to
/// be additive so it does not need one.
/// </remarks>
public sealed record EngineeringObjectAttachmentState(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeInBytes,
    string? ContentHash = null);
