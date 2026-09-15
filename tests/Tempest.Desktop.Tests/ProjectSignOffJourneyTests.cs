using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Audit;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Quotations;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The Sign off tab's own `WP 20.10E` journey (Product Owner finding D18):
/// accept a two-line quote, complete one of the two deliverables it
/// creates, Sign off refused naming the other, <b>Raise change order</b>
/// carries it, Sign off then succeeds — with an audit row for every act
/// along the way.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ProjectSignOffJourneyTests
{
    [AvaloniaFact]
    public async Task SignOff_RefusedWithTheOtherDeliverableNamed_RaiseChangeOrderCarriesIt_ThenSignOffSucceeds()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);

        try
        {
            await host.StartAsync();
            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            var navigator = host.ShellNavigator!;
            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            var quotations = (IQuotationService)host.Services!.GetService(typeof(IQuotationService));
            var deliverables = (IDeliverableService)host.Services!.GetService(typeof(IDeliverableService));
            var auditQuery = (IAuditQuery)host.Services!.GetService(typeof(IAuditQuery));

            var project = await host.ProjectDirectory!.CreateAsync("P-SIGNOFF", "Sign Off Journey Project");

            // ---- Accept a two-line quote: two deliverables ----
            var quote = await quotations.CreateAsync(project.Id);
            Assert.True(quote.Succeeded, quote.Reason);
            await quotations.AddLineAsync(quote.Quotation!.Id, "Kept deliverable", 5m, new Money(100m, quote.Quotation.Currency), null);
            await quotations.AddLineAsync(quote.Quotation.Id, "Carried deliverable", null, null, new Money(1_000m, quote.Quotation.Currency));
            Assert.True((await quotations.SendAsync(quote.Quotation.Id)).Succeeded);
            var accepted = await quotations.AcceptAsync(quote.Quotation.Id);
            Assert.True(accepted.Succeeded, accepted.Reason);
            Assert.Equal(2, accepted.Quotation!.Lines.Count);

            var keptLine = accepted.Quotation.Lines.Single(l => l.Description == "Kept deliverable");
            var carriedLine = accepted.Quotation.Lines.Single(l => l.Description == "Carried deliverable");

            // ---- Complete one of the two ----
            var completion = await deliverables.CompleteAsync(keptLine.DeliverableId!.Value, project.Id, DateOnly.FromDateTime(DateTime.UtcNow));
            Assert.True(completion.Succeeded, completion.Reason);

            // ---- Enter the Sign off tab the way the application does ----
            await navigator.OpenProjectAsync(project.Id, ProjectArea.SignOff).ConfigureAwait(true);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var projectWorkspace = GetPrivateField<ProjectWorkspaceView>(window, "_projectWorkspace");
            var signOffView = projectWorkspace.SignOffView;

            await RenderUntilAsync(window, () =>
                signOffView.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).Contains("still open against the quote", StringComparison.Ordinal)));
            LayOut(window);

            Assert.Contains(
                signOffView.GetLogicalDescendants().OfType<TextBlock>(),
                t => (t.Text ?? string.Empty).Contains("Carried deliverable", StringComparison.Ordinal));
            Assert.DoesNotContain(
                signOffView.GetLogicalDescendants().OfType<TextBlock>(),
                t => (t.Text ?? string.Empty).Contains("Kept deliverable", StringComparison.Ordinal));

            // ---- Sign off is refused, naming the other deliverable ----
            var statementBox = signOffView.GetLogicalDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "Sign-off statement");
            statementBox.Text = "Delivered; remaining scope carried by change order.";
            var signOffButton = signOffView.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Sign off"));
            signOffButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var statusAfterRefusal = GetPrivateField<TextBlock>(signOffView, "_status");
            await RenderUntilAsync(window, () => (statusAfterRefusal.Text ?? string.Empty).Contains("Carried deliverable", StringComparison.Ordinal));
            LayOut(window);

            Assert.Contains("Carried deliverable", statusAfterRefusal.Text ?? string.Empty, StringComparison.Ordinal);
            Assert.Contains("change order", statusAfterRefusal.Text ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.Null(((Project)(await domain.Repository.FindAsync(project.Id))!).ClosedOn);

            // ---- Raise change order — carries the open deliverable, opens the Quote tab ----
            var raiseButton = signOffView.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Raise change order…"));
            raiseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Quotation? changeOrder = null;
            await RenderUntilAsync(window, () =>
            {
                var childEntries = domain.Repository.ListChildrenAsync(project.Id).GetAwaiter().GetResult();
                changeOrder = domain.Repository.MaterialiseAsync<Quotation>(childEntries).GetAwaiter().GetResult()
                    .FirstOrDefault(q => q.QuotationKind == QuotationKind.ChangeOrder);
                return changeOrder is not null;
            });
            Assert.NotNull(changeOrder);
            Assert.StartsWith("CO-", changeOrder!.Reference, StringComparison.Ordinal);
            Assert.Contains(changeOrder.Lines, l => l.DeliverableId == carriedLine.DeliverableId);

            // Raise change order opened the Quote tab, right up, on the new change order.
            await RenderUntilAsync(window, () =>
                navigator.Current is { Area: ShellArea.ProjectWorkspace, ProjectArea: ProjectArea.Quote } location && location.ProjectId == project.Id);

            // ---- Back on Sign off: the item now reads "carried by CO-..." and sign-off succeeds ----
            await navigator.OpenProjectAsync(project.Id, ProjectArea.SignOff).ConfigureAwait(true);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            signOffView = projectWorkspace.SignOffView;

            await RenderUntilAsync(window, () =>
                signOffView.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).Contains(changeOrder.Reference, StringComparison.Ordinal)));
            LayOut(window);
            Assert.Contains(
                signOffView.GetLogicalDescendants().OfType<TextBlock>(),
                t => (t.Text ?? string.Empty).Contains($"carried by {changeOrder.Reference}", StringComparison.Ordinal));

            var statementBoxAgain = signOffView.GetLogicalDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "Sign-off statement");
            statementBoxAgain.Text = "Delivered; remaining scope carried by change order.";
            var signOffButtonAgain = signOffView.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Sign off"));
            signOffButtonAgain.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await RenderUntilAsync(window, () => domain.Repository.FindAsync(project.Id).GetAwaiter().GetResult() is Project { ClosedOn: not null });
            var closedProject = (Project)(await domain.Repository.FindAsync(project.Id))!;
            Assert.NotNull(closedProject.ClosedOn);
            Assert.NotNull(closedProject.SignOff);

            // ---- An audit row for every act along the way ----
            var deliverableAudit = await auditQuery.QueryAsync(new AuditQueryCriteria(objectId: keptLine.DeliverableId!.Value));
            Assert.NotEmpty(deliverableAudit);
            var completionAudit = await auditQuery.QueryAsync(new AuditQueryCriteria(objectId: completion.Completion!.Id));
            Assert.NotEmpty(completionAudit);
            var changeOrderAudit = await auditQuery.QueryAsync(new AuditQueryCriteria(objectId: changeOrder.Id));
            Assert.True(changeOrderAudit.Count >= 2, "Expected an audit row for the change order's own create and its carried line.");
            var projectAudit = await auditQuery.QueryAsync(new AuditQueryCriteria(objectId: project.Id));
            Assert.Contains(projectAudit, a => a.Detail.GetValueOrDefault("Detail", string.Empty).Contains("Signed off", StringComparison.Ordinal));

            await host.ShutdownAsync();
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found on {instance.GetType().Name}.");
        return (T)field.GetValue(instance)!;
    }

    private static async Task RenderUntilAsync(MainWindow window, Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            LayOut(window);
        }
    }

    private static void LayOut(Window window)
    {
        if (!window.IsVisible)
            window.Show();

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Size(1400, 900));
            window.Arrange(new Rect(0, 0, 1400, 900));
        }
    }
}
