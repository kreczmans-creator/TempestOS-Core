namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// The in-memory claim table behind `TD-38`'s rule: a business identifier
/// is unique among live (not soft-deleted, not superseded) objects of the
/// same Kind within the same project. Comparison is ordinal,
/// case-insensitive, trimmed.
/// </summary>
/// <remarks>
/// <para>
/// Maintained by <see cref="EngineeringObjectFactory{T}"/> at creation and
/// by <see cref="EngineeringObjectBase.RenameAsync"/> at rename, both
/// under the domain write lock (`ADR-0145`) — every call this type sees is
/// already serialised by that lock, so nothing here needs its own
/// asynchronous coordination; the internal <see langword="lock"/> guards
/// only against a future caller outside that discipline, cheaply.
/// </para>
/// <para>
/// A pure projection of the live object set, never a second store: no
/// claim here is durable, and none needs to be. A restart loses every
/// claim exactly as it loses <see cref="IEngineeringObjectRepository"/>'s
/// own in-memory index, and <see cref="EngineeringObjectRehydrationService"/>
/// rebuilds both from the same durable state (`TD-85`).
/// </para>
/// </remarks>
public interface IBusinessIdentifierIndex
{
    /// <summary>
    /// The Id of the live object already holding <paramref name="businessIdentifier"/>
    /// for <paramref name="kind"/>/<paramref name="projectScopeId"/>, other
    /// than <paramref name="excludingObjectId"/> itself — or <see langword="null"/>
    /// if none holds it. Read-only: claims nothing.
    /// </summary>
    Guid? FindConflict(string kind, Guid? projectScopeId, string businessIdentifier, Guid excludingObjectId);

    /// <summary>
    /// Claims <paramref name="businessIdentifier"/> for <paramref name="objectId"/>
    /// under <paramref name="kind"/>/<paramref name="projectScopeId"/>,
    /// first releasing whatever <paramref name="objectId"/> previously held.
    /// The "apply" step of `ADR-0145`'s project/commit/apply shape — called
    /// only after a commit, still under the domain write lock, once
    /// <see cref="FindConflict"/> has already been checked against the same
    /// state the commit landed.
    /// </summary>
    void Claim(string kind, Guid? projectScopeId, string businessIdentifier, Guid objectId);

    /// <summary>Releases whatever <paramref name="objectId"/> holds, if anything — a no-op otherwise (a soft-delete frees its identifier, `TD-38`).</summary>
    void Release(Guid objectId);

    /// <summary>Discards every claim — the first step of a rehydration rebuild (`TD-85`).</summary>
    void Clear();
}

/// <summary>The one production <see cref="IBusinessIdentifierIndex"/> implementation.</summary>
public sealed class BusinessIdentifierIndex : IBusinessIdentifierIndex
{
    private readonly object _gate = new();
    private readonly Dictionary<(string Kind, Guid? Scope), Dictionary<string, Guid>> _claimsByScope = new();
    private readonly Dictionary<Guid, (string Kind, Guid? Scope, string Identifier)> _claimsByObject = new();

    /// <inheritdoc />
    public Guid? FindConflict(string kind, Guid? projectScopeId, string businessIdentifier, Guid excludingObjectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(businessIdentifier);

        var trimmed = businessIdentifier.Trim();

        lock (_gate)
        {
            if (_claimsByScope.TryGetValue((kind, projectScopeId), out var bucket)
                && bucket.TryGetValue(trimmed, out var holderId)
                && holderId != excludingObjectId)
            {
                return holderId;
            }

            return null;
        }
    }

    /// <inheritdoc />
    public void Claim(string kind, Guid? projectScopeId, string businessIdentifier, Guid objectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(businessIdentifier);

        var trimmed = businessIdentifier.Trim();

        lock (_gate)
        {
            ReleaseNoLock(objectId);

            if (!_claimsByScope.TryGetValue((kind, projectScopeId), out var bucket))
            {
                bucket = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
                _claimsByScope[(kind, projectScopeId)] = bucket;
            }

            bucket[trimmed] = objectId;
            _claimsByObject[objectId] = (kind, projectScopeId, trimmed);
        }
    }

