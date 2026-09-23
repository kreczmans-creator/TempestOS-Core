namespace Tempest.Desktop.Tests;

/// <summary>
/// The `Tempest.Desktop`-specific half of
/// `Tempest.Core.Tests.Runtime.FrozenLayersUnreachableTests` — that file's
/// own reflection sweep covers every `src/` assembly
/// `Tempest.Core.Tests` can reference (Core, Samples, Workspace,
/// Validation, Harness); `Tempest.Desktop` itself is Avalonia-only and is
/// only reachable from this test project, so it gets its own, matching
/// assertion here rather than being left uncovered by the Core-side sweep.
/// </summary>
public sealed class FrozenLayersUnreachableFromDesktopTests
{
    /// <summary>Kept identical to <c>Tempest.Core.Tests.Runtime.FrozenLayersUnreachableTests.FrozenTypeNames</c> — see that file's own remarks for where each name comes from.</summary>
    private static readonly string[] FrozenTypeNames =
    [
        "RestApiHostedService", "ApiRequestHandler",
        "PluginAssemblyLoader", "PluginAssemblyLoadException", "PluginAssemblyNotFoundException",
        "IPluginAssemblyLoader", "PluginSignatureVerifier", "PluginSignatureEnvelope",
        "PluginSignatureVerificationFailedException", "PluginTrustStore", "IPluginTrustStore",
        "PluginTrustDeniedException", "PluginTrustPermission", "PluginTrustTier", "PluginCapability",
        "PluginComponentPrincipalRegistry", "IPluginComponentPrincipalRegistry", "IPluginComponentPrincipalRecorder",
        "PluginDeniedTypeRegistry", "IPluginDeniedTypeRegistry", "IPluginDeniedTypeRecorder",
        "PluginUnsignedLoadNotAllowedException",
        "ILicense", "ILicenseProvider", "ILicenseValidator", "License", "LicenseDto", "LicenseProvider",
        "LicenseValidationException", "LicenseValidationResult", "LicenseValidator", "LicensingException",
    ];

    [Fact]
    public void TheDesktopAssembly_DeclaresNoFrozenPluginLicensingOrRestApiType()
    {
        var assembly = typeof(MainWindow).Assembly;

        var found = assembly.GetTypes()
            .Where(type => Array.IndexOf(FrozenTypeNames, type.Name) >= 0)
            .Select(type => type.FullName)
            .ToList();

        Assert.True(found.Count == 0,
            "Expected no frozen plugin-loading/licensing/REST-API type in Tempest.Desktop. Found: " + string.Join(", ", found));
    }
}
