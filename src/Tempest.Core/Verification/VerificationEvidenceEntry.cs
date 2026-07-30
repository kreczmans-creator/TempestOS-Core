namespace Tempest.Core.Verification;

/// <summary>
/// A single piece of evidence supporting a verification's own outcome —
/// makes what was actually examined (an inspection note, a test result, a
/// reference to a supporting artefact) explicit and inspectable.
/// </summary>
/// <param name="Description">The evidence itself, in plain engineering language.</param>
/// <param name="Reference">An optional pointer to where this evidence can be found (a file path, a test report Id, a standard clause). <see langword="null"/> if none.</param>
public sealed record VerificationEvidenceEntry(string Description, string? Reference);
