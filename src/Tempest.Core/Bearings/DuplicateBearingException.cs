namespace Tempest.Core.Bearings;

/// <summary>Thrown when <see cref="IBearingCatalog.RegisterAsync"/> is given a <c>bearingId</c> that is already registered.</summary>
public sealed class DuplicateBearingException : BearingsException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="DuplicateBearingException"/> class.
    /// </summary>
    /// <param name="bearingId">The bearing identity that is already registered.</param>
    public DuplicateBearingException(string bearingId)
        : base($"A bearing is already registered with Id '{bearingId}'.")
    {
        BearingId = bearingId;
    }

    /// <summary>Gets the bearing identity that is already registered.</summary>
    public string BearingId { get; }
}
