namespace Tempest.Core.Bearings;

internal sealed class Bearing : IBearing
{
    public Bearing(
        string bearingId,
        BearingDefinition definition,
        BearingValidationState validationState,
        string? supersededByBearingId,
        Guid underlyingDocumentId,
        int revisionNumber)
    {
        BearingId = bearingId;
        Definition = definition;
        ValidationState = validationState;
        SupersededByBearingId = supersededByBearingId;
        UnderlyingDocumentId = underlyingDocumentId;
        RevisionNumber = revisionNumber;
    }

    public string BearingId { get; }
    public BearingDefinition Definition { get; }
    public BearingValidationState ValidationState { get; }
    public string? SupersededByBearingId { get; }
    public Guid UnderlyingDocumentId { get; }
    public int RevisionNumber { get; }
}
