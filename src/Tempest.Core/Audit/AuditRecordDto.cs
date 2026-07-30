namespace Tempest.Core.Audit;

/// <summary>
/// The plain, serialization-only shape <see cref="AuditRecorder"/> and
/// <see cref="AuditQuery"/> exchange with <see cref="Persistence.IPersistenceStore"/>.
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="AuditRecord"/> itself — the
/// public type validates its own arguments and is what every consumer
/// sees; this DTO is a serialization-only concern, kept private to this
/// namespace's own storage mechanics.
/// </remarks>
internal sealed record AuditRecordDto(string ActorId, string Action, DateTimeOffset OccurredAt, Dictionary<string, string> Detail);
