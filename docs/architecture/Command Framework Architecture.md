# Command Framework Architecture

**Status: designed — WP 5.1A (ADR-0036, ADR-0037, ADR-0038). Not yet
implemented — WP 5.1B.**

## Objective

Design the Command Framework: a uniform, UI-agnostic way for a discrete
unit of application logic to be invoked consistently from any caller —
a menu, a toolbar, a keyboard shortcut, a context menu, a future touch
gesture, a future automation script, or a future AI service — without
the Runtime Host, Event Bus, or Navigation Framework needing to change,
and without introducing a second, competing notion of "how a module
gets asked to do something." This is `WP 5.1A` — architecture only. No
production code is modified; no implementation begins; `WP 5.1B`
implements exactly what this document decides.

## Repository Investigation

**`ICommand` already exists, as a contract only** (`src/Tempest.Core/
Commands/ICommand.cs`, `WP 4.0`). It is a plain marker interface with no
members — "a command is data: a concrete type implementing this
interface carries whatever parameters its own execution needs as
ordinary properties." Its own doc comment already commits this Work
Package to two things, confirmed by direct inspection, not assumed: **a
command is dispatched by its own concrete type** (not by some separate
descriptor type standing in for it), and **a command never depends on,
or is invoked by, Navigation** — ADR-0022, already Accepted, already
binding, not reopened here. No handler contract and no dispatcher exist
yet anywhere in the repository — confirmed directly (`grep -rl
"ICommandHandler\|ICommandDispatcher" src/` returns nothing).

**`CommandContractTests.cs` proves exactly what `WP 4.0` intended and
nothing more**: that a concrete command type can be constructed, carries
its own data, and is assignable to `ICommand` — no dispatch, no
handler, no registration is exercised, because none exists.

**What already exists that this design must reuse, not duplicate:**

- **`IEventBus`/`EventBus`** (`Tempest.Core.Events`, `WP 4.4D`, ADR-0028)
  — imperative `Subscribe`/`Unsubscribe`, a single `_gate`-locked
  dictionary keyed by exact type, a snapshot-then-dispatch-outside-the-
  lock pattern. The nearest structural precedent for how a command
  handler might be registered — but, critically, **not** the transport a
  command should be dispatched *through*: `Risks.md` R3 and
  `WorkPackages.md`'s own `WP 5.1` scope both already state the
  governing distinction explicitly — "an event has zero or more
  subscribers and no expected result; a command has exactly one handler
  and an expected result." Reusing `IEventBus.PublishAsync` for command
  dispatch would mean a `SaveProjectCommand` failure is caught, logged,
  and silently absorbed exactly like an isolated event-subscriber
  failure (ADR-0028) — the opposite of what "an expected result" means.
  See RD-0039.
- **`INavigationProvider`/`NavigationService`** (`Tempest.Core.Navigation`,
  `WP 5.0A`/`WP 5.0B`, ADR-0031/ADR-0032) — the second, independently-
  designed DI-public platform service with an imperative, string-keyed
  registry (`Register`/`Unregister`, an ordered `Items` snapshot). The
  closest precedent for "a UI needs to enumerate a catalogue of
  invokable things by a stable string Id, without the catalogue knowing
  anything about rendering" — exactly the shape a menu, toolbar, or
  keyboard-shortcut binding needs for Commands too. **Explicitly
  orthogonal** — ADR-0022 remains fully intact; neither this design nor
  `NavigationService` gains any new reference to the other.
- **`ModuleMetadataAttribute`** (ADR-0027) — proves the platform already
  has a working, declarative, reflection-based metadata mechanism where
  one is actually needed (Discovery runs before the DI container exists,
  so avoiding instantiation is a real constraint there). Command
  registration, like Navigation registration, happens *after*
  Dependency Injection Built — the instantiation-avoidance problem that
  justifies `ModuleMetadataAttribute` does not exist here. See RD-0038.
- **`ADR-0017`** — Discovery, Registration, and Lifecycle are Host-owned,
  never DI-public. Applied again below to settle whether the Command
  Framework carries orchestration authority (it does not).
- **`ADR-0023`** — the four-layer platform model, downward-only
  dependencies. Whatever the Command Framework turns out to be, it is
  classified against this model, not exempted from it.
- **The Registry Pattern** (`docs/academy/04 Design Patterns/
  01-the-registry-pattern.md`) — already applied twice
  (`RuntimeModuleManager`, `NavigationService`); this design applies it a
  third time, for the same reason both prior applications exist: a
  single, trustworthy, duplicate-rejecting, read-only-externally
  catalogue of "what do we know about X."
- **`TempestServiceProvider`** (`Tempest.Core.DependencyInjection`) — a
  simple, type-keyed container with exactly two lifetimes (Singleton,
  Transient), no generic/open-generic registration, and no mechanism for
  a module to register a *new* service into the container after it is
  built (the `ServiceCollection` is frozen into a `TempestServiceProvider`
  once, during Dependency Injection Built, before any module is even
  constructed). This is a real, load-bearing constraint on the
  registration model below — confirmed by direct inspection of
  `TempestServiceProvider.cs` and `ServiceCollection.cs`; not assumed.
  See RD-0040.
- **`Tempest.App.Shell`** (`WP 5.0C`/`WP 5.0D`, ADR-0033–ADR-0035) —
  already names Commands as a future consumer of its own input handling,
  without designing anything: "a menu selection may dispatch a command
  whose own handler calls `NavigationService.Navigate(...)`, exactly as
  `ADR-0022`'s own... shape illustrates. Neither Navigation nor Commands
  depends on the other; the Shell wires them." This design fulfils that
  forward reference; it does not revisit the Shell's own composition
  model, which requires no change.

