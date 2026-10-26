using Avalonia.Threading;
using Tempest.Calc.Library;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// Addendum C, C-6: the state behind Settings → Demo project — the two menu actions "Install demo project" and "Remove demo project", with no UI in it.
/// Neither runs by itself: each asks the person first, in the application's own confirmation dialog (the same Yes/No overlay "Switch person" uses; nothing
/// that answers for the person), says in one plain sentence what it did, and tells the shell whether the workspace changed so the Project Explorer and the
/// other lists refresh. The install and the removal themselves are <see cref="CalcDemoProjectService"/>'s; this class never touches a row.
/// <para>
/// Threading: every method is called on the UI thread and continues on it. The install makes about sixty calculations, so it runs on the pool, and its
/// progress lines reach <see cref="Changed"/> on the UI thread whichever thread reported them.
/// </para>
/// </summary>
public sealed class DemoProjectViewModel
{
    public const string InstallCaption = "Install demo project";
    public const string RemoveCaption = "Remove demo project";

    /// <summary>The install's confirmation title and button.</summary>
    public const string InstallConfirmTitle = "Install demo project";

    /// <summary>The removal's confirmation title and button.</summary>
    public const string RemoveConfirmTitle = "Remove demo project";

    private readonly CalcDemoProjectService _service;
    private readonly Func<string, string, string, Task<bool>> _confirm;
    private readonly Func<Guid, bool>? _isOpen;
    private bool _busy;

    /// <param name="service">The demonstration project's install and removal.</param>
    /// <param name="confirm">Asks the person (title, message, confirm button) and answers whether they agreed: the shell's <c>ConfirmationDialog.ConfirmAsync</c>. It must wait for the person; an answer given without them would install or remove the demonstration unasked.</param>
    /// <param name="isOpen">Whether the project with that id is the one open now (a removal is refused while it is); <see langword="null"/> treats no project as open.</param>
    public DemoProjectViewModel(CalcDemoProjectService service, Func<string, string, string, Task<bool>> confirm, Func<Guid, bool>? isOpen = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _confirm = confirm ?? throw new ArgumentNullException(nameof(confirm));
        _isOpen = isOpen;
    }

    /// <summary>Raised (on the UI thread) whenever anything the section shows has changed.</summary>
    public event Action? Changed;

    /// <summary>Raised (on the UI thread) when an act finished, with its sentence and whether the workspace changed — the shell's report-then-refresh convention.</summary>
    public event Action<string, ActionOutcome>? Completed;

    /// <summary>Whether the demonstration project is in this database now.</summary>
    public bool IsInstalled { get; private set; }

    /// <summary>The project's id when it is installed.</summary>
    public Guid? ProjectId { get; private set; }

    /// <summary>An install or a removal is running.</summary>
    public bool IsBusy => _busy;

    /// <summary>The install can be asked for: it is not installed and nothing is running.</summary>
    public bool CanInstall => !_busy && !IsInstalled;

    /// <summary>The removal can be asked for: it is installed and nothing is running.</summary>
    public bool CanRemove => !_busy && IsInstalled;

    /// <summary>One plain sentence: what is installed, what is running, or what the last act did.</summary>
    public string Status { get; private set; } = string.Empty;

    /// <summary>What an install makes, in a sentence, for the section's text.</summary>
    public static string About { get; } =
        $"{CalcDemoProject.DisplayName} is an invented press cell with every linked chain this build ships, 13 packs of draft calculations built from invented data. "
        + "Every sheet of it says DEMO. Install it to try the application; remove it when you are done. It is never installed unless you ask, and removing it leaves every other project untouched.";

    /// <summary>Reads whether the demonstration is installed (on landing in Settings, and after every act).</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var status = await _service.StatusAsync(cancellationToken).ConfigureAwait(true);
        IsInstalled = status.Installed;
        ProjectId = status.ProjectId;

        if (!_busy && Status.Length == 0)
            Status = IsInstalled ? $"The demo project '{CalcDemoProject.DisplayName}' is installed." : "The demo project is not installed.";

