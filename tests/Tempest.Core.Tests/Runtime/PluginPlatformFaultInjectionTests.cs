using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography.X509Certificates;
using Tempest.Core.Configuration;
using Tempest.Core.Diagnostics;
using Tempest.Core.Identity;
using Tempest.Core.Modules;
using Tempest.Core.Navigation;
using Tempest.Core.Plugins;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Runtime;

// WP 13.3A: Fault Injection & Resilience - hostile, real-Host, multi-plugin
// scenarios built through TempestHostBuilder (never an isolated unit call),
// proving the WHOLE platform degrades gracefully under every ADR-0025/
// ADR-0107/ADR-0110/ADR-0111/ADR-0112 failure category simultaneously, not
// merely re-proving one category in isolation. Every scenario asserts both
// the Host's own resilience (HostState.Running, then a clean StopAsync) and
// IDiagnosticsProvider.Plugins/Modules recording the correct, specific
// outcome for every candidate - never a generic catch-all.
//
// Shares the "Console output capture" collection with every other Runtime
// host-integration test file (TempestHostPluginLifecycleTests,
// TempestHostPluginTrustTests, etc.) - the same convention that already
// serializes every real TempestHost run in this test project. This also
// makes it safe for the signature-verification scenarios below to write
// real *.cer files into the process's own AppContext.BaseDirectory/
// TrustedPublishers folder: PluginTrustStore's public constructor
// (which TempestHost itself always uses - ADR-0112, "a fixed convention,
// not configurable") has no TempestHostBuilder-level override, so a
// Host-level (not PluginManifestDiscoveryService-level) signature test has
// no other way to reach it. Each certificate file is written under a
// GUID-derived name and deleted again in the scenario's own try/finally -
// additive, never replacing another entry - so no risk of collision with
// any other test in this serialized collection.
[Collection("Console output capture")]
public class PluginPlatformFaultInjectionTests
{
    // ==================================================================
    // 1. Malformed manifests - not-valid-JSON, missing required field,
    // unparseable version - mixed alongside a healthy plugin. Only the
    // malformed ones isolate; the Host still reaches Running.
    // ==================================================================

    [Fact]
    public async Task RunAsync_MalformedManifests_MixedWithHealthyPlugin_OnlyMalformedIsolated_HostReachesRunning()
    {
        using var temp = new TempDirectory();

        var notJsonFolder = CreateFolder(temp.Path, "a-not-json");
        File.WriteAllText(Path.Combine(notJsonFolder, PluginManifestDiscoveryService.ManifestFileName), "{ this is not json at all");

        var missingFieldFolder = CreateFolder(temp.Path, "b-missing-field");
        File.WriteAllText(
            Path.Combine(missingFieldFolder, PluginManifestDiscoveryService.ManifestFileName),
            PluginManifestJsonBuilder.Build(id: "host-test.missing-field", name: null));

        var badVersionFolder = CreateFolder(temp.Path, "c-bad-version");
        File.WriteAllText(
            Path.Combine(badVersionFolder, PluginManifestDiscoveryService.ManifestFileName),
            PluginManifestJsonBuilder.Build(id: "host-test.bad-version", minimumPlatformVersion: "not-a-version"));

        var healthyFolder = CreateFolder(temp.Path, "d-healthy");
        var healthyAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            healthyFolder, "Healthy.dll", "host-test.healthy-1", "Healthy Plugin", "1.0.0");
        WriteManifestJson(healthyFolder, PluginSigningTestHelper.BuildDto("host-test.healthy-1", Path.GetFileName(healthyAssembly)));

        var host = BuildUnsignedAllowedHost(temp.Path);

        await RunUntilRunningAsync(host);

        var diagnostics = GetDiagnostics(host);
        Assert.Equal(PluginRegistryState.Loaded, Single(diagnostics, "host-test.healthy-1").State);

        // Every InvalidPluginManifestException - not-valid-JSON, missing
        // required field, unparseable version - is recorded keyed by its
        // candidate folder path, never by whatever Id the manifest DID
        // manage to declare (WP 13.3B architecture review finding: using an
        // unvalidated, self-declared Id as the registry's own unique key
        // would let a malformed manifest that never fully validates - and so
        // never reaches DuplicatePluginIdException's own uniqueness check -
        // spoof a genuine, unrelated, already-Loaded plugin's own Id). The
        // declared Id, where the manifest got far enough to parse one, is
        // still surfaced safely - as free text inside Detail only, per
        // PluginFailureLogging.RecordIsolatedFailure's own remarks.
        var notJsonEntry = Single(diagnostics, notJsonFolder);
        Assert.Equal(PluginRegistryState.Failed, notJsonEntry.State);
        var missingFieldEntry = Single(diagnostics, missingFieldFolder);
        Assert.Equal(PluginRegistryState.Failed, missingFieldEntry.State);
        Assert.Contains("host-test.missing-field", missingFieldEntry.Detail);
        var badVersionEntry = Single(diagnostics, badVersionFolder);
        Assert.Equal(PluginRegistryState.Failed, badVersionEntry.State);
        Assert.Contains("host-test.bad-version", badVersionEntry.Detail);

        // Genuinely distinguishable causes, not merely a shared generic
        // "invalid manifest" string for all three: the not-valid-JSON
        // candidate's own Detail names its own real cause, and none of the
        // three collide with one another.
        Assert.NotNull(notJsonEntry.Detail);
        Assert.Contains("JSON", notJsonEntry.Detail!, StringComparison.Ordinal);
        Assert.Contains("Name", missingFieldEntry.Detail!, StringComparison.Ordinal);
        Assert.Contains("unparseable MinimumPlatformVersion", badVersionEntry.Detail!, StringComparison.Ordinal);
        Assert.NotEqual(notJsonEntry.Detail, missingFieldEntry.Detail);
        Assert.NotEqual(missingFieldEntry.Detail, badVersionEntry.Detail);
        Assert.NotEqual(notJsonEntry.Detail, badVersionEntry.Detail);

        Assert.Equal(4, diagnostics.Plugins.Count);

