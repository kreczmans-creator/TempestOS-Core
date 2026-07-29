using Tempest.Core.Events;
using Tempest.Core.Navigation;
using Tempest.Core.Runtime;
using Tempest.Samples;

namespace Tempest.App.Shell;

/// <summary>
/// TempestOS's application shell — <c>Tempest.App</c>'s own composition
/// root (ADR-0033). Constructs and runs a <see cref="ITempestHost"/>,
/// resolves <see cref="INavigationProvider"/>/<see cref="IEventBus"/>
/// through <see cref="ITempestHost.Services"/> (ADR-0034), and presents
/// Navigation and Content regions to a user, using its own private,
/// hand-registered page mapping (ADR-0035).
/// </summary>
/// <remarks>
/// <para>
/// A minimum viable Shell (`WP 5.0D`): a Workspace (the console screen as a
/// whole), a Navigation Region, a Content Region, and a reserved-but-
/// unpopulated Status Bar. No colour, theming, ANSI styling, dialogs, or
/// notifications — clarity over appearance, exactly as this Work Package's
/// own brief requires.
/// </para>
/// <para>
/// Is a composition root, not a module or a hosted service (ADR-0033): it
/// constructs the <see cref="ITempestHost"/> it presents, so it must exist
/// before that Host even begins running — the opposite relationship a
/// module or hosted service has to the Host. <see cref="RunAsync"/> starts
/// the Host's own <see cref="ITempestHost.RunAsync"/> as a background task
/// rather than awaiting it directly, since the Shell's own presentation loop
/// and the Host's own run proceed concurrently.
/// </para>
/// </remarks>
public sealed class TempestShell : IEventHandler<NavigationRequestedEvent>, IAsyncDisposable
{
    private readonly ITempestHost _host;
    private readonly TextWriter _output;
    private readonly TextReader _input;
    private readonly Dictionary<string, IPage> _pages;
    private readonly IPage _unknownPage = new PlaceholderPage(
        "Not Found",
        "No view is registered for this navigation item. This is expected for " +
        "any item a module or plugin contributes beyond this Shell's own " +
        "built-in pages (ADR-0035) - not an error.");

    private INavigationProvider? _navigationProvider;
    private IEventBus? _eventBus;
    private IReadOnlyList<NavigationItem> _currentItems = [];
    private Task? _hostRunTask;

    /// <summary>
    /// Initialises a new instance of the <see cref="TempestShell"/> class.
    /// </summary>
    /// <param name="host">
    /// The Runtime Host this Shell constructs its own composition around.
    /// Must be in <see cref="HostState.Created"/> — not yet run.
    /// </param>
    /// <param name="output">The writer the Shell renders into.</param>
    /// <param name="input">The reader the Shell reads navigation selections from.</param>
    /// <remarks>
    /// Registers this Shell's own built-in pages here, keyed by the exact
    /// <see cref="NavigationItem.Id"/> constants
    /// <see cref="NavigationSampleModule"/>/<see cref="SecondaryNavigationSampleModule"/>
    /// themselves expose, rather than duplicating the literal strings. Note
    /// that <c>NavigationItemId</c> is a compile-time <see langword="const"/>
    /// — the C# compiler inlines its value directly into this assembly's own
    /// IL, so reading it alone does <b>not</b> force <c>Tempest.Samples</c>
    /// to load at runtime. The explicit <c>typeof(...).Assembly</c> access
    /// below is what actually forces the load, before <see cref="RunAsync"/>
    /// starts the Host's own Module Discovery phase — without it, Discovery's
    /// <c>AppDomain.CurrentDomain.GetAssemblies()</c> scan would find zero
    /// <c>Tempest.Samples</c> modules, since nothing else in this process
    /// ever references a non-<see langword="const"/> member of that
    /// assembly.
    /// </remarks>
    public TempestShell(ITempestHost host, TextWriter output, TextReader input)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(input);

        _host = host;
        _output = output;
        _input = input;

        _ = typeof(NavigationSampleModule).Assembly;

