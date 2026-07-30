namespace Tempest.Core.Identity;

/// <summary>
/// Identifies a single actor — a local user or system principal.
/// </summary>
/// <remarks>
/// Deliberately minimal, mirroring <see cref="Commands.ICommand"/>'s own
/// marker-plus-data shape. Carries no credential, authentication, or
/// federation concept — this release's identity model is local-only by
/// design (ADR-0043).
/// </remarks>
public interface IIdentity
{
    /// <summary>Gets a stable, unique identifier for this identity.</summary>
    string Id { get; }

    /// <summary>Gets a human-readable display name.</summary>
    string DisplayName { get; }
}
