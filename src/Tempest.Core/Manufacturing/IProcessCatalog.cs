using Tempest.Core.ReferenceData;

namespace Tempest.Core.Manufacturing;

/// <summary>
/// The authoritative library of manufacturing process reference data (A7).
/// </summary>
/// <remarks>
/// <para>
/// Register, retrieve, revise, govern, read history and supersede all come
/// from <see cref="IReferenceDataCatalog{TDefinition}"/>, shared with every
/// other Group A library. What is added here is process-specific:
/// resolving a process by family and name, and the process query.
/// </para>
/// <para>
/// <b>What this contract deliberately does not offer.</b> No process
/// planning, no route generation, no process selection, no cost model, no
/// cycle-time estimation, no supplier capability. A7 supplies the reference
/// evidence those capabilities will consume.
/// </para>
/// </remarks>
public interface IProcessCatalog : IReferenceDataCatalog<ProcessDefinition>
{
    /// <summary>
    /// Returns the process registered under <paramref name="family"/>,
    /// <paramref name="name"/> and <paramref name="variant"/>, or
    /// <see langword="null"/> if none is.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<ProcessDefinition>?> FindByNameAsync(
        ProcessFamily family,
        string name,
        string? variant = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every registered process matching <paramref name="query"/>, in the
    /// same order <see cref="IReferenceDataCatalog{TDefinition}.ListAsync"/>
    /// uses. Never <see langword="null"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<ProcessDefinition>>> SearchAsync(ProcessQuery query, CancellationToken cancellationToken = default);
}