**No duplication found.** Nothing under `Tempest.Core` currently
dispatches anything by an ID a UI could bind a keystroke to; nothing
under `Tempest.App` currently presents a command surface. The boundary
this Work Package draws does not overlap or re-decide anything an
existing platform service already owns.

## Architecture

### Is the Command Framework a Platform Service?

Yes. Applying `ADR-0017`'s own test — *does this component carry
orchestration authority over the module pipeline (register, initialise,
start, stop, dispose)?* — a command dispatcher clearly does not. It
cannot register a module, retrigger Discovery, or drive anything through
its own lifecycle; it can only accept a request to run one already-
registered piece of application logic and report what happened.
**The Command Framework is therefore DI-public**, registered as an
ordinary container-constructed singleton — the identical shape ADR-0020
established for the Event Bus and ADR-0032 confirmed a second time for
Navigation, now confirmed a third time. See ADR-0036.

### Two Contracts, Not One: Dispatch and Discovery Are Different Problems

A command has two genuinely different consumers, with two genuinely
different needs, and conflating them into one interface would force one
need to compromise the other:

1. **A caller that already has a concrete, typed command instance with
   real data** — for example, application logic that already knows it
   wants to save a specific project (`new SaveProjectCommand("tempest.
   sample")`). This caller needs **type-safe dispatch**: hand the
   compiler a concrete `ICommand`, get back a result, know at compile
   time which handler contract must exist.
2. **A caller that only has a string** — a keyboard shortcut binding
   loaded from configuration, a menu definition, a toolbar button, a
   future automation script, a future AI service enumerating "what can
   I ask this application to do." None of these carry a C# generic type
   parameter at runtime; all of them need **Id-based invocation**: look
   up a command by a stable string, and trigger it, without the caller
   ever needing to reference a concrete `ICommand` type at compile time.

Two contracts, matching these two needs exactly:

```csharp
namespace Tempest.Core.Commands;

/// <summary>
/// Handles exactly one concrete <see cref="ICommand"/> type.
/// </summary>
public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    Task<CommandResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Dispatches a concrete, already-constructed command to its one
/// registered handler.
/// </summary>
public interface ICommandDispatcher
{
    void RegisterHandler<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ICommand;

    Task<CommandResult> DispatchAsync<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : ICommand;
}

/// <summary>
/// Describes one invokable command for a caller that has only a string
/// Id — a menu, a toolbar, a keyboard shortcut, automation, or an AI
/// service — without that caller ever needing <typeparamref name="TCommand"/>
/// at compile time.
/// </summary>
public sealed class CommandDescriptor
{
    public CommandDescriptor(
        string id,
        string displayName,
        string? category = null,
        string? description = null,
        string? icon = null,
        Func<bool>? canExecute = null,
        Func<ICommand>? createDefault = null);

    public string Id { get; }
    public string DisplayName { get; }
    public string? Category { get; }
    public string? Description { get; }
    public string? Icon { get; }
    public Func<bool>? CanExecute { get; }
    public Func<ICommand>? CreateDefault { get; }
}

/// <summary>
/// The Id-keyed catalogue of every registered <see cref="CommandDescriptor"/>
/// — the Command Framework's own Registry-pattern application, and the
/// surface a menu, toolbar, keyboard-shortcut map, or future AI/automation
/// caller enumerates and invokes against.
/// </summary>
public interface ICommandRegistry
{
    void RegisterDescriptor(CommandDescriptor descriptor);
    IReadOnlyList<CommandDescriptor> Items { get; }
    Task<CommandResult> InvokeAsync(string id, CancellationToken cancellationToken = default);
}

public sealed class CommandResult
{
    public static CommandResult Success(string? message = null);
    public static CommandResult Failure(string message);

    public bool Succeeded { get; }
    public string? Message { get; }
}

public class CommandException : Exception { /* base, mirrors NavigationException/PluginException's own shape */ }
public sealed class DuplicateCommandHandlerException : CommandException { }
public sealed class DuplicateCommandIdException : CommandException { }
public sealed class CommandHandlerNotRegisteredException : CommandException { }
public sealed class CommandNotFoundException : CommandException { }
```

**Why a `CommandResult` return type, when `ICommand` itself carries no
result shape.** A command "has an expected result" (the property that
distinguishes it from an event, per `Risks.md` R3 and the Engineering
Glossary) — but that result is about *outcome* (did it succeed; if not,
why), not necessarily a typed return *value*. `v0.5` defines only the
outcome shape; a future, generic `ICommand<TResult>`/`ICommandHandler<TCommand,
TResult>` pair, for a command whose caller needs a typed value back
(not merely success/failure), is explicitly deferred — see Deferred,
below — because no current or near-term consumer needs one, and
guessing its shape now would be exactly the speculative-design-ahead-of-
need pattern this project's own principles already warn against.

**Why `InvokeAsync` returns `Task<CommandResult>` but only supports a
parameterless `CreateDefault`.** Id-based invocation (a keyboard
shortcut, a menu item, an AI service enumerating `Items`) is,
deliberately, scoped to commands that need no caller-supplied data —
"Save," "Undo," "New Project" — which is the overwhelming majority of
what a menu or keyboard shortcut binds to in practice. A command that
genuinely needs caller-supplied parameters (`OpenModuleCommand("tempest.
module.alpha")`) is dispatched through `ICommandDispatcher.DispatchAsync`
directly, by whatever code already has the data — exactly as `ICommand`'s
own doc comment already anticipated ("dispatched by its own type"). This
is an explicit, honest scope boundary, not an oversight: `CommandDescriptor.
CreateDefault` is `Func<ICommand>?`, not `Func<object?, ICommand>` —
extending it to accept caller-supplied parameters through an Id-based
path, if a real future need arises, is a purely additive change to
`CommandDescriptor` alone.

### Registration Model: Imperative, Two-Part, Mirroring Navigation and the Event Bus

