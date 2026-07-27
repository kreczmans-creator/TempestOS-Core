# ADR-0031: Navigation Contracts Belong in Tempest.Core; Rendering Remains an Application Responsibility

## Status

Accepted — `v0.5.0` "Developer Experience" release, `WP 5.0A` (Navigation
Framework Architecture), 2026-07-27. Resolves the one open question
`docs/releases/v0.4.0/Architecture.md` named for this release under its
prior name, `WP 4.6A`: *does Navigation belong in `Tempest.Core` at all,
given everything built so far is UI-agnostic and `Tempest.App` is a
console loop?*

## Context

Every platform capability built through `v0.4.0` — Configuration,
Logging, Discovery, Registration, Dependency Injection, Lifecycle, the
Event Bus, Background Services, Plugin Manifest — is provably
UI-agnostic: none of it renders anything, none of it references a UI
framework, and `FOUNDATION.md` states plainly that the Runtime Host must
remain UI-agnostic. Navigation is, on its face, a different kind of
thing: its entire purpose is to help a user move between screens. The
naive reading is that "navigation" is inherently a UI concept and
therefore belongs in `Tempest.App`, not `Tempest.Core`.

That reading conflates two genuinely separate questions: *what pages
exist, how are they organised, and how does something request moving to
one* (a model question) versus *how does a chosen page actually get
drawn on screen* (a rendering question). Every existing Platform API
this project has already built — `ICommand`, `IEvent`,
`IEventHandler<T>` — is itself proof that "represents a concept a human
eventually interacts with" and "is UI-agnostic" are not in tension: a
command represents a user's requested action without knowing what button
triggered it; an event represents something that happened without
knowing what, if anything, redraws in response.

## Decision

**The Navigation *model* — `NavigationItem`, `INavigationProvider`/
`NavigationService`, and the notification that a navigation was
requested — lives in `Tempest.Core`, in a new `Tempest.Core.Navigation`
namespace, following `ADR-0024`'s established capability-packaging
pattern exactly.** Rendering — resolving a `NavigationItem.Id` to an
actual screen, view, or console menu case, and performing that swap —
is, and remains, entirely `Tempest.App`'s (or any future UI shell's)
responsibility, with zero rendering type, delegate, or UI framework
reference anywhere in `Tempest.Core.Navigation`.

Concretely, `Tempest.Core.Navigation` may contain:

- Data describing a navigable destination (identity, title, an optional
  *symbolic* icon key, ordering, grouping, hierarchy, an optional
  visibility predicate).
- A registry of that data, imperatively populated.
- A notification, published through the existing `IEventBus`, that a
  navigation to a specific, already-registered destination was
  requested.

`Tempest.Core.Navigation` may **never** contain:

- A `View`, `Page`, `Component`, `Control`, or any type from, or
  modelled after, a specific UI framework.
- A delegate, callback, or function reference intended to *render*
  anything.
- Any notion of "what is currently displayed" — that is rendering state,
  owned by whatever is doing the rendering.

## Consequences

**Positive:**

- The one open architectural question this release carried into
  implementation now has a decided, written answer, exactly as `WP 4.5`'s
  own precedent (resolving its equivalent open questions before
  implementation) already established as this project's practice.
- Navigation slots into the existing four-layer platform model
  (`ADR-0023`) as an ordinary Platform API/Service pair, with no new
  layer, no exception carved out for it, and no special-casing anywhere
  in `TempestHost`.
- A future, entirely different `Tempest.App` replacement (a GUI shell, a
  web front end) could resolve the exact same `Tempest.Core.Navigation`
  registry and render it completely differently, without
  `Tempest.Core.Navigation` itself needing to change — proving the
  boundary is real, not just declared.

**Negative:**

- `Tempest.App` (or whatever renders) now carries the responsibility of
  maintaining its own, private mapping from `NavigationItem.Id` to
  whatever it actually knows how to draw — `Tempest.Core.Navigation`
  cannot and will not do this for it. This mapping is invisible to, and
  entirely outside the authority of, the platform.
- A future contributor accustomed to frameworks where "navigation" and
  "the router that also renders" are the same concept may find this
  split counter-intuitive at first — the Academy article accompanying
  this ADR exists specifically to make the boundary, and the reasoning
  behind it, discoverable before that confusion causes a rendering
  reference to leak into `Tempest.Core`.

## Future Considerations

If a genuinely compelling reason ever emerges to blur this boundary (for
example, a `Tempest.Core`-level notion of "the currently active item," if
several independent UI shells all need to agree on it simultaneously),
that is itself a new architectural decision requiring its own ADR — not a
precedent this ADR's own boundary should be read as inviting.
