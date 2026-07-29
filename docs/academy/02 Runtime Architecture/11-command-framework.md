# Command Framework

**Status: designed — `WP 5.1A` (ADR-0036–ADR-0038). Implemented —
`WP 5.1B`, exactly as designed; see that Work Package's own retrospective
for the one small, disclosed implementation nuance (`CommandHandlerTable`).**

## 1. Introduction

A **command** is a request to do one specific thing, packaged as an
object, so that "what should happen" (the request) and "how it happens"
(the handler) can be written, tested, and invoked independently of each
other. TempestOS's Command Framework — architected in `WP 5.1A`
(`ADR-0036`–`ADR-0038`), building on the `ICommand` contract that has
existed since `WP 4.0` — is this project's own answer to a question
almost every application eventually faces: *how does a menu item, a
keyboard shortcut, a toolbar button, and a future automation script all
end up triggering the same piece of application logic, without four
separate, duplicated wiring paths?*

## 2. Purpose

To explain the Command Framework from first principles — assuming no
prior knowledge of command-oriented design — before describing how
TempestOS specifically implements it. A reader who has never built a
command system before should finish this document understanding not
just *what* `ICommandDispatcher`/`ICommandRegistry` do, but *why* a
system shaped this way exists at all, and why it looks different from
the two mechanisms (`IEventBus`, `INavigationProvider`) it most closely
resembles on the surface.

## 3. Background — Why Commands Exist as Their Own Concept

Consider an application with a "Save" feature. Without any dedicated
concept for it, "Save" ends up implemented directly, and separately,
inside a menu-click handler, a keyboard-shortcut handler, and a toolbar-
button handler — three copies of the same logic, or three call sites all
reaching into the same helper method by convention rather than by
contract. Add a fourth caller (an automation script, a test, a future AI
assistant) and the pattern repeats a fourth time. The **Command pattern**
— one of the original object-oriented design patterns, described in the
1994 "Gang of Four" book *Design Patterns* — solves this by making "Save"
itself a first-class object: a `SaveCommand` that carries whatever data
it needs and can be handed to *anything* that knows how to invoke a
command, without that caller needing to know what "Save" actually does.

