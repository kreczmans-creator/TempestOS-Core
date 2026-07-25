# Reflection-Based Discovery

## 1. Introduction

`ReflectionFrameworkDiscoveryService` (WP 2.1) is TempestOS's answer to a
problem every extensible, plugin-friendly platform eventually faces: how do
you find "every implementation of X" without maintaining a hand-written
list of them anywhere? This document describes the general technique —
reflection-based discovery — and the specific discipline TempestOS applies
to keep it safe, deterministic, and testable, a discipline reused a second
time, almost unchanged, when Plugin Discovery (WP 4.2) needed to find
manifests on disk rather than types in memory, and a third time, with one
genuine new wrinkle, when Hosted Service Discovery (WP 4.5) needed to find
`IHostedService` implementations that carry no metadata at all.

## 2. Purpose

To explain reflection-based discovery as a reusable technique — not
specific to `IModule` — and to name the four disciplines that make it safe
to use in production code, each of which TempestOS applies consistently.

## 3. Background

.NET's reflection APIs (`Assembly.GetTypes()`, `Type.IsAssignableFrom`, and
so on) let code inspect and instantiate types it has never seen at compile
time. This is exactly the capability an extensible system needs — new
modules should be addable by *implementing an interface*, never by editing
the code that finds modules (the Open/Closed Principle, concretely). It is
also, used carelessly, a reliable source of flaky tests, cryptic exceptions,
and non-deterministic behaviour, because reflection APIs make almost no
guarantees about ordering, and because "instantiate everything that matches"
will happily instantiate things a caller never intended to be treated as
real candidates (an abstract base class, an open generic definition, the
interface itself).

## 4. The Problem

1. **What should be excluded**, given that scanning "everything implementing
   an interface" naturally surfaces the interface itself, abstract classes
   partially implementing it, and open generic type definitions that cannot
   be instantiated at all?
2. **What order should results come back in**, given reflection APIs impose
   none?
3. **How is this tested**, given that "scan whatever happens to be loaded
   into the process" depends on ambient, hard-to-control state?
4. **What happens when a candidate type fails to load at all** — a real
   condition (`ReflectionTypeLoadException`) reflection APIs can produce
   for reasons having nothing to do with the type you actually care about?

## 5. The Design

**Four disciplines, applied consistently everywhere TempestOS uses this
pattern:**

1. **Filter before instantiating.** Check `IsInterface`, `IsAbstract`,
   `IsGenericTypeDefinition`, and `IsAssignableFrom` — in that order, cheap
   checks first — *before* attempting to construct anything. This keeps a
   scan from ever trying to instantiate something that structurally cannot
   be a real candidate. Hosted Service Discovery (WP 4.5) takes this
   discipline one step further: because `IHostedService` carries no
   `Id`/`Name`/`Version` to read, there is nothing discovery would ever
   need a live instance *for* — so it never instantiates a candidate at
   all, filtering purely on type shape and moving straight to the result
   list. This is not a special case of the discipline; it is the same
   discipline taken to its logical conclusion once metadata itself turns
   out to be unnecessary.
2. **Impose a deterministic order explicitly.** Reflection's own
   enumeration order is an implementation detail, not a guarantee — TempestOS
   always sorts its own output (ascending, ordinal, by a stable key) rather
   than trusting whatever order the runtime happens to produce, so the same
   scan produces the same result every time, on every machine.
3. **Isolate load failures per candidate, not per scan.**
   `ReflectionTypeLoadException` is a real-world condition where *some*
   types in an assembly fail to load — handling it explicitly means one
   broken type does not crash discovery of every other, healthy type in the
   same assembly.
4. **Expose an `internal`, explicit-input seam for testing.** A public
   contract that says "scans loaded assemblies" is inherently difficult to
   test deterministically — the fix is not to weaken the public contract,
   but to expose a second, `internal` overload that operates over an
   explicit, caller-supplied list, visible to the test assembly via
   `InternalsVisibleTo`. The real algorithm (filter, validate, deduplicate,
   sort) is then fully testable, independent of ambient process state.

## 6. Alternatives Considered

**Attribute-based discovery instead of interface-based.** A viable, real
alternative (`[Module("id", "name", "1.0")]` read via
`Type.GetCustomAttribute`) — TempestOS uses exactly this shape, additively,
for a narrower purpose (`ModuleMetadataAttribute`, ADR-0027) once a genuine
need (constructor injection into a discovered module) justified it. The
two are not mutually exclusive: interface-based discovery gives compile-time
enforcement an attribute cannot; attribute-based reading avoids
instantiating a candidate at all, which matters specifically when
instantiation itself is the thing you need to avoid.

