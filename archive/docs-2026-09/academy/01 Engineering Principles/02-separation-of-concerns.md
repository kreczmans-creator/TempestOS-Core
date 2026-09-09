# Separation of Concerns

## What

Separation of Concerns (SoC) is the principle of dividing a system into distinct
sections, each addressing one specific concern, with minimal overlap between them.
A "concern" is any piece of information or behaviour that could plausibly change
for its own, independent reason: how data is stored, how it's validated, how it's
displayed, how errors are logged, how a resource's lifetime is managed.

SoC is broader and older than SRP (which is really SoC applied specifically to
classes). SoC applies at every scale: functions, classes, modules, layers,
services, and whole subsystems.

## Why

When concerns are mixed together, a change to one forces you to touch, understand,
and re-test code belonging to a different concern that you had no reason to want
to change. Mixed concerns also make code harder to read, because a reader has to
hold multiple, unrelated mental models in their head simultaneously to understand
a single piece of code.

## Benefits

- Each concern can be understood, tested, and changed in isolation.
- Concerns can be recombined differently without rewriting them — the same
  discovery logic can feed different registration strategies; the same lifecycle
  orchestration can drive modules constructed by different mechanisms.
- Bugs are easier to localise: if module *state tracking* is wrong, you look at
  the lifecycle manager, not the discovery service, because state tracking is not
  a concern discovery has any part in.

## Disadvantages

- Over-separating concerns that are actually tightly coupled in practice can
  produce more indirection and more files to navigate than the problem warrants,
  without a corresponding gain in flexibility, since the "separate" pieces still
  have to change together anyway.
- Drawing the boundary in the wrong place is worse than not separating at all: a
  boundary that splits one cohesive concern into two artificially separate pieces
  creates coordination overhead (two places to update, kept in sync by convention
  rather than the compiler) without actually decoupling anything.

## When to Use

When two pieces of logic genuinely change for different reasons, on different
timelines, potentially owned by different people or teams, or when one is likely
to need multiple different implementations while the other stays fixed.

## When Not to Use

When two pieces of logic are so tightly coupled that they always change together
— forcing a boundary between them just adds ceremony. Not every function needs to
be split into "the part that does X" and "the part that does Y" if X and Y have no
independent existence.

## How TempestOS Applies It

The clearest expression of SoC in TempestOS is the explicit, four-stage pipeline
introduced across WP 2.1–2.4, and the fixed responsibility list WP 2.4's brief
stated directly:

> Discovery Service — discovers modules. Runtime Module Manager — registers
> modules. Lifecycle Manager — orchestrates modules. Service Provider — creates
> module instances.

Each of these concerns changes for a genuinely different reason:

- Discovery changes if *how modules are found* changes (a new assembly-loading
  strategy, a plugin-directory scanner, an attribute-based filter instead of a
  type-based one).
- Registration changes if *how the runtime catalogues known modules* changes (a
  persistent registry instead of in-memory, a different duplicate-handling
  policy).
- Lifecycle changes if *how modules are started, stopped, and torn down* changes
  (a different ordering strategy — see the ordering note in `ModuleLifecycleManager`'s
  own documentation — or new lifecycle states).
- The Service Provider changes if *how instances are constructed and wired
  together* changes (a different container, different lifetime rules).

Each concern lives in its own class, was delivered in its own work package, and —
critically — depends on the others only through narrow interfaces
(`IRuntimeModuleManager`, `ITempestServiceProvider`), never on each other's
concrete implementation details. `ModuleLifecycleManager` does not know or care
*how* `TempestServiceProvider` constructs an instance, only that
`ITempestServiceProvider.GetService(Type)` will hand one back or throw a
descriptive exception trying.

## Key Takeaway

Separation of Concerns is only valuable when the concerns you're separating are
actually independent — the discipline is in correctly identifying *where* the real
seams are (as WP 2.4's fixed responsibility list did explicitly, in writing,
before any code was changed), not in mechanically splitting everything into more
pieces.
