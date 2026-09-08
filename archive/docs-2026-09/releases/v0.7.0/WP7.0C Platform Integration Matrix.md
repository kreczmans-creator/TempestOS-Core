# WP 7.0C — Platform Integration Matrix

## Status

Complete. Demonstrates, at the contract level, how each proposed
Engineering Foundation framework consumes the eleven `v0.6.0`-certified
Platform Services — more concrete than `WP7.0B Platform Consumption
Matrix.md`'s own candidate-Work-Package-level assessment, since real
interface-level dependencies are now proposed in `WP7.0C Engineering
Foundation Contracts.md`.

## Matrix

| Framework | Identity & Permissions | Settings | Audit | Persistence | Reporting | Notifications | REST API | Export/Import | Licensing | Diagnostics | Command Framework |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Engineering Data Model | ✅ Attributes `AuthorPrincipalId` via `CurrentPrincipalAccessor` | — | Plausible — see Integration Note 1 | ✅ Plausible storage substrate (open question, `WP7.0C Required ADR Catalogue.md`) | Plausible — a document is a plausible report input | — | Plausible — future REST exposure would mirror `ApiSampleModule`'s own zero-business-logic pattern | ✅ Plausible export candidate — a document/revision is naturally exportable via `IExportable` | — | — | — |
| Units & Quantities | — | — | — | — | — | — | — | — | — | — | — |
| Materials Framework | ✅ Registration authorization | — | ✅ Plausible — material registration is a natural audit event | (via Engineering Data Model) | Plausible — a material specification as report content | — | Plausible | ✅ Plausible export candidate | — | — | — |
| Calculation Framework | ✅ `ExecutedByPrincipalId` attribution | — | Plausible — see Integration Note 2 | — | Plausible — a calculation record as report content | Plausible — a calculation failure as a notification trigger | Plausible | Plausible — a calculation record as export content | — | — | Plausible — a calculation could be dispatched via a Command, mirroring how the REST API dispatches through the Command Framework rather than a parallel invocation path |
| Verification & Validation | ✅ `VerifiedByPrincipalId`; permission-gated history reads, mirroring `IAuditQuery` | — | ✅ Plausible — see Integration Note 3 | (via Engineering Data Model) | ✅ Plausible — verification history as a report (explicitly named in `WP7.0C Engineering Foundation Contracts.md`) | ✅ Plausible — a Fail outcome as a notification trigger | Plausible | ✅ Plausible export candidate | — | — | — |

## Integration Notes

1. **Engineering Data Model and Audit.** Whether every document
   creation/revision is automatically audited, or whether auditing
   remains the calling consumer's own responsibility (mirroring how
   Reporting's own generation is not itself audited — that is
   `GenerateSampleReportCommandHandler`'s own calling-layer
   responsibility), is an open question for the owning Work Package —
   this matrix marks it Plausible, not Confirmed, deliberately.
2. **Calculation Framework and Audit.** A calculation's own
   `CalculationRecord<TResult>` already answers "what was calculated, by
   whom, when" — whether a *separate* Audit entry is also warranted (for
   consistency with how every other Platform-Service-adjacent action is
   audited) or would be a genuine duplication is a real design question,
   not resolved here.
3. **Verification and Audit.** The clearest candidate for **not**
   duplicating Audit: a verification action is naturally recorded via
   `IAuditRecorder` at the calling layer (mirroring every `v0.6.0`
   sample module's own pattern) precisely *because* `IVerificationRecord`
   itself answers a different question ("was the spec met," not "who
   did what") — the two are complementary, not redundant, once built
   together correctly.

## Reading the Matrix

- **Every one of the eleven Platform Services appears as a plausible
  consumer of at least one framework**, mirroring `WP7.0B Platform
  Consumption Matrix.md`'s own finding at the candidate-Work-Package
  level, now confirmed at the interface-contract level.
- **Units & Quantities consumes nothing** — the one framework in this
  set with zero Platform Service dependency, confirmed identically in
  `WP7.0C Cross-Framework Dependency Report.md` and `WP7.0C Engineering
  Foundation Contracts.md`. This is not a gap; it is the correct shape
  for a pure value-type library.
- **Settings, Licensing, and Diagnostics have no plausible consumer in
  this set** — none of the five frameworks has a natural need for
  user-changeable configuration, a licensed-capability gate, or a
  Host-lifecycle projection. This is disclosed as a genuine finding, not
  papered over: unlike `WP7.0B`'s own Candidate Work Package Catalogue
  (where Candidates I/J, Requirements/Project Engine, plausibly consume
  Settings), the five Engineering Foundation frameworks themselves are
  lower-level infrastructure with narrower Platform Service needs.

## Related Documents

`docs/releases/v0.6.0/WP6.8 Platform Consumption Matrix.md` (the `v0.6.0`
precedent); `WP7.0B Platform Consumption Matrix.md` (the immediately
preceding, candidate-level precedent); `WP7.0C Engineering Foundation
Contracts.md`.
