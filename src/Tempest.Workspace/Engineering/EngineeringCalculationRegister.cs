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
/// existed.
/// </para>
/// <para>
/// <b>Two of its operations are dispatched; two are not, and that is
/// stated rather than glossed.</b> Renaming and retiring go through
/// <see cref="ICommandDispatcher"/> and the handlers
/// <see cref="CalculationsWorkspaceRegistration"/> registers. Creating the
/// object and writing its <c>calculatedBy</c> link go directly through
/// <see cref="CalculationObjectFactoryRegistry"/> and
/// <see cref="IHasRelationships.LinkAsync"/> — the same factory
/// <c>CreateCalculationObjectCommandHandler</c> itself calls — because
/// <see cref="CommandResult"/> carries no created identity and this
/// surface needs the new object's Id in order to link it. No
/// authorisation gate is skipped by that: the dispatcher performs none.
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
/// <b>Retiring is not deleting, deliberately (`TD-169`).</b> Retiring never
/// calls <see cref="IDeletable.DeleteAsync"/>. (The one place this type
/// does call it is withdrawing an object whose own creation failed, which
/// is residue rather than anybody's engineering work — see
/// <see cref="NameAsync"/>.) That is a soft delete
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
            try
            {
                await relationships
                    .LinkAsync(recordId, CalculationTemplateRegistry.CalculatedByRelationshipKind, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Compensate, then report precisely which of the two
                // outcomes actually happened.
                //
                // The object exists durably at this point and carries the
                // name, so "it could not be named" would be untrue. What is
                // missing is its link to the record, and this workspace
                // finds a named calculation by that link, so the object
                // would sit in the repository reachable by every other
                // read model and by none of this one's.
                //
                // The repository has no unregister — that is `TD-147`'s
                // disclosure — but a soft delete does remove the object
                // from every read model that filters `IsDeleted`, this
                // one's `ListAsync` included. It is the right instrument
                // *here* and only here: this object is not somebody's
                // engineering work, it is the residue of a creation that
                // failed, and it has existed for the length of one failed
                // call. That is the opposite of the case this type refuses
                // to use `DeleteAsync` for elsewhere.
                //
                // The compensation can itself fail — most likely for the
                // very reason the link did — so its outcome is carried in
                // the exception rather than assumed.
                var compensated = await TryWithdrawAsync(created, cancellationToken).ConfigureAwait(false);

                throw new EngineeringCalculationNamingException(created.Id, recordId, displayName, compensated, ex);
            }
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
    /// Withdraws an object whose creation did not complete, and reports
    /// whether that succeeded.
    /// </summary>
    /// <remarks>
    /// Soft-deleting it is the only withdrawal the platform offers, and it
    /// removes the object from every read model that filters
    /// <see cref="IDeletable.IsDeleted"/> — which is all of them. Failure
    /// is returned rather than thrown, because the caller is already
    /// reporting a failure and a second exception thrown from inside the
    /// first one's handling would replace the accurate account with a
    /// less accurate one.
    /// </remarks>
    private static async Task<bool> TryWithdrawAsync(IEngineeringObject created, CancellationToken cancellationToken)
    {
        if (created is not IDeletable deletable)
            return false;

        try
        {
            await deletable.DeleteAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>The named calculation carrying <paramref name="recordId"/>, or <see langword="null"/> where nobody has named that record.</summary>
    /// <param name="recordId">The calculation record to look up.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task<NamedCalculation?> FindByRecordAsync(Guid recordId, CancellationToken cancellationToken = default)
    {
        var named = await ListAsync(cancellationToken).ConfigureAwait(false);

        return named.FirstOrDefault(n => n.RecordId == recordId);
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
    /// What retiring <paramref name="calculationObjectId"/> would do, in
    /// the same words the act itself would use, without doing it.
    /// </summary>
    /// <remarks>
    /// <b>Every retained terminal state is terminal.</b>
    /// <see cref="LifecycleTransitionTable"/> gives both
    /// <see cref="LifecycleState.Archived"/> and
    /// <see cref="LifecycleState.Cancelled"/> no permitted targets at all,
    /// so a retirement cannot be undone through the lifecycle any more than
    /// a soft delete can be undone through the repository — the difference,
    /// and it is the whole difference, is that a retired calculation stays
    /// visible, listable and openable while a soft-deleted one does not.
    /// A surface must therefore be able to say what will happen before it
    /// happens, which is what this exists for.
    /// </remarks>
    /// <param name="calculationObjectId">The named calculation in question.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see cref="CalculationRegisterOutcome.Succeeded"/> is whether the retirement <em>could</em> proceed; nothing is changed either way.</returns>
    public async Task<CalculationRegisterOutcome> DescribeRetirementAsync(
        Guid calculationObjectId, CancellationToken cancellationToken = default)
    {
        var (refusal, reachable, status) = await ResolveRetirementAsync(calculationObjectId, cancellationToken).ConfigureAwait(false);

        if (refusal is not null)
            return refusal;

        return new CalculationRegisterOutcome(
            true,
            $"Retiring this calculation will move it from {status} to {reachable}, which is a terminal state — it cannot be moved back afterwards. "
            + "Nothing is deleted: the calculation, its record and its evidence stay held, and it stays openable under \"Show retired calculations\". "
            + "Press Retire again to confirm.");
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
    /// to route around. <see cref="DescribeRetirementAsync"/> answers the
    /// same question without acting, so a surface can say what will happen
    /// before it happens.
    /// </remarks>
    /// <param name="calculationObjectId">The named calculation to retire.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task<CalculationRegisterOutcome> RetireAsync(
        Guid calculationObjectId, CancellationToken cancellationToken = default)
    {
        var (refusal, reachable, _) = await ResolveRetirementAsync(calculationObjectId, cancellationToken).ConfigureAwait(false);

        if (refusal is not null)
            return refusal;

        var result = await _dispatcher
            .DispatchAsync(new SetCalculationStatusCommand(calculationObjectId, CalculationObjectFactoryRegistry.CalculationKind, reachable!.Value), cancellationToken)
            .ConfigureAwait(false);

        return result.Succeeded
            ? new CalculationRegisterOutcome(
                true,
                $"Retired from the active list as {reachable}. Nothing was deleted — the calculation, its record and its evidence are all still held, "
                + "and it can still be opened.")
            : new CalculationRegisterOutcome(false, result.Message ?? "The retirement was refused.");
    }

    /// <summary>
    /// The one place that decides whether a retirement can proceed and
    /// where to, shared by the description and the act so the two can
    /// never disagree.
    /// </summary>
    private async Task<(CalculationRegisterOutcome? Refusal, LifecycleState? Reachable, LifecycleState Status)> ResolveRetirementAsync(
        Guid calculationObjectId, CancellationToken cancellationToken)
    {
        var target = await _domain.Repository.FindAsync(calculationObjectId, cancellationToken).ConfigureAwait(false);

        if (target is not IHasLifecycle lifecycle)
            return (new CalculationRegisterOutcome(false, "That calculation is no longer held, or has no lifecycle status."), null, LifecycleState.Draft);

        if (RetiredStates.Contains(lifecycle.Status))
            return (new CalculationRegisterOutcome(false, $"That calculation is already {lifecycle.Status} and is not in the active list."), null, lifecycle.Status);

        // The injected table, never a fresh LifecycleTransitionTable: this
        // must agree with the one SetCalculationStatusCommandHandler
        // actually transitions against, or a permitted retirement gets
        // refused with a fabricated reason.
        var table = _domain.LifecycleTable;

        foreach (var candidate in RetiredStates)
        {
            if (table.IsPermitted(lifecycle.Status, candidate))
                return (null, candidate, lifecycle.Status);
        }

        var permitted = table.GetPermittedTargets(lifecycle.Status);

        return (
            new CalculationRegisterOutcome(
                false,
                $"A calculation that is {lifecycle.Status} cannot be retired directly — the platform's lifecycle permits only "
                + (permitted.Count == 0 ? "no further transition at all" : string.Join(", ", permitted))
                + " from there. Nothing was changed and nothing was deleted."),
            null,
            lifecycle.Status);
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

            // A Calculation object can carry more than one `calculatedBy`
            // link — the Calculations workspace's own Execute and
            // Recalculate commands append one per execution — so the
            // newest is taken, and taken by CreatedAt rather than by
            // arrival order, which is what CalculationRecordReader's own
            // GetLatestAsync already does with the same links. Relying on
            // enumeration order would let a restart change the answer,
            // because the index is rebuilt document by document.
            recordId = links
                .Where(l => string.Equals(l.RelationshipKind, CalculationTemplateRegistry.CalculatedByRelationshipKind, StringComparison.Ordinal))
                .OrderBy(l => l.CreatedAt)
                .LastOrDefault()
                ?.TargetId;
        }

        var status = subject is IHasLifecycle lifecycle ? lifecycle.Status : LifecycleState.Draft;

        // Which project it belongs to is asked of ProjectMembership, the
        // platform's single definition of that question, which walks the
        // whole IHasParent chain. Reading one hop and labelling whatever it
        // finds would be a second, wrong answer: a calculation filed under
        // a Calculation Set inside a project has the set as its parent and
        // the project as its owner, and the project directory reads the
        // owner.
        var projectId = await ProjectMembership
            .ResolveOwningProjectAsync(_domain.Repository, subject.Id, cancellationToken)
            .ConfigureAwait(false);

        return new NamedCalculation(
            ObjectId: subject.Id,
            DisplayName: subject is IHasBusinessIdentifier identified ? identified.DisplayName : subject.Kind,
            RecordId: recordId,
            Status: status,
            ProjectId: projectId,
            ProjectLabel: projectId is null ? null : await DescribeProjectAsync(projectId.Value, cancellationToken).ConfigureAwait(false),
            CreatedAt: subject.CreatedAt);
    }

    private async Task<string?> DescribeProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await _domain.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false);

        return project is IHasBusinessIdentifier identified ? identified.DisplayName : project?.Kind;
    }
}

