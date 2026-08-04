# ADR-0069: The Engineering Cockpit Is the Workspace's Own Default Landing Screen

## Status

Accepted — `v0.8.0` "Engineering Workspace", `WP 8.0C` (Engineering
Workspace UX Specification), 2026-08-04. Resolves what screen a user
actually sees immediately after `ADR-0068`'s own Workspace launches.

## Context

`ADR-0068` decided *that* the Workspace is `Tempest.App`'s own default
launch target; it did not decide *what the Workspace shows first*.
Today (`WP 8.1A`'s shipped shell), start-up presents an empty Project
Explorer and no dashboard of any kind — a placeholder, not a considered
first screen. `WP 8.0C`'s own controlling instruction names "the
Engineering Cockpit discussed during product planning" and states it
"should become the engineer's primary landing page," requiring a real
decision about what that means for `Program.cs`'s own observable
start-up behaviour, not left to an implementation Work Package to infer.

Two real options exist for a landing screen: a static Home page (a
welcome/orientation screen, cheap to build, quickly stale) or a live
dashboard reflecting current project state (the Engineering Cockpit,
`WP8.0C Engineering Cockpit Specification.md`).

## Decision

**The Engineering Cockpit — not a placeholder Home page — is the
Workspace's own default landing screen, shown immediately after
start-up (or after project selection, where no project is yet implicit)
and reachable as the target of every "home" breadcrumb segment
throughout the Workspace.** The Cockpit is live and data-driven: every
visit reflects current Engineering Core state (Requirements,
Verification, Calculation status, Digital Thread summary, Attention
Centre), not a cached or authored welcome message.

This is a product/UX decision with a real architectural consequence:
the Cockpit's own regions (`WP8.0C Engineering Cockpit Specification.md`
§2) each require a read against existing Engineering Core capability
(status queries already exposed for Requirements, Verification,
Calculations) — no new query capability is introduced by this decision
itself, but a future implementation Work Package must wire the Cockpit
to all of them before start-up can honour this ADR, which the
`Screen Catalogue.md`'s own "Today vs. Target" disclosure already
names as not yet built.

## Consequences

**Positive:**

- Gives every persona (`WP8.0C User Journey Maps.md`, "Cross-Journey
  Observations") one consistent, useful first screen, rather than each
  persona needing their own separate entry point.
- Directly satisfies the controlling instruction's own explicit
  requirement that the Cockpit "should become the engineer's primary
  landing page" — a static Home page would not.
- Keeps the Workspace's own "what needs attention" discipline
  (Principle 9) true from the very first screen a user sees each
  session, not only once they navigate somewhere specific.

**Negative:**

- Requires every Engineering Core capability the Cockpit reads from
  (Requirements status, Verification status, Calculation status, Digital
  Thread evidence counts) to exist and be queryable before the Cockpit
  can be implemented as specified — a real implementation dependency a
  future Work Package must sequence correctly, not a cost of this
  decision alone but one this decision makes explicit.
- A brand-new, empty project's own Cockpit must handle every region
  being empty gracefully (`WP8.0C Engineering Cockpit Specification.md`
  §5) — a static Home page would not have carried this obligation.

## Alternatives Considered

**A static Home/welcome page**, shown once or always — considered and
rejected. Cheap to build, but directly contradicts the controlling
instruction's own explicit framing of the Cockpit as the "primary
landing page," and would not satisfy Principle 9 ("what needs
attention?") on the very screen where an engineer most needs that
question answered.

**No default landing screen — start directly in the Project Explorer**
(today's actual shipped behaviour) — considered and rejected as the
permanent target state, though correctly accepted as `WP 8.1A`'s own
interim shell behaviour (disclosed, `UX Specification.md` §0's own
Sequencing Finding) pending this specification's existence.

## Related Documents

`ADR-0068`; `WP8.0C UX Specification.md` §6; `WP8.0C Engineering
Cockpit Specification.md`; `WP8.0C Screen Catalogue.md` §2; `WP8.0C
User Journey Maps.md`.