A closely related idea, the **Mediator pattern**, generalises this
further: instead of a caller needing a direct reference to whatever
handles its request, a caller hands the request to a single, shared
mediator (in TempestOS's case, `ICommandDispatcher`), which is
responsible for finding and invoking whatever actually handles it. This
is precisely why TempestOS's own `ICommandDispatcher.DispatchAsync<TCommand>`
looks the way it does — the caller never holds a reference to
`SaveProjectCommandHandler` at all, only to the dispatcher and the
command.

A third, related idea worth knowing about — **CQRS** (Command Query
Responsibility Segregation) — separates commands (requests that change
state) from queries (requests that read state) as two entirely distinct
pipelines, often with different storage models for each.
**TempestOS does not adopt CQRS** — `ICommand` covers "do a thing," full
stop, with no parallel query-side contract, because no current or
near-term consumer needs the stronger separation CQRS provides, and
introducing one now would be exactly the kind of speculative design this
project's own principles warn against. Knowing that CQRS exists, and
knowing why TempestOS didn't reach for it, is part of understanding the
design that TempestOS *did* choose.

## 4. The Problem

`WP 5.1A`'s own brief named the requirement precisely: commands must be
invokable consistently from a menu, a toolbar, a keyboard shortcut, a
context menu, programmatic code, future automation, and a future AI
service — and the framework must be entirely UI-agnostic, so a future
tablet, mobile, or web interface can reuse it unchanged. A naive
Command-pattern implementation (a `SaveCommand` type and a hand-written
`if`/`switch` somewhere that knows how to run one) solves the *first*
caller (programmatic code, which already has a concrete type to
construct) but does nothing for the other five — none of them can
reference a C# generic type parameter at compile time. A keyboard
shortcut binding is data ("Ctrl+S maps to `file.save`"), not code; it
needs a stable **string** to bind to, resolved to a real command only at
the moment it is actually invoked.

## 5. The Design

TempestOS's Command Framework answers this with two contracts solving
two different problems, not one contract trying to do both (see
`Command Framework Architecture.md` for the complete design):

- **`ICommandDispatcher`** — the Mediator: a typed caller that already
  has a concrete `ICommand` instance calls `DispatchAsync<TCommand>`,
  which finds the one registered `ICommandHandler<TCommand>` and invokes
  it, returning a `CommandResult` so the caller actually knows whether it
  succeeded.
- **`ICommandRegistry`** — the Id-keyed catalogue a UI-agnostic caller
  needs: a `CommandDescriptor` (Id, display name, category, an
  `CanExecute` predicate, and a default-instance factory) is registered
  once, and any caller with only a string — a keyboard shortcut, a menu,
  automation, an AI service — calls `InvokeAsync(id)`, which resolves the
  descriptor, constructs a default command instance, and dispatches it
  through the same `ICommandDispatcher` above.

This mirrors, deliberately, the shape `INavigationProvider` already
established for an almost identical problem: "let a UI-agnostic platform
service tell a UI what exists, by a stable string Id, without the
service knowing anything about rendering." Where Navigation's Id-keyed
catalogue answers "where can a user go," the Command Framework's
answers "what can a user ask the application to do" — genuinely
different questions, deliberately similar answers, kept orthogonal by
`ADR-0022`.

## 6. Alternatives Considered

See `Command Framework Architecture.md`'s own "Registration Model"
section and `RD-0038`–`RD-0041` for the complete reasoning behind
rejecting declarative/attribute-based registration, dispatching through
the Event Bus, a reflection-discovered generic handler service, and
silent-override registration. In summary, each alternative was rejected
because it either solved a problem TempestOS does not have (an
instantiation-avoidance concern Discovery has and Commands do not), or
because it would have borrowed a mechanism whose own semantics do not
match what a command actually needs (the Event Bus's zero-or-more,
no-expected-result shape).

## 7. Why This Solution Was Chosen

Two contracts, not one, because "a typed caller with real data" and "a
string-only caller with no data" are genuinely different problems that a
single interface would have to compromise to solve simultaneously.
Imperative registration, not reflection, because the DI container is
already built by the time a module could register anything, removing
the one reason a declarative mechanism like `ModuleMetadataAttribute`
exists in the first place. Propagating handler exceptions rather than
isolating them, because a command's entire reason for existing —
"exactly one handler and an expected result" — depends on the caller
actually being able to observe that result.

## 8. Architectural Principles

- **Separation of Concerns** — dispatch (finding and invoking the one
  handler) and discovery (letting a UI enumerate what exists by Id) are
  two responsibilities, given two contracts, not folded into one.
- **Single Responsibility** — a `CommandDescriptor` describes; a
  handler behaves; a dispatcher routes. None of the three does another's
  job, mirroring the same split `IModule`/`IModuleLifecycle` already
  established for Modules.
- **Fail Fast** — a duplicate handler or descriptor registration is
  rejected immediately, loudly, at registration time, rather than
  silently tolerated and discovered later as confusing runtime behaviour.
- **Reuse Before Invention** — the registration shape reuses the Event
  Bus's own imperative-instance-registration pattern; the Id-catalogue
  shape reuses Navigation's own registry pattern. No new mechanism was
  invented where an existing one already solved the same underlying
  problem.

## 9. Benefits

- One framework serves every caller named in the requirement — desktop,
  tablet, phone, automation, and future AI invocation — without any of
  them needing special-case support, because Id-based invocation already
  *is* the automation-friendly, UI-agnostic surface.
- A command's "expected result" property is real, not just documented
  intent — a caller can trust that no thrown exception plus
  `CommandResult.Succeeded` means the command actually ran to
  completion.
- Zero new capability was required anywhere else in the platform — the
  DI container, the Runtime Host, the Event Bus, and Navigation are all
  unchanged.

## 10. Trade-offs

- Two registration calls (handler, descriptor) for a module that wants
  both typed and Id-based invocation of the same command — a small,
  deliberate cost of keeping the two concerns independently useful.
- "First registration wins" is not, by itself, a complete answer to
  command-Id *ownership* — a real, disclosed gap (`CMD-1`/`TD-11`, see
  `Command Framework Architecture.md`'s Security Review) that a future
  Work Package will need to address once third-party plugins are a real
  actor, not merely a future one.

## 11. Common Mistakes

- **Assuming a command should be dispatched through the Event Bus**
  because both look like "publish/subscribe-shaped" mechanisms at a
  glance. They are not the same shape: an event has zero or more
  subscribers and no expected result; a command has exactly one handler
  and an expected result. Reusing one for the other silently corrupts
  whichever property gets borrowed away.
- **Assuming a command handler's failure should be isolated**, by
  analogy with Event Bus subscribers or module lifecycle failures. A
  command's entire distinguishing property is that its caller needs to
  know the outcome — isolating that outcome away defeats the reason a
  command exists as a concept distinct from an event in the first place.
- **Reaching for reflection or a DI-container-resolved generic handler**
  before checking whether the container actually supports it.
  TempestOS's own container does not support open-generic or keyed
  registration — a fact worth confirming by reading the code, not
  assumed by analogy with richer DI containers used elsewhere.

## 12. Future Evolution

A typed result value (`ICommand<TResult>`), undo/redo, a command-Id
ownership/reservation model, and configuration-backed keyboard-shortcut
bindings are all named, explicitly deferred possibilities in `Command
Framework Architecture.md` — not designed now, because no real consumer
needs any of them yet. The framework's own seams (`CommandResult`,
`CanExecute`, the two-contract split) are deliberately shaped so that
each of these can be added later without requiring either
`ICommandDispatcher` or `ICommandRegistry` to change shape. `WP 5.1B`
implemented the design exactly as described here, with one internal
collaborator this document did not need to anticipate —
`CommandHandlerTable`, shared by the dispatcher and the registry so both
operate against the identical handler set — see that Work Package's own
retrospective for the full story.

## 13. Key Takeaways

1. A command is a request packaged as data, dispatched to exactly one
   handler, distinguished from an event specifically by carrying an
   expected result the caller must be able to observe.
2. Two contracts — a typed dispatcher, an Id-keyed registry — solve two
   genuinely different caller problems; collapsing them into one would
   force a compromise neither caller should have to accept.
3. A framework designed to serve "every possible future UI" does not
   need UI-specific machinery — it needs one, well-chosen point of
   indirection (a stable string Id) that every UI, present or future,
   can bind to on its own terms.

## Related Documents

`Command Framework Architecture.md` (the complete design); `ADR-0022`
(Navigation/Command orthogonality); `ADR-0036`–`ADR-0038` (this Work
Package's own decisions); `Navigation Architecture` (the closest
structural precedent); `Failure Isolation Across TempestOS` (Case 5);
`docs/academy/04 Design Patterns/01-the-registry-pattern.md`;
`docs/academy/03 Work Packages/WP5.1A-command-framework-architecture.md`;
`docs/academy/03 Work Packages/WP5.1B-command-framework-implementation.md`.
