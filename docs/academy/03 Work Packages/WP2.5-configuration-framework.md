# WP 2.5 — Configuration Framework

## 1. Introduction

WP 2.5 introduced configuration as a first-class platform service: data the rest
of the runtime can read, sourced from one or more providers, merged
deterministically, validated at build time, and made available through the
dependency injection container WP 2.4 introduced. It is the first work package
whose *primary* deliverable is not part of the module pipeline itself
(Discovery → Registration → Lifecycle → Dependency Injection) — configuration is
a service the pipeline, and everything else in the runtime, can *depend on*,
rather than another stage of the pipeline itself.

It is also the first work package to require a small, deliberate, additive
extension to a previous work package's own public API (WP 2.4's DI container) —
not a redesign, but a genuine gap that only became visible once something
(configuration) needed a capability the container didn't yet have: registering
an already-built instance rather than a type to construct.

## 2. Purpose

To give every runtime service — modules included — one consistent way to read
named configuration values, without needing to know where those values actually
came from, and to make configuration a citizen of the dependency injection
container established in WP 2.4, so it can be requested via ordinary constructor
injection like any other service.

## 3. Background

Before WP 2.5, `Tempest.Core.Configuration` already contained
`ApplicationConfiguration` and `ConfigurationService` — a narrow, pre-existing
pair of types from the original platform bootstrap work, describing fixed
workspace paths (`WorkspaceRoot`, `ProjectsPath`, and similar). These predate the
module pipeline entirely and solve a different, narrower problem: where does
TempestOS keep its files on disk. WP 2.5 did not touch, extend, or redesign
either type — the Configuration Framework this work package introduces is a new,
general-purpose, key/value configuration system living alongside them in the
same namespace, not a replacement for them.

By the time WP 2.5 began, the DI container (WP 2.4) already supported
constructor injection, singleton/transient lifetimes, and descriptive resolution
failures — but every registration to date had been of a *type* the container
could construct itself via reflection. Nothing before WP 2.5 had ever needed to
register something the container could not construct on its own.

## 4. The Problem

1. **What does "read configuration" actually mean as a contract**, given the
   brief's explicit architectural principle that configuration is data, never
   business logic, and consumers only ever read it?
2. **How is a value looked up** — by exact match with a descriptive failure, by
   speculative check, or both — and how is "does this key exist at all"
   distinguished from "what value does it have"?
3. **Where do values actually come from**, and how can more sources (JSON,
   environment variables, command line, database — explicitly *not* built now)
   be added later without changing the provider or builder's own contracts?
4. **What happens when more than one source defines the same key** — and
   critically, is that the *same* problem as one source defining the same key
   twice, or a genuinely different situation requiring different handling?
5. **What do hierarchical keys** like `Runtime:Logging:MinimumLevel` actually
   require of this work package, given object binding is explicitly out of
   scope?
6. **How does a pre-built configuration value get into a dependency injection
   container that, as of WP 2.4, only knew how to construct types via
   reflection?**
7. **What validation must happen, and when** — at the moment a value is read, or
   once, up front, when configuration is assembled?

## 5. The Design

**`IConfigurationProvider`** — the read-only contract: `Get(key)` (throws
`ConfigurationKeyNotFoundException` if missing), `TryGetValue(key, out value)`
(non-throwing), `ContainsKey(key)`, `GetAll()`. No mutation method exists on this
interface, at all — this is a direct, literal expression of the brief's stated
principle that consumers read configuration and never modify it: there is
structurally nothing to call that would let them try.

**`ConfigurationProvider`** — the concrete, `internal`-constructed
implementation. Stores values as a `ReadOnlyDictionary<string, string>` built
from a *defensive copy* of whatever it's given, using
`StringComparer.OrdinalIgnoreCase` throughout, so keys are genuinely
case-insensitive and the provider's internal state can never be affected by
anything that happens to the collection it was built from afterward.

**`IConfigurationSource`** — a single method, `Load()`, returning
`IEnumerable<KeyValuePair<string, string>>` rather than a dictionary,
specifically so a source *can* produce the same key more than once — a
dictionary-shaped return type would make that structurally impossible to
represent, and "detect duplicate keys within the same source" (an explicit
requirement) needs duplicates to be a representable, catchable condition, not
something already silently prevented by the return type.

**`MemoryConfigurationSource`** — the one source this work package implements,
wrapping a caller-supplied sequence of key/value pairs verbatim.

**`ConfigurationBuilder`** — `AddSource(source)` (logs registration, returns
`this` for chaining) and `Build()`. `Build()` iterates sources in the order they
were added, loading each one, validating every entry (null key, empty/whitespace
key, null value — each throwing `InvalidConfigurationEntryException`), tracking
keys seen *within the current source only* (resetting per source) to detect
same-source duplicates (`DuplicateConfigurationKeyException`), and merging into
a single case-insensitive dictionary where a later source's value for a key
silently and deliberately overwrites an earlier source's value for the same
key — the intended, documented distinction between a duplicate (an error) and
an override (the entire point of supporting multiple sources).