**Testing via a real, whole-assembly scan of the shared test assembly.**
Tried conceptually and rejected the moment real test fixtures existed: a
test assembly needs deliberately-broken fixtures (duplicate IDs, invalid
metadata, excluded shapes) for *other* tests to exercise, and a
whole-assembly "happy path" scan would immediately trip over them. The
`internal`, explicit-type-list seam exists specifically to avoid this
collision.

## 7. Why This Solution Was Chosen

Each discipline above answers a specific, real failure mode reflection-based
discovery would otherwise have: filtering-before-instantiating avoids
wasted or unsafe construction attempts; explicit ordering avoids
non-deterministic tests and non-reproducible startup behaviour;
per-candidate isolation avoids one bad type taking down an entire scan;
the internal seam avoids tests that interfere with each other by
construction. None of the four is optional — removing any one reintroduces
exactly the failure mode it exists to prevent.

## 8. Architectural Principles

- **Open/Closed Principle** — new candidates are found automatically by
  existing; discovery's own code never changes to accommodate a new one.
- **Deterministic Systems** — explicit sorting converts an unordered
  reflection API into a reproducible one.
- **Defensive Programming** — `ReflectionTypeLoadException` is handled
  explicitly, not left to crash an entire scan over one bad type.
- **Testing Strategy** (Engineering Standard) — the internal-seam pattern
  this document describes is the specific, named origin of that broader
  standard.

## 9. Benefits

- A new module (or plugin, or any future reflection-discovered candidate)
  requires zero changes to the discovery code that finds it — it is found
  automatically, the next time discovery runs, simply by existing and
  implementing the right contract.
- The same four disciplines, applied a second time for Plugin Discovery
  (scanning folders for manifest files rather than types for interfaces),
  required no new pattern to be invented — only the same one, reused.
- Applied a third time for Hosted Service Discovery (WP 4.5), the pattern
  needed no new discipline either — only a simpler application of the
  first one, since a hosted service's complete absence of metadata means
  the `ModuleMetadataAttribute`-style prerequisite modules once needed
  (ADR-0027, to avoid instantiating a module carrying constructor
  dependencies) never arises at all.
- Deterministic ordering, established here first, turned out to matter for
  more than discovery's own output — `ModuleLifecycleManager` (WP 2.3)
  needed its own ordering guarantee built directly on top of discovery's.

## 10. Trade-offs

- Every discoverable type in a scanned assembly is transiently instantiated
  on every discovery pass (for interface-based discovery specifically) —
  this depends entirely on the convention that constructors are cheap and
  side-effect-free (ADR-0003), enforced by documentation and discipline,
  not by the compiler.
- `AppDomain.CurrentDomain.GetAssemblies()` only sees assemblies already
  loaded into the process — discovery alone cannot find modules sitting
  unloaded on disk; a separate loading step (Plugin Loading, WP 4.2) is
  needed to make them visible first.

## 11. Common Mistakes

The mistake most worth naming: assuming `Activator.CreateInstance`'s
exceptions are self-explanatory. A type with no public parameterless
constructor throws a generic, unhelpful exception from deep inside
reflection machinery if this isn't guarded against explicitly — worth
remembering whenever discovery's filtering logic is extended to a new kind
of candidate.

A second, related mistake: testing "the real discovery mechanism works"
using only a synthetic or mocked stand-in for what's being discovered. WP
4.2's own implementation deliberately built a genuinely valid, loadable
compiled assembly at test time (`DynamicPluginAssemblyBuilder`), rather
than a test double standing in for "an assembly," specifically so its
proof that Module Discovery sees a plugin-loaded type exercises the real
mechanism, not a simulation of it.

## 12. Future Evolution

- **A dedicated ordering/priority property**, if alphabetical-by-`Id`
  ordering ever proves insufficient for a genuine startup-ordering need —
  changing only the sort key this pattern is built from, not its filtering
  or validation logic.
- **Attribute-based discovery for further candidate kinds**, following
  `ModuleMetadataAttribute`'s own precedent, if a future capability needs to
  avoid instantiation the way modules needed to avoid it for constructor
  injection.

## 13. Key Takeaways

1. Reflection-based discovery is safe in production specifically because of
   four disciplines applied consistently — filter before instantiating,
   impose explicit ordering, isolate per-candidate load failures, and expose
   an internal seam for deterministic testing — not because reflection
   itself is inherently safe.
2. The same pattern, once proven correct in one place (Module Discovery),
   is worth reusing directly the next time a structurally similar problem
   appears (Plugin Discovery, Hosted Service Discovery) — rather than
   re-deriving a new discovery mechanism from scratch. A third reuse that
   requires *simplifying* the pattern rather than extending it (Hosted
   Service Discovery never needing to instantiate anything) is still
   evidence the original four disciplines were well-chosen, not a sign
   the pattern needed to grow new cases to keep working.
3. Testing "discovery finds the real thing" is only as convincing as what
   the test actually discovers — a genuinely loadable, dynamically-built
   assembly proves the claim; a synthetic stand-in does not.
