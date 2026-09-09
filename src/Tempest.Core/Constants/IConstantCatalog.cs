using Tempest.Core.ReferenceData;

namespace Tempest.Core.Constants;

/// <summary>
/// The authoritative library of engineering constants and fundamentals
/// (A6).
/// </summary>
/// <remarks>
/// <para>
/// Register, retrieve, revise, govern, read history and supersede all come
/// from <see cref="IReferenceDataCatalog{TDefinition}"/>, shared with every
/// other Group A library. What is added here is constants-specific:
/// resolving a constant by its own symbol, the constants query, and the
/// <see cref="IReleasedConstantSource"/> seam a future calculation
/// capability consumes.
/// </para>
/// <para>
/// <b>What this contract deliberately does not offer.</b> No expression
/// evaluation, no unit-system conversion policy, no uncertainty
/// propagation, no arithmetic on constants of any kind. A6 records
/// constants; using them is somebody else's job.
/// </para>
/// </remarks>
public interface IConstantCatalog : IReferenceDataCatalog<ConstantDefinition>, IReleasedConstantSource
{
    /// <summary>
    /// Returns the constant registered under <paramref name="symbol"/>
    /// whatever its validation state, or <see langword="null"/> if none is.
    /// </summary>
    /// <remarks>
    /// The librarian's lookup, not the calculation's:
    /// <see cref="IReleasedConstantSource.FindReleasedAsync"/> is the one a
    /// consumer of constants should use, because it will not hand back a
    /// value nobody has verified.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<ConstantDefinition>?> FindBySymbolAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every registered constant matching <paramref name="query"/>, in the
    /// same order <see cref="IReferenceDataCatalog{TDefinition}.ListAsync"/>
    /// uses. Never <see langword="null"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<ConstantDefinition>>> SearchAsync(ConstantQuery query, CancellationToken cancellationToken = default);
}