**Decision: a module constructor-injects `ICommandDispatcher` and/or
`ICommandRegistry` and registers, imperatively, during its own
`InitialiseAsync`/`StartAsync`** — the identical call-site shape
`ClockLifecycleObserverModule` already uses for `IEventBus.Subscribe`
and `NavigationSampleModule` already uses for `INavigationProvider.
Register`. Two calls, not one, because dispatch and discovery are
different problems (above):

```csharp
// In a module's InitialiseAsync:
_commandDispatcher.RegisterHandler<SaveProjectCommand>(new SaveProjectCommandHandler(_projectService));
_commandRegistry.RegisterDescriptor(new CommandDescriptor(
    id: "file.save",
    displayName: "Save Project",
    category: "File",
    canExecute: () => _projectService.HasOpenProject,
    createDefault: () => new SaveProjectCommand(_projectService.CurrentProjectId!)));
```

**Why imperative, not declarative/reflection-based** (mirrors
`Navigation Framework Architecture.md`'s own reasoning exactly, restated
here because it is being applied to a new component, not merely cited):
a declarative, attribute-read-by-reflection mechanism
(`ModuleMetadataAttribute`'s own shape) exists specifically to answer
metadata questions *without instantiating* the type in question — a
real constraint only because Discovery runs before the DI container
exists. Command registration happens *after* Dependency Injection
Built, *during* Module Initialisation — the module is already
constructed and already being driven through its own lifecycle. There is
no instantiation-avoidance problem here for a declarative mechanism to
solve, and introducing one anyway would duplicate machinery that already
exists for a case that needs it. See RD-0038.

**Why not resolve `ICommandHandler<TCommand>` through the DI container
itself** (the shape `ICommand`'s own `WP 4.0` doc comment might suggest
— "the framework... resolves exactly one handler for a given command
type"). `TempestServiceProvider` has no open-generic registration and no
mechanism for a module to add a new registration to the `ServiceCollection`
after it has already been frozen into a provider — confirmed directly in
the Repository Investigation, above. Making `ICommandHandler<TCommand>` a
real, container-resolved service type would require either (a) a new
Host-side reflection pass scanning every module for `ICommandHandler<T>`
implementations before the container is built (a new Discovery-shaped
mechanism, structurally invasive, and unnecessary — see RD-0040), or (b)
extending the container itself with open-generic/keyed registration (a
genuine container redesign, squarely out of this Work Package's scope
per its own governing rule). **Registering a handler *instance* directly
with `ICommandDispatcher`, exactly as `IEventBus.Subscribe<TEvent>(IEventHandler<TEvent>
handler)` already registers a subscriber *instance*, needs zero new DI
capability** — the module constructs (or already holds, via its own
ordinary constructor injection) the handler instance, and hands it to
the dispatcher. "Exactly one handler for a given command type" is still
true and still enforced — just enforced by `ICommandDispatcher`'s own
internal dictionary, at registration time, the same place
`NavigationService.Register` enforces "exactly one item for a given Id."
This satisfies `ICommand`'s own original invariant without requiring any
extension to the DI container — the correct outcome for a design
Work Package that finds an *implied* approach insufficient but does not
need to redesign anything to correct it.

**Duplicate registration is rejected, never silently overridden.**
`RegisterHandler<TCommand>` throws `DuplicateCommandHandlerException` if
a handler for `TCommand` is already registered; `RegisterDescriptor`
throws `DuplicateCommandIdException` if `Id` is already registered —
mirroring `NavigationService.Register`'s own `DuplicateNavigationItemException`
and `RuntimeModuleManager.Register`'s own `DuplicateModuleRegistrationException`
exactly: **first registration wins; a collision is a rejected, isolated
failure of the *later* registrant, never a silent takeover.** See
RD-0041, and the Security Review, below, for why this matters more here
than it first appears to.

**No `Unregister`/`Deregister` exists for either contract in `v0.5`.**
Deliberately, not by oversight — see the Security Review's discussion of
`NAV-1` (`docs/security/Platform Security Review v0.5.0.md`), which found
that `NavigationService.Unregister(string id)` has no ownership check:
any caller holding an `INavigationProvider` reference can remove any
other component's registered item. The Command Framework does not
repeat this shape. A command handler or descriptor, once registered,
persists for the Host's entire remaining run — the same permanent-
registration model `RuntimeModuleManager` already established, and one
this design deliberately holds to rather than reintroducing a
known-problematic removal API for a need `v0.5` does not have.

### Dispatch Model: Failures Propagate, They Are Not Isolated

**Decision: `DispatchAsync`/`InvokeAsync` let a handler's own exception
propagate directly to the caller — they do not catch, log, and isolate
it the way `EventBus.PublishAsync` isolates a subscriber's exception.**
This is a deliberate, reasoned divergence from the Event Bus's own
failure model, not an inconsistency: "a command has exactly one handler
and an expected result" (the Engineering Glossary's own, already-
established distinction) means the caller — a menu, a keyboard shortcut
handler, an automation script, a future AI service — genuinely needs to
know whether the command it asked for actually succeeded, in order to
react (show an error, retry, report failure up its own chain). Silently
absorbing that failure the way an isolated event-subscriber failure is
absorbed would make "expected result" a fiction. `CommandResult.Failure`
is available for a handler that wants to report a *business*-level
failure (an invalid save, a validation error) without throwing at all;
an exception is reserved for a genuine defect in the handler's own
execution, exactly as the platform's existing exception conventions
already distinguish "an expected, named failure case" from "something
went wrong that nobody anticipated." See ADR-0038 and the updated
Failure Isolation Across TempestOS concept guide (Case 5, below).

`OperationCanceledException` propagates identically to every other
cancellable operation in this platform — checked before dispatch, never
swallowed, mirroring `EventBus.PublishAsync`'s and `NavigationService.
Navigate`'s own established convention.

### Ownership and Dependency Direction

```
Module / Plugin-loaded Module
        │  (constructor-injects)
        ▼
ICommandDispatcher / ICommandRegistry
        │
        ├── RegisterHandler<TCommand>(instance)   — dispatch-side
        └── RegisterDescriptor(descriptor)         — discovery-side
        
Caller with a concrete ICommand ──▶ ICommandDispatcher.DispatchAsync<TCommand>
Caller with only a string Id     ──▶ ICommandRegistry.InvokeAsync(id) ──▶ (internally) DispatchAsync
```

- A module or plugin-loaded module depends downward on
  `ICommandDispatcher`/`ICommandRegistry`, exactly as it already depends
  on `IEventBus`, `INavigationProvider`, `ILogger`, or any other
  DI-public service (ADR-0023: dependencies flow downward only).
- The concrete registry/dispatcher implementation depends on nothing
  module-specific — it holds `ICommandHandler<TCommand>` instances and
  `CommandDescriptor` values as opaque data and delegates, exactly as
  `EventBus` holds `IEventHandler<TEvent>` instances without knowing
  what any of them do.
- **Never depends on `INavigationProvider`, and is never depended on by
  it.** ADR-0022 remains fully intact: a command handler's own
  application logic may call `NavigationService.Navigate(...)` directly
  as an ordinary, explicit dependency (exactly as `ADR-0022`'s own
  `OpenModuleCommand → NavigationService.Navigate(...)` illustration
  already shows) — the Command Framework itself never references
  Navigation, and `NavigationService` never references the Command
  Framework.
- **Never depends on `IEventBus`, and is never dispatched through it.**
  A command handler's own application logic may, as one of its effects,
  publish an ordinary event through `IEventBus` (exactly as `ADR-0022`'s
  own `SaveProjectCommand → ProjectService.Save() → ProjectSavedEvent`
  illustration already shows) — but the Command Framework's own
  dispatch path never touches `IEventBus`.
- `Tempest.App`'s Shell (or any future UI shell) depends downward on
  `ICommandRegistry` (to enumerate `Items` for a menu/toolbar/keyboard-
  shortcut binding and to call `InvokeAsync`) exactly as it already
  depends downward on `INavigationProvider`/`IEventBus` via
  `ITempestHost.Services` — no change to that resolution mechanism is
  needed; `ICommandDispatcher`/`ICommandRegistry` are simply two more
  DI-public services reachable through the same, already-implemented
  surface (ADR-0034).

### Command, Event, Navigation, Module — the Four-Way Distinction

A recurring question this design exists to answer clearly, once, rather
than leave implicit:

| Concept | Cardinality | Expects a result? | Invoked by | Owns behaviour? |
|---|---|---|---|---|
| **Module** | One instance per discovered type | N/A — has a lifecycle, not an invocation | The Host, through Discovery/Registration/Lifecycle | Yes — a module *is* a unit of long-lived platform capability |
| **Event** | Zero or more subscribers | No | Any publisher, via `IEventBus.PublishAsync` | No — a subscriber reacts; the event itself carries only data |
| **Navigation** | One registered item per Id | No (publishes a notification; does not itself "succeed" or "fail" as a unit of work) | A caller with an Id, via `NavigationService.Navigate` | No — the model holds metadata; rendering decides what "going there" means |
| **Command** | Exactly one handler per type | **Yes** | A typed caller (`DispatchAsync`) or a string caller (`InvokeAsync`) | No — a command carries data; its handler owns the behaviour, exactly as `IModuleLifecycle` (not `IModule`) owns a module's own behaviour |

**A command is not a module** — it has no lifecycle, is not discovered
by reflection, and is not driven through Initialise/Start/Stop/Dispose.
**A command is not an event** — it has exactly one handler, not zero or
more, and its caller expects to know the outcome. **A command is not
navigation** — Navigation's `Items`/`Navigate` describe "where a user can
go"; the Command Framework's `Items`/`InvokeAsync` describe "what a user
can ask the application to do," and the two remain orthogonal by design
(ADR-0022) even though their public shapes — an Id-keyed registry, an
ordered `Items` snapshot for a UI to enumerate — echo one another
deliberately, since both solve the identical "let a UI-agnostic platform
service tell a UI what exists" problem.

## Integration

**Runtime Host.** No change. `ICommandDispatcher`/`ICommandRegistry` are
registered as ordinary singletons during the existing Platform Services
Registered phase (Phase 6) — `services.Singleton<ICommandDispatcher,
CommandDispatcher>()`, alongside the existing `services.Singleton<IEventBus,
EventBus>()` and `services.Singleton<INavigationProvider, NavigationService>()`
lines. No new `Host Lifecycle.md` phase, no new `HostState`, no new
transition — the identical "DI-public platform service needs no Host
Lifecycle change" outcome Navigation and the Event Bus each already
demonstrated.

**Event Bus.** A peer, never a transport for command dispatch (see
Dispatch Model, above, and RD-0039). A command handler's own application
logic may depend on `IEventBus` directly and publish an event as one of
its effects — the same peer relationship ADR-0022 already establishes
between Navigation and application logic, now stated for Commands too.

**Navigation.** Orthogonal, per ADR-0022 — reaffirmed, not reopened. A
command handler's own application logic may depend on
`INavigationProvider` directly and call `Navigate(...)` as one of its
effects. Neither service references the other's contract.

**Application Shell.** The Shell becomes exactly the "application logic"
ADR-0022 already describes, now with a second DI-public service to
resolve through `ITempestHost.Services` alongside `INavigationProvider`/
`IEventBus`. A future Shell input-handling change (translating a
keystroke or menu selection into `ICommandRegistry.InvokeAsync(id)`) is
`WP 5.1B`'s or a later Work Package's own implementation concern — this
document requires no change to the Shell's own composition model
(`Shell & Composition Framework Architecture.md`) to make that possible;
the seam already exists.

**Modules.** A module constructor-injects `ICommandDispatcher`/
`ICommandRegistry` and registers its own handler(s)/descriptor(s) during
`InitialiseAsync`/`StartAsync`, exactly mirroring how it already
registers with `IEventBus`/`INavigationProvider`. No change to
`ModuleBase`/`ModuleLifecycleBase` (the Module SDK) is required — both
already support constructor-injecting any DI-public service.

**Hosted Services.** No integration. A hosted service has no
user-facing invocation surface to bind a command to; if a hosted
service's own work ever needs to be triggered on demand, that is a
command handler that itself depends on whatever the hosted service
exposes as an ordinary collaborator — no special-casing, and no new
capability, is introduced for this.

**Plugins.** No plugin-specific mechanism is introduced — a plugin-loaded
module registers command handlers and descriptors exactly as a
first-party module does, through ordinary constructor injection, zero
new mechanism, mirroring Navigation's and the Event Bus's own plugin
parity exactly. **See the Security Review, below, for the trust-boundary
implication this parity carries** — the Command Framework introduces no
*new* vulnerability, but it does give the platform's already-disclosed
plugin-trust gap (`docs/security/Platform Security Review v0.5.0.md`,
`TD-09`) a second, user-visible surface to manifest through.

**Dependency Injection.** No new container capability is required —
confirmed directly above (Registration Model). `ICommandDispatcher`/
`ICommandRegistry` are registered exactly as `IEventBus`/`INavigationProvider`
already are; `ICommandHandler<TCommand>` instances and `CommandDescriptor`
values are held as opaque, imperatively-registered data, never resolved
by the container itself.

**Logging.** No new logging capability required. `CommandDispatcher`/
`CommandRegistry` accept an optional `ILogger?`, the universal
constructor convention every other platform service already uses, and
log registration/dispatch/failure activity through it exactly like
`EventBus`/`NavigationService` already do.

**Configuration.** No command-specific configuration exists or is
designed for `v0.5`. A future, user-customisable keyboard-shortcut-to-
command-Id binding would need a configuration-backed map — explicitly
deferred, named here so it is not silently forgotten, not because it is
currently planned (mirroring Navigation's own explicit deferral of a
permission model it does not yet need).

## User Experience (Architecture Only — No UI Implementation)

**Desktop.** A menu item, a toolbar button, a keyboard shortcut, and a
context menu entry are all, structurally, the same thing from the
Command Framework's own point of view: a UI-specific trigger bound to a
`CommandDescriptor.Id`. `Tempest.App`'s Shell (or any future desktop UI
technology) owns its *own* private mapping from a keystroke, a menu
position, or a toolbar slot to a command Id — exactly the same shape the
Shell already owns for `NavigationItem.Id` (`Shell & Composition
Framework Architecture.md`'s "Page/View Construction"). `Tempest.Core.Commands`
never sees, holds, or needs to know that mapping exists.

**Tablet (touch actions) and Phone (simplified actions).** Structurally
identical to Desktop's keyboard-shortcut/menu binding — a touch gesture
or a simplified action is just a different UI's own mapping to the same,
already-designed `CommandDescriptor.Id`. Nothing in `Tempest.Core.Commands`
needs to change, or even be aware, to support a future tablet or phone
shell — this is the direct, intended consequence of resolving invocation
at the Id-indirection layer rather than per-shell. **This is the design's
central promise to future form factors, made concrete now rather than
merely asserted**: a future tablet, mobile, or web interface consumes
the identical `ICommandRegistry`/`ICommandDispatcher` surface a console
shell consumes today.

**Future: AI Invocation, Automation, Scripting.** The framework requires
no AI-specific or automation-specific mode. A future AI service or
automation script enumerates `ICommandRegistry.Items`, filters by each
descriptor's own `CanExecute()`, reads `DisplayName`/`Category`/
`Description` to decide what a command does, and calls `InvokeAsync(id)`
— the identical path a keyboard shortcut already uses. Id-based
invocation *is* the automation-friendly surface; it does not need a
separate one built for it.

## Command Availability and Enable/Disable Behaviour

`CommandDescriptor.CanExecute` is an optional `Func<bool>?` predicate,
evaluated by the caller at query time — the identical shape and
identical reasoning `NavigationItem.IsVisible` already establishes
(`Navigation Framework Architecture.md`): `null` means always available;
a supplied predicate is evaluated fresh each time a caller (a menu
renderer, a toolbar, an automation script deciding what it may safely
invoke) needs to know. `ICommandRegistry` does not filter `Items` by
`CanExecute` on the caller's behalf — a registry reports what is
registered; interpreting availability is the reader's own job, exactly
as `NavigationService.Items` already does not filter by `IsVisible`.
`InvokeAsync` does **not** itself re-check `CanExecute` before
dispatching — a caller that already decided to invoke a command has
already made that judgement; re-checking silently inside the framework
would hide a caller's own bug (invoking something it should have known
was unavailable) rather than surfacing it. A handler remains free to
return `CommandResult.Failure` if invoked in a state it considers
invalid, regardless of what `CanExecute` last reported.

## Future Undo/Redo Compatibility

**Explicitly deferred, not designed now.** No current or near-term
consumer needs undo/redo, and guessing its shape ahead of a real need
would be exactly the speculative-design pattern this project's own
principles already warn against (mirroring Navigation's own deferral of
a permission model, and `RD-0002`'s original deferral of authentication
concepts platform-wide). The design above does not foreclose it: a
future `IUndoableCommand` marker interface, or a handler that returns an
inverse command as part of its own `CommandResult`, could be layered on
top of `ICommandHandler<TCommand>`/`ICommandDispatcher` without changing
either contract's own shape — the same "additive, not a redesign"
property Navigation's `IsVisible` seam already demonstrates for a future
permission model.

## Security Review (Mandatory — Against the Platform Security Baseline)

Per the Platform Security Baseline established by `WP 5.0S`
(`docs/security/Platform Security Review v0.5.0.md`), every area named
in this Work Package's own brief was reviewed against the threat model
in `docs/security/Threat Model.md`.

**Public API exposure.** `ICommandDispatcher`/`ICommandRegistry` are
DI-public, exactly like `IEventBus`/`INavigationProvider` — no more
exposure than an already-accepted, already-reviewed precedent.
**Reviewed. No new concern.**

**Dependency Injection exposure.** No new DI capability is introduced
(see Registration Model, above); no service-locator risk is created —
`ICommandDispatcher`/`ICommandRegistry` themselves carry no reference
back to the container that constructed them, identical to every
existing DI-public service reviewed by `WP 5.0S`. **Reviewed. No new
concern.**

**Trust boundaries and future privilege escalation.** The Command
Framework introduces no *new* trust boundary crossing — a plugin-loaded
module already had full process trust before this design (`WP 5.0S`,
`TD-09`), and registering a command handler grants it no capability it
did not already have. What the Command Framework *does* do is give
`TD-09`'s already-disclosed risk a second, **user-visible** surface: a
plugin's own command could appear in `ICommandRegistry.Items` —
indistinguishable in shape from a first-party command — and a user could
be invited to invoke it, believing it to be a first-party feature. This
is not a new vulnerability; it is `TD-09`'s existing scope, now
concretely widened to include a UI-facing consumer. **Recorded: `TD-09`'s
own Technical Debt Register entry is updated to name the Command
Framework as a second affected surface, alongside Navigation. No new
Technical Debt entry is created for this specific point** — minting a
second entry for the same underlying architectural gap would
double-count one root cause as two, contrary to `Security Principles.md`
Principle 7.

**Command spoofing.** Addressed directly by this design's own
duplicate-rejection rule (`DuplicateCommandHandlerException`/
`DuplicateCommandIdException`, above): a later registration for an
already-claimed command type or Id is rejected and isolated, never
silently accepted as an override. **However, this Work Package's own
security review surfaced a genuine, non-obvious gap this rejection rule
does not close, present identically in the already-implemented
Navigation Framework and not previously disclosed by `WP 5.0S`:**

> **Finding CMD-1 (Medium) — Registration-order squatting.**
> "First registration wins" rejects a *later* duplicate, but does
> nothing to establish that the *first* registrant was the intended
> owner of a well-known Id. `ModuleLifecycleManager` initialises modules
> in ascending-Id order (unchanged, existing behaviour); if a
> plugin-loaded module's own Id happens to sort before the first-party
> module that a well-known command Id (for example, `"file.save"`)
> legitimately belongs to, the plugin's `RegisterDescriptor`/
> `RegisterHandler` call runs first, "wins" the collision check
> legitimately by this design's own rule, and the *real* owner's later
> registration is the one rejected and isolated — silently, per the
> existing, unmodified per-module isolation (ADR-0013), exactly as any
> other module-Initialise-time failure already is. **Why it matters:** a
> user could invoke what they believe is the first-party "Save" command
> and instead run a plugin's own handler for it, entirely within this
> design's own stated rules. **Exploit scenario:** a plugin author
> chooses a module Id that sorts alphabetically ahead of the legitimate
> owner's Id (command and navigation Ids are not secret — they are the
> kind of identifier a plugin author integrating with TempestOS would
> reasonably know or discover) and registers the same well-known command
> Id first. **Impact:** requires `TD-09`'s own precondition (a plugin
> already loaded with full process trust) to already hold; the
> *marginal* new risk this finding adds on top of `TD-09` is the
> specific, concrete mechanism by which that existing trust gap could be
> used to impersonate a *specific*, named, user-facing capability,
> rather than a general statement that a plugin is fully trusted.
> **Recommendation:** a future ownership/priority/reservation model
> (for example, reserving a namespace prefix for first-party command and
> navigation Ids, or giving a first-party registration explicit priority
> over a plugin-sourced one) — an architectural change, out of this Work
> Package's own scope. **Introduced by this Work Package or
> pre-existing:** the underlying mechanism (first-registration-wins by
> module-Initialisation order) is pre-existing — it already applies to
> `NavigationService.Register` today, entirely unchanged by this Work
> Package. This Work Package's own review is what surfaced it, for both
> Navigation and the newly-designed Command Framework; `WP 5.0S`'s own
> audit did not examine registration-order specifically. **Fixed:** No —
> architectural; recorded as `Technical Debt Register.md` `TD-11` and
> `docs/security/Security Roadmap.md`, item 10.

**Command injection.** `InvokeAsync(string id, ...)` performs a
dictionary lookup against a fixed set of *already-registered* Ids — it
does not construct a type from a caller-supplied string via reflection,
`Activator.CreateInstance`, or any dynamic-evaluation mechanism. There is
no vocabulary here an attacker could extend beyond what a trusted
registration call already established. **Reviewed. No injection vector
identified.**

**Plugin implications.** Covered above (Trust boundaries; `CMD-1`) — no
additional finding beyond those two.

**Reflection implications.** This design uses no reflection anywhere —
confirmed directly above (Registration Model rejects a reflection-based
alternative, RD-0038/RD-0040, for reasons unrelated to security but with
the incidental security benefit that no new reflection-based attack
surface is introduced). **Reviewed. No concern.**

**Event Bus interaction.** Covered above (Integration; Dispatch Model) —
the Command Framework never dispatches through `IEventBus`, and a
handler's own use of `IEventBus` as a peer carries no different risk
than any other module's existing use of it, already reviewed by
`WP 5.0S`. **Reviewed. No new concern.**

**Logging.** No secret or credential is carried by `CommandDescriptor`
or `CommandResult` by design (display metadata and a success/failure
outcome only). If a future concrete command type carries sensitive
parameters (a password field, a future authentication command), logging
it in plaintext is the same, already-disclosed gap `WP 5.0S` recorded as
`SEC-02` (no secrets-redaction convention in the logging framework) —
cross-referenced, not a new finding.

**Future multi-user implications.** `CommandDispatcher`/`CommandRegistry`
are process-wide singletons, exactly like `EventBus`/`NavigationService`
— inherits `WP 5.0S`'s `FR-1` finding (no per-tenant DI scope) directly.
Cross-referenced, not a new finding.

**Summary.** One new finding (`CMD-1`, Medium, not fixed, architectural,
recorded as `TD-11`); one existing finding's scope widened (`TD-09`, not
re-severity-rated). **The Platform Security Baseline is not weakened by
this Work Package.** No fix was implemented — correctly, since both
require an architectural ownership/priority model out of this Work
Package's own scope, per its own governing rule to STOP and recommend
rather than redesign.

## Architecture Review

**Existing ADRs reviewed:** ADR-0009 (Composition Root — not engaged;
the Command Framework needs no Composition Root treatment, exactly as
the Event Bus did not), ADR-0017 (Discovery/Registration/Lifecycle
Host-owned — unaffected; the Command Framework carries no orchestration
authority), ADR-0020 (Event Bus DI-public — the direct precedent this
design's own ownership decision reasons from), ADR-0022 (Navigation/
Command orthogonality — reaffirmed, not reopened; this is the first
Work Package to actually design the "Command" side of that decision),
ADR-0023 (four-layer model — preserved), ADR-0027 (`ModuleMetadataAttribute`
— directly reasoned from, to explain why its own mechanism is *not*
reused here), ADR-0028 (Event Bus dispatch/failure model — directly
contrasted with, to justify the Command Framework's own, deliberately
different failure model), ADR-0031/ADR-0032 (Navigation — the closest
structural precedent for the registry half of this design).

**No existing ADR proved insufficient in a way this Work Package could
not resolve within existing architecture.** The one place an *implied*
prior direction (dispatch resolved through the DI container, per
`ICommand`'s own `WP 4.0` doc comment) met a real constraint (the
container's lack of open-generic/keyed registration) was resolved by
reusing the Event Bus's own imperative-instance-registration shape, not
by extending the container — no STOP condition was triggered.

**New ADRs required:** three, at the same granularity Navigation
(ADR-0031, ADR-0032) and the Shell (ADR-0033–ADR-0035) each received —
**ADR-0036** (the Command Framework is a DI-public platform service),
**ADR-0037** (imperative, two-part registration; rejects declarative/
reflection/Event-Bus-as-transport alternatives), **ADR-0038** (dispatch
failures propagate to the caller, deliberately diverging from the Event
Bus's per-subscriber isolation).

**Rejected Designs added:** four — **RD-0038** (declarative/attribute-
based command registration), **RD-0039** (dispatching commands through
the Event Bus), **RD-0040** (`ICommandHandler<TCommand>` as a
DI-container-resolved, reflection-discovered service), **RD-0041**
(allowing a later registration to silently override an earlier one).

**Architecture documents requiring an update, found during this Work
Package's own review (not new drift introduced by it):**

- `Platform Service Map.md` — the Command Framework's own entry updated
  from "contract implemented; dispatcher planned" to "architected —
  `WP 5.1A`; dispatcher implementation pending `WP 5.1B`," mirroring
  Navigation's and the Shell's own status-line convention exactly.
- `Ownership Matrix.md` — a new Command Framework row added (DI-public,
  container-constructed, mirroring the existing Event Bus row).
  **A genuine, pre-existing documentation drift was found and corrected
  along the way, unrelated to this Work Package's own design work:**
  `Ownership Matrix.md` never received a Navigation row at all, at
  either `WP 5.0A` or `WP 5.0B` — confirmed by direct inspection; no row
  for `INavigationProvider`/`NavigationService` existed anywhere in the
  file before this Work Package. Added now, disclosed here rather than
  silently patched, following this project's own "found drift is
  disclosed, not silently corrected" convention.
- `Engineering Glossary.md` — the existing "Command" entry (`WP 4.0`)
  updated from "contract implemented... handler contract and dispatcher
  not yet defined" to reflect this Work Package's own completed design,
  the same evolution its "Navigation" and "Event Bus" entries already
  went through at their own design phases.
- `docs/academy/02 Runtime Architecture/08-failure-isolation.md` — this
  concept guide's own "Future Evolution" section already named "a
  Command Framework handler" explicitly as a future test case for its
  four-case failure-isolation pattern. This Work Package is that test,
  and the outcome is genuinely a **fifth case** (Case 5 — Command
  Dispatch: propagate, do not isolate) — the first of the five cases
  where the answer is neither "isolated like a module" nor "no new case
  needed, like Navigation," but a third, deliberately different outcome.
  Updated accordingly; see the Academy Updates section below.

**No Rejected Design entry required updating**, and no existing
Rejected Design was found to conflict with this Work Package's own
conclusions.

## Testing Strategy (For `WP 5.1B` — Not Exercised by This Work Package)

No test is added by this Work Package (architecture only). The following
is the testing strategy `WP 5.1B`'s own implementation should satisfy,
following this project's own established "prefer real implementations
over mocks" convention:

- **Dispatch**: a registered handler is invoked with the exact command
  instance passed to `DispatchAsync`; its `CommandResult` is returned
  unchanged to the caller.
- **No handler registered**: `DispatchAsync`/`InvokeAsync` throw
  `CommandHandlerNotRegisteredException`/`CommandNotFoundException`
  respectively, mirroring `NavigationItemNotFoundException`'s own
  "unknown Id" precedent.
- **Duplicate registration**: a second `RegisterHandler<TCommand>` for an
  already-registered `TCommand`, or a second `RegisterDescriptor` for an
  already-registered `Id`, throws the corresponding `Duplicate*`
  exception and is isolated by the existing, unmodified
  `ModuleLifecycleManager` per-module isolation (ADR-0013) — no new
  Host-level failure policy is needed, mirroring Navigation's own proof
  of the identical point.
- **Handler exception propagation**: an exception thrown inside
  `HandleAsync` propagates, uncaught, out of `DispatchAsync`/`InvokeAsync`
  to the caller — proving the deliberate divergence from
  `EventBus.PublishAsync`'s own isolation, directly, the same way
  `EventBusTests.cs` proves isolation *does* happen there.
- **Cancellation**: `OperationCanceledException` propagates uncaught,
  checked before dispatch.
- **`CanExecute`**: `Items` reports every registered descriptor
  regardless of its own `CanExecute` result; `InvokeAsync` does not
  itself re-check `CanExecute` before dispatching.
- **Constructor injection through a real Host**: a real, discovered
  module (a new fixture, following `ClockModule`'s/`NavigationSampleModule`'s
  own precedent) constructor-injects `ICommandDispatcher`/
  `ICommandRegistry` and registers a real handler/descriptor during
  `InitialiseAsync`, proven through the real, unmodified `TempestHost`.
- **Plugin parity**: a plugin-loaded module registers a command handler
  through the identical path a normally-discovered module uses,
  mirroring `PluginAssemblyLoaderTests`'/`NavigationSampleModuleIntegrationTests`'
  own "prove the existing mechanism needs no change" methodology.
- **Registration-order squatting (`CMD-1`)**: at minimum, a test proving
  the *current, disclosed* behaviour (first registration wins,
  regardless of which module is "the intended owner") so the finding
  remains an honestly-documented, deliberately-accepted gap rather than
  an untested assumption — not a fix, since `CMD-1` is explicitly
  deferred.

## Required for v0.5 vs. Deferred Beyond v0.5

**Required for v0.5 (this design; implementation is `WP 5.1B`):**

- `ICommand` (existing, `WP 4.0`, unchanged), `ICommandHandler<TCommand>`,
  `ICommandDispatcher`, `CommandDescriptor`, `ICommandRegistry`,
  `CommandResult`, and the five exception types above.
- DI-public registration, no Host Lifecycle change.
- Module and plugin-loaded-module contribution via ordinary constructor
  injection — no special-casing for either.
- Duplicate-registration rejection for both handler and descriptor
  registration.
- Handler exceptions propagate to the caller, unisolated.

**Explicitly deferred beyond v0.5 (named here so they are not silently
forgotten, not because any of them is currently planned):**

- **A typed result value** (`ICommand<TResult>`/`ICommandHandler<TCommand,
  TResult>`) beyond success/failure — no current consumer needs one.
- **Undo/redo.** See its own section above.
- **A command Id ownership/priority/reservation model**, closing
  `CMD-1`/`TD-11`. Requires a future Architecture Work Package.
- **A first-class permission/role model** governing who may invoke which
  command — no authentication or authorisation concept exists anywhere
  in this platform yet; mirrors Navigation's own identical deferral.
- **User-customisable keyboard-shortcut/menu/toolbar bindings**, and any
  configuration-backed storage for them.
- **Command handler unregistration.** No current need; deliberately not
  designed, to avoid reintroducing `NAV-1`'s own known ownership gap in
  a new form.
- **A generic Id-based invocation path for parameterised commands**
  (beyond `CommandDescriptor.CreateDefault`'s parameterless factory).

## Future Extensibility

- **`WP 5.1B`** implements exactly the shape above.
- **`WP 5.2` (Diagnostics)** could enumerate `ICommandRegistry.Items`
  for a health/status page's own "recent commands" or "available
  actions" view, exactly as it may do the same for `INavigationProvider.
  Items` — no change to either service anticipated.
- **A future GUI, tablet, mobile, or web shell** consumes the identical
  `ICommandDispatcher`/`ICommandRegistry` surface this document designs
  — the direct, intended fulfilment of this Work Package's own stated
  future-form-factor objective, proven the same way `ADR-0031` already
  anticipated for Navigation: the platform/application boundary drawn
  here is real, not merely declared.
- **A future permission/ownership model** plugs into `CanExecute` and a
  new registration-priority mechanism without either `ICommandDispatcher`
  or `ICommandRegistry` needing to change shape — the seam is already in
  place, deliberately, per the deferrals above.

## Related Documents

`ADR-0017` (Discovery/Registration/Lifecycle Host-owned — unaffected);
`ADR-0020` (Event Bus DI-public — direct precedent); `ADR-0022`
(Navigation/Command orthogonality — binding, reaffirmed); `ADR-0023`
(four-layer model); `ADR-0027` (`ModuleMetadataAttribute` — reasoned
from, not reused); `ADR-0028` (Event Bus failure model — contrasted
with); `ADR-0031`/`ADR-0032` (Navigation — closest structural
precedent); `ADR-0036`–`ADR-0038` (this Work Package); `Rejected
Designs.md` (`RD-0038`–`RD-0041`); `Navigation Framework Architecture.md`;
`Event Bus Architecture.md`; `Shell & Composition Framework
Architecture.md`; `docs/security/Platform Security Review v0.5.0.md`
(`TD-09`, `NAV-1`); `docs/security/Security Roadmap.md` (item 10,
`TD-11`); `docs/governance/Quality/Technical Debt Register.md`
(`TD-09`, `TD-11`); `docs/academy/04 Design Patterns/
01-the-registry-pattern.md`; `docs/academy/02 Runtime Architecture/
08-failure-isolation.md` (Case 5); `docs/releases/v0.5.0/WorkPackages.md`
(`WP 5.1`).
