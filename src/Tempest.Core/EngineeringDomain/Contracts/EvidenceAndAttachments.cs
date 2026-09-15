namespace Tempest.Core.EngineeringDomain;

/// <summary>A composed, read-side traversal result — never a stored relationship (WP8.2A Relationship Catalogue §5).</summary>
public interface IEvidence
{
    Guid SubjectId { get; }
    IReadOnlyList<IEngineeringRelationship> SupportingRelationships { get; }
    IReadOnlyList<IVerificationResult> VerificationResults { get; }
    IReadOnlyList<ICalculationResult> CalculationResults { get; }
}

public interface IAttachment
{
    Guid Id { get; }
    string FileName { get; }
    string ContentType { get; }
    long SizeInBytes { get; }

    /// <summary>
    /// The SHA-256 of the bytes <see cref="IAttachmentContentStore"/> holds
    /// for this attachment, as lowercase hex — or <see langword="null"/>
    /// when this platform holds no content for it (`TD-31`).
    /// </summary>
    /// <remarks>
    /// Metadata <em>about</em> the content, deliberately not the content:
    /// it is what lets a read verify that the bytes that came back are the
    /// bytes that went in, and it keeps an object's state small enough to
    /// rehydrate a whole graph without loading a single file.
    /// <see langword="null"/> is a legitimate, permanent state — an
    /// attachment that describes a file this platform does not hold, which
    /// is every attachment created before `TD-31` and any created as
    /// metadata alone since.
    /// </remarks>
    string? ContentHash { get; }
}

/// <summary>A markup shape an <see cref="AttachmentAnnotation"/> draws (`TD-98`).</summary>
public enum AnnotationTool
{
    /// <summary>An axis-aligned rectangle, two opposite corners.</summary>
    Rectangle,

    /// <summary>An ellipse inscribed in a bounding box, two opposite corners.</summary>
    Ellipse,

    /// <summary>A freehand line, one point per recorded pointer position.</summary>
    Freehand,

    /// <summary>A straight arrow from a start point to an end point, head at the end.</summary>
    Arrow,

    /// <summary>A single anchor point carrying a short note (<see cref="AttachmentAnnotation.Text"/>).</summary>
    TextNote,
}

/// <summary>
/// One point of an <see cref="AttachmentAnnotation"/>'s own geometry, in the
/// page's native, unrotated content units — the same units the viewer's own
/// page source reports for a page's natural size (`TD-98`). Never in
/// rendered pixels or the viewer's current
/// zoom/rotation: a stroke drawn at 400% zoom on a page rotated 90° must
/// read back in exactly the same place once the viewer is back at 100% and
/// unrotated, and units tied to the view the stroke happened to be drawn
/// under could not promise that.
/// </summary>
public readonly record struct AnnotationPoint(double X, double Y);

/// <summary>
/// One piece of markup on one page of one attachment (`TD-98`) — a small,
/// immutable record kept <b>beside</b> the attachment it marks up, never by
/// rewriting the attachment's own bytes (`TD-31`'s content-addressed store
/// is untouched by every annotation write).
/// </summary>
/// <param name="Id">This annotation's own identity, stable across restarts.</param>
/// <param name="AttachmentId">The attachment this annotation marks up.</param>
/// <param name="PageIndex">The zero-based page this annotation is on — the same convention the viewer's own page sources use.</param>
/// <param name="Tool">What shape this annotation is.</param>
/// <param name="Points">
/// This annotation's own geometry, in the page's native content units:
/// two opposite corners for <see cref="AnnotationTool.Rectangle"/> and
/// <see cref="AnnotationTool.Ellipse"/>; start and end for
/// <see cref="AnnotationTool.Arrow"/>; every recorded pointer position, in
/// order, for <see cref="AnnotationTool.Freehand"/>; one anchor point for
/// <see cref="AnnotationTool.TextNote"/>.
/// </param>
/// <param name="ColorHex">The stroke colour, as <c>#RRGGBB</c> — one of the design tokens' own swatches.</param>
/// <param name="Text">The note's own text for <see cref="AnnotationTool.TextNote"/>; <see langword="null"/> for every other tool.</param>
/// <param name="CreatedAtUtc">When this annotation was recorded.</param>
/// <param name="CreatedByPrincipalId">Who recorded it.</param>
public sealed record AttachmentAnnotation(
    Guid Id,
    Guid AttachmentId,
    int PageIndex,
    AnnotationTool Tool,
    IReadOnlyList<AnnotationPoint> Points,
    string ColorHex,
    string? Text,
    DateTimeOffset CreatedAtUtc,
    string CreatedByPrincipalId);

/// <summary>
/// The markup-and-annotation facet (`TD-98`) — composed alongside
/// <see cref="IHasAttachments"/> on every engineering object, since an
/// annotation only ever exists on a page of an attachment that object
/// already owns.
/// </summary>
/// <remarks>
/// Every write is one transaction with an audit row, exactly like every
/// other mutator on this platform (`ADR-0145`); annotations rehydrate with
/// their owner and are exported with it, because they live in the owner's
/// own durable state rather than in a second store.
/// </remarks>
public interface IHasAttachmentAnnotations
{
    /// <summary>Records a new annotation on <paramref name="attachmentId"/>'s <paramref name="pageIndex"/>.</summary>
    Task<AttachmentAnnotation> AddAttachmentAnnotationAsync(
        Guid attachmentId,
        int pageIndex,
        AnnotationTool tool,
        IReadOnlyList<AnnotationPoint> points,
        string colorHex,
        string? text,
        CancellationToken cancellationToken = default);

    /// <summary>Every annotation recorded on <paramref name="attachmentId"/>, across every page.</summary>
    Task<IReadOnlyList<AttachmentAnnotation>> GetAttachmentAnnotationsAsync(
        Guid attachmentId, CancellationToken cancellationToken = default);

    /// <summary>Removes one annotation by its own id. Does nothing if it is not there (already removed).</summary>
    Task DeleteAttachmentAnnotationAsync(Guid annotationId, CancellationToken cancellationToken = default);

    /// <summary>Removes every annotation on <paramref name="attachmentId"/>'s <paramref name="pageIndex"/>.</summary>
    Task ClearAttachmentAnnotationsAsync(Guid attachmentId, int pageIndex, CancellationToken cancellationToken = default);
}
