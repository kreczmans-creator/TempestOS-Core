# Minimal Interface, Extension-Method Sugar

## 1. Introduction

`IServiceCollection` and `ITempestServiceProvider` (WP 2.4) both follow the same
shape: a small, single-method core interface, with every convenience overload a
caller actually uses implemented as an extension method on top of it. This
document describes that pattern and why TempestOS reaches for it specifically
when a public API needs both a minimal, implementable contract and a richer,
ergonomic calling surface.

## 2. Purpose

To keep an interface's *implementable* surface as small as possible — anyone
writing a new `IServiceCollection` or `ITempestServiceProvider` implementation
only has to implement one real method — while still giving *callers* the full,
convenient API a hand-written, monolithic interface would offer.

## 3. Background

WP 2.4's brief specified four registration forms (`Singleton<T>()`,
`Singleton<TInterface,TImplementation>()`, `Transient<T>()`,
`Transient<TInterface,TImplementation>()`) and, separately, needed a way to
register a runtime `Type` object with no compile-time generic argument
available at all — specifically for `AddDiscoveredModules`, which only has a
`ModuleDescriptor.ModuleType` (a `System.Type` value), not a `<T>` the compiler
could bind to.

## 4. The Problem

A literal reading of the brief might have produced four separate methods
directly on `IServiceCollection`, each generic, none of them usable for a
reflection-discovered type with no compile-time type argument. Something needed
to bridge "the four convenient, generic forms callers want to write" and "a
single, Type-based form the framework itself needs internally."

## 5. The Design

`IServiceCollection` exposes exactly one real method: `Add(Type serviceType,
Type implementationType, ServiceLifetime lifetime)`. A separate static class,
`ServiceCollectionExtensions`, provides `Singleton(Type, Type)`,
`Singleton<TService>()`, `Singleton<TService,TImplementation>()`, and the
`Transient` equivalents — every one of them, ultimately, calling `Add`.
`ITempestServiceProvider` mirrors this: one real method, `GetService(Type)`, with
`GetService<T>()` provided as an extension method calling it.

## 6. Alternatives Considered

**Four generic methods directly on the interface**, as the brief's phrasing
might suggest at first read. Rejected: this would have made every
`IServiceCollection` implementation responsible for reimplementing all four
forms, and would still have left no way to register a bare, reflection-obtained
`Type` without an awkward workaround.

## 7. Why This Solution Was Chosen

The minimal-core-plus-extensions shape satisfies both needs simultaneously: the
interface itself stays small and easy to implement or mock; callers get exactly
the convenient API the brief specified; and the framework's own internal code
(`AddDiscoveredModules`) gets a Type-based entry point that was never a
compromise or an afterthought — it's the same method the generic overloads
themselves delegate to.

## 8. Architectural Principles

Interface Segregation (from SOLID) — the interface exposes only what an
implementer must provide; callers who want the richer surface get it without
that surface being part of what a new implementation is obligated to write.

## 9. Benefits

A hypothetical second `IServiceCollection` implementation (an in-memory test
double with additional inspection capabilities, say) only needs to implement one
method to get the entire convenient API "for free," since the extension methods
work against the interface, not any specific implementation.

## 10. Trade-offs

A reader unfamiliar with the pattern, looking only at `IServiceCollection`'s own
definition, might not immediately realise `Singleton<T>()` exists at all — it's
discoverable only by knowing to look at `ServiceCollectionExtensions`, or by
IDE autocomplete surfacing extension methods in scope.

## 11. Common Mistakes

Adding a new convenience form directly to the interface, rather than as an
extension method, the next time a new registration style is needed — this would
silently reintroduce the exact problem this pattern exists to avoid (every
implementation now needing to implement the new form directly).

## 12. Future Evolution

Any future registration form (a factory-based registration, for instance) should
be added as a new extension method calling `Add`, or a small, additional
core-interface method if it genuinely can't be expressed in terms of `Add`
alone — not as a method bolted directly onto `IServiceCollection`.

## 13. Key Takeaway

When an interface needs to be both minimal-to-implement and rich-to-call, those
two goals aren't in tension if the rich API is built as extension methods over a
small core — the tension only exists if both are forced to live on the same
interface definition.
