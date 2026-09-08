# WP 7.2A — Commercial Assessment

## Purpose

Assesses each candidate programme's own commercial value, informed
directly by `VISION.md`'s own Product Principles, `Future Capability
Register.md`'s Commercial category, and `Product Roadmap.md`'s own
phase sequencing — and carries forward the commercial risks bearing on
this Work Package's own recommendation, per its Risk Assessment
requirement.

## Governing Commercial Principle

`VISION.md`'s own Product Principle 1 states the standing rule this
assessment applies throughout: **"Capability before commercial policy."**
`WP 6.6` (Licensing) already established the precedent — a capability's
own commercial value is assessed by what it enables for a real future
customer, not by inventing a pricing or packaging model ahead of one.
No candidate programme in this catalogue has a named, real customer or
confirmed commercial opportunity behind it — this assessment states each
programme's *plausible* commercial value honestly as plausible, never as
confirmed.

## Per-Programme Commercial Assessment

### Programme A — Requirements & Verification Platform

**Plausible commercial value: Medium-High, realising `VISION.md`'s own
stated target user for the first time.** `VISION.md`'s own "Target
Users" section names "an individual engineer or a small professional
engineering practice" as the first real external user this platform will
ever have, "once Engineering Modules ship." Programme A is the first
programme evaluated here that would actually ship one. A working
requirements-and-verification capability is also the most horizontally
applicable of any Engineering Discipline capability — every engineering
practice, regardless of which of the seven remaining discipline
categories it works in, needs requirements management and verification
tracking, making this the single Engineering Discipline candidate least
dependent on which specific discipline a future customer turns out to
practice.

**Commercial risk:** No confirmed customer exists. This programme's own
commercial value remains unconfirmed until a real practice actually
adopts it — disclosed honestly, not assumed.

### Programme F — Platform Hardening

**Plausible commercial value: Medium, primarily as a deployment
enabler rather than a revenue driver of its own.** Closing `FCR-0003`/
`FCR-0004` (REST API authentication/TLS) is a prerequisite for any
customer deployment beyond a single trusted local machine — real
commercial value once a customer needs multi-machine or networked
access, but no such customer exists today. Closing `FCR-0001` (plugin
trust isolation) has commercial value primarily as an enabler of
`FCR-0002` (a third-party plugin ecosystem) — a plausible future revenue
model (a plugin marketplace, a certified-partner programme) with no
named plans behind it today.

**Commercial risk:** Building this now, with no real deployment scenario
or plugin author, risks the same "engineered ahead of a business case"
pattern `VISION.md`'s own Product Principle 3 explicitly cautions
against — this programme's own commercial value is real but currently
latent, not active.

### Programme G — AI & Engineering Intelligence

**Plausible commercial value: Low, unconfirmed, and currently
unscopeable.** No document in this repository names a concrete AI/
automation commercial scenario — no target customer, no product concept,
no named differentiator. `FCR-0024`'s own entry is explicit that the
underlying capability already exists structurally; there is no
commercial gap this programme would close today.

### Programmes B, C, D, E — Mechanical, Building Services/HVAC, Structural, Electrical

**Plausible commercial value: Unknown, and cannot be honestly estimated
without a real capability to evaluate.** Each of these categories
plausibly represents real commercial value in the abstract — engineering
practices in every one of these disciplines exist and could plausibly
become customers — but `Future Capability Register.md` names zero
concrete capabilities within any of them, so no specific commercial
claim can be made about *what* a customer in any of these disciplines
would actually pay for. Estimating commercial value for an unspecified
capability would be speculation dressed as analysis — this assessment
declines to do so, consistent with `WP7.0B Engineering Discipline
Assessment.md`'s own identical discipline.

## Commercial Risks and Mitigations

Per this Work Package's own Risk Assessment requirement:

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Programme A ships with no real, named customer to validate it against, repeating the same "designed from aspiration, not real demand" pattern `WP7.0B Roadmap Risk Register.md`'s own `RR-1` already discloses for the Engineering Foundation itself | Medium | Medium | Treat Programme A's own scope as provisional until a real practice or customer scenario validates it, exactly as `RR-1` already recommends for the frameworks it builds on — do not consider the Requirements Engine's own design final before that validation occurs. |
| `FCR-0025` (Commercial Licensing Model) is engineered without a named pricing/packaging strategy once Programme A ships a real, licensable capability for the first time | Low (not yet triggered) | Medium | `FCR-0025`'s own existing register entry already requires explicit Product/Commercial input before scoping begins — unchanged by this review; Programme A's own completion is a plausible future trigger, not a current one. |
| Sequencing Programme F second (not first) delays commercial readiness for any customer deployment beyond a single local machine | Medium | Medium | Named explicitly in `WP7.2A Recommended Programme.md` as an accepted, disclosed trade-off, with each of Programme F's own triggers actively monitored, not deferred indefinitely. |

## Verdict

Programme A carries the strongest plausible commercial value of any
candidate evaluated — not because a customer has been confirmed, but
because it is the only programme that would ship the kind of capability
`VISION.md` itself names as this platform's first real external-facing
product. This assessment supports `WP7.2A Recommended Programme.md`'s
own conclusion without asserting a confirmed commercial case this
repository does not yet have evidence for.

## Related Documents

`VISION.md`; `docs/governance/Future Capability Register.md`
(`FCR-0025`, `FCR-0026`); `docs/governance/Product Roadmap.md`;
`WP7.0B Roadmap Risk Register.md` (`RR-1`, `CR-1`, `CR-2`); `WP7.2A
Recommended Programme.md`.
