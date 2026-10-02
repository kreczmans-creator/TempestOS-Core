using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessOperations;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.DependencyInjection;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Quotes;
using Tempest.Core.Persistence;
using Tempest.Core.Quotations;
using Tempest.Core.Secrets;
using Tempest.Core.Settings;
using Tempest.Core.Tests.BusinessOperations;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Quotations;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Quotes;

/// <summary>
/// `v0.24.0` X3 over a real workspace: a quotation written, approved by a
/// second person, exported, sent and accepted through
/// <see cref="IQuotationService"/> is read by <see cref="DomainXeroQuoteSource"/>
/// and followed in Xero (simulator) step by step; and the
/// <c>RegisterQuotes</c> hook's services resolve from a container.
/// </summary>
public sealed class XeroQuoteDomainJourneyTests
{
    [Fact]
    public async Task AQuotationWrittenApprovedSentAndAccepted_InTempestOs_IsFollowedInXero()
    {
        using var temp = new TempDirectory();
        var (host, _) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);
        var quotations = QuotationTestHost.Quotations(host);
        var principals = QuotationTestHost.Principals(host);
        var organisations = (OrganisationCatalog)QuotationTestHost.Organisations(host);
        await OperationsFixtures.RegisterAsync(organisations, QuoteSyncTestKit.ClientReference, new Organisation
        {
            Reference = QuoteSyncTestKit.ClientReference,
            Name = "Acme Engineering Ltd",
            Status = RelationshipStatus.Active,
            Roles = [PartyKind.Customer],
        });

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "P0012", "Bracket programme");
        var quoteId = (await quotations.CreateAsync(projectId, "P0012-Q-001", QuoteSyncTestKit.ClientReference)).Quotation!.Id;
        Assert.True((await quotations.AddLineAsync(quoteId, "Concept design", 12m, new Money(95m, CurrencyCode.Gbp), null, vatRate: VatRate.Standard)).Succeeded);
        Assert.True((await quotations.AddLineAsync(quoteId, "Drawing pack", null, null, new Money(1_200m, CurrencyCode.Gbp), vatRate: VatRate.Zero)).Succeeded);

        var source = new DomainXeroQuoteSource(QuotationTestHost.Domain(host), organisations);
        using var kit = await QuoteSyncTestKit.CreateAsync(source);

        // Draft: nothing.
        Assert.Empty(await kit.PlanAsync(quoteId));

        // Approved R1 and exported → DRAFT in Xero, same number, lines and PDF.
        await QuotationReviewTestSupport.SubmitAndApproveAsync(quotations, principals, quoteId);
        kit.Files.Store(quoteId, "R1 sheet");
        await kit.PlanAsync(quoteId);
        await kit.DrainAsync();

        var quote = kit.OnlyQuote;
        Assert.Equal("DRAFT", quote.Status);
        Assert.Equal("P0012-Q-001", quote.Number);
        Assert.Equal("R1", quote.Body["Reference"]!.GetValue<string>());
        Assert.Equal("P0012 Bracket programme", quote.Body["Summary"]!.GetValue<string>());
        var lines = quote.Body["LineItems"]!.AsArray();
        Assert.Equal(12m, lines[0]!["Quantity"]!.GetValue<decimal>());
        Assert.Equal(95m, lines[0]!["UnitAmount"]!.GetValue<decimal>());
        Assert.Equal("OUTPUT2", lines[0]!["TaxType"]!.GetValue<string>());
        Assert.Equal(1m, lines[1]!["Quantity"]!.GetValue<decimal>());
        Assert.Equal(1_200m, lines[1]!["UnitAmount"]!.GetValue<decimal>());
        Assert.Equal("ZERORATEDOUTPUT", lines[1]!["TaxType"]!.GetValue<string>());
        Assert.Single(quote.Attachments);

        // Sent → SENT; Accepted → ACCEPTED.
        Assert.True((await quotations.SendAsync(quoteId)).Succeeded);
        await kit.PlanAsync(quoteId);
        await kit.DrainAsync();
        Assert.Equal("SENT", kit.OnlyQuote.Status);

        Assert.True((await quotations.AcceptAsync(quoteId)).Succeeded);
        await kit.PlanAsync(quoteId);
        await kit.DrainAsync();
        Assert.Equal("ACCEPTED", kit.OnlyQuote.Status);

        Assert.Empty(await kit.PlanAsync(quoteId));
        Assert.Contains(quoteId, await source.ListIdsAsync());
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task TheRegisterQuotesHook_RegistersEachServiceOnce_AndTheyResolve()
    {
        using var temp = new TempDirectory();
        var (host, _) = await QuotationTestHost.StartAsync(temp.Path);
        using var kit = await QuoteSyncTestKit.CreateAsync();

        var services = new ServiceCollection();
        services.AddInstance(QuotationTestHost.Domain(host));
        services.AddInstance(QuotationTestHost.Organisations(host));
        services.AddInstance(kit.Api);
        services.AddInstance<IXeroLinkStore>(kit.Links);
        services.AddInstance<IXeroOutbox>(kit.Outbox);
        services.AddInstance<IPersistenceStore>(kit.Store);
        services.AddInstance<ISecretStore>(kit.Secrets);
        services.AddInstance(kit.Linker);
        services.AddInstance<IXeroSettingsReader>(kit.Settings);
        services.AddInstance<ISettingsProvider>(kit.SettingsProvider);
        services.Singleton<XeroTaxTypeResolver>();
        services.Singleton<XeroAccountCodeMap>();
        XeroServiceRegistration.AddXeroQuotes(services);
        var provider = new TempestServiceProvider(services);

        Assert.IsType<DomainXeroQuoteSource>(provider.GetService(typeof(IXeroQuoteSource)));
        var planner = (XeroQuotePlanner)provider.GetService(typeof(XeroQuotePlanner));
        Assert.Equal(XeroDocumentKind.Quote, planner.Kind);
        Assert.Equal(Quotation.CanonicalKind, planner.CanonicalKind);
        Assert.Equal(
            [XeroOperation.PushQuote, XeroOperation.SetQuoteStatus],
            ((XeroQuotePushHandler)provider.GetService(typeof(XeroQuotePushHandler))).Operations);
        var attachments = (XeroQuoteAttachmentHandler)provider.GetService(typeof(XeroQuoteAttachmentHandler));
        Assert.Equal([XeroOperation.UploadAttachment], attachments.Operations);
        Assert.Equal(XeroDocumentKind.Quote, attachments.DocumentKind);

        Assert.ThrowsAny<ServiceRegistrationException>(() => XeroServiceRegistration.AddXeroQuotes(services));
    }
}
