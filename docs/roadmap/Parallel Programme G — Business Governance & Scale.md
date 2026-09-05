# Parallel Programme G — Business Governance & Scale

**Part of** [Parallel Work Programme A–G](Parallel%20Work%20Programme%20A–G.md).
**Position in recommended order:** 2nd — the highest risk reduction per
hour spent, and fully independent of every engineering programme.
**Status of every sub-package below:** Defined, not started (2026-09-05).
**Claude Code required for this programme:** No.

## Programme Purpose

The commercial and legal structures that let the business take on real
work at real value without carrying unmanaged risk: contracts,
insurance, intellectual property, pricing, financial control, sales
pipeline, and the operating model that lets any of it scale.

**Standing limitation, stated once and applying to the whole
programme.** `G.1`, `G.2` and `G.3` touch legal, insurance and
regulatory matters. Nothing produced under this programme is legal,
insurance or tax advice, and nothing here substitutes for a qualified
professional. The deliverables are **structures, registers, checklists
and briefing documents that make professional review cheap and
focused** — the questions to ask, the terms to decide, the positions
already taken. Every clause position and every cover level is recorded
as **requires professional review** until a named professional has
actually reviewed it, and that review is recorded with a date.

---

## G.1 — Contract Templates & Commercial Terms

**1. Purpose.** Make sure every engagement runs on written terms that
say who owns what, who is liable for what, and when payment is due.

**2. Scope.** *In:* standard terms and conditions of sale; consultancy
and design services agreement; NDA (mutual and one-way); supply
agreement; the commercial term positions (payment, liability cap, IP
ownership, warranty, termination, variation, force majeure, governing
law); a term-by-term negotiation position register. *Out:* drafting
final binding language without professional review, and any advice on
another party's contract.

**3. Required inputs.** The engagement types actually offered; existing
contracts and any client terms already accepted; a decided position on
IP ownership (see `G.3`); professional legal input for final wording.

**4. Data / content fields.** Term register: `TermID`; `Term`;
`Our Preferred Position`; `Acceptable Fallback`;
`Walk-away Position`; `Rationale`; `Risk If Conceded`;
`Approval Required From`; `Professional Review Status`;
`Review Date`; `Notes`.
Contract register: `ContractID`; `Counterparty`; `Type`;
`Signed Date`; `Term/Expiry`; `Value`; `Key Deviations From Standard`;
`Liability Cap`; `IP Position`; `Renewal/Notice Date`;
`Document Location`.

**5. Outputs / artefacts.** Draft templates (T&Cs, services agreement,
NDA, supply agreement); `Commercial Terms Position Register.csv`;
`Contract Register.csv`; a briefing note for the professional review.

**6. Acceptance criteria.** Every standard term has a preferred, a
fallback and a walk-away position with a recorded rationale. Every
template is marked with its professional review status and date — an
unreviewed template is labelled as such on its face. The contract
register captures every deviation from standard actually agreed.

**7. Dependencies.** `G.3` (IP position — decide it before drafting);
`G.4` (payment terms). Feeds `C.4`, `E.5`.

**8. Recommended next action.** Write the term position register first.
It is the input a solicitor needs, and it makes their time cheap.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **No** — business governance content, not
product content. Contract *references* may appear against projects; the
terms themselves stay outside.

---

## G.2 — Insurance & Risk Register

**1. Purpose.** Know what the business is exposed to, what is insured,
and what is deliberately carried.

**2. Scope.** *In:* the business risk register (commercial, technical,
operational, financial, legal, key-person); insurance policies held,
their limits, exclusions and renewal dates; the professional indemnity
question specifically; risk treatment decisions. *Out:* choosing an
insurer, and any statement about what a policy actually covers — that is
the broker's and the policy wording's, cited, not paraphrased.

**3. Required inputs.** Existing policies and schedules; the real
engagement types and their exposure; client contractual insurance
requirements from `G.1`; broker input.

**4. Data / content fields.** Risk: `RiskID`; `Category`;
`Description`; `Cause`; `Consequence`; `Likelihood`; `Impact`;
`Gross Score`; `Existing Controls`; `Net Score`;
`Treatment` (Accept / Mitigate / Transfer / Avoid); `Owner`;
`Action`; `Review Date`; `Status`.
Insurance: `PolicyID`; `Type`; `Insurer`; `Policy Number`;
`Cover Limit`; `Excess`; `Key Exclusions`; `Aggregate or Each Claim`;
`Retroactive Date`; `Premium`; `Renewal Date`;
`Contractual Requirement Met?`; `Broker Contact`.

**5. Outputs / artefacts.** `Business Risk Register.csv`;
`Insurance Register.csv`; `Risk Management Policy.md`;
`Insurance Renewal Calendar.md`.

**6. Acceptance criteria.** Every risk has an owner, a treatment
decision and a review date — a risk with no owner is not managed. Every
policy records limit, excess, exclusions and renewal date. Every
insurance requirement imposed by a client contract in `G.1` is checked
against a policy actually held, and gaps are recorded as risks.

