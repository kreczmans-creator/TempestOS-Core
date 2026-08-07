using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Verification;

/// <summary>
/// Builds a new Verification Activity Domain object — a small,
/// Workspace-layer composition helper wrapping an
/// <see cref="EngineeringObjectFactory{T}"/> instance, mirroring
/// <see cref="Calculations.CalculationObjectFactoryRegistry"/>/
/// <see cref="Documents.DocumentObjectFactoryRegistry"/>'s own identical
/// shape (`WP 9.3A`) exactly. Never a Domain-layer registry contract —
/// this type lives entirely in <c>Tempest.App</c>.
/// </summary>
/// <remarks>
/// <c>"VerificationActivity"</c> is the only Kind this registry
/// constructs — the bare <see cref="Verification"/> marker Kind (`WP 8.2C`)
/// is deliberately never instantiated by this Work Package, disclosed in
/// `WP9.3A Technical Debt Assessment.md`: every named scope item this
/// Work Package's own controlling instruction lists is already satisfied
/// by <see cref="VerificationActivity"/> alone.
/// </remarks>
public sealed class VerificationActivityFactoryRegistry
{
    /// <summary>The Kind this registry can construct.</summary>
    public const string SupportedKind = "VerificationActivity";

    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="VerificationActivityFactoryRegistry"/> class.</summary>
    public VerificationActivityFactoryRegistry(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    /// <summary>Creates a new Verification Activity, moving it under <paramref name="parentId"/> if one is given.</summary>
    /// <param name="subjectId">The engineering object this activity verifies — <see cref="IVerificationActivity.SubjectId"/>.</param>
    /// <param name="method">The verification method — <see cref="IVerificationActivity.Method"/> (e.g. <c>"Inspection"</c>/<c>"Analysis"</c>/<c>"Test"</c>/<c>"Demonstration"</c>, an open string, mirroring <see cref="Tempest.Core.Verification.IVerificationRecord.Method"/>'s own identical, deliberately-open shape).</param>
    /// <exception cref="ArgumentException"><paramref name="displayName"/> is null/empty/whitespace.</exception>
    public async Task<IEngineeringObject> CreateAsync(
        string displayName, string initialContent, Guid subjectId, string method, Guid? parentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(initialContent);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);

        var factory = new EngineeringObjectFactory<VerificationActivity>(
            SupportedKind, _context, (doc, rev) => new VerificationActivity(
                doc, rev, _context, displayName, EngineeringObjectMetadata.Empty, subjectId, method));

        var created = await factory.CreateAsync(initialContent, cancellationToken).ConfigureAwait(false);

        if (parentId is { } pid && created is IHasParent hasParent)
            await hasParent.MoveAsync(pid, cancellationToken).ConfigureAwait(false);

        return created;
    }
}
