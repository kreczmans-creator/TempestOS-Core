# ADR-0162: Review Decisions Are a Local Intent-File Queue, Switched Off by Default Until v1.0 RC

## Status

Accepted — Product Owner decision, 2026-10-02.

**Supersedes `ADR-0157` Decision 5** ("Read-only. No approve, reject or
status-change path from the dashboard or the phone"), one day after that
ADR's own acceptance. The Product Owner's own words, verbatim: *"That's a
180 on a decision we made yesterday. My problem is I'm looking 3 years in
the future when I've got staff who's work I'm having to approve, so having
the mobile version or dashboard ability is going to be key to keep things
moving. If this is suited to be pushed to later releases then so be it,
but I'm sat here looking long term thinking if it's built now then I don't
have to change architecture later, and we can just have it 'switched off'
in the short term."* `ADR-0157`'s own "not wanted" was a real decision,
not an oversight — it is recorded here as superseded, not erased, so the
decision trail stays honest about both calls and why they differ.

## Context

The August 2026 Companion app (recovered ref
`refs/recovered/claude/tempestos-companion-mobile-ubznt3`) had one write
action: approve or return a Document-family object awaiting review, via
`SetDocumentStatusCommand`, dispatched through its own REST API extension
(`ADR-0114`, never merged). That REST API — the only inbound write surface
this platform has ever had — was frozen in `WP 17.2A` for cause: no real
authentication (`TD-13`), no TLS (`TD-14`), no real audit attribution
(`TD-15`). `ADR-0157` ported the Companion's *read* side only, explicitly
declining to revive a write path on top of known-insecure infrastructure.

The Product Owner's request changes the premise, not the risk: build the
real architecture now, while this is a one-person shop and the blast
radius of getting the plumbing wrong is small, so it does not need
rebuilding when staff exist and the feature is switched on for real. The
constraint is the same one `ADR-0157` already respected — nothing here
may depend on reviving the frozen, insecure REST API, and the real
authentication work (closing `TD-13`/`TD-14` properly, or an equivalent)
is a prerequisite for turning this on in anger, not an optional follow-up.

## Decision

**1. A review decision travels as a local intent file, never a network
call into Core.** Tempest-Dashboard already has the only network surface
this integration uses, and it is one-way: Core writes export files, an
agent on the same machine pushes them out
(`DashboardExportHostedService`'s own remarks, §3.4). This adds the
mirror of that pipe, at the same trust level, not a new one: the
dashboard records a decision locally; `agents/tempest-core-agent.*` —
already trusted to push Core's engineering data outward on the owner's
machine — pulls it and writes it to a directory on Core's own host;
`ReviewDecisionIntakeHostedService` reads that directory and dispatches
the real lifecycle command. No new credential, no new listener, no new
machine this didn't already reach.

**2. Four disciplines dispatch a real command; Evidence does not.**
`reviews.json`'s five disciplines map onto exactly four existing,
unmodified lifecycle commands — approve transitions to
`LifecycleState.Approved` (`RequirementStatus.Approved` for Requirements),
reject/return transitions to `Draft`:

| Discipline | Command | Kind value(s) |
|---|---|---|
| Documents, Drawings, CAD Models | `SetDocumentStatusCommand` | `DocumentObjectFactoryRegistry.SupportedKinds` |
| Calculations | `SetCalculationStatusCommand` | `CalculationObjectFactoryRegistry.CalculationKind` |
| Verification Activities | `SetVerificationActivityStatusCommand` | `VerificationActivityFactoryRegistry.SupportedKind` |
| Requirements | `SetRequirementStatusCommand` | `RequirementsService.RequirementDocumentKind` |

Evidence's own "approve" is `Issue` — SkiaSharp PDF generation across
three non-atomic steps, already disclosed elsewhere in this codebase as a
real crash-safety gap (a crash between them leaves an Issued record with
no sheet). Automating that unattended, from an intent file nobody is
watching run, is a worse idea than leaving Evidence read-only a little
longer. Recorded as `TD-189`, not silently dropped — an intent file
naming Evidence is read and refused with a clear reason, never ignored.

**3. Off by default, both ends, independently.** `ReviewDecisions:Enabled`
(Core) and `reviewDecisions.enabled` (Dashboard) each default to `false`.
Both must be turned on for anything to happen — mirroring the
`ISignOffPolicy` precedent (`ADR-0161`, one day earlier: "global
second-person sign-off switch, off by default"), the same shape for the
same reason: ship the mechanism, decide when to use it later. With either
flag off, Dashboard shows no approve/reject control at all (not merely a
disabled one) and Core's intake service does not start its loop — no
intent directory is even read.

**4. No new identity system; the file itself declares its own author.**
An intent file carries `requestedBy` as free text the dashboard's own
Settings screen captures (a name, not a credential) — exactly as
candid as the trust boundary actually is: whoever can reach the
dashboard's write-enabled network, over `DASHBOARD_TOKEN`, the same
shared bearer secret every other network write already trusts. This is
not real per-person authentication or authorisation, and nothing here
claims it is. `TD-190` records that a real identity and permission model
(`companion.act`, mirroring the old Companion's own `ADR-0113`, or
reviving `TD-13`/`TD-14`'s fix properly) is the prerequisite for
switching this on for more than one person — i.e., for v1.0 RC. Until
then it is honestly fine for exactly the shop that exists today: the
Product Owner is the only person with a dashboard token.

**5. "Pending", not "applied", is what the dashboard shows.** The
dashboard has no channel back from the agent's pull, so it never learns
"applied" directly. A submitted decision is shown as pending until the
item it named is no longer in the next ingested `reviews.json` — the
same signal that already proves every other write path in this platform
(the desktop's own optimistic-then-reconciled pattern). A decision still
pending after a generous timeout (three export intervals,
`DashboardExportOptions.DefaultIntervalSeconds` × 3 ≈ 15 minutes) is
shown as failed/stale, not silently forgotten.

## Consequences

**Positive.** The architecture exists, proven against four real
commands, costing nothing to switch on later — exactly the "build once"
the Product Owner asked for. Nothing it adds is reachable while off.

**Negative / disclosed.**
- Latency: a decision takes up to one export interval (default 5
  minutes) to reach Core, then one agent-poll interval to be pulled back.
  Not real-time; this ADR does not claim otherwise.
- `requestedBy` is unverified free text. Fine for one person; not fine
  for staff whose work is being approved — `TD-190` names this exactly.
- Evidence stays read-only (`TD-189`).
- A reject always returns to `Draft`, never a finer-grained "needs
  changes" state — matches the old Companion's own binary action exactly;
  a richer verb set is a later `FCR`, not blocked by this one.

## Related Documents

`ADR-0157` (superseded, Decision 5); `ADR-0113`–`ADR-0117` (the original
Companion's write design, recovered); `ADR-0154` (export pattern this
mirrors); `ADR-0161` (the off-by-default precedent this follows);
`TD-13`/`TD-14`/`TD-15` (the frozen REST API's debt this deliberately
does not reopen); `TD-189`, `TD-190` (new); Tempest-Dashboard
`docs/DATA-CONTRACTS.md` §Reviews; TempestMobile `docs/adr/ADR-0001`.
