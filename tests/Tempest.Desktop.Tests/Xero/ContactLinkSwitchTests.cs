using System.Reflection;
using System.Runtime.ExceptionServices;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.ReferenceData;
using Tempest.Desktop.Views;
using static Tempest.Desktop.Tests.Xero.ContactLinkViewTests;

namespace Tempest.Desktop.Tests.Xero;

/// <summary>
/// `v0.24.0` U2 verifier fixes, round 3: while another organisation is being
/// opened the Xero section of the previous one is withdrawn, and Link / Unlink
/// act only on the organisation the section was rendered for; a refresh that
/// ends early still clears busy; a caller's cancellation during a write closes
/// the prompt once the write ends without a link.
/// </summary>
public sealed class ContactLinkSwitchTests
{
    [AvaloniaFact]
    public async Task UnlinkPressedWhileAnotherOrganisationIsBeingOpened_UnlinksNeither()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await LinkBothAsync(kit);
        var (contacts, hold) = HeldContacts(kit);
        var (window, view) = await ShowAsync(kit, contacts, kit.Organisations);
        await view.SelectAsync("ACME1");
        await kit.WaitAsync(() => Text(view, CustomersSuppliersView.XeroLinkStateName).StartsWith("Linked to Acme Engineering Ltd in Xero", StringComparison.Ordinal) && !view.IsXeroBusy);

        var gate = hold.Arm();
        var opening = view.SelectAsync("BRUNL");
        await kit.WaitAsync(() => hold.Entered);

        // The form already points at BRUNL; the section showing ACME1 is withdrawn and the press does nothing.
        Assert.Equal("BRUNL", view.EditingRecordId);
        Assert.False(Control(view, CustomersSuppliersView.XeroLinkStateName).IsEffectivelyVisible);
        Assert.False(Button(view, CustomersSuppliersView.UnlinkFromXeroName).IsEnabled);
        Click(view, CustomersSuppliersView.UnlinkFromXeroName);
        Click(view, CustomersSuppliersView.LinkToXeroName);
        Assert.False(view.XeroLinkPrompt!.IsVisible);

        gate.SetResult();
        await opening;
        await kit.WaitAsync(() => Text(view, CustomersSuppliersView.XeroLinkStateName).StartsWith("Linked to Brunel Fabrication Ltd in Xero", StringComparison.Ordinal) && !view.IsXeroBusy);

