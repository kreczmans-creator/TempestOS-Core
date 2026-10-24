namespace Tempest.Calc.Library;

/// <summary>A revision line of a pack.</summary>
public sealed record CalcPackRevision(string Revision, DateTimeOffset Date, string Author, string Description);

/// <summary>
/// A calc pack: several instances issued under one issue sheet with an
/// index and a revision table. Serialised as JSON into <c>Calc.Packs</c>.
/// </summary>
public sealed record CalcPack(
    Guid Id,
    Guid? ProjectId,
    string Title,
    string Revision,
    IReadOnlyList<Guid> InstanceIds,
    IReadOnlyList<CalcPackRevision> Revisions,
    CalcInstanceStatus Status,
    Guid? EvidenceId,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? IssuedAt = null,
    string? IssueReference = null,
    string? SheetAttachmentId = null)
{
    public const int MaxInstances = 50;
}
