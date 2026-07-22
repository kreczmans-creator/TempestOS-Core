# ADR-0009: The Composition Root Owns Externally-Created Services

## Status

Accepted — WP 2.5 (Configuration Framework), 2026-07-22. Reframed from its
original title, "DI container supports instance registration," following
review: the original framing named the *implementation* (`AddInstance`)
rather than the *architectural principle* it exists to serve. No behavioural
change accompanies this reframing — `AddInstance`, `ExistingInstance`, and the
`TempestServiceProvider` constructor change described below are unchanged from
when this ADR was first written; only the understanding of *why* they exist,
and what else might someday satisfy the same need, has been made explicit.

## Context

Some services cannot be constructed by the dependency injection container,
under any circumstance, no matter how sophisticated the container becomes —
not because of a limitation to fix, but because of what they fundamentally
are: **some services must exist before dependency injection begins.**

`IConfigurationProvider` is the first, but will not be the last. Its concrete
implementation, `ConfigurationProvider`, requires an already-merged dictionary
of values that only `ConfigurationBuilder.Build()` can produce — by loading and
validating whatever `IConfigurationSource` instances the composition root
supplied, at startup, *before* anything resembling a dependency graph exists to
resolve. There is no reflection-based construction path the container could
follow to arrive at the same object, because the object's construction depends
on information (which sources, in which order, containing what) that is a
property of the composition root's own startup sequence — not a property
derivable from a type's constructor parameters.

As of WP 2.4, `IServiceCollection`/`TempestServiceProvider` supported exactly
one registration shape: map a service type to an implementation type the
container itself would construct via reflection, recursively resolving that
implementation's own constructor dependencies. This shape has no way to express
"this object already exists, was built by something outside the container's
own resolution graph, and must simply be handed out."

## Decision

The composition root — whatever code assembles a running TempestOS instance,
today exercised directly by test setup, eventually a dedicated startup
sequence (see the companion Runtime Architecture document, *The Startup
Sequence*) — is recognised as owning a category of service the container will
never construct itself. Today, exactly one mechanism exists to hand such a
service to the container: `IServiceCollection.AddInstance(Type, object)` (with
a generic `AddInstance<TService>` convenience overload), backed by a new,
optional `ServiceDescriptor.ExistingInstance` property and a small addition to
`TempestServiceProvider`'s constructor, which pre-seeds its singleton cache
from any descriptor carrying one — `Resolve`/`Construct` themselves are
unchanged.

`AddInstance` is this ADR's *current* implementation, not its *complete*
statement. The principle being satisfied — some services are built outside the
container's own construction graph and simply need to be made resolvable
through it — could equally be satisfied, in future work, by:

- **Factory registrations** — a registered delegate the container invokes to
  produce a service, rather than an already-existing instance handed to it
  directly (useful when the value needs to be built lazily, or needs access to
  other already-resolved services at the moment of its own construction).
- **Singleton factories** — a variant of the above, specifically for
  singleton-lifetime services whose construction is deferred until first
  requested, rather than eagerly built at the composition root before the
  container exists at all.
- **Bootstrapped services** — services that require an explicit,
  ordered startup step (opening a connection, validating a licence, performing
  a handshake) before they are fit to hand to any consumer, where "construct
  it" and "it is ready to use" are genuinely different moments.

None of these are implemented today. All of them would be additional
expressions of the same underlying principle this ADR names, not competing
alternatives to `AddInstance` — a future work package introducing one should
extend this ADR's reasoning, not treat the question as newly open.

## Consequences

**Positive:**

- **The principle, not just the mechanism, is now named and citable.** A future
  engineer wondering "why can't the container just build configuration itself"
  has an answer that generalises, rather than one that only explains
  `AddInstance`'s existence in isolation.
- **Zero changes to the container's actual resolution logic.** Because
  `TempestServiceProvider.Resolve` already checked its singleton cache before
  attempting construction (a pre-existing WP 2.4 behaviour, there to support
  ordinary singleton registrations), pre-seeding that same cache with an
  existing instance was sufficient on its own.
- **One registry, not several.** A consumer resolving a service through
  `TempestServiceProvider` does not need to know or care whether it was
  registered via `Add` (reflection-constructed), `AddInstance` (pre-supplied),
  or — in the future — a factory registration; all would be visible through the
  same `GetService` call and the same `Descriptors` collection.
- **Backward compatible.** `ExistingInstance`'s optional, defaulted parameter
  means every existing call to `new ServiceDescriptor(serviceType,
  implementationType, lifetime)` across WP 2.1–2.4's code and tests continues to
  compile and behave identically, unchanged.

**Negative:**

- **A second registration concept to learn today**, likely a third and fourth
  in future — `Add`/`Singleton`/`Transient` versus `AddInstance`, versus
  whatever a future factory-registration mechanism introduces. Each addition
  should be justified by this ADR's principle, not added as an ad hoc
  convenience, or the container's registration surface risks growing without a
  unifying rationale a reader can hold onto.
- **`AddInstance` is always, implicitly, a singleton.** There is no way to
  register a pre-built instance as anything else, since a "transient pre-built
  instance" is not a coherent concept — worth stating explicitly, since every
  other registration method takes an explicit `ServiceLifetime` argument and
  `AddInstance` conspicuously doesn't.
- **No disposal tracking.** If a pre-built instance implements
  `IDisposable`/`IAsyncDisposable`, nothing in `TempestServiceProvider` will
  ever call it — the same gap already noted for ordinary constructed singletons
  in the WP 2.4 retrospective, now also applying to `AddInstance`-registered
  values.

## Future Considerations

Before implementing factory registrations, singleton factories, or bootstrapped
services, revisit this ADR rather than starting from a blank page — the
question "does this new mechanism serve the same principle `AddInstance`
serves, or is it solving something genuinely different" should be answered
explicitly, in writing, each time. If `TempestServiceProvider` ever gains
disposal-tracking (see the WP 2.4 retrospective's Future Evolution section), it
should account for every externally-created service uniformly, regardless of
which specific mechanism registered it — there is no principled reason for an
`AddInstance`-registered value and a future factory-registered value to be
treated differently with respect to disposal.
