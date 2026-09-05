namespace Tempest.Core.Bearings;

/// <summary>
/// Thrown when an operation requiring an existing bearing (e.g.
/// <see cref="IBearingCatalog.ReviseAsync"/>) is given a <c>bearingId</c>
/// that does not exist. <see cref="IBearingCatalog.FindAsync"/> itself
/// never throws this — a nullable return is used there instead, mirroring
/// <see cref="Materials.MaterialNotFoundException"/>'s own reasoning:
/// "not found" is an ordinary outcome for a catalogue lookup.
/// </summary>
public sealed class BearingNotFoundException : BearingsException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="BearingNotFoundException"/> class.
    /// </summary>
    /// <param name="bearingId">The bearing identity that does not exist.</param>
    public BearingNotFoundException(string bearingId)
        : base($"No bearing is registered with Id '{bearingId}'.")
    {
        BearingId = bearingId;
    }

    /// <summary>Gets the bearing identity that does not exist.</summary>
    public string BearingId { get; }
}
