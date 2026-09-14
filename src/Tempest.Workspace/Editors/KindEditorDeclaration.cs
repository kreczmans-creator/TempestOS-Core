namespace Tempest.Workspace.Editors;

/// <summary>
/// How one field in a <see cref="KindEditorDeclaration"/> is presented —
/// what kind of control the Object Editor renders for it (`WP 18.2A`).
/// </summary>
public enum EditorControlKind
{
    /// <summary>A single-line text value.</summary>
    Text,

    /// <summary>A multi-line text value.</summary>
    MultilineText,

    /// <summary>A value chosen from a fixed, named set (an enum's own values).</summary>
    Choice,

    /// <summary>A typed quantity — a number and a unit picked from the unit catalogue for its dimension.</summary>
    QuantityWithUnit,

    /// <summary>A reference to another engineering object, shown by name with a link that opens it.</summary>
    ObjectReference,

    /// <summary>A read-only list of rows, each naming its own facts.</summary>
    ReadOnlyList,

    /// <summary>The object's own attached files.</summary>
    Attachments,

    /// <summary>The object's own lifecycle position and history.</summary>
    Lifecycle,
}

/// <summary>
/// One field a <see cref="KindEditorDeclaration"/> names — its label, how
/// it is controlled, whether it can be edited here, and (when it can) the
/// Workspace command that writes it (`WP 18.2A`).
/// </summary>
/// <param name="Label">The field's own label, as shown beside its control.</param>
/// <param name="ControlKind">What kind of control renders this field.</param>
/// <param name="Editable">Whether this field can be changed from this editor. A read-only field (e.g. <em>Where used</em>, a Lifecycle facet the object's own status transitions govern instead) is still shown, never omitted for being uneditable.</param>
/// <param name="WriteCommandId">The <c>CommandDescriptor.Id</c> of the Workspace command that writes this field, when <paramref name="Editable"/> is <see langword="true"/>. <see langword="null"/> for a read-only field, or where the write is the object's own generic Rename/Revise rather than a dedicated command.</param>
public sealed record EditorFieldDeclaration(
    string Label,
    EditorControlKind ControlKind,
    bool Editable,
    string? WriteCommandId = null);

/// <summary>
/// One named, ordered group of fields within a <see cref="KindEditorDeclaration"/>
/// (`WP 18.2A`).
/// </summary>
/// <param name="Key">A stable key the Object Editor matches against to decide how to render this section — see <see cref="EditorSectionKeys"/> for the well-known set.</param>
/// <param name="Title">The section's own heading, as shown on screen.</param>
/// <param name="Fields">The fields this section declares, in display order.</param>
public sealed record EditorSectionDeclaration(string Key, string Title, IReadOnlyList<EditorFieldDeclaration> Fields)
{
    /// <summary>Builds a section from an inline field list.</summary>
    public EditorSectionDeclaration(string key, string title, params EditorFieldDeclaration[] fields)
        : this(key, title, (IReadOnlyList<EditorFieldDeclaration>)fields)
    {
    }
}

/// <summary>
/// The well-known <see cref="EditorSectionDeclaration.Key"/> values the
/// Object Editor's own declaration-driven rendering recognises
/// (`WP 18.2A`). A declaration built from other keys still renders — the
/// section's own title and field list — but only a recognised key gets the
/// section's real, working content (attachments, citations, and so on)
/// rather than a plain field list.
/// </summary>
public static class EditorSectionKeys
{
    /// <summary>The object's own identity — name and identifier readout.</summary>
    public const string Identity = "identity";

    /// <summary>The object's own descriptive metadata — owner, discipline, classification, tags, notes.</summary>
    public const string Description = "description";

    /// <summary>The object's own free-text content, revised via <c>IWorkspaceManager.ReviseObjectAsync</c>.</summary>
    public const string Content = "content";

    /// <summary>The assembly (or other structure) this object sits in — read-only, named, linked.</summary>
    public const string WhereUsed = "where-used";

