using Tempest.App.Projects;
using Tempest.App.Workspace.Calculations;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Engineering;

/// <summary>
/// The workspace's governed index of named calculations — what a
/// calculation is <em>called</em>, which project it belongs to, and whether
/// it is still wanted in the active list.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing here is a new concept.</b> A named calculation is the
/// platform's own <see cref="Calculation"/> Engineering Domain object,
/// created through <see cref="CalculationObjectFactoryRegistry"/> and
/// linked to its immutable <c>CalculationRecord</c> by the
/// <see cref="CalculationTemplateRegistry.CalculatedByRelationshipKind"/>
/// relationship the Digital Thread already defines. Renaming dispatches
/// <see cref="RenameCalculationObjectCommand"/>; retiring dispatches
/// <see cref="SetCalculationStatusCommand"/>; belonging to a project is
/// <see cref="IHasParent"/>. Every one of those was registered by
/// <see cref="CalculationsWorkspaceRegistration"/> long before this type
/// existed, and this type adds no mutation of its own.
/// </para>
/// <para>
/// <b>The record is the evidence; the object is the label.</b> A
/// <c>CalculationRecord</c> is immutable and is never renamed, never
/// reparented and never retired — renaming changes
/// <see cref="IHasBusinessIdentifier.DisplayName"/> on the Domain object
/// and touches neither the record, the object's identity, its revision
/// history, its relationships nor its lifecycle history. A calculation
/// whose name has changed is the same calculation, with the same evidence
/// behind it.
/// </para>
/// <para>
/// <b>Retiring is not deleting, deliberately (`TD-169`).</b> This type
/// never calls <see cref="IDeletable.DeleteAsync"/>. That is a soft delete
/// the platform has no undo for — <c>EngineeringObjectBase.DeleteAsync</c>
/// states in its own remarks that no writer anywhere clears the flag and
/// that every read model filters the object out — so using it would make a
/// calculation that has contributed to traceable engineering work vanish
/// from the product with no supported way back. Retiring instead
/// transitions the Domain object along
/// <see cref="LifecycleTransitionTable"/>'s own permitted route to a
/// retained terminal state, which from <see cref="LifecycleState.Draft"/>
/// is <see cref="LifecycleState.Cancelled"/>: the object stays in the
/// repository, its record stays where it was, and the workspace can still
/// list and open it.
/// </para>
/// </remarks>
public sealed class EngineeringCalculationRegister
{
    private readonly EngineeringDomainContext _domain;
    private readonly ICommandDispatcher _dispatcher;
    private readonly CalculationObjectFactoryRegistry _factories;
    private readonly IProjectContext? _projects;