**Exception hierarchy**: `ConfigurationException` (base),
`InvalidConfigurationEntryException`, `DuplicateConfigurationKeyException`,
`ConfigurationKeyNotFoundException` — the fourth, separate exception hierarchy
this codebase has introduced, following exactly the same base-plus-focused-
subtypes shape as discovery, registration, and lifecycle before it.

**DI registration**: `IServiceCollection` gained a new method, `AddInstance(Type,
object)` (plus a generic `AddInstance<TService>` sugar overload), and
`ServiceDescriptor` gained a new, optional `ExistingInstance` property.
`TempestServiceProvider`'s constructor now pre-seeds its singleton cache with
any descriptor's `ExistingInstance`, so resolving it later returns exactly that
instance without ever attempting construction. See ADR-0009 for the full
reasoning behind this addition.

Diagnostics: `ConfigurationBuilder` accepts an optional `LoggingService?` and
logs source registration, per-source loading, duplicate-key detection (before
throwing), and build completion (key/source counts) — the four events the
brief's Diagnostics requirement named explicitly.

## 6. Alternatives Considered

**Treating duplicate keys the same way regardless of whether they came from the
same source or different sources.** Rejected immediately, and explicitly, by the
brief itself (requirement #4's "later sources override earlier sources" versus
requirement #7's "detect duplicate keys within the same source"). This
distinction was not a judgment call this work package had to make from
scratch — it was specified — but implementing it correctly required real care:
the "seen keys" tracking set in `ConfigurationBuilder.Build()` had to be reset
*per source*, not accumulated across the whole build, or a key legitimately
overridden by a second source would have been misreported as a same-source
duplicate.

**A dictionary-based `IConfigurationSource.Load()` return type.** Considered
briefly and rejected: `IReadOnlyDictionary<string, string>` would have made
same-source duplicates structurally unrepresentable — a dictionary literally
cannot contain the same key twice — which would have made the explicit
"duplicate keys within the same source" validation requirement untestable and,
worse, silently impossible to violate rather than validated against.

**A `GetSection`/hierarchical-object API for colon-delimited keys.** Explicitly
rejected by the brief itself ("no object binding yet. String values only.") and
not attempted. Hierarchical keys are supported as an emergent property of plain,
flat string keys and case-insensitive dictionary storage — nothing additional
was built, and nothing additional was needed; a test confirms colon-delimited
keys behave exactly like any other string key.

**Making `IConfigurationProvider` constructible by the DI container via
reflection**, avoiding the need for any container extension at all. Rejected:
`ConfigurationProvider`'s constructor requires the already-merged dictionary
`ConfigurationBuilder.Build()` produces — there is no reflection-based
construction path that could plausibly assemble that dictionary from
registrations alone, since the whole point of the builder is to merge
*runtime-supplied* sources before a provider can exist at all.

**A separate, standalone instance registry, independent of `IServiceCollection`/
`ServiceDescriptor`.** Considered as an alternative to extending the DI
container itself — a small, separate class holding pre-built instances that
`ModuleLifecycleManager`-style consumers would need to check *in addition to*
the service provider. Rejected in favour of extending `ServiceDescriptor` and
`TempestServiceProvider`'s existing singleton-cache mechanism directly: it kept
resolution logic in exactly one place (`TempestServiceProvider.Resolve`,
unchanged) rather than requiring every future consumer to know about and check
two different registries. See ADR-0009 for the full reasoning.

## 7. Why This Solution Was Chosen

Every design choice in this work package traces back to the four architectural
principles the brief itself stated up front: configuration is data, not
business logic (hence no mutation methods, no behaviour beyond lookup);
configuration is immutable once the runtime has started (hence the defensive
copy into a `ReadOnlyDictionary`); configuration is loaded once (hence
`ConfigurationBuilder.Build()` being a one-shot operation producing an immutable
provider, not a live, re-buildable one); consumers read, never modify (hence
`IConfigurationProvider` exposing no write path at all, not even an internal
one a determined caller could reach through casting — there is genuinely
nothing to cast to).

## 8. Architectural Principles

- **Immutability** — the provider's storage, and the collections it hands out,
  are both immutable, following the exact same defensive-copy discipline
  established in WP 2.2 and WP 2.3.
- **Fail Fast** — every validation failure (null key, empty key, null value,
  duplicate key) is detected once, at `Build()` time, with a descriptive
  exception naming the offending source — not discovered later as a confusing
  `null` read somewhere downstream.
- **Separation of Concerns** — `IConfigurationSource` (where values come from)
  is fully decoupled from `IConfigurationProvider` (how values are read); adding
  a JSON or environment-variable source in a future work package requires
  implementing one new, small interface, touching nothing else.
- **Dependency Injection** — configuration is exposed to the rest of the runtime
  exclusively through the WP 2.4 container, via ordinary constructor injection,
  not through a static accessor or ambient singleton reached for directly.

## 9. Benefits

- Any future runtime service, including any future module, can declare a
  constructor dependency on `IConfigurationProvider` and receive fully-loaded,
  validated configuration automatically — proven directly by
  `ConfigurationDependencyInjectionTests`, which constructs a plain consumer
  class with exactly that dependency and resolves it through the container.
- Adding a new source in a future work package (JSON, say) requires
  implementing `IConfigurationSource.Load()` and nothing else — no change to
  `ConfigurationBuilder`, `ConfigurationProvider`, or any existing consumer.
- The instance-registration capability added to the DI container (ADR-0009) is
  immediately reusable by any future work package needing to register a
  runtime-supplied value the container cannot construct via reflection — not a
  one-off special case for configuration alone.

## 10. Trade-offs

- `ConfigurationBuilder.Build()` performs a full, eager merge of every source,
  once — there is no lazy or partial loading, and no way to add a source after
  `Build()` has been called without constructing an entirely new builder and
  provider. This is a deliberate consequence of "configuration is loaded once,"
  not an oversight, but it does mean a genuinely large number of sources or
  entries would all be validated and merged up front, synchronously.
- No environment variable, JSON, or command-line source exists yet — every
  configuration value in a running TempestOS instance today has to originate
  from code explicitly constructing a `MemoryConfigurationSource`. This is the
  brief's own explicit, deliberate scope boundary, not a gap discovered during
  implementation.
- The DI container's `AddInstance` capability, while minimal and additive, is a
  second registration path a reader now has to know about alongside `Add` — see
  ADR-0009's Consequences section for the full discussion of this cost.

## 11. Common Mistakes

The mistake most worth flagging from this work package is one that was
considered and avoided, not one that was made and fixed: conflating "duplicate
key" with "key overridden by a later source." These look, superficially, like
the same situation — a key with more than one candidate value — but they
require opposite handling: one is always an error, the other is the *entire
point* of supporting multiple sources. The implementation detail that makes the
distinction correct is easy to get wrong on a first attempt: the "keys seen so
far" tracking set inside `ConfigurationBuilder.Build()` must be a *new*,
per-source set, re-created at the top of each iteration of the outer
(per-source) loop — not a single set accumulated across the whole build. A set
accumulated across the whole build would misreport every legitimate override as
a duplicate-key error, since the key genuinely *would* have been "seen before"
by the time the second source's copy of it is processed.

A related trap for any future source implementation: `IConfigurationSource.Load()`
returning `IEnumerable<KeyValuePair<string, string>>` means a source *could*,
technically, stream values lazily rather than returning a fully materialised
list — but `MemoryConfigurationSource` deliberately materialises its entries
once, in its constructor (`entries.ToList()`), rather than storing and
re-yielding the caller's original `IEnumerable` on every `Load()` call. A future
source that re-evaluates a lazy sequence on every `Load()` call risks producing
*different* results on a second load than the first — directly at odds with
"configuration is loaded once."

## 12. Future Evolution

- **Additional sources** (JSON, environment variables, command line, database)
  are the most obvious and explicitly anticipated next step, each implementing
  `IConfigurationSource` independently — no change to `ConfigurationBuilder` or
  `ConfigurationProvider` should be required for any of them.
- **Object binding** (`Options<T>`-style typed configuration sections) is
  explicitly out of scope for WP 2.5 and was not attempted — a future work
  package introducing it should build it as a layer *on top of*
  `IConfigurationProvider.GetAll()`/`Get`, not by changing either's contract.
- **Reload-on-change / file watching** were explicitly excluded from this work
  package and are a genuinely significant departure from "configuration is
  immutable once the runtime has started" — any future work package
  introducing either would need to revisit that architectural principle
  directly, not quietly work around it.
- **The DI container's `AddInstance` capability** (ADR-0009) should be reused,
  not reinvented, by any future work package needing to register a
  runtime-supplied value — see that ADR's Future Considerations for the one
  known gap (disposal of registered instances is not tracked, mirroring the
  same gap already noted for constructed singletons in the WP 2.4
  retrospective).

## 13. Key Takeaways

1. Two situations that look identical on the surface (a key defined more than
   once) can require opposite handling depending on *where* the duplication
   occurs — the fix is almost always in how state is scoped (per-source versus
   whole-build), not in the validation logic's complexity.
2. A contract's return *type* can itself enforce or prevent an invariant before
   any validation code runs at all — choosing `IEnumerable<KeyValuePair<...>>`
   over a dictionary for `IConfigurationSource.Load()` was what made "detect
   duplicate keys within a source" a real, testable requirement instead of an
   already-impossible one.
3. A previous work package's public API can need a small, genuinely necessary,
   purely additive extension without that being a violation of "do not redesign
   existing architecture" — the test is whether existing behaviour and existing
   callers are left completely unchanged (they were: `TempestServiceProvider`'s
   `Resolve`/`Construct` methods were not touched at all) versus whether the
   change alters what already exists.
4. "No object binding yet" and "string values only" are not limitations to
   route around cleverly — they are scope boundaries doing real work, keeping
   this work package's surface area matched to what was actually asked for.
