# Parallel Programme D — Business OS

**Part of** [Parallel Work Programme A–G](Parallel%20Work%20Programme%20A–G.md).
**Position in recommended order:** concurrent — run wherever capacity exists.
**Status of every sub-package below:** Defined, not started (2026-09-05).
**Claude Code required for this programme:** No.

## Programme Purpose

The structures the business runs on: how clients, projects, money,
purchasing, quality and records are organised. Structure first — the
fields, states and rules — independent of whichever tool eventually
holds them.

**Standing rule for this programme:** define the *structure*, not the
tool. A structure defined properly can be implemented in a spreadsheet
this week and in TempestOS later. A structure defined as "how our
current tool happens to work" cannot be moved.

**Note on integration.** Programme D is the programme least likely to
become product content wholesale. Some of it (projects, documents,
quality records) maps onto TempestOS naturally; some of it (CRM,
finance) is ordinary business software the product need not become.
Each sub-package says which, honestly.

---

## D.1 — CRM Structure

**1. Purpose.** Know who the clients and prospects are, what has been
discussed, and what happens next — without relying on memory or an
inbox.

**2. Scope.** *In:* company and contact records; the enquiry-to-order
pipeline stages; interaction history; follow-up actions; the link from
enquiry to quotation to project. *Out:* marketing automation, and
selecting a CRM product.

**3. Required inputs.** Existing client list; the real stages an enquiry
passes through today (observed, not idealised).

**4. Data / content fields.** Company: `ClientID`; `Name`; `Sector`;
`Address`; `Website`; `Status` (Prospect / Active / Dormant / Former);
`Source`; `Owner`; `Payment Terms`; `Credit Limit`; `Notes`.
Contact: `ContactID`; `ClientID`; `Name`; `Role`; `Email`; `Phone`;
`Preferred Contact Method`; `Decision Authority`.
Opportunity: `OpportunityID`; `ClientID`; `Description`; `Stage`;
`Value`; `Probability`; `Expected Close`; `Quote Reference`;
`Next Action`; `Next Action Date`; `Outcome`; `Outcome Reason`.
Interaction: `InteractionID`; `Date`; `Type`; `Summary`; `Follow-up`.

**5. Outputs / artefacts.** `CRM Structure.md`; `Client Register.csv`;
`Opportunity Pipeline.csv`; a pipeline stage definition table.

**6. Acceptance criteria.** Every pipeline stage has an entry criterion
and an exit criterion — stages without both are decoration. Every
opportunity has a next action with a date, or an outcome. No record
depends on a specific software product.

**7. Dependencies.** Feeds `G.6`. Uses `C.4` quotation references.

**8. Recommended next action.** Write the pipeline stage definitions —
six stages at most, each with entry and exit criteria.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Partly.** Client and project linkage is
plausible; a full CRM is not a stated product ambition. Treat as
business tooling unless a product decision says otherwise.

---

## D.2 — Project Management Structure

**1. Purpose.** Run every job the same way: defined phases, defined
deliverables, defined states, visible status.

**2. Scope.** *In:* project record structure; phases and gates;
deliverable and milestone definitions; time and cost tracking
structure; change control; project closure. *Out:* resource-levelling
algorithms and scheduling software selection.

**3. Required inputs.** Real past project histories; `C.4` (quotation
scope becomes project scope); `D.3` (cost coding).

**4. Data / content fields.** Project: `ProjectID`; `ClientID`;
`Title`; `Quote Reference`; `Scope Statement`; `Start Date`;
`Target Completion`; `Actual Completion`; `Phase`;
`Status` (Not Started / Active / On Hold / Complete / Cancelled);
`Budget`; `Committed`; `Spent`; `Owner`; `Risk Level`.
Deliverable: `DeliverableID`; `ProjectID`; `Description`; `Type`;
`Due Date`; `Status`; `Accepted By`; `Acceptance Date`.
Change: `ChangeID`; `ProjectID`; `Description`; `Reason`;
`Cost Impact`; `Time Impact`; `Approved By`; `Date`.