    /// <summary>This object's own Bill-of-Materials line — how it is used within its parent's assembly.</summary>
    public const string BillOfMaterials = "bill-of-materials";

    /// <summary>The object's own attached files.</summary>
    public const string Attachments = "attachments";

    /// <summary>Evidence's own attached files, shown with size and hash.</summary>
    public const string Files = "files";

    /// <summary>Evidence's own subject tag — the Part, Assembly, Requirement or Deliverable it is about.</summary>
    public const string Subject = "subject";

    /// <summary>Evidence's own citations — the governed reference records it stood on.</summary>
    public const string Citations = "citations";

    /// <summary>Evidence's own declared figures — named, typed quantities.</summary>
    public const string DeclaredFigures = "declared-figures";

    /// <summary>The object's own lifecycle position and history.</summary>
    public const string Lifecycle = "lifecycle";

    /// <summary>Evidence's own check and issue records, read-only (the Check and Issue actions themselves are `WP 18.2B`).</summary>
    public const string CheckAndIssue = "check-and-issue";

    /// <summary>The object's own audit trail.</summary>
    public const string Audit = "audit";
}

/// <summary>
/// The Object Editor's own declaration for one canonical Kind: an ordered
/// list of sections, each an ordered list of fields (`WP 18.2A`). Where a
/// Kind has no registered declaration, the Object Editor falls back to its
/// existing generic rendering — unchanged, and still what every Kind used
/// before this Work Package.
/// </summary>
public sealed class KindEditorDeclaration
{
    /// <summary>Initialises a new instance of the <see cref="KindEditorDeclaration"/> class.</summary>
    /// <param name="kind">The canonical Kind this declaration renders.</param>
    /// <param name="sections">This Kind's own sections, in display order.</param>
    public KindEditorDeclaration(string kind, IReadOnlyList<EditorSectionDeclaration> sections)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(sections);

        Kind = kind;
        Sections = sections;
    }

    /// <summary>The canonical Kind this declaration renders.</summary>
    public string Kind { get; }

    /// <summary>This Kind's own sections, in display order.</summary>
    public IReadOnlyList<EditorSectionDeclaration> Sections { get; }

    /// <summary>Whether this declaration names a section under <paramref name="key"/> (one of <see cref="EditorSectionKeys"/>).</summary>
    public bool HasSection(string key) => Sections.Any(s => string.Equals(s.Key, key, StringComparison.Ordinal));
}

/// <summary>
/// Resolves the registered <see cref="KindEditorDeclaration"/> for a Kind,
/// if one exists (`WP 18.2A`) — a registry keyed by Kind, mirroring
/// <c>IWorkspaceManager</c>'s own <c>RegisterFacetProvider</c>/Kind-keyed
/// shape.
/// </summary>
public interface IKindEditorDeclarationRegistry
{
    /// <summary>Registers <paramref name="declaration"/> under its own <see cref="KindEditorDeclaration.Kind"/>.</summary>
    /// <exception cref="ArgumentException">A declaration is already registered for this Kind.</exception>
    void Register(KindEditorDeclaration declaration);

    /// <summary>The declaration registered for <paramref name="kind"/>, or <see langword="null"/> if none is.</summary>
    KindEditorDeclaration? For(string? kind);
}

/// <summary>The one <see cref="IKindEditorDeclarationRegistry"/> implementation.</summary>
public sealed class KindEditorDeclarationRegistry : IKindEditorDeclarationRegistry
{
    private readonly Dictionary<string, KindEditorDeclaration> _byKind = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Register(KindEditorDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        if (!_byKind.TryAdd(declaration.Kind, declaration))
            throw new ArgumentException($"A Kind editor declaration is already registered for '{declaration.Kind}'.", nameof(declaration));
    }

    /// <inheritdoc />
    public KindEditorDeclaration? For(string? kind) =>
        kind is not null && _byKind.TryGetValue(kind, out var declaration) ? declaration : null;
}
