namespace Tempest.Core.Verification;

/// <summary>The outcome of one verification.</summary>
public enum VerificationOutcome
{
    /// <summary>The engineering claim was demonstrated.</summary>
    Pass,

    /// <summary>The engineering claim was not demonstrated.</summary>
    Fail,

    /// <summary>The engineering claim was demonstrated subject to a disclosed qualification — see the record's own recorded criteria for which.</summary>
    Conditional
}