/// <summary>One named calculation, as a surface should present it.</summary>
/// <param name="ObjectId">The governed Domain object's own Id — what a rename or a retirement addresses.</param>
/// <param name="DisplayName">What it is called.</param>
/// <param name="RecordId">The newest immutable calculation record linked to it, where one is linked.</param>
/// <param name="Status">Its governed lifecycle status.</param>
/// <param name="ProjectId">The project it belongs to, resolved by <see cref="ProjectMembership"/>, or <see langword="null"/> where it belongs to none — a real, supported state.</param>
/// <param name="ProjectLabel">That project's own display name.</param>
/// <param name="CreatedAt">When it was named.</param>
public sealed record NamedCalculation(
    Guid ObjectId,
    string DisplayName,
    Guid? RecordId,
    LifecycleState Status,
    Guid? ProjectId,
    string? ProjectLabel,
    DateTimeOffset CreatedAt)
{
    /// <summary>Whether its status means it has left the active list. Derived, never supplied, so it cannot disagree with <see cref="Status"/>.</summary>
    public bool IsRetired => EngineeringCalculationRegister.IsRetired(Status);
}

/// <summary>
/// A calculation object was created and named, durably, but could not be
/// linked to the record it was named for.
/// </summary>
/// <remarks>
/// Distinct from a failure to create the object at all, because the two
/// leave the product in different states and an engineer needs to be told
/// which happened. See <see cref="EngineeringCalculationRegister.NameAsync"/>'s
/// own catch, and `TD-147` for why the created object cannot be withdrawn.
/// </remarks>
public sealed class EngineeringCalculationNamingException : Exception
{
    /// <summary>Initialises a new instance of the <see cref="EngineeringCalculationNamingException"/> class.</summary>
    /// <param name="calculationObjectId">The object that was created and named.</param>
    /// <param name="recordId">The record it could not be linked to.</param>
    /// <param name="displayName">The name it carries.</param>
    /// <param name="wasWithdrawn">Whether the half-created object was successfully withdrawn afterwards.</param>
    /// <param name="innerException">What the link failed with.</param>
    public EngineeringCalculationNamingException(
        Guid calculationObjectId, Guid recordId, string displayName, bool wasWithdrawn, Exception innerException)
        : base(
            $"The calculation object '{calculationObjectId}' was created and named \"{displayName}\", durably, but could not be linked to record "
            + $"'{recordId}': {innerException.Message} The calculation itself is recorded and unaffected. "
            + (wasWithdrawn
                ? "The half-created object was withdrawn, so nothing was left behind."
                : "The half-created object could not be withdrawn either, so it remains in the repository, carrying that name and belonging to whatever "
                  + "project was open, and will not appear in this workspace's list, which finds a named calculation by its record link (TD-170)."),
            innerException)
    {
        CalculationObjectId = calculationObjectId;
        RecordId = recordId;
        DisplayName = displayName;
        WasWithdrawn = wasWithdrawn;
    }

    /// <summary>Whether the half-created object was withdrawn afterwards, leaving nothing behind.</summary>
    public bool WasWithdrawn { get; }

    /// <summary>The object that was created and named.</summary>
    public Guid CalculationObjectId { get; }

    /// <summary>The record it could not be linked to.</summary>
    public Guid RecordId { get; }

    /// <summary>The name the created object carries.</summary>
    public string DisplayName { get; }
}

/// <summary>What happened when a surface asked for a rename or a retirement, in words the engineer should read.</summary>
/// <param name="Succeeded">Whether the governed act was performed.</param>
/// <param name="Message">What to tell the engineer, refusals included, in the platform's own words where it refused.</param>
public sealed record CalculationRegisterOutcome(bool Succeeded, string Message);