    /// <summary>Initialises a new instance of the <see cref="EngineeringCalculationRegister"/> class.</summary>
    /// <param name="domain">The engineering domain the named calculations live in.</param>
    /// <param name="dispatcher">The dispatcher every governed mutation goes through.</param>
    /// <param name="projects">The open project, where the shell has one — a calculation created inside a project belongs to it. <see langword="null"/> where no project context is composed.</param>
    public EngineeringCalculationRegister(
        EngineeringDomainContext domain, ICommandDispatcher dispatcher, IProjectContext? projects = null)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _domain = domain;
        _dispatcher = dispatcher;
        _projects = projects;
        _factories = new CalculationObjectFactoryRegistry(domain);
    }

    /// <summary>
    /// Names one executed calculation, so it can be found, renamed,
    /// organised and retired like any other engineering object.
    /// </summary>
    /// <remarks>
    /// The object is parented on the open project where one is open, using
    /// the platform's own <see cref="IHasParent"/> membership — the same
    /// relationship every other discipline's objects already use, never a
    /// folder and never a hierarchy of this surface's own invention.
    /// </remarks>
    /// <param name="recordId">The immutable record this calculation produced.</param>
    /// <param name="displayName">What the engineer wants it called.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The named calculation, as a surface should show it.</returns>
    public async Task<NamedCalculation> NameAsync(
        Guid recordId, string displayName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var projectId = _projects?.Current?.Id;

        var created = await _factories.CreateAsync(
            CalculationObjectFactoryRegistry.CalculationKind,
            identifier: null,
            displayName: displayName,
            initialContent: $"Engineering calculation recorded as calculation record {recordId}.",
            parentId: projectId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (created is IHasRelationships relationships)
        {
            await relationships
                .LinkAsync(recordId, CalculationTemplateRegistry.CalculatedByRelationshipKind, cancellationToken)
                .ConfigureAwait(false);
        }

        return await DescribeAsync(created, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Every named calculation this domain holds, newest first, whether or
    /// not it is still wanted in the active list.
    /// </summary>
    /// <remarks>
    /// A soft-deleted object is filtered out, matching every other
    /// <c>Tempest.App</c> read model. A retired one is not: retiring is a
    /// lifecycle state and the whole point of using it rather than a delete
    /// is that the calculation is still there to be found.
    /// </remarks>
    public async Task<IReadOnlyList<NamedCalculation>> ListAsync(CancellationToken cancellationToken = default)
    {
        var objects = await _domain.Repository.ListByKindAsync(CalculationObjectFactoryRegistry.CalculationKind, cancellationToken).ConfigureAwait(false);
        var named = new List<NamedCalculation>(objects.Count);

        foreach (var candidate in objects)
        {
            if (candidate is IDeletable { IsDeleted: true })
                continue;

            named.Add(await DescribeAsync(candidate, cancellationToken).ConfigureAwait(false));
        }

        return [.. named.OrderByDescending(n => n.CreatedAt)];
    }

    /// <summary>
    /// Changes what a calculation is called, and nothing else, through the
    /// platform's own rename command.
    /// </summary>
    /// <param name="calculationObjectId">The named calculation to rename.</param>
    /// <param name="newDisplayName">Its new display name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task<CalculationRegisterOutcome> RenameAsync(
        Guid calculationObjectId, string? newDisplayName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newDisplayName))
            return new CalculationRegisterOutcome(false, "A calculation needs a name. Type one, then press Rename.");

        var result = await _dispatcher
            .DispatchAsync(new RenameCalculationObjectCommand(calculationObjectId, CalculationObjectFactoryRegistry.CalculationKind, newDisplayName), cancellationToken)
            .ConfigureAwait(false);

        return result.Succeeded
            ? new CalculationRegisterOutcome(true, $"Renamed to \"{newDisplayName}\". The record, its revisions and its references are unchanged.")
            : new CalculationRegisterOutcome(false, result.Message ?? "The rename was refused.");
    }

    /// <summary>
    /// Takes a calculation out of the active list without deleting
    /// anything, by transitioning its Domain object to the nearest retained
    /// terminal lifecycle state its current state permits.
    /// </summary>
    /// <remarks>
    /// Where no permitted retained terminal state exists from where the
    /// object currently is, this refuses and says so rather than forcing
    /// one — an unreachable transition is a governance answer, not an error
    /// to route around.
    /// </remarks>
    /// <param name="calculationObjectId">The named calculation to retire.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task<CalculationRegisterOutcome> RetireAsync(
        Guid calculationObjectId, CancellationToken cancellationToken = default)
    {
        var target = await _domain.Repository.FindAsync(calculationObjectId, cancellationToken).ConfigureAwait(false);

        if (target is not IHasLifecycle lifecycle)
            return new CalculationRegisterOutcome(false, "That calculation is no longer held, or has no lifecycle status.");

        if (RetiredStates.Contains(lifecycle.Status))
            return new CalculationRegisterOutcome(false, $"That calculation is already {lifecycle.Status} and is not in the active list.");

        var table = new LifecycleTransitionTable();
        LifecycleState? reachable = null;

        foreach (var candidate in RetiredStates)
        {
            if (!table.IsPermitted(lifecycle.Status, candidate))
                continue;

            reachable = candidate;
            break;
        }

        if (reachable is not { } retiredState)
        {
            var permitted = table.GetPermittedTargets(lifecycle.Status);

            return new CalculationRegisterOutcome(
                false,
                $"A calculation that is {lifecycle.Status} cannot be retired directly — the platform's lifecycle permits only "
                + (permitted.Count == 0 ? "no further transition at all" : string.Join(", ", permitted))
                + " from there. Nothing was changed and nothing was deleted.");
        }

        var result = await _dispatcher
            .DispatchAsync(new SetCalculationStatusCommand(calculationObjectId, CalculationObjectFactoryRegistry.CalculationKind, retiredState), cancellationToken)
            .ConfigureAwait(false);

        return result.Succeeded
            ? new CalculationRegisterOutcome(
                true,
                $"Retired from the active list as {retiredState}. Nothing was deleted — the calculation, its record and its evidence are all still held, "
                + "and it can still be opened.")
            : new CalculationRegisterOutcome(false, result.Message ?? "The retirement was refused.");
    }

    /// <summary>
    /// The retained terminal lifecycle states a retirement may reach, in
    /// the order this workspace prefers them.
    /// </summary>
    /// <remarks>
    /// <see cref="LifecycleState.Archived"/> is preferred and is what a
    /// calculation that reached <see cref="LifecycleState.Superseded"/> or
    /// <see cref="LifecycleState.Obsolete"/> retires to.
    /// <see cref="LifecycleState.Cancelled"/> is the only one reachable
    /// from <see cref="LifecycleState.Draft"/>, which is where a freshly
    /// recorded calculation starts — see this type's own remarks and
    /// <c>TD-169</c>.
    /// </remarks>
    private static readonly LifecycleState[] RetiredStates = [LifecycleState.Archived, LifecycleState.Cancelled];

    /// <summary>Whether <paramref name="status"/> means "no longer in the active list".</summary>
    /// <param name="status">The lifecycle status to classify.</param>
    public static bool IsRetired(LifecycleState status) => RetiredStates.Contains(status);

    private async Task<NamedCalculation> DescribeAsync(IEngineeringObject subject, CancellationToken cancellationToken)
    {
        Guid? recordId = null;

        if (subject is IHasRelationships relationships)
        {
            var links = await relationships.GetRelationshipsAsync(cancellationToken).ConfigureAwait(false);

            recordId = links
                .FirstOrDefault(l => string.Equals(l.RelationshipKind, CalculationTemplateRegistry.CalculatedByRelationshipKind, StringComparison.Ordinal))
                ?.TargetId;
        }

        var status = subject is IHasLifecycle lifecycle ? lifecycle.Status : LifecycleState.Draft;
        var parentId = subject is IHasParent { ParentId: { } pid } ? pid : (Guid?)null;
        var parentLabel = parentId is null ? null : await DescribeParentAsync(parentId.Value, cancellationToken).ConfigureAwait(false);

        return new NamedCalculation(
            ObjectId: subject.Id,
            DisplayName: subject is IHasBusinessIdentifier identified ? identified.DisplayName : subject.Kind,
            RecordId: recordId,
            Status: status,
            IsRetired: IsRetired(status),
            ParentId: parentId,
            ParentLabel: parentLabel,
            CreatedAt: subject.CreatedAt);
    }

    private async Task<string?> DescribeParentAsync(Guid parentId, CancellationToken cancellationToken)
    {
        var parent = await _domain.Repository.FindAsync(parentId, cancellationToken).ConfigureAwait(false);

        return parent is IHasBusinessIdentifier identified ? identified.DisplayName : parent?.Kind;
    }
}

/// <summary>One named calculation, as a surface should present it.</summary>
/// <param name="ObjectId">The governed Domain object's own Id — what a rename or a retirement addresses.</param>
/// <param name="DisplayName">What it is called.</param>
/// <param name="RecordId">The immutable calculation record behind it, where one is linked.</param>
/// <param name="Status">Its governed lifecycle status.</param>
/// <param name="IsRetired">Whether that status means it has left the active list.</param>
/// <param name="ParentId">The object it belongs to — a project, where one was open when it was created.</param>
/// <param name="ParentLabel">That object's own display name.</param>
/// <param name="CreatedAt">When it was named.</param>
public sealed record NamedCalculation(
    Guid ObjectId,
    string DisplayName,
    Guid? RecordId,
    LifecycleState Status,
    bool IsRetired,
    Guid? ParentId,
    string? ParentLabel,
    DateTimeOffset CreatedAt);

/// <summary>What happened when a surface asked for a rename or a retirement, in words the engineer should read.</summary>
/// <param name="Succeeded">Whether the governed act was performed.</param>
/// <param name="Message">What to tell the engineer, refusals included, in the platform's own words where it refused.</param>
public sealed record CalculationRegisterOutcome(bool Succeeded, string Message);