        _pages = new Dictionary<string, IPage>(StringComparer.Ordinal)
        {
            [NavigationSampleModule.NavigationItemId] = new PlaceholderPage(
                "Home",
                "This is the Home page - a minimum viable placeholder. " +
                "Navigation, the Event Bus, and Shell composition are all " +
                "real; this page's own content is not."),
            [SecondaryNavigationSampleModule.NavigationItemId] = new PlaceholderPage(
                "Settings",
                "This is the Settings page - a minimum viable placeholder. " +
                "Navigation, the Event Bus, and Shell composition are all " +
                "real; this page's own content is not."),
        };
    }

    /// <summary>
    /// Runs the Shell end to end: starts the Host, resolves platform
    /// services, renders the initial frame, runs the input loop until an
    /// exit is requested, then requests a controlled Host shutdown.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token observed for the Shell's entire run, forwarded directly to
    /// the underlying <see cref="ITempestHost.RunAsync"/>.
    /// </param>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await StartAsync(cancellationToken).ConfigureAwait(false);
        await RunInputLoopAsync(cancellationToken).ConfigureAwait(false);
        await StopAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Starts the Host as a background task, waits for
    /// <see cref="ITempestHost.Services"/> to become available, resolves
    /// <see cref="INavigationProvider"/>/<see cref="IEventBus"/>, subscribes
    /// to <see cref="NavigationRequestedEvent"/>, and renders the initial
    /// frame (title, Navigation Region, reserved Status Bar).
    /// </summary>
    /// <param name="cancellationToken">A token observed while waiting for the Host to start.</param>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _output.WriteLine("====================================");
        _output.WriteLine(" TempestOS");
        _output.WriteLine("====================================");

        _hostRunTask = _host.RunAsync(cancellationToken);

        await WaitForServicesAsync(cancellationToken).ConfigureAwait(false);

        var services = _host.Services!;
        _navigationProvider = (INavigationProvider)services.GetService(typeof(INavigationProvider));
        _eventBus = (IEventBus)services.GetService(typeof(IEventBus));

        _eventBus.Subscribe(this);

        RenderNavigation();
        RenderStatusBar();
    }

    /// <summary>
    /// Reads navigation selections from the Shell's own input reader until
    /// an exit is requested (selection <c>0</c>), re-rendering the
    /// Navigation Region after each processed selection.
    /// </summary>
    /// <param name="cancellationToken">A token observed between reads.</param>
    public async Task RunInputLoopAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await _input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            var shouldContinue = await HandleInputAsync(line, cancellationToken).ConfigureAwait(false);

            if (!shouldContinue)
                break;

            RenderNavigation();
        }
    }

    /// <summary>
    /// Interprets a single line of input against the Navigation Region's
    /// current numbering: <c>0</c> requests exit; a number within range
    /// calls <see cref="INavigationProvider.Navigate"/> for the
    /// corresponding item; anything else is an invalid selection, reported
    /// and otherwise ignored.
    /// </summary>
    /// <param name="input">The line of input to interpret, or <see langword="null"/> (end of input).</param>
    /// <param name="cancellationToken">A token observed while navigating.</param>
    /// <returns>
    /// <see langword="false"/> if exit was requested (or input ended);
    /// otherwise <see langword="true"/>.
    /// </returns>
    public async Task<bool> HandleInputAsync(string? input, CancellationToken cancellationToken = default)
    {
        if (input is null)
            return false;

        var trimmed = input.Trim();

        if (trimmed == "0")
            return false;

        if (int.TryParse(trimmed, out var selection) && selection >= 1 && selection <= _currentItems.Count)
        {
            var item = _currentItems[selection - 1];
            await _navigationProvider!.Navigate(item.Id, cancellationToken).ConfigureAwait(false);
            return true;
        }

        _output.WriteLine("Invalid selection.");
        return true;
    }

    /// <inheritdoc />
    /// <remarks>Renders the Content Region for the requested item.</remarks>
    public Task HandleAsync(NavigationRequestedEvent @event, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);

        RenderContent(@event.Item);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Requests a controlled Host shutdown and awaits the Host's own
    /// background run task.
    /// </summary>
    public async Task StopAsync()
    {
        if (_eventBus is not null)
            _eventBus.Unsubscribe(this);

        await _host.StopAsync().ConfigureAwait(false);

        if (_hostRunTask is not null)
            await _hostRunTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>Disposes the underlying <see cref="ITempestHost"/>.</remarks>
    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync().ConfigureAwait(false);
    }

    private async Task WaitForServicesAsync(CancellationToken cancellationToken)
    {
        while (_host.Services is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }
    }

    private void RenderNavigation()
    {
        _currentItems = _navigationProvider!.Items;

        _output.WriteLine();
        _output.WriteLine("Navigation");
        _output.WriteLine("----------");

        for (var i = 0; i < _currentItems.Count; i++)
            _output.WriteLine($"{i + 1} - {_currentItems[i].Title}");

        _output.WriteLine("0 - Exit");
        _output.Write("> ");
    }

    private void RenderContent(NavigationItem item)
    {
        var page = _pages.TryGetValue(item.Id, out var found) ? found : _unknownPage;

        _output.WriteLine();
        _output.WriteLine("Content");
        _output.WriteLine("-------");
        page.Render(_output);
    }

    private void RenderStatusBar()
    {
        _output.WriteLine();
        _output.WriteLine("------------------------------------");
        _output.WriteLine("Status: (reserved for future use)");
    }
}
