namespace Tempest.Core.Bearings;

/// <summary>
/// Thrown when a <see cref="BearingValidationState"/> transition is
/// refused because the record's own provenance does not yet support it.
/// </summary>
/// <remarks>
/// The enforcement point for this library's own central rule: reference
/// data earns its status from its provenance, never from a caller
/// asserting one. Leaving <see cref="BearingValidationState.Draft"/>
/// requires a named source organisation and document; reaching
/// <see cref="BearingValidationState.Released"/> additionally requires a
/// named reviewer, a verification date, and
/// <see cref="BearingVerificationStatus.VerifiedAgainstSource"/>.
/// </remarks>
public sealed class BearingProvenanceIncompleteException : BearingsException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="BearingProvenanceIncompleteException"/> class.
    /// </summary>
    /// <param name="bearingId">The bearing whose transition was refused.</param>
    /// <param name="requestedState">The state that was requested.</param>
    /// <param name="reason">What the provenance is missing.</param>
    public BearingProvenanceIncompleteException(string bearingId, BearingValidationState requestedState, string reason)
        : base($"Bearing '{bearingId}' cannot become '{requestedState}': {reason}")
    {
        BearingId = bearingId;
        RequestedState = requestedState;
    }

    /// <summary>Gets the bearing whose transition was refused.</summary>
    public string BearingId { get; }

    /// <summary>Gets the state that was requested.</summary>
    public BearingValidationState RequestedState { get; }
}
