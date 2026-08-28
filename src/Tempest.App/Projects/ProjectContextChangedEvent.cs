using Tempest.Core.Events;

namespace Tempest.App.Projects;

/// <summary>
/// Published on the existing <see cref="IEventBus"/> whenever the current
/// project changes — opened, switched, or closed.
/// </summary>
/// <remarks>
/// Every surface that shows or scopes by the current project subscribes to
/// this one event rather than being wired individually, mirroring
/// <c>WorkspaceSelectionChangedEvent</c>'s own established shape. This is
/// what makes "see the current project everywhere appropriate" a single
/// fact with many observers instead of a value copied into several places
/// that can drift.
/// </remarks>
/// <param name="Previous">The project that was current before the change, or <see langword="null"/> if none was.</param>
/// <param name="Current">The project that is current after the change, or <see langword="null"/> if the project was closed.</param>
public sealed record ProjectContextChangedEvent(ProjectSummary? Previous, ProjectSummary? Current) : IEvent;