**5. Outputs / artefacts.** `Project Management Structure.md`;
`Project Register.csv`; `Project Phase & Gate Definitions.md`;
`Change Request Template.md`; a project closure checklist.

**6. Acceptance criteria.** Every phase gate names the evidence required
to pass it. Every deliverable has an acceptance criterion and a named
acceptor. Scope changes cannot be recorded without a cost and time
impact — even if that impact is explicitly zero.

**7. Dependencies.** `C.4`; `D.3`; `B.4`/`E.4` for design gates.

**8. Recommended next action.** Define the phases and gates for one real
project type end to end.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes, later** — TempestOS already
carries project tasks and a Product Spine (`v0.14.0`); this structure is
the closest fit in Programme D to real product content.

---

## D.3 — Finance Structure

**1. Purpose.** Know what money is committed, spent and owed, per job
and in total, without waiting for the accountant.

**2. Scope.** *In:* chart-of-accounts structure; job costing; invoice
and payment records; expense categories; cash-flow record structure;
VAT/tax treatment fields. *Out:* statutory accounting, tax advice, and
anything requiring an accountant's judgement — those are recorded as
**Unknown, requires professional input**, not guessed.

**3. Required inputs.** Existing accounting arrangements; `G.5`
(financial controls); professional advice for anything statutory.

**4. Data / content fields.** Account: `AccountCode`; `Name`; `Type`
(Income / Direct Cost / Overhead / Asset / Liability); `Notes`.
Transaction: `TransactionID`; `Date`; `Type`; `AccountCode`;
`ProjectID`; `Supplier/Client`; `Description`; `Net`; `Tax`; `Gross`;
`Currency`; `Status` (Committed / Invoiced / Paid); `Due Date`;
`Reference`. Job cost: `ProjectID`; `Quoted`; `Material Actual`;
`Subcontract Actual`; `Labour Hours`; `Labour Cost`; `Overhead
Applied`; `Total Actual`; `Margin`; `Margin %`.

**5. Outputs / artefacts.** `Finance Structure.md`; `Chart of
Accounts.csv`; `Job Costing Template.xlsx`; a cash-flow record
structure.

**6. Acceptance criteria.** Every project can be costed from the
structure alone. Committed, invoiced and paid are distinguished
throughout. Anything requiring professional accounting judgement is
flagged as such rather than answered.

**7. Dependencies.** `G.4`, `G.5`; `C.2` for cost basis.

**8. Recommended next action.** Build the job costing template and run
one completed real job through it, comparing quoted to actual.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Unlikely as product content.** Job
costing linked to projects is plausible; general accounting is not a
stated product ambition. Recorded honestly rather than aspirationally.

---

## D.4 — Supplier & Purchasing Operations

**1. Purpose.** Turn a decision to buy into a controlled, traceable
transaction.

**2. Scope.** *In:* the purchase requisition and order process; PO
numbering and content; goods-receipt and inspection records; invoice
matching; supplier non-conformance handling. *Out:* supplier selection
(`C.5`) and supplier capability data (`C.1`).

**3. Required inputs.** `C.1`; `C.5`; `G.5` (authority limits);
existing purchase records.

**4. Data / content fields.** PO: `PONumber`; `SupplierID`;
`ProjectID`; `Date`; `Line Items` (description, specification,
quantity, unit price, total); `Delivery Address`; `Required Date`;
`Promised Date`; `Terms`; `Raised By`; `Approved By`;
`Status` (Draft / Issued / Part Received / Received / Closed /
Cancelled); `Total Value`.
Receipt: `ReceiptID`; `PONumber`; `Date`; `Quantity Received`;
`Inspection Result`; `Non-conformance Reference`; `Received By`.

**5. Outputs / artefacts.** `Purchasing Process.md`; `PO Template`;
`Goods Receipt & Inspection Record Template`; `Supplier
Non-conformance Report Template`.

**6. Acceptance criteria.** Every PO traces to a project or an overhead
account. Approval authority matches `G.5` exactly. No goods can be
recorded as received without an inspection result — including
"inspection not required", stated deliberately.

