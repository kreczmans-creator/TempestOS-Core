# ADR-0003: Module Constructors Must Be Side-Effect-Free

## Status

Accepted — WP 2.1 (Module Discovery), reaffirmed WP 2.3 and WP 2.4.

## Context

`ReflectionFrameworkDiscoveryService` (WP 2.1) discovers `IModule` implementations
by scanning loaded assemblies, and — to read each candidate's `Id`, `Name`, and
`Version` — instantiates every valid-shaped type via `Activator.CreateInstance`,
reads the three properties, and then discards the instance. This happens for every
module type, every time discovery runs, regardless of whether that module is ever
actually used.

This convention was never written down as a rule anywhere in `IModule`'s contract.
It was implicit in how discovery happened to be implemented. By WP 2.3 and WP 2.4,
other components started relying on it being true without it ever having been
decided on purpose.

## Decision

TempestOS module constructors must be cheap and free of observable side effects.
No file handles opened, no network connections established, no background threads
started, no meaningful work done, in a module's constructor. Real resource
acquisition belongs in `IModuleLifecycle.InitialiseAsync`.

This was never enforced by a compiler check or a runtime guard — it is a
convention, documented in the Academy and in ADR form specifically so it is
discoverable and cannot be silently forgotten. It is, however, load-bearing in two
separate downstream places:

1. **Discovery (WP 2.1)** instantiates every candidate module transiently, purely
   to read metadata, then discards the instance. If constructors did real work,
   every discovery pass would trigger that work for every discoverable module,
   whether or not it is ever registered or run.
2. **Lifecycle (WP 2.3)** relies on a `Registered`-but-never-`Initialised` module
   having no instance at all yet, and therefore holding no resources — this is the
   entire justification for ADR-0004 (Dispose is legal from `Registered`). If
   construction acquired resources, disposing a never-initialised module would
   leak them, and ADR-0004's reasoning would not hold.

## Consequences

**Positive:**

- Discovery can freely, cheaply probe every `IModule`-shaped type in every loaded
  assembly without any risk of side effects piling up.
- ADR-0004 (permissive Dispose) is sound rather than merely convenient — see
  that ADR and the accompanying case study for the full chain of reasoning.
- Module authors get a single, memorable rule: "constructors just wire up
  fields; real work happens in `InitialiseAsync`."

**Negative:**

- The rule is unenforced. Nothing stops a module author from writing an expensive
  or side-effecting constructor, and nothing in the type system flags it. A future
  work package could add an analyzer or a runtime check (for example, timing
  construction and warning past a threshold), but none exists today.
- The rule is easy to forget precisely because it produces no immediate, visible
  failure when violated — a module with a slow constructor will simply make
  discovery slow, gradually, without an obvious single point of failure to trace
  it back to.

## Future Considerations

If this becomes a recurring source of bugs, consider:

- A Roslyn analyzer that flags non-trivial logic in constructors of types
  implementing `IModule`.
- A discovery-time timing check that logs a warning if any single module's
  transient construction (for metadata purposes) exceeds a threshold.

Neither is implemented; both are deliberately deferred rather than built
speculatively ahead of an actual, observed problem.
