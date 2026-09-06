namespace Tempest.Core.Materials;

/// <summary>
/// The one field <see cref="MaterialCatalogReconciliationService"/> needs
/// out of a stored material document: the record Id an orphaned document
/// would have to be re-indexed under.
/// </summary>
/// <remarks>
/// A deliberately partial read. The reconciliation sweep is looking at
/// documents whose index entry is missing, which is to say documents it
/// already has reason to distrust; deserialising the whole record — and
/// failing on any part of it — would defeat the sweep for exactly the
/// documents it exists to find. <c>System.Text.Json</c> ignores the fields
/// this shape omits, so a document that is intact in the one field that
/// matters is still repairable.
/// </remarks>
internal sealed record MaterialRecordIdentityProbe(string? RecordId);