    /// <inheritdoc />
    public void Release(Guid objectId)
    {
        lock (_gate)
        {
            ReleaseNoLock(objectId);
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_gate)
        {
            _claimsByScope.Clear();
            _claimsByObject.Clear();
        }
    }

    private void ReleaseNoLock(Guid objectId)
    {
        if (!_claimsByObject.Remove(objectId, out var previous))
            return;

        if (_claimsByScope.TryGetValue((previous.Kind, previous.Scope), out var bucket))
            bucket.Remove(previous.Identifier);
    }
}

/// <summary>
/// Thrown when a create or rename would leave two live objects of the
/// same Kind, in the same project, sharing a business identifier
/// (`TD-38`). Not advisory: this refuses the operation outright, exactly
/// like <see cref="CircularParentAssignmentException"/>/
/// <see cref="EngineeringObjectHasChildrenException"/>.
/// </summary>
public sealed class DuplicateBusinessIdentifierException : EngineeringDomainException
{
    /// <summary>The Kind both objects share.</summary>
    public string Kind { get; }

    /// <summary>The clashing business identifier, as typed.</summary>
    public string BusinessIdentifier { get; }

    /// <summary>The project both objects are within, or <see langword="null"/> for the "outside any project" scope (`TD-38`).</summary>
    public Guid? ProjectId { get; }

    /// <summary>The Id of the object that already holds this identifier.</summary>
    public Guid ExistingObjectId { get; }

    /// <summary>Initialises a new instance of the <see cref="DuplicateBusinessIdentifierException"/> class.</summary>
    public DuplicateBusinessIdentifierException(string kind, string businessIdentifier, Guid? projectId, string? projectDisplayName, Guid existingObjectId)
        : base(BuildMessage(kind, businessIdentifier, projectId, projectDisplayName, existingObjectId))
    {
        Kind = kind;
        BusinessIdentifier = businessIdentifier;
        ProjectId = projectId;
        ExistingObjectId = existingObjectId;
    }

    private static string BuildMessage(string kind, string businessIdentifier, Guid? projectId, string? projectDisplayName, Guid existingObjectId) =>
        projectId is { } id
            ? $"A {kind} named '{businessIdentifier}' already exists in project {projectDisplayName ?? id.ToString()} (id {id})."
            : $"A {kind} named '{businessIdentifier}' already exists (id {existingObjectId}).";
}

/// <summary>
/// Resolves the project a `TD-38` uniqueness check scopes to, and the
/// Kinds this Work Package enforces the rule for.
/// </summary>
/// <remarks>
/// <see cref="EnforcedKinds"/> is deliberately narrower than "every Kind
/// <see cref="EngineeringObjectFactory{T}"/> can construct": it names
/// exactly the Kinds `WP 20.1A2`'s own brief and tests cover — the five
/// factory registries' own Kinds, plus Evidence. Requirement (its own,
/// already-correct index, `RequirementsService`) and the Tasks/commercial
/// Kinds (Task, InvoiceRequest, Quotation, Timesheet, Deliverable,
/// Milestone, Decision, Risk — `WP 20.1B`'s own files) are left exactly as
/// they are: unchanged behaviour for paths this Work Package did not
/// audit or test. Extending this set to a further Kind is a one-line
/// addition once that Kind's own business identifier is confirmed.
/// </remarks>
public static class BusinessIdentifierScope
{
    /// <summary>The Kinds `WP 20.1A2` enforces `TD-38` for.</summary>
    public static readonly IReadOnlyCollection<string> EnforcedKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        // Mechanical (`MechanicalObjectFactoryRegistry`).
        "Project", "Assembly", "SubAssembly", "Part", "Component", "Configuration", "Baseline", "Release",

        // Calculations (`CalculationObjectFactoryRegistry`).
        "Calculation", "CalculationSet",

        // Documents (`DocumentObjectFactoryRegistry`).
        "Document", "Drawing", "CadModel",

        // Manufacturing (`ManufacturingObjectFactoryRegistry`).
        "ManufacturingOperation", "WorkInstruction", "Inspection",

