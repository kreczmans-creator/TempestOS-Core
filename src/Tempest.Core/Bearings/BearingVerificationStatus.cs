namespace Tempest.Core.Bearings;

/// <summary>Whether a bearing record's own values have been checked back against the source they claim to come from.</summary>
public enum BearingVerificationStatus
{
    /// <summary>
    /// Nothing has confirmed the values against their source. The honest
    /// default, and the value an import leaves behind: being imported is
    /// not being verified.
    /// </summary>
    NotVerified,

    /// <summary>A named reviewer checked the values against the cited source on a recorded date.</summary>
    VerifiedAgainstSource,

    /// <summary>The values were once verified, but the source has since been revised past the revision they were taken from.</summary>
    SupersededBySource
}
