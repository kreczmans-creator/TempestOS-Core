# ADR-0123: A Migration-Chain Collision Is a Registration Error, With No Opt-Out

## Status

Accepted — `WP 16.4B-R3` (Architecture), 2026-09-05. Records a decision
taken during `WP 16.4B-R1` and closed there with an explicit "No ADR";
the independent post-remediation review found that judgement wrong under
Engineering Governance §5, and this ADR corrects it. Extends `ADR-0120`
(durable state carries a schema version) without reopening it, and is a
deliberate divergence from `ADR-0122`, which is explained below rather
than left for a reader to notice.

## Context

`ADR-0120` makes migrations an ordered chain: a common (Kind-less) chain
runs first for a given version, then that `Kind`'s own chain.
`StateMigrationRegistry` held each chain in a dictionary keyed by
`FromVersion`, and `Register` wrote straight into it:

```csharp
chain[migration.FromVersion] = migration;
```

Two distinct collisions were therefore silent.

**Same chain, same version.** A second registration replaced the first —
last-wins, no exception, no log. This is precisely the defect `TD-69`
recorded in the DI container and `ADR-0122` closed there, in a second
place, found by grepping for the pattern after `ADR-0122` was written.

**Common versus Kind-specific, same version.** Worse, and specific to
this type. `Find` consults the common chain first and returns
immediately, so a Kind-specific migration registered at the same
`FromVersion` as a common one **never runs** — and the read loop still
stamps the record `FromVersion + 1` and carries on. The record therefore
arrives at the current version looking fully migrated while that Kind's
own field transformation never happened. That is exactly the
"parses correctly but means something different" failure `ADR-0120`
exists to prevent, reintroduced one layer up, in the migration-authoring
surface itself.

Nothing in the platform registers a real migration today —
`CurrentSchemaVersion` is `1` — so neither collision has ever occurred.
Both are traps set for whoever writes the first real migration.

## Decision

**A colliding migration registration throws. There is no opt-out.**

- `DuplicateStateMigrationException` — a migration is already registered
  for the identical chain and `FromVersion`.
- `ConflictingStateMigrationException` — registering would leave a common
  and a Kind-specific migration both claiming the same `FromVersion`.
  **Checked in both registration orders**, since either order produces
  the same unreachable migration and a guard that caught only one would
  read as complete while leaving the trap set.

`Find`'s common-chain-first ordering is unchanged — that is `ADR-0120`
Decision 2 and it is correct. This decision removes the *ambiguous* case
rather than redefining the defined one.

**Divergence from `ADR-0122`, deliberately.** The DI container's
equivalent guard carries an `allowReplace: true` opt-in. This one does
not, and the difference is the point:

- `ADR-0122` needed an escape hatch because roughly 330 registration call
  sites already existed and a test host substituting a fake for a
  platform service is a foreseeable, legitimate need.
- Here there are **zero** registered migrations, so no caller needs an
  escape; and unlike a service substitution, there is no coherent meaning
  for "replace this migration" — a chain either bridges a version or it
  does not. An opt-in would exist only to reopen the hazard.

If a legitimate replacer ever appears, that is a new decision, made then,
with the evidence in hand.

## Consequences

- The first author of a real migration finds a collision at startup, with
  the colliding Kind and version named, instead of shipping a record that
  looks migrated and is not.
- Registration is fail-fast at composition time, in a single-threaded
  startup path, so the throw cannot surprise a running system.
- **`Register` is now atomic on failure.** The original implementation
  created a Kind's chain dictionary before running its checks, so a throw
  left a phantom empty chain behind — benign, since `_byKind` is private
  and nothing reads its key set, but a real violation of the guarantee
  this ADR's own guard advertises. Fixed as part of this decision rather
  than recorded as debt, because a guard that promises atomicity should
  have it.
- Two exception types are added to a hierarchy that already has many.
  Both derive from `EngineeringDomainException`; no new root.
- The asymmetry with `ADR-0122` is a standing question a future reader
  will ask. It is answered above so they do not have to re-derive it.

## Alternatives Considered

**Leave it silent (the status quo).** Rejected: it is the same defect
class `TD-69` already proved harmful, and here its consequence is a
misread persisted record rather than a swapped service.

**Warn and continue.** Rejected, consistent with `ADR-0122`'s own
reasoning and with a second instance from this same release, where
`new-release.ps1`'s yellow warning about un-green CI was found to be
functionally equivalent to no check at all (`WP 16.1A-R1`).

**Run both migrations when they collide** (common, then Kind-specific,
advancing the version once). A real option, and arguably the most
permissive. Rejected because "both run" needs an ordering contract
between two independently-authored migrations that nothing establishes,
and silently composing two transformations nobody wrote together is a
worse failure than refusing the registration.

**Add `allowReplace`, mirroring `ADR-0122`.** Rejected — see Decision.

## Future Considerations

- Revisit if a real replacer appears; that is a new decision.
- The keyed-collection-silently-overwrites pattern has now been found
  twice (`ADR-0122`, here). Worth a sweep of remaining registration
  surfaces before v1.0 — `Platform Services`, navigation providers, and
  the rehydrator registry are the obvious candidates.

## Related Documents

`docs/adr/ADR-0120-durable-state-carries-a-schema-version-and-migrations-apply-only-on-read.md`;
`docs/adr/ADR-0122-registering-a-service-twice-is-an-error-not-a-replacement.md`;
`docs/releases/v0.16.0/WP16.4B-R1 Migration Collision Guard and Platform Service Registration.md`;
`docs/releases/v0.16.0/WP16.4B-R3 Per-Object Write Serialisation.md`;
`docs/governance/Quality/Technical Debt Register.md` (`TD-69`, `TD-87`);
`src/Tempest.Core/EngineeringDomain/Implementation/EngineeringObjectStateStore.cs`;
`docs/academy/06 Engineering Standards/Engineering Governance.md` §5.
