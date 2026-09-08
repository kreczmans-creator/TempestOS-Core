# WP 7.0A — Future Capability Summary

## Status

Complete. This document is the prioritisation pass `docs/governance/
Future Capability Register.md` itself does not perform in full — that
register records what each capability *is*; this document assesses,
for every one of its 28 entries, Strategic Value, Technical Complexity,
Platform Readiness, and a Suggested Implementation Phase (per `docs/
governance/Product Roadmap.md`'s own 8-phase model).

## Rating Scale

- **Strategic Value / Technical Complexity:** Low, Medium, High —
  qualitative, based on each entry's own "Business Value"/"Engineering
  Effort" fields in `Future Capability Register.md`, not a new
  assessment invented here.
- **Platform Readiness:** whether the platform has what this capability
  needs to be attempted today — **Ready**, **Partially Ready**, or **Not
  Ready** (a real dependency, per the entry's own "Dependencies" field,
  has not yet shipped).
- **Suggested Phase:** the `Product Roadmap.md` phase number this
  capability most plausibly belongs to — not a commitment, per that
  roadmap's own Non-Commitments section.

## Prioritisation Table

| FCR | Title | Category | Strategic Value | Technical Complexity | Platform Readiness | Suggested Phase |
|---|---|---|---|---|---|---|
| FCR-0001 | Plugin & Registration Trust Isolation Retrofit | Platform | High | Medium | Ready (mechanism exists) | 4 |
| FCR-0002 | Third-Party Plugin Ecosystem Enablement | Integrations | Unknown | Unknown | Not Ready (depends on FCR-0001) | 8 |
| FCR-0003 | REST API Authentication Mechanism | Platform | High | Medium-High | Ready | 4 |
| FCR-0004 | REST API Transport Security (TLS) | Platform | High | Low-Medium | Ready | 4 |
| FCR-0005 | Governance Register Health-Check Tooling | Platform | Medium | Low-Medium | Ready | 4 |
| FCR-0006 | `Runtime`↔`Diagnostics` Namespace Reference Resolution | Platform | Medium | Low | Ready | 4 |
| FCR-0007 | Native Query/Filter Capability for Persistence | Platform | Low (unmeasured) | Medium-High | Partially Ready (no measured trigger) | 6 |
| FCR-0008 | Legacy Logging Consolidation | Platform | Low | Low-Medium | Ready | 8 |
| FCR-0009 | Disposal Tracking for DI-Registered Singletons | Platform | Low | Medium | Not Ready (no disposable service exists) | 8 |
| FCR-0010 | Configurable Plugin Root/Manifest Conventions | Platform | Low | Low | Ready | 8 |
| FCR-0011 | `IHostedService` Naming Disambiguation | Platform | Low-Medium | Low-Medium | Ready | 8 |
| FCR-0012 | Reporting Delivery-Channel, History & Scheduling Capability | Platform | Low (no named need) | Medium-High | Partially Ready | 6 |
| FCR-0013 | Notification History/Inbox & Delivery-Channel Capability | Platform | Low (no named need) | Medium | Partially Ready | 6 |
| FCR-0014 | Advanced Settings Capability | Platform | Low (no named need) | Low-Medium | Ready | 6 |
| FCR-0015 | Export Artifact Compression & Encryption | Platform | Low (no named need) | Medium | Ready | 6/7 |
| FCR-0016 | Export Schema Migration/Upgrade Path | Platform | Low (no named need) | Medium-High | Not Ready (no schema bump exists) | 8 |
| FCR-0017 | License File Integrity Verification | Platform | Low (no named channel) | Medium | Ready | 6/7 |
| FCR-0018 | REST Request-Parameter Binding | Platform | Low (no named need) | Medium | Ready | 6 |
| FCR-0019 | Explicit Actor Parameter for Cross-Boundary Audit Attribution | Platform | Low (mitigated already) | Low-Medium | Ready | 6 |
| FCR-0020 | Secrets-Redaction Logging Convention | Infrastructure | Medium | Low-Medium | Not Ready (no real secret exists yet) | 4/7 |
| FCR-0021 | Multi-User / Tenant Isolation Architecture | Infrastructure | High (long-term) | High | Not Ready (no DI Scoped lifetime) | 7 |
| FCR-0022 | Cloud Synchronisation | Infrastructure | Unknown | Unknown, likely High | Not Ready | 7 |
| FCR-0023 | Offline Synchronisation & Mobile Client Support | Infrastructure | Unknown | Unknown, likely High | Not Ready | 8 |
| FCR-0024 | AI/Automation Command Invocation | AI | Unknown (depends on consumer) | Low (framework already supports it) | Ready (framework side) | 8 |
| FCR-0025 | Commercial Licensing Model | Commercial | Medium-High (long-term) | Medium-High | Not Ready (no commercial need named) | 7 |
| FCR-0026 | Defence-Sector / Regulated-Environment Compliance Readiness | Commercial | Unknown | Unknown, likely High | Not Ready | 7 |
| FCR-0027 | Requirements Engine | Systems Engineering | Unknown (central to vision) | Unknown, needs own Architecture WP | Not Ready (not yet classified under ADR-0013) | 5 |
| FCR-0028 | Project Engine / Secure Project Data Management | Project Management | Unknown (central to vision) | Unknown, needs own Architecture WP | Not Ready (not yet classified under ADR-0013) | 5 |

## Which Capabilities Should Form the Basis of `v0.7`

**Four capabilities are Ready today, High or Medium strategic value, and
Low-to-Medium complexity: `FCR-0001`, `FCR-0003`, `FCR-0004`, `FCR-0005`,
and `FCR-0006`.** These are the only entries in the table above rated
**Ready** *and* Medium-or-higher Strategic Value *and* not gated on an
external trigger that has not yet arrived (a real secret, a real
third-party plugin, a real deployment scenario). Every other entry rated
"Ready" (`FCR-0008`, `FCR-0010`, `FCR-0011`, `FCR-0014`, `FCR-0019`) is
rated Low Strategic Value precisely because no real, demonstrated need
has been named yet — building them now would be speculative, per
`Security Principles.md` Principle 7 and this document's own governing
discipline.

**Recommendation: these five (`FCR-0001`, `FCR-0003`, `FCR-0004`,
`FCR-0005`, `FCR-0006`) are the `v0.7` candidate basis.** See `WP7.0A
Recommended v0.7 Candidate Work Packages.md` for the full assessment and
proposed grouping into Work Packages.

## Related Documents

`docs/governance/Future Capability Register.md` (full detail per
entry); `docs/governance/Product Roadmap.md`; `WP7.0A Recommended v0.7
Candidate Work Packages.md`.