        // Verification (`VerificationActivityFactoryRegistry`).
        "VerificationActivity",

        // Evidence (`EvidenceService`).
        Evidence.Evidence.CanonicalKind,
    };

    /// <summary>
    /// Walks the parent chain from <paramref name="startId"/> upward and
    /// returns the nearest ancestor <see cref="Project"/>'s own Id —
    /// <paramref name="startId"/> itself if it already is one — or
    /// <see langword="null"/> once the walk runs out of parents without
    /// finding one: `TD-38`'s own "objects outside any project form their
    /// own scope". Synchronous, mirroring
    /// <c>EngineeringObjectBase.GuardAgainstCircularParent</c>'s identical
    /// reason: <paramref name="repository"/> is an in-memory cache, so a
    /// synchronous read here never awaits anything the domain write lock
    /// does not already hold uncontested.
    /// </summary>
    public static Guid? ResolveProjectId(Guid? startId, IEngineeringObjectRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        if (startId is not { } current)
            return null;

        var visited = new HashSet<Guid>();

        while (visited.Add(current))
        {
            var candidate = repository.FindAsync(current, CancellationToken.None).GetAwaiter().GetResult();

            if (candidate is Project project)
                return project.Id;

            if (candidate is not IHasParent { ParentId: { } parentId })
                return null;

            current = parentId;
        }

        // A cycle would mean some other invariant already failed; treat it
        // as "no project" rather than looping forever.
        return null;
    }

    /// <summary>
    /// Throws a fully-worded <see cref="DuplicateBusinessIdentifierException"/>
    /// if a still-<em>live</em> object other than <paramref name="objectId"/>
    /// holds <paramref name="businessIdentifier"/> for <paramref name="kind"/>/
    /// <paramref name="projectScopeId"/> in <paramref name="index"/> —
    /// otherwise returns having claimed nothing.
    /// </summary>
    /// <remarks>
    /// A claim held by an object that has since been soft-deleted is
    /// treated as no conflict at all and silently released here (`TD-38`:
    /// "a soft-delete frees the identifier") — deliberately, rather than
    /// by having <see cref="IDeletable.DeleteAsync"/> call
    /// <see cref="IBusinessIdentifierIndex.Release"/> itself: that mutator
    /// is outside this Work Package's own file-ownership boundary for
    /// <c>EngineeringObjectBase.cs</c> (the <see cref="EngineeringObjectBase.RenameAsync"/>
    /// path only). Checking liveness here, on the read side, needs no
    /// change to <c>DeleteAsync</c> at all.
    /// </remarks>
    public static void EnsureAvailable(
        IBusinessIdentifierIndex index, IEngineeringObjectRepository repository,
        string kind, Guid? projectScopeId, string businessIdentifier, Guid objectId)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(repository);

        if (index.FindConflict(kind, projectScopeId, businessIdentifier, objectId) is not { } holderId)
            return;

        var holder = repository.FindAsync(holderId, CancellationToken.None).GetAwaiter().GetResult();
        if (holder is IDeletable { IsDeleted: true })
        {
            index.Release(holderId);
            return;
        }

        throw BuildConflictException(kind, businessIdentifier, projectScopeId, holderId, repository);
    }

    /// <summary>Builds a fully-worded <see cref="DuplicateBusinessIdentifierException"/> for a conflict <see cref="IBusinessIdentifierIndex.FindConflict"/> already found, naming the project by its own display name where one is known.</summary>
    public static DuplicateBusinessIdentifierException BuildConflictException(
        string kind, string businessIdentifier, Guid? projectScopeId, Guid existingObjectId, IEngineeringObjectRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        string? projectDisplayName = null;

        if (projectScopeId is { } id)
        {
            var project = repository.FindAsync(id, CancellationToken.None).GetAwaiter().GetResult();
            projectDisplayName = (project as IHasBusinessIdentifier)?.DisplayName;
        }

        return new DuplicateBusinessIdentifierException(kind, businessIdentifier.Trim(), projectScopeId, projectDisplayName, existingObjectId);
    }
}
