# ADR-0135: P03 Compares, Ranks and Recommends, and Performs No Act of Procurement Authority

## Status

Accepted — `Group D` (P03 Commercial Intelligence), 2026-09-06.

## Context

This is `ADR-0127`'s rule for engineering conclusions and `ADR-0130`'s for
business authority, applied to procurement — where the pull towards
autonomy is strongest, because the decision looks arithmetic. The
comparison produces a score. The highest score wins. Why should a person
have to press a button?

Because the score is only as good as the criteria and the weights, both
of which somebody chose; because a weighted average of things measured in
different ways is a judgement wearing a number's clothes; and because
placing an order, awarding business, approving a supplier or committing
expenditure are acts with contractual and financial consequences that
software cannot be accountable for.

There is also a specific arithmetic trap. The natural way to score a
criterion nobody assessed is zero. Doing so ranks the supplier nobody
researched *below* the supplier who was researched and found wanting —
so the comparison systematically punishes candidates for the
organisation's own gaps in information, and does it invisibly.

## Decision

**No type under `Tempest.Core.CommercialIntelligence` offers an act of
procurement authority.** Enforced by a reflection test over every type in
the namespace, which fails on any public member whose name contains
`Award`, `PlaceOrder`, `RaiseOrder`, `IssuePurchaseOrder`,
`ApproveSupplier`, `QualifySupplier`, `CommitExpenditure`, `CommitSpend`,
`AcceptQuote`, `AcceptQuotation`, `SignContract` or `Procure`.

**Absent information is never scored as zero.**
`CriterionStandings.Score` returns `null` for `NotAssessed` and
`Unknown` — which are themselves distinct, because nobody looked and
somebody looked and could not find out are different states of the world.
An unestablished criterion reduces the candidate's `EstablishedWeight`
and becomes an outstanding question; it does not lower the score.

**Recommendation strength is a statement about the comparison, not a
confidence percentage.** `Insufficient` (too little established to rank),
`Provisional` (a leader, on information with gaps), `Marginal` (complete,
narrow), `Clear` (complete, decisive). A comparison resting on gaps cannot
reach better than `Provisional`.

**Exclusions stay in the record with their reasons.** A candidate failing
a mandatory criterion is excluded — never dropped — with the criterion and
its statement attached. A candidate excluded by a person's judgement
rather than by a criterion must name the person, and validation warns
where it does not.

**Decisions are recorded, never taken.**
`SourcingComparison.RequiresHumanDecision` is unconditionally `true`.
`SourcingDecisionState` includes `AlternativeChosen`, so disagreeing with
the recommendation is a first-class outcome rather than an anomaly. A
recorded decision that names nobody who took it is a **validation error**,
not a warning — it is the one thing `D5` must never hold quietly.

Where the organisation must:

| | `P03` does |
|---|---|
| Award the work | Rank the candidates and say what it could not establish |
| Approve a supplier | Record what somebody established about its capability, and who |
| Accept a quote | Record what was offered, how firm it is, and until when |
| Issue a quotation | Refuse to call it issued without naming who issued it and on what authority |
| Commit expenditure | Total an estimate and say which of its lines nobody has priced |

## Consequences

**The recommendation is frequently "not enough is known."** That is the
intended output of a comparison built on a half-populated library, and it
is more useful than a ranking that looks complete.

**A well-researched candidate can rank below a barely-researched one**,
because the second is scored only on what is known. The gap is reported
alongside, and the strength is capped at `Provisional` — the comparison
declines to hide either fact.

**Nothing in `P03` writes back to the supplier database.** Being a
candidate exists only within one comparison; it is not a status a supplier
acquires.
