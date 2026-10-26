using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;

namespace Tempest.Desktop.Tests.Soak;

/// <summary>Switches every optional calculator on (the soak covers all 54 templates), naming itself as the source.</summary>
internal sealed class SoakFeatureSource : ICalcFeatureSource
{
    public CalcFeatureFlags Read(out string? problem)
    {
        problem = null;
        return new CalcFeatureFlags([CalcFeatures.Lifting], "the soak harness (every optional calculator on)");
    }
}

/// <summary>
/// The real composition the Desktop uses, headless: <see cref="WorkspaceHost"/> over a real SQLite database in a temporary folder, with the
/// Evidence service, the PDF sheet renderer, the catalogue of every calculator and the one calculation service. The soak talks to the services
/// the application talks to and to nothing the application does not have.
/// </summary>
internal sealed class SoakHost : IAsyncDisposable
{
    public const string Author = "soak.author";
    public const string Checker = "soak.checker";

    private readonly PrincipalSession _session;
    private bool _disposed;

    private SoakHost(WorkspaceHost host, string root)
    {
        Host = host;
        Root = root;
        _session = (PrincipalSession)host.Services!.GetService(typeof(PrincipalSession));
        Domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
        Store = Domain.PersistenceStore;
    }

    public WorkspaceHost Host { get; }

    public string Root { get; }

    public EngineeringDomainContext Domain { get; }

    public IQueryablePersistenceStore Store { get; }

    public CalcInstanceService Service => Host.CalcInstances!;

    public IEvidenceService Evidence => Host.EvidenceService!;

    public string DatabasePath => Path.Combine(Root, SqlitePersistenceStore.DatabaseFileName);

    /// <summary>Starts the host over a fresh folder; the signed-in person is the soak's author.</summary>
    public static async Task<SoakHost> StartAsync(string root, bool fullDurability)
    {
        IReadOnlyList<string>? args = fullDurability ? [$"--{SqlitePersistenceStore.SynchronousConfigurationKey}=Full"] : null;
        var host = new WorkspaceHost(root, commandLineArgs: args, calcFeatureSource: new SoakFeatureSource());

        try
        {
            await host.StartAsync().ConfigureAwait(false);
        }
        catch
        {
            await host.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        var soak = new SoakHost(host, root);
        soak.SignInAs(Author);
        return soak;
    }

    /// <summary>Signs in <paramref name="identity"/>: the calculation service reads the signed-in person for authorship and for the independent check.</summary>
    public void SignInAs(string identity) =>
        _session.Establish(new PlatformPrincipal(new PlatformIdentity(identity, identity), ApplicationPermissions.LocalSession));

    /// <summary>The raw JSON the store holds for an instance: what a restart would read back.</summary>
    public async Task<string?> RawInstanceAsync(CalcInstance instance, CancellationToken cancellationToken = default)
    {
        var key = CalcInstanceStore.Key(instance.ProjectId, instance.Id);
        var read = await Store.ReadManyAsync(CalcInstanceStore.InstanceCollection, [key], cancellationToken).ConfigureAwait(false);
        return read.GetValueOrDefault(key);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            await Host.ShutdownAsync().ConfigureAwait(false);
        }
        finally
        {
            await Host.DisposeAsync().ConfigureAwait(false);
        }
    }
}
