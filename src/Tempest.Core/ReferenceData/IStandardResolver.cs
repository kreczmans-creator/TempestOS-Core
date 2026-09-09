namespace Tempest.Core.ReferenceData;

/// <summary>
/// The one thing a citing library needs from the Standards Library:
/// whether a cited standard is actually registered.
/// </summary>
/// <remarks>
/// <para>
/// Declared here, in the shared layer, rather than in
/// <c>Tempest.Core.Standards</c>, so that Bearings, Fasteners, Materials,
/// Components and Manufacturing can confirm their own citations resolve
/// without any of them taking a compile-time dependency on A2. A2
/// implements it; nobody else needs to know A2 exists.
/// </para>
/// <para>
/// Deliberately narrow. A citing library has no business reading a
/// standard's own title, scope or status — if it needs those it should
/// ask the Standards Library directly, and if it needs to <em>copy</em>
/// them it is duplicating A2's own data, which is exactly what
/// <see cref="StandardReference.StandardId"/> exists to prevent.
/// </para>
/// </remarks>
public interface IStandardResolver
{
    /// <summary>Whether a standard is registered under <paramref name="standardId"/>.</summary>
    Task<bool> ExistsAsync(string standardId, CancellationToken cancellationToken = default);
}
