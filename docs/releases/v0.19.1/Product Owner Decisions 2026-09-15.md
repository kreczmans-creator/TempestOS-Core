# Product Owner decisions — 2026-09-15 (early hours)

Answers to the seven questions the technical-debt rationalisation left
open, given while the overnight tranche (`WP 19.10A`–`19.10R`) was
running. Each is recorded as said and then as the engineering
consequence the next tranche takes from it.

## 1. Payment terms (`TD-180`)

**Said:** initially up front; there needs to be a per-client basis; a
drop-down with *Up front / 30 days / 60 days*.

**Consequence:** `Organisation` (the client) gains a payment-terms
field with that closed vocabulary, defaulting to *Up front*; a new
`InvoiceRequest` copies the client's terms at raise time (frozen, as
rates are) so a later change to the client does not move an issued
request's due date; the Finance bucket and the Business dashboard's
receivable list read the request's own terms instead of the thirty-day
heuristic; the Commercial section shows the drop-down. Package: the
commercial data model (`WP 20.1B`).

## 2. When a calculation is a task (`TD-181`)

**Said:** on creation it is a task — a calculation identified as a
deliverable, or as an evidence piece in a review package, needs doing
for a project to progress; it can be opened and completed by the same
person.

**Consequence:** every Calculation created under a project appears in
a Calculations bucket of the tasks read model from creation, with its
own Complete action (the same person may complete it); it leaves the
bucket on completion or when evidence citing it is issued, whichever
first; Engineering → Tasks and the Engineering dashboard gain the
Calculations heading the sketch showed. Open question for the package:
whether a calculation created outside any project is a task (proposed:
no — a task belongs to a project). Package: `WP 20.1B`.

## 3. The three dropped menu entries (View toggles, layout presets, About)

**Said:** to be confirmed once the application is open in front of the
Product Owner.

**Consequence:** stays a Warning in the v0.19.1 release notes; asked
again at the v0.19.1 acceptance.

## 4. QuickBooks Online (`B5`)

**Said:** only Xero for this release; QuickBooks can be added later.

**Consequence:** the QuickBooks Online item-reference gap is recorded
and deferred (P4 for v1.0.0); the connector stays in the code, selectable
in Settings, with its documented one-setting workaround; no v1.0.0 work
package carries it. The security posture statement (`WP RC.0C`) names
Xero as the supported live connector.

## 5. DWG preview (`TD-99`)

**Said:** if a licensed SDK is designed in and shipped, the licensing
must say so explicitly; if it is easier to keep DWG as a stored
attachment opened externally, that is also fine.

**Consequence:** for v1.0.0 a DWG stays a stored attachment with an
*Open externally* action (the registered application, Adobe or a CAD
viewer); the viewer says so honestly rather than "Unsupported"; SVG gets
an in-app page source (small). A licensed DWG renderer is a separate
decision with its licence text in the posture statement if ever taken.
Package: the viewer (`WP 20.2B`).

## 6. Engineering Assets surfaces (`TD-165`, `TD-160`)

**Said:** decide when the release candidate lands; engineering
capability from the start is wanted but not at the cost of the
schedule.

**Consequence:** not scheduled before the RC packages; re-raised with an
estimate when `WP RC.0A` starts, so the Product Owner decides with the
schedule in view.

## 7. The design-system templates and company facts

**Said:** the templates folder goes into `docs/design/templates/`
tomorrow at the computer; company facts if any differ from the
templates' footer.

**Consequence:** the document-templates package (`WP 20.0B`) starts
once the folder is committed; its brief reads the footer facts from the
templates themselves.
