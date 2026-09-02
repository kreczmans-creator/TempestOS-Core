# Production Rehydration & the Principal Boundary

**Programme:** Product Convergence & Recovery, 2026-08-29 ·
**Debt:** `TD-103`, `TD-104` (resolved), `TD-75` (partially) ·
**Decision:** `ADR-0116` ·
**Code:** `Tempest.App.Workspace.CanonicalObjectKinds`,
`Tempest.Core.Identity.ISessionPrincipalSource`,
`Tempest.Desktop.WorkspaceHost`

## Two defects with one shape

These look like unrelated pieces of work — one about persistence, one about
identity. They are the same defect twice:

> **The product worked because the sample harness happened to ship.**

Twelve engineering Kinds — Risk, Task, Decision, Supplier, Milestone and
the rest — could be persisted and read back only because a class inside
`Tempest.Samples` registered how. And the desktop shell established no
principal at all: the only callers of `EstablishCurrentPrincipal` in the
whole product were sample modules, during their own initialisation.

So a build of TempestOS without the samples could write a Risk and never
read it, and would run as nobody. Neither would have shown up in a test
suite, because the test process references the sample assembly. Everything
passed.

## The lesson: test the dependency, not the symptom

This is the part worth carrying to other work.

The natural test for "the product can rehydrate a Risk without the samples"
is to create a Risk, restart, and assert it comes back. That test is
correct, useful, and **proves nothing about the samples** — the sample
assembly is loaded in the test process either way. It would have passed
identically before this work.

You cannot unload an assembly to check. So the load-bearing test asserts
the thing itself:

```csharp
// ProductionRehydrationTests
foreach (var type in registeringTypes)
    Assert.NotEqual("Tempest.Samples", type.Assembly.GetName().Name);
```

Everything else in that file is behavioural and matters — but it matters
*because* that one assertion holds. When you are proving an absence, find
the assertion that fails when the absence stops being true, not the one
that describes what you hope follows from it.

## The nine nobody had noticed

The twelve sample-registered Kinds were known (`TD-75` names them). Auditing
the registration list would have found exactly those twelve and stopped.

Reflecting over the **domain** instead — every `EngineeringObjectBase`
subclass implementing `IRehydratable<T>` — found nine more:
`Approval`, `Assumption`, `EngineeringAction`, `Hazard`, `Issue`, `Review`,
`Simulation`, `Test`, `Verification`. Registered nowhere at all. Each could
be created, each was written to disk, each was discarded at the next launch,
and the loss was recorded as a `Warning` in a log with no reader.

A list of what you registered can only ever agree with itself. The check
that finds what is missing has to start from the other side:

```csharp
// TheProductionRegistry_CoversEveryPersistableDomainType
var persistable = typeof(EngineeringObjectBase).Assembly.GetTypes()
    .Where(t => t.IsSubclassOf(typeof(EngineeringObjectBase)))
    .Where(t => t.GetInterfaces().Any(i =>
        i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRehydratable<>)));
```

That check now stands permanently. It would have caught `TD-104` on the day
it was introduced, and it will catch the next one.

## Round-tripping properly: compare the whole state

The first version of the round-trip test asserted identity, Kind and
business identifier. Six Kinds failed — and the *test* was wrong, not the
product: `Approval`, `Review`, `Simulation`, `Test`, `Verification` and
`ExternalSystemLink` have no business identifier to carry.

The fix was to stop picking properties and compare `CaptureState()` before
and after, `TypeState` dictionary included. `TypeState` is each concrete
type's own contribution to its persisted form, so comparing it means an
omission in *any* one type's rehydration constructor fails as a changed key
— rather than passing because the three properties the test happened to
name were the three that survived.

When a test for "did this survive" enumerates fields, it tests the fields
you thought of.

## The principal boundary, and what it deliberately is not

TempestOS today is a local, single-user desktop application. So the boundary
says exactly that, and nothing more:

```
desktop session → ISessionPrincipalSource → ICurrentPrincipalAccessor → services → domain
```

One interface with one method. `LocalSessionPrincipalSource` answers it from
the operating system's account name — the only true statement available
about who is using a local single-user application.

What makes this a boundary rather than a shortcut is what it *refuses* to
do. No login, no credentials, no token, no roles model. No username threaded
through call sites. And no user field on engineering objects — an
engineering object is never responsible for knowing who is signed in; it
asks, through an accessor it already read. When Administration becomes the
authority for identity, it implements this one interface and nothing
downstream changes.

Two standing tests keep it honest, because the way this decision goes wrong
is by quietly growing:

- `TheBoundaryIsNotAuthentication` — no credential-, login- or token-shaped
  member appears in `Tempest.Core.Identity`.
- `NoEngineeringObject_CarriesAUserFieldOfItsOwn` — no domain type holds an
  `IPrincipal` or `IIdentity`.

## The bug the fix introduced

Worth recording, because it is the most instructive thing here.

The first implementation published the principal like this:

```csharp
if (SessionPrincipal is not null && principalAccessor is CurrentPrincipalAccessor accessor)
    accessor.SetCurrent(SessionPrincipal);   // wrong
```

Which reads as ordinary defensive care: don't overwrite with null.

The acceptance test for "no principal can be established" then failed with
`sample.verificationworkspace-user`. When the boundary declined to answer, a
**sample module's** principal was left standing as the session's — which is
`TD-103` itself, wearing a fix.

The boundary has to be authoritative in both directions. Publishing null is
not losing information; it is the answer. `"unknown"` authorship and
`RequirementVerificationState.Unknown` are preserved for exactly this
reason: unknown is a true statement, and a fabricated user is not.

Mutation `M4` restores that guard and is killed by the test that found it.

## What this did not fix

`TD-75`'s other half. `Tempest.App` still project-references
`Tempest.Samples`, so all 33 sample modules still initialise in a real
launch and the "Sample" ribbon tab is still visible to end users. That is a
packaging change with no bearing on rehydration, and it was left alone
rather than folded into a closure that would have read as more complete than
it was.

Half a debt closed and said so beats a whole one claimed.