        RaiseChanged();
    }

    /// <summary>"Install demo project": asks, then installs. Does nothing while something runs.</summary>
    public async Task InstallAsync(CancellationToken cancellationToken = default)
    {
        if (_busy)
            return;

        Status = string.Empty;
        await RefreshAsync(cancellationToken).ConfigureAwait(true);

        if (IsInstalled)
        {
            Finish($"The demo project '{CalcDemoProject.DisplayName}' is installed already; remove it before installing it again.", ActionOutcome.Failed);
            return;
        }

        var agreed = await _confirm(
            InstallConfirmTitle,
            $"Add the project '{CalcDemoProject.DisplayName}' to this database? It holds about sixty draft calculations in 13 packs, all built from invented data, every sheet marked DEMO. "
            + "Nothing else in your data is changed, and you can remove it again from Settings.",
            "Install").ConfigureAwait(true);

        if (!agreed)
        {
            Finish("Install demo project was cancelled.", ActionOutcome.NoChange);
            return;
        }

        await RunAsync(progress => _service.InstallAsync(progress, cancellationToken), "Installing the demo project…", cancellationToken).ConfigureAwait(true);
    }

    /// <summary>"Remove demo project": asks, then removes the demonstration and only the demonstration. Does nothing while something runs.</summary>
    public async Task RemoveAsync(CancellationToken cancellationToken = default)
    {
        if (_busy)
            return;

        Status = string.Empty;
        await RefreshAsync(cancellationToken).ConfigureAwait(true);

        if (!IsInstalled)
        {
            Finish("There is no demo project to remove.", ActionOutcome.Failed);
            return;
        }

        if (ProjectId is { } id && _isOpen?.Invoke(id) == true)
        {
            Finish($"Close the demo project '{CalcDemoProject.DisplayName}' before removing it.", ActionOutcome.Failed);
            return;
        }

        var agreed = await _confirm(
            RemoveConfirmTitle,
            $"Remove the project '{CalcDemoProject.DisplayName}' and everything in it, including anything you added to it? No other project is touched. This cannot be undone here.",
            "Remove").ConfigureAwait(true);

        if (!agreed)
        {
            Finish("Remove demo project was cancelled.", ActionOutcome.NoChange);
            return;
        }

        await RunAsync(_ => _service.RemoveAsync(cancellationToken), "Removing the demo project…", cancellationToken).ConfigureAwait(true);
    }

    private async Task RunAsync(Func<IProgress<string>, Task<CalcDemoOutcome>> act, string starting, CancellationToken cancellationToken)
    {
        _busy = true;
        Status = starting;
        RaiseChanged();

        CalcDemoOutcome outcome;

        try
        {
            // The install is mostly calculation: it runs on the pool, so the window stays alive while it works.
            var progress = new Progress<string>(line =>
            {
                Status = line;
                RaiseChanged();
            });
            outcome = await Task.Run(() => act(progress), cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            outcome = new CalcDemoOutcome(false, "The demo project act was cancelled; whatever it had started was undone.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or Tempest.Core.Persistence.PersistenceException)
        {
            // The service reports its own refusals as outcomes; this is the store itself failing, said without a stack trace.
            outcome = new CalcDemoOutcome(false, $"The demo project act failed ({ex.Message.TrimEnd('.')}); nothing more was done.");
        }
        finally
        {
            _busy = false;
        }

        await RefreshAsync(CancellationToken.None).ConfigureAwait(true);
        Finish(outcome.Message, outcome.Succeeded ? ActionOutcome.Changed : ActionOutcome.Failed);
    }

    private void Finish(string message, ActionOutcome outcome)
    {
        Status = message;
        RaiseChanged();
        Completed?.Invoke(message, outcome);
    }

    private void RaiseChanged()
    {
        if (Changed is not { } handler)
            return;

        if (Dispatcher.UIThread.CheckAccess())
            handler();
        else
            Dispatcher.UIThread.Post(handler);
    }
}
