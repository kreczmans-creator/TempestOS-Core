using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Projects;
using Tempest.Core.Quotations;
using Tempest.Desktop.Quotations;
using Tempest.Desktop.Views;
using Tempest.Workspace.Projects;

namespace Tempest.Desktop.Tests.Quotations;

/// <summary>
/// Colour review board B4: Business → Quotes exports exactly as the
/// project's own Quote tab does — the project's quote folder as the start
/// folder, "In review" (not "InReview") and DRAFT before approval, then
/// "Approved R1" and R1 after it — through the one shared
/// <see cref="QuotationSheetModelBuilder"/>.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class QuotesViewExportTests
{
    [AvaloniaFact]
    public async Task Export_StartsInTheProjectsQuoteFolder_AndPrintsTheStatusAsAPersonReadsIt_WithItsRevision()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        var folderRoot = Path.Combine(Path.GetTempPath(), $"tempest-quotes-view-folders-{Guid.NewGuid():N}");
        var exports = new List<string>();
        Window? window = null;
        try
        {
            await host.StartAsync();
            var quotations = Resolve<IQuotationService>(host);
            var organisations = Resolve<IOrganisationCatalog>(host);
            var project = await host.ProjectDirectory!.CreateAsync("P-QV-EXPORT", "Quotes View Export Project");
            var created = await quotations.CreateAsync(project.Id);
            Assert.True(created.Succeeded, created.Reason);
            var quoteId = created.Quotation!.Id;
            Assert.True((await quotations.AddLineAsync(quoteId, "Survey", null, null, new Money(500m, CurrencyCode.Gbp))).Succeeded);
            Assert.True((await quotations.SubmitForReviewAsync(quoteId)).Succeeded);
            var reference = created.Quotation.Reference;

            var filePicker = new StubFilePicker();
            var view = new QuotesView(
                Resolve<EngineeringDomainContext>(host), Resolve<ICommandDispatcher>(host), () => project.Id, organisations,
                host.ProjectDirectory!, new ProjectPicker(host.ProjectDirectory!), filePicker, new QuotationSheetRenderer(),
                () => "Issuer", () => "TempestOS test", (_, _) => { })
            {
                ProjectFolders = new ProjectFolderLocator(
                    new ProjectFolderService(new ProjectFolderOptions(folderRoot, [], "Quotes")), host.ProjectDirectory!, organisations),
            };
            window = new Window { Width = 1400, Height = 900, Content = view };
            window.Show();
            await view.RefreshAsync();
            Dispatcher.UIThread.RunJobs();

            // ---- In review: the project's quote folder, "In review", DRAFT ----
            var inReview = await ExportAsync(view, filePicker, reference, exports);
            var expectedFolder = Path.Combine(folderRoot, ProjectFolderService.NoCustomerFolderName, "P-QV-EXPORT", "Quotes");
            Assert.Equal(expectedFolder, filePicker.SaveRequests[^1].StartFolder);
            Assert.Equal($"{reference}-DRAFT-quote.pdf", filePicker.SaveRequests[^1].SuggestedFileName);
            Assert.Contains("Status: In review", inReview, StringComparison.Ordinal);
            Assert.DoesNotContain("InReview", inReview, StringComparison.Ordinal);

            // ---- Approved by a second person: "Approved R1", R1 ----
            var approved = await QuotationReviewSupport.AsReviewerAsync(host, () => quotations.ApproveAsync(quoteId));
            Assert.True(approved.Succeeded, approved.Reason);
            await view.RefreshAsync();
            Dispatcher.UIThread.RunJobs();

            var r1 = await ExportAsync(view, filePicker, reference, exports);
            Assert.Equal($"{reference}-R1-quote.pdf", filePicker.SaveRequests[^1].SuggestedFileName);
            Assert.Contains("Status: Approved R1", r1, StringComparison.Ordinal);
            Assert.Contains("Revision: R1", r1, StringComparison.Ordinal);
        }
        finally
        {
            try
            {
                window?.Close();
                Dispatcher.UIThread.RunJobs();
            }
            finally
            {
                await host.ShutdownAsync();
                await host.DisposeAsync();
                foreach (var path in exports)
                    TryDelete(() => File.Delete(path));
                TryDelete(() => Directory.Delete(folderRoot, recursive: true));
            }
        }
    }

    private static async Task<string> ExportAsync(QuotesView view, StubFilePicker filePicker, string reference, List<string> exports)
    {
        var path = Path.Combine(Path.GetTempPath(), $"quotes-view-export-{Guid.NewGuid():N}.pdf");
        exports.Add(path);
        filePicker.SetNextSavePath(path);

        view.GetLogicalDescendants().OfType<Button>()
            .First(b => AutomationProperties.GetName(b) == $"Export {reference}")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        var deadline = DesktopTestHelpers.Deadline(15);
        while (!ExportIsComplete(path) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(ExportIsComplete(path), "The export never completed.");
        return PdfTextExtractor.ExtractText(await File.ReadAllBytesAsync(path));
    }

    private static bool ExportIsComplete(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            using var exclusive = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return exclusive.Length > 0;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void TryDelete(Action delete)
    {
        try
        {
            delete();
        }
        catch (IOException)
        {
            // Best-effort cleanup only.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup only.
        }
    }

    private static T Resolve<T>(WorkspaceHost host) where T : class => (T)host.Services!.GetService(typeof(T));
}
