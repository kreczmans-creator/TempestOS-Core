using Tempest.Core.Events;

namespace Tempest.Samples;

/// <summary>
/// The lifecycle transition a <see cref="ClockModuleLifecycleEvent"/>
/// reports.
/// </summary>
public enum ClockModuleLifecycleTransition
{
    /// <summary>Published from <see cref="ClockModule.InitialiseAsync"/>.</summary>
    Initialised,

    /// <summary>Published from <see cref="ClockModule.StartAsync"/>.</summary>
    Started,

    /// <summary>Published from <see cref="ClockModule.StopAsync"/>.</summary>
    Stopped,
}

/// <summary>
/// Published by <see cref="ClockModule"/> through <see cref="IEventBus"/>
/// each time it completes a lifecycle transition.
/// </summary>
/// <remarks>
/// An ordinary data type carrying whatever a subscriber needs to react to
/// the transition — module identity, which transition occurred, when, and
/// a correlation identifier tying every event this particular module
/// instance publishes together. Carries no behaviour and no reference to
/// <see cref="ClockModule"/> itself; a subscriber depends only on this
/// event type, never on the module that publishes it (ADR-0020).
/// </remarks>
public sealed class ClockModuleLifecycleEvent : IEvent
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ClockModuleLifecycleEvent"/> class.
    /// </summary>
    /// <param name="moduleId">The publishing module's <see cref="Core.Modules.IModule.Id"/>.</param>
    /// <param name="moduleName">The publishing module's <see cref="Core.Modules.IModule.Name"/>.</param>
    /// <param name="transition">Which lifecycle transition occurred.</param>
    /// <param name="timestamp">The moment the transition completed.</param>
    /// <param name="correlationId">
    /// An identifier shared by every event this particular module instance
    /// publishes, letting a subscriber correlate the full sequence of
    /// transitions from one run.
    /// </param>
    public ClockModuleLifecycleEvent(
        string moduleId,
        string moduleName,
        ClockModuleLifecycleTransition transition,
        DateTimeOffset timestamp,
        Guid correlationId)
    {
        ModuleId = moduleId;
        ModuleName = moduleName;
        Transition = transition;
        Timestamp = timestamp;
        CorrelationId = correlationId;
    }

    /// <summary>Gets the publishing module's <see cref="Core.Modules.IModule.Id"/>.</summary>
    public string ModuleId { get; }

    /// <summary>Gets the publishing module's <see cref="Core.Modules.IModule.Name"/>.</summary>
    public string ModuleName { get; }

    /// <summary>Gets which lifecycle transition occurred.</summary>
    public ClockModuleLifecycleTransition Transition { get; }

    /// <summary>Gets the moment the transition completed.</summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the identifier shared by every event this particular module
    /// instance publishes.
    /// </summary>
    public Guid CorrelationId { get; }
}
