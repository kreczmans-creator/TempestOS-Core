# ADR-0022: Navigation and Commands Are Orthogonal Platform Services

## Status

Accepted — v0.4.0 release planning (WP 4.0 / WP 4.6A / WP 4.7), 2026-07-23.
Decided before implementation begins, resolving Risk R10 in
`docs/releases/v0.4.0/Risks.md`.

## Context

The release's original planning pass assumed Navigation depends on Command
Framework — navigation actions expressed as commands. A later revision
reordered the release so that Navigation (`WP 4.6A`/`4.6B`) now precedes
Command Framework (`WP 4.7`), inverting that assumption without resolving
it. Two questions were on the table: should navigation invoke commands, or
should commands invoke navigation?

Both directions create the same problem: whichever service is asked to
"know about" the other becomes coupled to a capability it does not itself
own, and the dependency direction between two peer platform services
becomes arbitrary — decided by implementation order rather than by what
each service actually is.

## Decision

Neither. **Navigation and Command Framework are orthogonal, independent
platform services. Neither depends on the other.** Intent is separated from
execution:

```
User Action
     │
     ▼
  Command
     │
     ▼
Application Logic
     │
     ├─────────────┐
     ▼             ▼
 EventBus     NavigationService
```

A command represents a requested action and executes application logic. It
does not know navigation exists. Application logic may, as one of its
possible outcomes, publish an event (via `IEventBus`, ADR-0020) that a
`NavigationService` optionally reacts to — or a command's application logic
may call `NavigationService.Navigate(...)` directly, as an explicit,
ordinary dependency, exactly as it would call any other platform service it
needs. Either way, **the coupling is application logic depending downward
on both `IEventBus` and `INavigationProvider`/`NavigationService` as
peers — never one of those two services depending on the other.**

Two illustrative shapes, both legitimate under this decision:

```
SaveProjectCommand → ProjectService.Save() → ProjectSavedEvent → NavigationService (optionally reacts)
```

```
OpenModuleCommand → NavigationService.Navigate(...)
```

In neither case does `NavigationService` reference `ICommand`, and in
neither case does the command dispatcher reference `INavigationProvider`.

## Consequences

**Positive:**

- **Resolves Risk R10 outright.** `WP 4.6A` no longer depends on `WP 4.7` —
  it only needs to define its own routing model and service interface. The
  release's revised order (Navigation before Command Framework) is now
  fully coherent, not merely tolerated.
- No circular or arbitrary dependency between two peer services whose
  relative implementation order was, until now, deciding their coupling
  direction by accident.
- Consistent with, and a direct application of, ADR-0020's own reasoning:
  a service is consumed by application logic through its own contract; it
  does not reach sideways into a peer service's contract to get work done.

**Negative:**

- Application logic (whatever calls a command's handler) now carries the
  responsibility of wiring intent to execution — deciding whether a given
  command's outcome should publish an event, call navigation directly, or
  both. This is a small, deliberate shift of responsibility onto command
  handlers themselves, not onto either infrastructure service.
- A future contributor unfamiliar with this decision could still reach for
  the more obvious-looking direct coupling ("just have the command call
  navigation, or have navigation invoke the command"). This ADR, and the
  Engineering Glossary's cross-reference between the two services, exist
  specifically to make the orthogonal relationship discoverable before that
  happens.

## Future Considerations

If a genuine need later arises for tighter navigation/command integration
(for example, a declarative "this command navigates to X on success"
convenience), that convenience should be built as a thin layer *on top of*
this orthogonal relationship — application logic still wiring the two
together explicitly under the hood — not as a reason to introduce a direct
dependency between `NavigationService` and the command dispatcher.