**7. Dependencies.** `G.1` (contractual requirements).

**8. Recommended next action.** Check current professional indemnity
cover against the highest-liability contract the business has actually
signed. That single comparison is the fastest risk finding available.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **No** — business governance content.
(TempestOS maintains its own separate technical `Risk Register.md`; the
two are unrelated and must not be merged.)

---

## G.3 — IP & Data Protection Framework

**1. Purpose.** Establish who owns what is created, how client
confidential information is handled, and how personal data is treated.

**2. Scope.** *In:* the default IP ownership position for client work;
background versus foreground IP; licence-back positions; confidentiality
handling and classification; personal data inventory, lawful basis,
retention and subject rights; the data breach response outline; tooling
and third-party data flows (including AI assistants). *Out:* patent
strategy, and any statement of regulatory conformance not actually
established.

**3. Required inputs.** `G.1`; `D.6` (retention); the real list of
systems holding data; the real list of third-party services used;
professional input on data protection obligations.

**4. Data / content fields.** IP: `IPItemID`; `Description`; `Type`
(background / foreground / third-party); `Owner`; `Created Under`
(project/contract); `Licence Position`; `Restrictions`; `Evidence of
Creation`; `Notes`.
Data: `DataAssetID`; `Description`; `Contains Personal Data?`;
`Categories`; `Lawful Basis`; `Source`; `Location/System`;
`Third Parties With Access`; `Transfer Outside Jurisdiction?`;
`Retention Period`; `Deletion Method`; `Confidentiality Class`;
`Owner`; `Review Date`.

**5. Outputs / artefacts.** `IP Policy.md`; `IP Register.csv`;
`Data Protection Framework.md`; `Data Asset Inventory.csv`;
`Information Classification Standard.md`; `Breach Response Outline.md`.

**6. Acceptance criteria.** The default IP position is stated in one
unambiguous sentence, and `G.1`'s templates match it exactly. Every data
asset holding personal data records a lawful basis and a retention
period, or is explicitly marked **Unknown — requires professional
review**. Third-party services, AI assistants included, appear in the
inventory with their real data flows.

**7. Dependencies.** `G.1`, `D.6`. Feeds `F.1` (what may be given to an
assistant as context).

**8. Recommended next action.** Build the data asset inventory. It
usually reveals that the framework's hardest questions are about tools
already in daily use.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Partly** — information classification
maps onto document metadata (`D.6`); the framework itself stays business
governance.

---

## G.4 — Pricing & Rate Card

**1. Purpose.** Decide what the business charges, and on what basis,
before the next enquiry rather than during it.

**2. Scope.** *In:* hourly and day rates by activity and seniority;
project and fixed-price pricing approach; margin policy by work type;
minimum charges; expenses and travel policy; discount authority; annual
review mechanism. *Out:* client-by-client negotiation, and any rate not
grounded in a real cost base.

**3. Required inputs.** `D.3` (real cost base and overhead); `C.2`
(process costs); real market comparison; a target margin decision.

**4. Data / content fields.** `RateID`; `Activity`; `Seniority/Grade`;
`Unit` (hour / day / fixed); `Standard Rate`; `Minimum Charge`;
`Cost Basis`; `Overhead Recovery`; `Target Margin %`;
`Discount Floor`; `Discount Authority`; `Currency`;
`Valid From`; `Valid To`; `Review Date`; `Rationale`.

**5. Outputs / artefacts.** `Rate Card.csv`; `Pricing Policy.md`
(including margin, discount authority and expenses);
`Rate Derivation.xlsx` (cost base → overhead → margin → rate, shown).

**6. Acceptance criteria.** Every rate is derived from a stated cost
base and target margin, with the derivation visible — a rate that cannot
be derived cannot be defended in a negotiation. Discount authority is
explicit. The card carries a validity period and a review date.

**7. Dependencies.** `D.3` (hard); `C.2`. Feeds `C.4`, `G.1`, `G.5`.

**8. Recommended next action.** Calculate the true cost per productive
hour, including overhead and non-billable time. Every rate decision
follows from that one number.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **No** as content (commercially
sensitive), though rates would be an input to any future estimating
capability.

---

## G.5 — Financial Controls & Forecasting

**1. Purpose.** Make sure money is committed deliberately, and that
future cash position is known before it becomes urgent.

**2. Scope.** *In:* authority limits by value and type; approval
workflow; cash-flow forecast structure; pipeline-weighted revenue
forecast; budget structure; the monthly financial review pack; the key
financial indicators actually watched. *Out:* statutory reporting and
tax planning, both requiring professional input.

**3. Required inputs.** `D.3` (finance structure); `G.4` (rates);
`D.1`/`G.6` (pipeline); real historical costs.

**4. Data / content fields.** Authority: `AuthorityID`;
`Transaction Type`; `Value Band`; `Approver`; `Second Approver
Required?`; `Documentation Required`; `Exceptions`.
Forecast: `Period`; `Opening Cash`; `Confirmed Revenue`;
`Weighted Pipeline Revenue`; `Committed Costs`; `Fixed Costs`;
`Variable Costs`; `Tax Provision`; `Closing Cash`;
`Minimum Cash Threshold`; `Assumptions`.
Indicators: `IndicatorID`; `Name`; `Definition`; `Target`;
`Frequency`; `Source`; `Owner`.

