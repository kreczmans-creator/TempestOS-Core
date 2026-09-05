namespace Tempest.Core.Bearings;

/// <summary>Thrown when a requested <see cref="BearingValidationState"/> transition is not permitted from the record's own current state.</summary>
public sealed class InvalidBearingValidationStateTransitionException : BearingsException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="InvalidBearingValidationStateTransitionException"/> class.
    /// </summary>
    /// <param name="bearingId">The bearing whose transition was rejected.</param>
    /// <param name="from">The record's own current state.</param>
    /// <param name="to">The state that was requested.</param>
    public InvalidBearingValidationStateTransitionException(string bearingId, BearingValidationState from, BearingValidationState to)
        : base($"Bearing '{bearingId}' cannot transition from '{from}' to '{to}'. Permitted from '{from}': {Describe(from)}.")
    {
        BearingId = bearingId;
        From = from;
        To = to;
    }

    /// <summary>Gets the bearing whose transition was rejected.</summary>
    public string BearingId { get; }

    /// <summary>Gets the record's own current state.</summary>
    public BearingValidationState From { get; }

    /// <summary>Gets the state that was requested.</summary>
    public BearingValidationState To { get; }

    private static string Describe(BearingValidationState from)
    {
        var targets = BearingValidationStates.GetPermittedTargets(from);
        return targets.Count == 0 ? "nothing (terminal state)" : string.Join(", ", targets);
    }
}
