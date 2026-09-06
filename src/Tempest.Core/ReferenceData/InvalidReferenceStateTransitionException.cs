namespace Tempest.Core.ReferenceData;

/// <summary>Thrown when a requested <see cref="ReferenceValidationState"/> transition is not permitted from the record's own current state.</summary>
public sealed class InvalidReferenceStateTransitionException : ReferenceDataException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="InvalidReferenceStateTransitionException"/> class.
    /// </summary>
    /// <param name="library">The reference library.</param>
    /// <param name="recordId">The record whose transition was rejected.</param>
    /// <param name="from">The record's own current state.</param>
    /// <param name="to">The state that was requested.</param>
    public InvalidReferenceStateTransitionException(string library, string recordId, ReferenceValidationState from, ReferenceValidationState to)
        : base(library, $"{library} record '{recordId}' cannot transition from '{from}' to '{to}'. Permitted from '{from}': {Describe(from)}.")
    {
        RecordId = recordId;
        From = from;
        To = to;
    }

    /// <summary>Gets the record whose transition was rejected.</summary>
    public string RecordId { get; }

    /// <summary>Gets the record's own current state.</summary>
    public ReferenceValidationState From { get; }

    /// <summary>Gets the state that was requested.</summary>
    public ReferenceValidationState To { get; }

    private static string Describe(ReferenceValidationState from)
    {
        var targets = ReferenceValidationStates.GetPermittedTargets(from);
        return targets.Count == 0 ? "nothing (terminal state)" : string.Join(", ", targets);
    }
}
