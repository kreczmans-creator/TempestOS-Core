# ADR-0112: Plugin Signing Is a Detached Manifest-and-Assembly Hash Signature, Verified at Plugin Discovery

## Status

Accepted — `WP 13.0A` (Plugin & Registration Trust Isolation
Architecture), 2026-08-13. Resolves the signing half of `Security
Roadmap.md` item 1, and extends `ADR-0025`'s failure classification with
four new categories (`ADR-0025` itself is Accepted/historical and is not
edited).

## Context

`ADR-0110` decided the isolation boundary is capability-scoped
enforcement, gated by a trust tier assigned once, at plugin load time.
That tier assignment needs a real mechanism — the Security Roadmap named
"code-signing verification before load" as one candidate, without
deciding a concrete scheme. `Plugin Manifest Architecture.md`'s own
disclosed Risk ("Security is an accepted, named gap, not a solved
problem… sandboxing, signing, and permissions are all explicit
non-goals") is the exact gap this ADR closes the signing half of.

`ADR-0026` already places Plugin Discovery (Phase 3.1) as a
side-effect-free validation step, strictly before Plugin Loading
(Phase 3.2, the first point an assembly is actually loaded via
`Assembly.LoadFrom`). Any signing scheme that can be verified from file
bytes alone — without loading the assembly into the CLR — fits this
existing boundary without a new phase.

## Decision

**A detached signature over a canonical hash of the manifest (minus its
own `Signature` field) concatenated with a hash of the declared
assembly's file bytes, verified using .NET's own
`System.Security.Cryptography` primitives — no new NuGet dependency,
consistent with `ADR-0005`'s reuse-first mandate — checked entirely
during Plugin Discovery, before any assembly is loaded.**

### Manifest fields — reconciled against the sibling's already-reserved shape

The sibling Plugin Architecture workstream's own manifest v2 design
(`Plugin Platform Architecture.md`) already fixes `PluginManifest.Signature`
as `string?` ("an opaque, encoded blob (algorithm and encoding
undecided here)") and `PluginManifest.Publisher` as `string?` ("free
text, unverified"), explicitly reserving their *semantics* — not their
CLR shape — for this document. This ADR therefore does not request a
richer, object-valued `Signature` field; it defines what the single
opaque string *contains*:

```json
"Signature": "{\"Algorithm\":\"RSA-SHA256\",\"PublisherCertificateThumbprint\":\"<SHA-256 hex, 64 chars>\",\"Value\":\"<Base64-encoded signature bytes>\"}",
"Publisher": "Acme Engineering Ltd."
```

`Signature`'s string value is itself a small, self-contained JSON
envelope (`Algorithm`, `PublisherCertificateThumbprint`, `Value`),
parsed only by this design's own verification logic — Plugin Discovery
otherwise treats the field exactly as the sibling document already
states: read, not interpreted. `Publisher` is **informational only** —
free text, unverified, never itself a trust anchor; trust comes
entirely from `PublisherCertificateThumbprint`-inside-`Signature`
matching an entry in the local trust store. `Signature` absent is a
valid, expected state (Unsigned-Local tier, gated by
`Plugins:AllowUnsignedLoad`); present-but-invalid is never downgraded to
absent. `RequestedCapabilities` (`ADR-0111`) is the sibling's own flat
`IReadOnlyList<string>` field, used directly as this design's capability
key list — no nested object.

### Payload and verification

`hash1 = SHA-256(canonical UTF-8 manifest JSON, Signature field omitted,
System.Text.Json default member-declaration ordering, no indentation)`;
`hash2 = SHA-256(raw bytes of the file named by AssemblyFileName)`;
`payload = hash1 ++ hash2` (64 raw bytes). The publisher signs `payload`
with `RSA.SignData` (RSA-PSS, SHA-256); `Value` (inside the `Signature`
envelope) is that signature, Base64-encoded.

Verification, at Plugin Discovery: parse the `Signature` string as the
JSON envelope above; resolve `PublisherCertificateThumbprint` against
the local trust store; recompute `payload` from the manifest (with its
raw `Signature` field value excluded from the canonical hash, exactly as
the envelope was excluded when originally signed) and assembly bytes on
disk; `RSA.VerifyData` against the matched certificate's public key;
confirm `NotBefore`/`NotAfter` covers "now." No network call, no
CRL/OCSP check. A `Signature` value that fails to parse as the expected
JSON envelope is treated identically to a signature that fails
cryptographic verification (category 15, below) — not a separate,
softer category.

### Trust store and tier assignment

A TempestOS-owned, flat-file store: a `TrustedPublishers/` folder, fixed
convention relative to `AppContext.BaseDirectory`, one `.cer`
(public-only X.509) file per trusted publisher, read once during Plugin
Discovery — mirroring `Plugin Manifest Architecture.md`'s own established
"fixed convention, not configurable yet, purely additive to make
configurable later" precedent for the plugins root and manifest file
name. TempestOS's own first-party publisher certificate ships in this
same folder as a fixed, always-present entry.

Tier assignment, at Plugin Loading (Phase 3.2), is exactly:

| Outcome | Tier |
|---|---|
| No `Signature` field, `Plugins:AllowUnsignedLoad` is `true` | Unsigned-Local |
| No `Signature` field, `Plugins:AllowUnsignedLoad` is `false` (default) | Rejected — new category 16 |
| `Signature` verifies, matched certificate is TempestOS's own | First-Party |
| `Signature` verifies, matched certificate is any other trusted entry | Verified-Signed |
| `Signature` present but fails to verify | Rejected — new category 15, never downgraded to Unsigned-Local |

### Failure classification — extends `ADR-0025`, does not edit it

`ADR-0025`'s own eleven-category table and its "What 'Isolated'
Guarantees, Uniformly" section apply unchanged to every category below —
each is Isolated, never Host-fatal, logged unconditionally, with startup
continuing and every other plugin still attempted.

**Numbered 15–18, not 12–14 — reconciled against the sibling Plugin
Architecture workstream's own `ADR-0107`.** Both this ADR and `ADR-0107`
independently extend `ADR-0025`'s table; `ADR-0107` was written first and
already occupies categories 12–14 (missing/incompatible/circular plugin
dependency). This ADR's own categories continue the single, project-wide
sequence from 15, rather than colliding with an already-claimed range.

| # | Category | Classification | Logging Severity | Notes |
|---|---|---|---|---|
| 15 | Manifest declares a `Signature` that fails to verify (thumbprint not in trust store, signature does not verify against the recomputed payload, or the certificate is outside its validity window) | **Isolated** | Error | Never falls back to Unsigned-Local — a broken signature is treated as tampering, not absence, mirroring how ADR-0025 already distinguishes a malformed manifest (category 2) from a well-formed-but-incompatible one (category 4). |
| 16 | No `Signature` field, and `Plugins:AllowUnsignedLoad` is not enabled | **Isolated** | Warning | Distinct from category 15 — an honest, unsigned plugin correctly declining to run under the operator's current configuration, not a corrupted artifact. Checked at Discovery, before Loading, so the plugin's assembly is never `Assembly.LoadFrom`'d. |
| 17 | Manifest's `RequestedCapabilities` includes a key outside the plugin's assigned tier's ceiling, or a plugin module's constructor requires a service type neither in the fixed always-allowed baseline nor covered by an eligible, granted `plugin.services.resolve:*` declaration | **Isolated** | Warning | Checked entirely within Plugin Loading (Phase 3.2) — the plugin never reaches Module Discovery. See `ADR-0111`. Recorded in the Plugin Registry (`ADR-0107`/`Plugin Platform Architecture.md`) as `PluginRegistryState.TrustDenied` — the sixth registry state that document's own §"The Boundary With Trust & Isolation" reserved for this decision. |
| 18 | A running plugin's own code attempts a capability-gated operation (publish an undeclared event type, register a navigation/command Id, register a DI service) it was not granted | **Isolated** | Warning | Not a Discovery/Loading-phase failure — occurs during Module Lifecycle or later, after the plugin is already `PluginRegistryState.Loaded`. `PermissionDeniedException` is thrown at the `IPermissionEvaluator.RequirePermission` call site exactly as `ADR-0044` already defines; the one operation is blocked, the plugin's module itself is unaffected. Deliberately mirrors `PermissionEvaluator`'s own existing Warning-level convention for an ordinary denied permission check — a new principal kind, not a new severity policy. |

No automatic retry, no silent recovery, and no per-plugin "critical"
opt-in apply to any of these four categories either — `ADR-0025`'s own
"What Is Explicitly Not Introduced" section governs them identically to
its original eleven.

## Consequences

**Positive:**

- Zero new runtime dependency — `System.Security.Cryptography` is
  already part of .NET, matching `ADR-0005`'s existing precedent.
- Verification happens entirely from file bytes, before any
  `Assembly.LoadFrom` call — an untrusted or tampered binary is never
  loaded into the process at all when its signature fails, strictly
  safer than verifying after Loading.
- No new Host Lifecycle phase — fits directly inside the existing
  Plugin Discovery/Loading boundary `ADR-0026` already established.
- The four new failure categories extend `ADR-0025`'s own table format
  and severity-assignment discipline exactly, rather than inventing a
  parallel classification scheme.

**Negative:**

- No revocation checking (CRL/OCSP) — a compromised publisher key
  remains trusted until an operator manually removes its `.cer` file
  from `TrustedPublishers/`. Accepted for this release: this platform
  has no network-facing surface today (`Security Roadmap.md` item 7 is a
  separate, unfired trigger), and building revocation checking against a
  network resource that does not otherwise exist would itself be
  speculative machinery ahead of a real need.
- `Plugins:AllowUnsignedLoad` is a single, global switch — an operator
  enabling it for one legitimately-unsigned internal tool permits every
  other unsigned candidate in the Plugins folder to load under the same
  clamped Unsigned-Local ceiling. A per-plugin allow-list is purely
  additive if real need for finer granularity emerges.

## Alternatives Considered

**Authenticode (Windows-native code signing, `signtool`/Win32 Crypt
APIs).** Seriously considered — it is the platform-conventional Windows
mechanism, and TempestOS's own build artefacts already run on Windows.
Rejected: verifying an Authenticode signature natively from cross-platform
.NET requires either shelling out to a Windows-only tool or a third-party
verification library — a new dependency this project's own reuse-first
discipline (`ADR-0005`) disfavours, for no capability a detached
`System.Security.Cryptography`-based signature does not already provide.
Authenticode also signs the assembly file itself, not the manifest+assembly
pair together — this design's payload deliberately covers both, so a
manifest cannot be swapped out from under a validly-signed assembly (or
vice versa) without invalidating the signature.

**A single hash over the concatenated raw bytes of the manifest file and
the assembly file**, rather than hashing each independently and
concatenating the two hashes. Considered as a simpler alternative.
Rejected: hashing the manifest and assembly independently lets
verification recompute either hash from whichever artefact is on disk
without needing to buffer both files' full raw bytes into one combined
stream, and keeps the manifest's own canonicalisation (JSON field
ordering, the `Signature` field's exclusion) cleanly separate from the
assembly's raw-byte hash — simpler to reason about and to implement
correctly.

**An OS-native or third-party certificate store** (Windows Certificate
Store, a PKCS#12/PFX-backed store) for trusted publishers, rather than a
TempestOS-owned flat-file `.cer` folder. Considered, for platform
convention. Rejected for this release: an OS-specific store API is not
portable and adds real complexity for a local trust model this design
deliberately keeps minimal — mirrors `Plugin Manifest Architecture.md`'s
own precedent of a fixed, simple, filesystem-based convention (the
plugins root directory, the manifest file name) over an OS-integrated
mechanism, with the same "purely additive to make configurable/pluggable
later" escape hatch.

**Falling back to Unsigned-Local when a `Signature` field is present but
fails to verify.** Considered directly, as the more lenient option.
Rejected: a present-but-broken signature is a stronger, more concerning
signal than an honestly-absent one — it indicates either a corrupted
distribution or active tampering, neither of which should be treated as
equivalent to "this plugin's author never claimed a signature at all."
Always rejecting outright (category 15, never category 16) keeps that
distinction real rather than papering over it.

## Related Documents

`ADR-0110` (the isolation-boundary decision this signing mechanism
feeds); `ADR-0111` (the capability model gated by the tier this ADR
assigns); `ADR-0025` (the failure classification table this ADR
extends, unedited); `ADR-0107` (the sibling extension of the same table,
categories 12–14, whose numbering this ADR continues from rather than
collides with); `ADR-0026` (the phase boundary this verification fits
inside); `ADR-0005` (the reuse-first/no-new-dependency mandate);
`Plugin Trust & Isolation Architecture.md`; `Plugin Manifest
Architecture.md`; `Plugin Platform Architecture.md`; `Security Roadmap.md`
item 1.
