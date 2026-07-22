# ADR-0011: Discovery and Registration Precede Dependency Injection Container Construction

## Status

Accepted — WP 2.7 (Runtime Host Architecture), 2026-07-22. Architecture only;
no code changes accompany this decision.

## Context

WP 2.7's brief suggested an illustrative Host phase order that lists
"Dependency Injection Built" before "Platform Services Registered," which in
turn precedes "Module Discovery" and "Module Registration." Read literally,
this would mean the Host builds a fully resolvable `ITempestServiceProvider`
*before* Discovery or Registration ever run.

This does not match the actual dependency graph of the services already
built. Two facts settle the question:

1. **`IFrameworkDiscoveryService` has no dependency on the DI container at
   all — deliberately, per ADR-0008.** `ReflectionFrameworkDiscoveryService`
   takes only assemblies and an optional `ILogger`; it does not need, and
   architecturally must not need, a resolvable container to do its job.
2. **The DI container has no mechanism to add registrations after
   construction.** `TempestServiceProvider` copies `IServiceCollection.Descriptors`
   into its own internal state once, at construction (WP 2.4). Registering a
   discovered module's concrete type (`AddDiscoveredModules`) must happen
   *before* the container is built, not after — there is no supported way to
   register something into an already-built provider.

Taken together, building the container before Discovery/Registration would
require either giving Discovery a container dependency it was explicitly
designed never to need (a direct violation of ADR-0008), or building the
container once, then rebuilding it a second time after modules are known —
neither of which is coherent with the platform's existing design.

## Decision

The Host's real phase order is: Configuration Built → Logging Built → Module
Discovery → Module Registration → Platform Services Registered (configuration,
logging, and every discovered module's concrete type all added to a
`ServiceCollection`) → Dependency Injection Built (`TempestServiceProvider`
constructed once, from that now-fully-populated collection) → Module
Initialisation.

This reorders two of the brief's illustrative phase names relative to each
other ("Dependency Injection Built" now follows "Module Discovery" and "Module
Registration," not the reverse) without abandoning any of the phases
themselves — every named phase still exists, in a sequence consistent with
how the underlying services actually depend on one another.

## Consequences

**Positive:**

- Discovery remains exactly as independent of DI as ADR-0008 established —
  the Host's own sequencing doesn't quietly reintroduce a dependency Discovery
  was deliberately designed without.
- The container is built exactly once per Host run, fully populated from the
  start — no "build, discover more, rebuild" cycle, which would have been a
  new capability the DI container does not have and was never asked to have.
- This ordering is the direct, mechanical consequence of facts already
  established by ADR-0008 (WP 2.1) and the container's own design (WP 2.4) —
  it required no new capability anywhere, only recognising how the existing
  pieces actually fit together.

**Negative:**

- A reader who takes the WP 2.7 brief's illustrative phase list at face value,
  without cross-referencing ADR-0008, could reasonably expect the opposite
  order. This ADR — and the Host Lifecycle document's own phase table — exist
  specifically to make the actual, correct order unambiguous and citable.

## Future Considerations

If a future work package ever needs Discovery to depend on a DI-resolved
service (breaking ADR-0008's independence deliberately, for a reason strong
enough to justify it), this ordering would need to be revisited from first
principles, not patched around — the entire reason Discovery can run before
the container exists is that it currently has no service dependency that
requires one.
