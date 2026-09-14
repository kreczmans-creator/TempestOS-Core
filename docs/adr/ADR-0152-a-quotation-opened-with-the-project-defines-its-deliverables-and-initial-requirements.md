# ADR-0152: A Quotation Opened With the Project Defines Its Deliverables and Initial Requirements

## Status

Accepted — `WP 19.5A` (Quotation core), 2026-09-14.

## Context

Product Owner comment item 4, on the `v0.19.0` candidate: the ribbon's
Deliverables category offered only a disabled "Complete Deliverable" — no
way to add one at all, and deliverables came only from a milestone's own
Timeline, which the Product Owner did not find. The Product Owner's own
model: **"a quote is opened with the project; that will define the initial
requirement set and will also define the deliverables."** Marked "a huge
priority" (2026-09-14), ahead of the review's cosmetic items, with the
spine named explicitly: *quote → requirements → deliverables → completion
→ invoice request*.

`ADR-0150` (`WP 19.0A`) already gave a project its commercial core —
client, purchase order, budget, a pinned rate card, dates — but nothing
that produces the project's own deliverables or its own requirements.
`ADR-0151` (`WP 19.1A`) closed the far end of the spine: a completed
deliverable raises an invoice request. This ADR closes the missing middle:
what a client agrees to buy, before any of it exists to complete.

A fully-built `CustomerQuotation`/`QuotationLine` type already exists —
frozen, under `src/Frozen/Tempest.Core.CommercialIntelligence`
(`ADR-0134`, `D-028`). It is unreachable from any shipped surface, not
compiled into `TempestOS.slnx`. The live `Quotation` Kind this ADR defines
is a separate, much simpler type in `Tempest.Core.Quotations` — same
domain word, deliberately not the frozen one, and nothing here reaches
into `src/Frozen/*`.

Not ERP, not PLM (`D-028`): a quotation carries a reference, a date, a
client, lines and their prices, and a status — no occurrence model, no
change control on a line, no procurement.

## Decision

