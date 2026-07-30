using Tempest.Core.Licensing;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Runtime;

// Registration validation: proves Licensing is wired into the real,
// unmodified TempestHost exactly as Service Registration Matrix.md/
// Service Lifecycle.md specify - ILicenseValidator runs pre-container,
// before Logging Built, never itself resolved through the container;
// ILicenseProvider is Composition-Root-constructed and AddInstance-
// registered at Phase 6. Also proves ADR-0050's own resolution of Risk
// Register.md's R5: a missing license file starts the Host normally
// (Unlicensed default); a malformed or expired one is Host-fatal,
// mirroring RunAsync_ConfigurationFailure_IsHostFatal_TransitionsToFaulted's
// own established pattern for a different pre-container failure.
[Collection("Console output capture")]
public class LicenseHostRegistrationTests
{
    private static async Task RunAgainstRunningHostAsync(string? licenseFilePath, Func<ITempestHost, Task> body)
    {
        var host = new TempestHostBuilder(Type.EmptyTypes, pluginsRootPathOverride: null, hostedServiceCandidateTypesOverride: Type.EmptyTypes, licenseFilePathOverride: licenseFilePath)
            .Build();
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(new StringWriter());

            var runTask = host.RunAsync();

            while (host.State is HostState.Created or HostState.Starting)
                await Task.Delay(5);

            await body(host);

            await host.StopAsync();
            await runTask;
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    // ------------------------------------------------------------------
    // Missing license file: R5's own resolution - not Host-fatal
    // ------------------------------------------------------------------

    [Fact]
    public Task Host_NoLicenseFile_StartsNormally_WithAnUnlicensedDefaultLicense() =>
        RunAgainstRunningHostAsync(null, host =>
        {
            Assert.Equal(HostState.Running, host.State);

            var licenseProvider = (ILicenseProvider)host.Services!.GetService(typeof(ILicenseProvider));

            Assert.Equal(LicenseValidator.UnlicensedLicenseeName, licenseProvider.CurrentLicense.LicenseeName);
            Assert.False(licenseProvider.HasCapability("anything"));

            return Task.CompletedTask;
        });

    [Fact]
    public Task Host_RegistersILicenseProvider_Resolvable() =>
        RunAgainstRunningHostAsync(null, host =>
        {
            var licenseProvider = host.Services!.GetService(typeof(ILicenseProvider));

            Assert.IsType<LicenseProvider>(licenseProvider);

            return Task.CompletedTask;
        });

    [Fact]
    public Task Host_ResolvingILicenseProviderTwice_ReturnsTheSameInstance() =>
        RunAgainstRunningHostAsync(null, host =>
        {
            var first = host.Services!.GetService(typeof(ILicenseProvider));
            var second = host.Services!.GetService(typeof(ILicenseProvider));

            Assert.Same(first, second);

            return Task.CompletedTask;
        });

    [Fact]
    public Task Host_ILicenseValidator_IsNeverResolvableThroughTheContainer() =>
        RunAgainstRunningHostAsync(null, host =>
        {
            Assert.Throws<Tempest.Core.DependencyInjection.ServiceNotRegisteredException>(() =>
                host.Services!.GetService(typeof(ILicenseValidator)));

            return Task.CompletedTask;
        });

    // ------------------------------------------------------------------
    // Valid license file
    // ------------------------------------------------------------------

    [Fact]
    public async Task Host_ValidLicenseFile_ExposesTheLicensedLicenseeAndCapabilities()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "license.json");
        File.WriteAllText(path, """{"LicenseeName":"Acme Corp","EnabledCapabilities":["feature.a"]}""");

        await RunAgainstRunningHostAsync(path, host =>
        {
            var licenseProvider = (ILicenseProvider)host.Services!.GetService(typeof(ILicenseProvider));

            Assert.Equal("Acme Corp", licenseProvider.CurrentLicense.LicenseeName);
            Assert.True(licenseProvider.HasCapability("feature.a"));

            return Task.CompletedTask;
        });
    }

    // ------------------------------------------------------------------
    // Invalid license: Host-fatal, mirroring the existing Configuration-
    // failure precedent for a different pre-container condition
    // ------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_MalformedLicenseFile_IsHostFatal_TransitionsToFaulted()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "license.json");
        File.WriteAllText(path, "not valid json {{{");

        var host = new TempestHostBuilder(Type.EmptyTypes, pluginsRootPathOverride: null, hostedServiceCandidateTypesOverride: Type.EmptyTypes, licenseFilePathOverride: path)
            .Build();
        var originalOut = Console.Out;
        var originalError = Console.Error;

        try
        {
            Console.SetOut(new StringWriter());
            Console.SetError(new StringWriter());

            await Assert.ThrowsAsync<LicenseValidationException>(() => host.RunAsync());

            Assert.Equal(HostState.Faulted, host.State);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task RunAsync_ExpiredLicenseFile_IsHostFatal_TransitionsToFaulted()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "license.json");
        var pastExpiry = DateTimeOffset.UtcNow.AddDays(-1).ToString("O");
        File.WriteAllText(path, $$"""{"LicenseeName":"Acme Corp","ExpiresAt":"{{pastExpiry}}"}""");

        var host = new TempestHostBuilder(Type.EmptyTypes, pluginsRootPathOverride: null, hostedServiceCandidateTypesOverride: Type.EmptyTypes, licenseFilePathOverride: path)
            .Build();
        var originalOut = Console.Out;
        var originalError = Console.Error;

        try
        {
            Console.SetOut(new StringWriter());
            Console.SetError(new StringWriter());

            var exception = await Assert.ThrowsAsync<LicenseValidationException>(() => host.RunAsync());

            Assert.Contains("expired", exception.FailureReason, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(HostState.Faulted, host.State);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task RunAsync_MissingLicenseeNameField_IsHostFatal_TransitionsToFaulted()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "license.json");
        File.WriteAllText(path, """{"EnabledCapabilities":["feature.a"]}""");

        var host = new TempestHostBuilder(Type.EmptyTypes, pluginsRootPathOverride: null, hostedServiceCandidateTypesOverride: Type.EmptyTypes, licenseFilePathOverride: path)
            .Build();
        var originalOut = Console.Out;
        var originalError = Console.Error;

        try
        {
            Console.SetOut(new StringWriter());
            Console.SetError(new StringWriter());

            await Assert.ThrowsAsync<LicenseValidationException>(() => host.RunAsync());

            Assert.Equal(HostState.Faulted, host.State);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
