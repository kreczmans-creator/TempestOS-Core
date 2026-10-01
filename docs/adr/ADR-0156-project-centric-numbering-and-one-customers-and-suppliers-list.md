# ADR-0156: Project-Centric Numbering and One Customers & Suppliers List

## Status

Accepted — Product Owner decision 2026-10-01.

Builds on `ADR-0142` (the `P04` Organisation and Contact catalogues),
`ADR-0150` (a project's client is an Organisation record id) and
`ADR-0152` (a quotation is opened with the project).

## Context

The Product Owner made three decisions on 2026-10-01:

1. **People and client contacts are different things.** The
   consultancy's own staff (Libraries → People, `Person`) need contact
   details only. A client is an organisation with company details and its
   own list of contacts.
2. **Customers and suppliers share one organisation model** and one level
   of detail, told apart by a type (Customer / Supplier / Both), kept in
   one list. Wherever an organisation is chosen it is picked from that
   list, never typed.
3. **Numbering is project-centric**, like document numbering:
   `CUSTOMER-PROJECTREF-DOCTYPE-NNN`, for example `ACME1-BRIDG1-Q-001`.

Until now a project was `P-NNNN`, a quotation `Q-<year>-NNN` (one
sequence for the whole store), a change order `CO-<year>-NNN`, a purchase
order `PO-<year>-NNN`, and an invoice request or a document carried no
number at all.

## Decision

**1. No new organisation model.** `Organisation` (`ADR-0142`) already
held the legal name, address, company number (`RegistrationNumber`), VAT
number (`TaxRegistration`), website and a `Roles` list; it gains
`CustomerCode`, `TelephoneNumber` and `EmailAddress`. The type is
`Organisation.TradingType`, *read from* `Roles` (`Customer`/`Prospect` →
Customer, `Supplier` → Supplier, both → Both; none → Customer, since every
older record was registered as a client) and written with
`Organisation.RolesFor`. Contacts are the existing `Contact` catalogue,
linked by `OrganisationReference`. `Person` gains `Phone` and nothing
else.

**2. One list, one editor.** Business → Customers & Suppliers
(`CustomersSuppliersView`) lists, adds and edits organisations and their
contacts. New Project → Client (`NewProjectPrompt`) is a drop-down of the
same catalogue — customers and organisations that are both — with
"Add organisation…" kept as a shortcut into the same list
(`OrganisationPicker`, which now always gives the new record a customer
code). A purchase order's supplier is chosen through
`OrganisationPicker.PickSupplierAsync` (suppliers and both). Quotations
and invoice requests take their client from the project, so they need no
picker of their own.

**3. Codes.** A *customer code* (on the organisation) is exactly five
characters and a *project reference* (per project) exactly six, each an
upper-case letter A–Z or a digit 0–9 (amended by runbook feedback C1:
"doesn't allow numbers in the reference … also make the project ID 6
letters long, not 5"). Both are suggested from the name
(`ProjectNumbering.SuggestCustomerCode` / `SuggestProjectReference`: the
first five or six letters and digits of the name, padded with `X`; on a
clash the last character, then the last two, step through A–Z then 0–9),
editable, upper-cased and unique — customer codes across the
organisation library (`IOrganisationCatalog.FindByCustomerCodeAsync`),
project references across every project-centric project identifier.

**4. The project identifier is `CUSTOMER-PROJECTREF`** (for example
`ACME1-BRIDG1`) when the chosen client has a customer code. Every generated number inside the project is
`<project identifier>-<DOCTYPE>-<NNN>`: one past the highest suffix any
record of that type (live or not) already uses under that prefix, so a
sequence per project per document type, starting at 001 — the first
quote in every project is 001. The prefix is read from the project's own
identifier, frozen at creation, never re-derived from the client's
current code: changing a customer code later renumbers nothing.

**5. The DOCTYPE table** is defined once, in
`Tempest.Core.Projects.ProjectNumbering.DocumentTypes`:

| Code | Record | Where generated |
|---|---|---|
| `Q` | Quotation | `QuotationService.CreateAsync` |
| `CO` | Change order | `QuotationService.CreateAsync` (`QuotationKind.ChangeOrder`) |
| `PO` | Purchase order | `PurchaseOrderService.CreateAsync` |
| `INV` | Invoice request | `InvoicingService` (the request's `Identifier`) |
| `DOC` | Document | `CreateDocumentObjectCommandHandler` |
| `DWG` | Drawing | `CreateDocumentObjectCommandHandler` |
| `CAD` | CAD model | `CreateDocumentObjectCommandHandler` |
| `CALC` | Calculation | reserved — calculations are not numbered by the platform yet |

Codes are never reassigned. A number given explicitly (a typed quotation
reference, a document identifier) is always kept as given.

**6. No migration; the old scheme is the fallback.** Existing projects
and records keep their identifiers. A project whose identifier is not of
the `XXXXX-XXXXXX` shape (five then six characters A–Z or 0–9) — every
project created before this decision,
and any new project created with no client or with a client that has no
customer code yet — gets `P-NNNN`, and inside it quotations, change
orders and purchase orders keep `Q-`/`CO-`/`PO-<year>-NNN`; invoice
requests and documents stay unnumbered. Records stored before the new
fields existed deserialise with them `null` (they are optional
`init` properties on the persisted JSON). The all-letter `AAAAA-BBBBB`
identifiers the first build of this decision issued (a five-letter
project reference, before runbook feedback C1) are still recognised as
project-centric (`ProjectNumbering.TryParseProjectIdentifier`), so
numbering keeps working inside projects created with them.

**7. Project folders are named on the codes** (runbook feedback C6:
"make the company folder just the 5 letter ID. make the project just the
project ID"). `ProjectFolderService` files a project under
`<root>\<customer code>\<project identifier>` — for example
`D:\01 Projects\ACME1\ACME1-BRIDG1`. A customer with no code is filed
under its sanitised name, a project with no customer under
`_No customer`. Existing folders are reused, never duplicated: a folder
equal to the code (or identifier), ignoring case, or one starting with it
followed by a space — the `<CODE> <Name>` and `<identifier> <project
name>` forms the first build created.

## Consequences

- Project-scoped internal registers (`RSK-`, `ISS-`, `DEC-`, `TSK-`,
  `MS-`, `DEL-NNN`) are already per project and are not documents; they
  are unchanged.
- Evidence issue references are typed by the engineer at Issue; they are
  not generated and so are unchanged.
- Project reference uniqueness is global (across customers), the
  simplest reading of "unique"; relaxing it to per-customer would only
  change `ProjectNumbering.ProjectReferencesIn`'s caller.
