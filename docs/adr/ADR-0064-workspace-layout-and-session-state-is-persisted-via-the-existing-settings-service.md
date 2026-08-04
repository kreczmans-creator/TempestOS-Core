# ADR-0064: Workspace Layout and Session State Is Persisted via the Existing Settings Service

## Status

Accepted — `v0.8.0` "Engineering Workspace", `WP 8.0A` (Engineering
Workspace Architecture), 2026-07-30. Resolves how the Workspace
remembers a user's own panel arrangement, open tabs, and last selection
across sessions.

## Context

A docking, multi-panel, tabbed desktop Workspace (`ADR-0062`) needs
somewhere to persist how a user last arranged it — panel positions and
sizes, which Document Area tabs were open, and the last-selected Project
Explorer node (`WP8.0A UI Architecture.md` §5) — so that closing and
reopening the Workspace does not discard the user's own working
arrangement. `ISettingsProvider` (`Tempest.Core.Settings`, `WP 6.4`)
already exists as this platform's own general-purpose, per-user,
runtime-mutable value store, built on `IPersistenceStore`
(`ADR-0041`/`ADR-0042`). No genuinely new requirement exists here that
`ISettingsProvider` does not already satisfy.

## Decision

**Workspace layout and session state (panel layout, open Document Area
tabs, last-selected Project Explorer node) is persisted via the
existing `ISettingsProvider`. No new persistence mechanism is
introduced.** The Workspace reads its own layout at startup via
`ISettingsProvider.GetValueAsync` and writes it back on change via
`SetValueAsync`, registering its own `ISettingDefinition`(s) exactly as
`SettingsSampleModule` already demonstrates the pattern for any other
runtime-mutable value.

Engineering data itself (a Requirement's own statement, status, or
revision history) is explicitly **not** Workspace state and is never
cached in Settings — every object view re-reads its own owning service
fresh on every open (`WP8.0A UI Architecture.md` §5), so no staleness
risk is introduced by this decision.

## Consequences

**Positive:**

- Zero new Platform Service, zero new storage abstraction — the sixth
  consecutive TempestOS capability (after Materials, Calculations,
  Verification, Requirements, and now the Workspace) to reach "reuse
  what already exists" as its own governing decision
  (`WP7.4.0 Architecture Baseline Summary.md`'s own cross-framework
  finding, extended here to presentation-layer state for the first
  time).
- Workspace layout state inherits `ISettingsProvider`'s own existing
  guarantees (in-memory cache over `IPersistenceStore`, invalidated on
  write) with no additional engineering effort.

**Negative:**

- `ISettingsProvider`'s own approved contract left a sensitive-value
  flag deliberately unadded (`ADR-0042`) — not a concern for layout
  state, which carries no sensitive content, but noted here since this
  is the first consumer of `ISettingsProvider` for structured,
  multi-field state (a panel layout) rather than a single scalar value;
  how a multi-field layout value is actually shaped as one or more
  `ISettingDefinition` entries is deferred to Contract Review, not
  designed in this architecture.

## Alternatives Considered

**A new, Workspace-specific persistence mechanism** — considered and
rejected outright; no requirement here differs from what
`ISettingsProvider` already satisfies, and introducing a second
mechanism for materially the same kind of data (per-user, runtime-
mutable) would violate this project's own repeatedly-demonstrated reuse
discipline for no benefit.

**Persisting layout via `IPersistenceStore` directly, bypassing
Settings** — considered and rejected. `ISettingsProvider` exists
specifically to mediate exactly this class of value (`ADR-0042`); going
around it to `IPersistenceStore` directly would duplicate the
in-memory-cache/invalidation behaviour `ISettingsProvider` already
provides, for no reason.

## Related Documents

`WP8.0A Workspace Architecture Document.md`; `WP8.0A UI
Architecture.md` §5; `ADR-0041`; `ADR-0042`; `docs/architecture/
Platform Service Map.md` (Settings entry).
