namespace Tempest.Core.ReferenceData;

/// <summary>
/// The base exception thrown when a Group A reference-data operation fails.
/// </summary>
/// <remarks>
/// One family for every P01 library, rather than a near-identical family
/// per library. A caller catching this catches a reference-data failure
/// from Materials, Standards, Fasteners, Bearings, Components, Constants
/// or Manufacturing alike; a caller needing to distinguish reads
/// <see cref="Library"/> or catches the specific subtype. Mirrors
/// <see cref="Persistence.PersistenceException"/>'s own base-plus-subtype
/// shape — <c>public class</c>, not <see langword="abstract"/>, matching
/// this codebase's own universal convention.
/// </remarks>
public class ReferenceDataException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ReferenceDataException"/> class.
    /// </summary>
    /// <param name="library">The reference library the failure came from (e.g. <c>"Bearings"</c>).</param>
    /// <param name="message">A message describing the failure.</param>
    public ReferenceDataException(string library, string message)
        : base(message)
    {
        Library = library;
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="ReferenceDataException"/> class.
    /// </summary>
    /// <param name="library">The reference library the failure came from.</param>
    /// <param name="message">A message describing the failure.</param>
    /// <param name="innerException">The underlying failure.</param>
    public ReferenceDataException(string library, string message, Exception innerException)
        : base(message, innerException)
    {
        Library = library;
    }

    /// <summary>Gets the reference library the failure came from.</summary>
    public string Library { get; }
}
