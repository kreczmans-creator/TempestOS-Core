# ADR-0116: Production Rehydration Is Owned by the Product, and the Session Principal Comes From One Boundary

## Status

Accepted — `WP — Production Rehydration & Principal Boundary`, 2026-08-29. Builds on `ADR-0113` (`TD-85`, the single persistence/rehydration boundary), `ADR-0105` (vocabulary ownership) and `ADR-0043` (the local-only identity model). Resolves `TD-103` and `TD-104`; partially resolves `TD-75`.

## Context

Two defects, unrelated in subject and identical in shape: **the product worked because the sample harness happened to ship.**

**Rehydration.** `ADR-0113` established one boundary for bringing engineering objects back after a restart, and every discipline registry registered its own Kinds through it. But twelve canonical Kinds — Risk, Task, Decision, Supplier, Milestone and the rest — were registered by exactly one class, `SampleEngineeringObjectRehydrators`, inside `Tempest.Samples`. A build of TempestOS without the samples could persist a Risk and could not read it back. `TD-75` names the shipping problem; this was its load-bearing consequence.

Auditing that turned up something worse. Reflecting over every `EngineeringObjectBase` subclass implementing `IRehydratable<T>` — rather than reading the registration list, which can only agree with itself — found **nine types registered nowhere at all**: `Approval`, `Assumption`, `EngineeringAction`, `Hazard`, `Issue`, `Review`, `Simulation`, `Test`, `Verification`. Each could be created, each was written to disk, and each was discarded on the next launch. The loss was reported as a `Warning`, into a log with no reader (`TD-104`).

**Identity.** The desktop shell established no principal at all. The only callers of `IIdentityService.EstablishCurrentPrincipal` in the entire product were sample modules, during their own initialisation — so what a running session was permitted to do depended on which sample initialised last, and a product built without samples ran as nobody. `TD-102` met this directly: the project Requirements surface reported every requirement's verification as unreadable, because `verification.read` was held by no one (`TD-103`).

## Decision

**1. Every persistable Kind has a production rehydrator, and `CanonicalObjectKinds` is where the homeless ones live.**

`Tempest.App.Workspace.CanonicalObjectKinds` declares all 21 Kinds and registers them, one line each, through the same `IEngineeringObjectRehydratorRegistry` every discipline uses. `SampleEngineeringObjectRehydrators` is deleted rather than deprecated.

These 21 were **not** distributed into the existing discipline registries, and that was the harder call. Those classes document their constants as "the Kind this registry can *construct*", and none of these 21 can be constructed there — they have no factory in front of them. Adding them would have made each registry's own stated contract false in order to avoid one new class. `CanonicalObjectKinds` is explicitly temporary: as each Kind gains a real discipline workspace, its constant and its registration move to that discipline's registry, the same way every Kind already there arrived.

This adds no second rehydration mechanism. It is one more caller of the one boundary `ADR-0113` established, invoked from the same composition step, and the registry still rejects a second, different claim on any Kind.

**2. The constants here are the canonical owner of those values (`ADR-0105`).**

They were previously string literals inside `Tempest.Samples` — exactly the vocabulary duplication `TD-93` describes. The Engineering Vocabulary Register records the change of ownership; `EngineeringVocabularyConsistencyTests` checks the register against the code.

**3. An unrecoverable object is an error the user sees, not a warning in a log.**

Unknown Kinds are logged at `Error` and surfaced in the running shell as a toast naming what could not be recovered. Rehydration still continues for everything else — one unregistered Kind must not cost a user the rest of their work — but the loss is *stated*. Silent discarding was the worse half of `TD-104`, and a `Warning` nobody reads is silent.

**4. The proof is the absence of a dependency, not the presence of a behaviour.**

`Tempest.Samples` is referenced by, and therefore loaded into, the test process. Every behavioural rehydration test would pass just as happily if the product still leaned on it. So `ProductionRehydrationTests.ProductionRegistration_UsesNoTypeFromTempestSamples` asserts the thing itself: no type on the production registration path, and no type any registered Kind comes back as, belongs to that assembly. That test is the load-bearing one; the round-trips are what make it mean something.

**5. One boundary decides who is using the application: `ISessionPrincipalSource`.**

```
desktop session → ISessionPrincipalSource → ICurrentPrincipalAccessor → services → domain
```

`WorkspaceHost` resolves it once at start-up, after module initialisation so the product's own answer stands rather than a sample's, and publishes it into `ICurrentPrincipalAccessor` — which every consumer already read, and which needed nothing on the consuming side to change. `LocalSessionPrincipalSource` answers from the operating system's own account name, because that is the only true statement available about who is using a local single-user application.

Deliberately **not** chosen: a username threaded through call sites, or a user field invented on engineering objects. An engineering object is never responsible for knowing who is signed in; it asks. `PrincipalBoundaryTests.NoEngineeringObject_CarriesAUserFieldOfItsOwn` keeps it that way.

**6. The boundary publishes its answer including `null`.**

A source that genuinely cannot establish a principal must leave the accessor empty. Publishing only a non-null answer would leave whatever a module happened to establish standing as the session's principal — which is `TD-103` itself, wearing a fix. This was not theoretical: the first implementation had the guard, and both the acceptance test and mutation `M4` reproduced a sample's principal surviving into a session that should have had none.

`EngineeringDocumentStore.UnknownAuthorPrincipalId` and `RequirementVerificationState.Unknown` are therefore preserved, not removed. "Unknown" is a true statement and a fabricated user is not.

**7. This is not authentication, and the absence is asserted.**

There are no credentials, no login, no external identity provider, no token and no roles model in this boundary, and none is implied. TempestOS today is a local single-user desktop application, and this says so honestly rather than building a permissions system nobody asked for. `ApplicationPermissions.LocalSession` is a flat, fixed list of the two permissions first-party surfaces need — `verification.read` and `audit.query` — resolved from nothing. Plugin capability permissions (`plugin.*`) are deliberately excluded: they gate what *registrants* may do and are checked against the registrant, never the person at the keyboard.

`PrincipalBoundaryTests.TheBoundaryIsNotAuthentication` guards this as a standing check rather than an intention, because the way this decision goes wrong is by quietly growing.

## Consequences

**Good.** Every persistable Kind survives a restart with its own type-specific state, proven kind by kind by comparing the full captured state before and after — so an omission in any one type's rehydration constructor fails as a changed `TypeState` key instead of passing unnoticed. `TheProductionRegistry_CoversEveryPersistableDomainType` is the standing check that would have caught the nine orphans and now guards against the next one. The running product knows who is using it, so audit attribution, authorship and permission-gated reads all say something true. Administration can become the authority for identity later by implementing one interface, with no change to the engineering domain.

**Accepted cost.** `CanonicalObjectKinds` is a list of 21 registrations in one class, which is a smell that correctly reflects the underlying fact: 21 real domain types have no discipline that owns them yet. It is visible rather than hidden, and it shrinks as disciplines arrive.

**Not resolved.** `TD-75`'s other half. `Tempest.App` still project-references `Tempest.Samples` and `WorkspaceManager` still carries `using Tempest.Samples;`, so all 33 sample modules still initialise in a real launch and the "Sample" ribbon tab and "Sample Objects" explorer area are still visible to end users. That is a packaging change with no bearing on rehydration, and was left alone rather than folded into a closure that would have overstated what changed.

**Future scope.** Authentication, real user accounts, sign-in and a permissions model belong to Administration (`TD-81`). `ISessionPrincipalSource` is the seam they arrive through. Nothing downstream of it will need to change when they do — which is the whole reason it exists.
