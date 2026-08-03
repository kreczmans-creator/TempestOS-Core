namespace Tempest.Core.Materials;

/// <summary>
/// The base exception thrown when a Materials Framework operation fails.
/// </summary>
/// <remarks>
/// Mirrors <see cref="EngineeringData.EngineeringDataException"/>'s own
/// base-plus-subtype pattern, itself mirroring
/// <see cref="Persistence.PersistenceException"/>, <see cref="Settings.SettingsException"/>,
/// and <see cref="Audit.AuditException"/> — <c>public class</c>, not
/// <see langword="abstract"/>, matching this codebase's own universal
/// convention rather than `WP7.0C Engineering Foundation Contracts.md`'s
/// own literal (and, per `WP 7.1A`'s own disclosed precedent, not
/// independently re-read against real sibling exception types) proposal.
/// </remarks>
public class MaterialsException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="MaterialsException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    public MaterialsException(string message)
        : base(message)
    {
    }
}
