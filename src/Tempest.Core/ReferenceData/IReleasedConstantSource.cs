namespace Tempest.Core.ReferenceData;

/// <summary>
/// One released engineering constant, handed to whatever consumes it
/// together with the traceability that makes the use defensible.
/// </summary>
/// <remarks>
/// <see cref="RecordId"/> and <see cref="RevisionNumber"/> travel with the
/// value on purpose. A calculation that used a constant must be able to
/// say afterwards <em>which</em> constant, at <em>which</em> revision —
/// and since a released record is immutable and a corrected value becomes
/// a new record that supersedes it, that pair identifies the exact number
/// used, permanently.
/// </remarks>
/// <param name="Symbol">The symbol the constant was looked up by.</param>
/// <param name="Name">The constant's own name.</param>
/// <param name="Value">The constant's own dimensioned value.</param>
/// <param name="RecordId">The registered record the value came from.</param>
/// <param name="RevisionNumber">The revision of that record the value came from.</param>
public sealed record ReleasedConstant(
    string Symbol,
    string Name,
    ReferenceQuantityValue Value,
    string RecordId,
    int RevisionNumber);

/// <summary>
/// The one thing a calculation needs from the Engineering Constants
/// Library: a constant it is actually allowed to rely on.
/// </summary>
/// <remarks>
/// <para>
/// Declared here, in the shared layer, rather than in
/// <c>Tempest.Core.Constants</c>, for the same reason
/// <see cref="IStandardResolver"/> is: a future calculation capability can
/// consume released constants without taking a compile-time dependency on
/// A6. A6 implements it; nobody else needs to know A6 exists.
/// </para>
/// <para>
/// <b>Released only, and that is the whole point.</b> A Draft or Checked
/// constant is a value nobody has finished verifying, and a calculation
/// that silently used one would produce a result whose trustworthiness
/// nobody could later establish. This seam therefore reports an
/// unreleased constant exactly as it reports one that does not exist —
/// not as an error to be handled, and never as a value to be used with a
/// caveat attached.
/// </para>
/// <para>
/// Deliberately narrow: no enumeration, no search, no category browsing.
/// A consumer that wants to explore the library should ask the library.
/// </para>
/// </remarks>
public interface IReleasedConstantSource
{
    /// <summary>
    /// Returns the released constant registered under
    /// <paramref name="symbol"/>, or <see langword="null"/> where none is
    /// registered <em>or</em> where the one registered has not been
    /// released.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty, or whitespace.</exception>
    Task<ReleasedConstant?> FindReleasedAsync(string symbol, CancellationToken cancellationToken = default);
}