**1. `Quotation : EngineeringObjectBase, IRehydratable<Quotation>`**
follows `InvoiceRequest`'s own shape exactly (`ADR-0151`): state via
`CaptureTypeState`/`ApplyTypeState`/`Rehydrate`, parented to the project it
quotes. Fields: `Reference` (given, or generated — see §4), `QuoteDate`,
`ClientOrganisationId` (defaults from the project's own client at
creation, a tag, never validated — `ADR-0150`'s own rule for
`Project.ClientOrganisationId`, carried through unchanged), `Currency`
(resolved from the project's own pinned rate card at creation, GBP
otherwise — `InvoicingService`'s own identical resolution, `ADR-0151` §3),
`ValidityDays` (default 30), `Terms`, `Lines`, and `Total` (computed from
`Lines`, never stored, so it can never drift from what the lines actually
carry — unlike `InvoiceRequest.Total`, which is fixed once at creation
because its own lines never change after; a `Quotation`'s lines do, while
Draft).

**2. `QuotationLine(Id, Description, Hours?, Rate?, FixedPrice?, Amount,
Basis, DeliverableId?, RequirementId?)`.** `Basis` (`Hourly`/`FixedPrice`)
says which of `Hours`×`Rate` or `FixedPrice` alone priced the line;
`Amount` is carried alongside rather than recomputed, exactly as
`InvoiceRequestLine.Amount` is. `DeliverableId`/`RequirementId` are
`null` until Accept (§5) fills them in. `Id` is this record's own stable
identity — a `Quotation`'s lines are edited in place while Draft, unlike
an `InvoiceRequest`'s, which never change after creation, so a line needs
an address `UpdateLineAsync`/`RemoveLineAsync` can name.

**3. `QuotationStatus`: `Draft → Sent → Accepted | Declined`** — four
values, not `InvoiceRequestStatus`'s nine: a quotation has no connector, no
"reauthorise", no "unavailable". `Accepted` and `Declined` are both
terminal — **no revision of an accepted quotation in this release**: a
change to accepted work is a new quotation, never an edit to this one.
`QuotationStatusTransitions` mirrors `InvoiceRequestStatusTransitions`'
own table shape exactly, `internal` for the identical reason.

**4. The reference counter is computed, not a stored counter.** A blank
`reference` at `CreateAsync` generates `Q-<yyyy>-<nnn>`: `QuotationService`
scans every `Quotation` this store already holds (live or not — a
reference, once used, is never reissued), keeps the highest `<nnn>` suffix
already used for `<yyyy>`, and mints one past it. No new persistence
mechanism — this is the same "derive from what the store already holds"
discipline `ProjectDirectory.CreateAsync` already uses for identifier
uniqueness (`TD-38`). **Disclosed, not hidden: two `CreateAsync` calls in
different processes racing for the same year could compute the same next
number**, since the scan happens before the write rather than inside a
single serialising transaction spanning both. Given a quote is created by
one person at a time from the Workspace, this is accepted rather than
built out — the identical tolerance the platform already has for
identifier uniqueness generally.

**5. `IQuotationService`/`QuotationService`** decides every act before
`Quotation`'s own mutators run, reporting a `QuotationRefusal` rather than
an exception (`InvoicingService`'s own discipline). `CreateAsync` refuses
only `ProjectNotFound`. `AddLineAsync`/`UpdateLineAsync`/`RemoveLineAsync`
refuse `QuotationNotDraft` once Sent, `InvalidLine` when a line is neither
hours-and-a-rate nor a fixed price (or is both), and
`LineCurrencyMismatch` when a supplied rate/fixed price is not in the
quotation's own currency — `Money`'s own refuse-rather-than-convert rule
(`ADR-0130`), applied here explicitly rather than left for `Money.Sum` to
throw later. `SendAsync` refuses `NothingToSend` (no lines) or
`TransitionNotPermitted` (not Draft); records `SentOn`.

**`AcceptAsync` is the one act that orchestrates other services**, and is
**not one atomic transaction** — disclosed here exactly as `ADR-0151` §5
discloses the identical shape for `InvoicingService.SendAsync`:
`EngineeringDomainContext.ExecuteWriteAsync` is `internal` to
`Tempest.Core.EngineeringDomain` and commits one object's own state and
audit row at a time (`ADR-0145`); widening it to a multi-object primitive
is a substrate change outside this Work Package's own files. The order is
therefore: (a) find, or create, a Milestone titled after the quotation's
own `Reference`, targeted `QuoteDate + ValidityDays` — via
`EngineeringObjectFactory<Milestone>` directly, not through
`Tempest.Workspace.Projects.IProjectMilestoneService` (see §6); (b) for
every line, in order, one Deliverable under that milestone and one
Requirement titled from the line, each its own transaction and audit row;
(c) only once every line's own objects exist, one final transaction moves
`Status` to `Accepted`, records `DecidedOn`, and records every created
`DeliverableId`/`RequirementId` on the quotation's own lines together —
**this is what makes a second Accept refused as already accepted**
(`TransitionNotPermitted`, since `Accepted` is terminal), not a second set
of objects. A crash between (b) and (c) is a real, disclosed gap: retrying
Accept in that state re-runs the whole line loop, creating a second
Deliverable per line and throwing `DuplicateRequirementIdentifierException`
on the first re-created Requirement (identifiers are
`"<Reference>-<n>"`, minted once). The kill switch this Work Package's
own brief names is scoped to Requirements association, not this — it is
disclosed in the code's own remarks rather than built out.

`DeclineAsync` refuses `TransitionNotPermitted` off Sent; creates nothing.

**6. Milestone and Deliverable are constructed directly in Core, never
through `IProjectMilestoneService`.** That service lives in
`Tempest.Workspace`, which `Tempest.Core` cannot reference — the
dependency graph runs the other way (`ADR-0023`) — and, separately, it is
wired today only onto the Desktop-only `WorkspaceHost.ProjectMilestoneWorkflow`
property, unreachable from the Core-only host this Work Package's own
journey tests run against. `Milestone`/`Deliverable` are themselves plain
`Tempest.Core.EngineeringDomain` types with no factory of their own
(`Tempest.Workspace.CanonicalObjectKinds`' own remarks: "twenty-one Kinds
... real, compiled, persistable ... with no factory in front of them"), so
`QuotationService.AcceptAsync` builds them the same way
`ProjectMilestoneService.CreateMilestoneAsync`/`CreateDeliverableAsync`
already do — through `EngineeringObjectFactory<T>` directly. The Kind
strings (`"Milestone"`/`"Deliverable"`) are repeated as literals, for the
same reason `Tempest.Core.Deliverables.DeliverableService.AddDeliverableAsync`
(§7) repeats them: their canonical owner,
`Tempest.Workspace.CanonicalObjectKinds`, lives in the Workspace project
too.

**7. `IDeliverableService.AddDeliverableAsync(projectId, title,
targetDate?)`** creates a deliverable directly, under a default milestone
titled `"Unquoted"` (created, the first time, with a target 90 days out
unless `targetDate` is given) — so the Deliverables tab can add one
without going through Timeline or a quotation, per the Product Owner's own
second complaint in comment item 4. It completes and raises an invoice
request exactly as a quoted deliverable does: nothing about how a
deliverable's milestone came to exist is visible to `DeliverableService.CompleteAsync`
or the completion hook (`ADR-0151` §7).

**8. A Requirement is associated to the project by allocation, not by
parenting — no change to `IRequirementsService`.** `Requirement` is a
plain DTO over `IEngineeringDocumentStore`, never an `EngineeringObjectBase`,
so it has no `IHasParent`/`MoveAsync` to call (confirmed: only
`EngineeringObjectBase`-derived types implement `IHasParent`). Per
`Tempest.Workspace.Projects.ProjectRequirementRegister`'s own established
mechanism — already built, unmodified by this Work Package — a
requirement belongs to a project when something it is linked to
(`RequirementRelationshipKinds.AllocatedTo` "above all, but any recorded
reference counts") is an engineering object `ProjectMembership` resolves
into that project. `AcceptAsync` links each created Requirement to the
**quotation itself** (`AllocatedTo`) — the quotation is already parented
to the project at `CreateAsync`, so it is already a project member by the
same walk `ProjectMembership.ResolveOwningProjectAsync` performs for
everything else, and `ProjectRequirementRegister.ListAsync` already finds
a requirement allocated to any project member. **The kill switch named in
this Work Package's own brief — falling back to a title-only association
if `IRequirementsService` would need changing in a way its own tests
forbid — was not needed**: no change to `IRequirementsService` was made or
considered necessary.

**9. `IQuotationService`/`QuotationService` is registered as an ordinary
Core service in `TempestHost.cs`**, alongside `IInvoicingService`, so
`EngineeringWorkspaceComposer.RegisterEngineeringDisciplines` and every
Core-only test host can resolve it identically. This is a deviation from
the Work Package brief's own "files you own" list (`TempestHost.cs` is not
named there) — disclosed as such in this Work Package's own report,
rather than worked around by constructing `QuotationService` only inside
the Workspace composer and leaving Core-only test hosts unable to reach it
the way `InvoicingTestHost`/`ProjectCommercialTestHost` already do for
every sibling service.

**10. Workspace registration follows the `InvoiceRequest` fork, not the
`DeliverableCompletion` one.** A `KindEditorDeclaration` (`Identity`, a new
`EditorSectionKeys.QuotationLines`, `Lifecycle`) renders through the
generic `ObjectEditorView`, not a bespoke Desktop view — the declaration
alone adds no content to the Lines section yet (no `PopulateQuotationAsync`
exists; `ObjectEditorView.cs` is untouched by this Work Package, per its
own brief), exactly as `InvoiceRequest`'s own Lines section rendered
nothing until its own `Populate*` method existed. Five commands
(`quotation.create`, `.add-line`, `.send`, `.accept`, `.decline`),
category **"Quotations"** (excluded from the engineering ribbon by
`WP 19.4A`'s own filter, per the seam map's own risk note). `quotation.create`
reads the shell's own ambient `CommandContext.ProjectId` — never a
selected object — matching the Product Owner's own words ("the quote
should be opened with the project") and `CreationPlacement`'s own
established precedent for "where does a new object without an explicit
destination go"; no project open resolves to `Guid.Empty`, refused by the
service as `ProjectNotFound` rather than thrown. No delete factory, no
rename — a quotation's own display name is derived from its reference,
mirroring `InvoiceRequest`'s own "no delete, only decline/void" precedent.

## Consequences

**Positive:** the spine the Product Owner named — quote → requirements →
deliverables → completion → invoice request — now has its missing first
link; a client's agreement produces real, durable Deliverables and
Requirements rather than a person re-typing both by hand; the Deliverables
tab can add a deliverable with no quote at all, closing the other half of
comment item 4; every act is a refusal result, never an exception, so a
caller (a command handler, a test, a future `WP 19.5B` UI) never writes a
`try`/`catch` around an ordinary business rule.

**Negative:** `AcceptAsync` is not atomic — a crash mid-loop leaves a live,
partially-fulfilled Sent quotation whose retry throws rather than resumes
cleanly (§5); the reference counter has a narrow, disclosed race window
under concurrent creation (§4); a Requirement's own association to its
quotation is a relationship link to the quotation object, not a typed
`SourceQuoteLineId` field — traceable, but one hop further to query than a
direct field would be, the same tradeoff `RequirementRelationshipKinds`
already accepts everywhere else in this discipline.

**Out of scope for `v0.19.1`:** no revision of an accepted quotation — a
change is a new quotation; no quote export (PDF) — the Product Owner's own
answer (comment item 9) scopes that to `WP 19.5B`, through the same
SkiaSharp path as the issue sheet, over exactly the fields this Kind
already carries (reference, date, client, lines with hours and price,
totals, terms); no Desktop surface — no rail entry, no "Add Deliverable"
button on the Deliverables tab, no quote-at-project-creation prompt, all
`WP 19.5B`'s own scope, per the brief's explicit "nothing under
`src/Tempest.Desktop`."

## Related Documents

`D-028`; `ADR-0145` (one object, one transaction); `ADR-0150` (the
project's own commercial core, read here for client and rate card);
`ADR-0151` (`InvoiceRequest`, the shape repeated again, and the identical
non-atomic-orchestration disclosure this ADR's §5 mirrors); `ADR-0134`
(the frozen `CustomerQuotation` this Kind is deliberately not);
`WorkPackages.md` (`WP 19.5A` row); Product Owner comment item 4
(2026-09-14) and item 9 (the export decision `WP 19.5B` implements).
