using System.Globalization;
using System.IO;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Quotations;
using Tempest.Workspace.Projects;

namespace Tempest.Desktop.Quotations;

/// <summary>
/// The one place a <see cref="Quotation"/> becomes a
/// <see cref="QuotationSheetModel"/>, and the one place its export start
/// folder is found — shared by the project's Quote tab
/// (<c>ProjectQuoteView</c>) and Business → Quotes (<c>QuotesView</c>), so
/// the two exports cannot drift apart again (colour review board B4: the
/// Quotes list printed <c>Status.ToString()</c>, "InReview" with no Rn,
/// and opened no project folder).
/// </summary>
public static class QuotationSheetModelBuilder
{
    /// <summary>The sheet model for <paramref name="quote"/>, as both quote views export and send it.</summary>
    public static async Task<QuotationSheetModel> BuildAsync(
        Quotation quote, EngineeringDomainContext domainContext, IOrganisationCatalog organisations,
        string issuerName, string applicationVersionText, DateTimeOffset generatedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(quote);
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(organisations);

        var (projectCode, projectName) = await ResolveProjectAsync(domainContext, quote.ParentId, cancellationToken).ConfigureAwait(true);
        var clientName = await ResolveClientNameAsync(organisations, quote.ClientOrganisationId, cancellationToken).ConfigureAwait(true);

        var lines = quote.Lines.Select(l => new QuotationSheetLineRow(
            l.Description,
            l.Basis == QuotationLineBasis.Hourly ? l.Hours?.ToString("0.##", CultureInfo.InvariantCulture) : null,
            l.Basis == QuotationLineBasis.Hourly ? MoneyDisplay.Format(l.Rate!.Value) : null,
            MoneyDisplay.Format(l.Amount))).ToList();

        return new QuotationSheetModel(
            IssuerName: issuerName,
            ProjectCode: projectCode,
            ProjectName: projectName,
            Client: clientName,
            Reference: quote.Reference,
            QuoteDate: quote.QuoteDate,
            ValidityDays: quote.ValidityDays,
            Currency: quote.Currency.ToString(),
            Lines: lines,
            Total: MoneyDisplay.Format(quote.Total),
            Terms: quote.Terms,
            Status: QuotationExport.StatusText(quote),
            GeneratedAtUtc: generatedAtUtc,
            ApplicationVersionText: applicationVersionText,
            Revision: QuotationExport.RevisionText(quote));
    }

    /// <summary>The client's own name, "(none)" when unset, or the id as recorded when the catalogue has no such organisation.</summary>
    public static async Task<string> ResolveClientNameAsync(
        IOrganisationCatalog organisations, string? clientOrganisationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organisations);

        if (string.IsNullOrWhiteSpace(clientOrganisationId))
            return "(none)";

        var found = await organisations.FindAsync(clientOrganisationId, cancellationToken).ConfigureAwait(true);
        return found?.Definition.Name ?? clientOrganisationId;
    }

    /// <summary>
    /// The project folder (or its quote subfolder) an export of a quote on
    /// <paramref name="projectId"/> starts in — <see langword="null"/>
    /// whenever there is none (no locator, no project, generation off, or a
    /// refusing file system), never an exception.
    /// </summary>
    public static async Task<string?> StartFolderAsync(
        ProjectFolderLocator? folders, Guid? projectId, CancellationToken cancellationToken = default)
    {
        if (folders is null || projectId is not { } id)
            return null;

        try
        {
            return await folders.QuoteFolderForAsync(id, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }

    private static async Task<(string Code, string Name)> ResolveProjectAsync(
        EngineeringDomainContext domainContext, Guid? projectId, CancellationToken cancellationToken)
    {
        if (projectId is not { } id || await domainContext.Repository.FindAsync(id, cancellationToken).ConfigureAwait(true) is not { } project)
            return (string.Empty, string.Empty);

        return ((project as IHasBusinessIdentifier)?.Identifier ?? string.Empty, (project as IHasBusinessIdentifier)?.DisplayName ?? string.Empty);
    }
}
