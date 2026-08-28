using Tempest.Core.Events;

namespace Tempest.App.Shell;

/// <summary>Published on the existing <see cref="IEventBus"/> after every shell navigation, carrying both ends of the move.</summary>
/// <param name="Previous">Where the user was.</param>
/// <param name="Current">Where the user now is.</param>
public sealed record ShellLocationChangedEvent(ShellLocation Previous, ShellLocation Current) : IEvent;
