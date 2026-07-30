namespace Tempest.Core.Verification;

/// <summary>
/// A single, explicit criterion checked as part of a verification —
/// makes what was actually checked inspectable, never hidden inside the
/// verifier's own unstated judgement.
/// </summary>
/// <param name="Description">The criterion itself, in plain engineering language.</param>
/// <param name="IsSatisfied">Whether this specific criterion held.</param>
/// <param name="Detail">Further detail about the check. <see langword="null"/> if none.</param>
public sealed record VerificationCriterion(string Description, bool IsSatisfied, string? Detail);
