# WP 9.0A — Mechanical Product Structure — Security Review Report

## Purpose

A proportionate security review of the three new Domain facets
(`IRenamable`/`IHasParent`/`IDeletable`), the six new Workspace commands,
and the new Kind-keyed extension points, reviewed across the same
dimensions this project's own established Security Review convention
uses (`WP7.1D`, `WP7.1E`, `WP7.3A`) — each classified **Not Applicable**,
**Accepted Risk**, **Technical Debt**, or **Release Blocking**. `v0.8.0`'s
own `WP8.9.0` release review explicitly named "zero dedicated Security
Reviews performed" as that release's own most important open
recommendation; this is the first Security Review since.

## Review

| Dimension | Finding | Classification |
|---|---|---|
| **Authorisation boundaries** | Every new Domain method (`RenameAsync`/`MoveAsync`/`DeleteAsync`) performs no internal permission gating — mirrors `IRequirementsService`/`IMaterialCatalog`'s own identical calling-layer-enforced posture (`ADR-0061`'s own precedent, never itself reopened). Every new Workspace command handler likewise performs no permission check of its own; Workspace mutation commands generally are calling-layer-enforced, consistent with every command this platform has shipped to date. | Not Applicable — reviewed, design consistent with established precedent |
| **Structural integrity — circular parent** | `MoveAsync` validates the full candidate-ancestor chain before committing, rejecting self-reference and any descendant target (`CircularParentAssignmentException`, `TEMPEST-VAL-006`, renumbered from `-002` by `WP 9.0B` — see that release's own Technical Debt Assessment) — proven by dedicated tests (`MoveAsync_UnderItself...`, `MoveAsync_UnderOwnDescendant...`). Without this guard, a malformed Move could produce an unbounded/cyclic tree, breaking every consumer that walks `ParentId` (the Explorer, the ancestry walk). | Not Applicable — reviewed, guard proven effective |
| **Structural integrity — orphaned children** | `DeleteAsync` rejects deletion of an object with live children (`EngineeringObjectHasChildrenException`, `TEMPEST-VAL-007`, renumbered from `-003` by `WP 9.0B`) — proven by dedicated tests. Prevents a deleted object's children from becoming permanently unreachable from any root. | Not Applicable — reviewed, guard proven effective |
| **Data retention / tamper resistance** | `DeleteAsync` is soft-delete only — no document, revision, or relationship is ever erased, matching the platform's own append-only posture (`TD-16`'s existing "no cryptographic signing" disclosure is unaffected, neither worsened nor improved). A "deleted" object remains fully queryable by anyone already holding its Id. | Accepted Risk — matches existing, already-disclosed platform-wide posture; not a new gap |
| **Revision/relationship integrity** | `MoveAsync`'s own `"groupedUnder"` link reuses the existing, already-reviewed `LinkAsync`/`IEngineeringDocumentStore.LinkAsync` append-only guarantee — no new write path was introduced. | Not Applicable — inherited, already-reviewed guarantee |
| **Concurrent modification** | Two concurrent `RenameAsync`/`MoveAsync`/`DeleteAsync` calls against the same object are not protected by compare-and-swap — the same disclosed posture `TD-25` already accepts for `ReviseAsync`/`SetStatusAsync`. `_structuralLock` (a plain `lock`) guarantees the in-memory field itself is never corrupted by a data race; it does not detect or reject a "lost update" between two editors' own intents. | Technical Debt — mirrors `TD-25`'s own existing disposition; not separately re-registered as new, since it is the identical, already-tracked pattern extended to three more mutators |
| **Exception disclosure** | `CircularParentAssignmentException`/`EngineeringObjectHasChildrenException` disclose only Guids and counts already known to the caller — no internal state, no stack detail. | Not Applicable — reviewed, no gap found |
| **Command Framework surface** | All six Mechanical `CommandDescriptor`s omit `createDefault` — none is invokable by bare Id through `ICommandRegistry.InvokeAsync`, closing off a class of "invoke a mutating command with no real target" concern by construction, not by a runtime check. | Not Applicable — reviewed, secure by construction |
| **Serialization safety** | No new serialization surface was introduced — every new type is either a plain C# record/class held in memory or reuses `IEngineeringDocumentStore`'s own existing, already-reviewed content path. | Not Applicable |
| **Resource exhaustion** | `MechanicalProductStructureNodeProvider`/`DeleteAsync`'s own children check both call `IEngineeringObjectRepository.ListAllAsync` and filter client-side — O(n) in total object count, the same characteristic `IAuditQuery`/`MaterialCatalog.ListAsync` already carry, disclosed, not newly introduced. | Technical Debt — mirrors `TD-22`/`TD-24`'s own existing "no measured problem yet" disposition; not separately re-registered |
| **Dependency risk / supply chain** | No new third-party dependency was introduced anywhere in this Work Package. | Not Applicable |
| **Secure defaults** | `IsDeleted` defaults `false`; `ParentId` defaults `null` (top level); `MoveAsync`/`DeleteAsync` both reject invalid input before mutating any state (validate-then-commit, never commit-then-validate). | Not Applicable — reviewed, secure by construction |
| **Backwards compatibility** | Every existing, already-shipped concrete Kind (Requirement, Risk, and every other of the ~30 non-Product-Structure classes) is unaffected — none composes any of the three new facets; `EngineeringObjectBase`'s own new unconditional members add surface, never remove or change any existing one. | Not Applicable |

## New Debt Disclosed by This Review

No new Technical Debt item is registered by this review specifically —
every finding above either confirms an existing, already-tracked
disposition (`TD-25`, `TD-22`/`TD-24`'s pattern) or finds no gap.
`TD-26` (the pre-existing Runtime Host timing characteristic) is
disclosed separately, in `WP9.0A Technical Debt Assessment.md` — found
during Workspace integration verification, not a security finding in
its own right (no data is exposed or corrupted; only a render is
transiently stale).

## Verdict

**Zero Release Blocking findings.** No new attack surface was
introduced; every mutating operation added by this Work Package validates
before committing; every disclosed risk either mirrors an already-accepted
platform-wide posture or an already-registered, unchanged Technical Debt
item.

## Related Documents

`ADR-0080`; `ADR-0081`; `WP7.3A Security Review Report.md` (`TD-25`
precedent); `WP9.0A Technical Debt Assessment.md`; `docs/governance/
Quality/Technical Debt Register.md`.
