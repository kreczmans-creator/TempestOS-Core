# Engineering Standard: Exception Design

## Purpose

This standard describes the consistent pattern TempestOS's runtime code follows
for exceptions, established in WP 2.1 and applied without deviation ever since —
through WP 2.4's own four hierarchies, and every capability the platform has
added since (Configuration, Plugins, the Runtime Host itself, and beyond).
Following it is not optional stylistic preference — it is what keeps
`catch` blocks throughout the codebase meaningful, and what makes every failure
message actually useful for debugging rather than merely present.

## The Rule

**One dedicated exception hierarchy per pipeline stage or capability, never
shared across stages.**

- Discovery (WP 2.1): `ModuleDiscoveryException` (base) →
  `DuplicateModuleIdException`.
- Registration (WP 2.2): `ModuleRegistrationException` (base) →
  `DuplicateModuleRegistrationException`, `ModuleNotRegisteredException`.
- Lifecycle (WP 2.3): `ModuleLifecycleException` (base) →
  `InvalidModuleLifecycleTransitionException`.
- Dependency Injection (WP 2.4): `ServiceResolutionException` (base) →
  `ServiceNotRegisteredException`, `CircularServiceDependencyException`,
  `AmbiguousConstructorException`.
- Configuration (WP 2.5): `ConfigurationException` (base) →
  `InvalidConfigurationEntryException`, `DuplicateConfigurationKeyException`,
  `ConfigurationKeyNotFoundException`.
- The Runtime Host (WP 2.7B): `HostException` (base) →
  `InvalidHostStateTransitionException`.
- Plugins (WP 4.2): `PluginException` (base) → `InvalidPluginManifestException`,
  `IncompatiblePluginVersionException`, `DuplicatePluginIdException`,
  `PluginAssemblyNotFoundException`, `PluginAssemblyLoadException`.

Two capabilities deliberately introduce **no** exception type at all —
Logging (WP 2.6) and Platform Versioning (WP 4.2A) are both designed to
*degrade* rather than throw (a sink failure is caught internally and reported
to `Console.Error`; missing version metadata falls back to a documented
default), which is a designed absence, not a gap in this standard's coverage.
The Event Bus (WP 4.4D) is a third: a subscriber's own exception is caught,
logged, and never rethrown — by design (ADR-0028) — so there is no failure
category here that escapes containment and needs a dedicated exception type
for anything to catch.

Each hierarchy exists so a caller can `catch` the *base* type for that stage and
handle "something went wrong during discovery" (or registration, or lifecycle,
or resolution, or configuration, or plugin loading) as one category, without
also silently catching failures that belong to an unrelated stage. This
decision is deliberate and was reconsidered explicitly more than once (see
WP 2.2's retrospective, "Alternatives Considered") — reusing an existing,
superficially similar hierarchy was tempting each time and rejected every
time, for the same reason. See `docs/architecture/Platform Service Map.md`'s
own per-service entries for each capability's current exception types —
this document explains the *rule*; that one documents each service's own
detail, since a second, hand-maintained exhaustive list here would
inevitably drift out of date as new capabilities are added.

**Dedicated subtypes carry structured data, not just a message string.** Every
subtype exposes the specific values relevant to that failure as real properties
— `DuplicateModuleIdException.ModuleId`, `InvalidModuleLifecycleTransitionException.CurrentState`/`AttemptedOperation`,
`ServiceNotRegisteredException.MissingServiceType`/`RequestedService`/`ResolutionChain`
— so a caller (or a test) can assert on the actual failure data, not parse a
message string to recover it.

**`ArgumentException`/`ArgumentNullException` for caller-contract violations;
dedicated exceptions for domain/business-rule violations.** A `null` argument or
a blank required string is a programming error at the call site — standard
.NET argument-validation exceptions are correct and sufficient. "This ID is
already registered," "this transition isn't valid from this state," "this
service has no registration" are not caller mistakes in the same sense — they
are legitimate outcomes of a correctly-called operation encountering an invalid
*situation*, and get TempestOS's own, dedicated exception types.

**Never a generic `Exception` or bare `InvalidOperationException` for a
condition worth a caller distinguishing.** If a failure mode is worth a test
category (WP 2.1 through WP 2.4's test suites all include dedicated tests per
failure mode), it is worth a dedicated exception type.

## Why

This standard exists because the alternative — generic exceptions,
string-matching on messages, or reusing an unrelated hierarchy for convenience —
produces code that *looks* like it handles errors but actually can't
distinguish between them reliably. See the Fail Fast Engineering Principle
document for the broader reasoning, and ADR-0007's Consequences section for a
concrete example of how much more informative TempestOS's exceptions are
compared to what `Activator.CreateInstance`'s own, generic failures would have
produced for the same underlying problem.

## Applying This Standard to New Work

When a new work package introduces a new category of failure:

1. Does it belong to an existing pipeline stage's hierarchy, or is it a new
   stage? If new, create a new base exception for that stage — do not attach it
   to an existing hierarchy just because the failure is superficially similar to
   one that hierarchy already handles.
2. Does the failure need structured data for a caller (or test) to act on
   meaningfully? If so, expose it as properties on the exception, not only in
   the message text.
3. Is this a caller-contract violation (null, blank, wrong type) or a
   domain-rule violation (duplicate, invalid transition, missing registration)?
   The former gets a standard .NET argument exception; the latter gets a
   dedicated type in the new or existing hierarchy.
