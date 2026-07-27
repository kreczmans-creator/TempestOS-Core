# ADR-0021: Background Service Failures Are Isolated by Default; Criticality Is Opt-In

## Status

Accepted — v0.4.0 release planning (WP 4.0 / WP 4.5), 2026-07-23. Decided
before implementation begins.

## Context

ADR-0013 established a clean, two-category failure model: a platform-service
failure (Configuration, Logging, Discovery, Registration, Dependency
Injection construction) is Host-fatal; a module failure is isolated. A
background/hosted service — named as a Future Extensibility item since WP
2.7A but never designed — does not fit cleanly into either category.
`Runtime Host Architecture.md` flagged exactly this kind of gap for a future
Requirements Engine or Project Engine ("would each need to be classified…
this is an open design question") without resolving it for hosted services
specifically.

Two defaults were considered:

**Option A — Host-fatal by default**, mirroring platform services: any
background service failing aborts or faults the Host, on the reasoning that
background work is often infrastructural.

**Option B — Isolated by default**, mirroring modules: a background
service's failure is caught, logged, and does not affect the Host's own
state; the platform continues running with that one service failed. A
service may explicitly opt into Host-fatal treatment by declaring itself
critical.

## Decision

Option B. Background service failures are **isolated by default**. A
background service's own exception is caught, logged, and does not fault
the Host — the Host continues running exactly as it already does when an
individual module fails (ADR-0013). A background service may declare itself
**critical** (the exact mechanism — an `IsCritical` flag on its registration
options, or a dedicated marker contract — is WP 4.0/4.5's own implementation
decision, not fixed here); a critical service's failure is Host-fatal,
exactly like a platform-service failure.

This mirrors a familiar, well-understood precedent: a desktop operating
system's Bluetooth stack, print spooler, or search indexer can each crash
independently without bringing down the shell that hosts them. TempestOS's
default for background work should be at least that resilient. The Host
terminates only when a service has explicitly declared that its failure
should matter that much — never by default, and never by silent inference.

## Consequences

**Positive:**

- Extends ADR-0013's boundary rather than contradicting it: the six
  foundational platform services remain unconditionally Host-fatal on
  failure; ordinary background services default to isolated, like modules;
  a background service earns platform-service-like treatment only by
  explicit declaration.
- The safer behaviour is the default. A background-service author who does
  nothing extra gets isolation, not accidental Host-wide termination — the
  same "fail safe by default" instinct Fail Fast already applies to
  configuration and startup, pointed the other way for a component whose
  author never asked it to be load-bearing.
- Familiar and predictable: this is the same mental model TempestOS's own
  future users will already have from the platforms TempestOS runs
  alongside.

**Negative:**

- Introduces a third failure category alongside "platform service" and
  "module." A future reader reasoning about "what happens if this fails"
  now has three defaults to know, not two. Mitigated by this ADR and by
  `Failure Behaviour.md` gaining an explicit new section once WP 4.5
  implements this.
- A background service that turns out, in practice, to be load-bearing —
  but whose author never marked it critical — fails silently isolated
  rather than loudly Host-fatal. This is an accepted risk of defaulting to
  the safer option: criticality is a deliberate, visible declaration a
  reviewer can check for during review, not something the runtime infers
  on its own.

## Future Considerations

Automatic restart or re-enablement of an isolated, failed background
service (the natural next question — "restarted or disabled," in the
language this decision was proposed in) is **not** decided by this ADR.
Only the fact that failure alone does not stop the Host is decided here; a
restart/backoff policy is a separate, additive capability for WP 4.5 or a
later release to design deliberately, not to assume as implied by this
decision.
