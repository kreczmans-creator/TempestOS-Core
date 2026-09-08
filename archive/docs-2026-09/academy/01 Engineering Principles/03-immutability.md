# Immutability

## What

An immutable object is one whose observable state cannot change after
construction. Once created, every property, field, and derived value remains
fixed for the object's entire lifetime. Any "change" produces a new object rather
than modifying the existing one in place.

## Why

Mutable shared state is one of the largest sources of bugs in software that
involves more than one thread, more than one owner, or any code path where an
object outlives the operation that created it. If any holder of a reference can
change the object underneath any other holder, every piece of code that reads
that object must defensively consider "what if this changed since I last looked?"
Immutability eliminates the question by construction: it cannot have changed,
because nothing can change it.

## Benefits

- **Safe to share.** An immutable object can be handed to any number of callers,
  on any number of threads, with no synchronisation required to read it safely.
- **Safe to cache and compare.** Because its value can never diverge from what it
  was at creation, an immutable object is trivially safe to hold onto, log,
  compare for equality, or use as a dictionary key.
- **Eliminates an entire category of bugs**: no "who changed this and when,"
  because nothing can.
- **Forces state transitions to be explicit.** If you can't mutate an object,
  representing "this thing progressed to a new state" requires either a new
  object or an explicit, separate piece of code whose job is to track that
  progression — which tends to make state management a deliberate design decision
  rather than something that happens implicitly, wherever convenient, via a
  setter call.

## Disadvantages

- Every "change" requires constructing a new instance, which can be wasteful if
  changes are frequent and the object is large (though for the small,
  metadata-shaped objects typical of a project like TempestOS, this cost is
  negligible).
- Genuinely needing to track something that changes often and needs cheap,
  frequent updates (a running counter, a live connection pool size) is awkward to
  express as "immutable snapshots" without extra machinery — somewhere in the
  system, something has to actually be mutable, or updates have to be batched
  into occasional new-snapshot construction.
- Can push complexity elsewhere rather than removing it: if state must change
  somewhere, immutability just moves the question of "where does the mutable
  state live" rather than answering it — it has to be answered by *something*.

## When to Use

For anything representing a fact, a record, or a point-in-time snapshot: an
identifier, a configuration value, a "this happened at this time" record, a
descriptor. Also appropriate any time an object will be shared across threads or
handed out to callers you don't fully trust not to mutate it.

## When Not to Use

When an object genuinely represents something that changes frequently and rapidly,
and constructing a new instance on every change would be either wasteful or would
obscure rather than clarify the design (a UI control's live pixel buffer, for
example). Immutability is a tool for taming *shared, long-lived* state — it is not
a universal requirement for every object in a system.

## How TempestOS Applies It

`RuntimeModule` (WP 2.2) is the clearest example: sealed, every property get-only,
constructor `internal` so only `RuntimeModuleManager` can create one — see
ADR-0001 and the accompanying case study for the full reasoning. Once a
`RuntimeModule` is handed to a caller via `Get`/`TryGet`/`GetAll`, its value is
fixed forever; there is no way, from any code outside `Tempest.Core`, to make it
say something different than what it said at registration time.

`ModuleDescriptor` (WP 2.1) is immutable for the same reason: it is a record of
what discovery found, at the moment it found it — a fact, not a live value.

`ModuleLifecycleStatus` (WP 2.3) is immutable too, but in a more subtle way: it is
a *snapshot* of something that genuinely does change over time (a module's
lifecycle state). Rather than making the underlying state itself immutable
(impossible — it has to change as a module progresses), `ModuleLifecycleManager`
keeps a private, mutable `TrackedModule` internally, and only ever hands callers
an immutable snapshot of it at the moment they ask. This is the pattern to reach
for when the *real* thing has to be mutable somewhere, but callers should never be
able to observe it changing out from under them mid-read.

Collections returned to callers follow the same philosophy at a different level:
`RuntimeModuleManager.GetAll()`/`.Modules` and `ModuleLifecycleManager.Modules`
both return a `ReadOnlyCollection<T>` wrapping a freshly-copied list — genuinely
immutable through any standard collection interface, not merely a `List<T>`
upcast to a read-only-looking type that a determined caller could downcast and
mutate.

## Key Takeaway

Immutability doesn't eliminate mutable state from a system — it *localises* it.
Somewhere, something is still tracking change (`ModuleLifecycleManager`'s private
`TrackedModule`), but everything *outside* that one place only ever sees fixed,
trustworthy values.
