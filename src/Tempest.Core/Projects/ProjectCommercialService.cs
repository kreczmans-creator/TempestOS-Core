using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Projects;

/// <summary>Why an <see cref="IProjectCommercialService"/> act was refused, or <see cref="None"/> if it was not.</summary>
/// <remarks>
/// A refusal is a first-class answer here, exactly as
/// <c>Tempest.Core.Evidence.EvidenceRefusal</c> is for citing an
/// unreleased reference record: pinning a rate card that has not reached
/// Released is an ordinary engineering-governance finding a surface
/// should show, not an error condition. Genuinely invalid input (no such
/// project) still names the shortfall as a refusal too, mirroring
/// <c>EvidenceRefusal.EvidenceNotFound</c>.
/// </remarks>
public enum ProjectCommercialRefusal
{
    /// <summary>Nothing was refused.</summary>
    None,

    /// <summary>No project is registered under the requested id.</summary>
    ProjectNotFound,

    /// <summary>No rate card is registered under the requested id.</summary>
    RateCardNotFound,

    /// <summary>The rate card exists but has not been released, so a project may not pin it.</summary>
    RateCardNotReleased,
}

/// <summary>The outcome of a <see cref="IProjectCommercialService"/> act: either it happened, or a refusal that says why it did not.</summary>
/// <param name="Refusal">Why the act was refused, or <see cref="ProjectCommercialRefusal.None"/>.</param>
/// <param name="Reason">The refusal in a sentence an engineer can read. <see langword="null"/> when nothing was refused.</param>
/// <param name="Project">The project acted on, when it could be resolved.</param>
public sealed record ProjectCommercialResult(ProjectCommercialRefusal Refusal, string? Reason, Project? Project)
{
    /// <summary>Whether the act actually happened.</summary>
    public bool Succeeded => Refusal == ProjectCommercialRefusal.None;
}

/// <summary>
/// The commercial acts a <see cref="Project"/> supports: naming its
/// client, its purchase order, its budget, its pinned rate card, its dates
/// and its project manager (`WP 19.0A`, `ADR-0150`). Every act is one
/// transaction with an audit row; pinning a rate card that is not
/// Released is decided here, before <see cref="Project"/>'s own mutator
/// ever runs, and reported back as a refusal result rather than an
/// exception — mirroring <c>Tempest.Core.Evidence.IEvidenceService</c>.
/// </summary>
public interface IProjectCommercialService
{
    /// <summary>Sets, or clears (<see langword="null"/>), <paramref name="projectId"/>'s own client — a record id in the Organisation catalogue, never validated as a structure.</summary>
    Task<ProjectCommercialResult> SetClientAsync(Guid projectId, string? clientOrganisationId, CancellationToken cancellationToken = default);

    /// <summary>Sets, or clears, <paramref name="projectId"/>'s own client purchase-order reference.</summary>
    Task<ProjectCommercialResult> SetPurchaseOrderAsync(Guid projectId, string? purchaseOrderReference, CancellationToken cancellationToken = default);

