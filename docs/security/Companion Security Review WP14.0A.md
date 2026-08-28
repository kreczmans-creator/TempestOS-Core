# Companion Security Review — WP 14.0A

**Scope:** the TempestOS Companion client (`Tempest.Companion`,
`Tempest.Companion.Contracts`), the server-side Companion API
(`CompanionApiRegistration`/`CompanionQueryService` in `Tempest.App`),
and the REST query-and-action surface (`Tempest.Core.Api`,
`ADR-0114`). Performed as part of `WP 14.0A` itself, per the standing
per-Work-Package security review practice. Every claim below is
**Verified** against the shipped source unless marked otherwise.

## 1. Identity & Authentication

- The Companion asserts identity through the platform's existing
  `X-Identity-Id` header (`ADR-0052`) — no parallel identity system was
  introduced. **There is still no real authentication anywhere in the
  platform** (`ADR-0043`: no password, token, or credential exists);
  the Companion neither weakens nor pretends to fix this (`TD-13`).
- Consequence, stated plainly: anyone who can reach the API port can
  assert any configured identity. The platform's deliberate mitigation —
  loopback-only bind, no TLS (`TD-14`) — is inherited **unchanged**; the
  Companion does not widen the bind, and its production reach is
  therefore the host machine or an operator-managed tunnel until
  `FCR-0003`/`FCR-0004` land. Recorded as `TD-58`.
- Failed authentication handling: a missing/blank header is `401`; an
  unconfigured identity resolves fail-closed to zero permissions
  (`ADR-0043`) and every Companion route then serves `403`.

## 2. Authorization

- Two new flat permission keys, enforced per route by the existing
  `IPermissionEvaluator` (`ADR-0044`): `companion.read` for every query,
  `companion.act` for the one action — so an awareness-only principal is
  configurable with no ability to mutate. Proven by integration test
  (`ReadOnlyPrincipal_CanRead_ButCannotAct`).
- The permission check runs before the query/action delegate executes
  (`ApiQueryRequestHandlerTests.HandleAsync_MissingPermission_Returns403_AndNeverExecutes`).
- An out-of-the-box platform configures no roles/principals → every
  Companion route returns `403`. Fail-closed by default, verified
  (`UnconfiguredIdentity_FailsClosed_AsForbidden`).

## 3. Mutation Path

- The single action binds its body to the **existing**
  `SetDocumentStatusCommand` and dispatches through `ICommandDispatcher`
  — the identical handler the desktop Ribbon runs; no new mutation
  mechanism exists to audit separately.
- The binder validates: non-empty body, non-empty GUID, Kind restricted
  to the Documents family (`Document`/`Drawing`/`CadModel` — the action
  cannot be repurposed as a generic lifecycle mutator), and a real
  `LifecycleState` name. Every violation maps to `400` with the reason,
  never `500` (`CompanionBindingTests`, `MalformedQuickAction_…`).

## 4. Audit

- Every authorized Companion request records the existing `api.request`
  audit action with `Method`/`Path`/`CallerIdentityId` — the same
  explicit-caller carriage `ADR-0052` established (the ambient principal
  is never touched; REST-originated audit rows keep `ActorId="unknown"`
  with the true caller in the detail, a pre-existing platform
  characteristic, not a Companion change).

## 5. Data at Rest on the Device

- **No secrets are stored** — the identity model has no credential to
  store. `settings.json` holds a server URL, an identity id, and a theme
  name.
- Cached snapshots (`cache/*.json`) are engineering data (names,
  statuses, attention text). They rest on OS user-profile filesystem
  permissions only — no app-level encryption. Acceptable at today's
  loopback deployment reality; **named precondition** for any off-box
  future: platform-appropriate secure storage, revisited with
  `FCR-0003`.
- Device-loss hygiene: "Clear Local Data" (confirm-gated) deletes every
  snapshot; a 401/403 response also stops cached data being served to a
  refused caller (`CompanionDataServiceTests.DeniedCaller_NeverServedFromCache`).

## 6. Data in Transit

- Plain HTTP, exactly as the platform serves it (`TD-14`). The client
  accepts `https://` URLs already, so a TLS-terminating platform
  (`FCR-0004`) needs no client change.

## 7. Error & Information Exposure

- Server side: unhandled exception detail is logged, never returned
  (`500 "Internal Server Error"` — pre-existing discipline, extended
  unchanged to the query pipeline and proven by
  `HandleAsync_UnhandledException_Returns500_NeverLeakingDetail`).
- Client side: every failure is normalised to `CompanionApiException`
  with a user-presentable message; raw stack traces never reach a
  screen (`ErrorStateView` renders `Message` only; transport detail
  stays in `InnerException` for diagnostics).

## 8. Notifications & Screen Privacy

- Notifications are poll-fetched and rendered only inside the app —
  nothing is pushed to an OS notification surface or lock screen in this
  release, so no notification-privacy exposure exists yet. The
  lock-screen privacy decision is explicitly attached to `FCR-0090`
  (push notifications) for when it becomes real.
- Background-preview/screenshot masking is not implemented (no platform
  head exists to implement it on) — recorded within `TD-57`'s scope for
  the heads Work Package.

## 9. Denial-of-Service Surface

- Query delegates execute reads per request with no caching or rate
  limiting — unchanged from the command surface's own posture and
  acceptable only at loopback exposure; revisit with `TD-58`.
- The client's 10-second timeout and cache fallback keep the phone
  responsive when the platform stalls; cancellation propagates
  (`OperationCanceledException` is rethrown, never mapped).

## 10. Findings Summary

| # | Finding | Disposition |
|---|---|---|
| 1 | No credential verification behind `X-Identity-Id` | Pre-existing, deliberate (`TD-13`); unchanged; bounds deployment via `TD-58` |
| 2 | No TLS | Pre-existing, deliberate (`TD-14`); client is `https`-ready |
| 3 | Cached engineering data unencrypted at rest on device | Accepted at loopback reach; named precondition for off-box (`TD-58`), hygiene paths shipped |
| 4 | No rate limiting on query delegates | Accepted at loopback reach; revisit with `TD-58` |
| 5 | Action surface could grow into a generic mutator | Prevented structurally: binder whitelists the Documents family; any new action is a new, reviewed registration |
| 6 | Lock-screen notification privacy | Not yet applicable (no push); attached to `FCR-0090` |

No finding blocks `WP 14.0A` at the platform's current, deliberate
exposure posture. Findings 1–4 are the existing platform posture
restated against a new client, not new vulnerabilities introduced by it.

## Related Documents

`ADR-0043`, `ADR-0044`, `ADR-0052`, `ADR-0113`, `ADR-0114`, `ADR-0115`;
`Technical Debt Register.md` (`TD-13`, `TD-14`, `TD-57`, `TD-58`,
`AT-24`); `Future Capability Register.md` (`FCR-0003`, `FCR-0004`,
`FCR-0090`); `docs/architecture/TempestOS Companion Architecture.md`.