**7. Dependencies.** `C.1`, `C.5`, `G.5`, `D.5`.

**8. Recommended next action.** Define PO numbering and the approval
authority table. Both are one-line decisions with long consequences.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Partly** — goods-receipt inspection
links naturally to quality records; purchasing generally is business
tooling.

---

## D.5 — Quality Management

**1. Purpose.** Make quality a recorded system rather than an assumed
standard of care — and make ISO 9001 certification a later formality
rather than a project.

**2. Scope.** *In:* the quality policy; process map; document and record
control; non-conformance and corrective action; inspection records;
calibration; internal audit; management review. *Out:* pursuing
certification itself, and any claim of conformance not yet true.

**3. Required inputs.** ISO 9001 clause structure (as a framework, not
reproduced); real process descriptions; existing inspection practice.

**4. Data / content fields.** Non-conformance: `NCRID`; `Date`;
`Source` (internal / supplier / client); `ProjectID`; `Description`;
`Immediate Action`; `Root Cause`; `Corrective Action`;
`Preventive Action`; `Owner`; `Due Date`; `Verification`;
`Closed Date`; `Cost of Failure`.
Inspection: `InspectionID`; `Item`; `Drawing/Revision`;
`Characteristics Checked`; `Method`; `Equipment`; `Result`;
`Inspector`; `Date`.
Calibration: `EquipmentID`; `Description`; `Serial`; `Range`;
`Accuracy`; `Calibration Date`; `Due Date`; `Certificate Reference`.

**5. Outputs / artefacts.** `Quality Manual.md`; `Process Map.md`;
`NCR Register.csv` and template; `Inspection Record Template`;
`Calibration Register.csv`; `Internal Audit Schedule & Checklist.md`.

**6. Acceptance criteria.** Every non-conformance record requires a root
cause before it can be closed — a corrective action without a root cause
is not accepted. The process map covers every process actually run.
Nothing claims conformance to a clause not actually met.

**7. Dependencies.** `D.2`, `D.4`, `D.6`; `B.4` for design review
records.

**8. Recommended next action.** Start the NCR register now, even empty,
and record the next real problem in it. A register that starts with a
real entry survives; one that starts with a template rarely does.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes, later** — quality records,
inspection and non-conformance align closely with the product's
verification and durability capabilities (`v0.14.0`).

---

## D.6 — Document & Records Management

**1. Purpose.** Make every document findable, current and correctly
versioned — and make it obvious which copy is authoritative.

**2. Scope.** *In:* document numbering and naming; revision control;
approval and issue states; retention periods; storage structure; the
distinction between a controlled document and a record. *Out:*
selecting a document management product, and migrating history.

**3. Required inputs.** Existing folder structures and their real
failure modes; `G.3` (retention and data protection); `A.2` for drawing
standards.

**4. Data / content fields.** `DocumentID`; `Title`; `Type` (drawing /
specification / calculation / report / record / template);
`ProjectID`; `Revision`; `Revision Date`; `Status` (Draft / For Review
/ Approved / Issued / Superseded / Obsolete); `Author`;
`Checked By`; `Approved By`; `Issue Date`; `Supersedes`;
`Retention Period`; `Storage Location`; `Confidentiality`;
`Distribution`.

**5. Outputs / artefacts.** `Document Control Procedure.md`;
`Document Numbering Convention.md`; `Document Register.csv`;
`Retention Schedule.md`; a folder structure specification.

**6. Acceptance criteria.** The numbering convention is unambiguous and
sortable, and produces a unique identifier without a central lookup.
Every document type has a stated retention period. Superseded documents
are identifiable as such wherever they are stored.

**7. Dependencies.** `G.3` (retention/legal); `D.5` (record control);
`A.2`.

**8. Recommended next action.** Fix the numbering convention before
another document is created. Retrospective renumbering is the expensive
alternative.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes** — TempestOS ships an Engineering
Documents Workspace (`WP 9.4A`) with durable objects and attachments
(`v0.14.0`); this convention is exactly the kind of content that
workspace would carry.