**5. Outputs / artefacts.** `Financial Controls Policy.md`;
`Authority Limits Table.csv`; `Cash Flow Forecast Template.xlsx`;
`Monthly Financial Review Pack.md`; `Key Indicators.csv`.

**6. Acceptance criteria.** Authority limits are unambiguous at every
value, with no gap or overlap between bands, and match `C.5` and `D.4`
exactly. The cash-flow forecast states its assumptions on the same sheet
as its numbers. Every indicator has a definition precise enough that two
people calculate it identically.

**7. Dependencies.** `D.3`, `G.4`, `G.6`. Feeds `C.5`, `D.4`.

**8. Recommended next action.** Write the authority limits table — one
page, immediate effect, and it is the control the other sub-packages
keep citing.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **No** — business operating content.

---

## G.6 — Business Development & Sales Pipeline

**1. Purpose.** Make new work a managed process rather than an
occurrence.

**2. Scope.** *In:* the target client profile; the service offering
definition; the enquiry-to-order process and its conversion metrics;
proposal approach; follow-up discipline; capability statement and
credentials; referral and repeat-business handling. *Out:* marketing
campaign execution and website production.

**3. Required inputs.** `D.1` (CRM structure); `C.4` (quotations and
their outcomes); real win/loss history; `E.5`/`F.5` for credentials.

**4. Data / content fields.** Offering: `OfferingID`; `Service`;
`Description`; `Target Client Type`; `Typical Value`;
`Typical Duration`; `Differentiator`; `Proof Point`;
`Delivery Capability` (in-house / partner / not yet).
Pipeline metric: `MetricID`; `Name`; `Definition`; `Current Value`;
`Sample Size`; `Period`; `Target`.
Win/loss: `OpportunityID`; `Outcome`; `Reason`; `Competitor`;
`Price Position`; `Lesson`.

**5. Outputs / artefacts.** `Service Offering Definition.md`;
`Target Client Profile.md`; `Capability Statement` (client-facing);
`Business Development Process.md`; `Win/Loss Register.csv`.

**6. Acceptance criteria.** Every offering names a real proof point —
delivered work, not aspiration — and states honestly whether the
capability exists in-house today. Every closed opportunity has a
recorded outcome reason. Conversion metrics state their sample size; a
rate from three enquiries is labelled as such.

**7. Dependencies.** `D.1`, `C.4`, `G.4`; `E.5`/`F.5` for credentials.

**8. Recommended next action.** Write the service offering definition
and mark each line honestly as in-house, partnered, or not yet. That
honesty determines what may be sold this month.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **No** — business operating content.

---

## G.7 — Operating Model & Scale Plan

**1. Purpose.** State how the business runs today, what breaks first as
it grows, and what has to be true before each next step.

**2. Scope.** *In:* the current operating model (roles, responsibilities,
capacity, dependencies); the key-person and single-point-of-failure
analysis; the capacity model; growth stages with entry conditions; the
make-versus-hire-versus-partner decision; the systems and tooling roadmap;
the TempestOS role within the operating model. *Out:* committed hiring
plans and dated financial projections.

**3. Required inputs.** Every other Programme D and G sub-package, at
least in outline; real utilisation and capacity data; `G.5` (financial
capacity).

**4. Data / content fields.** Role: `RoleID`; `Role`; `Responsibilities`;
`Currently Held By`; `Capacity`; `Single Point of Failure?`;
`Documented?`; `Successor/Cover`; `Risk`.
Stage: `StageID`; `Stage`; `Revenue/Volume Trigger`;
`What Breaks At This Point`; `Required Capability`;
`Required People`; `Required Systems`; `Entry Conditions`;
`Investment Required`; `Decision Owner`.
Capacity: `Activity`; `Hours Available`; `Hours Committed`;
`Utilisation`; `Constraint`.

**5. Outputs / artefacts.** `Operating Model.md`;
`Role & Responsibility Matrix.csv`; `Capacity Model.xlsx`;
`Scale Plan.md` (stages and entry conditions);
`Single Point of Failure Register.csv`.

**6. Acceptance criteria.** Every role names who holds it and whether it
is documented well enough for someone else to do it. Every growth stage
states what breaks and what must be true before entering — a stage
defined only by a revenue number is not a plan. TempestOS's own role in
the operating model is stated explicitly and honestly, including what it
does not yet do.

**7. Dependencies.** Programme D and `G.1`–`G.6`. Genuinely last in the
recommended order.

**8. Recommended next action.** Complete the single-point-of-failure
register. It is short, uncomfortable, and the most useful page in the
programme.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **No** as content — but this sub-package
is where the honest answer to "what should TempestOS actually do for
this business next?" is recorded, and its findings are a legitimate,
cited input to `docs/governance/Future Capability Register.md`.
