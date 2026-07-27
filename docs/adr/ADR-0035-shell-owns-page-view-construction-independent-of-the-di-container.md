# ADR-0035: The Shell Owns Page/View Construction, Independent of the Platform's DI Container

## Status

Accepted — `v0.5.0` "Developer Experience" release, `WP 5.0C` (Shell &
Composition Framework Architecture), 2026-07-27. Depends on `ADR-0031`
(Navigation's model is UI-agnostic; rendering is `Tempest.App`'s own
responsibility) already being decided; this ADR answers the question that
decision explicitly left to the Shell: how, concretely, does a
`NavigationItem.Id` become something rendered on screen?

## Context

`ADR-0031` already forbids any rendering type from ever appearing in
`Tempest.Core.Navigation`. It states that `Tempest.App` "maintains its
own, private mapping from `NavigationItem.Id` to whatever it actually
knows how to draw," without designing that mapping. Two genuinely
different shapes were available: (1) the Shell owns a closed, hand-
registered mapping it alone populates, covering exactly its own built-in
pages; or (2) page types are registered into the same DI container
module types already are, letting a module — potentially a plugin-loaded
one — contribute its own page alongside the `NavigationItem` it registers,
symmetrically with how it already contributes navigation itself.

Option (2) is the more extensible-sounding answer, but it runs directly
into `ADR-0023`'s own downward-only layering: a module contributing a
*view* would need to depend on some contract describing what a view is —
and that contract cannot live in `Tempest.Core` without reintroducing
exactly the rendering-type leak `ADR-0031` forbids, nor can it live in
`Tempest.App`, since a module depending on `Tempest.App` would be a
Module depending *upward* on the Shell's own layer, inverting the
platform's four-layer model outright.

## Decision

**The Shell owns a closed, hand-registered mapping from `NavigationItem.Id`
to a rendering action, covering exactly the built-in pages `Tempest.App`
itself ships with. An item with no matching registration renders a
generic, honest placeholder rather than failing.** Dependency injection
participates at exactly one boundary — the Shell's own, one-time
resolution of `INavigationProvider`/`IEventBus` via `ITempestHost.Services`
(`ADR-0034`) — and not inside page construction itself, which is ordinary
object construction using whatever a specific page's own rendering
closure needs, passed directly.

**Module- or plugin-contributed page rendering is explicitly deferred,
not solved, by this decision** — see `RD-0036` for the structural reason
(no downward-compatible contract for a module to depend on) and its
revisit trigger.

## Consequences

**Positive:**

- `ADR-0031`'s own boundary is honoured concretely, not just in principle:
  no rendering-shaped contract is introduced anywhere a module could
  reach, in either `Tempest.Core` or via an upward dependency on
  `Tempest.App`.
- The Shell's own page wiring stays simple — a hand-written mapping over
  a small, known set of built-in pages — appropriate to a `v0.5` console
  shell with no plugin ecosystem yet exercising it.
- No new capability is added to `ITempestHostBuilder`/`TempestHost`'s own
  registration surface; the platform's DI container's job (constructing
  modules and holding DI-public services) is unchanged.

**Negative:**

- A plugin that registers a `NavigationItem` today has no way to make its
  own page appear in the Content Region — it will render the Shell's
  generic placeholder until a future Work Package solves this
  deliberately. This is a disclosed, real limitation, not an oversight.
- The Shell's own mapping must be updated by hand whenever a new built-in
  page is added — an accepted, small maintenance cost for the
  simplicity gained, proportionate to how few built-in pages `v0.5`
  actually needs.

## Future Considerations

If a real plugin or module genuinely needs to contribute its own
rendering, the correct next step is a dedicated architectural decision —
most plausibly, a new, narrow, `Tempest.App`-side contract that a
plugin's *own assembly* can implement without depending on `Tempest.App`
at compile time (for example, resolved by convention or a manifest-level
declaration, mirroring how Plugin Manifest already separates "what a
plugin declares" from "what the runtime decides") — not a retrofit of
`Tempest.Core.Navigation` itself, which `ADR-0031` keeps permanently
rendering-free. See `RD-0036`'s own revisit trigger.
