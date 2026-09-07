using Tempest.Core.ReferenceData;

namespace Tempest.Core.Standards;

/// <summary>
/// The authoritative register of engineering standards (A2).
/// </summary>
/// <remarks>
/// <para>
/// Register, retrieve, revise, govern, read history and supersede all come
/// from <see cref="IReferenceDataCatalog{TDefinition}"/>, shared with every
/// other Group A library. What is added here is standards-specific:
/// resolving a standard by its own designation, and the standards query.
/// </para>
/// <para>
/// Implements <see cref="IStandardResolver"/>, the narrow seam every other
/// Group A library uses to confirm its own citations resolve. That is the
/// only thing A2 exposes to its peers, and it is declared in the shared
/// layer rather than here so that no citing library takes a compile-time
/// dependency on A2.
/// </para>
/// <para>
/// <b>What this contract deliberately does not offer.</b> No standard text,
/// no clause content, no conformity assessment, no statement that anything
/// complies with anything, and no advice on which standard to apply. A2
/// records that standards exist and what their publishers say about them.
/// </para>
/// </remarks>
public interface IStandardCatalog : IReferenceDataCatalog<StandardDefinition>, IStandardResolver
{
    /// <summary>
    /// Returns the standard registered under <paramref name="bodyCode"/>,
    /// <paramref name="designation"/> and <paramref name="edition"/>, or
    /// <see langword="null"/> if none is.
    /// </summary>
    /// <remarks>
    /// The edition is part of the identity. Omitting it looks up the record
    /// registered without an edition, which is a different record from any
    /// dated edition — not "the latest edition", which A2 has no authority
    /// to pick.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="bodyCode"/> or <paramref name="designation"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<StandardDefinition>?> FindByDesignationAsync(
        string bodyCode,
        string designation,
        string? edition = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every registered edition of one standard, in ascending record-Id
    /// order. Never <see langword="null"/>; empty where none is registered.
    /// </summary>
    /// <remarks>
    /// Deliberately returns them all rather than picking one: which edition
    /// applies to a given design is a contractual and regulatory question,
    /// not one A2 can answer.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="bodyCode"/> or <paramref name="designation"/> is null, empty, or whitespace.</exception>
    Task<IReadOnlyList<IReferenceRecord<StandardDefinition>>> FindEditionsAsync(
        string bodyCode,
        string designation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every registered standard matching <paramref name="query"/>, in the
    /// same order <see cref="IReferenceDataCatalog{TDefinition}.ListAsync"/>
    /// uses. Never <see langword="null"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<StandardDefinition>>> SearchAsync(StandardQuery query, CancellationToken cancellationToken = default);
}
