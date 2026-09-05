namespace Tempest.Core.Bearings;

/// <summary>
/// Thrown when a caller attempts to revise the engineering content of a
/// bearing record that is <see cref="BearingValidationState.Released"/> or
/// <see cref="BearingValidationState.Superseded"/>.
/// </summary>
/// <remarks>
/// Released engineering reference data is immutable by design. Downstream
/// calculations, verifications and baselines have already consumed the
/// released values; silently changing them underneath would make every
/// consumer's own record of what it used a lie. The supported path is
/// <see cref="IBearingCatalog.SupersedeAsync"/> — register the corrected
/// record and supersede the old one, so both survive and the change is
/// traceable.
/// </remarks>
public sealed class ReleasedBearingImmutableException : BearingsException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ReleasedBearingImmutableException"/> class.
    /// </summary>
    /// <param name="bearingId">The bearing whose revision was refused.</param>
    /// <param name="state">The record's own current state.</param>
    public ReleasedBearingImmutableException(string bearingId, BearingValidationState state)
        : base($"Bearing '{bearingId}' is '{state}' and cannot be revised. Register the corrected record and supersede this one instead.")
    {
        BearingId = bearingId;
        State = state;
    }

    /// <summary>Gets the bearing whose revision was refused.</summary>
    public string BearingId { get; }

    /// <summary>Gets the record's own current state.</summary>
    public BearingValidationState State { get; }
}
