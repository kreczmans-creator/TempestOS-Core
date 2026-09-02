namespace Tempest.Core.EngineeringData;

/// <summary>
/// The base exception thrown when an Engineering Data Model operation
/// fails.
/// </summary>
/// <remarks>
/// Mirrors <see cref="Persistence.PersistenceException"/>'s,
/// <see cref="Settings.SettingsException"/>'s, and
/// <see cref="Audit.AuditException"/>'s own base-plus-subtype pattern.
/// </remarks>
public class EngineeringDataException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="EngineeringDataException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    public EngineeringDataException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="EngineeringDataException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    /// <param name="innerException">The underlying failure.</param>
    public EngineeringDataException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
