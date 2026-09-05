# Parallel Programme C — Commercial Intelligence

**Part of** [Parallel Work Programme A–G](Parallel%20Work%20Programme%20A–G.md).
**Position in recommended order:** 3rd (after A and G).
**Status of every sub-package below:** Defined, not started (2026-09-05).
**Claude Code required for this programme:** No.

## Programme Purpose

What things actually cost, who actually supplies them, how long they
actually take, and how that becomes a quotation. This is the programme
that keeps engineering decisions connected to commercial reality.

**Standing rule for this programme:** every cost and lead-time figure
records **where it came from and when**. A price without a date is not
data. Figures derived from real quotations are marked `Actual`; figures
estimated are marked `Estimated`; anything else is `Unknown`.

---

## C.1 — Supplier Database

**1. Purpose.** One record of who can make or supply what, how well they
performed, and how to engage them.

**2. Scope.** *In:* suppliers and subcontractors actually used or
seriously evaluated — capability, capacity, commercial terms, contacts,
approval status, performance history. *Out:* speculative directory
scraping, and any performance claim not backed by a real order.

**3. Required inputs.** Purchase history; existing contact records;
supplier capability statements and certifications.

**4. Data / content fields.** `SupplierID`; `Name`; `Type`
(manufacturer / stockist / subcontractor / service); `Capabilities`;
`Processes` (linked to `A.7`); `Materials Handled`; `Size Envelope`;
`Certifications` (ISO 9001, AS9100, …) with expiry; `Location`;
`Lead Time Typical (days)`; `Minimum Order`; `Payment Terms`;
`Currency`; `Approval Status` (Approved / Conditional / Not Approved /
Unassessed); `Approval Date`; `Contact Name`; `Contact Details`;
`Quality Performance`; `Delivery Performance`; `Price Competitiveness`;
`Jobs Placed`; `Last Used`; `Issues/Notes`; `Source`; `Confidence`.

**5. Outputs / artefacts.** `Supplier Database.csv`; `Supplier Approval
Criteria.md`; a supplier scorecard template.

**6. Acceptance criteria.** Every supplier has an approval status and a
date. Every performance rating cites the number of real orders behind
it — an unsupported rating is `Unassessed`, not a guess. Certification
expiry dates are recorded where held.

**7. Dependencies.** `A.7` (process vocabulary). Feeds `C.3`, `C.5`,
`D.4`.

**8. Recommended next action.** Enter every supplier used in the last
twelve months, from purchase records. That is the real supply base.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes, later** — supplier and purchasing
data is a plausible future module, not a current one.

---

## C.2 — Process & Cost Library

**1. Purpose.** Put a defensible cost against each manufacturing process
so estimates stop being invented per enquiry.

**2. Scope.** *In:* machine and labour rates; setup costs; run rates;
material cost bases and multipliers; finishing and treatment costs;
quantity break effects; tooling amortisation; scrap and yield
allowances. *Out:* client-specific pricing (`G.4`) and margin policy
(`G.4`).

**3. Required inputs.** `A.7`; real quotations received and issued;
`C.1` for supplier-specific rates; a stated currency and date basis.

**4. Data / content fields.** `CostItemID`; `Process`; `Sub-process`;
`Cost Basis` (per hour / per part / per kg / per setup);
`Rate`; `Currency`; `Setup Cost`; `Setup Time`; `Cycle Time Basis`;
`Material Multiplier`; `Minimum Charge`; `Quantity Breaks`;
`Tooling Cost`; `Tooling Life`; `Scrap Allowance (%)`;
`Valid From`; `Valid To`; `Source` (real quote reference / supplier
rate card / estimate); `Confidence` (Actual / Estimated / Unknown);
`Notes`.

**5. Outputs / artefacts.** `Process Cost Library.csv`;
`Cost Estimating Basis.md` (the assumptions, stated once);
`Quantity Break Analysis.csv`.

**6. Acceptance criteria.** Every rate carries a date, a currency and a
source classification. No figure is presented as `Actual` without a
traceable quotation reference. The library reproduces the price of two
recent real jobs within a stated tolerance — and the tolerance is
recorded, whatever it turns out to be.

**7. Dependencies.** `A.7` (hard); `C.1`; `G.4` (rates).

**8. Recommended next action.** Back-calculate rates from three recent
real quotations before adding any published or assumed rate.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes, later** — cost data behind
estimating features. Commercially sensitive; treat access accordingly
(`G.3`).

---

## C.3 — Lead-Time Intelligence

**1. Purpose.** Make delivery promises from evidence rather than
optimism.

**2. Scope.** *In:* typical and worst-case lead times by process,
material, supplier and quantity; material availability; seasonal and
capacity effects; the difference between quoted and achieved lead time.
*Out:* project scheduling itself (`D.2`).