        await StopCleanlyAsync(host);
    }

    // ==================================================================
    // 1b. WP 13.3B regression: a malformed manifest that never fully
    // validates must never be able to spoof a genuine, unrelated,
    // already-Loaded plugin's own registry Id, even when it deliberately
    // declares that exact Id - PluginRegistryEntry.Id must remain a
    // reliable, collision-free key regardless of what an untrusted,
    // never-validated manifest claims.
    // ==================================================================

    [Fact]
    public async Task RunAsync_MalformedManifestDeclaresSameIdAsGenuineLoadedPlugin_DoesNotSpoofOrDuplicateTheGenuineEntry()
    {
        using var temp = new TempDirectory();

        const string sharedId = "host-test.spoof-target";

        var healthyFolder = CreateFolder(temp.Path, "a-healthy");
        var healthyAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            healthyFolder, "Healthy.dll", sharedId, "Genuine Plugin", "1.0.0");
        WriteManifestJson(healthyFolder, PluginSigningTestHelper.BuildDto(sharedId, Path.GetFileName(healthyAssembly)));

        // Declares the identical Id, but is otherwise malformed (missing
        // Name) - never reaches DuplicatePluginIdException's own
        // uniqueness check, since that only fires for candidates that pass
        // complete validation.
        var maliciousFolder = CreateFolder(temp.Path, "b-malicious-same-id");
        File.WriteAllText(
            Path.Combine(maliciousFolder, PluginManifestDiscoveryService.ManifestFileName),
            PluginManifestJsonBuilder.Build(id: sharedId, name: null));

        var host = BuildUnsignedAllowedHost(temp.Path);

        await RunUntilRunningAsync(host);

        var diagnostics = GetDiagnostics(host);

        // Exactly one entry for the shared Id, and it's the genuine,
        // Loaded one - the malformed candidate's own self-declared Id was
        // never used as its registry key, so it cannot collide.
        var sharedEntry = Assert.Single(diagnostics.Plugins, e => e.Id == sharedId);
        Assert.Equal(PluginRegistryState.Loaded, sharedEntry.State);

        var maliciousEntry = Single(diagnostics, maliciousFolder);
        Assert.Equal(PluginRegistryState.Failed, maliciousEntry.State);
        Assert.Contains(sharedId, maliciousEntry.Detail);

        Assert.Equal(2, diagnostics.Plugins.Count);

        await StopCleanlyAsync(host);
    }

    // ==================================================================
    // 2. Dependency failures - missing dependency, incompatible version
    // range, and a dependency chain where the dependency itself fails for
    // an unrelated reason (an invalid signature) - the dependent must be
    // isolated as DependencyUnmet specifically, not the dependency's own
    // Failed state leaking onto it.
    // ==================================================================

    [Fact]
    public async Task RunAsync_MissingDependency_IsolatesOnlyDependent_HealthySiblingStillLoads()
    {
        using var temp = new TempDirectory();

        var dependentFolder = CreateFolder(temp.Path, "a-dependent");
        WriteRawManifest(dependentFolder, "host-test.dependent-missing", dependencies:
            [("host-test.does-not-exist", "1.0.0", null)]);

        var siblingFolder = CreateFolder(temp.Path, "b-sibling");
        var siblingAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            siblingFolder, "Sibling.dll", "host-test.sibling-missing-dep", "Sibling", "1.0.0");
        WriteManifestJson(siblingFolder, PluginSigningTestHelper.BuildDto("host-test.sibling-missing-dep", Path.GetFileName(siblingAssembly)));

        var host = BuildUnsignedAllowedHost(temp.Path);
        await RunUntilRunningAsync(host);

        var diagnostics = GetDiagnostics(host);
        var dependentEntry = Single(diagnostics, "host-test.dependent-missing");
        Assert.Equal(PluginRegistryState.DependencyUnmet, dependentEntry.State);
        Assert.Equal(PluginRegistryState.Loaded, Single(diagnostics, "host-test.sibling-missing-dep").State);

        Assert.NotNull(dependentEntry.Detail);
        Assert.Contains("not present among eligible plugins", dependentEntry.Detail!, StringComparison.Ordinal);

        await StopCleanlyAsync(host);
    }

    [Fact]
    public async Task RunAsync_IncompatibleDependencyVersionRange_IsolatesOnlyDependent_TargetStillLoads()
    {
        using var temp = new TempDirectory();

        var targetFolder = CreateFolder(temp.Path, "a-target");
        var targetAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            targetFolder, "Target.dll", "host-test.version-target", "Target", "1.0.0");
        WriteManifestJson(targetFolder, PluginSigningTestHelper.BuildDto("host-test.version-target", Path.GetFileName(targetAssembly)));

        var dependentFolder = CreateFolder(temp.Path, "b-dependent");
        WriteRawManifest(dependentFolder, "host-test.dependent-version", dependencies:
            [("host-test.version-target", "2.0.0", null)]);

        var host = BuildUnsignedAllowedHost(temp.Path);
        await RunUntilRunningAsync(host);

        var diagnostics = GetDiagnostics(host);
        var dependentEntry = Single(diagnostics, "host-test.dependent-version");
        Assert.Equal(PluginRegistryState.DependencyUnmet, dependentEntry.State);
        Assert.Equal(PluginRegistryState.Loaded, Single(diagnostics, "host-test.version-target").State);

        // Distinguishable from a "missing dependency entirely" failure
        // reason - not a shared, generic "DependencyUnmet" string.
        Assert.NotNull(dependentEntry.Detail);
        Assert.Contains("required version range", dependentEntry.Detail!, StringComparison.Ordinal);
        Assert.DoesNotContain("not present among eligible plugins", dependentEntry.Detail!, StringComparison.Ordinal);

        await StopCleanlyAsync(host);
    }

    [Fact]
    public async Task RunAsync_DependencyItselfFailsForUnrelatedReason_DependentIsolatedAsDependencyUnmet_NotAsFailed()
    {
        using var temp = new TempDirectory();

        // The dependency: a real signed candidate whose signature envelope
        // is deliberately garbage - isolated at Discovery for its OWN
        // reason (category 15, PluginSignatureVerificationFailedException
        // -> PluginRegistryState.Failed), never even reaching the
        // dependency graph's own acceptedById set.
        var targetFolder = CreateFolder(temp.Path, "a-broken-target");
        var targetAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            targetFolder, "Target.dll", "host-test.broken-target", "Broken Target", "1.0.0");
        var targetDto = PluginSigningTestHelper.BuildDto("host-test.broken-target", Path.GetFileName(targetAssembly));
        targetDto.Signature = "{ not a valid envelope";
        WriteManifestJson(targetFolder, targetDto);

        // The dependent: perfectly valid on its own, but depends on the
        // above - which never becomes available, for a reason that has
        // nothing to do with dependency resolution at all.
        var dependentFolder = CreateFolder(temp.Path, "b-dependent-on-broken");
        WriteRawManifest(dependentFolder, "host-test.dependent-on-broken", dependencies:
            [("host-test.broken-target", "1.0.0", null)]);

        var host = BuildUnsignedAllowedHost(temp.Path);
        await RunUntilRunningAsync(host);

        var diagnostics = GetDiagnostics(host);

        // The dependency's own failure reason is preserved verbatim - Failed,
        // not DependencyUnmet (it has no dependencies of its own).
        Assert.Equal(PluginRegistryState.Failed, Single(diagnostics, "host-test.broken-target").State);

        // The dependent is isolated as DependencyUnmet specifically - the
        // dependency's own Failed state must never leak onto it.
        Assert.Equal(PluginRegistryState.DependencyUnmet, Single(diagnostics, "host-test.dependent-on-broken").State);

        await StopCleanlyAsync(host);
    }

    // ==================================================================
    // 3. Invalid signatures - malformed envelope, tampered assembly,
    // tampered manifest, untrusted publisher, expired certificate,
    // not-yet-valid certificate - mixed with healthy plugins in two runs.
    // ==================================================================

    [Fact]
    public async Task RunAsync_MalformedTamperedSignatures_AllIsolatedAsFailed_HealthySiblingStillLoads()
    {
        using var temp = new TempDirectory();
        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate("CN=Acme Plugins Ltd.");
        using var trustEntry = new TrustedPublisherFile(certificate);

        // Malformed envelope - not even parseable JSON.
        var malformedFolder = CreateFolder(temp.Path, "a-malformed-envelope");
        var malformedAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            malformedFolder, "Plugin.dll", "host-test.sig-malformed", "Malformed Sig", "1.0.0");
        var malformedDto = PluginSigningTestHelper.BuildDto("host-test.sig-malformed", Path.GetFileName(malformedAssembly));
        malformedDto.Signature = "{ not a valid envelope";
        WriteManifestJson(malformedFolder, malformedDto);

        // Tampered assembly - signed correctly, then the DLL bytes changed afterward.
        var tamperedAssemblyFolder = CreateFolder(temp.Path, "b-tampered-assembly");
        var tamperedAssemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            tamperedAssemblyFolder, "Plugin.dll", "host-test.sig-tampered-asm", "Tampered Asm", "1.0.0");
        var tamperedAssemblyDto = PluginSigningTestHelper.BuildDto("host-test.sig-tampered-asm", Path.GetFileName(tamperedAssemblyPath));
        tamperedAssemblyDto.Signature = PluginSigningTestHelper.ComputeValidSignatureEnvelopeJson(tamperedAssemblyDto, tamperedAssemblyPath, certificate);
        WriteManifestJson(tamperedAssemblyFolder, tamperedAssemblyDto);
        File.AppendAllText(tamperedAssemblyPath, "tampered-bytes-after-signing");

        // Tampered manifest - signed against the original content, then a
        // manifest field mutated afterward, so the recomputed hash no
        // longer matches.
        var tamperedManifestFolder = CreateFolder(temp.Path, "c-tampered-manifest");
        var tamperedManifestAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            tamperedManifestFolder, "Plugin.dll", "host-test.sig-tampered-manifest", "Tampered Manifest", "1.0.0");
        var tamperedManifestDto = PluginSigningTestHelper.BuildDto(
            "host-test.sig-tampered-manifest", Path.GetFileName(tamperedManifestAssembly), publisher: "Original Publisher");
        var tamperedManifestSignature = PluginSigningTestHelper.ComputeValidSignatureEnvelopeJson(tamperedManifestDto, tamperedManifestAssembly, certificate);
        tamperedManifestDto.Publisher = "Tampered Publisher";
        tamperedManifestDto.Signature = tamperedManifestSignature;
        WriteManifestJson(tamperedManifestFolder, tamperedManifestDto);

        var healthyFolder = CreateFolder(temp.Path, "d-healthy");
        var healthyAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            healthyFolder, "Healthy.dll", "host-test.sig-healthy-1", "Healthy", "1.0.0");
        WriteManifestJson(healthyFolder, PluginSigningTestHelper.BuildDto("host-test.sig-healthy-1", Path.GetFileName(healthyAssembly)));

        var host = BuildUnsignedAllowedHost(temp.Path);
        await RunUntilRunningAsync(host);

        var diagnostics = GetDiagnostics(host);
        var malformedEntry = Single(diagnostics, "host-test.sig-malformed");
        var tamperedAsmEntry = Single(diagnostics, "host-test.sig-tampered-asm");
        var tamperedManifestEntry = Single(diagnostics, "host-test.sig-tampered-manifest");

        Assert.Equal(PluginRegistryState.Failed, malformedEntry.State);
        Assert.Equal(PluginRegistryState.Failed, tamperedAsmEntry.State);
        Assert.Equal(PluginRegistryState.Failed, tamperedManifestEntry.State);
        Assert.Equal(PluginRegistryState.Loaded, Single(diagnostics, "host-test.sig-healthy-1").State);

        Assert.NotNull(malformedEntry.Detail);
        Assert.NotNull(tamperedAsmEntry.Detail);
        Assert.NotNull(tamperedManifestEntry.Detail);

        // The malformed-envelope cause (never reaches cryptographic
        // verification at all - the envelope itself doesn't parse) is its
        // own, distinct, well-identified cause.
        Assert.Contains("could not be parsed", malformedEntry.Detail!, StringComparison.OrdinalIgnoreCase);

        // WP 13.3B finding, documented rather than fixed here (out of this
        // sub-agent's own scope - see this work package's final report):
        // tampered-assembly and tampered-manifest are two genuinely
        // different root causes (a mutated DLL vs a mutated manifest
        // field), and PluginSignatureVerificationFailedException's own
        // message does at least distinguish WHICH plugin failed (each
        // embeds its own declared Id) - but WHY it failed collapses to the
        // exact same generic reason clause for both
        // (PluginManifestDiscoveryService.VerifySignature's own single,
        // hard-coded failureReason string for every cryptographic-
        // verification failure): a real diagnosability gap for whoever has
        // to act on this PluginRegistryEntry later, even though the
        // isolation behaviour itself (Failed, sibling unaffected) is
        // entirely correct.
        Assert.Contains("does not verify against the recomputed manifest and assembly hash", tamperedAsmEntry.Detail!, StringComparison.Ordinal);
        Assert.Contains("does not verify against the recomputed manifest and assembly hash", tamperedManifestEntry.Detail!, StringComparison.Ordinal);

        await StopCleanlyAsync(host);
    }

    [Fact]
    public async Task RunAsync_UntrustedExpiredNotYetValidCertificates_AllIsolatedAsFailed_HealthySiblingStillLoads()
    {
        using var temp = new TempDirectory();

        // Untrusted publisher - a real, otherwise-valid signature, but the
        // signing certificate was never added to the trust store.
        using var untrustedCertificate = PluginSigningTestHelper.CreateSelfSignedCertificate("CN=Unknown Publisher");
        var untrustedFolder = CreateFolder(temp.Path, "a-untrusted-publisher");
        var untrustedAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            untrustedFolder, "Plugin.dll", "host-test.sig-untrusted", "Untrusted", "1.0.0");
        var untrustedDto = PluginSigningTestHelper.BuildDto("host-test.sig-untrusted", Path.GetFileName(untrustedAssembly));
        untrustedDto.Signature = PluginSigningTestHelper.ComputeValidSignatureEnvelopeJson(untrustedDto, untrustedAssembly, untrustedCertificate);
        WriteManifestJson(untrustedFolder, untrustedDto);

        // Expired certificate - trusted, but its own validity window closed
        // in the past.
        using var expiredCertificate = PluginSigningTestHelper.CreateSelfSignedCertificate(
            "CN=Expired Publisher",
            notBefore: DateTimeOffset.UtcNow.AddDays(-60),
            notAfter: DateTimeOffset.UtcNow.AddDays(-30));
        using var expiredTrustEntry = new TrustedPublisherFile(expiredCertificate);
        var expiredFolder = CreateFolder(temp.Path, "b-expired-cert");
        var expiredAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            expiredFolder, "Plugin.dll", "host-test.sig-expired", "Expired", "1.0.0");
        var expiredDto = PluginSigningTestHelper.BuildDto("host-test.sig-expired", Path.GetFileName(expiredAssembly));
        expiredDto.Signature = PluginSigningTestHelper.ComputeValidSignatureEnvelopeJson(expiredDto, expiredAssembly, expiredCertificate);
        WriteManifestJson(expiredFolder, expiredDto);

        // Not-yet-valid certificate - trusted, but its own validity window
        // has not opened yet.
        using var notYetValidCertificate = PluginSigningTestHelper.CreateSelfSignedCertificate(
            "CN=Future Publisher",
            notBefore: DateTimeOffset.UtcNow.AddDays(30),
            notAfter: DateTimeOffset.UtcNow.AddDays(60));
        using var notYetValidTrustEntry = new TrustedPublisherFile(notYetValidCertificate);
        var notYetValidFolder = CreateFolder(temp.Path, "c-not-yet-valid-cert");
        var notYetValidAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            notYetValidFolder, "Plugin.dll", "host-test.sig-not-yet-valid", "Not Yet Valid", "1.0.0");
        var notYetValidDto = PluginSigningTestHelper.BuildDto("host-test.sig-not-yet-valid", Path.GetFileName(notYetValidAssembly));
        notYetValidDto.Signature = PluginSigningTestHelper.ComputeValidSignatureEnvelopeJson(notYetValidDto, notYetValidAssembly, notYetValidCertificate);
        WriteManifestJson(notYetValidFolder, notYetValidDto);

        var healthyFolder = CreateFolder(temp.Path, "d-healthy");
        var healthyAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            healthyFolder, "Healthy.dll", "host-test.sig-healthy-2", "Healthy", "1.0.0");
        WriteManifestJson(healthyFolder, PluginSigningTestHelper.BuildDto("host-test.sig-healthy-2", Path.GetFileName(healthyAssembly)));

        var host = BuildUnsignedAllowedHost(temp.Path);
        await RunUntilRunningAsync(host);

        var diagnostics = GetDiagnostics(host);
        var untrustedEntry = Single(diagnostics, "host-test.sig-untrusted");
        var expiredEntry = Single(diagnostics, "host-test.sig-expired");
        var notYetValidEntry = Single(diagnostics, "host-test.sig-not-yet-valid");

        Assert.Equal(PluginRegistryState.Failed, untrustedEntry.State);
        Assert.Equal(PluginRegistryState.Failed, expiredEntry.State);
        Assert.Equal(PluginRegistryState.Failed, notYetValidEntry.State);
        Assert.Equal(PluginRegistryState.Loaded, Single(diagnostics, "host-test.sig-healthy-2").State);

        Assert.NotNull(untrustedEntry.Detail);
        Assert.NotNull(expiredEntry.Detail);
        Assert.NotNull(notYetValidEntry.Detail);

        // Untrusted publisher never reaches cryptographic verification at
        // all - its own, distinct, well-identified cause.
        Assert.Contains("No trusted publisher certificate matches", untrustedEntry.Detail!, StringComparison.Ordinal);

        // WP 13.3B finding (same underlying cause as the collision this test
        // file's own RunAsync_MalformedTamperedSignatures... documents
        // above): expired and not-yet-valid are two genuinely different
        // certificate-validity failures, but PluginSignatureVerifier.Verify
        // itself only ever returns a bool - AssignTrustTier's own single
        // generic failureReason string for a failed Verify() call means
        // these two, PLUS a tampered-assembly/tampered-manifest failure, all
        // share the exact same "why" reason clause, distinguishable from
        // one another only by WHICH plugin Id the surrounding message names,
        // never by WHY each one actually failed.
        Assert.Contains("does not verify against the recomputed manifest and assembly hash", expiredEntry.Detail!, StringComparison.Ordinal);
        Assert.Contains("does not verify against the recomputed manifest and assembly hash", notYetValidEntry.Detail!, StringComparison.Ordinal);

        await StopCleanlyAsync(host);
    }

    // ==================================================================
    // 4. Trust denial - a capability outside the tier's ceiling, and
    // separately a non-compliant module constructor - mixed together with
    // healthy plugins in ONE run, TrustDenied for both, with distinguishable
    // Detail text for each of the two distinct denial reasons.
    // ==================================================================

    [Fact]
    public async Task RunAsync_TrustDenials_CeilingExceededAndNonCompliantConstructor_MixedWithHealthy_BothTrustDeniedWithDistinctDetail()
    {
        using var temp = new TempDirectory();

        var ceilingFolder = CreateFolder(temp.Path, "a-ceiling-exceeded");
        var ceilingAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            ceilingFolder, "Plugin.dll", "host-test.chaos-ceiling", "Ceiling Exceeded", "1.0.0");
        WriteManifestJson(
            ceilingFolder,
            PluginSigningTestHelper.BuildDto(
                "host-test.chaos-ceiling", Path.GetFileName(ceilingAssembly),
                requestedCapabilities: [PluginCapability.DiRegister]));

        var ctorFolder = CreateFolder(temp.Path, "b-noncompliant-ctor");
        var ctorAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssemblyWithCommandModule(
            ctorFolder, "Plugin.dll", "host-test.chaos-ctor", "Noncompliant Ctor", "1.0.0",
            "host-test.chaos-ctor.command", "Chaos Command");
        WriteManifestJson(ctorFolder, PluginSigningTestHelper.BuildDto("host-test.chaos-ctor", Path.GetFileName(ctorAssembly)));

        var healthyFolder1 = CreateFolder(temp.Path, "c-healthy-1");
        var healthyAssembly1 = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            healthyFolder1, "Plugin.dll", "host-test.chaos-healthy-1", "Healthy 1", "1.0.0");
        WriteManifestJson(healthyFolder1, PluginSigningTestHelper.BuildDto("host-test.chaos-healthy-1", Path.GetFileName(healthyAssembly1)));

        var healthyFolder2 = CreateFolder(temp.Path, "d-healthy-2");
        var healthyAssembly2 = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            healthyFolder2, "Plugin.dll", "host-test.chaos-healthy-2", "Healthy 2", "1.0.0");
        WriteManifestJson(healthyFolder2, PluginSigningTestHelper.BuildDto("host-test.chaos-healthy-2", Path.GetFileName(healthyAssembly2)));

        var host = BuildUnsignedAllowedHost(temp.Path);
        await RunUntilRunningAsync(host);

        var diagnostics = GetDiagnostics(host);

        var ceilingEntry = Single(diagnostics, "host-test.chaos-ceiling");
        Assert.Equal(PluginRegistryState.TrustDenied, ceilingEntry.State);
        Assert.NotNull(ceilingEntry.Detail);
        Assert.Contains(PluginCapability.DiRegister, ceilingEntry.Detail!, StringComparison.Ordinal);
        Assert.DoesNotContain("constructor", ceilingEntry.Detail!, StringComparison.OrdinalIgnoreCase);

        var ctorEntry = Single(diagnostics, "host-test.chaos-ctor");
        Assert.Equal(PluginRegistryState.TrustDenied, ctorEntry.State);
        Assert.NotNull(ctorEntry.Detail);
        Assert.Contains("constructor", ctorEntry.Detail!, StringComparison.OrdinalIgnoreCase);

        // Distinguishable from one another.
        Assert.NotEqual(ceilingEntry.Detail, ctorEntry.Detail);

        Assert.Equal(PluginRegistryState.Loaded, Single(diagnostics, "host-test.chaos-healthy-1").State);
        Assert.Equal(PluginRegistryState.Loaded, Single(diagnostics, "host-test.chaos-healthy-2").State);

        await StopCleanlyAsync(host);
    }

    // ==================================================================
    // 5. Capability denial at registration time (not load time) - a
    // plugin whose module passes EnforceTrust (constructor-injecting
    // INavigationProvider under an explicit plugin.services.resolve:*
    // grant) but whose manifest never grants plugin.navigation.register
    // itself - denied by NavigationService.Register at the moment its
    // InitialiseAsync actually calls it, isolated as a module activation
    // failure (PermissionDeniedException), never Host-fatal, and never
    // preventing a healthy sibling module's own activation.
    // ==================================================================

    [Fact]
    public async Task RunAsync_PluginPassesTrustButLacksRuntimeNavigationPermission_ModuleActivationIsolatesAsFailed_SiblingModuleStillActivates()
    {
        using var temp = new TempDirectory();
        var pluginsRoot = Path.Combine(temp.Path, "plugins");
        Directory.CreateDirectory(pluginsRoot);

        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate("CN=Acme Plugins Ltd.");
        using var trustEntry = new TrustedPublisherFile(certificate);

        // The denied plugin: granted the constructor-injection capability
        // for INavigationProvider, but NOT plugin.navigation.register
        // itself - EnforceTrust passes (its constructor is compliant), but
        // the runtime call inside InitialiseAsync is denied.
        var deniedFolder = CreateFolder(pluginsRoot, "a-registration-denied");
        var deniedAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssemblyWithNavigationModule(
            deniedFolder, "Denied.dll", "host-test.reg-denied", "Registration Denied", "1.0.0",
            "host-test.reg-denied.page", "Denied Page");
        var deniedDto = PluginSigningTestHelper.BuildDto(
            "host-test.reg-denied", Path.GetFileName(deniedAssembly),
            requestedCapabilities: [PluginCapability.ServiceResolve(typeof(INavigationProvider).FullName!)]);
        deniedDto.Signature = PluginSigningTestHelper.ComputeValidSignatureEnvelopeJson(deniedDto, deniedAssembly, certificate);
        WriteManifestJson(deniedFolder, deniedDto);

        // The healthy sibling: granted BOTH capabilities, so its own
        // registration succeeds - proving the denied module's failure
        // does not prevent this one's own activation.
        var healthyFolder = CreateFolder(pluginsRoot, "b-registration-healthy");
        var healthyAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssemblyWithNavigationModule(
            healthyFolder, "Healthy.dll", "host-test.reg-healthy", "Registration Healthy", "1.0.0",
            "host-test.reg-healthy.page", "Healthy Page");
        var healthyDto = PluginSigningTestHelper.BuildDto(
            "host-test.reg-healthy", Path.GetFileName(healthyAssembly),
            requestedCapabilities:
            [
                PluginCapability.ServiceResolve(typeof(INavigationProvider).FullName!),
                PluginCapability.Navigation,
            ]);
        healthyDto.Signature = PluginSigningTestHelper.ComputeValidSignatureEnvelopeJson(healthyDto, healthyAssembly, certificate);
        WriteManifestJson(healthyFolder, healthyDto);

        var deniedModuleType = LoadSingleModuleType(deniedAssembly);
        var healthyModuleType = LoadSingleModuleType(healthyAssembly);

        var builder = new TempestHostBuilder([deniedModuleType, healthyModuleType], pluginsRoot);
        var host = builder.Build();

        await RunUntilRunningAsync(host);

        var diagnostics = GetDiagnostics(host);

        // Both plugins pass Plugin Loading (EnforceTrust) - the denial only
        // happens later, during module activation.
        Assert.Equal(PluginRegistryState.Loaded, Single(diagnostics, "host-test.reg-denied").State);
        Assert.Equal(PluginRegistryState.Loaded, Single(diagnostics, "host-test.reg-healthy").State);

        var deniedModuleStatus = diagnostics.Modules.Single(m => m.Descriptor.Id == "host-test.reg-denied");
        Assert.Equal(ModuleState.Failed, deniedModuleStatus.State);
        Assert.NotNull(deniedModuleStatus.FailureReason);
        Assert.IsType<PermissionDeniedException>(deniedModuleStatus.FailureReason);

        var healthyModuleStatus = diagnostics.Modules.Single(m => m.Descriptor.Id == "host-test.reg-healthy");
        Assert.Equal(ModuleState.Running, healthyModuleStatus.State);
        Assert.Null(healthyModuleStatus.FailureReason);

        await StopCleanlyAsync(host);
    }

    // ==================================================================
    // 6. Constructor failures - a module type whose constructor throws
    // during actual instantiation (a real runtime exception from
    // otherwise fully trust-compliant code, not a trust-compliance
    // failure) - isolated, not Host-fatal, and a sibling still activates.
    // ==================================================================

    [Fact]
    public async Task RunAsync_ModuleConstructorThrowsAtInstantiation_IsolatedAsFailed_SiblingModuleStillActivates()
    {
        using var temp = new TempDirectory();
        var pluginsRoot = Path.Combine(temp.Path, "plugins");
        Directory.CreateDirectory(pluginsRoot);

        var throwingFolder = CreateFolder(pluginsRoot, "a-ctor-throws");
        var throwingAssembly = FaultInjectingPluginAssemblyBuilder.BuildModuleThatThrowsInConstructor(
            throwingFolder, "CtorThrows.dll", "host-test.ctor-throws", "Ctor Throws", "1.0.0",
            "ctor-fault-injection-marker");
        WriteManifestJson(throwingFolder, PluginSigningTestHelper.BuildDto("host-test.ctor-throws", Path.GetFileName(throwingAssembly)));

        var healthyFolder = CreateFolder(pluginsRoot, "b-ctor-healthy-sibling");
        var healthyAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            healthyFolder, "Healthy.dll", "host-test.ctor-healthy-sibling", "Healthy Sibling", "1.0.0");
        WriteManifestJson(healthyFolder, PluginSigningTestHelper.BuildDto("host-test.ctor-healthy-sibling", Path.GetFileName(healthyAssembly)));

        var throwingModuleType = LoadSingleModuleType(throwingAssembly);
        var healthyModuleType = LoadSingleModuleType(healthyAssembly);

        var builder = new TempestHostBuilder([throwingModuleType, healthyModuleType], pluginsRoot);
        builder.AddConfigurationSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>("Plugins:AllowUnsignedLoad", "true"),
        ]));
        var host = builder.Build();

        await RunUntilRunningAsync(host);

        var diagnostics = GetDiagnostics(host);
        Assert.Equal(PluginRegistryState.Loaded, Single(diagnostics, "host-test.ctor-throws").State);

        var throwingStatus = diagnostics.Modules.Single(m => m.Descriptor.Id == "host-test.ctor-throws");
        Assert.Equal(ModuleState.Failed, throwingStatus.State);
        Assert.NotNull(throwingStatus.FailureReason);
        Assert.Contains("ctor-fault-injection-marker", EffectiveFailureMessage(throwingStatus.FailureReason!), StringComparison.Ordinal);

        var healthyStatus = diagnostics.Modules.Single(m => m.Descriptor.Id == "host-test.ctor-healthy-sibling");
        Assert.Equal(ModuleState.Running, healthyStatus.State);

        await StopCleanlyAsync(host);
    }

    // ==================================================================
    // 7. Activation failures - InitialiseAsync and StartAsync each
    // throwing, isolated per the existing (pre-plugin-trust) module
    // lifecycle failure handling, neither preventing a third, healthy
    // module's own activation.
    // ==================================================================

    [Fact]
    public async Task RunAsync_InitialiseAsyncAndStartAsyncThrow_BothIsolatedAsFailed_HealthySiblingStillActivates()
    {
        using var temp = new TempDirectory();
        var pluginsRoot = Path.Combine(temp.Path, "plugins");
        Directory.CreateDirectory(pluginsRoot);

        var initFolder = CreateFolder(pluginsRoot, "a-initialise-throws");
        var initAssembly = FaultInjectingPluginAssemblyBuilder.BuildModuleThatThrowsInInitialiseAsync(
            initFolder, "InitThrows.dll", "host-test.init-throws", "Init Throws", "1.0.0",
            "initialise-fault-injection-marker");
        WriteManifestJson(initFolder, PluginSigningTestHelper.BuildDto("host-test.init-throws", Path.GetFileName(initAssembly)));

        var startFolder = CreateFolder(pluginsRoot, "b-start-throws");
        var startAssembly = FaultInjectingPluginAssemblyBuilder.BuildModuleThatThrowsInStartAsync(
            startFolder, "StartThrows.dll", "host-test.start-throws", "Start Throws", "1.0.0",
            "start-fault-injection-marker");
        WriteManifestJson(startFolder, PluginSigningTestHelper.BuildDto("host-test.start-throws", Path.GetFileName(startAssembly)));

        var healthyFolder = CreateFolder(pluginsRoot, "c-activation-healthy-sibling");
        var healthyAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            healthyFolder, "Healthy.dll", "host-test.activation-healthy-sibling", "Healthy Sibling", "1.0.0");
        WriteManifestJson(healthyFolder, PluginSigningTestHelper.BuildDto("host-test.activation-healthy-sibling", Path.GetFileName(healthyAssembly)));

        var initModuleType = LoadSingleModuleType(initAssembly);
        var startModuleType = LoadSingleModuleType(startAssembly);
        var healthyModuleType = LoadSingleModuleType(healthyAssembly);

        var builder = new TempestHostBuilder([initModuleType, startModuleType, healthyModuleType], pluginsRoot);
        builder.AddConfigurationSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>("Plugins:AllowUnsignedLoad", "true"),
        ]));
        var host = builder.Build();

        await RunUntilRunningAsync(host);

        var diagnostics = GetDiagnostics(host);

        var initStatus = diagnostics.Modules.Single(m => m.Descriptor.Id == "host-test.init-throws");
        Assert.Equal(ModuleState.Failed, initStatus.State);
        Assert.Contains("initialise-fault-injection-marker", EffectiveFailureMessage(initStatus.FailureReason!), StringComparison.Ordinal);

        var startStatus = diagnostics.Modules.Single(m => m.Descriptor.Id == "host-test.start-throws");
        Assert.Equal(ModuleState.Failed, startStatus.State);
        Assert.Contains("start-fault-injection-marker", EffectiveFailureMessage(startStatus.FailureReason!), StringComparison.Ordinal);

        var healthyStatus = diagnostics.Modules.Single(m => m.Descriptor.Id == "host-test.activation-healthy-sibling");
        Assert.Equal(ModuleState.Running, healthyStatus.State);

        await StopCleanlyAsync(host);
    }

    // ==================================================================
    // 8. Cyclic dependencies - a direct 2-cycle and a longer 3+ node
    // cycle, every member isolated as DependencyUnmet, a non-cyclic
    // sibling still loads.
    // ==================================================================

    [Fact]
    public async Task RunAsync_DirectTwoPluginCycle_BothIsolatedAsDependencyUnmet_NonCyclicSiblingStillLoads()
    {
        using var temp = new TempDirectory();

        var aFolder = CreateFolder(temp.Path, "a-cycle2-a");
        WriteRawManifest(aFolder, "host-test.cycle2-a", dependencies: [("host-test.cycle2-b", "1.0.0", null)]);

        var bFolder = CreateFolder(temp.Path, "b-cycle2-b");
        WriteRawManifest(bFolder, "host-test.cycle2-b", dependencies: [("host-test.cycle2-a", "1.0.0", null)]);

        var siblingFolder = CreateFolder(temp.Path, "c-noncyclic-sibling");
        var siblingAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            siblingFolder, "Sibling.dll", "host-test.cycle2-sibling", "Sibling", "1.0.0");
        WriteManifestJson(siblingFolder, PluginSigningTestHelper.BuildDto("host-test.cycle2-sibling", Path.GetFileName(siblingAssembly)));

        var host = BuildUnsignedAllowedHost(temp.Path);
        await RunUntilRunningAsync(host);

        var diagnostics = GetDiagnostics(host);
        Assert.Equal(PluginRegistryState.DependencyUnmet, Single(diagnostics, "host-test.cycle2-a").State);
        Assert.Equal(PluginRegistryState.DependencyUnmet, Single(diagnostics, "host-test.cycle2-b").State);
        Assert.Equal(PluginRegistryState.Loaded, Single(diagnostics, "host-test.cycle2-sibling").State);

        await StopCleanlyAsync(host);
    }

    [Fact]
    public async Task RunAsync_ThreePluginCycle_AllThreeIsolatedAsDependencyUnmet_NonCyclicSiblingStillLoads()
    {
        using var temp = new TempDirectory();

        var aFolder = CreateFolder(temp.Path, "a-cycle3-a");
        WriteRawManifest(aFolder, "host-test.cycle3-a", dependencies: [("host-test.cycle3-b", "1.0.0", null)]);

        var bFolder = CreateFolder(temp.Path, "b-cycle3-b");
        WriteRawManifest(bFolder, "host-test.cycle3-b", dependencies: [("host-test.cycle3-c", "1.0.0", null)]);

        var cFolder = CreateFolder(temp.Path, "c-cycle3-c");
        WriteRawManifest(cFolder, "host-test.cycle3-c", dependencies: [("host-test.cycle3-a", "1.0.0", null)]);

        var siblingFolder = CreateFolder(temp.Path, "d-noncyclic-sibling");
        var siblingAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            siblingFolder, "Sibling.dll", "host-test.cycle3-sibling", "Sibling", "1.0.0");
        WriteManifestJson(siblingFolder, PluginSigningTestHelper.BuildDto("host-test.cycle3-sibling", Path.GetFileName(siblingAssembly)));

        var host = BuildUnsignedAllowedHost(temp.Path);
        await RunUntilRunningAsync(host);

        var diagnostics = GetDiagnostics(host);
        Assert.Equal(PluginRegistryState.DependencyUnmet, Single(diagnostics, "host-test.cycle3-a").State);
        Assert.Equal(PluginRegistryState.DependencyUnmet, Single(diagnostics, "host-test.cycle3-b").State);
        Assert.Equal(PluginRegistryState.DependencyUnmet, Single(diagnostics, "host-test.cycle3-c").State);
        Assert.Equal(PluginRegistryState.Loaded, Single(diagnostics, "host-test.cycle3-sibling").State);

        await StopCleanlyAsync(host);
    }

    // ==================================================================
    // 9./10. Partial platform failure / combined chaos, and determinism -
    // a single deliberately hostile mix of several failure modes at once
    // (2 healthy, 1 malformed manifest, 1 missing dependency, 1 bad
    // signature, 1 trust-denied, 1 cyclic pair), run TWICE from fresh
    // TempestHost instances against the identical fixture, confirming
    // the outcome is bit-for-bit identical both times.
    // ==================================================================

    [Fact]
    public async Task RunAsync_CombinedChaosScenario_HealthyPluginsLoad_EveryFailureRecordedWithCorrectSpecificState()
    {
        using var temp = new TempDirectory();
        var malformedFolder = BuildChaosPluginsRoot(temp.Path);

        var host = BuildUnsignedAllowedHost(temp.Path);
        await RunUntilRunningAsync(host);

        var diagnostics = GetDiagnostics(host);
        AssertChaosOutcome(diagnostics, malformedFolder);

        await StopCleanlyAsync(host);
    }

    [Fact]
    public async Task RunAsync_CombinedChaosScenario_RunTwiceFromFreshHosts_ProducesIdenticalOutcomeBothTimes()
    {
        using var tempA = new TempDirectory();
        BuildChaosPluginsRoot(tempA.Path);
        var hostA = BuildUnsignedAllowedHost(tempA.Path);
        await RunUntilRunningAsync(hostA);
        var snapshotA = Snapshot(GetDiagnostics(hostA));
        await StopCleanlyAsync(hostA);

        using var tempB = new TempDirectory();
        BuildChaosPluginsRoot(tempB.Path);
        var hostB = BuildUnsignedAllowedHost(tempB.Path);
        await RunUntilRunningAsync(hostB);
        var snapshotB = Snapshot(GetDiagnostics(hostB));
        await StopCleanlyAsync(hostB);

        Assert.Equal(snapshotA.Count, snapshotB.Count);
        Assert.Equal(snapshotA, snapshotB);
    }

    // WP 13.3B: the Theory above only proves RUN-TO-RUN repeatability at a
    // single, fixed (alphabetical a-h) discovery order - never that a
    // genuinely DIFFERENT candidate-processing order settles on the same
    // outcome. This proves that separately: the identical eight-candidate
    // fixture, but built under folder names whose alphabetical order is a
    // deliberately different permutation, must still produce the exact same
    // per-plugin states as the canonical alphabetical baseline.
    [Fact]
    public async Task RunAsync_CombinedChaosScenario_DiscoveryOrderScrambled_ProducesSameOutcomeAsAlphabeticalOrder()
    {
        using var temp = new TempDirectory();
        var malformedFolder = BuildChaosPluginsRootScrambledOrder(temp.Path);

        var host = BuildUnsignedAllowedHost(temp.Path);
        await RunUntilRunningAsync(host);

        var diagnostics = GetDiagnostics(host);
        AssertChaosOutcome(diagnostics, malformedFolder);

        await StopCleanlyAsync(host);
    }

    /// <summary>
    /// Builds the identical eight-candidate combined-chaos fixture as
    /// <see cref="BuildChaosPluginsRoot"/> - same content, same inter-plugin
    /// Ids, same cyclic pair - but under folder name prefixes ordered so
    /// candidate discovery/processing order (alphabetical by folder name) is
    /// a deliberately different permutation from that method's own a-h
    /// order, proving <see cref="AssertChaosOutcome"/>'s own result depends
    /// on the dependency graph and each candidate's own content, never on
    /// which order the filesystem happens to enumerate candidates in.
    /// </summary>
    /// <returns>The malformed candidate's own folder path - see
    /// <see cref="BuildChaosPluginsRoot"/>'s own remarks on why.</returns>
    private static string BuildChaosPluginsRootScrambledOrder(string root)
    {
        var cycleBFolder = CreateFolder(root, "1-chaos-cycle-b");
        WriteRawManifest(cycleBFolder, "host-test.chaos2-cycle-b", dependencies: [("host-test.chaos2-cycle-a", "1.0.0", null)]);

        var trustDeniedFolder = CreateFolder(root, "2-chaos-trust-denied");
        var trustDeniedAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            trustDeniedFolder, "Plugin.dll", "host-test.chaos2-trust-denied", "Chaos Trust Denied", "1.0.0");
        WriteManifestJson(
            trustDeniedFolder,
            PluginSigningTestHelper.BuildDto(
                "host-test.chaos2-trust-denied", Path.GetFileName(trustDeniedAssembly),
                requestedCapabilities: [PluginCapability.DiRegister]));

        var healthy1Folder = CreateFolder(root, "3-chaos-healthy-1");
        var healthy1Assembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            healthy1Folder, "Plugin.dll", "host-test.chaos2-healthy-1", "Chaos Healthy 1", "1.0.0");
        WriteManifestJson(healthy1Folder, PluginSigningTestHelper.BuildDto("host-test.chaos2-healthy-1", Path.GetFileName(healthy1Assembly)));

        var badSigFolder = CreateFolder(root, "4-chaos-bad-signature");
        var badSigAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            badSigFolder, "Plugin.dll", "host-test.chaos2-bad-sig", "Chaos Bad Signature", "1.0.0");
        var badSigDto = PluginSigningTestHelper.BuildDto("host-test.chaos2-bad-sig", Path.GetFileName(badSigAssembly));
        badSigDto.Signature = "{ not a valid envelope";
        WriteManifestJson(badSigFolder, badSigDto);

        var cycleAFolder = CreateFolder(root, "5-chaos-cycle-a");
        WriteRawManifest(cycleAFolder, "host-test.chaos2-cycle-a", dependencies: [("host-test.chaos2-cycle-b", "1.0.0", null)]);

        var malformedFolder = CreateFolder(root, "6-chaos-malformed");
        File.WriteAllText(Path.Combine(malformedFolder, PluginManifestDiscoveryService.ManifestFileName), "{ not valid json at all");

        var missingDepFolder = CreateFolder(root, "7-chaos-missing-dep");
        WriteRawManifest(missingDepFolder, "host-test.chaos2-missing-dep", dependencies:
            [("host-test.chaos2-does-not-exist", "1.0.0", null)]);

        var healthy2Folder = CreateFolder(root, "8-chaos-healthy-2");
        var healthy2Assembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            healthy2Folder, "Plugin.dll", "host-test.chaos2-healthy-2", "Chaos Healthy 2", "1.0.0");
        WriteManifestJson(healthy2Folder, PluginSigningTestHelper.BuildDto("host-test.chaos2-healthy-2", Path.GetFileName(healthy2Assembly)));

        return malformedFolder;
    }

    /// <returns>The malformed candidate's own folder path - the not-valid-JSON
    /// candidate is recorded keyed by folder path, not by any declared Id
    /// (it never parsed far enough to have one) - see the malformed-manifests
    /// scenario's own remarks above for the same, general
    /// InvalidPluginManifestException recording behaviour.</returns>
    private static string BuildChaosPluginsRoot(string root)
    {
        var healthy1Folder = CreateFolder(root, "a-chaos-healthy-1");
        var healthy1Assembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            healthy1Folder, "Plugin.dll", "host-test.chaos2-healthy-1", "Chaos Healthy 1", "1.0.0");
        WriteManifestJson(healthy1Folder, PluginSigningTestHelper.BuildDto("host-test.chaos2-healthy-1", Path.GetFileName(healthy1Assembly)));

        var healthy2Folder = CreateFolder(root, "b-chaos-healthy-2");
        var healthy2Assembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            healthy2Folder, "Plugin.dll", "host-test.chaos2-healthy-2", "Chaos Healthy 2", "1.0.0");
        WriteManifestJson(healthy2Folder, PluginSigningTestHelper.BuildDto("host-test.chaos2-healthy-2", Path.GetFileName(healthy2Assembly)));

        var malformedFolder = CreateFolder(root, "c-chaos-malformed");
        File.WriteAllText(Path.Combine(malformedFolder, PluginManifestDiscoveryService.ManifestFileName), "{ not valid json at all");

        var missingDepFolder = CreateFolder(root, "d-chaos-missing-dep");
        WriteRawManifest(missingDepFolder, "host-test.chaos2-missing-dep", dependencies:
            [("host-test.chaos2-does-not-exist", "1.0.0", null)]);

        var badSigFolder = CreateFolder(root, "e-chaos-bad-signature");
        var badSigAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            badSigFolder, "Plugin.dll", "host-test.chaos2-bad-sig", "Chaos Bad Signature", "1.0.0");
        var badSigDto = PluginSigningTestHelper.BuildDto("host-test.chaos2-bad-sig", Path.GetFileName(badSigAssembly));
        badSigDto.Signature = "{ not a valid envelope";
        WriteManifestJson(badSigFolder, badSigDto);

        var trustDeniedFolder = CreateFolder(root, "f-chaos-trust-denied");
        var trustDeniedAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            trustDeniedFolder, "Plugin.dll", "host-test.chaos2-trust-denied", "Chaos Trust Denied", "1.0.0");
        WriteManifestJson(
            trustDeniedFolder,
            PluginSigningTestHelper.BuildDto(
                "host-test.chaos2-trust-denied", Path.GetFileName(trustDeniedAssembly),
                requestedCapabilities: [PluginCapability.DiRegister]));

        var cycleAFolder = CreateFolder(root, "g-chaos-cycle-a");
        WriteRawManifest(cycleAFolder, "host-test.chaos2-cycle-a", dependencies: [("host-test.chaos2-cycle-b", "1.0.0", null)]);

        var cycleBFolder = CreateFolder(root, "h-chaos-cycle-b");
        WriteRawManifest(cycleBFolder, "host-test.chaos2-cycle-b", dependencies: [("host-test.chaos2-cycle-a", "1.0.0", null)]);

        return malformedFolder;
    }

    private static void AssertChaosOutcome(IDiagnosticsProvider diagnostics, string malformedFolder)
    {
        Assert.Equal(PluginRegistryState.Loaded, Single(diagnostics, "host-test.chaos2-healthy-1").State);
        Assert.Equal(PluginRegistryState.Loaded, Single(diagnostics, "host-test.chaos2-healthy-2").State);
        Assert.Equal(PluginRegistryState.Failed, Single(diagnostics, malformedFolder).State);
        Assert.Equal(PluginRegistryState.Failed, Single(diagnostics, "host-test.chaos2-bad-sig").State);
        Assert.Equal(PluginRegistryState.TrustDenied, Single(diagnostics, "host-test.chaos2-trust-denied").State);
        Assert.Equal(PluginRegistryState.DependencyUnmet, Single(diagnostics, "host-test.chaos2-cycle-a").State);
        Assert.Equal(PluginRegistryState.DependencyUnmet, Single(diagnostics, "host-test.chaos2-cycle-b").State);
        Assert.Equal(PluginRegistryState.DependencyUnmet, Single(diagnostics, "host-test.chaos2-missing-dep").State);

        Assert.Equal(8, diagnostics.Plugins.Count);
    }

    /// <summary>
    /// Normalises each entry's own Id for cross-run comparison: the
    /// malformed-manifest candidate is recorded keyed by its own absolute
    /// folder path (see <see cref="BuildChaosPluginsRoot"/>'s own remarks),
    /// which necessarily differs between two runs against two different
    /// <see cref="TempDirectory"/> roots even though the fixture content is
    /// identical - reduced here to just the folder's own fixed name so the
    /// comparison proves identical OUTCOMES, not identical incidental
    /// temp-path text.
    /// </summary>
    private static List<(string Id, PluginRegistryState State)> Snapshot(IDiagnosticsProvider diagnostics) =>
        diagnostics.Plugins
            .Select(e => (Id: Path.GetFileName(e.Id.TrimEnd(Path.DirectorySeparatorChar)), e.State))
            .OrderBy(pair => pair.Id, StringComparer.Ordinal)
            .ToList();

    // ==================================================================
    // Helpers
    // ==================================================================

    private static string CreateFolder(string root, string folderName)
    {
        var path = Path.Combine(root, folderName);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteManifestJson(string candidateFolder, PluginManifestDto dto) =>
        File.WriteAllText(
            Path.Combine(candidateFolder, PluginManifestDiscoveryService.ManifestFileName),
            PluginSigningTestHelper.ToManifestJson(dto));

    private static void WriteRawManifest(
        string candidateFolder,
        string id,
        IReadOnlyList<(string DependencyId, string MinimumVersion, string? MaximumVersion)>? dependencies = null)
    {
        var dependencyFragments = dependencies?
            .Select(d => PluginManifestJsonBuilder.DependencyFragment.On(d.DependencyId, d.MinimumVersion, d.MaximumVersion))
            .ToList();

        File.WriteAllText(
            Path.Combine(candidateFolder, PluginManifestDiscoveryService.ManifestFileName),
            PluginManifestJsonBuilder.Build(
                id: id,
                assemblyFileName: "DoesNotNeedToExist.dll",
                dependencies: dependencyFragments));
    }

    private static ITempestHost BuildUnsignedAllowedHost(string pluginsRoot)
    {
        var builder = new TempestHostBuilder(Type.EmptyTypes, pluginsRoot);
        builder.AddConfigurationSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>("Plugins:AllowUnsignedLoad", "true"),
        ]));
        return builder.Build();
    }

    // Keyed by host instance so RunUntilRunningAsync/StopCleanlyAsync can be
    // called as a simple, uniform pair at every one of this file's call
    // sites without every caller having to thread its own RunAsync() Task
    // through by hand, mirroring the shape every other Runtime host test in
    // this project already uses inline.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ITempestHost, Task> _runTasksByHost = new();

    private static async Task RunUntilRunningAsync(ITempestHost host)
    {
        var runTask = host.RunAsync();
        _runTasksByHost.AddOrUpdate(host, runTask);

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        Assert.Equal(HostState.Running, host.State);
    }

    private static async Task StopCleanlyAsync(ITempestHost host)
    {
        await host.StopAsync();

        if (_runTasksByHost.TryGetValue(host, out var runTask))
            await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }

    private static IDiagnosticsProvider GetDiagnostics(ITempestHost host) =>
        (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));

    private static PluginRegistryEntry Single(IDiagnosticsProvider diagnostics, string id) =>
        diagnostics.Plugins.Single(e => e.Id == id);

    private static string EffectiveFailureMessage(Exception exception) =>
        exception is TargetInvocationException { InnerException: { } innerException }
            ? innerException.Message
            : exception.Message;

    /// <summary>
    /// Pre-loads a dynamically-built plugin assembly (via the exact same
    /// <see cref="Assembly.LoadFrom(string)"/> path <see cref="Plugins.PluginAssemblyLoader"/>
    /// itself uses during a real Host run - idempotent by path identity, so
    /// the Host's own later load of the identical path returns the same
    /// cached <see cref="Assembly"/>/<see cref="Type"/>) and returns its
    /// single discovered <see cref="IModule"/> type, so it can be
    /// named explicitly in <see cref="TempestHostBuilder"/>'s own
    /// discovery-candidate-types test seam - letting a real Host's Module
    /// Discovery find exactly this one plugin module, without a full
    /// AppDomain scan ever touching this test assembly's own many
    /// internal-visibility-only IModule fixtures (see
    /// TempestHostPluginLifecycleTests's own remarks on that hazard).
    /// </summary>
    private static Type LoadSingleModuleType(string assemblyPath)
    {
        var assembly = Assembly.LoadFrom(assemblyPath);
        return assembly.GetTypes().Single(t =>
            typeof(IModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
    }

    /// <summary>
    /// Writes a real, self-signed <see cref="X509Certificate2"/> into the
    /// process's own <c>AppContext.BaseDirectory/TrustedPublishers</c>
    /// folder under a GUID-derived file name - additive, never replacing
    /// any other entry - and deletes only that one file on disposal. See
    /// this file's own top-level remarks for why a Host-level signature
    /// test has no other way to reach <see cref="Plugins.PluginTrustStore"/>.
    /// </summary>
    private sealed class TrustedPublisherFile : IDisposable
    {
        private readonly string _path;

        public TrustedPublisherFile(X509Certificate2 certificate)
        {
            var folder = Path.Combine(AppContext.BaseDirectory, "TrustedPublishers");
            Directory.CreateDirectory(folder);
            _path = Path.Combine(folder, $"fault-injection-{Guid.NewGuid():N}.cer");
            File.WriteAllBytes(_path, certificate.Export(X509ContentType.Cert));
        }

        public void Dispose()
        {
            try
            {
                File.Delete(_path);
            }
            catch (IOException)
            {
                // Best-effort cleanup only - never fails the test.
            }
        }
    }

    /// <summary>
    /// Builds plugin module fixtures that fail during real instantiation or
    /// a real lifecycle call - as opposed to <see cref="DynamicPluginAssemblyBuilder"/>'s
    /// own fixtures, which are always well-behaved once constructed. Kept
    /// local to this file (never shared, never editing
    /// <see cref="DynamicPluginAssemblyBuilder"/> itself) since no other
    /// WP 13.3A sub-agent's own test file needs a deliberately-throwing
    /// module body.
    /// </summary>
    private static class FaultInjectingPluginAssemblyBuilder
    {
        public static string BuildModuleThatThrowsInConstructor(
            string outputDirectory, string fileName, string moduleId, string moduleName, string moduleVersion, string exceptionMessage)
        {
            var (typeBuilder, assemblyBuilder, dllPath) = DefineModuleType(outputDirectory, fileName, moduleId, moduleName, moduleVersion, "ThrowsInConstructor");

            var baseCtor = typeof(ModuleLifecycleBase).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(string), typeof(string), typeof(string)], null)!;
            var exceptionCtor = typeof(InvalidOperationException).GetConstructor([typeof(string)])!;

            var ctorBuilder = typeBuilder.DefineConstructor(
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                CallingConventions.Standard, Type.EmptyTypes);

            var il = ctorBuilder.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldstr, moduleId);
            il.Emit(OpCodes.Ldstr, moduleName);
            il.Emit(OpCodes.Ldstr, moduleVersion);
            il.Emit(OpCodes.Call, baseCtor);
            il.Emit(OpCodes.Ldstr, exceptionMessage);
            il.Emit(OpCodes.Newobj, exceptionCtor);
            il.Emit(OpCodes.Throw);

            typeBuilder.CreateType();
            assemblyBuilder.Save(dllPath);
            return dllPath;
        }

        public static string BuildModuleThatThrowsInInitialiseAsync(
            string outputDirectory, string fileName, string moduleId, string moduleName, string moduleVersion, string exceptionMessage) =>
            BuildModuleThatThrowsInLifecycleMethod(
                outputDirectory, fileName, moduleId, moduleName, moduleVersion, exceptionMessage,
                nameof(ModuleLifecycleBase.InitialiseAsync), "ThrowsInInitialiseAsync");

        public static string BuildModuleThatThrowsInStartAsync(
            string outputDirectory, string fileName, string moduleId, string moduleName, string moduleVersion, string exceptionMessage) =>
            BuildModuleThatThrowsInLifecycleMethod(
                outputDirectory, fileName, moduleId, moduleName, moduleVersion, exceptionMessage,
                nameof(ModuleLifecycleBase.StartAsync), "ThrowsInStartAsync");

        private static string BuildModuleThatThrowsInLifecycleMethod(
            string outputDirectory, string fileName, string moduleId, string moduleName, string moduleVersion,
            string exceptionMessage, string lifecycleMethodName, string typeSuffix)
        {
            var (typeBuilder, assemblyBuilder, dllPath) = DefineModuleType(outputDirectory, fileName, moduleId, moduleName, moduleVersion, typeSuffix);

            var baseCtor = typeof(ModuleLifecycleBase).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(string), typeof(string), typeof(string)], null)!;

            var ctorBuilder = typeBuilder.DefineConstructor(
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                CallingConventions.Standard, Type.EmptyTypes);
            var ctorIl = ctorBuilder.GetILGenerator();
            ctorIl.Emit(OpCodes.Ldarg_0);
            ctorIl.Emit(OpCodes.Ldstr, moduleId);
            ctorIl.Emit(OpCodes.Ldstr, moduleName);
            ctorIl.Emit(OpCodes.Ldstr, moduleVersion);
            ctorIl.Emit(OpCodes.Call, baseCtor);
            ctorIl.Emit(OpCodes.Ret);

            var baseMethod = typeof(ModuleLifecycleBase).GetMethod(lifecycleMethodName)!;
            var exceptionCtor = typeof(InvalidOperationException).GetConstructor([typeof(string)])!;

            var methodBuilder = typeBuilder.DefineMethod(
                lifecycleMethodName,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                typeof(Task),
                [typeof(CancellationToken)]);

            var methodIl = methodBuilder.GetILGenerator();
            methodIl.Emit(OpCodes.Ldstr, exceptionMessage);
            methodIl.Emit(OpCodes.Newobj, exceptionCtor);
            methodIl.Emit(OpCodes.Throw);

            typeBuilder.DefineMethodOverride(methodBuilder, baseMethod);

            typeBuilder.CreateType();
            assemblyBuilder.Save(dllPath);
            return dllPath;
        }

        private static (TypeBuilder TypeBuilder, PersistedAssemblyBuilder AssemblyBuilder, string DllPath) DefineModuleType(
            string outputDirectory, string fileName, string moduleId, string moduleName, string moduleVersion, string typeSuffix)
        {
            var dllPath = Path.Combine(outputDirectory, fileName);

            var assemblyName = new AssemblyName($"{Path.GetFileNameWithoutExtension(fileName)}-{Guid.NewGuid():N}");
            var assemblyBuilder = new PersistedAssemblyBuilder(assemblyName, typeof(object).Assembly);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

            var typeBuilder = moduleBuilder.DefineType(
                $"{assemblyName.Name}.Dynamic{typeSuffix}PluginModule",
                TypeAttributes.Public | TypeAttributes.Class,
                typeof(ModuleLifecycleBase));

            var metadataAttributeCtor = typeof(ModuleMetadataAttribute).GetConstructor(
                [typeof(string), typeof(string), typeof(string)])!;
            typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(
                metadataAttributeCtor, [moduleId, moduleName, moduleVersion]));

            return (typeBuilder, assemblyBuilder, dllPath);
        }
    }
}