    /// <summary>Sets, or clears, <paramref name="projectId"/>'s own budget.</summary>
    Task<ProjectCommercialResult> SetBudgetAsync(Guid projectId, Money? budget, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pins <paramref name="rateCardId"/> against <paramref name="projectId"/>, taking the revision from the record actually read.
    /// Refused, as a result rather than an exception, when the card is not registered or has not reached <see cref="ReferenceValidationState.Released"/>.
    /// </summary>
    Task<ProjectCommercialResult> PinRateCardAsync(Guid projectId, string rateCardId, CancellationToken cancellationToken = default);

    /// <summary>Sets <paramref name="projectId"/>'s own start and target dates.</summary>
    Task<ProjectCommercialResult> SetDatesAsync(Guid projectId, DateOnly? startDate, DateOnly? targetDate, CancellationToken cancellationToken = default);

    /// <summary>Sets, or clears, <paramref name="projectId"/>'s own project manager, by identity id.</summary>
    Task<ProjectCommercialResult> SetProjectManagerAsync(Guid projectId, string? projectManagerIdentityId, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IProjectCommercialService"/> implementation.</summary>
public sealed class ProjectCommercialService : IProjectCommercialService
{
    private readonly EngineeringDomainContext _context;
    private readonly IRateCardCatalog _rateCards;

    /// <summary>Initialises a new instance of the <see cref="ProjectCommercialService"/> class.</summary>
    public ProjectCommercialService(EngineeringDomainContext context, IRateCardCatalog rateCards)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rateCards);

        _context = context;
        _rateCards = rateCards;
    }

    /// <inheritdoc />
    public async Task<ProjectCommercialResult> SetClientAsync(Guid projectId, string? clientOrganisationId, CancellationToken cancellationToken = default)
    {
        var project = await FindProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
            return NotFound(projectId);

        await project.SetClientAsync(clientOrganisationId, cancellationToken).ConfigureAwait(false);

        return new ProjectCommercialResult(ProjectCommercialRefusal.None, null, project);
    }

    /// <inheritdoc />
    public async Task<ProjectCommercialResult> SetPurchaseOrderAsync(Guid projectId, string? purchaseOrderReference, CancellationToken cancellationToken = default)
    {
        var project = await FindProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
            return NotFound(projectId);

        await project.SetPurchaseOrderAsync(purchaseOrderReference, cancellationToken).ConfigureAwait(false);

        return new ProjectCommercialResult(ProjectCommercialRefusal.None, null, project);
    }

    /// <inheritdoc />
    public async Task<ProjectCommercialResult> SetBudgetAsync(Guid projectId, Money? budget, CancellationToken cancellationToken = default)
    {
        var project = await FindProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
            return NotFound(projectId);

        await project.SetBudgetAsync(budget, cancellationToken).ConfigureAwait(false);

        return new ProjectCommercialResult(ProjectCommercialRefusal.None, null, project);
    }

    /// <inheritdoc />
    public async Task<ProjectCommercialResult> PinRateCardAsync(Guid projectId, string rateCardId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rateCardId);

        var project = await FindProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
            return NotFound(projectId);

        var record = await _rateCards.FindAsync(rateCardId, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return new ProjectCommercialResult(
                ProjectCommercialRefusal.RateCardNotFound, $"No rate card '{rateCardId}' is registered.", project);
        }

        if (record.ValidationState != ReferenceValidationState.Released)
        {
            return new ProjectCommercialResult(
                ProjectCommercialRefusal.RateCardNotReleased,
                $"Rate card '{rateCardId}' is {record.ValidationState}, not Released. A project may not pin a card nobody has verified.",
                project);
        }

        var pin = ReferencePin.For(_rateCards.LibraryName, record);
        await project.PinRateCardAsync(pin, cancellationToken).ConfigureAwait(false);

        return new ProjectCommercialResult(ProjectCommercialRefusal.None, null, project);
    }

    /// <inheritdoc />
    public async Task<ProjectCommercialResult> SetDatesAsync(Guid projectId, DateOnly? startDate, DateOnly? targetDate, CancellationToken cancellationToken = default)
    {
        var project = await FindProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
            return NotFound(projectId);

        await project.SetDatesAsync(startDate, targetDate, cancellationToken).ConfigureAwait(false);

        return new ProjectCommercialResult(ProjectCommercialRefusal.None, null, project);
    }

    /// <inheritdoc />
    public async Task<ProjectCommercialResult> SetProjectManagerAsync(Guid projectId, string? projectManagerIdentityId, CancellationToken cancellationToken = default)
    {
        var project = await FindProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
            return NotFound(projectId);

        await project.SetProjectManagerAsync(projectManagerIdentityId, cancellationToken).ConfigureAwait(false);

        return new ProjectCommercialResult(ProjectCommercialRefusal.None, null, project);
    }

    private async Task<Project?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) as Project;

    private static ProjectCommercialResult NotFound(Guid projectId) =>
        new(ProjectCommercialRefusal.ProjectNotFound, $"No project '{projectId}' is registered.", null);
}