        Assert.NotNull(await kit.Linker.FindLinkAsync("ACME1"));
        Assert.NotNull(await kit.Linker.FindLinkAsync("BRUNL"));
        Assert.DoesNotContain(kit.Audit.Rows, r => r.Action == XeroContactLinker.AuditLinkUnlinked);
        window.Close();
    }

    [AvaloniaFact]
    public async Task AnUnlinkActsOnTheOrganisationTheSectionWasRenderedFor_NotOnALaterFormLookup()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await LinkBothAsync(kit);
        var (organisations, control) = InterceptingProxy<IOrganisationCatalog>.Wrap(kit.Organisations);
        var (window, view) = await ShowAsync(kit, kit.Contacts, organisations);
        await view.SelectAsync("ACME1");
        await kit.WaitAsync(() => Text(view, CustomersSuppliersView.XeroLinkStateName).StartsWith("Linked to Acme Engineering Ltd in Xero", StringComparison.Ordinal) && !view.IsXeroBusy);

        // A look-up made at the click would now answer BRUNL for the form's record.
        control.Intercept = (method, _) => method.Name == nameof(IOrganisationCatalog.FindAsync) ? kit.Organisations.FindAsync("BRUNL") : null;
        Click(view, CustomersSuppliersView.UnlinkFromXeroName);
        await kit.WaitAsync(() => kit.Audit.Rows.Any(r => r.Action == XeroContactLinker.AuditLinkUnlinked));
        control.Intercept = null;
        await kit.WaitAsync(() => !view.IsXeroBusy && Text(view, CustomersSuppliersView.XeroStatusName).StartsWith("Unlinked", StringComparison.Ordinal));

        Assert.Null(await kit.Linker.FindLinkAsync("ACME1"));
        Assert.NotNull(await kit.Linker.FindLinkAsync("BRUNL"));
        Assert.StartsWith("Unlinked 'Acme Engineering Ltd'", Text(view, CustomersSuppliersView.XeroStatusName), StringComparison.Ordinal);
        window.Close();
    }

    [AvaloniaFact]
    public async Task ADetailsReadForThePreviousOrganisation_IsNotShownUnderTheNextOne()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await LinkBothAsync(kit);
        var (contacts, hold) = HeldContacts(kit);
        var (window, view) = await ShowAsync(kit, contacts, kit.Organisations);

        TaskCompletionSource gate;
        Task opening;
        using (kit.Simulator.HoldRequests())
        {
            await view.SelectAsync("ACME1");
            await kit.WaitAsync(() => kit.Simulator.InFlight == 1);

            gate = hold.Arm();
            opening = view.SelectAsync("BRUNL");
            await kit.WaitAsync(() => hold.Entered);
        }

        // ACME1's details answer arrives while BRUNL is still being opened.
        await kit.WaitAsync(() => kit.Simulator.InFlight == 0);
        for (var i = 0; i < 10; i++)
        {
            await Task.Delay(10);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal("BRUNL", view.EditingRecordId);
        Assert.DoesNotContain("Acme", Text(view, CustomersSuppliersView.XeroLinkStateName), StringComparison.Ordinal);
        Assert.False(Control(view, CustomersSuppliersView.XeroLinkStateName).IsEffectivelyVisible);

        gate.SetResult();
        await opening;
        await kit.WaitAsync(() => Text(view, CustomersSuppliersView.XeroLinkStateName).StartsWith("Linked to Brunel Fabrication Ltd in Xero", StringComparison.Ordinal) && !view.IsXeroBusy);
        window.Close();
    }

    [AvaloniaFact]
    public async Task ARefreshEndingEarly_OnAStoreFault_ClearsBusy_EvenWithAnOlderReadInFlight()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await LinkBothAsync(kit);
        var (organisations, control) = InterceptingProxy<IOrganisationCatalog>.Wrap(kit.Organisations);
        var (window, view) = await ShowAsync(kit, kit.Contacts, organisations);
        await view.SelectAsync("ACME1");
        await kit.WaitAsync(() => Text(view, CustomersSuppliersView.XeroLinkStateName).StartsWith("Linked to", StringComparison.Ordinal) && !view.IsXeroBusy);

        using (kit.Simulator.HoldRequests())
        {
            Click(view, CustomersSuppliersView.RefreshFromXeroName);
            await kit.WaitAsync(() => kit.Simulator.InFlight == 1 && view.IsXeroBusy);

            control.Intercept = (method, _) => method.Name == nameof(IOrganisationCatalog.FindAsync)
                ? Task.FromException<IReferenceRecord<Organisation>?>(new IOException("The organisation store is unreadable."))
                : null;
            await view.RefreshXeroAsync(readDetails: false);
            Assert.Equal("Could not read the Xero link: The organisation store is unreadable.", Text(view, CustomersSuppliersView.XeroStatusName));
            Assert.False(view.IsXeroBusy);
            Assert.True(Button(view, CustomersSuppliersView.RefreshFromXeroName).IsEnabled);
        }

        await kit.WaitAsync(() => kit.Simulator.InFlight == 0);
        control.Intercept = null;
        Assert.False(view.IsXeroBusy);
        window.Close();
    }

    [AvaloniaFact]
    public async Task ARefreshEndingEarly_OnAMissingOrganisation_ClearsBusy_EvenWithAnOlderReadInFlight()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await LinkBothAsync(kit);
        var (organisations, control) = InterceptingProxy<IOrganisationCatalog>.Wrap(kit.Organisations);
        var (window, view) = await ShowAsync(kit, kit.Contacts, organisations);
        await view.SelectAsync("ACME1");
        await kit.WaitAsync(() => Text(view, CustomersSuppliersView.XeroLinkStateName).StartsWith("Linked to", StringComparison.Ordinal) && !view.IsXeroBusy);

        using (kit.Simulator.HoldRequests())
        {
            Click(view, CustomersSuppliersView.RefreshFromXeroName);
            await kit.WaitAsync(() => kit.Simulator.InFlight == 1 && view.IsXeroBusy);

            control.Intercept = (method, _) => method.Name == nameof(IOrganisationCatalog.FindAsync)
                ? Task.FromResult<IReferenceRecord<Organisation>?>(null)
                : null;
            await view.RefreshXeroAsync(readDetails: false);
            Assert.False(view.IsXeroBusy);
            Assert.False(Control(view, CustomersSuppliersView.XeroLinkStateName).IsEffectivelyVisible);
        }

        await kit.WaitAsync(() => kit.Simulator.InFlight == 0);
        control.Intercept = null;
        Assert.False(view.IsXeroBusy);
        window.Close();
    }

    [AvaloniaFact]
    public async Task CallerCancelsWhileACreateRuns_AndTheCreateFails_ThePromptClosesAsCancelled()
    {
        var linker = new GatedLinker();
        var prompt = new XeroContactLinkPrompt(linker);
        var window = new Window { Content = new Panel { Children = { prompt } }, Width = 900, Height = 700 };
        window.Show();
        using var cancellation = new CancellationTokenSource();

        var answer = prompt.PromptAsync("ACME1", "Acme Engineering Ltd", cancellation.Token);
        await DesktopTestHelpers.WaitUntilAsync(() => !prompt.IsBusy && Button(prompt, XeroContactLinkPrompt.CreateButtonName).IsEnabled, 20);
        Click(prompt, XeroContactLinkPrompt.CreateButtonName);
        await DesktopTestHelpers.WaitUntilAsync(() => prompt.IsBusy, 20);

        await cancellation.CancelAsync();
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(10);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }

        // The write is never cancelled: the prompt waits for its answer.
        Assert.False(answer.IsCompleted);
        Assert.True(prompt.IsVisible);

        linker.Release(ConnectorResult<XeroLink>.Unavailable("Xero did not answer."));
        await DesktopTestHelpers.WaitUntilAsync(() => answer.IsCompleted, 20);
        Assert.Null(await answer);
        Assert.False(prompt.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public async Task CallerCancelsWhileACreateRuns_AndTheCreateSucceeds_TheLinkIsReturned()
    {
        var linker = new GatedLinker();
        var prompt = new XeroContactLinkPrompt(linker);
        var window = new Window { Content = new Panel { Children = { prompt } }, Width = 900, Height = 700 };
        window.Show();
        using var cancellation = new CancellationTokenSource();

        var answer = prompt.PromptAsync("ACME1", "Acme Engineering Ltd", cancellation.Token);
        await DesktopTestHelpers.WaitUntilAsync(() => !prompt.IsBusy && Button(prompt, XeroContactLinkPrompt.CreateButtonName).IsEnabled, 20);
        Click(prompt, XeroContactLinkPrompt.CreateButtonName);
        await DesktopTestHelpers.WaitUntilAsync(() => prompt.IsBusy, 20);
        await cancellation.CancelAsync();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var link = new XeroLink(XeroLink.CurrentSchemaVersion, ContactLinkTestKit.TenantId, new XeroDocumentRef(XeroDocumentKind.Contact, "ACME1"), "contact-1", null, null, "ACTIVE", null, null, DateTimeOffset.UnixEpoch, null, XeroContactLinker.LinkedByCreated);
        linker.Release(ConnectorResult<XeroLink>.Ok(link));
        await DesktopTestHelpers.WaitUntilAsync(() => answer.IsCompleted, 20);
        Assert.Same(link, await answer);
        Assert.False(prompt.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public async Task LinkToXeroPressedWhileAnotherOrganisationIsBeingLookedUp_DoesNotOpenThePrompt()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        await kit.AddOrganisationAsync(reference: "BRUNL", name: "Brunel Fabrication Ltd", customerCode: "BRUNL");
        var (organisations, control) = InterceptingProxy<IOrganisationCatalog>.Wrap(kit.Organisations);
        var (window, view) = await ShowAsync(kit, kit.Contacts, organisations);
        await view.SelectAsync("ACME1");
        await kit.WaitAsync(() => Button(view, CustomersSuppliersView.LinkToXeroName).IsEffectivelyVisible && !view.IsXeroBusy);

        // Hold the look-up of BRUNL: the form still shows ACME1 meanwhile.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = false;
        control.Intercept = (method, args) =>
            method.Name == nameof(IOrganisationCatalog.FindAsync) && Equals(args![0], "BRUNL") && !entered
                ? HeldFindAsync()
                : null;

        async Task<IReferenceRecord<Organisation>?> HeldFindAsync()
        {
            entered = true;
            await gate.Task.ConfigureAwait(true);
            return await kit.Organisations.FindAsync("BRUNL").ConfigureAwait(true);
        }

        var opening = view.SelectAsync("BRUNL");
        await kit.WaitAsync(() => entered);

        Assert.False(Control(view, CustomersSuppliersView.XeroLinkStateName).IsEffectivelyVisible);
        Click(view, CustomersSuppliersView.LinkToXeroName);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.False(view.XeroLinkPrompt!.IsVisible);

        gate.SetResult();
        await opening;
        await kit.WaitAsync(() => view.EditingRecordId == "BRUNL" && Button(view, CustomersSuppliersView.LinkToXeroName).IsEffectivelyVisible && !view.IsXeroBusy);
        Assert.False(view.XeroLinkPrompt.IsVisible);
        control.Intercept = null;
        window.Close();
    }

    [AvaloniaFact]
    public async Task AStoreFaultOnTheFirstRefreshAfterSwitching_IsShown()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await LinkBothAsync(kit);
        var (organisations, control) = InterceptingProxy<IOrganisationCatalog>.Wrap(kit.Organisations);
        var (window, view) = await ShowAsync(kit, kit.Contacts, organisations);
        await view.SelectAsync("ACME1");
        await kit.WaitAsync(() => Text(view, CustomersSuppliersView.XeroLinkStateName).StartsWith("Linked to Acme Engineering Ltd in Xero", StringComparison.Ordinal) && !view.IsXeroBusy);

        // BRUNL's own look-up succeeds; the Xero section's look-up of it faults.
        var brunelLookUps = 0;
        control.Intercept = (method, args) =>
            method.Name == nameof(IOrganisationCatalog.FindAsync) && Equals(args![0], "BRUNL") && ++brunelLookUps > 1
                ? Task.FromException<IReferenceRecord<Organisation>?>(new IOException("The organisation store is unreadable."))
                : null;

        await view.SelectAsync("BRUNL");
        await kit.WaitAsync(() => Text(view, CustomersSuppliersView.XeroStatusName).StartsWith("Could not read the Xero link", StringComparison.Ordinal) && !view.IsXeroBusy);

        Assert.Equal("BRUNL", view.EditingRecordId);
        Assert.True(Control(view, CustomersSuppliersView.XeroStatusName).IsEffectivelyVisible);
        Assert.DoesNotContain("Acme", Text(view, CustomersSuppliersView.XeroLinkStateName), StringComparison.Ordinal);
        Assert.False(Button(view, CustomersSuppliersView.UnlinkFromXeroName).IsEffectivelyVisible);
        Assert.False(Button(view, CustomersSuppliersView.LinkToXeroName).IsEffectivelyVisible);
        Assert.True(Button(view, CustomersSuppliersView.RefreshFromXeroName).IsEffectivelyVisible);

        // Retrying once the store reads again renders BRUNL.
        control.Intercept = null;
        Click(view, CustomersSuppliersView.RefreshFromXeroName);
        await kit.WaitAsync(() => Text(view, CustomersSuppliersView.XeroLinkStateName).StartsWith("Linked to Brunel Fabrication Ltd in Xero", StringComparison.Ordinal) && !view.IsXeroBusy);
        window.Close();
    }

    private static async Task LinkBothAsync(ContactLinkTestKit kit)
    {
        await kit.AddOrganisationAsync();
        await kit.AddOrganisationAsync(reference: "BRUNL", name: "Brunel Fabrication Ltd", customerCode: "BRUNL");
        await kit.Linker.LinkExistingAsync("ACME1", kit.Simulator.SeedContact("Acme Engineering Ltd", contactNumber: "ACME1"));
        await kit.Linker.LinkExistingAsync("BRUNL", kit.Simulator.SeedContact("Brunel Fabrication Ltd", contactNumber: "BRUNL"));
    }

    private static async Task<(Window Window, CustomersSuppliersView View)> ShowAsync(ContactLinkTestKit kit, IContactCatalog contacts, IOrganisationCatalog organisations)
    {
        var view = new CustomersSuppliersView(organisations, contacts) { XeroContacts = kit.Linker };
        var window = new Window { Content = view, Width = 1200, Height = 900 };
        window.Show();
        await view.RefreshAsync();
        return (window, view);
    }

    /// <summary>The kit's contact catalogue, whose <see cref="IContactCatalog.FindForOrganisationAsync"/> can be held once.</summary>
    private static (IContactCatalog Contacts, ContactHold Hold) HeldContacts(ContactLinkTestKit kit)
    {
        var (contacts, control) = InterceptingProxy<IContactCatalog>.Wrap(kit.Contacts);
        var hold = new ContactHold(kit.Contacts);
        control.Intercept = (method, args) => method.Name == nameof(IContactCatalog.FindForOrganisationAsync) ? hold.Find((string)args![0]!) : null;
        return (contacts, hold);
    }

    private sealed class ContactHold(IContactCatalog inner)
    {
        private TaskCompletionSource? _gate;

        public bool Entered { get; private set; }

        public TaskCompletionSource Arm()
        {
            Entered = false;
            _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _gate;
        }

        public async Task<IReadOnlyList<IReferenceRecord<Contact>>> Find(string organisationReference)
        {
            if (Interlocked.Exchange(ref _gate, null) is { } gate)
            {
                Entered = true;
                await gate.Task.ConfigureAwait(true);
            }

            return await inner.FindForOrganisationAsync(organisationReference).ConfigureAwait(true);
        }
    }

    /// <summary>A linker whose search answers at once with no candidates and whose create waits for <see cref="Release"/>.</summary>
    private sealed class GatedLinker : IXeroContactLinker
    {
        private readonly TaskCompletionSource<ConnectorResult<XeroLink>> _create = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release(ConnectorResult<XeroLink> result) => _create.SetResult(result);

        public Task<XeroLink?> FindLinkAsync(string organisationReference, CancellationToken cancellationToken = default) =>
            Task.FromResult<XeroLink?>(null);

        public Task<ConnectorResult<IReadOnlyList<XeroContactCandidate>>> FindCandidatesAsync(string organisationReference, CancellationToken cancellationToken = default) =>
            Task.FromResult(ConnectorResult<IReadOnlyList<XeroContactCandidate>>.Ok([]));

        public Task<ConnectorResult<XeroLink>> LinkExistingAsync(string organisationReference, string contactId, CancellationToken cancellationToken = default) =>
            _create.Task;

        public Task<ConnectorResult<XeroLink>> CreateAsync(string organisationReference, CancellationToken cancellationToken = default) =>
            _create.Task;

        public Task<ConnectorResult<XeroContactDetails>> ReadDetailsAsync(string organisationReference, CancellationToken cancellationToken = default) =>
            Task.FromResult(ConnectorResult<XeroContactDetails>.Unavailable("unused"));
    }
}

/// <summary>A <see cref="DispatchProxy"/> over <typeparamref name="T"/> that forwards every call to its target unless <see cref="Intercept"/> answers it.</summary>
/// <typeparam name="T">The interface proxied.</typeparam>
public class InterceptingProxy<T> : DispatchProxy
    where T : class
{
    private T? _target;

    /// <summary>Answers a call instead of the target (non-null), or lets it through (null).</summary>
    public Func<MethodInfo, object?[]?, object?>? Intercept { get; set; }

    /// <summary>A proxy over <paramref name="target"/>, and its control.</summary>
    public static (T Proxy, InterceptingProxy<T> Control) Wrap(T target)
    {
        var proxy = Create<T, InterceptingProxy<T>>();
        var control = (InterceptingProxy<T>)(object)proxy;
        control._target = target;
        return (proxy, control);
    }

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        if (Intercept?.Invoke(targetMethod, args) is { } answer)
            return answer;

        try
        {
            return targetMethod.Invoke(_target, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is { } inner)
        {
            ExceptionDispatchInfo.Capture(inner).Throw();
            throw;
        }
    }
}
