# Parallel Programme E — Engineering Assets

**Part of** [Parallel Work Programme A–G](Parallel%20Work%20Programme%20A–G.md).
**Position in recommended order:** 4th (after A, G, C).
**Status of every sub-package below:** Defined, not started (2026-09-05).
**Claude Code required for this programme:** No.

## Programme Purpose

The reusable engineering artefacts a client actually receives:
templates, calculation packs, verification evidence, review packs and
the documentation system that holds them together. This is the first
programme whose output is visible outside the business.

**Standing rule for this programme:** every template is proved by being
used once, on real work, before it is declared complete. A template that
has never been filled in is a draft, whatever its status field says.

---

## E.1 — Engineering Templates

**1. Purpose.** Stop re-creating the same documents, and make every
issued document recognisably from the same organisation.

**2. Scope.** *In:* drawing title blocks and sheet formats; technical
specification; design report; calculation sheet; test procedure and
report; inspection sheet; interface control document; issue and revision
blocks; the house style (fonts, units, numbering, tolerancing note,
standard notes). *Out:* CAD template files themselves where they depend
on a specific CAD system — those are recorded as a dependency, not
produced here.

**3. Required inputs.** `A.2` (standards for drawing practice); `D.6`
(numbering and revision convention); existing documents worth
standardising; brand assets.

**4. Data / content fields.** Per template: `TemplateID`; `Name`;
`Purpose`; `Applies To`; `Sections`; `Mandatory Fields`;
`Optional Fields`; `Standard Notes`; `Approval Fields`;
`Revision Block`; `Format`; `Owner`; `Version`; `Last Reviewed`.

**5. Outputs / artefacts.** A template set (one file per template);
`Engineering Template Index.md`; `House Style Guide.md` (units,
numbering, tolerancing default, standard notes, terminology).

**6. Acceptance criteria.** Every template carries the numbering,
revision and approval fields `D.6` defines. Each template has been used
once on real work before being marked Complete. The style guide fixes
units and tolerancing defaults explicitly, matching `A.6`.

**7. Dependencies.** `A.2`, `A.6`, `D.6`. Feeds `E.2`–`E.5`.

**8. Recommended next action.** Produce the title block and the
specification template first — they appear on nearly everything issued.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes** — document templates map directly
onto the Engineering Documents Workspace (`WP 9.4A`).

---

## E.2 — Calculation Packs

**1. Purpose.** Make recurring engineering calculations reusable,
checkable and consistent — the same method, every time, with the
assumptions visible.

**2. Scope.** *In:* the calculations actually repeated — bolted joints,
beam bending and deflection, shaft sizing, bearing life, weld sizing,
spring rate, gear geometry, lifting and pressure checks where relevant,
tolerance stack-up, thermal expansion. *Out:* finite element analysis
and anything requiring a specialist tool.

**3. Required inputs.** `A.6` (constants and formulae); `A.1`, `A.3`,
`A.4` (data); `B.3` (design rules and safety factors); `E.1`
(calculation sheet template).

**4. Data / content fields.** Per pack: `CalcID`; `Title`; `Purpose`;
`Method Source` (standard or textbook, cited); `Inputs` (symbol, unit,
typical range, source); `Assumptions`; `Formulae`; `Outputs`;
`Acceptance Criterion`; `Safety Factor Applied` and its basis;
`Validity Limits`; `Worked Example`; `Checked By`; `Version`.

**5. Outputs / artefacts.** One calculation pack per topic (spreadsheet
or document, both acceptable); `Calculation Pack Index.md`;
`Calculation Standard.md` (how a calculation is presented, checked and
signed).

**6. Acceptance criteria.** Every pack cites its method source and
states its validity limits. Every pack includes one worked example whose
result has been independently checked by hand. No pack silently applies
a safety factor — the factor and its basis appear on the sheet.

**7. Dependencies.** `A.6` (hard), `A.1`/`A.3`/`A.4`, `B.3`, `E.1`.

**8. Recommended next action.** Build the bolted joint pack first: most
used, most frequently got wrong, and it exercises `A.3` immediately.

**9. Claude Code required?** **No.** Spreadsheets and documents are the
right medium; the calculations are hand-checkable by design.

**10. TempestOS integration.** **Yes** — TempestOS ships a Calculation
framework (`FCR-0032`) and an Engineering Calculations Workspace
(`WP 9.2A`). These packs are the strongest candidate content in the
whole parallel programme for later import.

---

## E.3 — Verification Artefacts

**1. Purpose.** Prove a design meets its requirements, with evidence
someone else can audit.

**2. Scope.** *In:* the requirements-to-verification traceability
structure; verification method selection (inspection, analysis,
demonstration, test); test procedures and reports; acceptance criteria;
evidence records; the verification matrix. *Out:* physical testing
capability itself.

