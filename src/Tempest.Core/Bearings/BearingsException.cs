namespace Tempest.Core.Bearings;

/// <summary>
/// The base exception thrown when a Bearing Library operation fails.
/// </summary>
/// <remarks>
/// Mirrors <see cref="Materials.MaterialsException"/>'s own
/// base-plus-subtype pattern exactly, itself mirroring
/// <see cref="EngineeringData.EngineeringDataException"/> and
/// <see cref="Persistence.PersistenceException"/> — <c>public class</c>,
/// not <see langword="abstract"/>, matching this codebase's own universal
/// convention for a namespace-level exception base.
/// </remarks>
public class BearingsException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="BearingsException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    public BearingsException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="BearingsException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    /// <param name="innerException">The underlying failure.</param>
    public BearingsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
