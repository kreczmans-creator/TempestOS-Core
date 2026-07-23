namespace Tempest.Core.Events;

/// <summary>
/// Something that has happened, published for any interested subscriber to
/// observe.
/// </summary>
/// <remarks>
/// <para>
/// An event is data: a concrete type implementing this interface carries
/// whatever facts about what happened its subscribers need, as ordinary
/// properties. Unlike a <see cref="Commands.ICommand"/>, an event has zero
/// or more subscribers and no expected result — publishing an event to
/// nobody is not an error, and publishing to many subscribers does not
/// imply any of them owes the publisher a response.
/// </para>
/// <para>
/// This is a contract only. No event bus exists yet — declaring this
/// interface today introduces no new runtime behaviour. Per ADR-0020, the
/// future event bus is a DI-public platform service; a module publishes and
/// subscribes to events of this shape through it, never directly to another
/// module.
/// </para>
/// </remarks>
public interface IEvent
{
}
