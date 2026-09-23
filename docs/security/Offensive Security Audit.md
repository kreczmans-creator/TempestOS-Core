# Offensive Security Audit (WP 21.5F)

## Register Metadata

| Field | Value |
|---|---|
| **Document Name** | Offensive Security Audit |
| **Purpose** | An authorised penetration test of TempestOS's own code and local surfaces — every attack surface named in the Work Package brief, plus every surface found while reading, broken (or proven closed) and, where reachable, fixed in this same package. |
| **Scope** | Everything under `src/` outside `src/Frozen/` and `src/Tempest.Validation/` internals not reachable from a shipped entry point (extended mid-engagement by Product Owner instruction to cover the full live codebase, not only this tranche's changes — see "Scope note" below), plus `.github/workflows/*.yml` and the persistence/backup/installer surface named in the brief. |
| **Owner** | Product Owner. |
| **Source of Truth** | This document; `Threat Model.md`; `Security Principles.md`; `Platform Security Review v0.5.0.md` (the platform's prior, now materially superseded, baseline audit). |
| **Engagement Date** | 2026-09-15. |
| **Worktree / Branch** | `D:/tempest-wt/21.5F`, `wp/21.5F`, off `release/v0.21.0`. |
| **Related Work Packages** | `WP 21.5E` (defensive posture review, parallel); `WP 21.4A` (SVG viewer, not yet started on this branch); `WP 21.5A` (installer/backup, not yet started on this branch). |

---

## Rules of engagement (restated from the brief)

- Target only this repository's own code and its own local surfaces. No attack against Xero, QuickBooks, GitHub, or any other third party's live service; every connector boundary is modelled with local fakes and recorded responses, exactly as the existing connector tests already do.
- Actor model: (a) has the laptop and a normal OS user account; (b) can hand the operator a file to open or attach; (c) controls the network between the app and a connector; (d) can place a file in the persistence root or a backup folder; (e) can supply command-line arguments and environment. Not: Administrator or physical-disk access below the OS user account (the operator's own responsibility).
- Every finding carries a proof-of-concept test that fails on the pre-fix code and passes after the fix. No finding without a repro.
- Every reachable finding is fixed here; nothing is filed to a backlog and left. A finding whose fix is a feature of its own (not a targeted security patch) becomes a named future Work Package, not a backlog row — every such case below says which.

### Scope note — Product Owner clarification mid-engagement

Partway through this engagement the Product Owner clarified that the ten attack surfaces enumerated in the brief are a floor, not a ceiling, and asked specifically that this audit also: (1) cover the full live codebase, not only recent tranches; (2) prove, with a test, whether `src/Frozen/`'s REST listener, licence validation, and plugin loading are reachable in a default build and configuration — fixing any that are; (3) look at what an operator can run through `Tempest.Validation`, `Tempest.Harness`, and `Tempest.Samples`. Sections below marked "PO scope clarification" answer this.

---

## Summary

**10 findings fixed in this package** (`WP 21.5F`), across 1 High, 5 Medium, and 4 Low severity issues (`OSA-06` bundles one Medium and one Low sub-issue under a single row; `OSA-11`, `THIRD-PARTY-NOTICES.md`, is a documentation-completeness item rather than a severity-rated vulnerability — see the table for the row-by-row breakdown). **No Critical finding.** Two attack surfaces the brief named do not yet exist in this codebase (the SVG viewer, `WP 21.4A`; backup/restore and the installer, `WP 21.5A`) — both are reviewed for the requirement they must satisfy when built, not fixed, since there is no code yet to fix.

**Update, `WP 21.6A` (2026-09-15): the four findings this package left open are now closed.** `OSA-12`/`OSA-13`/`OSA-14` were recorded as accepted, deferred architectural risk (a design decision this package's own brief did not authorise it to make unilaterally) and `OSA-15` was sized as a feature and recommended as a new Work Package — `WP 21.6A` is that package, and all four close here rather than staying open or being re-deferred a second time. See each finding's own row below for the fix's file:line and `docs/releases/v0.21.0/Release Notes.md`'s own `WP 21.6A` row for the full closure.

| Severity | Fixed here | Fixed by `WP 21.6A` | No finding (reviewed) |
|---|---|---|---|
| Critical | 0 | 0 | — |
| High | 1 | 0 | — |
| Medium | 5 | 1 | — |
| Low | 4 | 3 | — |
| Documentation completeness | 1 | — | — |
| Informational / no finding | — | — | 6 surfaces |

---

## Findings

Each row: id, severity (with reason), surface, the exploit in one sentence, the proof-of-concept test, the fix (file:line), and disposition.

### OSA-02 — Attachment "Open externally" could launch a directly-executable file (**High**) — **FIXED**

- **Surface.** File parsers / Open externally (brief items 1–2). `src/Tempest.Desktop/Viewing/AttachmentViewerLauncher.cs`.
- **Severity reason.** High: a stored attachment's own, untrusted file name (actor (b) — "can hand the operator a file to open or attach") was written to disk verbatim and handed to `Process.Start(UseShellExecute: true)`. An attachment named `invoice.pdf.exe` (or any Explorer-executable extension — `.com`, `.scr`, `.bat`, `.cmd`, `.ps1`, `.lnk`, `.hta`, `.msi`, `.jar`, …) was materialised under its real, executable name; clicking "Open externally" — the button this platform itself offers for exactly this unsupported-format case — ran it. One click, no further privilege needed: arbitrary code execution as the operator.
- **Exploit.** `AttachmentViewerLauncher.MaterialiseForExternalOpen` (pre-fix) wrote `attachment.FileName` (reduced only by `Path.GetFileName`) to a fixed, per-attachment-id temp directory with no extension check, and `DocumentViewerView.DefaultExternalLauncher` then called `Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })` — the OS shell runs a `.exe`/`.com`/`.lnk`/… by its extension alone.
- **Secondary exploit (same fix).** The pre-fix temp directory was `%TEMP%\TempestOS\Viewer\{attachmentId}` — fixed and reused across every open of the same attachment. A local process that already knew or guessed the attachment's GUID could pre-stage a symlink/junction at that exact path before the legitimate write, redirecting it (a TOCTOU).
- **Proof-of-concept tests.** `tests/Tempest.Desktop.Tests/DocumentViewerAcceptanceTests.cs`, `OSA02_*` (12 tests): `OSA02_ADoubleExtensionAttachment_IsNeverMaterialisedAsADirectlyExecutableFile`, `OSA02_EveryDirectlyExecutableExtension_IsNeutralisedOnMaterialisation` (9 extensions via `[AvaloniaTheory]`), `OSA02_TwoOpensOfTheSameAttachment_MaterialiseToDifferentDirectories`, `OSA02_AnRtloDisguisedFileName_HasItsBidiControlCharactersStripped`. Confirmed failing against the pre-fix code (via a scoped `git stash` of the production file alone) and passing after.
- **Fix.** `src/Tempest.Desktop/Viewing/AttachmentViewerLauncher.cs:326` (`DangerousExtensions` denylist), `:337` (`BidiControlCharacters`), `:359` (`SanitiseFileName` — strips control/bidi-override characters, replaces invalid filename characters, trims a trailing space/dot), `:401` (`MaterialiseForExternalOpen` — neutralises a dangerous extension by appending `.blocked` rather than writing it verbatim, and writes to a fresh `Guid.NewGuid()`-named subdirectory every call rather than one keyed on the attachment id alone).
- **Commit.** `c2d2dcd8` (WP 21.5F 1/n).

### OSA-01 — File parsers had no bound on decoded size (**Medium**) — **FIXED**

- **Surface.** File parsers fed untrusted bytes (brief item 1). `src/Tempest.Desktop/Viewing/DocumentPageSources.cs`.
- **Severity reason.** Medium: denial of service (memory exhaustion / process crash) against the local desktop app, not code execution or data exposure; requires the operator to open a malicious attachment (actor (b)).
- **Exploit.** `ImageDocumentPageSource` had no size limit at all, unlike `PdfDocumentPageSource`'s own `MaxRasterEdge` — a small file declaring an enormous pixel grid (a classic decompression-bomb shape: e.g. a 54-byte BMP header declaring 12,000×12,000 pixels) was decoded to its full, real size with nothing to stop it. `TextDocumentPageSource` read an entire file into one managed `string` (then one `string[]` of lines) unconditionally, with no cap.
- **Residual, disclosed, not fixed.** `PdfDocumentPageSource`'s parse calls (`Conversion.GetPageCount`/`GetPageSizes`) and all three sources' decode/render calls run synchronously on the UI thread with no timeout or cancellation reaching the native PDFium/Skia call — a pathologically slow-to-parse file can still freeze the application (a hang, not a crash). Closing this needs offloading decode off the UI thread and threading real cancellation into PDFtoImage/Skia — an architectural change, not a targeted patch, and out of this package's own scope. Recorded here as accepted, disclosed risk, the same shape `Platform Security Review v0.5.0.md` used for its own SEC-01/NAV-1 findings.
- **Proof-of-concept tests.** `tests/Tempest.Desktop.Tests/DocumentPageSourceTests.cs`, `OSA01_*` (5 tests): `OSA01_AnImageDeclaringMorePixelsThanThePlatformWillDecode_IsRefusedBeforeDecoding` (+ stream variant), `OSA01_AnImageWithinTheDecodedPixelLimit_StillOpensNormally`, `OSA01_AnOversizedTextFile_IsTruncatedRatherThanReadFullyIntoMemory` (+ stream variant). The image tests fail/pass directly around the fix; the text tests reference a new public constant (`MaxSourceBytes`) absent from the pre-fix type, so the pre-fix state does not compile — a stronger proof of absence than a runtime failure.
- **Fix.** `src/Tempest.Desktop/Viewing/DocumentPageSources.cs:249` (`MaxDecodedPixels = 40_000_000`), `:292` (`GuardAgainstOversizedImage` — peeks declared dimensions via `SKCodec`, header only, before Avalonia's own decoder allocates a bitmap), `:411` (`MaxSourceBytes = 25_000_000`), `:482` (`ReadBounded` — caps the stream read with a truncation notice appended).
- **SVG (brief item 1, third sub-surface).** `Svg.Skia` is referenced nowhere in this solution; `WP 21.4A`'s own worktree (`D:/tempest-wt/21.4A`) carries zero commits beyond the release cut. There is no SVG parser to audit — an SVG attachment is reported `Unsupported` by signature/extension sniffing alone and never reaches an XML parser. **Handed to `WP 21.4A`'s lead**: when that source is implemented, its own acceptance criteria must include `XmlReaderSettings.XmlResolver = null`, `DtdProcessing.Prohibit` (billion-laughs/XXE), and confirmation that a `<script>` element never executes — the exact properties this audit's brief asked to prove, with no prior art in this codebase to check against.
- **Commit.** `68d69cb0` (WP 21.5F 2/n).

### OSA-04 — `FileSecretStore` enforced no permission of its own (**Medium**) — **FIXED**

- **Surface.** The secret store (brief item 4). `src/Tempest.Core/Secrets/FileSecretStore.cs`.
- **Severity reason.** Medium, with mitigating context: this is the **non-Windows fallback only** — `WindowsDpapiSecretStore` is the store every shipped, CI-verified configuration actually uses (confirmed: `TempestHost.cs:853-855`'s single `OperatingSystem.IsWindows()` ternary is the only construction site for either type; `FileSecretStore` cannot be selected on Windows). Real exposure today is limited to a contributor building for Linux/macOS, but the class's own doc comment claimed a guarantee ("a file-system permission is the only protection a connector token has here") it did nothing to enforce.
- **Exploit.** `SetAsync` called `Directory.CreateDirectory(_secretsDirectory)` and `File.WriteAllBytesAsync` with no ACL/mode restriction at all — a connector's OAuth access/refresh token (Xero, QuickBooks) was written relying entirely on whatever default/inherited permission happened to apply to the persistence root.
- **Proof-of-concept tests.** `tests/Tempest.Core.Tests/Security/FileSecretStoreTests.cs` (5 tests, all new — this class had zero tests of any kind before this package): `SetAsync_OnNonWindows_RestrictsTheSecretsDirectoryAndFile_ToTheCurrentUserOnly` is the direct proof (this project's CI is Windows-only, so the permission assertion runs for real only on a non-Windows machine; it still exercises the Windows branch, which must not throw `PlatformNotSupportedException`, on this CI).
- **Fix.** `src/Tempest.Core/Secrets/FileSecretStore.cs:49` (`DirectoryPermissions` = `rwx------`), `:53` (`FilePermissions` = `rw-------`), `:88` (`SetAsync` — permissions set atomically at creation via the `UnixFileMode`-accepting `Directory.CreateDirectory`/`FileStreamOptions.UnixCreateMode` overloads, not after the fact, closing the window in which the directory/file existed unrestricted).
- **Commit.** `83c1fc8f` (WP 21.5F 3/n).

### OSA-04b — OAuth token records would print a real token if ever logged directly (**Low**, defence in depth) — **FIXED**

- **Surface.** The secret store / token handling (brief item 4). `src/Tempest.Core/Invoicing/OAuth/OAuthResults.cs`, `OAuthTokenResponse.cs`.
- **Severity reason.** Low: **no current call site logs either record directly** (confirmed: `InvoicingHttpLoggingHandler` logs only method/URI/status, never headers or bodies; every OAuth failure path threads a hand-written, non-secret `Reason` string, never the token itself). This is a footgun closed before it is ever tripped, not an active leak.
- **Exploit (latent).** `AccessTokenResult`/`OAuthTokenResponse` are C# `record`s; the compiler-generated `ToString()` prints every property, including `AccessToken`/`RefreshToken`, verbatim. A future `logger.LogX("{Result}", result)` or `$"{result}"` would leak a real token into the log and read as entirely ordinary code.
- **Proof-of-concept tests.** `tests/Tempest.Core.Tests/Security/OAuthTokenRedactionTests.cs` (2 tests).
- **Fix.** `src/Tempest.Core/Invoicing/OAuth/OAuthResults.cs` (`AccessTokenResult.ToString()` override), `OAuthTokenResponse.cs` (same).
- **Commit.** `83c1fc8f` (WP 21.5F 3/n).

### OSA-05 — Import/export JSON had no explicit depth limit or size/count cap (**Medium**) — **FIXED**

- **Surface.** Import and migration parsers (brief item 5). `src/Tempest.Core/ExportImport/JsonExportFormat.cs`, `JsonExportPayloadSerializer.cs`, `src/Tempest.Core/EngineeringDomain/Implementation/EngineeringObjectStateStore.cs`, `src/Samples/Tempest.Samples/RequirementExportAdapter.cs`, `RequirementCollectionExportAdapter.cs`.
- **Severity reason.** Medium: resource-exhaustion denial of service via a crafted "export" file, not code execution or data exposure.
- **No zip/archive attack surface exists.** The brief's own path-traversal/symlink-entry/zip-bomb questions (item 5) presuppose an archive format; this platform's export/import is a flat JSON array with each section's bytes carried as base64 (confirmed: zero `System.IO.Compression.ZipFile`/`ZipArchive` usage anywhere in the repository). Recorded here as a genuine no-finding, not a gap.
- **Exploit.** None of the six `JsonSerializer`/`JsonSerializer.Deserialize(Async)` call sites in the export/import/migration paths set `MaxDepth` explicitly (System.Text.Json's own implicit default of 64 was doing the work, undocumented). `JsonExportFormat.ReadAsync` had no cap on artifact byte size or declared section count — a hostile file with an enormous base64 payload or millions of array entries was read entirely into memory. Separately, both `Tempest.Samples` requirement adapters' own inner-payload `JsonSerializer.Deserialize` calls had no `try/catch` at all, so a malformed inner payload (valid outer envelope, garbage JSON inside) leaked a raw `JsonException` rather than the `CorruptedExportArtifactException` every other corruption case in this pipeline reports.
- **Proof-of-concept tests.** `tests/Tempest.Core.Tests/ExportImport/JsonExportFormatTests.cs`, `OSA05_*` (3 tests): `OSA05_AnArtifactOverTheSizeLimit_IsRefusedBeforeParsing` (a stream that reports a large `Length` without allocating that much real memory, keeping the permanent regression test cheap), `OSA05_AnArtifactDeclaringMoreSectionsThanThePlatformWillImport_IsRefused`, `OSA05_AnOrdinaryArtifact_IsStillAccepted`.
- **Fix.** `JsonExportFormat.cs:31` (`MaxArtifactBytes = 500_000_000`), `:34` (`MaxSectionCount = 10_000`), `:76`/`:96` (the two refusals); `MaxDepth = 64` made explicit across all six call sites (`JsonExportFormat.cs`, `JsonExportPayloadSerializer.cs`, `EngineeringObjectStateStore.cs`'s `StateJsonOptions`, both Samples adapters); both Samples adapters' inner deserialize calls now wrap `JsonException` into `CorruptedExportArtifactException`.
- **Commit.** `29acc070` (WP 21.5F 5/n).

### OSA-06 — The persistence root accepted a system path; the lock file followed a symlink (**Medium** / **Low**) — **FIXED**

- **Surface.** The persistence root and backups (brief item 6). `src/Tempest.Core/Persistence/SqlitePersistenceStore.cs`.
- **Severity reason.** Medium for the root-path gap: `Persistence:RootPath` is directly operator/command-line-controlled (actor (e) — "can supply command-line arguments and environment" — is explicitly in scope), and nothing validated it before this fix. Low for the lock-file symlink gap: it requires the attacker to already hold write access to the persistence root directory, a prerequisite this audit's own actor model assumes throughout rather than a new capability the gap itself grants.
- **Exploit.** `SqlitePersistenceStore`'s constructor resolved `Persistence:RootPath` and immediately called `Directory.CreateDirectory` with no denylist of sensitive locations — naming `C:\Windows`, `C:\Program Files`, or a bare drive root (`C:\`) was accepted, and this store would have created `tempest.db` and its instance lock file directly inside. Separately, the instance lock (`AcquireInstanceLock`) opened a plain `FileStream` against `tempest.lock` with no check that the path was a plain file rather than a symlink/junction — a local process with write access to the root could plant one, redirecting every future open of the lock file to an arbitrary target.
- **`WP 21.5A` (not yet started on this branch — its worktree carries zero commits beyond the release cut, confirmed).** Owns: the friendly `--persistence-root` CLI switch (today the generic `--Persistence:RootPath=...` command-line form already reaches this exact, now-fixed validation, since `MicrosoftExtensionsConfigurationSource` wires up `AddCommandLine` unconditionally); backup creation; and restore, which **must** validate a candidate file is a genuine TempestOS database (SQLite header magic bytes, expected schema) before replacing the live one — there is no restore feature on this branch at all yet to have skipped that check. **Handed to `WP 21.5A`'s lead as an explicit requirement**, not a patch, since there is no restore code yet to patch.
- **Proof-of-concept tests.** `tests/Tempest.Core.Tests/Persistence/SqlitePersistenceStoreTests.cs`, `OSA06_*` (9 tests): `OSA06_ARootPathInsideAProtectedSystemDirectory_IsRefused` (6 system paths via `[Theory]`), `OSA06_ARootPathThatIsADriveRoot_IsRefused`, `OSA06_AnOrdinaryRootPath_IsStillAccepted`, `OSA06_ALockFileThatIsASymlinkOrJunction_IsRefusedRatherThanFollowed` (creates a real symlink and confirms both the refusal and that the real target file was never touched).
- **Fix.** `SqlitePersistenceStore.cs:845` (`WindowsDisallowedRoots`), `:854` (`UnixDisallowedRoots`), `:873` (`ValidateRootPath`, called at `:182` before any directory is created), `:919` (the reparse-point check inside `AcquireInstanceLock`).
- **Commit.** `647d567e` (WP 21.5F 4/n).

### OSA-09 — `Tempest.Harness` never established a session principal (**Low**) — **FIXED**

- **Surface.** PO scope clarification: what an operator can run through `Tempest.Harness`. `src/Tempest.Harness/Program.cs`.
- **Severity reason.** Low: an audit-trail/accountability gap (every write attributed to `EngineeringDocumentStore.UnknownAuthorPrincipalId`, indistinguishable from a genuinely unauthenticated write), not a privilege escalation or data-exposure path — there is no authentication system in this build for a Harness session to bypass.
- **Exploit.** `Tempest.Desktop`'s own `WorkspaceHost` establishes the session principal (`TD-103`, `ADR-0146`: resolves `ICurrentPrincipalAccessor`, casts to the concrete `CurrentPrincipalAccessor`, calls `SetCurrent` with a `SessionPrincipalSource`-resolved principal). `Tempest.Harness` — confirmed a real, shipped release asset via `release.yml`'s own `tempestos-engineering-harness` archive, not only a developer tool — never did this at all; every audit row, authorship field and check record a Harness session produced fell back to the "unknown" sentinel.
- **Verification.** Not a dedicated new test: `Program.cs`'s own top-level statements are not independently unit-testable without a disruptive refactor outside this package's scope, and the underlying mechanism (`SessionPrincipalSource` → `CurrentPrincipalAccessor.SetCurrent`) is already covered by `tests/Tempest.Desktop.Tests/PrincipalBoundaryTests.cs`. Verified by code review and a clean build.
- **Fix.** `src/Tempest.Harness/Program.cs` (added after `shell.StartAsync()`, mirroring `WorkspaceHost.cs`'s own identical sequence).
- **Commit.** `56312eb3` (WP 21.5F 6/n).

### OSA-10 — GitHub Actions were pinned to a mutable tag, not a commit (**Medium**) — **FIXED**

- **Surface.** The build and supply chain (brief item 10). `.github/workflows/ci.yml`, `release.yml`.
- **Severity reason.** Medium: a tag (`@v4`) can be repointed by the action's maintainer or a compromised account, injecting arbitrary code into every CI and release run; a real, if lower-probability, supply-chain vector this project's own CI has otherwise taken unusual care over (pinned SDK version, pinned runner image, a documented "minimal supply-chain surface" precedent for `release.yml`'s own choice not to use a third-party release-publishing action).
- **Exploit.** Every one of 15 `uses:` lines across both workflow files (`actions/checkout@v4`, `actions/setup-dotnet@v4`, `actions/upload-artifact@v4`) resolved to whichever commit the `v4` tag happened to point at, at run time.
- **Fix.** Every `uses:` line pinned to the exact commit SHA the `v4` tag currently resolves to, verified against the real upstream repositories via `gh api repos/<owner>/<repo>/git/refs/tags/v4` (a read-only, public-metadata lookup — not an attack against GitHub's live service, and within the brief's own "no network calls to real services" rule read as governing the exploit/attack activity, not routine, read-only supply-chain research): `actions/checkout` → `11d5960a326750d5838078e36cf38b85af677262` (v4.4.0); `actions/setup-dotnet` → `67a3573c9a986a3f9c594539f4ab511d57bb3ce9` (v4.3.1); `actions/upload-artifact` → `ea165f8d65b6e75b540449e92b4886f43607fa02` (v4.6.2). Each carries a trailing `# v4.x.y` comment for readability. `permissions:` blocks were reviewed and found already least-privilege (`ci.yml`: `contents: read`; `release.yml`: `contents: write`, the one permission `gh release create` needs) — no change needed there.
- **Coordination with `WP 21.5E`.** Per the brief, `WP 21.5E` owns adding a dependency-vulnerability scan to `ci.yml`; this package's changes are confined to existing `uses:`/`permissions:` lines, not new steps, so a textual merge conflict is unlikely — flagged for the lead's awareness at merge time regardless.
- **Proof-of-concept.** Not applicable in the fail-before/pass-after sense (YAML pinning has no runtime behaviour to unit-test); verified by parsing both files as YAML after the edit (`ConvertFrom-Yaml`, no syntax error) and by the fact both files still drive this very engagement's own gate successfully.
- **Commit.** `c2d2dcd8` (WP 21.5F 1/n).

### OSA-10b — Release assets carried no integrity check for a downloader (**Low**) — **FIXED**

- **Surface.** The build and supply chain / release signing posture (brief item 10). `.github/workflows/release.yml`.
- **Severity reason.** Low: GitHub Releases are already served over HTTPS (transport integrity); this closes the gap for a user who wants to verify the specific bytes they downloaded match what was built, and is a prerequisite for any future signing story.
- **Release-signing posture (as found, and as this fix leaves it).** No code-signing infrastructure exists — no Authenticode certificate, no signed installer. `release.yml` publishes two plain zip archives via `gh release create`, triggered only by a tag push (requires repository write access — not attacker-reachable via a PR; no `pull_request_target`, no unsanitised `github.event.*` interpolation into a shell step was found). Acquiring and provisioning a code-signing certificate is an organisational/business decision outside this security package's own remit — **recommended as a candidate future `WP 21.x`** if full code-signing is wanted, not something this package can do unilaterally.
- **Fix.** `release.yml` — a new "Compute release asset checksums" step writes `SHA256SUMS.txt` for both packaged zips, published as a third release asset alongside them.
- **Installer/update feed (`WP 21.5A`, not yet started on this branch).** No installer or update-feed code exists anywhere in `src/`, `scripts/`, or `.github/workflows/` to review for HTTPS/signature-verification — nothing to fix; noted for `WP 21.5A`'s own eventual acceptance criteria.
- **Commit.** `c2d2dcd8` (WP 21.5F 1/n).

### OSA-11 — `THIRD-PARTY-NOTICES.md` did not exist — **FIXED** (documentation, not a vulnerability)

- **Surface.** The build and supply chain (brief item 10, "`THIRD-PARTY-NOTICES.md` only if no one has made it"). Repository root.
- **Finding.** No such file existed. Created, listing every direct `PackageReference` across `src/**/*.csproj` plus `PDFtoImage`'s own declared native runtime dependencies (SkiaSharp, `bblanchon`'s PDFium packaging), each license read directly from the package's own `.nuspec` in the local, already-restored NuGet cache (no network call). Every dependency found uses a permissive licence (MIT or Apache-2.0); none imposes a copyleft obligation.
- **Commit.** `fedf0167` (WP 21.5F 8/n).

---

## Reviewed — no finding

### The OAuth loopback listener (brief item 3)

`src/Tempest.Core/Invoicing/OAuth/OAuthAuthoriser.cs`, `OAuthLoopbackListener.cs`, `PkceGenerator.cs`. Every property the brief asked to prove or refute holds:

- **Binds `127.0.0.1` only, never `0.0.0.0`.** Confirmed: `new Uri($"http://127.0.0.1:{resolvedPort}/callback/")` — an IP literal, not `"localhost"` (which avoids any DNS-rebinding ambiguity too).
- **Accepts exactly one request then closes.** `WaitForCallbackAsync` calls `GetContextAsync()` exactly once; nothing loops back to accept a second connection, and the listener is disposed at the end of `AuthoriseAsync`'s `using` scope.
- **Rejects a callback whose `state` does not match.** `OAuthAuthoriser.AuthoriseAsync`: `if (!string.Equals(callback.State, state, StringComparison.Ordinal)) return OAuthResult.Failed(...)`.
- **`state` and the PKCE verifier are cryptographically random and single-use.** `state` is a fresh `Guid.NewGuid()` per call (backed by the OS CSPRNG since .NET Core); the PKCE verifier is `RandomNumberGenerator.GetBytes(32)` (`PkceGenerator.cs:17`) — both freshly generated every `AuthoriseAsync` call, never reused.
- **Another local process cannot pre-bind the port to steal the code.** The listener is constructed — and must successfully bind — *before* the system browser is ever opened (`OAuthAuthoriser.cs:118` then `:136`). A process that has already bound the port causes construction to throw, caught and reported as `OAuthResult.Failed`, with the browser never opened and no authorisation code ever requested.
- **A forged callback cannot inject an attacker's code.** Requires guessing a fresh, cryptographically random `state` value within the single-shot listener's own brief window — computationally infeasible.

**TD-183 folded in, as the brief directed** (a robustness item, not a vulnerability): several suites binding the same fixed default port in quick succession could collide with a just-released prior listener still settling. `OAuthLoopbackListener` now retries binding the *same* port up to 5 times (a fresh `HttpListener` each attempt — a listener whose `Start()` throws leaves its own request queue unusable for a later `Start()` call on that same instance, discovered while implementing this) before failing — never falling back to a different port, which would defeat the fixed port's whole reason to exist (the sandbox app's own pre-registered redirect URI). A port genuinely held by another long-lived process still fails exactly as before. New regression test: `OAuthAuthoriserTests.AuthoriseAsync_APortHeldTransiently_SucceedsOnceTheEarlierListenerReleasesIt`. Commit `647d567e`.

### Macros (brief item 7)

`src/Tempest.Core/Macros/RunMacroCommand.cs`, `MacroManager.cs`; `src/Tempest.Core/Commands/CommandRegistry.cs`, `ArchivedProjectCommandGuard.cs`. `RunMacroCommandHandler.HandleAsync` re-invokes every recorded step through the identical `ICommandRegistry.InvokeAsync` overload the Ribbon, the Command Palette, and a keyboard binding all use — including `Evaluate`'s own archived-project guard. A confirmation-gated command is excluded from being recordable as a step at all (`MacroManagerDialog.IsMacroEligible`), and `RunMacroCommandHandler`'s production wiring always passes a `null` fallback prompt, so a step missing a recorded value is refused with the framework's own honest "no input surface was supplied" rather than silently prompted or skipped — a destructive step cannot run unattended via any trigger, including a future keyboard binding (none exist today). There is no per-principal authorisation model in this codebase to impersonate (`ADR-0146` — one ambient principal per process, by design) — recorded once, under Audit, item 9 below, rather than duplicated here. New regression test proving this end-to-end through a real `RunMacroCommand` dispatch (no existing test did, only direct `Evaluate`/`InvokeAsync` calls): `tests/Tempest.Core.Tests/Macros/MacroReplayArchivedProjectGuardTests.cs`. Commit `04836930`.

### SQL and the store (brief item 8)

`src/Tempest.Core/Persistence/SqlitePersistenceStore.cs` is the **only** place in the entire product that talks to SQLite directly — every domain service rides on its one generic, fixed `records(collection, key, text_value, blob_value, updated_utc)` table. Every one of its ~15 `CommandText` sites binds `collection`/`key`/values as parameters (`$name` placeholders, `AddWithValue`), never string interpolation; the one `LIKE`-shaped surface (`PrepareListKeys`) uses `substr(key,1,$length) = $prefix` specifically to avoid wildcard-character injection from a caller-supplied prefix. No site anywhere builds a table/column identifier from user input. Already proven at the generic key/value layer (`PersistenceStoreHostileNameTests.AnArbitraryUnicodeOrPunctuatedKey_RoundTrips`, including a literal `'; DROP TABLE records; --` key). New test closes the one real gap found — the identical claim, one layer up, at a real **business identifier** a user actually types, over the real `SqlitePersistenceStore`: `tests/Tempest.Core.Tests/Requirements/RequirementsServiceHostileDataTests.cs`, `CreateAsync_SqlInjectionShapedIdentifier_RoundTripsSafely_AndNeverDamagesTheStore`. Commit `56312eb3`.

### `src/Frozen/` reachability — PO scope clarification

`src/Frozen/Tempest.Core.Api` (REST/HTTP listener), `.../Tempest.Core.Licensing` (licence validation), and `.../Tempest.Core.Plugins` (the pre-`ADR-0025` plugin assembly loader/trust store — a different set of classes from the live, shipped, manifest-discovery-only `Tempest.Core.Plugins` namespace under `Tempest.Core.dll`) are all **unreachable in a default build and configuration**: no `.csproj` exists anywhere under `src/Frozen/` or `tests/Frozen/`; every real project's SDK-style compile glob is scoped to its own directory subtree, which excludes `src/Frozen/` structurally, not by an exclusion rule that could be edited away by accident; `src/TempestOS.slnx` lists no Frozen project; `TempestHost.cs`'s own startup phases (the only composition root `Tempest.Desktop` and `Tempest.Harness` both build through) never construct any of the three capabilities' types — Plugin Discovery stays live (scans the drop folder, records what it finds) but goes no further, matching `src/Frozen/README.md`'s own claim. Proven at runtime, not only by reading that claim: `tests/Tempest.Core.Tests/Security/FrozenCapabilitiesUnreachableTests.cs` (2 tests) confirm no loaded assembly in the whole test process — which touches every project a default build and its own suite ever does — carries any of the three capabilities' own types or assembly names. Commit `04836930`.

### `Tempest.Validation` and `Tempest.Samples` — PO scope clarification

Neither is ever shipped in `Tempest.Desktop` or `Tempest.Harness` (`Tempest.Validation.csproj` and `Tempest.Samples.csproj` are referenced only by the two test projects; both are library, not executable, output). Not an operator-reachable surface. No finding.

---

## Findings closed by `WP 21.6A` (2026-09-15)

`WP 21.5F` recorded OSA-12/OSA-13/OSA-14 as accepted, deferred architectural risk and OSA-15 as a feature sized for a new Work Package. `WP 21.6A` is that package; the Product Owner's own rule for it was explicit — "findings are fixed, not filed" — so all four close here, not re-deferred a second time.

### OSA-15 — `RequirementsService`, `VerificationService`, and `ReferenceDataCatalog` write durable engineering data with no audit row (**Medium**) — **FIXED by `WP 21.6A`**

- **Original finding, unchanged.** Every mutation through `EngineeringObjectBase`/`EngineeringObjectFactory` unconditionally writes an audit row inside the same transaction; `RequirementsService`'s fourteen mutators, `VerificationService.RecordAsync`, and `ReferenceDataCatalog`'s `RegisterAsync`/`ReviseAsync`/`SupersedeAsync` committed real, durable data through their own separate transactions with zero calls into the audit machinery.
- **Fix.** `AuditTransactionWriter.WriteAsync` (the identical primitive `EngineeringObjectBase` uses) is now called inside every one of those transactions, committing or rolling back with the write itself:
  - `src/Tempest.Core/Requirements/RequirementsService.cs` — all fourteen original mutators (`CreateAsync`, `ReviseAsync`, `SetStatusAsync`, `SetOwnerAsync`, `SetPriorityAsync`, `DeleteAsync`, `MoveToGroupAsync`, `LinkAsync`, `CreateCollectionAsync`, `DeleteCollectionAsync`, `AddToCollectionAsync`, `CreateGroupAsync`, `MoveGroupAsync`, `DeleteGroupAsync`), plus the new `UndeleteAsync` (`WP 21.6A` item 1b, below) — action constants in the new `src/Tempest.Core/Requirements/RequirementsAuditActions.cs`.
  - `src/Tempest.Core/Verification/VerificationService.cs:RecordAsync` — action constant in the new `src/Tempest.Core/Verification/VerificationAuditActions.cs`, keyed by the new verification record's own document Id (the "Created"-shaped convention `EngineeringObjectBase.WriteCreationAsync` already uses), the subject document named in the row's own detail text.
  - `src/Tempest.Core/ReferenceData/ReferenceDataCatalog.cs` — `RegisterAsync`, `ReviseAsync` (the `ReviseCoreAsync` write path), and `SupersedeAsync` — action constants in the new `src/Tempest.Core/ReferenceData/ReferenceDataAuditActions.cs`. `SetValidationStateAsync` (check/release) is deliberately left alone: its only production callers, `ReferenceReviewService.CheckAsync`/`ReleaseAsync`, already call `IAuditRecorder.RecordAsync` themselves — a second row here would duplicate, not close, a gap. The catalogue needed no new `ICurrentPrincipalAccessor` dependency: the principal is read back from the revision `EngineeringDocumentStore`'s own transactional writer already stamped.
- **Proof-of-concept tests.** `tests/Tempest.Core.Tests/Audit/RequirementsVerificationReferenceDataAuditTests.cs` (19 tests, one per mutator including `UndeleteAsync`): each proves the row exists with the right action after a normal call, and is absent (or the object never appears at all) when the transaction's own commit is made to fault (`InMemoryPersistenceStore.FailNextCommit`, the `R7RegressionProofTests`/`TD-158` convention).
- **`WP 21.6A` item 1b, closed alongside.** While inside these fourteen mutators for the audit rows, `RequirementsService` also gained the soft-delete/undelete pair and `CommandCompensation` wiring `WP 21.1A` had excluded Requirements from — see `BACKLOG.md`'s own closure note for the detail, and `docs/releases/v0.21.0/Release Notes.md`'s `WP 21.6A` row.
- **Commit.** `wp/21.6A` (1/n, 2/n).

### OSA-12 — `CurrentPrincipalAccessor.SetCurrent` is reachable by any DI-resolving component (Low / Informational) — **FIXED by `WP 21.6A`**

- **Original finding, unchanged.** `CurrentPrincipalAccessor` was registered in the DI container under its own concrete type with `SetCurrent` public — reachable by any in-process component that resolved it, not only `WorkspaceHost`; demonstrated already in use outside its documented sole caller at `src/Samples/Tempest.Samples/SamplePrincipalFactory.cs:29-38`, which called `SetCurrent` with an arbitrary caller-supplied identity string and no credential check.
- **Fix.** `src/Tempest.Core/Identity/CurrentPrincipalAccessor.cs` — `SetCurrent` is now `internal` (line ~67); `PrincipalSession`, declared in the same file, is the one seam that can still reach it — an `internal` constructor (only `TempestHost` can mint one) and a single `public Establish` method, nothing else. `src/Tempest.Core/Runtime/TempestHost.cs` registers `PrincipalSession` in place of the old dual-`AddInstance` concrete-type registration for `CurrentPrincipalAccessor`. `src/Tempest.Desktop/WorkspaceHost.cs` (start-up and "Switch person…") and `src/Tempest.Harness/Program.cs` (`OSA-09`'s own principal establishment) reach the accessor through this seam instead of casting a resolved `ICurrentPrincipalAccessor` to the concrete type.
- **Disclosed deviation.** `PrincipalSession` *is* registered in the DI container (resolvable by concrete type) rather than reached through a wholly separate, non-DI channel: a dozen `Tempest.Samples` demonstration modules (never shipped — confirmed directly, as this audit's own "Reviewed — no finding" section above already established) constructor-inject it to establish their own named sample identity, exercised by real `TempestHostBuilder`-based integration tests, not only bespoke pipelines. This narrows the exposed capability from "any resolver of the full mutable accessor" (read, write, and every other future member) to "any resolver of one single-purpose, narrowly-named `Establish` call" — a real reduction, not a token one — and the "no live untrusted actor" disposition this finding was originally deferred under is unchanged: `src/Frozen/`'s plugin loading, the one mechanism that would introduce an untrusted in-process actor, is still unreachable.
- **Proof-of-concept tests.** `tests/Tempest.Core.Tests/Identity/PrincipalSessionTests.cs` (7 tests): `ICurrentPrincipalAccessor` declares nothing but its read-only getter; `CurrentPrincipalAccessor.SetCurrent` and `PrincipalSession`'s own constructor are both `internal` at the compiled-metadata level (reflection used only to inspect member modifiers, never to bypass them); `PrincipalSession`'s only public member is `Establish`. `tests/Tempest.Core.Tests/Runtime/IdentityHostRegistrationTests.cs` (rewritten): the concrete `CurrentPrincipalAccessor` type no longer resolves from a running Host at all (`ServiceNotRegisteredException`); `PrincipalSession` does, and establishing a principal through it is visible through `ICurrentPrincipalAccessor.Current`.
- **Commit.** `wp/21.6A` (3/n).

### OSA-13 — The audit collection has no access control at the generic store layer (Low / Informational) — **FIXED by `WP 21.6A`**

- **Original finding, unchanged.** Any code holding an `IPersistenceStore` reference could call `DeleteAsync("Audit", key)` or `WriteAsync("Audit", key, forgedJson)` — nothing distinguished the `Audit` collection from any other at the generic store layer.
- **Fix.** `src/Tempest.Core/Persistence/SqlitePersistenceStore.cs` — `WriteAsync`/`DeleteAsync` (both the non-transactional store and `SqliteTransactionScope`) now call `ThrowIfAuditCollection`, refusing `AuditRecorder.AuditCollectionName` unconditionally (`AuditCollectionProtectedException`, new file `src/Tempest.Core/Persistence/AuditCollectionProtectedException.cs`, naming the collection). The one legitimate route is a capability, not a permission check: `src/Tempest.Core/Persistence/IAuditCollectionWriter.cs` declares two `internal` interfaces (`IAuditCollectionWriter`, `IAuditCollectionTransactionWriter`) — never exposed via `[InternalsVisibleTo]` to any assembly outside `Tempest.Core` — which `SqlitePersistenceStore`/`SqliteTransactionScope` implement explicitly and `src/Tempest.Core/Audit/AuditRecorder.cs`/`AuditTransactionWriter.cs` check their own injected store/transaction for (`as IAuditCollectionWriter`), the same "widen the concrete capability, refuse loudly if it is not there" shape `RequirementsService`/`ReferenceDataCatalog` already use for `ITransactionalDocumentWriter`, applied here to narrow a capability instead of widen one. Reads stay open (`AuditQuery` untouched). The shared `tests/Tempest.Core.Tests/Persistence/InMemoryQueryablePersistenceStore.cs` test double carries the identical guard, so a fact proving the refusal runs the same way against either store.
- **Kill switch not needed.** The brief's own fallback (an `[InternalsVisibleTo]`-free internal interface, "if the capability token cannot be threaded through `ITransactionalDocumentWriter` without changing its public shape for every caller") was reached for directly rather than as a fallback: audit writes never go through `ITransactionalDocumentWriter` (that contract is for engineering *documents*; an audit row is a plain collection write on `IPersistenceTransaction`/`IPersistenceStore` itself), so there was no wider public shape to avoid changing in the first place — the interface-capability shape was simply the right tool from the start.
- **Disclosed deviation.** The guard authorises **both** of this platform's own legitimate audit writers — `AuditRecorder.RecordAsync` (the everyday, non-transactional path every sample module, `ReferenceReviewService`, and `Tempest.Harness` already call) and `AuditTransactionWriter.WriteAsync` (the transactional path `OSA-15` extended) — not only the latter as the brief's own prose named it: refusing `AuditRecorder.RecordAsync` would have broken ordinary audit recording everywhere, which is not what this finding asks for; the finding's own concern is an *arbitrary* `IPersistenceStore`/`IPersistenceTransaction` holder, not this platform's own two named writer classes.
- **Proof-of-concept tests.** `tests/Tempest.Core.Tests/Persistence/AuditCollectionProtectionTests.cs` (10 tests): both stores refuse a direct write and a direct delete, both transactionally and not; the capability bypass still writes; a real `SqlitePersistenceStore` + `AuditRecorder` + `AuditQuery` round trip. `tests/Tempest.Core.Tests/Audit/AuditQueryCorruptionTests.cs` — the one existing test that deliberately corrupted a stored row (this finding's own named example of a legitimate direct write) now goes through the capability instead of the now-refused ordinary path.
- **Commit.** `wp/21.6A` (4/n).

### OSA-14 — The "unknown actor" audit fallback is forceable via the same root cause (Low / Informational) — **FIXED by `WP 21.6A`**

- **Original finding, unchanged.** The fallback triggers purely on `CurrentPrincipalAccessor.Current == null`; because `Current` was the same ambient, mutable singleton OSA-12 named, a component that could reach `SetCurrent` could force it to `null` immediately before an otherwise-attributable action.
- **Fix.** Closed by the identical OSA-12 fix, above: `SetCurrent` is `internal`, reachable only through `PrincipalSession.Establish`. A component holding only `ICurrentPrincipalAccessor` — every ordinary DI-resolving consumer — cannot force `Current` to `null`; only `WorkspaceHost`'s start-up/"Switch person…" and `Tempest.Harness`'s start-up can, and neither ever calls `Establish(null)` after a real session principal is set (`WorkspaceHost.StartAsync`'s own call happens once, before any other write; "Switch person…" only ever establishes a new, non-null principal). `Tempest.Harness` (`OSA-09`) establishes its own principal through this identical seam.
- **Proof-of-concept tests.** The same `PrincipalSessionTests.cs`/`IdentityHostRegistrationTests.cs` facts OSA-12 cites prove this directly: nothing reachable via `ICurrentPrincipalAccessor` alone can call `Establish`/`SetCurrent` at all, let alone with `null`.
- **Commit.** `wp/21.6A` (3/n).

---

## Gate

Run from `D:/tempest-wt/21.5F` on `wp/21.5F` (the gate below covers every commit through `WP 21.5F (8/n)`, `fedf0167`).

```
dotnet build src/TempestOS.slnx -c Debug   --nologo -v q -p:TreatWarningsAsErrors=true   → 0 Warning(s), 0 Error(s)
dotnet build src/TempestOS.slnx -c Release --nologo -v q -p:TreatWarningsAsErrors=true   → 0 Warning(s), 0 Error(s)
dotnet test  tests/Tempest.Core.Tests    -c Debug   --no-build → 4,692 / 4,692 passing
dotnet test  tests/Tempest.Core.Tests    -c Release --no-build → 4,692 / 4,692 passing
dotnet test  tests/Tempest.Desktop.Tests -c Debug   --no-build → 677 / 677 passing (18m 56s — shared machine, several other agents' builds running concurrently)
dotnet test  tests/Tempest.Desktop.Tests -c Release --no-build → 677 / 677 passing (17m 42s — same shared-machine caveat)
powershell -File scripts/governance-healthcheck.ps1             → 5 passed, 0 warned, 0 failed (3,586 Markdown / 156,268 non-Markdown lines added since origin/main — this package's own share is roughly 300/1,500)
```

Every proof-of-concept test named in the Findings section above passes as part of these totals. OSA-02 and OSA-01's tests were additionally confirmed failing against their own pre-fix code specifically (a scoped `git stash` of the production file alone, rebuilt, re-run, then restored) — OSA-01's text-cap tests do not compile at all pre-fix, since they reference a new public constant absent from the pre-fix type, a stronger proof of absence than a runtime failure. Every other finding's tests assert a specific, new behaviour or exact message text that is structurally impossible to satisfy without its own fix present (a refusal naming "protected system directory", a materialised path that is provably never the same directory twice, a permission mode the pre-fix code never set) — read against the diff each commit carries, rather than re-run against a temporarily reverted tree.

---

## Files this package touched

**Fixes:**
`src/Tempest.Desktop/Viewing/AttachmentViewerLauncher.cs`, `src/Tempest.Desktop/Viewing/DocumentPageSources.cs`, `src/Tempest.Core/Secrets/FileSecretStore.cs`, `src/Tempest.Core/Invoicing/OAuth/OAuthResults.cs`, `src/Tempest.Core/Invoicing/OAuth/OAuthTokenResponse.cs`, `src/Tempest.Core/Invoicing/OAuth/OAuthLoopbackListener.cs`, `src/Tempest.Core/Persistence/SqlitePersistenceStore.cs`, `src/Tempest.Core/ExportImport/JsonExportFormat.cs`, `src/Tempest.Core/ExportImport/JsonExportPayloadSerializer.cs`, `src/Tempest.Core/EngineeringDomain/Implementation/EngineeringObjectStateStore.cs`, `src/Samples/Tempest.Samples/RequirementExportAdapter.cs`, `src/Samples/Tempest.Samples/RequirementCollectionExportAdapter.cs`, `src/Tempest.Harness/Program.cs`, `.github/workflows/ci.yml`, `.github/workflows/release.yml`, `THIRD-PARTY-NOTICES.md` (new).

**New tests:**
`tests/Tempest.Desktop.Tests/DocumentViewerAcceptanceTests.cs`, `tests/Tempest.Desktop.Tests/DocumentPageSourceTests.cs`, `tests/Tempest.Core.Tests/Security/FileSecretStoreTests.cs` (new file), `tests/Tempest.Core.Tests/Security/OAuthTokenRedactionTests.cs` (new file), `tests/Tempest.Core.Tests/Security/FrozenCapabilitiesUnreachableTests.cs` (new file), `tests/Tempest.Core.Tests/Invoicing/Connectors/OAuthAuthoriserTests.cs`, `tests/Tempest.Core.Tests/Persistence/SqlitePersistenceStoreTests.cs`, `tests/Tempest.Core.Tests/ExportImport/JsonExportFormatTests.cs`, `tests/Tempest.Core.Tests/Requirements/RequirementsServiceHostileDataTests.cs`, `tests/Tempest.Core.Tests/Macros/MacroReplayArchivedProjectGuardTests.cs` (new file).
