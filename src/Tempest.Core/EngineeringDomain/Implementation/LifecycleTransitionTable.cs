namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// The canonical, platform-wide permitted-transition table over <see cref="LifecycleState"/> — mirrors
/// <see cref="Requirements.RequirementStatusTransitions"/>'s own dictionary-based shape. A same-to-same
/// transition is never permitted, matching that precedent exactly. <see cref="LifecycleState.Archived"/>
/// and <see cref="LifecycleState.Cancelled"/> are terminal — no contract in WP8.2B proposes a delete
/// operation, so a terminal state is reached and stays reached.
/// </summary>
public sealed class LifecycleTransitionTable : ILifecycleTransitionTable
{
    private static readonly IReadOnlyDictionary<LifecycleState, IReadOnlyList<LifecycleState>> PermittedTargets =
        new Dictionary<LifecycleState, IReadOnlyList<LifecycleState>>
        {
            [LifecycleState.Draft] = new[] { LifecycleState.InReview, LifecycleState.Cancelled },
            [LifecycleState.InReview] = new[] { LifecycleState.Approved, LifecycleState.Draft, LifecycleState.Cancelled },
            [LifecycleState.Approved] = new[] { LifecycleState.Released, LifecycleState.Draft, LifecycleState.Cancelled },
            [LifecycleState.Released] = new[] { LifecycleState.Superseded, LifecycleState.Obsolete },
            [LifecycleState.Superseded] = new[] { LifecycleState.Archived },
            [LifecycleState.Obsolete] = new[] { LifecycleState.Archived },
            [LifecycleState.Archived] = Array.Empty<LifecycleState>(),
            [LifecycleState.Cancelled] = Array.Empty<LifecycleState>(),
        };

    public bool IsPermitted(LifecycleState from, LifecycleState to) =>
        from != to && PermittedTargets.TryGetValue(from, out var targets) && targets.Contains(to);

    public IReadOnlyList<LifecycleState> GetPermittedTargets(LifecycleState from) =>
        PermittedTargets.TryGetValue(from, out var targets) ? targets : Array.Empty<LifecycleState>();
}
