namespace Tempest.Core.ReferenceData.Seeding;

/// <summary>
/// One record a seed dataset offers to a reference library: the identity
/// it will be registered under, the engineering description itself, and
/// where that description came from.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately the same three arguments
/// <see cref="IReferenceDataCatalog{TDefinition}.RegisterAsync"/> already
/// takes, and nothing more. A seed record carries no validation state and
/// no revision number, because it is not entitled to either: registration
/// starts every record at <see cref="ReferenceValidationState.Draft"/>,
/// and a record earns any further status from a person, never from the
/// dataset that supplied it.
/// </para>
/// </remarks>
/// <typeparam name="TDefinition">The library's own definition type.</typeparam>
/// <param name="RecordId">The stable identity the record is registered under. Stable across runs, so re-seeding recognises what it already wrote.</param>
/// <param name="Definition">The engineering description itself.</param>
/// <param name="Provenance">Where <paramref name="Definition"/>'s own values came from.</param>
public sealed record ReferenceSeedRecord<TDefinition>(
    string RecordId,
    TDefinition Definition,
    ReferenceProvenance Provenance);