**3. Required inputs.** `B.4` (review logic); `E.1` (templates);
`E.2` (calculations, as one verification method); the requirements
structure of a real project.

**4. Data / content fields.** Verification: `VerificationID`;
`RequirementID`; `Requirement Statement`; `Method` (I/A/D/T);
`Acceptance Criterion`; `Procedure Reference`; `Equipment`;
`Responsible`; `Planned Date`; `Result`; `Evidence Reference`;
`Status` (Not Started / In Progress / Passed / Failed / Waived);
`Waiver Reference`; `Verified By`; `Date`.

**5. Outputs / artefacts.** `Verification Matrix Template.csv`;
`Test Procedure Template.md`; `Test Report Template.md`;
`Verification Standard.md` (how a requirement is verified and evidenced).

**6. Acceptance criteria.** Every requirement in the matrix has exactly
one primary verification method and a stated acceptance criterion. No
verification is recorded as passed without an evidence reference. Waived
verifications require a recorded waiver with an approver — never a blank.

**7. Dependencies.** `B.4`, `E.1`, `E.2`.

**8. Recommended next action.** Build the verification matrix for one
real past project retrospectively; the gaps it reveals are the real
lesson.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes** — TempestOS ships Requirements
(`WP 9.1A`) and Verification Management (`WP 9.3A`) workspaces and a
Verification framework (`FCR-0033`). Strong later import candidate.

---

## E.4 — Design Review Packs

**1. Purpose.** Make every design review consistent, evidenced and
recorded, using the logic `B.4` defines.

**2. Scope.** *In:* the review pack contents by review type; the agenda;
the entry criteria (what must exist before a review is convened); the
finding record; the disposition and close-out process; the review
minutes. *Out:* the checklists' own content, which is `B.4`.

**3. Required inputs.** `B.4` (hard — the checks); `E.1`; `E.3`; `D.2`
(gates).

**4. Data / content fields.** Review: `ReviewID`; `ProjectID`;
`Review Type`; `Date`; `Attendees and Roles`; `Documents Reviewed with
Revisions`; `Entry Criteria Met?`; `Outcome` (Approved / Approved with
Actions / Not Approved); `Next Review`.
Finding: `FindingID`; `ReviewID`; `Description`; `Severity`;
`Category`; `Raised By`; `Owner`; `Agreed Action`; `Due Date`;
`Evidence of Close-out`; `Closed Date`; `Verified By`.

**5. Outputs / artefacts.** `Design Review Pack Template` (one per
review type); `Review Finding Register.csv`;
`Design Review Procedure.md`.

**6. Acceptance criteria.** No review can be recorded as Approved with
open Blocking findings. Every finding has an owner and a due date at the
moment it is raised. The document revisions reviewed are recorded
exactly — a review of "the drawings" without revisions is not evidence.

**7. Dependencies.** `B.4`, `E.1`, `E.3`, `D.2`.

**8. Recommended next action.** Define the entry criteria for each
review type. Reviews fail most often because they were convened too
early.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes, later** — review records fit the
verification and project structures the product already carries.

---

## E.5 — Technical Documentation System

**1. Purpose.** Define what documentation a delivered project comprises,
and make producing it a defined task rather than an afterthought.

**2. Scope.** *In:* the document deliverable set by project type; the
data pack contents; operating and maintenance manual structure;
as-built documentation; handover and acceptance records; the
documentation plan. *Out:* technical authoring of any specific manual.

**3. Required inputs.** `D.6` (control); `E.1` (templates); `E.3`
(verification evidence); `D.2` (project phases); client contractual
requirements from `G.1`.

**4. Data / content fields.** `DeliverableID`; `Project Type`;
`Document Type`; `Mandatory?`; `Contractual Basis`; `Owner`;
`Produced At Phase`; `Approval Required`; `Format`; `Retention`;
`Client Distribution`; `Template Reference`.

**5. Outputs / artefacts.** `Technical Documentation System.md`;
`Project Data Pack Contents.md` (per project type);
`O&M Manual Structure.md`; `Handover & Acceptance Record Template`.

**6. Acceptance criteria.** Every deliverable states whether it is
contractual and, if so, cites the term requiring it. Each names the
project phase that produces it — documentation with no owning phase does
not get produced. The set is complete enough that a project handover can
be assembled from this list alone.

**7. Dependencies.** `D.2`, `D.6`, `E.1`, `E.3`, `G.1`.

**8. Recommended next action.** Write the data pack contents list for
the most common project type, and check it against the last real
handover.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes, later** — the deliverable set maps
onto project and document structures already in the product.
