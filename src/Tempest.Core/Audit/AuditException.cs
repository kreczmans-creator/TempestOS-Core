namespace Tempest.Core.Audit;

/// <summary>
/// The base exception thrown when an Audit operation fails.
/// </summary>
/// <remarks>
/// Mirrors <see cref="Identity.IdentityException"/>'s and
/// <see cref="Settings.SettingsException"/>'s own base-plus-subtype
/// pattern. No subtype is defined in this release — every current Audit
/// failure mode is already covered by an existing exception type
/// (<see cref="ArgumentException"/> for invalid input,
/// <see cref="Persistence.PersistenceStoreUnavailableException"/> for a
/// storage failure, <see cref="Identity.PermissionDeniedException"/> for
/// an unauthorized query) — this base type exists for the approved
/// contract's own sake and for a future subtype, never thrown directly.
/// </remarks>
public class AuditException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="AuditException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    public AuditException(string message)
        : base(message)
    {
    }
}