**3. Required inputs.** `C.1`; purchase order history with promised and
actual dates; `A.7`.

**4. Data / content fields.** `LeadTimeID`; `Item/Process`; `Supplier`;
`Quantity Range`; `Quoted Lead Time (days)`;
`Achieved Lead Time (days)`; `Variance`; `Sample Size`;
`Best Case`; `Worst Case`; `Expedite Possible?`; `Expedite Premium`;
`Material Availability Risk`; `Seasonal Factor`;
`Observation Period`; `Source`; `Confidence`.

**5. Outputs / artefacts.** `Lead Time Intelligence.csv`;
`Lead Time Planning Guide.md` (the buffers to apply, and why);
a quoted-versus-achieved variance summary.

**6. Acceptance criteria.** Every lead time states a sample size and an
observation period. Quoted and achieved are recorded separately and
never merged. Recommended planning buffers are derived from the recorded
variance, not chosen.

**7. Dependencies.** `C.1` (hard); `A.7`.

**8. Recommended next action.** Extract promised-versus-actual dates
from the last twenty purchase orders. The variance is usually the most
useful number in this whole programme.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes, later** — planning and procurement
support.

---

## C.4 — Quote / Estimate Structure

**1. Purpose.** Fix one structure for every quotation and estimate, so
they are comparable, auditable and fast to produce.

**2. Scope.** *In:* the quotation document structure; the estimate
build-up (material, process, finishing, assembly, engineering time,
contingency, margin); assumptions and exclusions; validity terms;
revision control. *Out:* the commercial terms themselves (`G.1`) and
the rate card (`G.4`), both cited rather than restated.

**3. Required inputs.** `C.2`; `C.3`; `G.1`; `G.4`.

**4. Data / content fields.** `QuoteID`; `Client`; `Enquiry Reference`;
`Date`; `Validity Period`; `Revision`; `Scope Statement`;
`Assumptions`; `Exclusions`; `Line Items` (description, quantity, unit
cost basis, unit price, total); `Material Cost`; `Process Cost`;
`Engineering Time`; `Finishing`; `Assembly`; `Contingency %`;
`Margin %`; `Total Price`; `Lead Time Offered`; `Payment Terms`;
`Terms Reference`; `Prepared By`; `Approved By`;
`Outcome` (Won / Lost / Pending); `Outcome Reason`.

**5. Outputs / artefacts.** `Quotation Template.docx` (or equivalent);
`Estimate Build-up Template.xlsx`; `Quotation Structure.md`;
`Assumptions & Exclusions Standard Library.md`.

**6. Acceptance criteria.** The template produces a quotation with no
manual arithmetic outside the build-up sheet. Assumptions and exclusions
are mandatory fields, not optional. Outcome is recorded on every
quotation — a quotation with no recorded outcome teaches nothing.

**7. Dependencies.** `C.2`, `C.3`, `G.1`, `G.4`.

**8. Recommended next action.** Standardise the assumptions and
exclusions library first; it is where quotation risk actually lives.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes, later** — quotation is a
plausible future module; the structure defined here is what such a
module would implement.

---

## C.5 — Procurement Decision Support

**1. Purpose.** Decide who to buy from, and on what basis, consistently.

**2. Scope.** *In:* supplier selection criteria and weighting;
single-versus-multi-source policy; quotation comparison method;
total-cost-of-ownership factors beyond unit price; risk assessment;
escalation and approval thresholds. *Out:* the purchase order process
itself (`D.4`).

**3. Required inputs.** `C.1`; `C.2`; `C.3`; `G.5` (approval
thresholds).

**4. Data / content fields.** `CriterionID`; `Criterion`; `Weighting`;
`Measurement Method`; `Data Source`; `Threshold`;
`Disqualifier?`; plus a comparison record: `Requirement`;
`Suppliers Compared`; `Quoted Price`; `Lead Time`; `Quality Risk`;
`Total Cost of Ownership`; `Decision`; `Rationale`; `Approved By`;
`Date`.

**5. Outputs / artefacts.** `Procurement Decision Support.md`;
`Supplier Comparison Template.csv`; `Sourcing Policy.md` (single vs
multi-source, approval thresholds).

**6. Acceptance criteria.** Every criterion has a measurement method and
a named data source. Approval thresholds match `G.5`'s financial
controls exactly — a mismatch between the two is a defect in one of
them, resolved, not tolerated.

**7. Dependencies.** `C.1`–`C.3`, `G.5`.

**8. Recommended next action.** Write the sourcing policy and approval
thresholds; they are one page and prevent the most expensive mistakes.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes, later** — alongside `C.1` in any
future procurement capability.
