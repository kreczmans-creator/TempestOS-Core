# TempestOS Security Posture

## Status

**This document replaces `Threat Model.md` and `Security Roadmap.md` as
the security reference for `v1.0`**, per `WP RC.0C`'s own definition
("Security posture statement... Replaces `Threat Model.md` and `Security
Roadmap.md` for v1.0"), delivered here, brought forward, by `WP 21.5E`
(2026-09-15). Both predecessor documents remain in the repository,
unchanged except for a one-line pointer to this document — they are not
deleted, because their own reasoning (the v0.5.0 assets/actors/trust-
boundary model, and the sequenced roadmap items already resolved by
`WP 13.0A`/`WP 13.2A`) is still the correct history of how TempestOS got
here, and this document does not repeat it.

**Everything below is written from what this Work Package directly
verified against the code as it stands on 2026-09-15** — by reading the
source, by grepping the whole `src/` tree (not only the surfaces this
Work Package's own brief named), and by writing tests that prove specific
claims rather than asserting them — never from `Security Roadmap.md`'s
own prior intentions or from what a design document once said should be
true. Where this document's language differs from `WP RC.0C`'s own
one-line summary (most notably "no listener"), that is because the
verified reality is more specific than the summary, not because the
summary was wrong to write.

## The trust model

TempestOS is a **single-user, local-trust desktop application**. There is
one principal: whoever is signed into the Windows account running the
process. There is no authentication, no session, no multi-user isolation,
and none is simulated — "the user" and "the OS account" are the same
thing, exactly as `Threat Model.md`'s own actors table already stated for
`v0.5.0`, and nothing since has changed it. Every asset this platform
holds — the persistence database, connector tokens, exported artefacts,
logs — is exactly as protected as the Windows account and the disk it
sits on, and no more.

## What an attacker with the laptop can do

Someone who can run `Tempest.Desktop.exe` (or `dotnet run`) as the signed-
in Windows user already has everything that user has: the persistence
database (`tempest.db`, unencrypted at rest — SQLite in WAL mode, no
column or file encryption), every connector token
`WindowsDpapiSecretStore` holds (DPAPI ties ciphertext to the Windows
user, not to a second secret — decrypting it needs only that same user
account, which the attacker already has), every exported artefact, and
every log file. This is not a gap this Work Package found and left open —
it is what "single-user, local trust" means, stated plainly rather than
implied. See "What the operator must do", below, for the one control this
platform depends on to bound it.

A person with the laptop but **not** the Windows account (a locked
screen, a second account on a shared machine) gets nothing this platform
adds beyond what Windows itself already denies them — this platform has
no additional lock of its own, and does not claim to.

## What an attacker who can hand the operator a file can do

TempestOS opens two kinds of file an attacker could craft: an **attachment**
(any file type, attached to an engineering object and later opened) and an
**export/import artifact** (`.json`, this platform's own export format).

- **An attachment whose format has an in-app renderer** (PDF, image, text)
  is rendered by that renderer over its own real bytes — reviewed in
  `Platform Security Review v0.5.0.md`'s tradition of "isolate failure, not
  trust": a malformed file reports an error, never a crash (verified by
  this platform's own existing page-source tests, unchanged by this
  review).
- **An attachment with no in-app renderer** is materialised to a real file
  under the OS temp directory and offered via an **Open externally**
  button. This is where this review's one RED finding lives — see
  Findings, below: the materialised file keeps the attachment's own,
  attacker-chosen name and extension unexamined, and the button hands
  that path to the shell with `UseShellExecute = true`. A stored
  attachment named `invoice.pdf.exe` or `report.lnk` **runs** when a user
  clicks the button, exactly as if they had double-clicked it in
  Explorer. This is filed as RED and is not yet fixed — see Findings for
  why, and for the exact change needed.
- **An export/import artifact** is parsed as JSON (`System.Text.Json`),
  never XML — the whole class of external-entity/entity-expansion attack
  `Threat Model.md` never had cause to name does not apply here by
  construction, not by a mitigation this review added. Nesting depth is
  bounded by `System.Text.Json`'s own default (64). Overall artifact
  **size is not bounded** — see Findings (AMBER): a very large file handed
  to the operator could exhaust memory during import. Schema-version
  migration (`WP 20.3A`) never attempts a best-effort partial import — an
  incompatible or unmigratable section anywhere in the artifact aborts the
  whole import before any of it is applied.

A plugin manifest dropped into the plugins folder is discovered and
parsed (path-traversal-checked since `WP 5.0S`'s own PL-1 fix), but
**nothing loads it** — see "Frozen layers", below.

## What a network attacker between the app and Xero/QuickBooks can do

Nothing this review could find. Every outbound call this platform makes —
confirmed by reading every `HttpClient` construction in the live
codebase, not only the two named connectors — goes to a hard-coded
`https://` endpoint (`api.xero.com`, `login.xero.com`, `identity.xero.com`,
`(sandbox-)quickbooks.api.intuit.com`, `appcenter.intuit.com`,
`oauth.platform.intuit.com`) with no certificate-validation bypass
anywhere in the codebase (`ServerCertificateCustomValidationCallback`,
`DangerousAcceptAnyServerCertificateValidator` and similar were grepped
for across the whole `src/` tree — none exist). A network attacker who
controls a hop between the app and these hosts can, at most, deny service
(drop the connection) — TLS with default validation is what stands
between them and reading or altering the traffic, and nothing in this
codebase weakens it. Bearer tokens travel only in the `Authorization`
header (`XeroConnector.ApplyAuthHeaders`,
`QuickBooksOnlineConnector`'s equivalent), never in a URL or query
string, and the one HTTP diagnostics handler both connectors share
(`InvoicingHttpLoggingHandler`) logs only method, URI and status code —
never a header or a body — so a token cannot leak into this platform's
own log files either.

## The one listener, precisely

`WP RC.0C`'s own summary says "no listener." The precise, verified
statement is: **no persistent listener exists anywhere in this build**.
One **transient** listener exists: `OAuthLoopbackListener`
(`src/Tempest.Core/Invoicing/OAuth/OAuthLoopbackListener.cs`), created
only for the duration of an operator-initiated "Connect" action in
Settings, and disposed the instant that one round trip completes. It:

- binds `127.0.0.1` explicitly (never a wildcard address) — confirmed by
  reading `OAuthLoopbackListener`'s constructor, which builds its
  `RedirectUri` as `http://127.0.0.1:<port>/callback/` and adds exactly
  that prefix to the `HttpListener`;
- accepts exactly one request (`WaitForCallbackAsync` awaits
  `GetContextAsync()` once, is never called again, and the listener is
  disposed by the `using` in `OAuthAuthoriser.AuthoriseAsync` once that
  one request has been answered);
- is checked against a fresh, random, single-use `state` value
  (`Guid.NewGuid()`, generated per call, compared with
  `StringComparison.Ordinal` before any token exchange proceeds — a
  mismatched or missing state is refused, never silently accepted);
- pairs with PKCE `S256` (`PkceGenerator.Generate`, a 256-bit
  `RandomNumberGenerator`-sourced verifier), so even an attacker who
  somehow guessed the state could not complete the exchange without the
  verifier this process alone holds.

The frozen REST API (`Tempest.Core.Api`, `src/Frozen/`) is the only other
code in this repository's history that would ever open a listener, and it
is not part of this build at all — see "Frozen layers".

## Frozen layers: proven, not merely stated, unreachable

The Product Owner's scope clarification for this Work Package asked for
proof, not the freeze README's own word, that three frozen layers are
unreachable in a default build and configuration:

| Layer | Claim | How this Work Package proved it |
|---|---|---|
| REST API (`Tempest.Core.Api`) | The listener does not start unless `Runtime:RestApi:Enabled` is `true` | `RestApiHostedService.StartAsync` reads that key, fail-closed, before constructing any ASP.NET Core object — but this type is not even part of the build (below), so this is belt-and-braces, not the live guarantee. `FrozenLayersUnreachableTests.ADefaultlyConfiguredHost_NeverBindsTheFrozenRestApiDefaultPort` starts a real `ITempestHost` with default configuration and confirms nothing answers a connection on `127.0.0.1:5080` (the frozen service's own documented default port) |
| Plugin loading (`Tempest.Core.Plugins`'s loader/trust half) | Nothing loads a plugin | `TempestHost`'s own Plugin Discovery phase parses manifests (this stays live, deliberately — see below) but never passes them to a loader; `PluginAssemblyLoader`, the one type that ever calls `Assembly.LoadFrom` on a plugin, is not part of the build at all |
| Licensing (`Tempest.Core.Licensing`) | Nothing validates a licence | No live code anywhere references `ILicenseValidator`, `LicenseValidator`, `ILicenseProvider` or `LicenseProvider` — confirmed by grep and by reflection (below) |

**The structural proof, common to all three:** `src/Frozen/` has no
`.csproj` anywhere under it, is referenced by no `<Project>` in
`src/TempestOS.slnx`, and none of the solution's eight real project files
mentions "Frozen" in any item path (`FrozenLayersUnreachableTests.
TheSolution_ReferencesNoProjectUnderSrcFrozenOrTestsFrozen`,
`.NoReferencedCsproj_HasAnItemPathMentioningFrozen`). This is stronger
than a configuration flag: the frozen types do not exist in any assembly
this solution produces. A reflection sweep over every live `src/`
assembly (`Tempest.Core`, `Tempest.Samples`, `Tempest.Workspace`,
`Tempest.Validation`, `Tempest.Harness`, and `Tempest.Desktop` separately
in `Tempest.Desktop.Tests`) confirms none of them declares
`RestApiHostedService`, `ApiRequestHandler`, `PluginAssemblyLoader`,
`PluginSignatureVerifier`, `PluginTrustStore`, `ILicenseValidator`,
`LicenseValidator`, `ILicenseProvider`, `LicenseProvider`, or any of the
other frozen type names
(`FrozenLayersUnreachableTests.NoLiveAssembly_DeclaresAnyFrozenPluginLicensingOrRestApiType`,
`Tempest.Desktop.Tests.FrozenLayersUnreachableFromDesktopTests`).

**Plugin manifest *discovery* is deliberately excluded from this claim** —
it stays live, in `src/Tempest.Core/Plugins/PluginManifestDiscoveryService.cs`,
and `TempestHost` runs it on every startup against `<AppContext.
BaseDirectory>/Plugins` (or a configured root). It reads and parses
whatever manifest JSON it finds there — path-traversal-checked since the
`v0.5.0` review's own PL-1 fix — and records the result, but the
discovered manifests are used for nothing beyond a log line
(`pluginManifests.Count`); they are never handed to a loader, because no
loader exists in this build. A hostile manifest can, at worst, make
Discovery log a rejection or a count; it cannot result in code execution,
because the one type capable of that is absent from every compiled
assembly.

## Findings

Severity: **RED** — exploitable now, with real consequence. **AMBER** —
a real weakness with a precondition. **GREEN** — reviewed, defended,
noted for completeness.

| Severity | Surface | File : line | Status |
|---|---|---|---|
| RED | Open externally hands an unsanitised file name/extension straight to `ShellExecute` | `src/Tempest.Desktop/Viewing/AttachmentViewerLauncher.cs:329-352` (`MaterialiseForExternalOpen`) + `src/Tempest.Desktop/Viewing/DocumentViewerView.cs:247-260` (`DefaultExternalLauncher`) | **Filed as `TD-184`, not fixed** — owned by `WP 21.4A` tonight; see below |
| AMBER | Import artifact stream has no size cap | `src/Tempest.Core/ExportImport/JsonExportFormat.cs:40-68` (`ReadAsync`), `src/Tempest.Core/ExportImport/ImportService.cs:129-133` (`ImportAsync`) | Filed — `TD-185` |
| GREEN | OAuth loopback: loopback-only bind, single response, random single-use state, PKCE S256 | `src/Tempest.Core/Invoicing/OAuth/OAuthLoopbackListener.cs`, `OAuthAuthoriser.cs`, `PkceGenerator.cs` | Reviewed, defended |
| GREEN | `FileSecretStore` discloses its own weaker guarantee in a logged warning on every construction; cannot be selected on Windows (`TempestHost` selects `WindowsDpapiSecretStore` unconditionally when `OperatingSystem.IsWindows()`, no configuration override exists) | `src/Tempest.Core/Secrets/FileSecretStore.cs:37-54`, `src/Tempest.Core/Runtime/TempestHost.cs:853-855` | Reviewed, defended |
| GREEN | Connector HTTP clients pinned to hard-coded `https://` endpoints; no certificate-validation bypass anywhere in the codebase; tokens travel only in the `Authorization` header, never a URL; the shared HTTP logging handler logs method/URI/status only, never headers or bodies | `src/Tempest.Core/Runtime/TempestHost.cs:1180-1231` (`BuildXeroConnector`, `BuildQuickBooksOnlineConnector`), `src/Tempest.Core/Invoicing/OAuth/InvoicingHttpLoggingHandler.cs` | Reviewed, defended |
| GREEN | A macro cannot replay a mutating command against an archived project (`ArchivedProjectCommandGuard`, consulted from `CommandRegistry.Evaluate` for every invocation route including a macro's own `ICommandRegistry.InvokeAsync`), and cannot silently answer a confirmation a live person would have to see (`RunMacroCommandHandler.BuildStepPrompt` returns no prompt for a step whose binding needs a confirmation and whose recording cannot answer one; `CommandRegistry.InvokeAsync` then refuses the step with "needs additional input, and no input surface was supplied" rather than proceeding) | `src/Tempest.Core/Commands/ArchivedProjectCommandGuard.cs`, `src/Tempest.Core/Commands/CommandRegistry.cs:189-218,297-306`, `src/Tempest.Core/Macros/RunMacroCommand.cs:121-178` | Reviewed, defended |
| GREEN | Audit log: the acting principal is resolved automatically for every write (`ICurrentPrincipalAccessor`, defaulting to a recorded `"unknown"` rather than omitting the actor when none is established); no in-app command, API or UI path deletes, truncates or rewrites an audit record — `IAuditRecorder`/`IAuditQuery` expose only write, read and list | `src/Tempest.Core/Audit/AuditRecorder.cs`, `AuditTransactionWriter.cs`, `IAuditQuery.cs`, `IAuditRecorder.cs` | Reviewed, defended (residual note below) |
| GREEN | Attachment content store is hash- and attachment-id-addressed (SHA-256 hex / GUID hex keys into the persistence database) — no externally-controlled string ever reaches a file-system or SQL path; every read is re-verified (size + hash) against what was written | `src/Tempest.Core/EngineeringDomain/Implementation/AttachmentContentStore.cs` | Reviewed, defended |
| GREEN | No SQL injection: every `SqliteCommand.CommandText` in the persistence store is a fixed literal; every value (including free-text search terms passed to FTS5 `MATCH`) is bound via a named parameter, never concatenated into the command text | `src/Tempest.Core/Persistence/SqlitePersistenceStore.cs` (grepped exhaustively for `CommandText =`) | Reviewed, defended |
| GREEN | No unsafe deserialisation anywhere in the live codebase: no `XmlSerializer`, `BinaryFormatter`, `XmlReader.Create` or `XDocument.Parse`/`.Load` over any externally-supplied content exists; every parse of external data (export/import artifacts, settings documents, plugin manifests) uses `System.Text.Json`, whose default `MaxDepth` (64) bounds nesting and which has no entity-expansion mechanism to exploit | grepped exhaustively across `src/` | Reviewed, defended |
| GREEN | Every direct file write in the live codebase lands in one of three places: the persistence database (via `IBinaryPersistenceStore`/`IPersistenceStore`), a fixed subdirectory of the persistence root (`secrets/`, `logs/`), or a destination the operator chose through a real OS save dialog (`IFilePicker.PickSavePathAsync`) — the sole exception is the RED finding above, and no write anywhere reaches outside the persistence root or the OS temp directory | grepped exhaustively for `File.WriteAllBytes(Async)`, `File.WriteAllText(Async)`, `File.Create`, `new FileStream` across `src/` | Reviewed, defended |
| GREEN | No other network listener or `HttpClient` exists anywhere in the live codebase beyond the two named above | grepped exhaustively across `src/` | Reviewed, defended |
| GREEN | Frozen layers (REST API, plugin loading, licensing) are structurally absent from every compiled assembly, and a default-configuration host never binds the frozen REST API's own default port | see "Frozen layers", above | Proven by test |

### `TD-184`: the exact fix Open externally needs

`AttachmentViewerLauncher.MaterialiseForExternalOpen`
(`src/Tempest.Desktop/Viewing/AttachmentViewerLauncher.cs:329-352`)
computes `safeName` via `Path.GetFileName(fileName)` — which correctly
strips any directory component (a `../` escape cannot place the file
outside its own per-attachment temp subfolder) — but never inspects the
resulting extension before writing the file and returning its path.
`DocumentViewerView.OpenExternally` → `DefaultExternalLauncher`
(`src/Tempest.Desktop/Viewing/DocumentViewerView.cs:247-260`) then calls
`Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })`
on exactly that path. For an attachment whose name ends in `.exe`,
`.com`, `.bat`, `.cmd`, `.msi`, `.scr`, `.ps1`, `.vbs`, `.js`, `.jar`,
`.cpl`, `.lnk`, `.reg` or `.hta`, Windows' own shell association for
that extension **runs it** — not "opens it in its own application," the
behaviour every other branch of this feature (a `.dwg`, a `.docx`)
honestly promises.

**The smallest correct fix:** in `MaterialiseForExternalOpen`, after
computing `safeName`, check `Path.GetExtension(safeName)` against a
fixed denylist of directly-executable/shell-dangerous extensions (the
list above is a reasonable start); when it matches, append a neutral,
non-executable suffix (for example `.blocked`) to the written file's own
name before combining it into `path`, so `ShellExecute` finds no
registered handler for `whatever.exe.blocked` and
`DefaultExternalLauncher`'s own existing `catch` reports nothing rather
than launching anything. This is additive to the existing method, uses
the same isolation shape every other "no handler for this" case in the
method already reports, and needs no change to `DocumentViewerView` at
all. A regression test belongs beside the existing
`ADwgAttachment_OpensExternally_RatherThanReportingUnsupported` journey
test in `tests/Tempest.Desktop.Tests/DocumentViewerAcceptanceTests.cs`:
attach a file named (for example) `invoice.pdf.exe`, open it, assert
`Session.MaterialisedPath` does not end in `.exe`.

Not fixed by this Work Package: `src/Tempest.Desktop/Viewing/*` is
`WP 21.4A`'s own "Files you own" tonight, and this Work Package's kill
switch is explicit that a RED fix touching another package's owned files
is recorded, not applied, so the lead can apply it at `WP 21.4A`'s merge.

**Residual note on the audit log (not filed as a finding):** every
mutating service this review read calls `AuditTransactionWriter.WriteAsync`
inside the same transaction as its own write, and no counterexample was
found in the surfaces this review covered — but there is no dedicated,
structural coverage test enumerating every mutating call site in the
codebase and asserting each one audits, the way `NoBlockingPersistenceCallsTests`
does for the blocking-call convention. This review did not find a gap; it
also did not exhaustively prove there is none. Recorded here, not in
`BACKLOG.md`, because it names a verification gap, not a code defect this
review has evidence of.

## What the operator must do

TempestOS's own controls stop at the process boundary — three things sit
outside them entirely, and the platform depends on the operator for all
three:

1. **Disk encryption** (BitLocker or equivalent) on the drive holding the
   persistence root. Without it, `tempest.db` and everything under
   `secrets/` are plain files on disk, readable by anyone who can mount
   or image that drive — DPAPI protects a stolen *copy* of the secrets
   folder (it decrypts to nothing without the matching Windows account),
   but disk encryption is what protects the drive itself from being read
   outside Windows altogether.
2. **A real Windows account, not a shared or guest one**, with a
   screen-lock the operator actually uses. Every asset this platform
   holds is exactly as available as that account.
3. **Backups**, kept somewhere other than the same disk. This platform
   has no ransomware or hardware-failure protection of its own; a backup
   the operator controls is the only recovery path if the persistence
   root is lost, corrupted, or encrypted by something else running under
   the same account.

See `PHYSICAL_REVIEW.md` §8 for the three-line operator checklist this
review adds there.

## What is deliberately not defended

Consistent with `Security Principles.md` Principle 7 ("do not invent
security theatre ahead of a real need"), and unchanged by this review:

- **No authentication, no session, no multi-user isolation.** `Threat
  Model.md` assumptions 4–5 remain not live; nothing here simulates them.
- **No encryption at rest for the persistence database.** `tempest.db`
  and every attachment inside it are plain SQLite, protected only by the
  operator's own disk encryption (above).
- **No defence against the OS-account holder themselves.** An audit
  record, a backup file, or the database itself can all be altered or
  deleted by direct file/database access outside the application — the
  same access level "the operator" already has by definition. This
  platform defends against what the *application's own surfaces* permit,
  not against what the OS account can always do to its own files.
- **No plugin sandbox, code signing enforcement, or third-party plugin
  support** — moot for this release: the loader that would need one is
  not part of the build (see "Frozen layers").
- **No secrets-redaction convention in the logging framework.** One real
  secret class exists today (connector tokens) and this review confirmed
  directly that neither the shared invoicing HTTP handler nor any other
  code path this review found logs a token, header, or bearer credential
  anywhere — but no structural convention (a marker type, a redacting
  `ILogSink`) exists to catch a *future* accidental log statement the way
  `Security Roadmap.md` item 3 originally recommended one before any
  secret existed. This remains open, carried forward rather than
  re-filed, since the trigger condition (a live secret) is now real.

## What tonight's other packages must satisfy

Two packages land on `release/v0.21.0` the same night as this review, each
reviewed here by its own brief (this Work Package does not touch either
package's files — see the Kill switch):

**`WP 21.4A` (the viewer — SVG, markup, tiling).** Beyond fixing `TD-184`
above (it owns the file that needs the fix):

- `Svg.Skia` renders an attachment's own untrusted bytes. Confirm it does
  not fetch or follow an external resource reference an SVG can carry
  (an `<image href="http://...">` or `file://` reference, an XML
  external entity if the underlying parser is XML-based) — an SVG is
  exactly the "attacker hands the operator a file" case this document's
  own findings already cover for every other format, and this is the one
  new parser added tonight.
- The malformed-SVG-reports-an-error-never-a-crash acceptance (already in
  `WP 21.4A`'s own brief, item 1) is a security property as much as a
  reliability one — keep it.
- Annotation records are new, mutable, persisted state
  (`AttachmentAnnotation`) — confirm each write goes through one
  transaction with an audit row, exactly as this document's audit-log
  finding above expects of every mutation.

**`WP 21.5A` (installer, upgrade, backup/restore).**

- The update feed check must default to off, as the brief already
  requires, so nothing phones home unasked — this document's "no
  persistent listener" and "outbound HTTPS only" findings should still
  hold once it ships; confirm the feed URL itself is `https://` with
  default certificate validation, matching every other outbound call this
  document reviewed.
- **Restore from backup** opens an arbitrary file the operator (or
  whoever handed them a `.db` file) chose, for "verified by opening it
  read-only and counting its tables." Confirm the connection string used
  for that open disables SQLite extension loading (`Microsoft.Data.Sqlite`'s
  default `EnableExtensions` is already off — confirm nothing turns it
  on) and that "counting its tables" reads schema metadata
  (`sqlite_master`/`PRAGMA table_list`) rather than executing anything
  the candidate file itself supplies. A crafted `.db` file is exactly the
  "hand the operator a file" threat this document's findings already
  cover for every other format that gets opened.
- The pre-migration backup and the Settings "Back up now"/"Restore from
  backup" actions all write or replace real files — confirm each stays
  under the persistence root or the operator's own file-picker choice, matching
  this document's "every direct file write" finding above, and that
  Restore's own audit row (already required by the brief) records the
  principal exactly as every other mutation here does.

## Dependency scanning

`WP RC.0C`'s second half — "Dependency vulnerability scan is a required
CI check" — is delivered alongside this document: `.github/workflows/ci.yml`'s
`dependency-scan` job runs `dotnet list <solution> package --vulnerable
--include-transitive` and fails the job (and, through it, the `gate` job
every branch-protection rule requires) on any finding, or on a scan
output its own parser (`scripts/check-vulnerable-packages.ps1`) cannot
confirm as clean — silence is never read as "no vulnerabilities."
`.github/dependabot.yml` adds weekly update pull requests for both NuGet
packages and the GitHub Actions used in this repository's own workflows,
so a package does not simply age out of anyone's attention between two
scans. `THIRD-PARTY-NOTICES.md` (beside the root `README.md`) lists every
direct package and its licence. As of 2026-09-15, `dotnet list package
--vulnerable --include-transitive` reports no vulnerable package in any
of this solution's eight projects.

## Related documents

`Threat Model.md` and `Security Roadmap.md` (superseded by this document
for `v1.0`; kept as history, each carrying a one-line pointer here);
`Security Principles.md` (the standing principles this review was judged
against — still current, not superseded); `Platform Security Review
v0.5.0.md` (the last full audit before this one); `Plugin Trust &
Isolation Architecture.md` (the design this document's "Frozen layers"
section assumes, since frozen); `src/Frozen/README.md` (the freeze's own
terms); `BACKLOG.md` (`TD-184`, `TD-185`); `PHYSICAL_REVIEW.md` §8.
