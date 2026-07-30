# ADR-0043: Identity Model Scope Is Local-Only, Extensible

## Status

Accepted — `WP 6.1` (Permissions & Identity), 2026-07-29.

## Context

`v0.6.0`'s own architecture package (`docs/releases/v0.6.0/Release
Architecture.md`, `Required ADRs.md`) anticipated this decision but
deliberately left it unratified pending `WP 6.1`'s own implementation
phase. `Threat Model.md` assumptions 4 and 5 already bound this
platform's threat model to a single local user/process; no prior release
has any authentication concept, credential store, or external identity
provider integration. `WP 6.1` is the first Work Package with a genuine
reason to introduce an identity concept at all, and could plausibly
over-scope toward external identity-provider federation (OAuth/OIDC/
SAML) that nothing currently named in `v0.6.0`'s own Work Packages
actually requires — not even `WP 6.3` (REST API), which needs *a*
principal to authorize against, not a federated login flow.

A second question surfaced only during implementation, not anticipated
in the architecture package: what should happen when a caller asks for
a principal by an identity id nothing has configured? The architecture
package's own `Public Interface Catalogue.md` draft did not include an
`IIdentityService` at all — only `IIdentity`, `IPrincipal`,
`ICurrentPrincipalAccessor`, `IPermissionEvaluator`, and `Permission`
were drafted, with the principal-resolution mechanism explicitly
deferred to this Work Package (`Platform Service Contracts.md`'s own
Identity & Permissions section: "the mechanism a future Shell login flow
... must still define").

## Decision

**The identity model is local-only in this release, with no
authentication step.** `IIdentityService.GetPrincipal(identityId)`
trusts the caller-supplied `identityId` outright — there is no password,
token, or credential check of any kind. This is not an oversight; it
mirrors `ADR-0043`'s own scope boundary exactly as anticipated, and
matches every other local-only assumption this platform already makes
(`Threat Model.md` assumptions 4/5).

**Roles and principal-to-role assignment are configuration-sourced, not
administered at runtime.** A role is defined by
`Identity:Roles:{RoleName}:Permissions` (a comma-separated permission
key list); a principal's roles are assigned by
`Identity:Principals:{IdentityId}:Roles`. No administration UI, REST
endpoint, or runtime-mutable store exists for either in this release —
an operator edits configuration directly, exactly as any other
`IConfigurationProvider`-sourced value is set today.

**An identity id with no matching `Identity:Principals:*` configuration
resolves to a principal with zero permissions — fail-closed, not an
error.** `IIdentityService.GetPrincipal` never throws for an
unrecognized identity id; it returns a inert, harmless principal instead.
This was a genuine implementation-phase decision, not anticipated in the
architecture package: the alternative (throwing `IdentityNotFoundException`
for any unconfigured id) would force every caller to handle an exception
for the ordinary, expected case of "an identity nobody has configured
yet" — a worse default than a principal that simply can hold nothing.

**A principal referencing an undefined role throws
`RoleNotFoundException`**, distinct from the above — this is a genuine
configuration defect (an operator typo, a stale reference to a deleted
role), not an ordinary "nobody granted anything" case, and is reported
loudly rather than silently ignored.

**Role model types (`IRole`, `Role`, `IRoleProvider`, `RoleProvider`)
and the identity-resolution service (`IIdentityService`, `IdentityService`)
are additive elaborations, not a revision of any interface the
architecture package actually drafted.** `IIdentity`, `IPrincipal`,
`ICurrentPrincipalAccessor`, `IPermissionEvaluator`, and `Permission` are
all implemented exactly as `Public Interface Catalogue.md` specified,
with zero signature changes.

## Consequences

**Positive:**

- No speculative federation machinery is built for a need no named
  `v0.6.0` Work Package actually has — consistent with this project's own
  engineering discipline against building for hypothetical future
  requirements.
- The fail-closed default for an unrecognized identity means a
  misconfigured or forgotten principal is safely inert rather than either
  raising an exception every caller must handle, or (worse) silently
  granted broad access.
- Configuration-sourced roles reuse `IConfigurationProvider` exactly as
  it already exists — no new persistence dependency, no new
  administration surface to build or secure in this release.

**Negative:**

- There is genuinely no way to change a principal's roles at runtime in
  this release — every change requires editing configuration and
  restarting the running instance (`IConfigurationProvider` is immutable
  once built, Case Study 05). This is a disclosed, accepted limitation,
  not a defect — see this Work Package's own Technical Debt Assessment.
- `IIdentityService.GetPrincipal` trusts its caller completely; nothing
  in this release prevents a caller from asking to become any identity
  id it chooses. This is acceptable only because no untrusted caller
  exists yet (no REST API, no plugin currently calls it) — `WP 6.3`'s own
  architecture phase must revisit this directly before exposing identity
  establishment to a network caller.
- `RoleNotFoundException` is validated lazily, on first
  `GetPrincipal`/`EstablishCurrentPrincipal` call for a given principal —
  not eagerly at Host startup. A configuration typo referencing an
  undefined role is not discovered until something actually resolves that
  principal, which could be later than an operator would like.

## Alternatives Considered

**Building external identity-provider federation now** (OAuth/OIDC/SAML),
"since Identity will need it eventually." Rejected as speculative scope
beyond what any named `v0.6.0` Work Package actually requires — this
project's own engineering discipline explicitly rejects building for
hypothetical future requirements, and no Work Package in this release
names a federation need.

**Throwing for an unrecognized identity id**, mirroring
`ConfigurationKeyNotFoundException`'s own strict behaviour. Rejected —
Configuration's own strictness makes sense because every configuration
key a component reads is expected to exist by contract; the same is not
true of an arbitrary identity id a caller might supply, which is
expected to legitimately not be configured in the ordinary case (nobody
has been granted access yet). Forcing every caller to catch an exception
for that ordinary case would be a worse default.

**A runtime-mutable, administered role/principal store** (in place of
configuration-sourced grants). Rejected for this release — no named
`v0.6.0` Work Package provides an administration surface to manage such
a store safely (that would itself need Settings, `WP 6.4`, and its own
authorization gate), and configuration-sourced grants are sufficient for
this release's own local-only scope.

## Related Documents

`docs/releases/v0.6.0/Release Architecture.md`, `Platform Services
Overview.md`, `Public Interface Catalogue.md`, `Required ADRs.md`
(this decision's own anticipated form); `docs/security/Threat Model.md`
assumptions 4/5; `ADR-0044` (Authorization Enforcement Point, decided
alongside this one); `docs/academy/05 Case Studies/` Case Study 05
(Configuration immutability, the precedent this decision's own
"restart required" trade-off follows).
